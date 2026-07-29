// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Author-controlled metadata captured from one exact immutable managed-folder package revision.
    /// </summary>
    internal sealed class SkinManagedFolderPackageMetadata
    {
        public string Name { get; }

        public string Creator { get; }

        public string ContentRevision { get; }

        public SkinManagedFolderPackageMetadata(string name, string creator, string contentRevision)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Creator = creator ?? throw new ArgumentNullException(nameof(creator));
            ContentRevision = string.IsNullOrEmpty(contentRevision)
                ? throw new ArgumentException("The package content revision is required.", nameof(contentRevision))
                : contentRevision;
        }

        public override string ToString() => nameof(SkinManagedFolderPackageMetadata);
    }

    /// <summary>
    /// Shared scanner/import metadata gate for a fully captured package capsule.
    /// </summary>
    internal static class SkinManagedFolderPackageMetadataReader
    {
        private const long max_metadata_file_bytes = 1024 * 1024;
        private const int max_metadata_value_characters = 256;
        private const string unnamed_skin = "No name";
        private const string unknown_creator = "Unknown";

        private static readonly Encoding strict_utf8 = new UTF8Encoding(false, true);

        public static bool TryRead(
            SkinPackageRevisionCapsule capsule,
            out SkinManagedFolderPackageMetadata? metadata)
        {
            ArgumentNullException.ThrowIfNull(capsule);
            metadata = null;

            SkinPackageFileRevision? skinIni = capsule.Files.SingleOrDefault(
                file => string.Equals(file.ResourceName, "skin.ini", StringComparison.OrdinalIgnoreCase));

            if (skinIni == null || skinIni.Length > max_metadata_file_bytes)
                return false;

            try
            {
                using var resources = capsule.CreateResourceView();
                using Stream? stream = resources.GetStream("skin.ini");

                if (stream == null)
                    return false;

                using var reader = new StreamReader(stream, strict_utf8, true, 1024, leaveOpen: false);
                bool inGeneralSection = true;
                string? parsedName = null;
                string? parsedCreator = null;
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)
                        || line.AsSpan().TrimStart().StartsWith("//".AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int commentIndex = line.IndexOf("//", StringComparison.Ordinal);

                    if (commentIndex > 0)
                        line = line[..commentIndex];

                    line = line.TrimEnd();

                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        inGeneralSection = string.Equals(line[1..^1], "General", StringComparison.Ordinal);
                        continue;
                    }

                    if (!inGeneralSection)
                        continue;

                    int separator = line.IndexOf(':');

                    if (separator < 0)
                        continue;

                    string key = line[..separator].Trim();
                    string value = line[(separator + 1)..].Trim();

                    if (string.Equals(key, "Name", StringComparison.Ordinal))
                    {
                        if (!isSafeMetadataValue(value))
                            return false;

                        parsedName = value;
                    }
                    else if (string.Equals(key, "Author", StringComparison.Ordinal))
                    {
                        if (!isSafeMetadataValue(value))
                            return false;

                        parsedCreator = value;
                    }
                }

                metadata = new SkinManagedFolderPackageMetadata(
                    string.IsNullOrEmpty(parsedName) ? unnamed_skin : parsedName,
                    string.IsNullOrEmpty(parsedCreator) ? unknown_creator : parsedCreator,
                    capsule.ContentRevision);
                return true;
            }
            catch (Exception exception) when (exception is IOException
                                               or ObjectDisposedException
                                               or DecoderFallbackException
                                               or InvalidOperationException)
            {
                return false;
            }
        }

        private static bool isSafeMetadataValue(string value)
            => value.Length <= max_metadata_value_characters
               && !value.Any(char.IsControl);
    }

    /// <summary>
    /// Exact Realm fields issued from a trusted staged-import plan and final target capsule.
    /// </summary>
    internal sealed class SkinManagedFolderNewRecordPublicationData
    {
        private const string fingerprint_domain = "oms.skin.managed-folder.new-record-publication.v1";

        public Guid RecordId { get; }

        public string ManagedRelativePath { get; }

        public string Name { get; }

        public string Creator { get; }

        public string InstantiationInfo { get; }

        public string ContentRevision { get; }

        public string AuthorityOwner => SkinManagedFolderScanner.AUTHORITY_OWNER;

        public string Fingerprint { get; }

        internal SkinManagedFolderNewRecordPublicationData(
            SkinManagedFolderNewRecordPublicationPlan plan,
            SkinManagedFolderPackageMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(metadata);

            if (!SkinManagedFolderFactory.IsInstantiationInfoAllowed(
                    SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO))
            {
                throw new InvalidOperationException("The managed-folder instantiation allowlist is invalid.");
            }

            RecordId = plan.PlannedRecordId;
            ManagedRelativePath = plan.TargetManagedRelativePath;
            Name = metadata.Name;
            Creator = metadata.Creator;
            InstantiationInfo = SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO;
            ContentRevision = metadata.ContentRevision;
            Fingerprint = computeFingerprint(
                RecordId,
                ManagedRelativePath,
                Name,
                Creator,
                InstantiationInfo,
                ContentRevision,
                AuthorityOwner);
        }

        public SkinInfo CreateRecord()
            => new SkinInfo(Name, Creator, InstantiationInfo)
            {
                ID = RecordId,
                Hash = ContentRevision,
                FilesystemStoragePath = ManagedRelativePath,
                IsExternalFilesystemStorage = false,
                FilesystemStorageAuthorityOwner = AuthorityOwner,
                Protected = false,
                DeletePending = false,
            };

        public bool IsExactRecord(SkinInfo record)
            => record != null
               && record.IsManaged
               && record.ID == RecordId
               && record.Files.Count == 0
               && string.Equals(record.FilesystemStoragePath, ManagedRelativePath, StringComparison.Ordinal)
               && !record.IsExternalFilesystemStorage
               && string.Equals(record.FilesystemStorageAuthorityOwner, AuthorityOwner, StringComparison.Ordinal)
               && !record.Protected
               && !record.DeletePending
               && string.Equals(record.Name, Name, StringComparison.Ordinal)
               && string.Equals(record.Creator, Creator, StringComparison.Ordinal)
               && string.Equals(record.InstantiationInfo, InstantiationInfo, StringComparison.Ordinal)
               && string.Equals(record.Hash, ContentRevision, StringComparison.Ordinal);

        internal static bool IsValidFingerprint(string? fingerprint)
            => fingerprint is { Length: 64 }
               && fingerprint.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        internal static string ComputeRecordFingerprint(SkinInfo record)
        {
            ArgumentNullException.ThrowIfNull(record);

            return computeFingerprint(
                record.ID,
                record.FilesystemStoragePath ?? string.Empty,
                record.Name,
                record.Creator,
                record.InstantiationInfo,
                record.Hash,
                record.FilesystemStorageAuthorityOwner ?? string.Empty);
        }

        private static string computeFingerprint(
            Guid recordId,
            string managedRelativePath,
            string name,
            string creator,
            string instantiationInfo,
            string contentRevision,
            string authorityOwner)
        {
            using var stream = new MemoryStream();
            append(stream, fingerprint_domain);
            append(stream, recordId.ToString("N"));
            append(stream, managedRelativePath);
            append(stream, name);
            append(stream, creator);
            append(stream, instantiationInfo);
            append(stream, contentRevision);
            append(stream, authorityOwner);
            append(stream, "files=0");
            append(stream, "external=false");
            append(stream, "protected=false");
            append(stream, "delete-pending=false");
            return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
        }

        private static void append(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(int)];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }

        public override string ToString() => nameof(SkinManagedFolderNewRecordPublicationData);
    }
}
