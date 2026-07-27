// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace osu.Game.Skinning.Windows
{
    /// <summary>
    /// Holds the native authority chain from a local volume root through the OMS data root to <c>chartskin</c>.
    /// </summary>
    /// <remarks>
    /// Direct-child discovery and capture share the same held <c>chartskin</c> handle. A caller must validate the final
    /// inventory before treating absence from <see cref="BaselineEntries"/> as authoritative.
    /// </remarks>
    [SupportedOSPlatform("windows10.0.16299")]
    internal sealed class WindowsSkinManagedAuthoritySession : IDisposable
    {
        private const int max_authority_directory_entries = 65536;
        private const int max_windows_component_characters = 255;

        private readonly IWindowsSkinPackageCaptureFileSystem fileSystem;
        private readonly WindowsSkinManagedPackageCapture packageCapture;
        private readonly List<IWindowsSkinPackageCaptureHandle> handles;
        private readonly List<NodeRecord> authorityNodes;
        private readonly List<AuthorityLinkRecord> authorityLinks;
        private readonly IWindowsSkinPackageCaptureHandle dataRoot;
        private readonly IWindowsSkinPackageCaptureHandle managedRoot;
        private readonly WindowsSkinPackageEntryMetadata managedRootMetadata;
        private readonly WindowsSkinPackageDirectoryEntry[] baselineEntries;
        private bool disposed;

        public IReadOnlyList<WindowsSkinPackageDirectoryEntry> BaselineEntries { get; }

        private WindowsSkinManagedAuthoritySession(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            List<IWindowsSkinPackageCaptureHandle> handles,
            List<NodeRecord> authorityNodes,
            List<AuthorityLinkRecord> authorityLinks,
            IWindowsSkinPackageCaptureHandle dataRoot,
            IWindowsSkinPackageCaptureHandle managedRoot,
            WindowsSkinPackageEntryMetadata managedRootMetadata,
            WindowsSkinPackageDirectoryEntry[] baselineEntries)
        {
            this.fileSystem = fileSystem;
            packageCapture = new WindowsSkinManagedPackageCapture(fileSystem);
            this.handles = handles;
            this.authorityNodes = authorityNodes;
            this.authorityLinks = authorityLinks;
            this.dataRoot = dataRoot;
            this.managedRoot = managedRoot;
            this.managedRootMetadata = managedRootMetadata;
            this.baselineEntries = baselineEntries;
            BaselineEntries = Array.AsReadOnly(baselineEntries);
        }

        public static WindowsSkinManagedAuthoritySession Open(
            string dataRootAbsolutePath,
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dataRootAbsolutePath);
            ArgumentNullException.ThrowIfNull(fileSystem);
            cancellationToken.ThrowIfCancellationRequested();

            if (!tryParseDataRoot(dataRootAbsolutePath, out char driveLetter, out string[] dataRootSegments))
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            var handles = new List<IWindowsSkinPackageCaptureHandle>();

            try
            {
                IWindowsSkinPackageCaptureHandle volumeRoot = own(fileSystem.OpenLocalVolumeRoot(driveLetter), handles);
                WindowsSkinPackageEntryMetadata volumeRootMetadata = queryStableRoot(fileSystem, volumeRoot);
                var authorityNodes = new List<NodeRecord>
                {
                    new NodeRecord(volumeRoot, volumeRootMetadata),
                };
                var authorityLinks = new List<AuthorityLinkRecord>();
                IWindowsSkinPackageCaptureHandle current = volumeRoot;

                foreach (string segment in dataRootSegments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    OpenedDirectory opened = openExpectedDirectory(
                        fileSystem,
                        current,
                        segment,
                        WindowsSkinPackageOpenMode.AuthorityDirectory,
                        SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                        cancellationToken,
                        handles);
                    authorityLinks.Add(new AuthorityLinkRecord(
                        current,
                        opened.CanonicalName,
                        opened.Metadata,
                        WindowsSkinPackageOpenMode.AuthorityDirectory));
                    current = opened.Handle;
                    authorityNodes.Add(new NodeRecord(current, opened.Metadata));
                }

                cancellationToken.ThrowIfCancellationRequested();
                OpenedDirectory managed = openExpectedDirectory(
                    fileSystem,
                    current,
                    SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY,
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);
                authorityLinks.Add(new AuthorityLinkRecord(
                    current,
                    managed.CanonicalName,
                    managed.Metadata,
                    WindowsSkinPackageOpenMode.AuthorityDirectory));
                authorityNodes.Add(new NodeRecord(managed.Handle, managed.Metadata));

                WindowsSkinPackageDirectoryEntry[] baseline = canonicaliseDirectoryEntries(
                    getDirectoryEntries(fileSystem, managed.Handle, max_authority_directory_entries, cancellationToken));

                return new WindowsSkinManagedAuthoritySession(
                    fileSystem,
                    handles,
                    authorityNodes,
                    authorityLinks,
                    current,
                    managed.Handle,
                    managed.Metadata,
                    baseline);
            }
            catch
            {
                disposeHandles(handles);
                throw;
            }
        }

        public SkinManagedPackageCaptureResult CaptureObservedChild(
            WindowsSkinPackageDirectoryEntry candidate,
            SkinPackageRevisionCapsuleLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(candidate);

            if (!baselineEntries.Contains(candidate))
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            return packageCapture.CaptureObservedChild(managedRoot, candidate, limits, cancellationToken);
        }

        internal SkinManagedFolderPhysicalIdentity ManagedRootIdentity
            => toMutationIdentity(managedRootMetadata.Identity);

        internal SkinManagedFolderPhysicalIdentity CaptureExistingMutationSource(
            string managedRelativePath,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!SkinManagedFolderPath.TryNormalise(managedRelativePath, out string normalisedPath)
                || !string.Equals(managedRelativePath, normalisedPath, StringComparison.Ordinal))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            string childName = managedRelativePath[(managedRelativePath.IndexOf('/') + 1)..];
            WindowsSkinPackageDirectoryEntry[] matches = baselineEntries.Where(entry => namesEqual(entry.Name, childName)).ToArray();

            if (matches.Length == 0)
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageUnavailable);

            if (matches.Length != 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            WindowsSkinPackageDirectoryEntry candidate = matches[0];

            if (candidate.Metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (candidate.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                || !candidate.Metadata.Identity.IsUsable
                || candidate.Metadata.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);
            }

            OpenedDirectory opened = openExpectedDirectory(
                fileSystem,
                managedRoot,
                candidate.Name,
                WindowsSkinPackageOpenMode.MutationSourceDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);
            authorityNodes.Add(new NodeRecord(opened.Handle, opened.Metadata));
            ValidateCompleteAndStable(cancellationToken);
            return toMutationIdentity(opened.Metadata.Identity);
        }

        internal SkinManagedFolderTargetNameSlot CaptureAbsentMutationTargetNameSlot(
            string managedRelativePath,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!SkinManagedFolderPath.TryNormalise(managedRelativePath, out string normalisedPath)
                || !string.Equals(managedRelativePath, normalisedPath, StringComparison.Ordinal))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            string childName = managedRelativePath[(managedRelativePath.IndexOf('/') + 1)..];

            if (baselineEntries.Any(entry => namesEqual(entry.Name, childName)))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            try
            {
                using IWindowsSkinPackageCaptureHandle unexpected = fileSystem.OpenChildNoFollow(
                    managedRoot,
                    childName,
                    WindowsSkinPackageOpenMode.CapturedDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.PackageUnavailable)
            {
            }

            ValidateCompleteAndStable(cancellationToken);
            return new SkinManagedFolderTargetNameSlot(managedRelativePath, ManagedRootIdentity);
        }

        internal SkinManagedFolderStagedSourceCapture CaptureStagedMutationSource(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            OpenedDirectory stagingRoot = openExpectedDirectory(
                fileSystem,
                dataRoot,
                "skin-mutation-staging",
                WindowsSkinPackageOpenMode.AuthorityDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);

            if (!string.Equals(stagingRoot.CanonicalName, "skin-mutation-staging", StringComparison.Ordinal))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            authorityLinks.Add(new AuthorityLinkRecord(
                dataRoot,
                stagingRoot.CanonicalName,
                stagingRoot.Metadata,
                WindowsSkinPackageOpenMode.AuthorityDirectory));
            authorityNodes.Add(new NodeRecord(stagingRoot.Handle, stagingRoot.Metadata));

            string expectedSourceName = operationId.ToString("N");
            OpenedDirectory source = openExpectedDirectory(
                fileSystem,
                stagingRoot.Handle,
                expectedSourceName,
                WindowsSkinPackageOpenMode.MutationSourceDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);

            if (!string.Equals(source.CanonicalName, expectedSourceName, StringComparison.Ordinal))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            authorityLinks.Add(new AuthorityLinkRecord(
                stagingRoot.Handle,
                source.CanonicalName,
                source.Metadata,
                WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory));
            authorityNodes.Add(new NodeRecord(source.Handle, source.Metadata));

            ValidateCompleteAndStable(cancellationToken);
            return new SkinManagedFolderStagedSourceCapture(
                toMutationIdentity(stagingRoot.Metadata.Identity),
                toMutationIdentity(source.Metadata.Identity));
        }

        /// <summary>
        /// Verifies that the authority chain is still linked to the held identities and that the complete direct-child
        /// inventory is byte-for-byte metadata-equivalent to the baseline enumeration.
        /// </summary>
        public void ValidateCompleteAndStable(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);

            WindowsSkinPackageDirectoryEntry[] current;

            try
            {
                current = canonicaliseDirectoryEntries(
                    getDirectoryEntries(fileSystem, managedRoot, baselineEntries.Length, cancellationToken));
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            if (!baselineEntries.SequenceEqual(current, DirectoryEntryComparer.Instance))
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            disposeHandles(handles);
        }

        internal static bool TryGetManagedRelativePath(string directChildName, out string managedRelativePath)
        {
            managedRelativePath = string.Empty;

            if (!isValidRequestSegment(directChildName)
                || !SkinPackageResourceNameValidator.TryNormalise(directChildName, out string normalisedName, out int depth)
                || depth != 1)
            {
                return false;
            }

            managedRelativePath = $"{SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY}/{normalisedName}";
            return true;
        }

        private static OpenedDirectory openExpectedDirectory(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            IWindowsSkinPackageCaptureHandle parent,
            string requestedName,
            WindowsSkinPackageOpenMode mode,
            SkinManagedPackageCaptureRejectionReason unavailableReason,
            CancellationToken cancellationToken,
            List<IWindowsSkinPackageCaptureHandle> handles)
        {
            if (!isValidRequestSegment(requestedName))
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            string normalisedRequestedName;

            try
            {
                normalisedRequestedName = requestedName.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            WindowsSkinPackageDirectoryEntry[] matches = getDirectoryEntries(fileSystem, parent, max_authority_directory_entries, cancellationToken)
                                                        .Where(entry => namesEqual(entry.Name, normalisedRequestedName))
                                                        .ToArray();

            if (matches.Length == 0)
            {
                try
                {
                    using IWindowsSkinPackageCaptureHandle alias = fileSystem.OpenChildNoFollow(parent, requestedName, mode, unavailableReason);
                    throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (exception.RejectionReason == unavailableReason)
                {
                    throw;
                }
            }

            if (matches.Length != 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            WindowsSkinPackageDirectoryEntry candidate = matches[0];

            if (candidate.Metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (candidate.Metadata.Kind != WindowsSkinPackageEntryKind.Directory)
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

            IWindowsSkinPackageCaptureHandle opened = own(
                fileSystem.OpenChildNoFollow(parent, candidate.Name, mode, unavailableReason),
                handles);
            WindowsSkinPackageEntryMetadata openedMetadata = fileSystem.QueryMetadata(opened);
            validateOpenedEntry(candidate.Metadata, openedMetadata);
            return new OpenedDirectory(opened, openedMetadata, candidate.Name);
        }

        private static WindowsSkinPackageEntryMetadata queryStableRoot(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            IWindowsSkinPackageCaptureHandle root)
        {
            WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(root);

            if (metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (metadata.Kind != WindowsSkinPackageEntryKind.Directory || !metadata.Identity.IsUsable || metadata.DeletePending)
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

            return metadata;
        }

        private static void validateAuthorityNodes(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            IReadOnlyList<NodeRecord> nodes,
            CancellationToken cancellationToken)
        {
            foreach (NodeRecord node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WindowsSkinPackageEntryMetadata current = fileSystem.QueryMetadata(node.Handle);

                if (current.IsReparsePoint)
                    throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                if (current.Kind != WindowsSkinPackageEntryKind.Directory
                    || current.Identity != node.Baseline.Identity
                    || current.DeletePending)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }
            }
        }

        private static void validateAuthorityLinks(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            IReadOnlyList<AuthorityLinkRecord> links,
            CancellationToken cancellationToken)
        {
            foreach (AuthorityLinkRecord link in links)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WindowsSkinPackageDirectoryEntry[] matches = getDirectoryEntries(fileSystem, link.Parent, max_authority_directory_entries, cancellationToken)
                                                            .Where(entry => namesEqual(entry.Name, link.CanonicalName))
                                                            .ToArray();

                if (matches.Length > 1)
                    throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

                if (matches.Length != 1 || matches[0].Metadata.Identity != link.Baseline.Identity)
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);

                using IWindowsSkinPackageCaptureHandle reopened = fileSystem.OpenChildNoFollow(
                    link.Parent,
                    link.CanonicalName,
                    link.VerificationMode,
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                WindowsSkinPackageEntryMetadata reopenedMetadata = fileSystem.QueryMetadata(reopened);

                if (reopenedMetadata.IsReparsePoint)
                    throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                if (reopenedMetadata.Kind != WindowsSkinPackageEntryKind.Directory
                    || reopenedMetadata.Identity != link.Baseline.Identity
                    || reopenedMetadata.DeletePending)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }
            }
        }

        private static IReadOnlyList<WindowsSkinPackageDirectoryEntry> getDirectoryEntries(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            IWindowsSkinPackageCaptureHandle directory,
            int maxEntries,
            CancellationToken cancellationToken)
            => fileSystem.Enumerate(directory, maxEntries, cancellationToken)
                         .Where(entry => entry.Name is not "." and not "..")
                         .ToArray();

        private static WindowsSkinPackageDirectoryEntry[] canonicaliseDirectoryEntries(IEnumerable<WindowsSkinPackageDirectoryEntry> entries)
            => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                      .ToArray();

        private static bool namesEqual(string candidate, string requestedName)
        {
            try
            {
                return string.Equals(
                    candidate.Normalize(NormalizationForm.FormC),
                    requestedName.Normalize(NormalizationForm.FormC),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void validateOpenedEntry(
            WindowsSkinPackageEntryMetadata enumerated,
            WindowsSkinPackageEntryMetadata opened)
        {
            if (opened.IsReparsePoint || enumerated.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (!opened.Identity.IsUsable
                || opened.DeletePending
                || opened.Identity != enumerated.Identity
                || opened.Kind != enumerated.Kind
                || opened.FileAttributes != enumerated.FileAttributes
                || opened.ReparseTag != enumerated.ReparseTag
                || (opened.Kind == WindowsSkinPackageEntryKind.File
                    && (opened.Length != enumerated.Length
                        || opened.CreationTime != enumerated.CreationTime
                        || opened.LastWriteTime != enumerated.LastWriteTime
                        || opened.ChangeTime != enumerated.ChangeTime)))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            }
        }

        private static bool tryParseDataRoot(string path, out char driveLetter, out string[] segments)
        {
            driveLetter = default;
            segments = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(path)
                || path.Length < 3
                || !char.IsAsciiLetter(path[0])
                || path[1] != ':'
                || path[2] is not ('\\' or '/'))
            {
                return false;
            }

            string normalised = path.Replace('/', '\\').TrimEnd('\\');

            if (normalised.Length == 2)
            {
                driveLetter = char.ToUpperInvariant(normalised[0]);
                return true;
            }

            if (normalised.Length < 4 || normalised[2] != '\\')
                return false;

            string[] parsedSegments = normalised[3..].Split('\\', StringSplitOptions.None);

            if (parsedSegments.Any(segment => !isValidRequestSegment(segment)))
                return false;

            driveLetter = char.ToUpperInvariant(normalised[0]);
            segments = parsedSegments;
            return true;
        }

        private static bool isValidRequestSegment(string segment)
        {
            if (segment.Length > max_windows_component_characters
                || !SkinPackageResourceNameValidator.IsValidWindowsSegment(segment))
            {
                return false;
            }

            try
            {
                return segment.Normalize(NormalizationForm.FormC).Length <= max_windows_component_characters;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static IWindowsSkinPackageCaptureHandle own(
            IWindowsSkinPackageCaptureHandle handle,
            List<IWindowsSkinPackageCaptureHandle> handles)
        {
            if (handle == null)
                throw new InvalidOperationException("The native adapter returned no handle.");

            handles.Add(handle);
            return handle;
        }

        private static void disposeHandles(List<IWindowsSkinPackageCaptureHandle> handles)
        {
            Exception? firstException = null;

            for (int i = handles.Count - 1; i >= 0; i--)
            {
                try
                {
                    handles[i].Dispose();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            handles.Clear();

            if (firstException != null)
                ExceptionDispatchInfo.Capture(firstException).Throw();
        }

        private static WindowsSkinPackageCaptureFileSystemException reject(SkinManagedPackageCaptureRejectionReason reason)
            => new WindowsSkinPackageCaptureFileSystemException(reason);

        private static SkinManagedFolderPhysicalIdentity toMutationIdentity(WindowsSkinPackagePhysicalIdentity identity)
            => new SkinManagedFolderPhysicalIdentity(
                identity.VolumeSerialNumber,
                identity.FileIdPart0,
                identity.FileIdPart1);

        private sealed class DirectoryEntryComparer : IEqualityComparer<WindowsSkinPackageDirectoryEntry>
        {
            public static DirectoryEntryComparer Instance { get; } = new DirectoryEntryComparer();

            public bool Equals(WindowsSkinPackageDirectoryEntry? x, WindowsSkinPackageDirectoryEntry? y)
                => ReferenceEquals(x, y)
                   || (x != null
                       && y != null
                       && string.Equals(x.Name, y.Name, StringComparison.Ordinal)
                       && x.Metadata.Equals(y.Metadata));

            public int GetHashCode(WindowsSkinPackageDirectoryEntry obj) => HashCode.Combine(obj.Name, obj.Metadata);
        }

        private readonly record struct OpenedDirectory(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Metadata,
            string CanonicalName);

        private readonly record struct NodeRecord(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Baseline);

        private readonly record struct AuthorityLinkRecord(
            IWindowsSkinPackageCaptureHandle Parent,
            string CanonicalName,
            WindowsSkinPackageEntryMetadata Baseline,
            WindowsSkinPackageOpenMode VerificationMode);
    }
}
