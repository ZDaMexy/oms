// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Game.Skinning.Windows;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A resolver-issued request for one existing external package directory.
    /// </summary>
    /// <remarks>
    /// The absolute path and its segments are sensitive process-local preflight data, not a filesystem capability.
    /// Native capture must reopen every segment from a verified local-volume handle and retain the resulting proof.
    /// </remarks>
    internal sealed class SkinExternalPackageCaptureRequest
    {
        internal string NormalisedAbsolutePath { get; }

        internal char DriveLetter { get; }

        private readonly string[] pathSegments;

        internal IReadOnlyList<string> PathSegments => Array.AsReadOnly(pathSegments);

        internal SkinExternalPackageCaptureRequest(
            string normalisedAbsolutePath,
            char driveLetter,
            IReadOnlyList<string> pathSegments,
            object resolverIssuer)
        {
            if (!SkinFilesystemStorageResolver.IsExternalCaptureRequestIssuer(resolverIssuer))
                throw new InvalidOperationException("Only the storage resolver can issue an external capture request.");

            ArgumentException.ThrowIfNullOrEmpty(normalisedAbsolutePath);
            ArgumentNullException.ThrowIfNull(pathSegments);

            if (!char.IsAsciiLetter(driveLetter) || pathSegments.Count == 0 || pathSegments.Any(string.IsNullOrEmpty))
                throw new ArgumentException("The external capture request is invalid.");

            NormalisedAbsolutePath = normalisedAbsolutePath;
            DriveLetter = char.ToUpperInvariant(driveLetter);
            this.pathSegments = pathSegments.ToArray();
        }

        public override string ToString() => nameof(SkinExternalPackageCaptureRequest);
    }

    /// <summary>
    /// Hard capture budgets in addition to the immutable capsule budgets.
    /// </summary>
    internal sealed class SkinExternalPackageCaptureLimits
    {
        public const int DEFAULT_MAX_AUTHORITY_DEPTH = 64;
        public const int DEFAULT_MAX_HELD_HANDLE_COUNT = 8257;
        public const int DEFAULT_MAX_LOGICAL_MANIFEST_BYTES = 512 * 1024;

        public static SkinExternalPackageCaptureLimits Default { get; } = new SkinExternalPackageCaptureLimits(
            SkinPackageRevisionCapsuleLimits.Default,
            DEFAULT_MAX_AUTHORITY_DEPTH,
            DEFAULT_MAX_HELD_HANDLE_COUNT,
            DEFAULT_MAX_LOGICAL_MANIFEST_BYTES);

        public SkinPackageRevisionCapsuleLimits CapsuleLimits { get; }

        public int MaxAuthorityDepth { get; }

        public int MaxHeldHandleCount { get; }

        /// <summary>
        /// Maximum canonical byte size of the logical manifest. This deliberately leaves headroom in the 1 MiB
        /// canonical mutation journal for intent fields and progress evidence.
        /// </summary>
        public int MaxLogicalManifestBytes { get; }

        public SkinExternalPackageCaptureLimits(
            SkinPackageRevisionCapsuleLimits capsuleLimits,
            int maxAuthorityDepth,
            int maxHeldHandleCount,
            int maxLogicalManifestBytes)
        {
            CapsuleLimits = capsuleLimits ?? throw new ArgumentNullException(nameof(capsuleLimits));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAuthorityDepth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeldHandleCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLogicalManifestBytes);

            MaxAuthorityDepth = maxAuthorityDepth;
            MaxHeldHandleCount = maxHeldHandleCount;
            MaxLogicalManifestBytes = maxLogicalManifestBytes;
        }

        public override string ToString() => nameof(SkinExternalPackageCaptureLimits);
    }

    internal enum SkinExternalPackageLogicalEntryKind : byte
    {
        Directory = 1,
        File = 2,
    }

    /// <summary>
    /// One immutable, ordinal logical-tree entry captured together with an external package capsule.
    /// </summary>
    internal sealed class SkinExternalPackageLogicalManifestEntry
    {
        public string RelativePath { get; }

        public SkinExternalPackageLogicalEntryKind Kind { get; }

        public long Length { get; }

        internal SkinExternalPackageLogicalManifestEntry(
            string relativePath,
            SkinExternalPackageLogicalEntryKind kind,
            long length)
        {
            RelativePath = relativePath;
            Kind = kind;
            Length = length;
        }

        public override string ToString() => $"{nameof(SkinExternalPackageLogicalManifestEntry)}:{Kind}";
    }

    /// <summary>
    /// A bounded immutable commitment to the complete captured logical tree, including explicit empty directories.
    /// </summary>
    internal sealed class SkinExternalPackageLogicalManifest
    {
        public const int CURRENT_VERSION = 1;

        private static readonly byte[] digest_domain = Encoding.ASCII.GetBytes("OMS/SkinExternalPackageLogicalManifest/v1\0");
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        public int Version => CURRENT_VERSION;

        public string ContentRevision { get; }

        public string Digest { get; }

        public IReadOnlyList<SkinExternalPackageLogicalManifestEntry> Entries { get; }

        public int FileCount { get; }

        public long TotalFileBytes { get; }

        public int CanonicalByteCount { get; }

        private SkinExternalPackageLogicalManifest(
            string contentRevision,
            string digest,
            SkinExternalPackageLogicalManifestEntry[] entries,
            int fileCount,
            long totalFileBytes,
            int canonicalByteCount)
        {
            ContentRevision = contentRevision;
            Digest = digest;
            Entries = Array.AsReadOnly(entries);
            FileCount = fileCount;
            TotalFileBytes = totalFileBytes;
            CanonicalByteCount = canonicalByteCount;
        }

        internal static bool TryCreate(
            IReadOnlyList<SkinPackageCapturedEntry?> capturedEntries,
            SkinPackageRevisionCapsule capsule,
            int maxCanonicalBytes,
            out SkinExternalPackageLogicalManifest? manifest)
        {
            ArgumentNullException.ThrowIfNull(capturedEntries);
            ArgumentNullException.ThrowIfNull(capsule);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCanonicalBytes);
            manifest = null;

            var files = capsule.Files.ToDictionary(file => file.ResourceName, StringComparer.OrdinalIgnoreCase);
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<SkinExternalPackageLogicalManifestEntry>(capturedEntries.Count);
            int fileCount = 0;
            long totalFileBytes = 0;

            foreach (SkinPackageCapturedEntry? captured in capturedEntries)
            {
                if (captured == null
                    || !SkinPackageResourceNameValidator.TryNormalise(captured.RelativePath, out string normalisedPath, out _)
                    || !string.Equals(captured.RelativePath, normalisedPath, StringComparison.Ordinal))
                {
                    return false;
                }

                switch (captured.Kind)
                {
                    case SkinPackageCapturedEntryKind.Directory:
                        entries.Add(new SkinExternalPackageLogicalManifestEntry(
                            normalisedPath,
                            SkinExternalPackageLogicalEntryKind.Directory,
                            0));
                        break;

                    case SkinPackageCapturedEntryKind.File:
                        if (!files.TryGetValue(normalisedPath, out SkinPackageFileRevision? revision)
                            || revision.Length != captured.DeclaredLength
                            || !seenFiles.Add(normalisedPath))
                        {
                            return false;
                        }

                        try
                        {
                            fileCount = checked(fileCount + 1);
                            totalFileBytes = checked(totalFileBytes + revision.Length);
                        }
                        catch (OverflowException)
                        {
                            return false;
                        }

                        entries.Add(new SkinExternalPackageLogicalManifestEntry(
                            normalisedPath,
                            SkinExternalPackageLogicalEntryKind.File,
                            revision.Length));
                        break;

                    default:
                        return false;
                }
            }

            if (seenFiles.Count != files.Count)
                return false;

            SkinExternalPackageLogicalManifestEntry[] ordered = entries.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                                                                                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                                                                                .ToArray();
            int canonicalBytes;

            try
            {
                canonicalBytes = checked(digest_domain.Length + sizeof(int) + sizeof(int));
                canonicalBytes = checked(canonicalBytes + sizeof(int) + strict_utf8.GetByteCount(capsule.ContentRevision));

                foreach (SkinExternalPackageLogicalManifestEntry entry in ordered)
                {
                    canonicalBytes = checked(canonicalBytes + sizeof(int) + strict_utf8.GetByteCount(entry.RelativePath));
                    canonicalBytes = checked(canonicalBytes + sizeof(byte) + sizeof(long));
                }
            }
            catch (Exception exception) when (exception is OverflowException or EncoderFallbackException)
            {
                return false;
            }

            if (canonicalBytes > maxCanonicalBytes)
                return false;

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(digest_domain);
            appendInt32(hash, CURRENT_VERSION);
            appendUtf8(hash, capsule.ContentRevision);
            appendInt32(hash, ordered.Length);

            foreach (SkinExternalPackageLogicalManifestEntry entry in ordered)
            {
                appendUtf8(hash, entry.RelativePath);
                appendByte(hash, (byte)entry.Kind);
                appendInt64(hash, entry.Length);
            }

            manifest = new SkinExternalPackageLogicalManifest(
                capsule.ContentRevision,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                ordered,
                fileCount,
                totalFileBytes,
                canonicalBytes);
            return true;
        }

        private static void appendByte(IncrementalHash hash, byte value)
        {
            Span<byte> bytes = stackalloc byte[1];
            bytes[0] = value;
            hash.AppendData(bytes);
        }

        private static void appendInt32(IncrementalHash hash, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        private static void appendInt64(IncrementalHash hash, long value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        private static void appendUtf8(IncrementalHash hash, string value)
        {
            byte[] bytes = strict_utf8.GetBytes(value);
            appendInt32(hash, bytes.Length);
            hash.AppendData(bytes);
        }

        public override string ToString()
            => $"{nameof(SkinExternalPackageLogicalManifest)}:Entries={Entries.Count}:Files={FileCount}";
    }

    /// <summary>
    /// Immutable identity values observed from one held root-to-leaf ancestry.
    /// </summary>
    /// <remarks>
    /// The values alone are not authority. A caller must retain and validate the native session which owns the handles.
    /// </remarks>
    internal sealed class SkinFolderPhysicalAncestryProof
    {
        private static readonly byte[] digest_domain = Encoding.ASCII.GetBytes("OMS/SkinFolderPhysicalAncestryProof/v1\0");
        private readonly SkinManagedFolderPhysicalIdentity[] nodes;

        public IReadOnlyList<SkinManagedFolderPhysicalIdentity> Nodes { get; }

        public SkinManagedFolderPhysicalIdentity RootIdentity => nodes[^1];

        public string Digest { get; }

        public int HeldNodeCount => nodes.Length;

        internal SkinFolderPhysicalAncestryProof(IEnumerable<SkinManagedFolderPhysicalIdentity> identities)
        {
            ArgumentNullException.ThrowIfNull(identities);
            nodes = identities.ToArray();

            if (nodes.Length < 2
                || nodes.Any(identity => !identity.IsUsable)
                || nodes.Any(identity => identity.VolumeSerialNumber != nodes[0].VolumeSerialNumber)
                || nodes.Distinct().Count() != nodes.Length)
            {
                throw new ArgumentException("The physical ancestry proof is invalid.", nameof(identities));
            }

            Nodes = Array.AsReadOnly((SkinManagedFolderPhysicalIdentity[])nodes.Clone());
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(digest_domain);
            appendInt32(hash, nodes.Length);

            foreach (SkinManagedFolderPhysicalIdentity identity in nodes)
            {
                appendUInt64(hash, identity.VolumeSerialNumber);
                appendUInt64(hash, identity.FileIdPart0);
                appendUInt64(hash, identity.FileIdPart1);
            }

            Digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        public bool Overlaps(SkinFolderPhysicalAncestryProof other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (RootIdentity.VolumeSerialNumber != other.RootIdentity.VolumeSerialNumber)
                return false;

            return RootIdentity == other.RootIdentity
                   || Array.IndexOf(nodes, other.RootIdentity) >= 0
                   || Array.IndexOf(other.nodes, RootIdentity) >= 0;
        }

        private static void appendInt32(IncrementalHash hash, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        private static void appendUInt64(IncrementalHash hash, ulong value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        public override string ToString() => $"{nameof(SkinFolderPhysicalAncestryProof)}:Depth={nodes.Length}";
    }

    internal interface ISkinExternalFolderAuthoritySession : IDisposable
    {
        SkinFolderPhysicalAncestryProof PhysicalProof { get; }

        int HeldHandleCount { get; }

        void Validate(CancellationToken cancellationToken = default);
    }

    internal interface ISkinExternalPackageCaptureSession : ISkinExternalFolderAuthoritySession
    {
        SkinExternalPackageLogicalManifest LogicalManifest { get; }

        string PhysicalTreeFingerprint { get; }

        string CaptureFingerprint { get; }

        SkinPackageRevisionCapsule TakeCapsule();
    }

    internal sealed class SkinExternalFolderAuthorityCaptureResult
    {
        public SkinManagedPackageCaptureRejectionReason RejectionReason { get; }

        public ISkinExternalFolderAuthoritySession? Session { get; }

        public bool IsSuccess => Session != null;

        private SkinExternalFolderAuthorityCaptureResult(
            SkinManagedPackageCaptureRejectionReason rejectionReason,
            ISkinExternalFolderAuthoritySession? session)
        {
            RejectionReason = rejectionReason;
            Session = session;
        }

        internal static SkinExternalFolderAuthorityCaptureResult Success(ISkinExternalFolderAuthoritySession session)
            => new SkinExternalFolderAuthorityCaptureResult(
                SkinManagedPackageCaptureRejectionReason.None,
                session ?? throw new ArgumentNullException(nameof(session)));

        internal static SkinExternalFolderAuthorityCaptureResult Reject(SkinManagedPackageCaptureRejectionReason reason)
        {
            if (!Enum.IsDefined(reason) || reason is SkinManagedPackageCaptureRejectionReason.None or SkinManagedPackageCaptureRejectionReason.CapsuleRejected)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinExternalFolderAuthorityCaptureResult(reason, null);
        }

        public override string ToString() => $"{nameof(SkinExternalFolderAuthorityCaptureResult)}:{RejectionReason}";
    }

    internal sealed class SkinExternalPackageCaptureResult
    {
        public SkinManagedPackageCaptureRejectionReason RejectionReason { get; }

        public SkinPackageRevisionCapsuleRejectionReason CapsuleRejectionReason { get; }

        public ISkinExternalPackageCaptureSession? Session { get; }

        public bool IsSuccess => Session != null;

        private SkinExternalPackageCaptureResult(
            SkinManagedPackageCaptureRejectionReason rejectionReason,
            SkinPackageRevisionCapsuleRejectionReason capsuleRejectionReason,
            ISkinExternalPackageCaptureSession? session)
        {
            RejectionReason = rejectionReason;
            CapsuleRejectionReason = capsuleRejectionReason;
            Session = session;
        }

        internal static SkinExternalPackageCaptureResult Success(ISkinExternalPackageCaptureSession session)
            => new SkinExternalPackageCaptureResult(
                SkinManagedPackageCaptureRejectionReason.None,
                SkinPackageRevisionCapsuleRejectionReason.None,
                session ?? throw new ArgumentNullException(nameof(session)));

        internal static SkinExternalPackageCaptureResult Reject(SkinManagedPackageCaptureRejectionReason reason)
        {
            if (!Enum.IsDefined(reason) || reason is SkinManagedPackageCaptureRejectionReason.None or SkinManagedPackageCaptureRejectionReason.CapsuleRejected)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinExternalPackageCaptureResult(
                reason,
                SkinPackageRevisionCapsuleRejectionReason.None,
                null);
        }

        internal static SkinExternalPackageCaptureResult RejectCapsule(SkinPackageRevisionCapsuleRejectionReason reason)
        {
            if (!Enum.IsDefined(reason) || reason == SkinPackageRevisionCapsuleRejectionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinExternalPackageCaptureResult(
                SkinManagedPackageCaptureRejectionReason.CapsuleRejected,
                reason,
                null);
        }

        public override string ToString()
            => $"{nameof(SkinExternalPackageCaptureResult)}:{RejectionReason}:{CapsuleRejectionReason}";
    }

    internal interface ISkinExternalFolderCaptureService
    {
        SkinExternalFolderAuthorityCaptureResult OpenAuthority(
            SkinExternalPackageCaptureRequest? request,
            SkinExternalPackageCaptureLimits? limits = null,
            CancellationToken cancellationToken = default);

        SkinExternalPackageCaptureResult CaptureHeld(
            SkinExternalPackageCaptureRequest? request,
            SkinExternalPackageCaptureLimits? limits = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Platform-safe production entry point for held external-folder proof and package capture.
    /// </summary>
    internal sealed class SkinExternalFolderCaptureService : ISkinExternalFolderCaptureService
    {
        public SkinExternalFolderAuthorityCaptureResult OpenAuthority(
            SkinExternalPackageCaptureRequest? request,
            SkinExternalPackageCaptureLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
                return SkinExternalFolderAuthorityCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                return SkinExternalFolderAuthorityCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.UnsupportedPlatform);

            if (!NativeMethods.HasExpectedLayouts)
                return SkinExternalFolderAuthorityCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            return new WindowsSkinManagedPackageCapture().OpenExternalAuthority(request, limits, cancellationToken);
        }

        public SkinExternalPackageCaptureResult CaptureHeld(
            SkinExternalPackageCaptureRequest? request,
            SkinExternalPackageCaptureLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
                return SkinExternalPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                return SkinExternalPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.UnsupportedPlatform);

            if (!NativeMethods.HasExpectedLayouts)
                return SkinExternalPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            return new WindowsSkinManagedPackageCapture().CaptureExternalHeld(request, limits, cancellationToken);
        }
    }
}
