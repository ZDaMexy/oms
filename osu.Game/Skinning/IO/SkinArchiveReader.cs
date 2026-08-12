// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.IO.Archives;

namespace osu.Game.Skinning.IO
{
    internal static class SkinArchiveImportLimits
    {
        public const long MAX_ARCHIVE_BYTES = 512L * 1024 * 1024;
        public const long MAX_CENTRAL_DIRECTORY_BYTES = 4L * 1024 * 1024;
        public const long MAX_METADATA_BYTES = 8L * 1024 * 1024;
        public const int MAX_ENTRY_COUNT = 8192;
        public const int MAX_FILE_COUNT = 8192;
        public const int MAX_DEPTH = 32;
        public const int MAX_RAW_NAME_BYTES = 1024;
        public const int MAX_PATH_CHARACTERS = 512;
        public const int MAX_SEGMENT_CHARACTERS = 255;
        public const long MAX_ENTRY_COMPRESSED_BYTES = 72L * 1024 * 1024;
        public const long MAX_ENTRY_EXPANDED_BYTES = 64L * 1024 * 1024;
        public const long MAX_TOTAL_COMPRESSED_BYTES = 512L * 1024 * 1024;
        public const long MAX_TOTAL_EXPANDED_BYTES = 512L * 1024 * 1024;
        public const long MAX_SKIN_INFO_BYTES = 1024 * 1024;
        public const long MAX_CONFIGURATION_BYTES = 16L * 1024 * 1024;
        public const int MAX_COMPRESSION_RATIO = 100;
        public const long COMPRESSION_RATIO_SLACK_BYTES = 1024 * 1024;
    }

    internal enum SkinArchiveRejectionReason
    {
        InvalidSource,
        ArchiveByteBudgetExceeded,
        InvalidArchive,
        MultiDiskArchive,
        Zip64Archive,
        CentralDirectoryBudgetExceeded,
        MetadataBudgetExceeded,
        EntryCountBudgetExceeded,
        FileCountBudgetExceeded,
        UnsupportedCompression,
        EncryptedEntry,
        UnsupportedEntryKind,
        InvalidEntryName,
        EntryNameBudgetExceeded,
        EntryDepthBudgetExceeded,
        DuplicateEntryPath,
        PathTypeConflict,
        EntryCompressedByteBudgetExceeded,
        EntryExpandedByteBudgetExceeded,
        TotalCompressedByteBudgetExceeded,
        TotalExpandedByteBudgetExceeded,
        CompressionRatioExceeded,
        ConfigurationByteBudgetExceeded,
        DeclaredSizeMismatch,
        ActualSizeMismatch,
        CrcMismatch,
        SourceReadFailed,
    }

    internal sealed class SkinArchiveImportException : IOException
    {
        public SkinArchiveRejectionReason Reason { get; }

        public SkinArchiveImportException(SkinArchiveRejectionReason reason, Exception? innerException = null)
            : base($"Skin archive rejected: {reason}.", innerException)
        {
            Reason = reason;
        }
    }

    internal enum SkinArchiveInstantiationKind
    {
        Legacy,
        DefaultLegacy,
        Triangles,
        Argon,
        ArgonPro,
        Retro,
        Oms,
    }

    /// <summary>
    /// A skin-only ZIP reader which completes all metadata admission before exposing a filename or entry stream.
    /// </summary>
    internal sealed class SkinArchiveReader : ArchiveReader
    {
        private const uint local_header_signature = 0x04034b50;
        private const uint central_header_signature = 0x02014b50;
        private const uint end_of_central_directory_signature = 0x06054b50;

        private static readonly Encoding fallback_encoding;
        private static readonly Encoding strict_utf8 = new UTF8Encoding(false, true);
        private static readonly uint[] crc_table = createCrcTable();

        private readonly SourceOwner source;
        private readonly ZipArchive archive;
        private readonly IReadOnlyList<EntryMetadata> entries;
        private readonly Dictionary<string, EntryMetadata> filesByName;
        private readonly Dictionary<int, byte[]> cachedEntries = new Dictionary<int, byte[]>();
        private readonly HashSet<int> validatedEntries = new HashSet<int>();
        private readonly CancellationToken cancellationToken;

        private long actualExpandedBytes;
        private bool disposed;

        public SkinArchiveInstantiationKind InstantiationKind { get; private set; } = SkinArchiveInstantiationKind.Legacy;

        static SkinArchiveReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            fallback_encoding = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        private SkinArchiveReader(SourceOwner source, string name, CancellationToken cancellationToken)
            : base(name)
        {
            this.source = source;
            this.cancellationToken = cancellationToken;

            try
            {
                entries = parseMetadata(source.Stream, cancellationToken);

                source.Stream.Position = 0;
                archive = new ZipArchive(source.Stream, ZipArchiveMode.Read, true, fallback_encoding);
                reconcileArchiveEntries();

                filesByName = entries.Where(e => !e.IsDirectory)
                                     .ToDictionary(e => e.NormalisedName, StringComparer.OrdinalIgnoreCase);

                InstantiationKind = readInstantiationKind();
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public static async ValueTask<ArchiveReader> OpenAsync(ImportTask task, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(task);
            cancellationToken.ThrowIfCancellationRequested();

            SourceOwner owner = await SourceOwner.OpenAsync(task, cancellationToken).ConfigureAwait(false);

            try
            {
                // Preserve ImportTask's established reader naming contract: stream tasks retain the caller-supplied name,
                // while filesystem paths expose only their leaf filename.
                string archiveName = task.Stream == null ? Path.GetFileName(task.Path) : task.Path;
                return new SkinArchiveReader(owner, archiveName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                owner.Dispose();
                throw;
            }
            catch (SkinArchiveImportException)
            {
                owner.Dispose();
                throw;
            }
            catch (Exception e)
            {
                owner.Dispose();
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive, e);
            }
        }

        public override IEnumerable<string> Filenames => filesByName.Keys.ExcludeSystemFileNames();

        public override Stream GetStream(string name)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!filesByName.TryGetValue(name, out EntryMetadata? metadata))
                return null!;

            byte[] bytes = materialise(metadata, cache: metadata.IsSkinInfo);
            return new CancellationAwareReadStream(bytes, cancellationToken);
        }

        public override void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            cachedEntries.Clear();
            archive.Dispose();
            source.Dispose();
        }

        private SkinArchiveInstantiationKind readInstantiationKind()
        {
            EntryMetadata? skinInfo = entries.SingleOrDefault(e => !e.IsDirectory && e.IsSkinInfo);

            if (skinInfo == null)
                return SkinArchiveInstantiationKind.Legacy;

            try
            {
                byte[] content = materialise(skinInfo, cache: true);
                using var memory = new MemoryStream(content, writable: false);
                using var streamReader = new StreamReader(memory, Encoding.UTF8, true, 1024, true);
                using var jsonReader = new JsonTextReader(streamReader) { MaxDepth = 16 };
                var dto = JsonSerializer.CreateDefault().Deserialize<SkinInfoDto>(jsonReader);
                return SkinArchiveInstantiationPolicy.Resolve(dto?.InstantiationInfo);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SkinArchiveImportException)
            {
                throw;
            }
            catch
            {
                return SkinArchiveInstantiationKind.Legacy;
            }
        }

        private byte[] materialise(EntryMetadata metadata, bool cache)
        {
            if (cachedEntries.TryGetValue(metadata.Index, out byte[]? cached))
                return cached;

            cancellationToken.ThrowIfCancellationRequested();

            ZipArchiveEntry entry = archive.Entries[metadata.Index];
            using Stream input = entry.Open();
            using var output = new MemoryStream(checked((int)metadata.ExpandedSize));

            var crc = new Crc32Calculator();
            byte[] buffer = new byte[64 * 1024];
            long actual = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read;

                try
                {
                    read = input.Read(buffer, 0, buffer.Length);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.SourceReadFailed, e);
                }

                if (read == 0)
                    break;

                actual = checked(actual + read);

                if (actual > metadata.ExpandedSize || actual > SkinArchiveImportLimits.MAX_ENTRY_EXPANDED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.ActualSizeMismatch);

                crc.Append(buffer.AsSpan(0, read));
                output.Write(buffer, 0, read);
            }

            if (actual != metadata.ExpandedSize)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.ActualSizeMismatch);

            if (crc.GetCurrentHash() != metadata.Crc32)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.CrcMismatch);

            if (validatedEntries.Add(metadata.Index))
            {
                actualExpandedBytes = checked(actualExpandedBytes + actual);
                if (actualExpandedBytes > SkinArchiveImportLimits.MAX_TOTAL_EXPANDED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.TotalExpandedByteBudgetExceeded);
            }

            byte[] result = output.ToArray();
            if (cache)
                cachedEntries.Add(metadata.Index, result);

            return result;
        }

        private void reconcileArchiveEntries()
        {
            if (archive.Entries.Count != entries.Count)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

            for (int i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                EntryMetadata expected = entries[i];
                ZipArchiveEntry actual = archive.Entries[i];

                if (!string.Equals(actual.FullName, expected.OriginalName, StringComparison.Ordinal)
                    || actual.Length != expected.ExpandedSize
                    || actual.CompressedLength != expected.CompressedSize)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DeclaredSizeMismatch);
            }
        }

        private static IReadOnlyList<EntryMetadata> parseMetadata(Stream stream, CancellationToken cancellationToken)
        {
            if (!stream.CanRead || !stream.CanSeek)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidSource);

            long archiveLength;

            try
            {
                archiveLength = stream.Length;
            }
            catch (Exception e)
            {
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidSource, e);
            }

            if (archiveLength < 22)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);
            if (archiveLength > SkinArchiveImportLimits.MAX_ARCHIVE_BYTES)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.ArchiveByteBudgetExceeded);

            long tailLength = Math.Min(archiveLength, 22 + ushort.MaxValue);
            byte[] tail = new byte[checked((int)tailLength)];
            readExactlyAt(stream, archiveLength - tailLength, tail);

            int eocdIndex = -1;
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (readUInt32(tail, i) != end_of_central_directory_signature)
                    continue;

                ushort commentLength = readUInt16(tail, i + 20);
                if (i + 22 + commentLength == tail.Length)
                {
                    eocdIndex = i;
                    break;
                }
            }

            if (eocdIndex < 0)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

            ushort diskNumber = readUInt16(tail, eocdIndex + 4);
            ushort centralDisk = readUInt16(tail, eocdIndex + 6);
            ushort entriesOnDisk = readUInt16(tail, eocdIndex + 8);
            ushort entryCount = readUInt16(tail, eocdIndex + 10);
            uint centralSize = readUInt32(tail, eocdIndex + 12);
            uint centralOffset = readUInt32(tail, eocdIndex + 16);

            if (diskNumber != 0 || centralDisk != 0 || entriesOnDisk != entryCount)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.MultiDiskArchive);

            if (entryCount == ushort.MaxValue || centralSize == uint.MaxValue || centralOffset == uint.MaxValue)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.Zip64Archive);

            if (entryCount > SkinArchiveImportLimits.MAX_ENTRY_COUNT)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.EntryCountBudgetExceeded);
            if (centralSize > SkinArchiveImportLimits.MAX_CENTRAL_DIRECTORY_BYTES)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.CentralDirectoryBudgetExceeded);

            long eocdOffset = archiveLength - tailLength + eocdIndex;
            long centralEnd = checked((long)centralOffset + centralSize);
            if (centralEnd != eocdOffset)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

            stream.Position = centralOffset;

            var result = new List<EntryMetadata>(entryCount);
            long metadataBytes = 0;
            long totalCompressed = 0;
            long totalExpanded = 0;
            long configurationBytes = 0;
            int fileCount = 0;

            for (int index = 0; index < entryCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] header = readExactly(stream, 46);

                if (readUInt32(header, 0) != central_header_signature)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                ushort versionMadeBy = readUInt16(header, 4);
                ushort versionNeeded = readUInt16(header, 6);
                ushort flags = readUInt16(header, 8);
                ushort method = readUInt16(header, 10);
                uint crc32 = readUInt32(header, 16);
                uint compressedSize = readUInt32(header, 20);
                uint expandedSize = readUInt32(header, 24);
                ushort nameLength = readUInt16(header, 28);
                ushort extraLength = readUInt16(header, 30);
                ushort commentLength = readUInt16(header, 32);
                ushort startDisk = readUInt16(header, 34);
                uint externalAttributes = readUInt32(header, 38);
                uint localHeaderOffset = readUInt32(header, 42);

                if (startDisk != 0)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.MultiDiskArchive);
                if (versionNeeded >= 45 || compressedSize == uint.MaxValue || expandedSize == uint.MaxValue || localHeaderOffset == uint.MaxValue)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.Zip64Archive);
                if ((flags & (0x0001 | 0x0040 | 0x2000)) != 0)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.EncryptedEntry);
                if ((flags & ~0x080e) != 0 || (method == 0 && (flags & 0x0006) != 0))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedCompression);
                if (method is not 0 and not 8)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedCompression);
                if (nameLength == 0)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName);
                if (nameLength > SkinArchiveImportLimits.MAX_RAW_NAME_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.EntryNameBudgetExceeded);

                byte[] rawName = readExactly(stream, nameLength);
                byte[] extra = readExactly(stream, extraLength);
                if (containsZip64Extra(extra))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.Zip64Archive);
                skipExactly(stream, commentLength);

                string originalName;
                try
                {
                    originalName = ((flags & 0x0800) != 0 ? strict_utf8 : fallback_encoding).GetString(rawName);
                }
                catch (DecoderFallbackException e)
                {
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName, e);
                }

                bool nameIsDirectory = originalName.EndsWith('/') || originalName.EndsWith('\\');
                bool isDirectory = validateEntryKind(versionMadeBy, externalAttributes, nameIsDirectory);
                string normalisedName = normaliseAndValidateName(originalName, isDirectory);

                if (isDirectory && (compressedSize != 0 || expandedSize != 0))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DeclaredSizeMismatch);

                if (!isDirectory)
                {
                    fileCount++;
                    if (fileCount > SkinArchiveImportLimits.MAX_FILE_COUNT)
                        throw new SkinArchiveImportException(SkinArchiveRejectionReason.FileCountBudgetExceeded);
                }

                if (compressedSize > SkinArchiveImportLimits.MAX_ENTRY_COMPRESSED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.EntryCompressedByteBudgetExceeded);
                if (expandedSize > SkinArchiveImportLimits.MAX_ENTRY_EXPANDED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.EntryExpandedByteBudgetExceeded);

                enforceRatio(expandedSize, compressedSize);

                totalCompressed = checked(totalCompressed + compressedSize);
                totalExpanded = checked(totalExpanded + expandedSize);
                metadataBytes = checked(metadataBytes + 256L + rawName.Length + extraLength + commentLength + normalisedName.Length * sizeof(char));

                if (totalCompressed > SkinArchiveImportLimits.MAX_TOTAL_COMPRESSED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.TotalCompressedByteBudgetExceeded);
                if (totalExpanded > SkinArchiveImportLimits.MAX_TOTAL_EXPANDED_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.TotalExpandedByteBudgetExceeded);
                if (metadataBytes > SkinArchiveImportLimits.MAX_METADATA_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.MetadataBudgetExceeded);

                bool isConfiguration = !isDirectory && (normalisedName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                                                          || normalisedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                if (isConfiguration)
                {
                    configurationBytes = checked(configurationBytes + expandedSize);
                    if (configurationBytes > SkinArchiveImportLimits.MAX_CONFIGURATION_BYTES)
                        throw new SkinArchiveImportException(SkinArchiveRejectionReason.ConfigurationByteBudgetExceeded);
                }

                result.Add(new EntryMetadata(index, originalName, normalisedName, isDirectory, versionNeeded, flags, method, crc32,
                    compressedSize, expandedSize, localHeaderOffset, rawName));
            }

            if (stream.Position != centralEnd)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

            enforceRatio(totalExpanded, totalCompressed);
            validatePathSet(result);
            validateLocalHeaders(stream, result, centralOffset, cancellationToken);
            markShortenedNamesAndSkinInfo(result);

            return result;
        }

        private static void validateLocalHeaders(Stream stream, List<EntryMetadata> entries, long centralOffset, CancellationToken cancellationToken)
        {
            var ranges = new List<(long Start, long End)>(entries.Count);

            foreach (EntryMetadata entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] local = new byte[30];
                readExactlyAt(stream, entry.LocalHeaderOffset, local);

                if (readUInt32(local, 0) != local_header_signature)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                ushort flags = readUInt16(local, 6);
                ushort method = readUInt16(local, 8);
                uint crc = readUInt32(local, 14);
                uint compressed = readUInt32(local, 18);
                uint expanded = readUInt32(local, 22);
                ushort nameLength = readUInt16(local, 26);
                ushort extraLength = readUInt16(local, 28);

                if (readUInt16(local, 4) != entry.VersionNeeded || flags != entry.Flags || method != entry.Method || nameLength != entry.RawName.Length)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                byte[] localName = new byte[nameLength];
                readExactlyAt(stream, checked((long)entry.LocalHeaderOffset + 30), localName);
                if (!localName.AsSpan().SequenceEqual(entry.RawName))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                byte[] localExtra = new byte[extraLength];
                readExactlyAt(stream, checked((long)entry.LocalHeaderOffset + 30 + nameLength), localExtra);
                if (containsZip64Extra(localExtra))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.Zip64Archive);

                bool dataDescriptor = (flags & 0x0008) != 0;
                if (!dataDescriptor && (crc != entry.Crc32 || compressed != entry.CompressedSize || expanded != entry.ExpandedSize))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DeclaredSizeMismatch);
                if (dataDescriptor
                    && ((crc != 0 && crc != entry.Crc32)
                        || (compressed != 0 && compressed != entry.CompressedSize)
                        || (expanded != 0 && expanded != entry.ExpandedSize)))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DeclaredSizeMismatch);

                long dataStart = checked((long)entry.LocalHeaderOffset + 30 + nameLength + extraLength);
                long dataEnd = checked(dataStart + entry.CompressedSize);
                if (dataEnd > centralOffset)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                ranges.Add((entry.LocalHeaderOffset, dataEnd));
            }

            ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (int i = 1; i < ranges.Count; i++)
            {
                if (ranges[i].Start < ranges[i - 1].End)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);
            }
        }

        private static void validatePathSet(List<EntryMetadata> entries)
        {
            var kinds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (EntryMetadata entry in entries)
            {
                string key = entry.NormalisedName.TrimEnd('/');
                if (!kinds.TryAdd(key, entry.IsDirectory))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DuplicateEntryPath);
            }

            foreach ((string path, bool isDirectory) in kinds)
            {
                string[] segments = path.Split('/');
                string prefix = string.Empty;

                for (int i = 0; i < segments.Length - 1; i++)
                {
                    prefix = i == 0 ? segments[i] : $"{prefix}/{segments[i]}";
                    if (kinds.TryGetValue(prefix, out bool ancestorIsDirectory) && !ancestorIsDirectory)
                        throw new SkinArchiveImportException(SkinArchiveRejectionReason.PathTypeConflict);
                }

                if (!isDirectory && kinds.TryGetValue(path + "/", out _))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.PathTypeConflict);
            }
        }

        private static void markShortenedNamesAndSkinInfo(List<EntryMetadata> entries)
        {
            HashSet<string> visibleNames = entries.Where(e => !e.IsDirectory)
                                                  .Select(e => e.NormalisedName)
                                                  .ExcludeSystemFileNames()
                                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<EntryMetadata> files = entries.Where(e => !e.IsDirectory && visibleNames.Contains(e.NormalisedName)).ToList();
            string prefix = getCommonPrefix(files.Select(e => e.NormalisedName));
            if (!prefix.EndsWith('/'))
                prefix = string.Empty;

            var shortened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (EntryMetadata entry in files)
            {
                string shortenedName = normaliseAndValidateName(entry.NormalisedName.Substring(prefix.Length), false);
                if (!shortened.Add(shortenedName))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.DuplicateEntryPath);

                entry.IsSkinInfo = string.Equals(shortenedName, "skininfo.json", StringComparison.OrdinalIgnoreCase);
                if (entry.IsSkinInfo && entry.ExpandedSize > SkinArchiveImportLimits.MAX_SKIN_INFO_BYTES)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.ConfigurationByteBudgetExceeded);
            }
        }

        private static string getCommonPrefix(IEnumerable<string> values)
        {
            using IEnumerator<string> enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
                return string.Empty;

            string prefix = enumerator.Current;
            while (enumerator.MoveNext())
            {
                string current = enumerator.Current;
                int length = Math.Min(prefix.Length, current.Length);
                int i = 0;
                while (i < length && prefix[i] == current[i])
                    i++;
                prefix = prefix.Substring(0, i);
                if (prefix.Length == 0)
                    break;
            }

            return prefix;
        }

        private static bool validateEntryKind(ushort versionMadeBy, uint externalAttributes, bool nameIsDirectory)
        {
            int unixType = (int)(externalAttributes >> 16) & 0xF000;
            bool dosDirectory = (externalAttributes & 0x10) != 0;

            // Volume labels and reparse points are not files. Treat these semantics as hostile even if the producer has
            // supplied an inconsistent host-OS marker; they must never enter the hash-backed store as ordinary resources.
            if ((externalAttributes & (0x0008 | 0x0400)) != 0)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedEntryKind);

            if (unixType != 0)
            {
                if (unixType is not 0x4000 and not 0x8000)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedEntryKind);

                bool unixDirectory = unixType == 0x4000;
                if (unixDirectory != nameIsDirectory)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedEntryKind);
            }

            if (dosDirectory && !nameIsDirectory)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.UnsupportedEntryKind);

            return nameIsDirectory;
        }

        private static string normaliseAndValidateName(string value, bool isDirectory)
        {
            if (value.IndexOf('\0') >= 0)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName);

            string path;
            try
            {
                path = value.Replace('\\', '/').Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException e)
            {
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName, e);
            }

            if (isDirectory)
                path = path.TrimEnd('/');

            if (path.Length == 0 || path.Length > SkinArchiveImportLimits.MAX_PATH_CHARACTERS || path.StartsWith('/'))
                throw new SkinArchiveImportException(path.Length > SkinArchiveImportLimits.MAX_PATH_CHARACTERS
                    ? SkinArchiveRejectionReason.EntryNameBudgetExceeded
                    : SkinArchiveRejectionReason.InvalidEntryName);

            string[] segments = path.Split('/');
            if (segments.Length > SkinArchiveImportLimits.MAX_DEPTH)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.EntryDepthBudgetExceeded);

            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment.Length > SkinArchiveImportLimits.MAX_SEGMENT_CHARACTERS
                    || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' '))
                    throw new SkinArchiveImportException(segment.Length > SkinArchiveImportLimits.MAX_SEGMENT_CHARACTERS
                        ? SkinArchiveRejectionReason.EntryNameBudgetExceeded
                        : SkinArchiveRejectionReason.InvalidEntryName);

                foreach (char c in segment)
                {
                    if (c < 0x20 || c is '<' or '>' or ':' or '"' or '|' or '?' or '*')
                        throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName);
                }

                string deviceCandidate = segment.Split('.')[0].TrimEnd(' ', '.');
                if (isWindowsDeviceName(deviceCandidate))
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidEntryName);
            }

            return isDirectory ? path + "/" : path;
        }

        private static bool isWindowsDeviceName(string value)
        {
            if (value.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || value.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || value.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || value.Equals("NUL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.Length == 4 && value[3] is >= '1' and <= '9')
                return value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase);

            if (value.Length == 4 && value[3] is '\u00b9' or '\u00b2' or '\u00b3')
                return value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase);

            return false;
        }

        private static bool containsZip64Extra(ReadOnlySpan<byte> extra)
        {
            int offset = 0;

            while (offset < extra.Length)
            {
                if (extra.Length - offset < 4)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

                ushort identifier = BinaryPrimitives.ReadUInt16LittleEndian(extra.Slice(offset, 2));
                ushort size = BinaryPrimitives.ReadUInt16LittleEndian(extra.Slice(offset + 2, 2));
                offset += 4;

                if (size > extra.Length - offset)
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);
                if (identifier == 0x0001)
                    return true;

                offset += size;
            }

            return false;
        }

        private static void enforceRatio(long expanded, long compressed)
        {
            long allowed;
            try
            {
                allowed = checked(compressed * SkinArchiveImportLimits.MAX_COMPRESSION_RATIO + SkinArchiveImportLimits.COMPRESSION_RATIO_SLACK_BYTES);
            }
            catch (OverflowException)
            {
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.CompressionRatioExceeded);
            }

            if (expanded > allowed)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.CompressionRatioExceeded);
        }

        private static byte[] readExactly(Stream stream, int length)
        {
            byte[] result = new byte[length];
            try
            {
                stream.ReadExactly(result);
                return result;
            }
            catch (Exception e) when (e is not SkinArchiveImportException)
            {
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive, e);
            }
        }

        private static void readExactlyAt(Stream stream, long position, byte[] destination)
        {
            if (position < 0 || position > stream.Length - destination.Length)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);

            stream.Position = position;
            try
            {
                stream.ReadExactly(destination);
            }
            catch (Exception e)
            {
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive, e);
            }
        }

        private static void skipExactly(Stream stream, int length)
        {
            long target = checked(stream.Position + length);
            if (target > stream.Length)
                throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidArchive);
            stream.Position = target;
        }

        private static ushort readUInt16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

        private static uint readUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

        private static uint[] createCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                table[i] = value;
            }

            return table;
        }

        private sealed class EntryMetadata
        {
            public int Index { get; }
            public string OriginalName { get; }
            public string NormalisedName { get; }
            public bool IsDirectory { get; }
            public ushort VersionNeeded { get; }
            public ushort Flags { get; }
            public ushort Method { get; }
            public uint Crc32 { get; }
            public long CompressedSize { get; }
            public long ExpandedSize { get; }
            public long LocalHeaderOffset { get; }
            public byte[] RawName { get; }
            public bool IsSkinInfo { get; set; }

            public EntryMetadata(int index, string originalName, string normalisedName, bool isDirectory, ushort versionNeeded, ushort flags, ushort method,
                                 uint crc32, long compressedSize, long expandedSize, long localHeaderOffset, byte[] rawName)
            {
                Index = index;
                OriginalName = originalName;
                NormalisedName = normalisedName;
                IsDirectory = isDirectory;
                VersionNeeded = versionNeeded;
                Flags = flags;
                Method = method;
                Crc32 = crc32;
                CompressedSize = compressedSize;
                ExpandedSize = expandedSize;
                LocalHeaderOffset = localHeaderOffset;
                RawName = rawName;
            }
        }

        private sealed class SkinInfoDto
        {
            public string? InstantiationInfo { get; set; }
        }

        private sealed class Crc32Calculator
        {
            private uint value = uint.MaxValue;

            public void Append(ReadOnlySpan<byte> bytes)
            {
                uint current = value;
                foreach (byte b in bytes)
                    current = crc_table[(current ^ b) & 0xff] ^ (current >> 8);
                value = current;
            }

            public uint GetCurrentHash() => ~value;
        }

        private sealed class CancellationAwareReadStream : Stream
        {
            private readonly MemoryStream inner;
            private readonly CancellationToken cancellationToken;

            public CancellationAwareReadStream(byte[] content, CancellationToken cancellationToken)
            {
                inner = new MemoryStream(content, writable: false);
                this.cancellationToken = cancellationToken;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return inner.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return inner.Read(buffer);
            }

            public override int ReadByte()
            {
                cancellationToken.ThrowIfCancellationRequested();
                return inner.ReadByte();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(inner.Read(buffer, offset, count));
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(inner.Read(buffer.Span));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => inner.Length;

            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }
        }

        private sealed class SourceOwner : IDisposable
        {
            public Stream Stream { get; }
            private readonly string? temporaryPath;
            private bool disposed;

            private SourceOwner(Stream stream, string? temporaryPath = null)
            {
                Stream = stream;
                this.temporaryPath = temporaryPath;
            }

            public static async ValueTask<SourceOwner> OpenAsync(ImportTask task, CancellationToken cancellationToken)
            {
                Stream source;

                try
                {
                    source = task.Stream ?? File.Open(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (Exception e)
                {
                    task.Stream?.Dispose();
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidSource, e);
                }

                bool canSeek;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    canSeek = source.CanSeek;
                }
                catch (Exception e)
                {
                    source.Dispose();
                    if (e is OperationCanceledException)
                        throw;
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidSource, e);
                }

                if (canSeek)
                {
                    try
                    {
                        source.Position = 0;
                        if (source.Length > SkinArchiveImportLimits.MAX_ARCHIVE_BYTES)
                            throw new SkinArchiveImportException(SkinArchiveRejectionReason.ArchiveByteBudgetExceeded);
                        return new SourceOwner(source);
                    }
                    catch (Exception e)
                    {
                        source.Dispose();
                        if (e is SkinArchiveImportException or OperationCanceledException)
                            throw;
                        throw new SkinArchiveImportException(SkinArchiveRejectionReason.InvalidSource, e);
                    }
                }

                string temporaryPath = Path.Combine(Path.GetTempPath(), $"oms-skin-archive-{Guid.NewGuid():N}.tmp");
                FileStream? spool = null;

                try
                {
                    spool = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

                    byte[] buffer = new byte[64 * 1024];
                    long total = 0;

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            break;

                        total = checked(total + read);
                        if (total > SkinArchiveImportLimits.MAX_ARCHIVE_BYTES)
                            throw new SkinArchiveImportException(SkinArchiveRejectionReason.ArchiveByteBudgetExceeded);

                        await spool.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    await spool.FlushAsync(cancellationToken).ConfigureAwait(false);
                    spool.Position = 0;
                    return new SourceOwner(spool, temporaryPath);
                }
                catch (Exception e)
                {
                    spool?.Dispose();
                    tryDelete(temporaryPath);
                    if (e is SkinArchiveImportException or OperationCanceledException)
                        throw;
                    throw new SkinArchiveImportException(SkinArchiveRejectionReason.SourceReadFailed, e);
                }
                finally
                {
                    source.Dispose();
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;

                Stream.Dispose();
                if (temporaryPath != null)
                    tryDelete(temporaryPath);
            }

            private static void tryDelete(string path)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }
    }

    internal static class SkinArchiveInstantiationPolicy
    {
        private const string legacy = "osu.Game.Skinning.LegacySkin, osu.Game";
        private const string bms_legacy = "osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms";
        private const string default_legacy = "osu.Game.Skinning.DefaultLegacySkin, osu.Game";
        private const string old_default = "osu.Game.Skinning.DefaultSkin, osu.Game";
        private const string triangles = "osu.Game.Skinning.TrianglesSkin, osu.Game";
        private const string argon = "osu.Game.Skinning.ArgonSkin, osu.Game";
        private const string argon_pro = "osu.Game.Skinning.ArgonProSkin, osu.Game";
        private const string retro = "osu.Game.Skinning.RetroSkin, osu.Game";
        private const string oms = "osu.Game.Skinning.OmsSkin, osu.Game";

        public static SkinArchiveInstantiationKind Resolve(string? value) => value switch
        {
            null or "" or legacy or bms_legacy => SkinArchiveInstantiationKind.Legacy,
            default_legacy => SkinArchiveInstantiationKind.DefaultLegacy,
            old_default or triangles => SkinArchiveInstantiationKind.Triangles,
            argon => SkinArchiveInstantiationKind.Argon,
            argon_pro => SkinArchiveInstantiationKind.ArgonPro,
            retro => SkinArchiveInstantiationKind.Retro,
            oms => SkinArchiveInstantiationKind.Oms,
            _ => SkinArchiveInstantiationKind.Legacy,
        };
    }
}
