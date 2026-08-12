// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Compact durable encoding of the exact logical tree paired with one external capsule.
    /// </summary>
    /// <remarks>
    /// This is recovery evidence, not a path capability. The encoding contains relative package names only and is
    /// never included in support diagnostics or logs.
    /// </remarks>
    internal sealed class SkinManagedCopyLogicalManifest
    {
        private const int current_version = 1;
        private const int max_encoded_bytes = 700 * 1024;
        private static readonly byte[] domain = Encoding.ASCII.GetBytes("OMS/SkinManagedCopyLogicalManifest/v1\0");
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        private readonly Entry[] entries;

        public string Encoded { get; }

        public string Digest { get; }

        public IReadOnlyList<Entry> Entries { get; }

        private SkinManagedCopyLogicalManifest(string encoded, string digest, Entry[] entries)
        {
            Encoded = encoded;
            Digest = digest;
            this.entries = entries;
            Entries = Array.AsReadOnly((Entry[])entries.Clone());
        }

        public static SkinManagedCopyLogicalManifest Create(SkinExternalPackageLogicalManifest source)
        {
            ArgumentNullException.ThrowIfNull(source);
            Entry[] entries = source.Entries.Select(entry => new Entry(entry.RelativePath, entry.Kind, entry.Length)).ToArray();
            byte[] payload = encode(entries);

            if (payload.Length > max_encoded_bytes)
                throw new InvalidOperationException("The managed-copy manifest exceeds its durable budget.");

            return new SkinManagedCopyLogicalManifest(
                Convert.ToBase64String(payload),
                Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
                entries);
        }

        public static bool TryParse(string encoded, string digest, out SkinManagedCopyLogicalManifest manifest)
        {
            manifest = null!;

            if (string.IsNullOrEmpty(encoded)
                || encoded.Length > checked(((max_encoded_bytes + 2) / 3) * 4)
                || !SkinExternalRegistryJournalBinding.IsLowercaseSha256(digest))
            {
                return false;
            }

            byte[] payload;

            try
            {
                payload = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                return false;
            }

            if (payload.Length == 0
                || payload.Length > max_encoded_bytes
                || !string.Equals(
                    Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
                    digest,
                    StringComparison.Ordinal)
                || !tryDecode(payload, out Entry[] entries))
            {
                return false;
            }

            manifest = new SkinManagedCopyLogicalManifest(encoded, digest, entries);
            return true;
        }

        public bool Matches(SkinExternalPackageLogicalManifest candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            if (candidate.Entries.Count != entries.Length)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                SkinExternalPackageLogicalManifestEntry candidateEntry = candidate.Entries[i];

                if (!string.Equals(entries[i].RelativePath, candidateEntry.RelativePath, StringComparison.Ordinal)
                    || entries[i].Kind != candidateEntry.Kind
                    || entries[i].Length != candidateEntry.Length)
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] encode(IReadOnlyList<Entry> entries)
        {
            using var stream = new MemoryStream();
            stream.Write(domain);
            writeInt32(stream, current_version);
            writeInt32(stream, entries.Count);

            foreach (Entry entry in entries)
            {
                if (!entry.IsValid)
                    throw new ArgumentException("The managed-copy manifest entry is invalid.", nameof(entries));

                byte[] name = strict_utf8.GetBytes(entry.RelativePath);
                writeInt32(stream, name.Length);
                stream.Write(name);
                stream.WriteByte((byte)entry.Kind);
                writeInt64(stream, entry.Length);

                if (stream.Length > max_encoded_bytes)
                    throw new InvalidOperationException("The managed-copy manifest exceeds its durable budget.");
            }

            return stream.ToArray();
        }

        private static bool tryDecode(ReadOnlySpan<byte> payload, out Entry[] entries)
        {
            entries = Array.Empty<Entry>();
            int offset = 0;

            if (!readExact(payload, ref offset, domain.Length, out ReadOnlySpan<byte> observedDomain)
                || !observedDomain.SequenceEqual(domain)
                || !readInt32(payload, ref offset, out int version)
                || version != current_version
                || !readInt32(payload, ref offset, out int count)
                || count < 1
                || count > SkinPackageRevisionCapsuleLimits.Default.MaxEntryCount)
            {
                return false;
            }

            var decoded = new Entry[count];
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kindsByPath = new Dictionary<string, SkinExternalPackageLogicalEntryKind>(
                StringComparer.OrdinalIgnoreCase);
            SkinPackageRevisionCapsuleLimits limits = SkinPackageRevisionCapsuleLimits.Default;
            int fileCount = 0;
            long totalFileBytes = 0;

            for (int i = 0; i < count; i++)
            {
                if (!readInt32(payload, ref offset, out int nameLength)
                    || nameLength <= 0
                    || nameLength > SkinPackageRevisionCapsuleLimits.Default.MaxResourceNameLength * 4
                    || !readExact(payload, ref offset, nameLength, out ReadOnlySpan<byte> nameBytes))
                {
                    return false;
                }

                string name;

                try
                {
                    name = strict_utf8.GetString(nameBytes);
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }

                if (!readExact(payload, ref offset, 1, out ReadOnlySpan<byte> kindBytes)
                    || !readInt64(payload, ref offset, out long length))
                {
                    return false;
                }

                var entry = new Entry(name, (SkinExternalPackageLogicalEntryKind)kindBytes[0], length);

                if (!entry.IsValid
                    || !SkinPackageResourceNameValidator.TryNormalise(name, out _, out int depth)
                    || depth > limits.MaxDepth
                    || !names.Add(name)
                    || !kindsByPath.TryAdd(name, entry.Kind))
                    return false;

                if (entry.Kind == SkinExternalPackageLogicalEntryKind.File)
                {
                    if (entry.Length > limits.MaxFileBytes)
                        return false;

                    try
                    {
                        fileCount = checked(fileCount + 1);
                        totalFileBytes = checked(totalFileBytes + entry.Length);
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }

                    if (fileCount > limits.MaxFileCount
                        || totalFileBytes > limits.MaxPackageBytes)
                    {
                        return false;
                    }
                }

                decoded[i] = entry;
            }

            if (offset != payload.Length
                || !decoded.SequenceEqual(
                    decoded.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal))
                || decoded.Any(entry => !hasExactDirectoryParents(entry.RelativePath, kindsByPath)))
            {
                return false;
            }

            entries = decoded;
            return true;
        }

        private static bool hasExactDirectoryParents(
            string relativePath,
            IReadOnlyDictionary<string, SkinExternalPackageLogicalEntryKind> kindsByPath)
        {
            int separator = relativePath.IndexOf('/');

            while (separator >= 0)
            {
                string parent = relativePath[..separator];

                if (!kindsByPath.TryGetValue(parent, out SkinExternalPackageLogicalEntryKind kind)
                    || kind != SkinExternalPackageLogicalEntryKind.Directory)
                {
                    return false;
                }

                separator = relativePath.IndexOf('/', separator + 1);
            }

            return true;
        }

        private static bool readExact(ReadOnlySpan<byte> source, ref int offset, int count, out ReadOnlySpan<byte> value)
        {
            value = default;

            if (count < 0 || offset < 0 || offset > source.Length - count)
                return false;

            value = source.Slice(offset, count);
            offset += count;
            return true;
        }

        private static bool readInt32(ReadOnlySpan<byte> source, ref int offset, out int value)
        {
            value = 0;

            if (!readExact(source, ref offset, sizeof(int), out ReadOnlySpan<byte> bytes))
                return false;

            value = BinaryPrimitives.ReadInt32BigEndian(bytes);
            return true;
        }

        private static bool readInt64(ReadOnlySpan<byte> source, ref int offset, out long value)
        {
            value = 0;

            if (!readExact(source, ref offset, sizeof(long), out ReadOnlySpan<byte> bytes))
                return false;

            value = BinaryPrimitives.ReadInt64BigEndian(bytes);
            return true;
        }

        private static void writeInt32(Stream stream, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            stream.Write(bytes);
        }

        private static void writeInt64(Stream stream, long value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            stream.Write(bytes);
        }

        internal readonly record struct Entry(
            string RelativePath,
            SkinExternalPackageLogicalEntryKind Kind,
            long Length)
        {
            public bool IsValid
                => SkinPackageResourceNameValidator.TryNormalise(RelativePath, out string normalised, out _)
                   && string.Equals(RelativePath, normalised, StringComparison.Ordinal)
                   && Enum.IsDefined(Kind)
                   && (Kind == SkinExternalPackageLogicalEntryKind.Directory
                       ? Length == 0
                       : Length >= 0);
        }
    }
}
