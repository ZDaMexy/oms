// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO.Archives;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Readers;
using ZipArchive = System.IO.Compression.ZipArchive;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Imports an already downloaded package without granting it replacement or source-file deletion authority.
    /// </summary>
    public sealed class BmsDownloadImporter : IBmsDownloadImporter
    {
        private const long max_compressed_bytes = 2L * 1024 * 1024 * 1024;
        private const long max_expanded_bytes = 8L * 1024 * 1024 * 1024;
        private const long max_file_bytes = 2L * 1024 * 1024 * 1024;
        private const int max_entries = 50_000;

        private readonly Storage storage;
        private readonly BmsFolderImporter folderImporter;

        public BmsDownloadImporter(Storage storage, RealmAccess realm)
        {
            this.storage = storage;
            folderImporter = new BmsFolderImporter(storage, realm);
        }

        public async Task<IReadOnlyList<BmsDownloadImportedBeatmap>> ImportAsync(string archivePath, IReadOnlyCollection<string> expectedMd5s, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expectedMd5s.Count == 0 || expectedMd5s.Any(md5 => md5.Length != 32 || !md5.All(char.IsAsciiHexDigit)))
                throw new InvalidDataException("A download must identify the original chart MD5 hashes it is expected to contain.");

            var expected = new HashSet<string>(expectedMd5s, StringComparer.OrdinalIgnoreCase);
            string fullArchivePath = Path.GetFullPath(archivePath);
            string taskRoot = Path.GetDirectoryName(fullArchivePath) ?? throw new InvalidDataException("The download has no task directory.");
            string downloadsRoot = Path.TrimEndingDirectorySeparator(storage.GetFullPath("bms-downloads"));

            if (!string.Equals(Path.GetDirectoryName(taskRoot), downloadsRoot, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(Path.GetFileName(taskRoot), out _))
                throw new InvalidDataException("The archive must be inside its exclusive BMS download task directory.");

            ensureNoReparsePoints(fullArchivePath);

            string extractionRoot = Path.Combine(taskRoot, $"extraction-{Guid.NewGuid():N}");

            if (Directory.Exists(extractionRoot) || File.Exists(extractionRoot))
                throw new IOException("The download extraction directory is already occupied.");

            Directory.CreateDirectory(extractionRoot);

            try
            {
                Dictionary<string, string> chartFiles;

                try
                {
                    chartFiles = await Task.Run(() => extractArchive(fullArchivePath, extractionRoot, cancellationToken), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is ArchiveException or ExtractionException or EndOfStreamException)
                {
                    throw new InvalidDataException("The downloaded BMS archive is damaged or cannot be extracted.", exception);
                }

                if (!expected.IsSubsetOf(chartFiles.Values))
                    throw new InvalidDataException("The downloaded package does not contain all the requested original chart MD5 hashes.");

                var imported = new List<BmsDownloadImportedBeatmap>();

                foreach (string folder in chartFiles.Keys.Select(path => Path.GetDirectoryName(path)!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ensureNoReparsePoints(storage.GetFullPath(BmsFolderImporter.SONGS_STORAGE_PATH));

                    var result = await folderImporter.Import(new ImportTask(folder), cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.ImportedBeatmapSet == null)
                        throw new InvalidDataException($"The downloaded BMS directory '{Path.GetFileName(folder)}' contains no valid playable charts.");

                    var published = result.ImportedBeatmapSet.PerformRead(set => set.Beatmaps.Select(beatmap =>
                        (Beatmap: new BmsDownloadImportedBeatmap(beatmap.ID, beatmap.MD5Hash),
                         Path: storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, beatmap.LocalFilePath!)))).ToArray());

                    foreach (var beatmap in published)
                    {
                        // An existing managed copy may have changed since its last scan. Never report a missing target as installed.
                        if (expected.Contains(beatmap.Beatmap.Md5))
                        {
                            ensureNoReparsePoints(beatmap.Path);
                            using var source = new FileStream(beatmap.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous);
                            string actualMd5 = Convert.ToHexString(await MD5.HashDataAsync(source, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();

                            if (!string.Equals(actualMd5, beatmap.Beatmap.Md5, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("An existing library copy no longer contains the requested original chart.");
                        }

                        imported.Add(beatmap.Beatmap);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!expected.IsSubsetOf(imported.Select(beatmap => beatmap.Md5)))
                    throw new InvalidDataException("The requested charts were present in the package but could not all be added to the library.");

                return imported.DistinctBy(beatmap => beatmap.BeatmapId).ToArray();
            }
            finally
            {
                // This directory was created only for this invocation. Previously committed chartbms folders remain intact.
                Directory.Delete(extractionRoot, true);
            }
        }

        private static Dictionary<string, string> extractArchive(string archivePath, string extractionRoot, CancellationToken cancellationToken)
        {
            using var source = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (source.Length > max_compressed_bytes)
                throw new InvalidDataException("The BMS download exceeds the 2 GiB compressed size limit.");

            if (!ArchiveFactory.IsArchive(source, out var type) || type is not (ArchiveType.Zip or ArchiveType.Rar or ArchiveType.SevenZip))
                throw new InvalidDataException("The download is not a ZIP, RAR or 7z package.");

            using var archive = ArchiveFactory.Open(source, new ReaderOptions
            {
                ArchiveEncoding = ZipArchiveReader.DEFAULT_ENCODING,
                LeaveStreamOpen = true,
            });

            var names = new Dictionary<string, (string Name, bool Directory, bool Explicit)>(StringComparer.OrdinalIgnoreCase);
            var chartFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var buffer = new byte[128 * 1024];
            long expandedBytes = 0;

            if (archive.Type == ArchiveType.Zip)
            {
                var entries = new List<IArchiveEntry>();

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entries.Count == max_entries)
                        throw new InvalidDataException("The BMS download contains more than 50,000 entries.");

                    if (entry.IsEncrypted)
                        throw new InvalidDataException($"Archive entry '{entry.Key}' is encrypted.");

                    entries.Add(entry);
                }

                source.Position = 0;

                // Both readers preserve central-directory order. .NET supplies attributes missing from SharpCompress 0.39;
                // SharpCompress keeps the existing Unicode extra-field, filename-encoding and compression support.
                using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: Encoding.GetEncoding(932));

                if (zip.Entries.Count != entries.Count)
                    throw new InvalidDataException("The ZIP package has inconsistent entry metadata.");

                for (int i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    validateAttributes(zip.Entries[i].ExternalAttributes);
                    string relativePath = reservePath(entry.Key ?? string.Empty, entry.IsDirectory, names);

                    if (entry.Size > max_file_bytes || zip.Entries[i].Length > max_file_bytes)
                        throw new InvalidDataException($"Archive entry '{relativePath}' exceeds the 2 GiB file size limit.");

                    if (entry.IsDirectory)
                        continue;

                    using var input = entry.OpenEntryStream();
                    expandedBytes += extractFile(input, extractionRoot, relativePath, expandedBytes, buffer, chartFiles, cancellationToken);
                }
            }
            else if (archive.Type == ArchiveType.Rar || archive.Type == ArchiveType.SevenZip)
            {
                int entryCount = 0;
                using var reader = archive.ExtractAllEntries();

                while (reader.MoveToNextEntry())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = reader.Entry;

                    if (++entryCount > max_entries)
                        throw new InvalidDataException("The BMS download contains more than 50,000 entries.");

                    if (entry.IsEncrypted || !string.IsNullOrEmpty(entry.LinkTarget) || entry is RarEntry { IsRedir: true })
                        throw new InvalidDataException($"Archive entry '{entry.Key}' is encrypted or a link.");

                    validateAttributes(entry.Attrib ?? 0);
                    string relativePath = reservePath(entry.Key ?? string.Empty, entry.IsDirectory, names);

                    if (entry.Size > max_file_bytes)
                        throw new InvalidDataException($"Archive entry '{relativePath}' exceeds the 2 GiB file size limit.");

                    if (entry.IsDirectory)
                        continue;

                    using var input = reader.OpenEntryStream();
                    expandedBytes += extractFile(input, extractionRoot, relativePath, expandedBytes, buffer, chartFiles, cancellationToken);
                }
            }
            else
                throw new InvalidDataException("BMS downloads must be ZIP, RAR or 7z packages.");

            return chartFiles;
        }

        private static long extractFile(Stream input, string extractionRoot, string relativePath, long expandedBytes, byte[] buffer, Dictionary<string, string> chartFiles, CancellationToken cancellationToken)
        {
            string destination = Path.Combine(extractionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var md5 = BmsImportExtensions.IsBeatmapFile(relativePath) ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) : null;
            long fileBytes = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = input.Read(buffer, 0, buffer.Length);

                if (read == 0)
                    break;

                if (read > max_file_bytes - fileBytes || read > max_expanded_bytes - expandedBytes - fileBytes)
                    throw new InvalidDataException("The BMS download exceeds its file or expanded size limit.");

                output.Write(buffer, 0, read);
                md5?.AppendData(buffer, 0, read);
                fileBytes += read;
            }

            if (md5 != null)
                chartFiles.Add(destination, Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant());

            return fileBytes;
        }

        private static string reservePath(string rawPath, bool isDirectory, Dictionary<string, (string Name, bool Directory, bool Explicit)> names)
        {
            string relativePath = rawPath.Replace('\\', '/');

            if (isDirectory)
                relativePath = relativePath.TrimEnd('/');

            string[] components = relativePath.Split('/');
            string current = string.Empty;

            for (int i = 0; i < components.Length; i++)
            {
                string component = components[i];

                if (component.Length == 0 || component == "." || component == ".."
                    || component.EndsWith('.') || component.EndsWith(' ')
                    || component.Any(c => c < ' ' || c is '<' or '>' or ':' or '"' or '|' or '?' or '*')
                    || isDeviceName(component))
                    throw new InvalidDataException($"Archive path '{rawPath}' is not a valid relative Windows path.");

                current = current.Length == 0 ? component : $"{current}/{component}";
                bool directory = i < components.Length - 1 || isDirectory;
                bool explicitEntry = i == components.Length - 1;

                if (names.TryGetValue(current, out var previous))
                {
                    if (!string.Equals(previous.Name, current, StringComparison.Ordinal)
                        || !previous.Directory || !directory || (explicitEntry && previous.Explicit))
                        throw new InvalidDataException($"Archive path '{rawPath}' duplicates or conflicts with another entry.");

                    if (explicitEntry)
                        names[current] = (current, true, true);
                }
                else
                    names.Add(current, (current, directory, explicitEntry));
            }

            return relativePath;
        }

        private static bool isDeviceName(string name)
        {
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();

            return stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$"
                   || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                                        && stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3');
        }

        private static void validateAttributes(int attributes)
        {
            int unixType = (attributes >> 16) & 0xf000;
            int rarUnixType = attributes & 0xf000;

            if ((attributes & (int)FileAttributes.ReparsePoint) != 0
                || (unixType != 0 && unixType != 0x8000 && unixType != 0x4000)
                || (rarUnixType != 0 && rarUnixType != 0x8000 && rarUnixType != 0x4000))
                throw new InvalidDataException("Archive links and special filesystem entries are not allowed.");
        }

        private static void ensureNoReparsePoints(string path)
        {
            for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("BMS downloads must not use linked directories or files.");
            }
        }
    }
}
