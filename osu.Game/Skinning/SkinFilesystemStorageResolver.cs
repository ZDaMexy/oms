// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using osu.Framework.Platform;

namespace osu.Game.Skinning
{
    /// <summary>
    /// The storage authority represented by the schema-56 filesystem fields on a <see cref="SkinInfo"/>.
    /// </summary>
    internal enum SkinFilesystemStorageAuthority
    {
        Invalid,
        RealmPackage,
        ManagedFolder,
        ExternalFolder,
    }

    /// <summary>
    /// A stable, non-sensitive reason why a filesystem-backed skin record was rejected.
    /// </summary>
    internal enum SkinFilesystemStorageRejectionReason
    {
        None,
        ExternalMarkerWithoutPath,
        DeletePending,
        ProtectedRecord,
        FixedIdRecord,
        MixedStorageAuthorities,
        ManagedPathMustBeRelative,
        ManagedPathOutsideRoot,
        ManagedRootSelected,
        ExternalPathMustBeAbsolute,
        ExternalVolumeRootSelected,
        ExternalManagedAuthorityConflict,
        UnsupportedPathSyntax,
        DirectoryUnavailable,
        PathIsNotDirectory,
        ReparsePoint,
        PathInspectionFailed,
    }

    /// <summary>
    /// A validated view of the storage authority declared by one <see cref="SkinInfo"/>.
    /// </summary>
    /// <remarks>
    /// This result only reports that a read-only discovery preflight accepted an existing package root. It is not an I/O
    /// or mutation capability: any later read, rename, delete, or import must revalidate through a dedicated no-follow
    /// service. Package entries are also outside this contract and require a separate immutable inventory snapshot before
    /// parsing or decoding resources.
    /// </remarks>
    internal sealed class SkinFilesystemStorageResolution
    {
        public SkinFilesystemStorageAuthority Authority { get; }

        public SkinFilesystemStorageRejectionReason RejectionReason { get; }

        /// <summary>
        /// Whether the storage declaration and, for a folder, the instantaneous path preflight were accepted. This does
        /// not validate package contents, <see cref="SkinInfo.InstantiationInfo"/>, selection eligibility, or a stable
        /// filesystem identity.
        /// </summary>
        public bool IsValid => Authority != SkinFilesystemStorageAuthority.Invalid;

        public bool IsFilesystemBacked => Authority is SkinFilesystemStorageAuthority.ManagedFolder or SkinFilesystemStorageAuthority.ExternalFolder;

        /// <summary>
        /// The lexically normalised absolute package root observed during preflight. This value is process-local, must
        /// never be included in diagnostics, and does not prove a stable physical identity for later mutation.
        /// </summary>
        internal string? NormalisedAbsolutePath { get; }

        /// <summary>
        /// The normalised data-root-relative authority path for a managed package, using forward slashes. It contains a
        /// user-controlled package directory name and must never be included in diagnostics.
        /// </summary>
        internal string? NormalisedManagedRelativePath { get; }

        /// <summary>
        /// A sensitive resolver-issued carrier for the later native managed-package capture. This remains null for
        /// Realm, external and rejected records. It is not a path capability and must never be logged.
        /// </summary>
        internal SkinManagedPackageCaptureRequest? ManagedCaptureRequest { get; }

        internal SkinFilesystemStorageResolution(
            SkinFilesystemStorageAuthority authority,
            SkinFilesystemStorageRejectionReason rejectionReason = SkinFilesystemStorageRejectionReason.None,
            string? absolutePath = null,
            string? managedRelativePath = null,
            SkinManagedPackageCaptureRequest? managedCaptureRequest = null)
        {
            Authority = authority;
            RejectionReason = rejectionReason;
            NormalisedAbsolutePath = absolutePath;
            NormalisedManagedRelativePath = managedRelativePath;
            ManagedCaptureRequest = managedCaptureRequest;
        }

        public override string ToString() => $"{Authority}:{RejectionReason}";
    }

    /// <summary>
    /// Closes the schema-56 path/authority combinations and performs a fail-closed lexical/reparse preflight of an existing folder.
    /// </summary>
    internal static class SkinFilesystemStorageResolver
    {
        internal const string MANAGED_ROOT_DIRECTORY = "chartskin";

        private static readonly object managed_capture_request_issuer = new object();

        private static readonly HashSet<Guid> fixed_skin_ids = new HashSet<Guid>
        {
            SkinInfo.OMS_SKIN,
            SkinInfo.TRIANGLES_SKIN,
            SkinInfo.ARGON_SKIN,
            SkinInfo.ARGON_PRO_SKIN,
            SkinInfo.CLASSIC_SKIN,
            SkinInfo.RETRO_SKIN,
            SkinInfo.RANDOM_SKIN,
        };

        public static SkinFilesystemStorageResolution ResolveExisting(SkinInfo skinInfo, Storage storage)
        {
            ArgumentNullException.ThrowIfNull(skinInfo);
            ArgumentNullException.ThrowIfNull(storage);

            if (string.IsNullOrEmpty(skinInfo.FilesystemStoragePath))
            {
                return skinInfo.IsExternalFilesystemStorage
                    ? reject(SkinFilesystemStorageRejectionReason.ExternalMarkerWithoutPath)
                    : new SkinFilesystemStorageResolution(SkinFilesystemStorageAuthority.RealmPackage);
            }

            string dataStorageRoot;

            try
            {
                dataStorageRoot = storage.GetFullPath(string.Empty);
            }
            catch (Exception exception) when (isStorageResolutionException(exception))
            {
                return reject(SkinFilesystemStorageRejectionReason.PathInspectionFailed);
            }

            return ResolveExisting(skinInfo, dataStorageRoot, PhysicalFilesystemInfoProvider.Instance);
        }

        internal static SkinFilesystemStorageResolution ResolveExisting(
            SkinInfo skinInfo,
            string dataStorageRoot,
            ISkinFilesystemInfoProvider filesystem)
        {
            ArgumentNullException.ThrowIfNull(skinInfo);
            ArgumentException.ThrowIfNullOrEmpty(dataStorageRoot);
            ArgumentNullException.ThrowIfNull(filesystem);

            string? declaredPath = skinInfo.FilesystemStoragePath;

            if (string.IsNullOrEmpty(declaredPath))
            {
                return skinInfo.IsExternalFilesystemStorage
                    ? reject(SkinFilesystemStorageRejectionReason.ExternalMarkerWithoutPath)
                    : new SkinFilesystemStorageResolution(SkinFilesystemStorageAuthority.RealmPackage);
            }

            if (skinInfo.DeletePending)
                return reject(SkinFilesystemStorageRejectionReason.DeletePending);

            if (skinInfo.Protected)
                return reject(SkinFilesystemStorageRejectionReason.ProtectedRecord);

            if (IsFixedSkinId(skinInfo.ID))
                return reject(SkinFilesystemStorageRejectionReason.FixedIdRecord);

            if (skinInfo.Files.Count > 0)
                return reject(SkinFilesystemStorageRejectionReason.MixedStorageAuthorities);

            return skinInfo.IsExternalFilesystemStorage
                ? resolveExternal(declaredPath, dataStorageRoot, filesystem)
                : resolveManaged(declaredPath, dataStorageRoot, filesystem);
        }

        private static SkinFilesystemStorageResolution resolveManaged(
            string declaredPath,
            string dataStorageRoot,
            ISkinFilesystemInfoProvider filesystem)
        {
            if (Path.IsPathRooted(declaredPath))
                return reject(SkinFilesystemStorageRejectionReason.ManagedPathMustBeRelative);

            if (!tryGetPortableSegments(declaredPath, out string[] segments))
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);

            if (segments.Length == 1 && string.Equals(segments[0], MANAGED_ROOT_DIRECTORY, StringComparison.OrdinalIgnoreCase))
                return reject(SkinFilesystemStorageRejectionReason.ManagedRootSelected);

            if (segments.Length != 2 || !string.Equals(segments[0], MANAGED_ROOT_DIRECTORY, StringComparison.OrdinalIgnoreCase))
                return reject(SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot);

            string storageRoot;
            string managedRoot;
            string packageRoot;

            try
            {
                storageRoot = normaliseAbsolutePath(dataStorageRoot);
                managedRoot = normaliseAbsolutePath(Path.Combine(storageRoot, MANAGED_ROOT_DIRECTORY));
                packageRoot = normaliseAbsolutePath(Path.Combine(managedRoot, segments[1]));
            }
            catch (Exception exception) when (isPathException(exception))
            {
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);
            }

            if (!isStrictChildOf(packageRoot, managedRoot))
                return reject(SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot);

            SkinFilesystemStorageRejectionReason inspection = inspectDirectoryChain(storageRoot, packageRoot, filesystem);

            if (inspection != SkinFilesystemStorageRejectionReason.None)
                return reject(inspection);

            return new SkinFilesystemStorageResolution(
                SkinFilesystemStorageAuthority.ManagedFolder,
                absolutePath: packageRoot,
                managedRelativePath: $"{MANAGED_ROOT_DIRECTORY}/{segments[1]}",
                managedCaptureRequest: new SkinManagedPackageCaptureRequest(storageRoot, segments[1], managed_capture_request_issuer));
        }

        internal static bool IsFixedSkinId(Guid id) => fixed_skin_ids.Contains(id);

        private static SkinFilesystemStorageResolution resolveExternal(
            string declaredPath,
            string dataStorageRoot,
            ISkinFilesystemInfoProvider filesystem)
        {
            if (!Path.IsPathFullyQualified(declaredPath))
                return reject(SkinFilesystemStorageRejectionReason.ExternalPathMustBeAbsolute);

            if (!hasSupportedPathSyntax(declaredPath, allowDriveDesignator: true))
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);

            string packageRoot;
            string? volumeRoot;

            try
            {
                packageRoot = normaliseAbsolutePath(declaredPath);
                volumeRoot = Path.GetPathRoot(packageRoot);
            }
            catch (Exception exception) when (isPathException(exception))
            {
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);
            }

            if (string.IsNullOrEmpty(volumeRoot))
                return reject(SkinFilesystemStorageRejectionReason.ExternalPathMustBeAbsolute);

            volumeRoot = normaliseAbsolutePath(volumeRoot);

            if (!isLocalDriveRoot(volumeRoot))
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);

            if (string.Equals(packageRoot, volumeRoot, StringComparison.OrdinalIgnoreCase))
                return reject(SkinFilesystemStorageRejectionReason.ExternalVolumeRootSelected);

            string managedRoot;

            try
            {
                managedRoot = normaliseAbsolutePath(Path.Combine(normaliseAbsolutePath(dataStorageRoot), MANAGED_ROOT_DIRECTORY));
            }
            catch (Exception exception) when (isPathException(exception))
            {
                return reject(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);
            }

            if (string.Equals(packageRoot, managedRoot, StringComparison.OrdinalIgnoreCase)
                || isStrictChildOf(packageRoot, managedRoot)
                || isStrictChildOf(managedRoot, packageRoot))
                return reject(SkinFilesystemStorageRejectionReason.ExternalManagedAuthorityConflict);

            SkinFilesystemStorageRejectionReason inspection = inspectDirectoryChain(volumeRoot, packageRoot, filesystem);

            if (inspection != SkinFilesystemStorageRejectionReason.None)
                return reject(inspection);

            return new SkinFilesystemStorageResolution(
                SkinFilesystemStorageAuthority.ExternalFolder,
                absolutePath: packageRoot);
        }

        private static bool tryGetPortableSegments(string path, out string[] segments)
        {
            segments = Array.Empty<string>();

            if (!hasSupportedPathSyntax(path, allowDriveDesignator: false))
                return false;

            string normalised = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                                    .TrimEnd(Path.DirectorySeparatorChar);

            if (normalised.Length == 0)
                return false;

            segments = normalised.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);

            return true;
        }

        private static bool hasSupportedPathSyntax(string path, bool allowDriveDesignator)
        {
            if (isUncOrDevicePath(path))
                return false;

            string normalised = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                                    .TrimEnd(Path.DirectorySeparatorChar);

            if (normalised.Length == 0)
                return false;

            string[] segments = normalised.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];

                if (string.IsNullOrEmpty(segment))
                    return false;

                if (segment is "." or "..")
                    return false;

                bool isDriveDesignator = allowDriveDesignator
                                         && i == 0
                                         && segment.Length == 2
                                         && segment[1] == ':'
                                         && char.IsAsciiLetter(segment[0]);

                if (isDriveDesignator)
                    continue;

                if (!SkinPackageResourceNameValidator.IsValidWindowsSegment(segment))
                    return false;
            }

            return true;
        }

        private static bool isUncOrDevicePath(string path)
            => path.StartsWith(new string(Path.DirectorySeparatorChar, 2), StringComparison.Ordinal)
               || path.StartsWith(new string(Path.AltDirectorySeparatorChar, 2), StringComparison.Ordinal);

        private static string normaliseAbsolutePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;

            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : Path.TrimEndingDirectorySeparator(fullPath);
        }

        private static bool isLocalDriveRoot(string pathRoot)
            => pathRoot.Length == 3
               && char.IsAsciiLetter(pathRoot[0])
               && pathRoot[1] == ':'
               && pathRoot[2] == Path.DirectorySeparatorChar;

        private static bool isStrictChildOf(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
                return false;

            string rootWithSeparator = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;

            return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static SkinFilesystemStorageRejectionReason inspectDirectoryChain(
            string inspectionRoot,
            string target,
            ISkinFilesystemInfoProvider filesystem)
        {
            var paths = new List<string> { inspectionRoot };

            if (!string.Equals(inspectionRoot, target, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath;

                try
                {
                    relativePath = Path.GetRelativePath(inspectionRoot, target);
                }
                catch (Exception exception) when (isPathException(exception))
                {
                    return SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax;
                }

                if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    return SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot;

                string current = inspectionRoot;

                foreach (string segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, segment);
                    paths.Add(current);
                }
            }

            for (int i = 0; i < paths.Count; i++)
            {
                FileAttributes attributes;

                try
                {
                    attributes = filesystem.GetAttributes(paths[i]);
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    return SkinFilesystemStorageRejectionReason.DirectoryUnavailable;
                }
                catch (Exception exception) when (isPathException(exception))
                {
                    return SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax;
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
                {
                    return SkinFilesystemStorageRejectionReason.PathInspectionFailed;
                }

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    return SkinFilesystemStorageRejectionReason.ReparsePoint;

                if (!attributes.HasFlag(FileAttributes.Directory))
                    return i == paths.Count - 1
                        ? SkinFilesystemStorageRejectionReason.PathIsNotDirectory
                        : SkinFilesystemStorageRejectionReason.PathInspectionFailed;
            }

            return SkinFilesystemStorageRejectionReason.None;
        }

        private static bool isPathException(Exception exception)
            => exception is ArgumentException or NotSupportedException or PathTooLongException;

        private static bool isStorageResolutionException(Exception exception)
            => isPathException(exception) || exception is UnauthorizedAccessException or IOException or SecurityException;

        private static SkinFilesystemStorageResolution reject(SkinFilesystemStorageRejectionReason reason)
            => new SkinFilesystemStorageResolution(SkinFilesystemStorageAuthority.Invalid, reason);

        internal static bool IsManagedCaptureRequestIssuer(object? candidate)
            => ReferenceEquals(candidate, managed_capture_request_issuer);

        internal interface ISkinFilesystemInfoProvider
        {
            FileAttributes GetAttributes(string path);
        }

        private sealed class PhysicalFilesystemInfoProvider : ISkinFilesystemInfoProvider
        {
            public static PhysicalFilesystemInfoProvider Instance { get; } = new PhysicalFilesystemInfoProvider();

            public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
        }
    }
}
