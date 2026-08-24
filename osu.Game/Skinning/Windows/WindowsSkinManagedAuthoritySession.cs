// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
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
        private const string delete_node_fingerprint_domain =
            "OMS/SkinManagedFolderDeleteNodeFingerprint/v1";

        private readonly IWindowsSkinPackageCaptureFileSystem fileSystem;
        private readonly WindowsSkinManagedPackageCapture packageCapture;
        private readonly List<IWindowsSkinPackageCaptureHandle> handles;
        private readonly List<NodeRecord> authorityNodes;
        private readonly List<AuthorityLinkRecord> authorityLinks;
        private readonly IWindowsSkinPackageCaptureHandle dataRoot;
        private readonly IWindowsSkinPackageCaptureHandle managedRoot;
        private readonly WindowsSkinPackageEntryMetadata managedRootMetadata;
        private readonly WindowsSkinPackageDirectoryEntry[] baselineEntries;
        private IWindowsSkinPackageCaptureHandle? mutationManagedRoot;
        private OpenedDirectory? mutationStagingRoot;
        private WindowsSkinPackageDirectoryEntry[]? mutationStagingBaselineEntries;
        private WindowsSkinPackageDirectoryEntry[]? managedCopyInitialStagingEntries;
        private WindowsSkinPackageDirectoryEntry? mutationStagedSourceBaselineEntry;
        private OpenedDirectory? mutationSource;
        private WindowsSkinPackageDirectoryEntry? mutationSourceBaselineEntry;
        private HeldMutationTree? mutationSourceTree;
        private string? mutationSourceOriginalName;
        private SkinManagedFolderTargetNameSlot? mutationTargetNameSlot;
        private WindowsSkinPackageDirectoryEntry? mutationDeleteRemovedBaselineEntry;
        private string? mutationDeleteSourceName;
        private string? mutationDeleteTombstoneName;
        private MutationRenameState mutationRenameState;
        private bool stagedMutationCaptured;
        private bool managedCopyStagingPrepared;
        private Guid managedCopyOperationId;
        private bool managedCopyProvisionalRootCreated;
        private bool managedCopyProvisionalWritten;
        private IWindowsSkinPackageCaptureHandle? managedCopyProvisionalRoot;
        private WindowsSkinPackageEntryMetadata managedCopyProvisionalRootMetadata;
        private Dictionary<string, ManagedCopyCreatedNodeEvidence>? managedCopyCreatedNodeEvidence;
        private HeldMutationTree? managedCopyWrittenTree;
        private SkinManagedCopyProvisionalInspection? managedCopyRecoveryInspection;
        private string? managedCopyRecoveryTargetPath;
        private SkinManagedFolderPhysicalIdentity? managedCopyRecoveryStagedRootIdentity;
        private SkinManagedFolderPhysicalIdentity? managedCopyRecoveryProvisionalIdentity;
        private string? managedCopyRecoveryManifestDigest;
        private string? managedCopyRecoveryContentRevision;
        private bool stagedMutationForwardApplied;
        private bool stagedMutationRolledBack;
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
            ManagedRootAncestryProof = new SkinFolderPhysicalAncestryProof(
                authorityNodes.Select(node => toMutationIdentity(node.Baseline.Identity)));
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

        internal SkinFolderPhysicalAncestryProof ManagedRootAncestryProof { get; }

        internal SkinManagedFolderPhysicalIdentity CaptureExistingMutationSource(
            string managedRelativePath,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (mutationSource != null || stagedMutationCaptured)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

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

            ensureMutationManagedRoot(cancellationToken);
            OpenedDirectory opened = openExpectedDirectory(
                fileSystem,
                mutationManagedRoot!,
                candidate.Name,
                WindowsSkinPackageOpenMode.MutationSourceDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);
            HeldMutationTree tree = holdMutationTree(opened.Handle, opened.Metadata, handles, cancellationToken);
            ValidateCompleteAndStable(cancellationToken);
            mutationSource = opened;
            mutationSourceBaselineEntry = candidate;
            mutationSourceTree = tree;
            mutationSourceOriginalName = candidate.Name;
            mutationRenameState = MutationRenameState.Prepared;
            return toMutationIdentity(opened.Metadata.Identity);
        }

        internal string GetCapturedMutationDeleteSourceNodeManifest(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (mutationRenameState != MutationRenameState.Prepared
                || mutationSourceTree == null
                || mutationSourceTree.DescendantsReleased)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            ValidateCompleteAndStable(cancellationToken);
            validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);
            return createMutationDeleteNodeManifest(mutationSourceTree);
        }

        /// <summary>
        /// Captures the immutable logical package from the exact source already pinned for mutation. The mutation
        /// tree remains held after this method returns and is revalidated around reads through those exact handles.
        /// </summary>
        internal SkinPackageRevisionCapsule CaptureCapturedMutationSourceRevision(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (mutationRenameState != MutationRenameState.Prepared
                || mutationSourceBaselineEntry == null
                || mutationSourceTree == null
                || mutationSourceTree.DescendantsReleased)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            ValidateCompleteAndStable(cancellationToken);
            validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);

            SkinPackageCapturedEntry[] entries = mutationSourceTree.Nodes
                                                                   .Skip(1)
                                                                   .Select(node =>
                                                                       node.Baseline.Kind == WindowsSkinPackageEntryKind.Directory
                                                                           ? SkinPackageCapturedEntry.CreateDirectory(node.RelativePath)
                                                                           : SkinPackageCapturedEntry.CreateFile(
                                                                               node.RelativePath,
                                                                               node.Baseline.Length,
                                                                               () => fileSystem.CreateNonOwningReadStream(node.Handle)))
                                                                   .ToArray();
            SkinPackageRevisionCapsuleCreationResult captured =
                SkinPackageRevisionCapsuleFactory.Create(
                    entries,
                    SkinPackageRevisionCapsuleLimits.Default,
                    cancellationToken);

            if (!captured.IsSuccess)
                throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);

            SkinPackageRevisionCapsule capsule = captured.Capsule!;

            try
            {
                ValidateCompleteAndStable(cancellationToken);
                validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);
                return capsule;
            }
            catch
            {
                capsule.Dispose();
                throw;
            }
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

            if (mutationTargetNameSlot != null)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            ensureMutationManagedRoot(cancellationToken);

            try
            {
                using IWindowsSkinPackageCaptureHandle unexpected = fileSystem.OpenChildNoFollow(
                    mutationManagedRoot!,
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
            var targetNameSlot = new SkinManagedFolderTargetNameSlot(managedRelativePath, ManagedRootIdentity);
            mutationTargetNameSlot = targetNameSlot;
            return targetNameSlot;
        }

        internal SkinManagedFolderStagedSourceCapture CaptureStagedMutationSource(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty
                || mutationSource != null
                || stagedMutationCaptured)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            OpenedDirectory stagingRoot;

            if (managedCopyStagingPrepared && mutationStagingRoot is { } preparedStagingRoot)
            {
                stagingRoot = preparedStagingRoot;
            }
            else
            {
                stagingRoot = openExpectedDirectory(
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
            }

            string expectedSourceName = operationId.ToString("N");
            WindowsSkinPackageDirectoryEntry[] stagingBaseline = canonicaliseDirectoryEntries(
                getDirectoryEntries(
                    fileSystem,
                    stagingRoot.Handle,
                    max_authority_directory_entries,
                    cancellationToken));
            WindowsSkinPackageDirectoryEntry? sourceCandidate =
                getOptionalExactNameEntry(stagingBaseline, expectedSourceName);

            if (managedCopyStagingPrepared
                && (managedCopyInitialStagingEntries == null
                    || stagingBaseline.Length != managedCopyInitialStagingEntries.Length + 1
                    || !managedCopyInitialStagingEntries.SequenceEqual(
                        stagingBaseline.Where(entry => !namesEqual(entry.Name, expectedSourceName)),
                        DirectoryEntryComparer.Instance)))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            if (sourceCandidate == null
                || !string.Equals(
                    sourceCandidate.Name,
                    expectedSourceName,
                    StringComparison.Ordinal))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
            }

            OpenedDirectory source = openExpectedDirectory(
                fileSystem,
                stagingRoot.Handle,
                expectedSourceName,
                WindowsSkinPackageOpenMode.ProvisionalDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);

            if (!string.Equals(source.CanonicalName, expectedSourceName, StringComparison.Ordinal))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            HeldMutationTree sourceTree = holdMutationTree(
                source.Handle,
                source.Metadata,
                handles,
                cancellationToken,
                provisional: true);

            if (managedCopyStagingPrepared
                && (!managedCopyProvisionalWritten
                    || managedCopyCreatedNodeEvidence == null
                    || managedCopyWrittenTree == null
                    || source.Metadata.Identity != managedCopyProvisionalRootMetadata.Identity
                    || source.Metadata.CreationTime != managedCopyProvisionalRootMetadata.CreationTime))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            if (managedCopyStagingPrepared)
                assertManagedCopyTreeMatchesEvidence(sourceTree, managedCopyCreatedNodeEvidence!);

            SkinManagedPackageCaptureResult captured = packageCapture.CaptureProvisionalChild(
                stagingRoot.Handle,
                sourceCandidate,
                cancellationToken: cancellationToken);

            if (!captured.IsSuccess
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    captured.PhysicalTreeFingerprint))
            {
                throw reject(
                    captured.IsSuccess
                        ? SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture
                        : captured.RejectionReason == SkinManagedPackageCaptureRejectionReason.CapsuleRejected
                        ? SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture
                        : captured.RejectionReason);
            }

            SkinPackageRevisionCapsule capsule = captured.Capsule!;

            try
            {
                SkinExternalPackageLogicalManifest? logicalManifest = null;

                if (managedCopyStagingPrepared)
                {
                    SkinPackageCapturedEntry?[] logicalEntries = sourceTree.Nodes
                        .Skip(1)
                        .Select(node => node.Baseline.Kind == WindowsSkinPackageEntryKind.Directory
                            ? SkinPackageCapturedEntry.CreateDirectory(node.RelativePath)
                            : new SkinPackageCapturedEntry(
                                SkinPackageCapturedEntryKind.File,
                                node.RelativePath,
                                node.Baseline.Length))
                        .ToArray();

                    if (!SkinExternalPackageLogicalManifest.TryCreate(
                            logicalEntries,
                            capsule,
                            SkinExternalPackageCaptureLimits.DEFAULT_MAX_LOGICAL_MANIFEST_BYTES,
                            out logicalManifest))
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.LogicalManifestBudgetExceeded);
                    }
                }

                mutationStagingRoot = stagingRoot;
                mutationStagingBaselineEntries = stagingBaseline;
                mutationStagedSourceBaselineEntry = sourceCandidate;
                mutationSource = source;
                mutationSourceBaselineEntry = sourceCandidate;
                mutationSourceTree = sourceTree;
                mutationSourceOriginalName = expectedSourceName;
                stagedMutationCaptured = true;

                if (managedCopyStagingPrepared
                    && managedCopyProvisionalRoot is { } originalProvisionalRoot)
                {
                    // CaptureStagedMutationSource has now re-opened the exact root and proven both its immutable
                    // identity/creation evidence and every intended child. Retaining the create-time root handle would
                    // deny the subsequent handle-relative rename on Windows even though the new source tree already
                    // owns the complete authority. Release only that superseded, compare-proven handle.
                    if (ReferenceEquals(originalProvisionalRoot, source.Handle))
                        throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

                    HeldMutationTree originalWrittenTree = managedCopyWrittenTree
                                                           ?? throw reject(
                                                               SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                    releaseMutationTreeDescendants(originalWrittenTree, originalProvisionalRoot);

                    for (int i = 1; i < originalWrittenTree.Nodes.Count; i++)
                        handles.Remove(originalWrittenTree.Nodes[i].Handle);

                    originalProvisionalRoot.Dispose();
                    handles.Remove(originalProvisionalRoot);
                    managedCopyProvisionalRoot = null;
                    managedCopyWrittenTree = null;
                }

                ValidateCompleteAndStable(cancellationToken);
                var result = new SkinManagedFolderStagedSourceCapture(
                    toMutationIdentity(stagingRoot.Metadata.Identity),
                    toMutationIdentity(source.Metadata.Identity),
                    captured.PhysicalTreeFingerprint!,
                    capsule,
                    logicalManifest);
                capsule = null!;
                return result;
            }
            finally
            {
                capsule?.Dispose();
            }
        }

        internal SkinManagedFolderPhysicalIdentity PrepareManagedCopyStaging(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty
                || managedCopyStagingPrepared
                || mutationStagingRoot != null
                || mutationSource != null
                || stagedMutationCaptured)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            OpenedDirectory stagingRoot;

            try
            {
                stagingRoot = openExpectedDirectory(
                    fileSystem,
                    dataRoot,
                    "skin-mutation-staging",
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.PackageUnavailable)
            {
                IWindowsSkinPackageCaptureHandle created = own(
                    fileSystem.CreateChildNoFollowNoReplace(dataRoot, "skin-mutation-staging", directory: true),
                    handles);
                WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(created);

                if (metadata.Kind != WindowsSkinPackageEntryKind.Directory
                    || metadata.IsReparsePoint
                    || metadata.DeletePending
                    || metadata.NumberOfLinks != 1)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
                }

                created.Dispose();
                handles.Remove(created);
                stagingRoot = openExpectedDirectory(
                    fileSystem,
                    dataRoot,
                    "skin-mutation-staging",
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);
            }

            if (!string.Equals(stagingRoot.CanonicalName, "skin-mutation-staging", StringComparison.Ordinal))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            authorityLinks.Add(new AuthorityLinkRecord(
                dataRoot,
                stagingRoot.CanonicalName,
                stagingRoot.Metadata,
                WindowsSkinPackageOpenMode.AuthorityDirectory));
            authorityNodes.Add(new NodeRecord(stagingRoot.Handle, stagingRoot.Metadata));
            WindowsSkinPackageDirectoryEntry[] baseline = canonicaliseDirectoryEntries(
                getDirectoryEntries(
                    fileSystem,
                    stagingRoot.Handle,
                    max_authority_directory_entries,
                    cancellationToken));
            string operationSlot = operationId.ToString("N");

            if (baseline.Any(entry => namesEqual(entry.Name, operationSlot)))
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            mutationStagingRoot = stagingRoot;
            managedCopyInitialStagingEntries = baseline;
            managedCopyOperationId = operationId;
            managedCopyStagingPrepared = true;
            ValidateCompleteAndStable(cancellationToken);
            return toMutationIdentity(stagingRoot.Metadata.Identity);
        }

        internal SkinManagedFolderPhysicalIdentity CreateManagedCopyProvisionalRoot(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty
                || !managedCopyStagingPrepared
                || managedCopyOperationId != operationId
                || managedCopyProvisionalRootCreated
                || managedCopyProvisionalWritten
                || mutationStagingRoot is not { } stagingRoot
                || managedCopyInitialStagingEntries == null
                || stagedMutationCaptured)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            ValidateCompleteAndStable(cancellationToken);
            string operationSlot = operationId.ToString("N");
            IWindowsSkinPackageCaptureHandle root = own(
                fileSystem.CreateChildNoFollowNoReplace(stagingRoot.Handle, operationSlot, directory: true),
                handles);
            WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(root);
            validateCreatedNode(metadata, WindowsSkinPackageEntryKind.Directory, 0);
            managedCopyProvisionalRoot = root;
            managedCopyProvisionalRootMetadata = metadata;
            managedCopyProvisionalRootCreated = true;
            // Once the operation root exists, caller cancellation cannot strand it before its identity is durably
            // recorded as Copying. The caller gets another cancellable boundary before creation and immediately before
            // the first destination write.
            validateManagedCopyStagingWithRoot(operationId, CancellationToken.None);
            return toMutationIdentity(metadata.Identity);
        }

        internal void WriteManagedCopyProvisional(
            Guid operationId,
            SkinPackageRevisionCapsule capsule,
            SkinManagedCopyLogicalManifest logicalManifest,
            Action firstDestinationWrite,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(capsule);
            ArgumentNullException.ThrowIfNull(logicalManifest);
            ArgumentNullException.ThrowIfNull(firstDestinationWrite);

            if (operationId == Guid.Empty
                || !managedCopyStagingPrepared
                || managedCopyOperationId != operationId
                || !managedCopyProvisionalRootCreated
                || managedCopyProvisionalWritten
                || mutationStagingRoot == null
                || managedCopyProvisionalRoot is not { } provisionalRoot
                || managedCopyInitialStagingEntries == null
                || stagedMutationCaptured)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            var createdHandles = new List<IWindowsSkinPackageCaptureHandle>();
            var createdNodes = new Dictionary<string, ManagedCopyCreatedNodeEvidence>(StringComparer.OrdinalIgnoreCase);
            WindowsSkinPackageEntryMetadata provisionalRootMetadata = managedCopyProvisionalRootMetadata;
            bool recoveryOwned = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCompleteAndStable(cancellationToken);
                validateManagedCopyStagingWithRoot(operationId, CancellationToken.None);
                var directories = new Dictionary<string, IWindowsSkinPackageCaptureHandle>(StringComparer.OrdinalIgnoreCase)
                {
                    [string.Empty] = provisionalRoot,
                };

                foreach (SkinManagedCopyLogicalManifest.Entry entry in logicalManifest.Entries
                                                                                 .Where(entry => entry.Kind == SkinExternalPackageLogicalEntryKind.Directory)
                                                                                 .OrderBy(entry => entry.RelativePath.Count(character => character == '/'))
                                                                                 .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                                                                                 .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (string parentPath, string childName) = splitResourcePath(entry.RelativePath);

                    if (!directories.TryGetValue(parentPath, out IWindowsSkinPackageCaptureHandle? parent))
                        throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

                    IWindowsSkinPackageCaptureHandle created = own(
                        fileSystem.CreateChildNoFollowNoReplace(parent, childName, directory: true),
                        handles);
                    createdHandles.Add(created);
                    WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(created);
                    validateCreatedNode(metadata, WindowsSkinPackageEntryKind.Directory, 0);
                    directories.Add(entry.RelativePath, created);
                    createdNodes.Add(
                        entry.RelativePath,
                        new ManagedCopyCreatedNodeEvidence(
                            metadata.Identity,
                            metadata.Kind,
                            metadata.CreationTime));
                }

                using var resources = capsule.CreateResourceView();

                foreach (SkinManagedCopyLogicalManifest.Entry entry in logicalManifest.Entries
                                                                                 .Where(entry => entry.Kind == SkinExternalPackageLogicalEntryKind.File))
                {
                    if (!recoveryOwned)
                        cancellationToken.ThrowIfCancellationRequested();
                    (string parentPath, string childName) = splitResourcePath(entry.RelativePath);

                    if (!directories.TryGetValue(parentPath, out IWindowsSkinPackageCaptureHandle? parent))
                        throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

                    IWindowsSkinPackageCaptureHandle created = own(
                        fileSystem.CreateChildNoFollowNoReplace(parent, childName, directory: false),
                        handles);
                    createdHandles.Add(created);
                    WindowsSkinPackageEntryMetadata createdMetadata = fileSystem.QueryMetadata(created);
                    validateCreatedNode(createdMetadata, WindowsSkinPackageEntryKind.File, 0);
                    createdNodes.Add(
                        entry.RelativePath,
                        new ManagedCopyCreatedNodeEvidence(
                            createdMetadata.Identity,
                            createdMetadata.Kind,
                            createdMetadata.CreationTime));

                    if (!recoveryOwned)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        firstDestinationWrite();
                        recoveryOwned = true;
                    }

                    using Stream? source = resources.GetStream(entry.RelativePath) ?? throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                    using Stream destination = fileSystem.CreateNonOwningWriteStream(created);
                    byte[] buffer = new byte[64 * 1024];
                    long total = 0;

                    while (true)
                    {
                        int read = source.Read(buffer, 0, buffer.Length);

                        if (read == 0)
                            break;

                        destination.Write(buffer, 0, read);
                        total = checked(total + read);

                        if (total > entry.Length)
                            throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                    }

                    destination.Flush();
                    WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(created);
                    validateCreatedNode(metadata, WindowsSkinPackageEntryKind.File, entry.Length);

                    if (total != entry.Length)
                        throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                }

                if (!recoveryOwned)
                {
                    firstDestinationWrite();
                    recoveryOwned = true;
                }

                releaseManagedCopyCreatedHandles(createdHandles);
                HeldMutationTree written = holdMutationTree(
                    provisionalRoot,
                    provisionalRootMetadata,
                    handles,
                    CancellationToken.None,
                    provisional: true);
                assertManagedCopyTreeMatches(written, logicalManifest);
                assertManagedCopyTreeMatchesEvidence(written, createdNodes);
                managedCopyCreatedNodeEvidence = new Dictionary<string, ManagedCopyCreatedNodeEvidence>(
                    createdNodes,
                    StringComparer.OrdinalIgnoreCase);
                managedCopyWrittenTree = written;
                managedCopyProvisionalWritten = true;
            }
            catch
            {
                if (!recoveryOwned && provisionalRoot != null)
                {
                    try
                    {
                        releaseManagedCopyCreatedHandles(createdHandles);
                        HeldMutationTree partial = holdMutationTree(
                            provisionalRoot,
                            provisionalRootMetadata,
                            handles,
                            CancellationToken.None,
                            provisional: true);
                        assertManagedCopyTreeMatchesEvidence(partial, createdNodes);
                        deleteHeldTree(partial);

                        managedCopyProvisionalRoot = null;
                        managedCopyProvisionalRootCreated = false;
                        managedCopyProvisionalRootMetadata = default;
                        managedCopyCreatedNodeEvidence = null;
                        managedCopyWrittenTree = null;
                    }
                    catch
                    {
                        // Exact cleanup could not be proven. The durable intent remains for recovery.
                    }
                }

                throw;
            }
        }

        internal SkinManagedCopyProvisionalInspection InspectManagedCopyProvisionalState(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity? expectedProvisionalIdentity,
            SkinManagedCopyLogicalManifest logicalManifest,
            string expectedContentRevision,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(logicalManifest);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty
                || !SkinManagedFolderPath.TryNormalise(targetManagedRelativePath, out string normalisedTarget)
                || !string.Equals(targetManagedRelativePath, normalisedTarget, StringComparison.Ordinal)
                || !expectedStagedRootIdentity.IsUsable
                || expectedStagedRootIdentity.VolumeSerialNumber != managedRootMetadata.Identity.VolumeSerialNumber
                || (expectedProvisionalIdentity != null
                    && (!expectedProvisionalIdentity.Value.IsUsable
                        || expectedProvisionalIdentity.Value.VolumeSerialNumber
                        != managedRootMetadata.Identity.VolumeSerialNumber))
                || !SkinManagedFolderMutationJournal.IsValidContentRevision(expectedContentRevision))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            if (managedCopyRecoveryInspection != null)
            {
                if (managedCopyOperationId != operationId
                    || !string.Equals(
                        managedCopyRecoveryTargetPath,
                        targetManagedRelativePath,
                        StringComparison.Ordinal)
                    || managedCopyRecoveryStagedRootIdentity != expectedStagedRootIdentity
                    || managedCopyRecoveryProvisionalIdentity != expectedProvisionalIdentity
                    || !string.Equals(
                        managedCopyRecoveryManifestDigest,
                        logicalManifest.Digest,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        managedCopyRecoveryContentRevision,
                        expectedContentRevision,
                        StringComparison.Ordinal))
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                }

                ValidateCompleteAndStable(cancellationToken);
                return managedCopyRecoveryInspection;
            }

            if (mutationStagingRoot != null
                || mutationSource != null
                || stagedMutationCaptured
                || managedCopyStagingPrepared)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            ensureMutationManagedRoot(cancellationToken);
            validateBaselineCompleteAndStable(cancellationToken);
            string targetName = getManagedDirectChildName(targetManagedRelativePath);
            WindowsSkinPackageDirectoryEntry? target = getOptionalExactNameEntry(
                getCurrentManagedRootInventory(cancellationToken),
                targetName);

            if (target != null)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    SkinManagedCopyProvisionalInspectionStatus.TargetOccupied,
                    ManagedRootIdentity));
            }

            OpenedDirectory stagingRoot;

            try
            {
                stagingRoot = openExpectedDirectory(
                    fileSystem,
                    dataRoot,
                    "skin-mutation-staging",
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.PackageUnavailable)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    SkinManagedCopyProvisionalInspectionStatus.RootIdentityMismatch,
                    ManagedRootIdentity));
            }

            if (!string.Equals(stagingRoot.CanonicalName, "skin-mutation-staging", StringComparison.Ordinal)
                || toMutationIdentity(stagingRoot.Metadata.Identity) != expectedStagedRootIdentity)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    SkinManagedCopyProvisionalInspectionStatus.RootIdentityMismatch,
                    ManagedRootIdentity));
            }

            authorityLinks.Add(new AuthorityLinkRecord(
                dataRoot,
                stagingRoot.CanonicalName,
                stagingRoot.Metadata,
                WindowsSkinPackageOpenMode.AuthorityDirectory));
            authorityNodes.Add(new NodeRecord(stagingRoot.Handle, stagingRoot.Metadata));
            WindowsSkinPackageDirectoryEntry[] stagingInventory = canonicaliseDirectoryEntries(
                getDirectoryEntries(
                    fileSystem,
                    stagingRoot.Handle,
                    max_authority_directory_entries,
                    cancellationToken));
            string sourceName = operationId.ToString("N");
            WindowsSkinPackageDirectoryEntry? source = getOptionalExactNameEntry(stagingInventory, sourceName);

            mutationStagingRoot = stagingRoot;
            managedCopyInitialStagingEntries = stagingInventory;
            managedCopyOperationId = operationId;
            managedCopyStagingPrepared = true;

            if (expectedProvisionalIdentity == null)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    source == null
                        ? SkinManagedCopyProvisionalInspectionStatus.Absent
                        : SkinManagedCopyProvisionalInspectionStatus.IdentityMismatch,
                    ManagedRootIdentity));
            }

            if (source == null
                || !string.Equals(source.Name, sourceName, StringComparison.Ordinal)
                || source.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                || source.Metadata.IsReparsePoint
                || source.Metadata.DeletePending
                || toMutationIdentity(source.Metadata.Identity) != expectedProvisionalIdentity.Value)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    SkinManagedCopyProvisionalInspectionStatus.IdentityMismatch,
                    ManagedRootIdentity));
            }

            OpenedDirectory openedSource = openExpectedDirectory(
                fileSystem,
                stagingRoot.Handle,
                sourceName,
                WindowsSkinPackageOpenMode.ProvisionalDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);
            HeldMutationTree sourceTree = holdMutationTree(
                openedSource.Handle,
                openedSource.Metadata,
                handles,
                cancellationToken,
                provisional: true);
            SkinManagedCopyProvisionalInspectionStatus treeStatus =
                classifyManagedCopyProvisionalTree(sourceTree, logicalManifest);

            mutationStagingBaselineEntries = stagingInventory;
            mutationStagedSourceBaselineEntry = source;
            mutationSource = openedSource;
            mutationSourceBaselineEntry = source;
            mutationSourceTree = sourceTree;
            mutationSourceOriginalName = sourceName;
            stagedMutationCaptured = true;

            if (treeStatus != SkinManagedCopyProvisionalInspectionStatus.Complete)
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    treeStatus,
                    ManagedRootIdentity,
                    expectedProvisionalIdentity));
            }

            SkinManagedPackageCaptureResult captured = packageCapture.CaptureProvisionalChild(
                stagingRoot.Handle,
                source,
                cancellationToken: cancellationToken);

            if (!captured.IsSuccess
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(captured.PhysicalTreeFingerprint))
            {
                throw reject(captured.IsSuccess
                    ? SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture
                    : captured.RejectionReason);
            }

            using SkinPackageRevisionCapsule capsule = captured.Capsule!;

            if (!string.Equals(capsule.ContentRevision, expectedContentRevision, StringComparison.Ordinal)
                || !SkinManagedFolderPackageMetadataReader.TryRead(
                    capsule,
                    out SkinManagedFolderPackageMetadata? metadata))
            {
                return remember(new SkinManagedCopyProvisionalInspection(
                    SkinManagedCopyProvisionalInspectionStatus.ManifestMismatch,
                    ManagedRootIdentity,
                    expectedProvisionalIdentity));
            }

            ValidateCompleteAndStable(cancellationToken);
            return remember(new SkinManagedCopyProvisionalInspection(
                SkinManagedCopyProvisionalInspectionStatus.Complete,
                ManagedRootIdentity,
                expectedProvisionalIdentity,
                metadata,
                captured.PhysicalTreeFingerprint));

            SkinManagedCopyProvisionalInspection remember(
                SkinManagedCopyProvisionalInspection inspection)
            {
                managedCopyRecoveryInspection = inspection;
                managedCopyOperationId = operationId;
                managedCopyRecoveryTargetPath = targetManagedRelativePath;
                managedCopyRecoveryStagedRootIdentity = expectedStagedRootIdentity;
                managedCopyRecoveryProvisionalIdentity = expectedProvisionalIdentity;
                managedCopyRecoveryManifestDigest = logicalManifest.Digest;
                managedCopyRecoveryContentRevision = expectedContentRevision;
                return inspection;
            }
        }

        internal void CleanupExactManagedCopyProvisional(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedProvisionalIdentity,
            SkinManagedCopyLogicalManifest logicalManifest,
            string expectedContentRevision,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(logicalManifest);
            cancellationToken.ThrowIfCancellationRequested();

            SkinManagedCopyProvisionalInspection inspection = InspectManagedCopyProvisionalState(
                operationId,
                targetManagedRelativePath,
                expectedStagedRootIdentity,
                expectedProvisionalIdentity,
                logicalManifest,
                expectedContentRevision,
                cancellationToken);

            if (inspection.Status != SkinManagedCopyProvisionalInspectionStatus.Empty
                || mutationSourceTree == null
                || mutationSource == null
                || stagedMutationRolledBack
                || stagedMutationForwardApplied)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            ValidateCompleteAndStable(cancellationToken);
            deleteHeldTree(mutationSourceTree);
            mutationSourceTree = null;
            mutationSource = null;
            stagedMutationRolledBack = true;
            managedCopyRecoveryInspection = new SkinManagedCopyProvisionalInspection(
                SkinManagedCopyProvisionalInspectionStatus.Absent,
                ManagedRootIdentity);
            ValidateCompleteAndStable(CancellationToken.None);
        }

        private void releaseManagedCopyCreatedHandles(
            List<IWindowsSkinPackageCaptureHandle> createdHandles)
        {
            for (int i = createdHandles.Count - 1; i >= 0; i--)
            {
                IWindowsSkinPackageCaptureHandle created = createdHandles[i];
                created.Dispose();
                handles.Remove(created);
                createdHandles.RemoveAt(i);
            }
        }

        internal bool IsManagedCopyProvisionalAbsent(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!managedCopyStagingPrepared
                || managedCopyOperationId != operationId
                || managedCopyProvisionalRootCreated
                || managedCopyProvisionalWritten
                || mutationStagingRoot == null
                || managedCopyInitialStagingEntries == null
                || stagedMutationCaptured)
            {
                return false;
            }

            ValidateCompleteAndStable(cancellationToken);
            return true;
        }

        internal SkinManagedFolderStagedImportFilesystemResult MoveCapturedStagedMutationSourceToTarget(
            SkinManagedFolderTargetNameSlot targetNameSlot,
            string expectedContentRevision,
            string expectedTreeFingerprint,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!stagedMutationCaptured
                || stagedMutationForwardApplied
                || stagedMutationRolledBack
                || mutationSource is not { } source
                || mutationSourceTree == null
                || mutationTargetNameSlot == null
                || mutationManagedRoot == null
                || mutationStagingRoot == null
                || mutationStagingBaselineEntries == null
                || mutationStagedSourceBaselineEntry == null
                || !ReferenceEquals(targetNameSlot, mutationTargetNameSlot)
                || targetNameSlot.ManagedRootIdentity != ManagedRootIdentity
                || !SkinManagedFolderMutationJournal.IsValidContentRevision(
                    expectedContentRevision)
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    expectedTreeFingerprint))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            string targetName = getManagedDirectChildName(
                targetNameSlot.ManagedRelativePath);
            ValidateCompleteAndStable(cancellationToken);
            validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            releaseMutationTreeDescendants(mutationSourceTree, source.Handle);
            fileSystem.RenameChildNoReplace(
                source.Handle,
                mutationManagedRoot,
                targetName);
            stagedMutationForwardApplied = true;

            // As with rename, cancellation no longer owns the outcome after the first visible physical step.
            return captureAndValidateMovedStagedPackage(
                targetName,
                expectedContentRevision,
                expectedTreeFingerprint,
                CancellationToken.None);
        }

        internal SkinManagedFolderPhysicalIdentity RenameCapturedMutationSourceToTarget(
            SkinManagedFolderTargetNameSlot targetNameSlot,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (mutationRenameState != MutationRenameState.Prepared
                || mutationSource is not { } source
                || mutationSourceTree == null
                || mutationSourceBaselineEntry == null
                || mutationSourceOriginalName == null
                || mutationTargetNameSlot == null
                || !ReferenceEquals(targetNameSlot, mutationTargetNameSlot)
                || targetNameSlot.ManagedRootIdentity != ManagedRootIdentity)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            string targetName = getManagedDirectChildName(targetNameSlot.ManagedRelativePath);
            ValidateCompleteAndStable(cancellationToken);
            validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            releaseMutationTreeDescendants(mutationSourceTree, source.Handle);
            fileSystem.RenameChildNoReplace(source.Handle, mutationManagedRoot!, targetName);
            mutationRenameState = MutationRenameState.ForwardApplied;

            // The physical move is now externally visible. Caller cancellation can no longer turn this into a clean
            // pre-operation abort, so final verification deliberately runs to completion without the caller token.
            validateMutationRenameLocation(
                targetName,
                mutationSourceOriginalName,
                CancellationToken.None);
            return toMutationIdentity(source.Metadata.Identity);
        }

        internal SkinManagedFolderRenameInspection InspectMutationRenameState(
            string sourceManagedRelativePath,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (!expectedSourceIdentity.IsUsable)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            string sourceName = getManagedDirectChildName(sourceManagedRelativePath);
            string targetName = getManagedDirectChildName(targetManagedRelativePath);

            if (namesEqual(sourceName, targetName))
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            ensureMutationManagedRoot(cancellationToken);
            ValidateCompleteAndStable(cancellationToken);
            WindowsSkinPackageDirectoryEntry[] current = getCurrentManagedRootInventory(cancellationToken);
            WindowsSkinPackageDirectoryEntry? source = getOptionalExactNameEntry(current, sourceName);
            WindowsSkinPackageDirectoryEntry? target = getOptionalExactNameEntry(current, targetName);
            var inspectionHandles = new List<IWindowsSkinPackageCaptureHandle>();

            try
            {
                if (source != null && target != null)
                {
                    holdAndValidateInspectionTree(source, inspectionHandles, cancellationToken);
                    holdAndValidateInspectionTree(target, inspectionHandles, cancellationToken);
                    ValidateCompleteAndStable(cancellationToken);
                    return new SkinManagedFolderRenameInspection(SkinManagedFolderRenameInspectionStatus.Both);
                }

                if (source == null && target == null)
                {
                    ValidateCompleteAndStable(cancellationToken);
                    return new SkinManagedFolderRenameInspection(SkinManagedFolderRenameInspectionStatus.Neither);
                }

                WindowsSkinPackageDirectoryEntry existing = source ?? target!;
                SkinManagedFolderPhysicalIdentity existingIdentity =
                    toMutationIdentity(holdAndValidateInspectionTree(existing, inspectionHandles, cancellationToken));
                ValidateCompleteAndStable(cancellationToken);

                if (existingIdentity != expectedSourceIdentity)
                    return new SkinManagedFolderRenameInspection(SkinManagedFolderRenameInspectionStatus.IdentityMismatch);

                return new SkinManagedFolderRenameInspection(
                    source != null
                        ? SkinManagedFolderRenameInspectionStatus.SourceOnly
                        : SkinManagedFolderRenameInspectionStatus.TargetOnly);
            }
            finally
            {
                disposeHandles(inspectionHandles);
            }
        }

        internal void CleanupExactMutationDeleteTombstone(
            string sourceManagedRelativePath,
            string tombstoneManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            string expectedSourceNodeManifest,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            string sourceName = getManagedDirectChildName(sourceManagedRelativePath);
            string tombstoneName = getManagedDirectChildName(tombstoneManagedRelativePath);

            if (namesEqual(sourceName, tombstoneName)
                || !expectedSourceIdentity.IsUsable
                || !SkinManagedFolderDeleteManifest.IsValid(
                    expectedSourceNodeManifest))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            SkinManagedFolderRenameInspection before = InspectMutationRenameState(
                sourceManagedRelativePath,
                tombstoneManagedRelativePath,
                expectedSourceIdentity,
                cancellationToken);

            if (before.Status == SkinManagedFolderRenameInspectionStatus.Neither)
                return;

            if (before.Status != SkinManagedFolderRenameInspectionStatus.TargetOnly)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            // A live same-session detach has not yet attempted recursive disposition, so a missing node in the
            // release-to-exclusive-recapture gap is drift, not proof of a crash-era partial delete. Only a newly
            // opened recovery session may accept a durable-manifest subset.
            bool requireExactLiveManifest = mutationRenameState == MutationRenameState.ForwardApplied;

            ensureMutationManagedRoot(cancellationToken);
            WindowsSkinPackageDirectoryEntry[] current = getCurrentManagedRootInventory(cancellationToken);
            WindowsSkinPackageDirectoryEntry? targetEntry = getOptionalExactNameEntry(current, tombstoneName);

            if (targetEntry == null
                || getOptionalExactNameEntry(current, sourceName) != null
                || toMutationIdentity(targetEntry.Metadata.Identity) != expectedSourceIdentity)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            WindowsSkinPackageDirectoryEntry? removedBaseline = (mutationSourceBaselineEntry
                                                                    ?? getOptionalExactNameEntry(baselineEntries, tombstoneName)) ?? throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            if (requireExactLiveManifest)
            {
                if (mutationSourceTree is not { DescendantsReleased: false } heldDeleteTree)
                    throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

                if (!string.Equals(
                        createMutationDeleteNodeManifest(heldDeleteTree),
                        expectedSourceNodeManifest,
                        StringComparison.Ordinal))
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }

                validateHeldMutationTree(
                    fileSystem,
                    heldDeleteTree,
                    cancellationToken);
                validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
                validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                releaseHeldMutationTreeForDeleteRecapture(heldDeleteTree);
            }

            var deleteHandles = new List<IWindowsSkinPackageCaptureHandle>();

            try
            {
                OpenedDirectory opened = openExpectedDirectory(
                    fileSystem,
                    mutationManagedRoot!,
                    targetEntry.Name,
                    WindowsSkinPackageOpenMode.DeleteExclusiveDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    deleteHandles);

                if (toMutationIdentity(opened.Metadata.Identity)
                    != expectedSourceIdentity)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }

                HeldMutationTree deleteTree = holdMutationTree(
                    opened.Handle,
                    opened.Metadata,
                    deleteHandles,
                    cancellationToken,
                    provisional: true,
                    deleteExclusive: true);
                string survivingManifest =
                    createMutationDeleteNodeManifest(deleteTree);

                bool manifestMatches = requireExactLiveManifest
                    ? string.Equals(
                        survivingManifest,
                        expectedSourceNodeManifest,
                        StringComparison.Ordinal)
                    : SkinManagedFolderDeleteManifest.IsSubset(
                        survivingManifest,
                        expectedSourceNodeManifest);

                if (!manifestMatches)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }

                validateHeldMutationTree(
                    fileSystem,
                    deleteTree,
                    cancellationToken);
                validateAuthorityNodes(
                    fileSystem,
                    authorityNodes,
                    cancellationToken);
                validateAuthorityLinks(
                    fileSystem,
                    authorityLinks,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // The verification recapture is deliberately discarded before this point: only these fresh
                // no-follow, delete-exclusive handles carry DELETE access. Every node was proven to be a
                // surviving member of the durable source manifest, and cannot be renamed away through another
                // handle, before the first recursive disposition becomes visible.
                deleteHeldTree(deleteTree, deleteHandles);
            }
            finally
            {
                disposeHandles(deleteHandles);
            }

            mutationDeleteRemovedBaselineEntry = removedBaseline;
            mutationDeleteSourceName = sourceName;
            mutationDeleteTombstoneName = tombstoneName;
            mutationRenameState = MutationRenameState.DeleteApplied;
            validateMutationDeleteLocation(CancellationToken.None);
        }

        internal SkinManagedFolderStagedImportInspection InspectStagedMutationImportState(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (operationId == Guid.Empty
                || !expectedStagedRootIdentity.IsUsable
                || !expectedSourceIdentity.IsUsable
                || expectedStagedRootIdentity.VolumeSerialNumber
                   != managedRootMetadata.Identity.VolumeSerialNumber
                || expectedSourceIdentity.VolumeSerialNumber
                   != managedRootMetadata.Identity.VolumeSerialNumber)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            string sourceName = operationId.ToString("N");
            string targetName = getManagedDirectChildName(targetManagedRelativePath);
            ensureMutationManagedRoot(cancellationToken);

            if (stagedMutationCaptured && stagedMutationForwardApplied)
                validateMovedStagedLocation(targetName, cancellationToken);
            else if (stagedMutationCaptured && stagedMutationRolledBack)
                validateRolledBackStagedLocation(cancellationToken);
            else
                validateBaselineCompleteAndStable(cancellationToken);

            var inspectionHandles = new List<IWindowsSkinPackageCaptureHandle>();

            try
            {
                WindowsSkinPackageDirectoryEntry? stagingEntry = getOptionalExactNameEntry(
                    getDirectoryEntries(
                        fileSystem,
                        dataRoot,
                        max_authority_directory_entries,
                        cancellationToken),
                    "skin-mutation-staging");

                if (stagingEntry == null
                    || !string.Equals(
                        stagingEntry.Name,
                        "skin-mutation-staging",
                        StringComparison.Ordinal)
                    || stagingEntry.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                    || stagingEntry.Metadata.IsReparsePoint
                    || toMutationIdentity(stagingEntry.Metadata.Identity)
                    != expectedStagedRootIdentity)
                {
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus.RootIdentityMismatch);
                }

                OpenedDirectory stagingRoot = openExpectedDirectory(
                    fileSystem,
                    dataRoot,
                    stagingEntry.Name,
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    inspectionHandles);

                if (toMutationIdentity(stagingRoot.Metadata.Identity)
                    != expectedStagedRootIdentity)
                {
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus.RootIdentityMismatch);
                }

                WindowsSkinPackageDirectoryEntry[] stagingInventory =
                    canonicaliseDirectoryEntries(
                        getDirectoryEntries(
                            fileSystem,
                            stagingRoot.Handle,
                            max_authority_directory_entries,
                            cancellationToken));
                WindowsSkinPackageDirectoryEntry[] managedInventory =
                    getCurrentManagedRootInventory(cancellationToken);
                WindowsSkinPackageDirectoryEntry? source =
                    getOptionalExactNameEntry(stagingInventory, sourceName);
                WindowsSkinPackageDirectoryEntry? target =
                    getOptionalExactNameEntry(managedInventory, targetName);

                if ((source != null
                     && !string.Equals(
                         source.Name,
                         sourceName,
                         StringComparison.Ordinal))
                    || (target != null
                        && !string.Equals(
                            target.Name,
                        targetName,
                        StringComparison.Ordinal)))
                {
                    validateInspectionRoots(
                        stagingRoot,
                        stagingInventory,
                        cancellationToken);
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus
                            .IdentityMismatch);
                }

                if (source != null && target != null)
                {
                    captureInspectionMetadata(
                        stagingRoot.Handle,
                        source,
                        out _,
                        cancellationToken);
                    captureInspectionMetadata(
                        mutationManagedRoot!,
                        target,
                        out _,
                        cancellationToken);
                    validateInspectionRoots(
                        stagingRoot,
                        stagingInventory,
                        cancellationToken);
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus.Both);
                }

                if (source == null && target == null)
                {
                    validateInspectionRoots(
                        stagingRoot,
                        stagingInventory,
                        cancellationToken);
                    validateExactStagedInspectionState(
                        stagingRoot.Handle,
                        sourceName,
                        targetName,
                        expectedSourceIdentity,
                        SkinManagedFolderStagedImportInspectionStatus.Neither,
                        cancellationToken);
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus.Neither);
                }

                WindowsSkinPackageDirectoryEntry existing = source ?? target!;

                if (toMutationIdentity(existing.Metadata.Identity)
                    != expectedSourceIdentity)
                {
                    return stagedInspection(
                        SkinManagedFolderStagedImportInspectionStatus.IdentityMismatch);
                }

                SkinManagedFolderPackageMetadata? metadata =
                    captureInspectionMetadata(
                        source != null
                            ? stagingRoot.Handle
                            : mutationManagedRoot!,
                        existing,
                        out string? treeFingerprint,
                        cancellationToken);
                validateInspectionRoots(
                    stagingRoot,
                    stagingInventory,
                    cancellationToken);
                SkinManagedFolderStagedImportInspectionStatus determinateStatus =
                    source != null
                        ? SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                        : SkinManagedFolderStagedImportInspectionStatus.TargetOnly;
                validateExactStagedInspectionState(
                    stagingRoot.Handle,
                    sourceName,
                    targetName,
                    expectedSourceIdentity,
                    determinateStatus,
                    cancellationToken);

                return new SkinManagedFolderStagedImportInspection(
                    determinateStatus,
                    ManagedRootIdentity,
                    source == null ? expectedSourceIdentity : null,
                    metadata,
                    treeFingerprint);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason
                == SkinManagedPackageCaptureRejectionReason.PackageUnavailable)
            {
                return stagedInspection(
                    SkinManagedFolderStagedImportInspectionStatus.RootIdentityMismatch);
            }
            finally
            {
                disposeHandles(inspectionHandles);
            }

            SkinManagedFolderStagedImportInspection stagedInspection(
                SkinManagedFolderStagedImportInspectionStatus status)
                => new SkinManagedFolderStagedImportInspection(
                    status,
                    ManagedRootIdentity);
        }

        internal void CleanupExactStagedMutationSource(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            SkinManagedFolderStagedImportInspection before =
                InspectStagedMutationImportState(
                    operationId,
                    targetManagedRelativePath,
                    expectedStagedRootIdentity,
                    expectedSourceIdentity,
                    cancellationToken);

            if (before.Status == SkinManagedFolderStagedImportInspectionStatus.Neither)
                return;

            if (before.Status != SkinManagedFolderStagedImportInspectionStatus.SourceOnly)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            if (stagedMutationCaptured
                && !stagedMutationForwardApplied
                && mutationSource is { } heldSource
                && mutationSourceTree != null
                && mutationStagingRoot is { } heldStagingRoot
                && toMutationIdentity(heldSource.Metadata.Identity)
                   == expectedSourceIdentity
                && toMutationIdentity(heldStagingRoot.Metadata.Identity)
                   == expectedStagedRootIdentity)
            {
                validateHeldMutationTree(
                    fileSystem,
                    mutationSourceTree,
                    cancellationToken);
                deleteHeldTree(mutationSourceTree);
                mutationSource = null;
                mutationSourceTree = null;
                stagedMutationRolledBack = true;
            }
            else
            {
                deleteFreshStagedSource(
                    operationId,
                    expectedStagedRootIdentity,
                    expectedSourceIdentity,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderStagedImportInspection after =
                InspectStagedMutationImportState(
                    operationId,
                    targetManagedRelativePath,
                    expectedStagedRootIdentity,
                    expectedSourceIdentity,
                    cancellationToken);

            if (after.Status != SkinManagedFolderStagedImportInspectionStatus.Neither)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
        }

        /// <summary>
        /// Verifies that the authority chain is still linked to the held identities and that the complete direct-child
        /// inventory is byte-for-byte metadata-equivalent to the baseline enumeration.
        /// </summary>
        public void ValidateCompleteAndStable(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (stagedMutationCaptured)
            {
                if (stagedMutationForwardApplied)
                {
                    validateMovedStagedLocation(
                        getManagedDirectChildName(
                            mutationTargetNameSlot!.ManagedRelativePath),
                        cancellationToken);
                }
                else if (stagedMutationRolledBack)
                    validateRolledBackStagedLocation(cancellationToken);
                else
                    validatePreparedStagedLocation(cancellationToken);

                return;
            }

            if (mutationRenameState == MutationRenameState.DeleteApplied)
            {
                validateMutationDeleteLocation(cancellationToken);
                return;
            }

            if (mutationRenameState == MutationRenameState.ForwardApplied)
            {
                validateMutationRenameLocation(
                    getManagedDirectChildName(mutationTargetNameSlot!.ManagedRelativePath),
                    mutationSourceOriginalName!,
                    cancellationToken);
                return;
            }

            validateBaselineCompleteAndStable(cancellationToken);

            if (managedCopyStagingPrepared
                && !managedCopyProvisionalWritten
                && managedCopyInitialStagingEntries != null)
            {
                if (managedCopyProvisionalRootCreated)
                    validateManagedCopyStagingWithRoot(managedCopyOperationId, cancellationToken);
                else if (mutationStagingRoot is { } stagingRoot)
                    validateExactInventory(
                        stagingRoot.Handle,
                        managedCopyInitialStagingEntries,
                        cancellationToken);
            }
        }

        private void validateBaselineCompleteAndStable(CancellationToken cancellationToken)
        {
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

        private void ensureMutationManagedRoot(CancellationToken cancellationToken)
        {
            if (mutationManagedRoot != null)
                return;

            OpenedDirectory opened = openExpectedDirectory(
                fileSystem,
                dataRoot,
                SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY,
                WindowsSkinPackageOpenMode.MutationManagedRootDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                handles);

            if (opened.Metadata.Identity != managedRootMetadata.Identity)
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);

            authorityLinks.Add(new AuthorityLinkRecord(
                dataRoot,
                opened.CanonicalName,
                opened.Metadata,
                WindowsSkinPackageOpenMode.CapturedDirectory));
            authorityNodes.Add(new NodeRecord(opened.Handle, opened.Metadata));
            mutationManagedRoot = opened.Handle;
        }

        private SkinManagedFolderStagedImportFilesystemResult captureAndValidateMovedStagedPackage(
            string targetName,
            string expectedContentRevision,
            string expectedTreeFingerprint,
            CancellationToken cancellationToken)
        {
            validateMovedStagedLocation(targetName, cancellationToken);
            WindowsSkinPackageDirectoryEntry[] current =
                getCurrentManagedRootInventory(cancellationToken);
            WindowsSkinPackageDirectoryEntry? target =
                getOptionalExactNameEntry(current, targetName);

            if (target == null
                || mutationSource is not { } source
                || target.Metadata.Identity != source.Metadata.Identity)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            SkinManagedPackageCaptureResult captured =
                packageCapture.CaptureProvisionalChild(
                    mutationManagedRoot!,
                    target,
                    cancellationToken: cancellationToken);

            if (!captured.IsSuccess
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    captured.PhysicalTreeFingerprint))
            {
                throw reject(
                    captured.IsSuccess
                        ? SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture
                        : captured.RejectionReason
                    == SkinManagedPackageCaptureRejectionReason.CapsuleRejected
                        ? SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture
                        : captured.RejectionReason);
            }

            SkinPackageRevisionCapsule capsule = captured.Capsule!;

            try
            {
                if (!string.Equals(
                        capsule.ContentRevision,
                        expectedContentRevision,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        captured.PhysicalTreeFingerprint,
                        expectedTreeFingerprint,
                        StringComparison.Ordinal))
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                }

                validateMovedStagedLocation(targetName, cancellationToken);
                return new SkinManagedFolderStagedImportFilesystemResult(
                    toMutationIdentity(target.Metadata.Identity),
                    captured.PhysicalTreeFingerprint!,
                    capsule);
            }
            catch
            {
                capsule.Dispose();
                throw;
            }
        }

        private void validatePreparedStagedLocation(
            CancellationToken cancellationToken)
        {
            if (mutationStagingRoot is not { } stagingRoot
                || mutationStagingBaselineEntries == null
                || mutationSource is not { } source
                || mutationSourceTree == null
                || mutationStagedSourceBaselineEntry == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            validateBaselineCompleteAndStable(cancellationToken);
            validateLinkInventory(
                stagingRoot.Handle,
                mutationStagingBaselineEntries,
                cancellationToken);
            validateHeldMutationTree(
                fileSystem,
                mutationSourceTree,
                cancellationToken);

            WindowsSkinPackageEntryMetadata held =
                fileSystem.QueryMetadata(source.Handle);

            if (held.Identity != source.Metadata.Identity
                || held.Kind != WindowsSkinPackageEntryKind.Directory
                || held.IsReparsePoint
                || held.DeletePending)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }
        }

        private void validateMovedStagedLocation(
            string targetName,
            CancellationToken cancellationToken)
        {
            if (mutationStagingRoot is not { } stagingRoot
                || mutationStagingBaselineEntries == null
                || mutationStagedSourceBaselineEntry == null
                || mutationSource is not { } source
                || mutationSourceTree == null
                || mutationManagedRoot == null
                || mutationSourceOriginalName == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            cancellationToken.ThrowIfCancellationRequested();
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
            validateInventoryWithoutEntry(
                stagingRoot.Handle,
                mutationStagingBaselineEntries,
                mutationStagedSourceBaselineEntry,
                cancellationToken);

            WindowsSkinPackageDirectoryEntry[] managed =
                getCurrentManagedRootInventory(cancellationToken);

            if (managed.Length != baselineEntries.Length + 1)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            WindowsSkinPackageDirectoryEntry? target =
                getOptionalExactNameEntry(managed, targetName);

            if (target == null
                || !string.Equals(target.Name, targetName, StringComparison.Ordinal)
                || target.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                || target.Metadata.IsReparsePoint
                || target.Metadata.DeletePending
                || target.Metadata.Identity != source.Metadata.Identity)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            foreach (WindowsSkinPackageDirectoryEntry baseline in baselineEntries)
            {
                WindowsSkinPackageDirectoryEntry? matching =
                    managed.SingleOrDefault(
                        entry => string.Equals(
                            entry.Name,
                            baseline.Name,
                            StringComparison.Ordinal));

                if (matching == null
                    || !DirectoryEntryComparer.Instance.Equals(
                        baseline,
                        matching))
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }
            }

            WindowsSkinPackageEntryMetadata heldMetadata =
                fileSystem.QueryMetadata(source.Handle);

            if (heldMetadata.IsReparsePoint
                || heldMetadata.Kind != WindowsSkinPackageEntryKind.Directory
                || heldMetadata.Identity != source.Metadata.Identity
                || heldMetadata.NumberOfLinks != 1
                || heldMetadata.DeletePending)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            if (mutationSourceTree.DescendantsReleased)
            {
                OpenedDirectory reopened = openExpectedDirectory(
                    fileSystem,
                    mutationManagedRoot,
                    targetName,
                    WindowsSkinPackageOpenMode.ProvisionalDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged,
                    cancellationToken,
                    handles);
                HeldMutationTree recaptured = holdMutationTree(
                    reopened.Handle,
                    reopened.Metadata,
                    handles,
                    cancellationToken,
                    provisional: true);
                validateRecapturedMutationTree(
                    mutationSourceTree,
                    recaptured);
                mutationSourceTree = recaptured;
            }

            validateHeldMutationTree(
                fileSystem,
                mutationSourceTree,
                cancellationToken);
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        private void validateRolledBackStagedLocation(
            CancellationToken cancellationToken)
        {
            if (mutationStagingRoot is not { } stagingRoot
                || mutationStagingBaselineEntries == null
                || mutationStagedSourceBaselineEntry == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
            validateExactInventory(
                mutationManagedRoot ?? managedRoot,
                baselineEntries,
                cancellationToken);
            validateInventoryWithoutEntry(
                stagingRoot.Handle,
                mutationStagingBaselineEntries,
                mutationStagedSourceBaselineEntry,
                cancellationToken);
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        private void validateInspectionRoots(
            OpenedDirectory stagingRoot,
            WindowsSkinPackageDirectoryEntry[] stagingBaseline,
            CancellationToken cancellationToken)
        {
            validateLinkInventory(
                stagingRoot.Handle,
                stagingBaseline,
                cancellationToken);
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        private SkinManagedFolderPackageMetadata? captureInspectionMetadata(
            IWindowsSkinPackageCaptureHandle parent,
            WindowsSkinPackageDirectoryEntry candidate,
            out string? treeFingerprint,
            CancellationToken cancellationToken)
        {
            treeFingerprint = null;
            SkinManagedPackageCaptureResult captured =
                packageCapture.CaptureProvisionalChild(
                    parent,
                    candidate,
                    cancellationToken: cancellationToken);

            if (!captured.IsSuccess)
            {
                if (captured.RejectionReason
                    == SkinManagedPackageCaptureRejectionReason.CapsuleRejected)
                {
                    return null;
                }

                throw reject(captured.RejectionReason);
            }

            if (!SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    captured.PhysicalTreeFingerprint))
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            }

            using SkinPackageRevisionCapsule capsule = captured.Capsule!;
            treeFingerprint = captured.PhysicalTreeFingerprint;

            return SkinManagedFolderPackageMetadataReader.TryRead(
                capsule,
                out SkinManagedFolderPackageMetadata? metadata)
                ? metadata
                : null;
        }

        private void validateExactStagedInspectionState(
            IWindowsSkinPackageCaptureHandle stagingRoot,
            string sourceName,
            string targetName,
            SkinManagedFolderPhysicalIdentity expectedIdentity,
            SkinManagedFolderStagedImportInspectionStatus expectedStatus,
            CancellationToken cancellationToken)
        {
            WindowsSkinPackageDirectoryEntry? source =
                getOptionalExactNameEntry(
                    canonicaliseDirectoryEntries(
                        getDirectoryEntries(
                            fileSystem,
                            stagingRoot,
                            max_authority_directory_entries,
                            cancellationToken)),
                    sourceName);
            WindowsSkinPackageDirectoryEntry? target =
                getOptionalExactNameEntry(
                    getCurrentManagedRootInventory(cancellationToken),
                    targetName);

            bool sourceExact = isExactSlot(source, sourceName);
            bool targetExact = isExactSlot(target, targetName);
            bool valid = expectedStatus switch
            {
                SkinManagedFolderStagedImportInspectionStatus.SourceOnly =>
                    sourceExact && target == null,
                SkinManagedFolderStagedImportInspectionStatus.TargetOnly =>
                    source == null && targetExact,
                SkinManagedFolderStagedImportInspectionStatus.Neither =>
                    source == null && target == null,
                _ => false,
            };

            if (!valid)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason
                        .EntryChangedDuringCapture);
            }

            return;

            bool isExactSlot(
                WindowsSkinPackageDirectoryEntry? current,
                string expectedName)
                => current != null
                   && string.Equals(
                       current.Name,
                       expectedName,
                       StringComparison.Ordinal)
                   && current.Metadata.Kind
                   == WindowsSkinPackageEntryKind.Directory
                   && !current.Metadata.IsReparsePoint
                   && !current.Metadata.DeletePending
                   && toMutationIdentity(current.Metadata.Identity)
                   == expectedIdentity;
        }

        private void deleteFreshStagedSource(
            Guid operationId,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken)
        {
            var cleanupHandles = new List<IWindowsSkinPackageCaptureHandle>();

            try
            {
                OpenedDirectory stagingRoot = openExpectedDirectory(
                    fileSystem,
                    dataRoot,
                    "skin-mutation-staging",
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    cleanupHandles);

                if (toMutationIdentity(stagingRoot.Metadata.Identity)
                    != expectedStagedRootIdentity)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }

                string sourceName = operationId.ToString("N");
                WindowsSkinPackageDirectoryEntry[] stagingInventory =
                    canonicaliseDirectoryEntries(
                        getDirectoryEntries(
                            fileSystem,
                            stagingRoot.Handle,
                            max_authority_directory_entries,
                            cancellationToken));
                WindowsSkinPackageDirectoryEntry? candidate =
                    getOptionalExactNameEntry(stagingInventory, sourceName);

                if (candidate == null
                    || !string.Equals(
                        candidate.Name,
                        sourceName,
                        StringComparison.Ordinal)
                    || toMutationIdentity(candidate.Metadata.Identity)
                       != expectedSourceIdentity)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }

                OpenedDirectory source = openExpectedDirectory(
                    fileSystem,
                    stagingRoot.Handle,
                    sourceName,
                    WindowsSkinPackageOpenMode.ProvisionalDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    cleanupHandles);
                HeldMutationTree tree = holdMutationTree(
                    source.Handle,
                    source.Metadata,
                    cleanupHandles,
                    cancellationToken,
                    provisional: true);
                captureInspectionMetadata(
                    stagingRoot.Handle,
                    candidate,
                    out _,
                    cancellationToken);
                validateHeldMutationTree(fileSystem, tree, cancellationToken);
                deleteHeldTree(tree, cleanupHandles);
            }
            finally
            {
                disposeHandles(cleanupHandles);
            }
        }

        private void deleteHeldTree(HeldMutationTree tree)
            => deleteHeldTree(tree, handles);

        private void deleteHeldTree(
            HeldMutationTree tree,
            List<IWindowsSkinPackageCaptureHandle> ownerHandles)
        {
            if (tree.DescendantsReleased)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            for (int i = tree.Nodes.Count - 1; i >= 0; i--)
            {
                IWindowsSkinPackageCaptureHandle handle = tree.Nodes[i].Handle;
                fileSystem.DeleteNoFollow(handle);
                handle.Dispose();
                ownerHandles.Remove(handle);
            }

            tree.DescendantsReleased = true;
        }

        private static void validateCreatedNode(
            WindowsSkinPackageEntryMetadata metadata,
            WindowsSkinPackageEntryKind expectedKind,
            long expectedLength)
        {
            if (!metadata.Identity.IsUsable
                || metadata.Kind != expectedKind
                || metadata.IsReparsePoint
                || metadata.DeletePending
                || metadata.NumberOfLinks != 1
                || (expectedKind == WindowsSkinPackageEntryKind.File && metadata.Length != expectedLength))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            }
        }

        private void validateManagedCopyStagingWithRoot(
            Guid? operationId,
            CancellationToken cancellationToken)
        {
            if (!managedCopyProvisionalRootCreated
                || managedCopyProvisionalRoot == null
                || mutationStagingRoot is not { } stagingRoot
                || managedCopyInitialStagingEntries == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            WindowsSkinPackageEntryMetadata held = fileSystem.QueryMetadata(managedCopyProvisionalRoot);

            if (held.Identity != managedCopyProvisionalRootMetadata.Identity
                || held.Kind != WindowsSkinPackageEntryKind.Directory
                || held.IsReparsePoint
                || held.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            WindowsSkinPackageDirectoryEntry[] current = canonicaliseDirectoryEntries(
                getDirectoryEntries(
                    fileSystem,
                    stagingRoot.Handle,
                    managedCopyInitialStagingEntries.Length + 1,
                    cancellationToken));
            WindowsSkinPackageDirectoryEntry[] additions = current
                .Where(entry => managedCopyInitialStagingEntries.All(baseline => !namesEqual(baseline.Name, entry.Name)))
                .ToArray();

            if (current.Length != managedCopyInitialStagingEntries.Length + 1
                || additions.Length != 1
                || additions[0].Metadata.Identity != held.Identity
                || (operationId != null
                    && !string.Equals(additions[0].Name, operationId.Value.ToString("N"), StringComparison.Ordinal))
                || !managedCopyInitialStagingEntries.SequenceEqual(
                    current.Where(entry => !ReferenceEquals(entry, additions[0])),
                    DirectoryEntryComparer.Instance))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }
        }

        private static (string ParentPath, string ChildName) splitResourcePath(string relativePath)
        {
            int separator = relativePath.LastIndexOf('/');
            return separator < 0
                ? (string.Empty, relativePath)
                : (relativePath[..separator], relativePath[(separator + 1)..]);
        }

        private static void assertManagedCopyTreeMatches(
            HeldMutationTree tree,
            SkinManagedCopyLogicalManifest manifest)
        {
            if (tree.Nodes.Count != manifest.Entries.Count + 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

            var expected = manifest.Entries.ToDictionary(
                entry => entry.RelativePath,
                entry => entry.Kind == SkinExternalPackageLogicalEntryKind.Directory
                    ? WindowsSkinPackageEntryKind.Directory
                    : WindowsSkinPackageEntryKind.File,
                StringComparer.OrdinalIgnoreCase);

            foreach (MutationTreeNodeRecord node in tree.Nodes.Skip(1))
            {
                if (!expected.Remove(node.RelativePath, out WindowsSkinPackageEntryKind kind)
                    || node.Baseline.Kind != kind)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }
            }

            if (expected.Count != 0)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
        }

        private static void assertManagedCopyTreeMatchesEvidence(
            HeldMutationTree tree,
            IReadOnlyDictionary<string, ManagedCopyCreatedNodeEvidence> createdNodes)
        {
            if (tree.Nodes.Count != createdNodes.Count + 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

            var remaining = new Dictionary<string, ManagedCopyCreatedNodeEvidence>(
                createdNodes,
                StringComparer.OrdinalIgnoreCase);

            foreach (MutationTreeNodeRecord node in tree.Nodes.Skip(1))
            {
                if (!remaining.Remove(node.RelativePath, out ManagedCopyCreatedNodeEvidence expected)
                    || node.Baseline.Identity != expected.Identity
                    || node.Baseline.Kind != expected.Kind
                    || node.Baseline.CreationTime != expected.CreationTime)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }
            }

            if (remaining.Count != 0)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
        }

        private static SkinManagedCopyProvisionalInspectionStatus classifyManagedCopyProvisionalTree(
            HeldMutationTree tree,
            SkinManagedCopyLogicalManifest manifest)
        {
            if (tree.Nodes.Count == 1)
                return SkinManagedCopyProvisionalInspectionStatus.Empty;

            var expected = manifest.Entries.ToDictionary(
                entry => entry.RelativePath,
                StringComparer.OrdinalIgnoreCase);
            bool complete = tree.Nodes.Count == manifest.Entries.Count + 1;

            foreach (MutationTreeNodeRecord node in tree.Nodes.Skip(1))
            {
                if (!expected.TryGetValue(node.RelativePath, out SkinManagedCopyLogicalManifest.Entry entry)
                    || !string.Equals(node.RelativePath, entry.RelativePath, StringComparison.Ordinal))
                {
                    return SkinManagedCopyProvisionalInspectionStatus.ManifestMismatch;
                }

                WindowsSkinPackageEntryKind expectedKind = entry.Kind switch
                {
                    SkinExternalPackageLogicalEntryKind.Directory => WindowsSkinPackageEntryKind.Directory,
                    SkinExternalPackageLogicalEntryKind.File => WindowsSkinPackageEntryKind.File,
                    _ => throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest),
                };

                if (node.Baseline.Kind != expectedKind
                    || (expectedKind == WindowsSkinPackageEntryKind.File
                        && node.Baseline.Length > entry.Length))
                {
                    return SkinManagedCopyProvisionalInspectionStatus.ManifestMismatch;
                }

                if (expectedKind == WindowsSkinPackageEntryKind.File
                    && node.Baseline.Length != entry.Length)
                {
                    complete = false;
                }

                expected.Remove(node.RelativePath);
            }

            return complete && expected.Count == 0
                ? SkinManagedCopyProvisionalInspectionStatus.Complete
                : SkinManagedCopyProvisionalInspectionStatus.Partial;
        }

        private void validateExactInventory(
            IWindowsSkinPackageCaptureHandle directory,
            WindowsSkinPackageDirectoryEntry[] expected,
            CancellationToken cancellationToken)
        {
            WindowsSkinPackageDirectoryEntry[] current;

            try
            {
                current = canonicaliseDirectoryEntries(
                    getDirectoryEntries(
                        fileSystem,
                        directory,
                        expected.Length,
                        cancellationToken));
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason
                == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            if (!expected.SequenceEqual(
                    current,
                    DirectoryEntryComparer.Instance))
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }
        }

        private void validateLinkInventory(
            IWindowsSkinPackageCaptureHandle directory,
            WindowsSkinPackageDirectoryEntry[] expected,
            CancellationToken cancellationToken)
        {
            WindowsSkinPackageDirectoryEntry[] current;

            try
            {
                current = canonicaliseDirectoryEntries(
                    getDirectoryEntries(
                        fileSystem,
                        directory,
                        expected.Length,
                        cancellationToken));
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason
                == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            if (!mutationInventoryLinksMatch(expected, current))
            {
                throw reject(
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }
        }

        private void validateInventoryWithoutEntry(
            IWindowsSkinPackageCaptureHandle directory,
            WindowsSkinPackageDirectoryEntry[] baseline,
            WindowsSkinPackageDirectoryEntry removed,
            CancellationToken cancellationToken)
        {
            WindowsSkinPackageDirectoryEntry[] expected =
                baseline.Where(entry => !ReferenceEquals(entry, removed)).ToArray();
            validateExactInventory(directory, expected, cancellationToken);
        }

        private static string createMutationDeleteNodeManifest(
            HeldMutationTree tree)
            => SkinManagedFolderDeleteManifest.Create(
                tree.Nodes.Select(createMutationDeleteNodeFingerprint));

        private static string createMutationDeleteNodeFingerprint(
            MutationTreeNodeRecord node)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(
                       stream,
                       new UTF8Encoding(false, true),
                       leaveOpen: true))
            {
                WindowsSkinPackageEntryMetadata metadata = node.Baseline;
                writer.Write(delete_node_fingerprint_domain);
                writer.Write(node.RelativePath);
                writer.Write(metadata.Identity.VolumeSerialNumber);
                writer.Write(metadata.Identity.FileIdPart0);
                writer.Write(metadata.Identity.FileIdPart1);
                writer.Write((int)metadata.Kind);
                writer.Write(metadata.CreationTime);
                writer.Write(metadata.FileAttributes);
                writer.Write(metadata.ReparseTag);
                writer.Write(metadata.NumberOfLinks);
                writer.Write(metadata.DeletePending);

                if (metadata.Kind == WindowsSkinPackageEntryKind.File)
                {
                    writer.Write(metadata.Length);
                    writer.Write(metadata.LastWriteTime);
                    writer.Write(metadata.ChangeTime);
                }
            }

            return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
                          .ToLowerInvariant();
        }

        private HeldMutationTree holdMutationTree(
            IWindowsSkinPackageCaptureHandle root,
            WindowsSkinPackageEntryMetadata rootMetadata,
            List<IWindowsSkinPackageCaptureHandle> treeHandles,
            CancellationToken cancellationToken,
            bool provisional = false,
            bool deleteExclusive = false)
        {
            if (deleteExclusive && !provisional)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            var nodes = new List<MutationTreeNodeRecord>();
            var directories = new List<MutationTreeDirectoryRecord>();
            var identities = new HashSet<WindowsSkinPackagePhysicalIdentity>();
            SkinPackageRevisionCapsuleLimits limits =
                SkinPackageRevisionCapsuleLimits.Default;
            var pending = new Stack<PendingMutationTreeNode>();
            pending.Push(new PendingMutationTreeNode(root, rootMetadata, string.Empty, 0));

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingMutationTreeNode current = pending.Pop();
                WindowsSkinPackageEntryMetadata metadata = current.Metadata;

                if (metadata.IsReparsePoint)
                    throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                if (!metadata.Identity.IsUsable || metadata.DeletePending)
                    throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);

                if (metadata.NumberOfLinks != 1)
                    throw reject(SkinManagedPackageCaptureRejectionReason.HardLinkedFile);

                if (!identities.Add(metadata.Identity))
                    throw reject(SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity);

                if (nodes.Count >= SkinManagedFolderDeleteManifest.MaximumNodeCount)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);
                }

                nodes.Add(new MutationTreeNodeRecord(
                    current.Handle,
                    metadata,
                    current.RelativePath));

                if (metadata.Kind != WindowsSkinPackageEntryKind.Directory)
                    continue;

                int discoveredLogicalEntries = nodes.Count - 1 + pending.Count;
                int remainingEntries = limits.MaxEntryCount - discoveredLogicalEntries;

                if (remainingEntries < 0)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);
                }
                WindowsSkinPackageDirectoryEntry[] entries;

                try
                {
                    entries = canonicaliseDirectoryEntries(
                        getDirectoryEntries(
                            fileSystem,
                            current.Handle,
                            remainingEntries,
                            cancellationToken));
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                    exception.RejectionReason
                    == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);
                }

                var canonicalNames = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (WindowsSkinPackageDirectoryEntry entry in entries)
                {
                    string canonicalName;

                    try
                    {
                        canonicalName = entry.Name.Normalize(
                            NormalizationForm.FormC);
                    }
                    catch (ArgumentException)
                    {
                        throw reject(
                            SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                    }

                    if (!canonicalNames.Add(canonicalName))
                    {
                        throw reject(
                            SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
                    }
                }

                directories.Add(new MutationTreeDirectoryRecord(
                    current.Handle,
                    entries));

                for (int i = entries.Length - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WindowsSkinPackageDirectoryEntry entry = entries[i];

                    if (!isValidRequestSegment(entry.Name))
                        throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

                    if (entry.Metadata.IsReparsePoint)
                    {
                        throw reject(
                            SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);
                    }

                    string rawRelativePath = string.IsNullOrEmpty(
                        current.RelativePath)
                        ? entry.Name
                        : $"{current.RelativePath}/{entry.Name}";

                    if (!SkinPackageResourceNameValidator.TryNormalise(
                            rawRelativePath,
                            out string relativePath,
                            out int depth)
                        || !string.Equals(
                            rawRelativePath,
                            relativePath,
                            StringComparison.Ordinal)
                        || depth != current.Depth + 1
                        || depth > limits.MaxDepth
                        || relativePath.Length > limits.MaxResourceNameLength)
                    {
                        throw reject(
                            SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                    }

                    WindowsSkinPackageOpenMode mode = entry.Metadata.Kind switch
                    {
                        WindowsSkinPackageEntryKind.Directory when deleteExclusive =>
                            WindowsSkinPackageOpenMode.DeleteExclusiveDirectory,
                        WindowsSkinPackageEntryKind.File when deleteExclusive =>
                            WindowsSkinPackageOpenMode.DeleteExclusiveFile,
                        WindowsSkinPackageEntryKind.Directory when provisional =>
                            WindowsSkinPackageOpenMode.ProvisionalDirectory,
                        WindowsSkinPackageEntryKind.File when provisional =>
                            WindowsSkinPackageOpenMode.ProvisionalFile,
                        WindowsSkinPackageEntryKind.Directory =>
                            WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory,
                        WindowsSkinPackageEntryKind.File =>
                            WindowsSkinPackageOpenMode.MutationSourceVerificationFile,
                        _ => throw reject(
                            SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType),
                    };
                    IWindowsSkinPackageCaptureHandle child = own(
                        fileSystem.OpenChildNoFollow(
                            current.Handle,
                            entry.Name,
                            mode,
                            SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture),
                        treeHandles);
                    WindowsSkinPackageEntryMetadata childMetadata =
                        fileSystem.QueryMetadata(child);
                    validateOpenedEntry(entry.Metadata, childMetadata);
                    pending.Push(new PendingMutationTreeNode(
                        child,
                        childMetadata,
                        relativePath,
                        depth));
                }
            }

            var tree = new HeldMutationTree(nodes, directories, provisional);
            validateHeldMutationTree(fileSystem, tree, cancellationToken);
            return tree;
        }

        private static void validateHeldMutationTree(
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            HeldMutationTree tree,
            CancellationToken cancellationToken)
        {
            if (tree.DescendantsReleased)
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            validateNodes();

            foreach (MutationTreeDirectoryRecord directory in tree.Directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WindowsSkinPackageDirectoryEntry[] current;

                try
                {
                    current = canonicaliseDirectoryEntries(
                        getDirectoryEntries(
                            fileSystem,
                            directory.Handle,
                            directory.Baseline.Length,
                            cancellationToken));
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                    exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }

                bool inventoryMatches = tree.Provisional
                    ? mutationInventoryLinksMatch(directory.Baseline, current)
                    : directory.Baseline.SequenceEqual(
                        current,
                        DirectoryEntryComparer.Instance);

                if (!inventoryMatches)
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            validateNodes();
            return;

            void validateNodes()
            {
                foreach (MutationTreeNodeRecord node in tree.Nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WindowsSkinPackageEntryMetadata current = fileSystem.QueryMetadata(node.Handle);

                    if (current.IsReparsePoint)
                        throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                    if (!current.Identity.IsUsable
                        || current.Identity != node.Baseline.Identity
                        || current.Kind != node.Baseline.Kind
                        || current.CreationTime != node.Baseline.CreationTime
                        || current.FileAttributes != node.Baseline.FileAttributes
                        || current.ReparseTag != node.Baseline.ReparseTag
                        || current.NumberOfLinks != 1
                        || current.DeletePending
                        || (current.Kind == WindowsSkinPackageEntryKind.File
                            && (current.Length != node.Baseline.Length
                                || current.LastWriteTime != node.Baseline.LastWriteTime
                                || current.ChangeTime != node.Baseline.ChangeTime)))
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                    }
                }
            }
        }

        private static void releaseMutationTreeDescendants(
            HeldMutationTree tree,
            IWindowsSkinPackageCaptureHandle source)
        {
            if (tree.DescendantsReleased
                || tree.Nodes.Count == 0
                || !ReferenceEquals(tree.Nodes[0].Handle, source))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            for (int i = tree.Nodes.Count - 1; i >= 1; i--)
                tree.Nodes[i].Handle.Dispose();

            tree.DescendantsReleased = true;
        }

        private void releaseHeldMutationTreeForDeleteRecapture(
            HeldMutationTree heldDeleteTree)
        {
            if (heldDeleteTree.DescendantsReleased
                || heldDeleteTree.Nodes.Count == 0
                || heldDeleteTree.Nodes.Any(node => !handles.Contains(node.Handle)))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            IWindowsSkinPackageCaptureHandle? originalSourceHandle =
                mutationSource?.Handle;

            for (int i = heldDeleteTree.Nodes.Count - 1; i >= 0; i--)
            {
                IWindowsSkinPackageCaptureHandle handle =
                    heldDeleteTree.Nodes[i].Handle;
                handles.Remove(handle);
                handle.Dispose();
            }

            heldDeleteTree.DescendantsReleased = true;

            if (originalSourceHandle != null
                && heldDeleteTree.Nodes.All(node =>
                    !ReferenceEquals(node.Handle, originalSourceHandle)))
            {
                if (!handles.Remove(originalSourceHandle))
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                }

                originalSourceHandle.Dispose();
            }

            mutationSource = null;
            mutationSourceTree = null;
        }

        private static bool mutationInventoryLinksMatch(
            WindowsSkinPackageDirectoryEntry[] expected,
            WindowsSkinPackageDirectoryEntry[] actual)
        {
            if (expected.Length != actual.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                if (!string.Equals(
                        expected[i].Name,
                        actual[i].Name,
                        StringComparison.Ordinal)
                    || expected[i].Metadata.Identity
                       != actual[i].Metadata.Identity
                    || expected[i].Metadata.Kind != actual[i].Metadata.Kind)
                {
                    return false;
                }
            }

            return true;
        }

        private static void validateRecapturedMutationTree(
            HeldMutationTree baseline,
            HeldMutationTree recaptured)
        {
            if (!baseline.DescendantsReleased
                || recaptured.DescendantsReleased
                || baseline.Nodes.Count != recaptured.Nodes.Count
                || baseline.Directories.Count != recaptured.Directories.Count)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            for (int i = 0; i < baseline.Nodes.Count; i++)
            {
                WindowsSkinPackageEntryMetadata expected = baseline.Nodes[i].Baseline;
                WindowsSkinPackageEntryMetadata actual = recaptured.Nodes[i].Baseline;

                if (!string.Equals(
                        baseline.Nodes[i].RelativePath,
                        recaptured.Nodes[i].RelativePath,
                        StringComparison.Ordinal))
                {
                    throw reject(
                        SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }

                if (i == 0)
                {
                    // A successful rename is allowed to advance the renamed directory's own change timestamps.
                    if (actual.IsReparsePoint
                        || actual.Kind != WindowsSkinPackageEntryKind.Directory
                        || actual.Identity != expected.Identity
                        || actual.Length != expected.Length
                        || actual.CreationTime != expected.CreationTime
                        || actual.FileAttributes != expected.FileAttributes
                        || actual.ReparseTag != expected.ReparseTag
                        || actual.NumberOfLinks != expected.NumberOfLinks
                        || actual.DeletePending)
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                    }
                }
                else if (!actual.Equals(expected))
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                }
            }

            for (int i = 0; i < baseline.Directories.Count; i++)
            {
                WindowsSkinPackageDirectoryEntry[] expectedEntries = baseline.Directories[i].Baseline;
                WindowsSkinPackageDirectoryEntry[] actualEntries = recaptured.Directories[i].Baseline;

                if (expectedEntries.Length != actualEntries.Length)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }

                for (int j = 0; j < expectedEntries.Length; j++)
                {
                    if (!string.Equals(expectedEntries[j].Name, actualEntries[j].Name, StringComparison.Ordinal)
                        || expectedEntries[j].Metadata.Identity != actualEntries[j].Metadata.Identity
                        || expectedEntries[j].Metadata.Kind != actualEntries[j].Metadata.Kind)
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                    }
                }
            }
        }

        private WindowsSkinPackagePhysicalIdentity holdAndValidateInspectionTree(
            WindowsSkinPackageDirectoryEntry entry,
            List<IWindowsSkinPackageCaptureHandle> inspectionHandles,
            CancellationToken cancellationToken)
        {
            if (entry.Metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (entry.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                || !entry.Metadata.Identity.IsUsable
                || entry.Metadata.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);
            }

            OpenedDirectory opened = openExpectedDirectory(
                fileSystem,
                mutationManagedRoot!,
                entry.Name,
                WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                cancellationToken,
                inspectionHandles);
            HeldMutationTree tree = holdMutationTree(
                opened.Handle,
                opened.Metadata,
                inspectionHandles,
                cancellationToken);
            validateHeldMutationTree(fileSystem, tree, cancellationToken);
            return opened.Metadata.Identity;
        }

        private void validateMutationRenameLocation(
            string expectedName,
            string absentName,
            CancellationToken cancellationToken)
        {
            if (mutationSource is not { } source
                || mutationSourceTree == null
                || mutationSourceBaselineEntry == null
                || mutationManagedRoot == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            cancellationToken.ThrowIfCancellationRequested();
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);

            if (!mutationSourceTree.DescendantsReleased)
                validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);

            WindowsSkinPackageDirectoryEntry[] current = getCurrentManagedRootInventory(cancellationToken);

            if (current.Length != baselineEntries.Length)
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

            WindowsSkinPackageDirectoryEntry? moved = getOptionalExactNameEntry(current, expectedName);

            if (moved == null
                || !string.Equals(moved.Name, expectedName, StringComparison.Ordinal)
                || getOptionalExactNameEntry(current, absentName) != null
                || moved.Metadata.IsReparsePoint
                || moved.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                || moved.Metadata.Identity != source.Metadata.Identity
                || moved.Metadata.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            foreach (WindowsSkinPackageDirectoryEntry baseline in baselineEntries)
            {
                if (ReferenceEquals(baseline, mutationSourceBaselineEntry))
                    continue;

                WindowsSkinPackageDirectoryEntry? matching =
                    current.SingleOrDefault(entry => string.Equals(entry.Name, baseline.Name, StringComparison.Ordinal));

                if (matching == null || !DirectoryEntryComparer.Instance.Equals(baseline, matching))
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            WindowsSkinPackageEntryMetadata heldMetadata = fileSystem.QueryMetadata(source.Handle);

            if (heldMetadata.IsReparsePoint
                || heldMetadata.Kind != WindowsSkinPackageEntryKind.Directory
                || heldMetadata.Identity != source.Metadata.Identity
                || heldMetadata.NumberOfLinks != 1
                || heldMetadata.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }

            if (mutationSourceTree.DescendantsReleased)
            {
                OpenedDirectory reopened = openExpectedDirectory(
                    fileSystem,
                    mutationManagedRoot,
                    expectedName,
                    WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged,
                    cancellationToken,
                    handles);
                HeldMutationTree recaptured = holdMutationTree(
                    reopened.Handle,
                    reopened.Metadata,
                    handles,
                    cancellationToken);
                validateRecapturedMutationTree(mutationSourceTree, recaptured);
                mutationSourceTree = recaptured;
            }

            validateHeldMutationTree(fileSystem, mutationSourceTree, cancellationToken);
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        private void validateMutationDeleteLocation(CancellationToken cancellationToken)
        {
            if (mutationDeleteRemovedBaselineEntry == null
                || mutationDeleteSourceName == null
                || mutationDeleteTombstoneName == null
                || mutationManagedRoot == null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            cancellationToken.ThrowIfCancellationRequested();
            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
            WindowsSkinPackageDirectoryEntry[] current = getCurrentManagedRootInventory(cancellationToken);

            if (current.Length != baselineEntries.Length - 1
                || getOptionalExactNameEntry(current, mutationDeleteSourceName) != null
                || getOptionalExactNameEntry(current, mutationDeleteTombstoneName) != null)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            foreach (WindowsSkinPackageDirectoryEntry baseline in baselineEntries)
            {
                if (ReferenceEquals(baseline, mutationDeleteRemovedBaselineEntry))
                    continue;

                WindowsSkinPackageDirectoryEntry? matching =
                    current.SingleOrDefault(entry => string.Equals(entry.Name, baseline.Name, StringComparison.Ordinal));

                if (matching == null || !DirectoryEntryComparer.Instance.Equals(baseline, matching))
                    throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }

            validateAuthorityNodes(fileSystem, authorityNodes, cancellationToken);
            validateAuthorityLinks(fileSystem, authorityLinks, cancellationToken);
        }

        private WindowsSkinPackageDirectoryEntry[] getCurrentManagedRootInventory(CancellationToken cancellationToken)
        {
            try
            {
                return canonicaliseDirectoryEntries(
                    getDirectoryEntries(
                        fileSystem,
                        mutationManagedRoot ?? managedRoot,
                        max_authority_directory_entries,
                        cancellationToken));
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            }
        }

        private static WindowsSkinPackageDirectoryEntry? getOptionalExactNameEntry(
            IEnumerable<WindowsSkinPackageDirectoryEntry> entries,
            string requestedName)
        {
            WindowsSkinPackageDirectoryEntry[] matches =
                entries.Where(entry => namesEqual(entry.Name, requestedName)).ToArray();

            if (matches.Length > 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            return matches.SingleOrDefault();
        }

        private static string getManagedDirectChildName(string managedRelativePath)
        {
            if (!SkinManagedFolderPath.TryNormalise(managedRelativePath, out string normalisedPath)
                || !string.Equals(managedRelativePath, normalisedPath, StringComparison.Ordinal))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            int separator = managedRelativePath.IndexOf('/');
            string childName = separator >= 0 ? managedRelativePath[(separator + 1)..] : string.Empty;

            if (!isValidRequestSegment(childName))
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            return childName;
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

        private sealed record HeldMutationTree(
            IReadOnlyList<MutationTreeNodeRecord> Nodes,
            IReadOnlyList<MutationTreeDirectoryRecord> Directories,
            bool Provisional)
        {
            internal bool DescendantsReleased { get; set; }
        }

        private readonly record struct MutationTreeNodeRecord(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Baseline,
            string RelativePath);

        private readonly record struct PendingMutationTreeNode(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Metadata,
            string RelativePath,
            int Depth);

        private readonly record struct MutationTreeDirectoryRecord(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageDirectoryEntry[] Baseline);

        private readonly record struct ManagedCopyCreatedNodeEvidence(
            WindowsSkinPackagePhysicalIdentity Identity,
            WindowsSkinPackageEntryKind Kind,
            long CreationTime);

        private enum MutationRenameState
        {
            Prepared,
            ForwardApplied,
            DeleteApplied,
        }
    }
}
