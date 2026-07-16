// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.Skinning
{
    internal enum SkinPackageCapturedEntryKind
    {
        Directory,
        File,
    }

    /// <summary>
    /// One path and, for a file, one stable read callback supplied by a future package capture service.
    /// </summary>
    /// <remarks>
    /// This type contains no filesystem path or authority. The producer must keep the returned stream stable for the
    /// duration of a factory call; the factory only promises that the resulting capsule owns a defensive content copy.
    /// </remarks>
    internal sealed class SkinPackageCapturedEntry
    {
        public SkinPackageCapturedEntryKind Kind { get; }

        internal string? RelativePath { get; }

        internal long DeclaredLength { get; }

        private readonly Func<Stream?>? openRead;

        internal SkinPackageCapturedEntry(
            SkinPackageCapturedEntryKind kind,
            string? relativePath,
            long declaredLength = 0,
            Func<Stream?>? openRead = null)
        {
            Kind = kind;
            RelativePath = relativePath;
            DeclaredLength = declaredLength;
            this.openRead = openRead;
        }

        public static SkinPackageCapturedEntry CreateDirectory(string? relativePath)
            => new SkinPackageCapturedEntry(SkinPackageCapturedEntryKind.Directory, relativePath);

        public static SkinPackageCapturedEntry CreateFile(string? relativePath, byte[]? content)
            => new SkinPackageCapturedEntry(
                SkinPackageCapturedEntryKind.File,
                relativePath,
                content?.LongLength ?? 0,
                content == null ? null : () => new MemoryStream(content, writable: false));

        public static SkinPackageCapturedEntry CreateFile(string? relativePath, long declaredLength, Func<Stream?>? openRead)
            => new SkinPackageCapturedEntry(SkinPackageCapturedEntryKind.File, relativePath, declaredLength, openRead);

        internal Stream? OpenRead() => openRead?.Invoke();

        public override string ToString() => $"{nameof(SkinPackageCapturedEntry)}:{Kind}";
    }

    /// <summary>
    /// Resource and raw-byte budgets applied before a package revision capsule can own any content.
    /// </summary>
    internal sealed class SkinPackageRevisionCapsuleLimits
    {
        public static SkinPackageRevisionCapsuleLimits Default { get; } = new SkinPackageRevisionCapsuleLimits(
            maxEntryCount: 8192,
            maxFileCount: 8192,
            maxDepth: 32,
            maxResourceNameLength: 512,
            maxFileBytes: 64L * 1024 * 1024,
            maxPackageBytes: 512L * 1024 * 1024);

        public int MaxEntryCount { get; }
        public int MaxFileCount { get; }
        public int MaxDepth { get; }
        public int MaxResourceNameLength { get; }
        public long MaxFileBytes { get; }
        public long MaxPackageBytes { get; }

        public SkinPackageRevisionCapsuleLimits(
            int maxEntryCount,
            int maxFileCount,
            int maxDepth,
            int maxResourceNameLength,
            long maxFileBytes,
            long maxPackageBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntryCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResourceNameLength);
            ArgumentOutOfRangeException.ThrowIfNegative(maxFileBytes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maxFileBytes, int.MaxValue);
            ArgumentOutOfRangeException.ThrowIfNegative(maxPackageBytes);

            MaxEntryCount = maxEntryCount;
            MaxFileCount = maxFileCount;
            MaxDepth = maxDepth;
            MaxResourceNameLength = maxResourceNameLength;
            MaxFileBytes = maxFileBytes;
            MaxPackageBytes = maxPackageBytes;
        }

        public override string ToString() => nameof(SkinPackageRevisionCapsuleLimits);
    }

    internal enum SkinPackageRevisionCapsuleRejectionReason
    {
        None,
        EmptyPackage,
        UnsupportedEntryKind,
        InvalidResourceName,
        ResourceNameBudgetExceeded,
        DepthBudgetExceeded,
        DuplicateEntryPath,
        PathTypeConflict,
        EntryCountBudgetExceeded,
        FileCountBudgetExceeded,
        InvalidDeclaredLength,
        FileByteBudgetExceeded,
        PackageByteBudgetExceeded,
        SourceUnavailable,
        SourceNotReadable,
        SourceLengthMismatch,
        SourceReadFailed,
    }

    /// <summary>
    /// The immutable identity of one file in a package revision.
    /// </summary>
    internal sealed class SkinPackageFileRevision
    {
        private readonly byte[] contentHashBytes;

        internal string ResourceName { get; }
        internal string ContentHash { get; }

        public long Length { get; }

        internal SkinPackageFileRevision(string resourceName, long length, byte[] contentHash)
        {
            ResourceName = resourceName;
            Length = length;
            contentHashBytes = (byte[])contentHash.Clone();
            ContentHash = Convert.ToHexString(contentHash);
        }

        internal void AppendContentHashTo(IncrementalHash hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            hash.AppendData(contentHashBytes);
        }

        public override string ToString() => $"{nameof(SkinPackageFileRevision)}:Length{Length}";
    }

    internal sealed class SkinPackageRevisionCapsuleCreationResult
    {
        public SkinPackageRevisionCapsuleRejectionReason RejectionReason { get; }

        public bool IsSuccess => Capsule != null;

        public SkinPackageRevisionCapsule? Capsule { get; }

        private SkinPackageRevisionCapsuleCreationResult(
            SkinPackageRevisionCapsuleRejectionReason rejectionReason,
            SkinPackageRevisionCapsule? capsule)
        {
            RejectionReason = rejectionReason;
            Capsule = capsule;
        }

        internal static SkinPackageRevisionCapsuleCreationResult Success(SkinPackageRevisionCapsule capsule)
            => new SkinPackageRevisionCapsuleCreationResult(
                SkinPackageRevisionCapsuleRejectionReason.None,
                capsule ?? throw new ArgumentNullException(nameof(capsule)));

        internal static SkinPackageRevisionCapsuleCreationResult Reject(SkinPackageRevisionCapsuleRejectionReason reason)
        {
            if (reason == SkinPackageRevisionCapsuleRejectionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinPackageRevisionCapsuleCreationResult(reason, null);
        }

        public override string ToString() => $"{nameof(SkinPackageRevisionCapsuleCreationResult)}:{RejectionReason}";
    }

    /// <summary>
    /// Owns a complete defensive byte snapshot and canonical content revision for one package.
    /// </summary>
    /// <remarks>
    /// The capsule does not know where the bytes came from and therefore proves no filesystem containment, no-follow,
    /// physical identity, or capture-time atomicity. Those are requirements on the future native capture service.
    /// </remarks>
    internal sealed class SkinPackageRevisionCapsule : IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, byte[]> resources;
        private readonly ReadOnlyCollection<string> resourceNames;
        private bool disposed;

        internal string ContentRevision { get; }

        public IReadOnlyList<SkinPackageFileRevision> Files { get; }

        public int FileCount => Files.Count;

        public long TotalBytes { get; }

        /// <remarks>
        /// <paramref name="resources"/> transfers exclusive ownership. The caller must not retain or mutate the
        /// dictionary or any contained buffer after construction.
        /// </remarks>
        internal SkinPackageRevisionCapsule(
            Dictionary<string, byte[]> resources,
            SkinPackageFileRevision[] files,
            string contentRevision,
            long totalBytes)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            ArgumentNullException.ThrowIfNull(files);
            ArgumentException.ThrowIfNullOrEmpty(contentRevision);

            resourceNames = Array.AsReadOnly(files.Select(file => file.ResourceName).ToArray());
            Files = Array.AsReadOnly((SkinPackageFileRevision[])files.Clone());
            ContentRevision = contentRevision;
            TotalBytes = totalBytes;
        }

        public IResourceStore<byte[]> CreateResourceView()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return new CapsuleResourceView(this);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;

                foreach (byte[] content in resources.Values)
                    CryptographicOperations.ZeroMemory(content);

                resources.Clear();
            }
        }

        public override string ToString() => $"{nameof(SkinPackageRevisionCapsule)}:Files{FileCount}:Bytes{TotalBytes}";

        private byte[]? get(string name)
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
            }

            if (!SkinPackageResourceNameValidator.TryNormalise(name, out string normalisedName, out _))
                return null;

            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return resources.TryGetValue(normalisedName, out byte[]? content)
                    ? (byte[])content.Clone()
                    : null;
            }
        }

        private IEnumerable<string> getAvailableResources()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return resourceNames;
            }
        }

        private sealed class CapsuleResourceView : IResourceStore<byte[]>
        {
            private readonly SkinPackageRevisionCapsule capsule;

            public CapsuleResourceView(SkinPackageRevisionCapsule capsule)
            {
                this.capsule = capsule;
            }

            public byte[] Get(string name) => capsule.get(name)!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<byte[]>(cancellationToken);

                return Task.FromResult(capsule.get(name)!);
            }

            public Stream? GetStream(string name)
            {
                byte[]? content = capsule.get(name);
                return content == null ? null : new MemoryStream(content, writable: false);
            }

            public IEnumerable<string> GetAvailableResources() => capsule.getAvailableResources();

            public void Dispose()
            {
                // Views never own the capsule. The future active publication is the single capsule owner.
            }
        }
    }

    internal static class SkinPackageRevisionCapsuleFactory
    {
        private static readonly byte[] revision_domain = Encoding.ASCII.GetBytes("OMS.SkinPackage.ContentRevision.v1\0");

        public static SkinPackageRevisionCapsuleCreationResult Create(
            IReadOnlyList<SkinPackageCapturedEntry?> entries,
            SkinPackageRevisionCapsuleLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);
            limits ??= SkinPackageRevisionCapsuleLimits.Default;
            cancellationToken.ThrowIfCancellationRequested();

            if (entries.Count > limits.MaxEntryCount)
                return reject(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);

            var validatedEntries = new List<ValidatedEntry>(entries.Count);
            var explicitKinds = new Dictionary<string, SkinPackageCapturedEntryKind>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SkinPackageCapturedEntry? entry = entries[i];

                if (entry == null || !Enum.IsDefined(entry.Kind))
                    return reject(SkinPackageRevisionCapsuleRejectionReason.UnsupportedEntryKind);

                if (!SkinPackageResourceNameValidator.TryNormalise(entry.RelativePath, out string normalisedPath, out int depth))
                    return reject(SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName);

                if (normalisedPath.Length > limits.MaxResourceNameLength)
                    return reject(SkinPackageRevisionCapsuleRejectionReason.ResourceNameBudgetExceeded);

                if (depth > limits.MaxDepth)
                    return reject(SkinPackageRevisionCapsuleRejectionReason.DepthBudgetExceeded);

                if (explicitKinds.TryGetValue(normalisedPath, out SkinPackageCapturedEntryKind existingKind))
                {
                    return reject(existingKind == entry.Kind
                        ? SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath
                        : SkinPackageRevisionCapsuleRejectionReason.PathTypeConflict);
                }

                explicitKinds.Add(normalisedPath, entry.Kind);
                validatedEntries.Add(new ValidatedEntry(entry, normalisedPath, depth));
            }

            var effectiveEntries = new HashSet<string>(explicitKinds.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (ValidatedEntry entry in validatedEntries)
            {
                string? parent = getParentResourceName(entry.NormalisedPath);

                while (parent != null)
                {
                    if (explicitKinds.TryGetValue(parent, out SkinPackageCapturedEntryKind parentKind)
                        && parentKind == SkinPackageCapturedEntryKind.File)
                    {
                        return reject(SkinPackageRevisionCapsuleRejectionReason.PathTypeConflict);
                    }

                    if (effectiveEntries.Add(parent) && effectiveEntries.Count > limits.MaxEntryCount)
                        return reject(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);

                    parent = getParentResourceName(parent);
                }
            }

            if (effectiveEntries.Count > limits.MaxEntryCount)
                return reject(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);

            ValidatedEntry[] files = validatedEntries
                                     .Where(entry => entry.Source.Kind == SkinPackageCapturedEntryKind.File)
                                     .OrderBy(entry => entry.NormalisedPath, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(entry => entry.NormalisedPath, StringComparer.Ordinal)
                                     .ToArray();

            if (files.Length == 0)
                return reject(SkinPackageRevisionCapsuleRejectionReason.EmptyPackage);

            if (files.Length > limits.MaxFileCount)
                return reject(SkinPackageRevisionCapsuleRejectionReason.FileCountBudgetExceeded);

            long totalBytes = 0;

            foreach (ValidatedEntry file in files)
            {
                if (file.Source.DeclaredLength < 0)
                    return reject(SkinPackageRevisionCapsuleRejectionReason.InvalidDeclaredLength);

                if (file.Source.DeclaredLength > limits.MaxFileBytes)
                    return reject(SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded);

                try
                {
                    totalBytes = checked(totalBytes + file.Source.DeclaredLength);
                }
                catch (OverflowException)
                {
                    return reject(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded);
                }

                if (totalBytes > limits.MaxPackageBytes)
                    return reject(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded);
            }

            var ownedResources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var fileRevisions = new List<SkinPackageFileRevision>(files.Length);
            bool ownershipTransferred = false;

            try
            {
                foreach (ValidatedEntry file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    SkinPackageRevisionCapsuleRejectionReason readRejection = tryReadContent(
                        file.Source,
                        cancellationToken,
                        out byte[]? content);

                    if (readRejection != SkinPackageRevisionCapsuleRejectionReason.None)
                        return reject(readRejection);

                    bool contentTransferred = false;

                    try
                    {
                        byte[] contentHash = SHA256.HashData(content!);

                        try
                        {
                            var fileRevision = new SkinPackageFileRevision(file.NormalisedPath, content!.LongLength, contentHash);
                            ownedResources.Add(file.NormalisedPath, content);
                            contentTransferred = true;
                            fileRevisions.Add(fileRevision);
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(contentHash);
                        }
                    }
                    finally
                    {
                        if (!contentTransferred)
                            CryptographicOperations.ZeroMemory(content!);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                SkinPackageFileRevision[] revisionFiles = fileRevisions.ToArray();
                string contentRevision = computeContentRevision(revisionFiles, cancellationToken);
                var capsule = new SkinPackageRevisionCapsule(ownedResources, revisionFiles, contentRevision, totalBytes);
                ownershipTransferred = true;
                return SkinPackageRevisionCapsuleCreationResult.Success(capsule);
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    foreach (byte[] content in ownedResources.Values)
                        CryptographicOperations.ZeroMemory(content);

                    ownedResources.Clear();
                }
            }
        }

        private static SkinPackageRevisionCapsuleRejectionReason tryReadContent(
            SkinPackageCapturedEntry source,
            CancellationToken cancellationToken,
            out byte[]? content)
        {
            content = null;
            Stream? stream;

            try
            {
                stream = source.OpenRead();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (isExpectedSourceException(exception))
            {
                return SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed;
            }

            if (stream == null)
                return SkinPackageRevisionCapsuleRejectionReason.SourceUnavailable;

            byte[]? buffer = null;
            bool success = false;

            try
            {
                try
                {
                    using (stream)
                    {
                        buffer = new byte[(int)source.DeclaredLength];

                        if (!stream.CanRead)
                            return SkinPackageRevisionCapsuleRejectionReason.SourceNotReadable;

                        int offset = 0;

                        while (offset < buffer.Length)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int remaining = buffer.Length - offset;
                            int read = stream.Read(buffer, offset, remaining);

                            if (read <= 0 || read > remaining)
                                return SkinPackageRevisionCapsuleRejectionReason.SourceLengthMismatch;

                            offset += read;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        if (stream.ReadByte() != -1)
                            return SkinPackageRevisionCapsuleRejectionReason.SourceLengthMismatch;

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (isExpectedSourceException(exception))
                {
                    return SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed;
                }

                content = buffer;
                success = true;
                return SkinPackageRevisionCapsuleRejectionReason.None;
            }
            finally
            {
                if (!success && buffer != null)
                    CryptographicOperations.ZeroMemory(buffer);
            }
        }

        private static string computeContentRevision(
            IReadOnlyList<SkinPackageFileRevision> files,
            CancellationToken cancellationToken)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(revision_domain);
            appendInt32(hash, files.Count);

            foreach (SkinPackageFileRevision file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] name = Encoding.UTF8.GetBytes(file.ResourceName);
                appendInt32(hash, name.Length);
                hash.AppendData(name);
                appendInt64(hash, file.Length);
                file.AppendContentHashTo(hash);
            }

            return Convert.ToHexString(hash.GetHashAndReset());
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

        private static bool isExpectedSourceException(Exception exception)
            => exception is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException or SecurityException;

        private static string? getParentResourceName(string resourceName)
        {
            int separator = resourceName.LastIndexOf('/');
            return separator < 0 ? null : resourceName[..separator];
        }

        private static SkinPackageRevisionCapsuleCreationResult reject(SkinPackageRevisionCapsuleRejectionReason reason)
            => SkinPackageRevisionCapsuleCreationResult.Reject(reason);

        private readonly record struct ValidatedEntry(
            SkinPackageCapturedEntry Source,
            string NormalisedPath,
            int Depth);
    }

    internal static class SkinPackageResourceNameValidator
    {
        public static bool TryNormalise(string? resourceName, out string normalisedName, out int depth)
        {
            normalisedName = string.Empty;
            depth = 0;

            if (string.IsNullOrEmpty(resourceName))
                return false;

            string normalised;

            try
            {
                normalised = resourceName.Replace('\\', '/').Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (normalised.StartsWith('/') || normalised.EndsWith('/'))
                return false;

            string[] segments = normalised.Split('/', StringSplitOptions.None);

            if (segments.Any(segment => !IsValidWindowsSegment(segment)))
                return false;

            normalisedName = string.Join('/', segments);
            depth = segments.Length;
            return true;
        }

        internal static bool IsValidWindowsSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment)
                || segment is "." or ".."
                || segment.EndsWith(' ')
                || segment.EndsWith('.')
                || isReservedWindowsDeviceName(segment))
            {
                return false;
            }

            foreach (char character in segment)
            {
                if (character < ' '
                    || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool isReservedWindowsDeviceName(string segment)
        {
            string stem = segment.Split('.')[0];

            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (stem.Length != 4 || !isReservedDeviceNumber(stem[3]))
                return false;

            return stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                   || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool isReservedDeviceNumber(char character)
            => character is >= '1' and <= '9' or '¹' or '²' or '³';
    }
}
