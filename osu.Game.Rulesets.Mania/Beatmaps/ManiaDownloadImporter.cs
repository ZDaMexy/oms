// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.IO;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Rulesets.Mania.Beatmaps
{
    /// <summary>
    /// Publishes an authorised mirror download through the existing filesystem-backed mania importer.
    /// The requested original IDs are checked before any directory is added to the library.
    /// </summary>
    public sealed class ManiaDownloadImporter : IManiaDownloadImporter
    {
        private const long max_archive_bytes = 2L * 1024 * 1024 * 1024;
        private const long max_file_bytes = 2L * 1024 * 1024 * 1024;
        private const long max_expanded_bytes = 8L * 1024 * 1024 * 1024;
        private const long max_chart_bytes = 32L * 1024 * 1024;
        private const long max_total_chart_bytes = 128L * 1024 * 1024;
        private const uint max_central_directory_bytes = 32 * 1024 * 1024;
        private const int max_entries = 50_000;

        private static readonly Encoding fallback_encoding;
        private static readonly Encoding strict_utf8 = new UTF8Encoding(false, true);
        private static readonly uint[] crc_table = createCrcTable();

        private readonly Storage storage;
        private readonly ManiaFolderImporter folderImporter;

        static ManiaDownloadImporter()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            fallback_encoding = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        public ManiaDownloadImporter(Storage storage, RealmAccess realm)
        {
            this.storage = storage;
            folderImporter = new ManiaFolderImporter(storage, realm);
        }

        public async Task<IReadOnlyList<ManiaDownloadImportedBeatmap>> ImportAsync(string archivePath, int expectedSetId, int requestedBeatmapId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedSetId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedBeatmapId);

            string fullArchivePath = Path.GetFullPath(archivePath);
            string taskRoot = Path.GetDirectoryName(fullArchivePath) ?? throw new InvalidDataException("The download has no task directory.");
            string downloadsRoot = Path.TrimEndingDirectorySeparator(storage.GetFullPath("mania-downloads"));

            if (!string.Equals(Path.GetDirectoryName(taskRoot), downloadsRoot, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(Path.GetFileName(taskRoot), out _))
                throw new InvalidDataException("The archive must be inside its exclusive mania download task directory.");

            ensureNoReparsePoints(fullArchivePath);
            string extractionOwner = Path.Combine(taskRoot, $"extraction-{Guid.NewGuid():N}");
            string extractionRoot = Path.Combine(extractionOwner, $"set-{expectedSetId}");

            if (Directory.Exists(extractionOwner) || File.Exists(extractionOwner))
                throw new IOException("The download extraction directory is already occupied.");

            Directory.CreateDirectory(extractionRoot);

            try
            {
                (string Path, string Md5) target;
                try
                {
                    target = await Task.Run(() => extractAndValidate(fullArchivePath, extractionRoot, expectedSetId, requestedBeatmapId, cancellationToken), cancellationToken).ConfigureAwait(false);
                }
                catch (EndOfStreamException exception)
                {
                    throw new InvalidDataException("The downloaded ZIP is incomplete.", exception);
                }
                cancellationToken.ThrowIfCancellationRequested();
                ensureNoReparsePoints(storage.GetFullPath(ManiaFolderImporter.MANIA_STORAGE_PATH));

                // One .osz is one song directory. A wrapper or unrelated sibling directory receives no publication authority.
                var result = await folderImporter.Import(new ImportTask(Path.GetDirectoryName(target.Path)!), cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.ImportedBeatmapSet == null)
                    throw new InvalidDataException("The requested mania difficulty could not be added to the library.");

                var imported = result.ImportedBeatmapSet.PerformRead(set => set.Beatmaps.Select(beatmap =>
                    (Beatmap: new ManiaDownloadImportedBeatmap(beatmap.ID, beatmap.OnlineID, beatmap.MD5Hash),
                     Path: storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, beatmap.LocalFilePath!)))).ToArray());
                var requested = imported.SingleOrDefault(beatmap => beatmap.Beatmap.OnlineId == requestedBeatmapId && beatmap.Beatmap.Md5 == target.Md5);

                if (requested.Beatmap == null)
                    throw new InvalidDataException("The library did not retain the requested original mania difficulty.");

                // Reuse must not turn stale database metadata into a successful installation or overwrite a player's file.
                ensureNoReparsePoints(requested.Path);
                using var source = new FileStream(requested.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous);
                string actualMd5 = Convert.ToHexString(await MD5.HashDataAsync(source, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();

                if (actualMd5 != target.Md5)
                    throw new InvalidDataException("An existing library copy no longer contains the requested original mania difficulty.");

                return imported.Select(beatmap => beatmap.Beatmap).ToArray();
            }
            finally
            {
                // Only the extraction directory owned by this invocation is removed; committed songs remain intact.
                Directory.Delete(extractionOwner, true);
            }
        }

        private static (string Path, string Md5) extractAndValidate(string archivePath, string extractionRoot, int expectedSetId, int requestedBeatmapId, CancellationToken cancellationToken)
        {
            using var source = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entries = readZipMetadata(source, cancellationToken);
            source.Position = 0;
            using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: fallback_encoding);

            if (zip.Entries.Count != entries.Count)
                throw new InvalidDataException("The downloaded ZIP has inconsistent entry metadata.");

            byte[] buffer = new byte[128 * 1024];
            long expanded = 0;
            var chartFiles = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadata = entries[i];
                var entry = zip.Entries[i];

                if (entry.FullName != metadata.Name || entry.Length != metadata.Size || entry.CompressedLength != metadata.CompressedSize)
                    throw new InvalidDataException("The downloaded ZIP has inconsistent entry metadata.");

                if (metadata.Directory)
                    continue;

                string destination = Path.Combine(extractionRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long fileBytes = 0;
                uint crc = uint.MaxValue;
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (read > metadata.Size - fileBytes || read > max_expanded_bytes - expanded)
                        throw new InvalidDataException("The downloaded ZIP exceeds its actual expanded size limit.");

                    output.Write(buffer, 0, read);
                    fileBytes += read;
                    expanded += read;
                    for (int b = 0; b < read; b++)
                        crc = crc_table[(crc ^ buffer[b]) & 0xff] ^ (crc >> 8);
                }

                if (fileBytes != metadata.Size || ~crc != metadata.Crc)
                    throw new InvalidDataException("The downloaded ZIP contains an incomplete or damaged file.");

                if (metadata.Path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    chartFiles.Add(destination);
            }

            var candidates = chartFiles.Where(path => readOriginalIds(path) == (expectedSetId, requestedBeatmapId)).ToArray();

            if (candidates.Length != 1)
                throw new InvalidDataException("The downloaded package does not uniquely contain the requested original set and difficulty IDs.");

            string targetPath = candidates[0];
            using var chart = File.OpenRead(targetPath);

            if (!OsuFileModeDetector.IsMania(chart))
                throw new InvalidDataException("The requested difficulty is not an original mania chart.");

            chart.Position = 0;
            using (var reader = new LineBufferedReader(chart, true))
            {
                string? magic = reader.PeekLine();

                while (magic != null && string.IsNullOrWhiteSpace(magic))
                {
                    reader.ReadLine();
                    magic = reader.PeekLine();
                }

                if (magic == null || !magic.Trim().StartsWith("osu file format v", StringComparison.Ordinal))
                    throw new InvalidDataException("The requested difficulty has no valid .osu header.");

                IBeatmap decoded;
                try
                {
                    decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                }
                catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
                {
                    throw new InvalidDataException("The requested mania difficulty cannot be decoded.", exception);
                }

                if (decoded.BeatmapInfo.OnlineID != requestedBeatmapId || decoded.BeatmapInfo.BeatmapSet?.OnlineID != expectedSetId
                    || decoded.BeatmapInfo.Ruleset.OnlineID != 3 || decoded.HitObjects.Count == 0)
                    throw new InvalidDataException("The requested original mania difficulty is not playable.");
            }

            chart.Position = 0;
            return (targetPath, Convert.ToHexString(MD5.HashData(chart)).ToLowerInvariant());
        }

        private static (int SetId, int BeatmapId) readOriginalIds(string path)
        {
            using var source = File.OpenRead(path);
            using var reader = new LineBufferedReader(source);
            string? section = null;
            int setId = 0;
            int beatmapId = 0;
            string? rawLine;

            while ((rawLine = reader.ReadLine()) != null)
            {
                string line = rawLine.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                    section = line;

                if (section != "[Metadata]")
                    continue;

                int separator = line.IndexOf(':');
                if (separator < 0)
                    continue;
                string key = line[..separator].Trim();
                if (key == "BeatmapSetID")
                    int.TryParse(line[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out setId);
                else if (key == "BeatmapID")
                    int.TryParse(line[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out beatmapId);
            }

            return (setId, beatmapId);
        }

        private static List<ZipEntryMetadata> readZipMetadata(FileStream source, CancellationToken cancellationToken)
        {
            if (source.Length < 22 || source.Length > max_archive_bytes)
                throw new InvalidDataException("The mania download has an invalid compressed size.");

            byte[] tail = new byte[(int)Math.Min(source.Length, 22 + ushort.MaxValue)];
            source.Position = source.Length - tail.Length;
            source.ReadExactly(tail);
            int end = -1;

            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (readUInt32(tail, i) == 0x06054b50 && i + 22 + readUInt16(tail, i + 20) == tail.Length)
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
                throw new InvalidDataException("The mania download is not a complete ZIP package.");

            int count = readUInt16(tail, end + 10);
            uint centralSize = readUInt32(tail, end + 12);
            uint centralOffset = readUInt32(tail, end + 16);

            if (readUInt16(tail, end + 4) != 0 || readUInt16(tail, end + 6) != 0 || readUInt16(tail, end + 8) != count
                || count == ushort.MaxValue || centralSize == uint.MaxValue || centralOffset == uint.MaxValue)
                throw new InvalidDataException("Split and ZIP64 mania packages are not supported.");

            if (count == 0 || count > max_entries || centralSize > max_central_directory_bytes
                || (long)centralOffset + centralSize != source.Length - tail.Length + end)
                throw new InvalidDataException("The downloaded ZIP exceeds its entry or metadata limit, or is incomplete.");

            var entries = new List<ZipEntryMetadata>(count);
            var names = new Dictionary<string, (string Name, bool Directory, bool Explicit)>(StringComparer.OrdinalIgnoreCase);
            long expanded = 0;
            long chartBytes = 0;
            source.Position = centralOffset;
            long centralEnd = (long)centralOffset + centralSize;

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (centralEnd - source.Position < 46)
                    throw new InvalidDataException("The downloaded ZIP has a truncated central directory.");

                byte[] header = new byte[46];
                source.ReadExactly(header);

                if (readUInt32(header, 0) != 0x02014b50)
                    throw new InvalidDataException("The downloaded ZIP has a damaged central directory.");

                ushort flags = readUInt16(header, 8);
                ushort method = readUInt16(header, 10);
                uint compressed = readUInt32(header, 20);
                uint size = readUInt32(header, 24);
                ushort nameLength = readUInt16(header, 28);
                ushort extraLength = readUInt16(header, 30);
                ushort commentLength = readUInt16(header, 32);
                uint attributes = readUInt32(header, 38);
                uint offset = readUInt32(header, 42);

                if (readUInt16(header, 34) != 0 || readUInt16(header, 6) >= 45
                    || compressed == uint.MaxValue || size == uint.MaxValue || offset == uint.MaxValue)
                    throw new InvalidDataException("Split and ZIP64 mania packages are not supported.");
                if ((flags & (0x0001 | 0x0040 | 0x2000)) != 0)
                    throw new InvalidDataException("Encrypted mania packages are not supported.");
                if (method is not 0 and not 8 || (flags & ~0x080e) != 0 || nameLength == 0 || nameLength > 1024)
                    throw new InvalidDataException("The downloaded ZIP has an unsupported compression method or filename.");
                if (nameLength + extraLength + commentLength > centralEnd - source.Position)
                    throw new InvalidDataException("The downloaded ZIP has truncated filename metadata.");

                byte[] rawName = new byte[nameLength];
                source.ReadExactly(rawName);
                byte[] extra = new byte[extraLength];
                source.ReadExactly(extra);
                rejectZip64Extra(extra);
                source.Position += commentLength;
                string name;

                try
                {
                    name = ((flags & 0x0800) != 0 ? strict_utf8 : fallback_encoding).GetString(rawName);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("The downloaded ZIP has an invalid filename encoding.", exception);
                }

                bool directory = name.EndsWith('/') || name.EndsWith('\\');
                int unixType = (int)(attributes >> 16) & 0xf000;

                if ((attributes & (uint)FileAttributes.ReparsePoint) != 0
                    || (unixType != 0 && unixType != (directory ? 0x4000 : 0x8000))
                    || ((attributes & (uint)FileAttributes.Directory) != 0 && !directory))
                    throw new InvalidDataException("Archive links and special filesystem entries are not allowed.");

                string path = reservePath(name, directory, names);
                if (compressed > max_archive_bytes || size > max_file_bytes || (directory && (size != 0 || compressed != 0)))
                    throw new InvalidDataException("The downloaded ZIP exceeds its file size limit or has an invalid directory.");

                expanded += size;
                if (path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase) && !directory)
                {
                    if (size > max_chart_bytes)
                        throw new InvalidDataException("A downloaded .osu file exceeds its parsing limit.");
                    chartBytes += size;
                }

                if (expanded > max_expanded_bytes || chartBytes > max_total_chart_bytes)
                    throw new InvalidDataException("The downloaded ZIP exceeds its expanded or chart parsing limit.");

                entries.Add(new ZipEntryMetadata(name, path, directory, readUInt16(header, 6), flags, method, readUInt32(header, 16), compressed, size, offset, rawName));
            }

            if (source.Position != centralEnd)
                throw new InvalidDataException("The downloaded ZIP has inconsistent central directory metadata.");

            var ranges = new List<(long Start, long End)>();
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Offset > (long)centralOffset - 30)
                    throw new InvalidDataException("The downloaded ZIP has an invalid local header location.");

                source.Position = entry.Offset;
                byte[] header = new byte[30];
                source.ReadExactly(header);
                ushort nameLength = readUInt16(header, 26);
                ushort extraLength = readUInt16(header, 28);

                if (readUInt32(header, 0) != 0x04034b50 || readUInt16(header, 4) != entry.Version
                    || readUInt16(header, 6) != entry.Flags || readUInt16(header, 8) != entry.Method || nameLength != entry.RawName.Length)
                    throw new InvalidDataException("The downloaded ZIP has inconsistent local entry metadata.");
                if (source.Position + nameLength + extraLength > centralOffset)
                    throw new InvalidDataException("The downloaded ZIP has truncated local filename metadata.");

                byte[] rawName = new byte[nameLength];
                source.ReadExactly(rawName);
                byte[] extra = new byte[extraLength];
                source.ReadExactly(extra);
                rejectZip64Extra(extra);

                if (!rawName.AsSpan().SequenceEqual(entry.RawName))
                    throw new InvalidDataException("The downloaded ZIP has inconsistent local filenames.");

                bool descriptor = (entry.Flags & 8) != 0;
                uint crc = readUInt32(header, 14);
                uint compressed = readUInt32(header, 18);
                uint size = readUInt32(header, 22);

                if ((!descriptor && (crc != entry.Crc || compressed != entry.CompressedSize || size != entry.Size))
                    || (descriptor && ((crc != 0 && crc != entry.Crc) || (compressed != 0 && compressed != entry.CompressedSize) || (size != 0 && size != entry.Size))))
                    throw new InvalidDataException("The downloaded ZIP has inconsistent file size or CRC metadata.");

                long dataEnd = source.Position + entry.CompressedSize;
                if (dataEnd > centralOffset)
                    throw new InvalidDataException("The downloaded ZIP has a truncated entry.");
                ranges.Add((entry.Offset, dataEnd));
            }

            ranges.Sort((first, second) => first.Start.CompareTo(second.Start));
            for (int i = 1; i < ranges.Count; i++)
            {
                if (ranges[i].Start < ranges[i - 1].End)
                    throw new InvalidDataException("The downloaded ZIP has overlapping entries.");
            }

            return entries;
        }

        private static string reservePath(string rawPath, bool directory, Dictionary<string, (string Name, bool Directory, bool Explicit)> names)
        {
            string path = rawPath.Replace('\\', '/');
            if (directory)
                path = path.TrimEnd('/');
            string[] components = path.Split('/');

            if (path.Length > 512 || components.Length > 32)
                throw new InvalidDataException("The downloaded ZIP exceeds its path or depth limit.");

            string current = string.Empty;
            for (int i = 0; i < components.Length; i++)
            {
                string component = components[i];
                string stem = component.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
                bool device = stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$"
                              || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                                                   && stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3');

                if (component.Length == 0 || component.Length > 255 || component is "." or ".." || component.EndsWith('.') || component.EndsWith(' ')
                    || component.Any(character => character < ' ' || character is '<' or '>' or ':' or '"' or '|' or '?' or '*') || device)
                    throw new InvalidDataException($"Archive path '{rawPath}' is not a valid relative Windows path.");

                current = current.Length == 0 ? component : $"{current}/{component}";
                bool isDirectory = i < components.Length - 1 || directory;
                bool explicitEntry = i == components.Length - 1;

                if (names.TryGetValue(current, out var previous))
                {
                    if (previous.Name != current || !previous.Directory || !isDirectory || (explicitEntry && previous.Explicit))
                        throw new InvalidDataException($"Archive path '{rawPath}' duplicates or conflicts with another entry.");
                    if (explicitEntry)
                        names[current] = (current, true, true);
                }
                else
                    names.Add(current, (current, isDirectory, explicitEntry));
            }

            return path;
        }

        private static void rejectZip64Extra(byte[] extra)
        {
            for (int offset = 0; offset < extra.Length;)
            {
                if (extra.Length - offset < 4)
                    throw new InvalidDataException("The downloaded ZIP has malformed extra metadata.");
                ushort tag = readUInt16(extra, offset);
                int length = readUInt16(extra, offset + 2);
                if (tag == 1 || length > extra.Length - offset - 4)
                    throw new InvalidDataException("The downloaded ZIP has unsupported or malformed extra metadata.");
                offset += 4 + length;
            }
        }

        private static void ensureNoReparsePoints(string path)
        {
            for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Mania downloads must not use linked directories or files.");
            }
        }

        private static ushort readUInt16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        private static uint readUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

        private static uint[] createCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }

        private record ZipEntryMetadata(string Name, string Path, bool Directory, ushort Version, ushort Flags, ushort Method, uint Crc,
                                        long CompressedSize, long Size, long Offset, byte[] RawName);
    }
}
