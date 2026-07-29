// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;

namespace osu.Game.Skinning
{
    internal enum SkinManagedFolderMutationAuthorityRejectionReason
    {
        None,
        RecoveryPending,
        ExistingRecordMissing,
        ExistingRecordIneligible,
        ExistingRecordPathConflict,
        InvalidTargetNameSlot,
        TargetNameSlotOccupied,
        StagedSourceRejected,
        NativeAuthorityRejected,
    }

    internal sealed class SkinManagedFolderMutationNativeAuthorityException : Exception
    {
        public SkinManagedFolderMutationNativeAuthorityException()
            : base(nameof(SkinManagedFolderMutationNativeAuthorityException))
        {
        }

        public override string ToString() => nameof(SkinManagedFolderMutationNativeAuthorityException);
    }

    internal enum SkinManagedFolderRenameInspectionStatus
    {
        SourceOnly,
        TargetOnly,
        Both,
        Neither,
        IdentityMismatch,
    }

    /// <summary>
    /// Non-sensitive held-root inspection of the two direct-child slots named by a rename journal.
    /// </summary>
    internal readonly record struct SkinManagedFolderRenameInspection
    {
        public SkinManagedFolderRenameInspectionStatus Status { get; }

        public SkinManagedFolderRenameInspection(SkinManagedFolderRenameInspectionStatus status)
        {
            if (!Enum.IsDefined(status))
                throw new ArgumentOutOfRangeException(nameof(status));

            Status = status;
        }

        public override string ToString() => $"{nameof(SkinManagedFolderRenameInspection)}:{Status}";
    }

    internal enum SkinManagedFolderStagedImportInspectionStatus
    {
        SourceOnly,
        TargetOnly,
        Both,
        Neither,
        IdentityMismatch,
        RootIdentityMismatch,
    }

    /// <summary>
    /// Non-sensitive physical inspection of one fixed staged source and managed target slot.
    /// </summary>
    internal sealed class SkinManagedFolderStagedImportInspection
    {
        public SkinManagedFolderStagedImportInspectionStatus Status { get; }

        public SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }

        public SkinManagedFolderPhysicalIdentity? TargetIdentity { get; }

        public SkinManagedFolderPackageMetadata? PackageMetadata { get; }

        public string? TreeFingerprint { get; }

        public SkinManagedFolderStagedImportInspection(
            SkinManagedFolderStagedImportInspectionStatus status,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            SkinManagedFolderPhysicalIdentity? targetIdentity = null,
            SkinManagedFolderPackageMetadata? packageMetadata = null,
            string? treeFingerprint = null)
        {
            if (!Enum.IsDefined(status)
                || !managedRootIdentity.IsUsable
                || (treeFingerprint != null
                    && !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        treeFingerprint)))
            {
                throw new ArgumentException("The staged-import inspection is invalid.");
            }

            Status = status;
            ManagedRootIdentity = managedRootIdentity;
            TargetIdentity = targetIdentity;
            PackageMetadata = packageMetadata;
            TreeFingerprint = treeFingerprint;
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderStagedImportInspection)}:{Status}";
    }

    /// <summary>
    /// Exact, final target capture returned after an identity-preserving staged move.
    /// </summary>
    internal sealed class SkinManagedFolderStagedImportFilesystemResult : IDisposable
    {
        private SkinPackageRevisionCapsule? capsule;

        public SkinManagedFolderPhysicalIdentity TargetIdentity { get; }

        public string TreeFingerprint { get; }

        public SkinPackageRevisionCapsule Capsule
            => capsule ?? throw new ObjectDisposedException(nameof(SkinManagedFolderStagedImportFilesystemResult));

        public SkinManagedFolderStagedImportFilesystemResult(
            SkinManagedFolderPhysicalIdentity targetIdentity,
            string treeFingerprint,
            SkinPackageRevisionCapsule capsule)
        {
            if (!targetIdentity.IsUsable
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    treeFingerprint))
            {
                throw new ArgumentException("The staged-import target evidence is invalid.");
            }

            TargetIdentity = targetIdentity;
            TreeFingerprint = treeFingerprint;
            this.capsule = capsule ?? throw new ArgumentNullException(nameof(capsule));
        }

        public void Dispose()
        {
            SkinPackageRevisionCapsule? owned = Interlocked.Exchange(ref capsule, null);
            owned?.Dispose();
        }

        public override string ToString() => nameof(SkinManagedFolderStagedImportFilesystemResult);
    }

    internal interface ISkinManagedFolderMutationNativeAuthority
    {
        ISkinManagedFolderMutationNativeSession Open(CancellationToken cancellationToken);
    }

    internal interface ISkinManagedFolderMutationNativeSession : IDisposable
    {
        SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }

        SkinManagedFolderPhysicalIdentity CaptureExistingSource(
            string managedRelativePath,
            CancellationToken cancellationToken);

        SkinManagedFolderStagedSourceCapture CaptureStagedSource(
            Guid operationId,
            CancellationToken cancellationToken);

        SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
            string managedRelativePath,
            CancellationToken cancellationToken);

        SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
            SkinManagedFolderTargetNameSlot targetNameSlot,
            CancellationToken cancellationToken);

        SkinManagedFolderStagedImportFilesystemResult MoveCapturedStagedSourceToTarget(
            SkinManagedFolderTargetNameSlot targetNameSlot,
            string expectedContentRevision,
            string expectedTreeFingerprint,
            CancellationToken cancellationToken);

        SkinManagedFolderRenameInspection InspectRenameState(
            string sourceManagedRelativePath,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken);

        SkinManagedFolderStagedImportInspection InspectStagedImportState(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken);

        void CleanupExactStagedSource(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
            SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
            CancellationToken cancellationToken);

        void ValidateCompleteAndStable(CancellationToken cancellationToken);
    }

    internal sealed class SkinManagedFolderStagedSourceCapture : IDisposable
    {
        private SkinPackageRevisionCapsule? capsule;

        public SkinManagedFolderPhysicalIdentity StagedRootIdentity { get; }

        public SkinManagedFolderPhysicalIdentity SourceIdentity { get; }

        public string TreeFingerprint { get; }

        public SkinPackageRevisionCapsule Capsule
            => capsule ?? throw new ObjectDisposedException(nameof(SkinManagedFolderStagedSourceCapture));

        public SkinManagedFolderStagedSourceCapture(
            SkinManagedFolderPhysicalIdentity stagedRootIdentity,
            SkinManagedFolderPhysicalIdentity sourceIdentity,
            string treeFingerprint,
            SkinPackageRevisionCapsule capsule)
        {
            if (!SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    treeFingerprint))
            {
                throw new ArgumentException(
                    "The staged source tree fingerprint is invalid.",
                    nameof(treeFingerprint));
            }

            StagedRootIdentity = stagedRootIdentity;
            SourceIdentity = sourceIdentity;
            TreeFingerprint = treeFingerprint;
            this.capsule = capsule ?? throw new ArgumentNullException(nameof(capsule));
        }

        public bool IsUsableFor(SkinManagedFolderPhysicalIdentity managedRootIdentity)
            => StagedRootIdentity.IsUsable
               && SourceIdentity.IsUsable
               && capsule != null
               && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                   TreeFingerprint)
               && StagedRootIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber
               && SourceIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber;

        public void Dispose()
        {
            SkinPackageRevisionCapsule? owned = Interlocked.Exchange(ref capsule, null);
            owned?.Dispose();
        }

        public override string ToString() => nameof(SkinManagedFolderStagedSourceCapture);
    }

    internal sealed class SkinManagedFolderTargetNameSlot
    {
        public string ManagedRelativePath { get; }
        public SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }

        public SkinManagedFolderTargetNameSlot(
            string managedRelativePath,
            SkinManagedFolderPhysicalIdentity managedRootIdentity)
        {
            if (!SkinManagedFolderPath.TryNormalise(managedRelativePath, out string normalised)
                || !string.Equals(managedRelativePath, normalised, StringComparison.Ordinal)
                || !managedRootIdentity.IsUsable)
            {
                throw new ArgumentException("The managed target name slot is invalid.");
            }

            ManagedRelativePath = managedRelativePath;
            ManagedRootIdentity = managedRootIdentity;
        }

        public override string ToString() => nameof(SkinManagedFolderTargetNameSlot);
    }

    /// <summary>
    /// Held source evidence captured from the operation-derived staging slot by the native mutation session.
    /// </summary>
    /// <remarks>
    /// The evidence has no arbitrary path input. Its only valid logical slot is derived from its operation ID under the
    /// fixed OMS staging authority, while the native session retains the staging-root/source no-follow handles.
    /// </remarks>
    internal sealed class SkinManagedFolderStagedSourceAuthority
    {
        public Guid OperationId { get; }
        public string Authority => SkinManagedFolderMutationJournal.STAGED_SOURCE_AUTHORITY;
        public string RelativePath { get; }
        public SkinManagedFolderPhysicalIdentity PhysicalIdentity { get; }
        public SkinManagedFolderPhysicalIdentity StagedRootIdentity { get; }
        public string ContentRevision { get; }
        public string TreeFingerprint { get; }

        internal SkinManagedFolderStagedSourceAuthority(
            Guid operationId,
            SkinManagedFolderPhysicalIdentity physicalIdentity,
            SkinManagedFolderPhysicalIdentity stagedRootIdentity,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string contentRevision,
            string treeFingerprint)
        {
            if (operationId == Guid.Empty
                || !stagedRootIdentity.IsUsable
                || !physicalIdentity.IsUsable
                || stagedRootIdentity.VolumeSerialNumber != managedRootIdentity.VolumeSerialNumber
                || physicalIdentity.VolumeSerialNumber != managedRootIdentity.VolumeSerialNumber
                || !SkinManagedFolderMutationJournal.IsValidContentRevision(
                    contentRevision)
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    treeFingerprint))
            {
                throw new ArgumentException("The staged source authority is invalid.");
            }

            OperationId = operationId;
            RelativePath = SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(operationId);
            PhysicalIdentity = physicalIdentity;
            StagedRootIdentity = stagedRootIdentity;
            ContentRevision = contentRevision;
            TreeFingerprint = treeFingerprint;
        }

        public bool Validate(SkinManagedFolderPhysicalIdentity managedRootIdentity)
            => string.Equals(Authority, SkinManagedFolderMutationJournal.STAGED_SOURCE_AUTHORITY, StringComparison.Ordinal)
               && string.Equals(
                   RelativePath,
                    SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(OperationId),
                    StringComparison.Ordinal)
               && StagedRootIdentity.IsUsable
               && PhysicalIdentity.IsUsable
               && StagedRootIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber
               && PhysicalIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber
               && SkinManagedFolderMutationJournal.IsValidContentRevision(
                   ContentRevision)
               && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                   TreeFingerprint);

        public override string ToString() => nameof(SkinManagedFolderStagedSourceAuthority);
    }

    /// <summary>
    /// Immutable pre-publication reservation. It is not a Realm write capability.
    /// </summary>
    /// <remarks>
    /// The staged-import slice must later issue a one-shot publisher only after a durable FilesystemApplied journal and
    /// final native target verification. The ordinary scanner never consumes this plan directly.
    /// </remarks>
    internal sealed class SkinManagedFolderNewRecordPublicationPlan
    {
        public Guid OperationId { get; }
        public Guid PlannedRecordId { get; }
        public string TargetManagedRelativePath { get; }
        public SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }
        public string Version => SkinManagedFolderMutationJournal.NEW_RECORD_PUBLICATION_PLAN_VERSION;

        internal SkinManagedFolderNewRecordPublicationPlan(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity managedRootIdentity)
        {
            if (operationId == Guid.Empty
                || !SkinManagedFolderPath.TryNormalise(targetManagedRelativePath, out string normalised)
                || !string.Equals(targetManagedRelativePath, normalised, StringComparison.Ordinal)
                || !managedRootIdentity.IsUsable)
            {
                throw new ArgumentException("The managed-folder publication authority is invalid.");
            }

            OperationId = operationId;
            PlannedRecordId = operationId;
            TargetManagedRelativePath = targetManagedRelativePath;
            ManagedRootIdentity = managedRootIdentity;
        }

        public bool Validate(
            Guid operationId,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity managedRootIdentity)
            => OperationId == operationId
               && PlannedRecordId == operationId
               && string.Equals(TargetManagedRelativePath, targetManagedRelativePath, StringComparison.Ordinal)
               && ManagedRootIdentity == managedRootIdentity
               && string.Equals(Version, SkinManagedFolderMutationJournal.NEW_RECORD_PUBLICATION_PLAN_VERSION, StringComparison.Ordinal);

        public SkinManagedFolderNewRecordPublicationData CreatePublicationData(
            SkinManagedFolderPackageMetadata metadata)
            => new SkinManagedFolderNewRecordPublicationData(this, metadata);

        public override string ToString() => nameof(SkinManagedFolderNewRecordPublicationPlan);
    }

    internal sealed class SkinManagedFolderExistingRecordAuthority
    {
        public Guid RecordId { get; }
        public string ManagedRelativePath { get; }
        public SkinManagedFolderPhysicalIdentity PhysicalIdentity { get; }

        internal SkinManagedFolderExistingRecordAuthority(
            Guid recordId,
            string managedRelativePath,
            SkinManagedFolderPhysicalIdentity physicalIdentity)
        {
            RecordId = recordId;
            ManagedRelativePath = managedRelativePath;
            PhysicalIdentity = physicalIdentity;
        }

        public override string ToString() => nameof(SkinManagedFolderExistingRecordAuthority);
    }

    /// <summary>
    /// Held qualification result. It exposes identities and name slots but deliberately has no mutation method.
    /// </summary>
    internal sealed class SkinManagedFolderMutationAuthoritySession : IDisposable
    {
        private SkinManagedFolderOperationCoordinator.Lease? coordinatorLease;
        private ISkinManagedFolderMutationNativeSession? nativeSession;
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly ISkinManagedFolderMutationJournalStore journalStore;
        private readonly Func<CancellationToken, bool> validateLogicalAuthority;
        private readonly object sessionGate = new object();
        private SkinManagedFolderMutationJournal? durableJournal;
        private SkinManagedFolderNewRecordPublicationData? stagedImportPublicationData;
        private bool stagedImportPublisherAttempted;

        public Guid OperationId { get; }
        public SkinManagedFolderMutationKind Kind { get; }
        public SkinManagedFolderExistingRecordAuthority? ExistingRecord { get; }
        public SkinManagedFolderTargetNameSlot? TargetNameSlot { get; }
        public SkinManagedFolderStagedSourceAuthority? StagedSource { get; }
        public SkinManagedFolderNewRecordPublicationPlan? NewRecordPublicationPlan { get; }
        public SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }

        internal SkinManagedFolderMutationAuthoritySession(
            Guid operationId,
            SkinManagedFolderMutationKind kind,
            SkinManagedFolderExistingRecordAuthority? existingRecord,
            SkinManagedFolderTargetNameSlot? targetNameSlot,
            SkinManagedFolderStagedSourceAuthority? stagedSource,
            SkinManagedFolderNewRecordPublicationPlan? newRecordPublicationPlan,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationJournalStore journalStore,
            SkinManagedFolderOperationCoordinator.Lease coordinatorLease,
            ISkinManagedFolderMutationNativeSession nativeSession,
            Func<CancellationToken, bool> validateLogicalAuthority)
        {
            ArgumentNullException.ThrowIfNull(coordinatorLease);
            ArgumentNullException.ThrowIfNull(nativeSession);
            ArgumentNullException.ThrowIfNull(coordinator);
            ArgumentNullException.ThrowIfNull(journalStore);
            ArgumentNullException.ThrowIfNull(validateLogicalAuthority);

            OperationId = operationId;
            Kind = kind;
            ExistingRecord = existingRecord;
            TargetNameSlot = targetNameSlot;
            StagedSource = stagedSource;
            NewRecordPublicationPlan = newRecordPublicationPlan;
            ManagedRootIdentity = nativeSession.ManagedRootIdentity;
            this.coordinator = coordinator;
            this.journalStore = journalStore;
            this.coordinatorLease = coordinatorLease;
            this.nativeSession = nativeSession;
            this.validateLogicalAuthority = validateLogicalAuthority;

            if (operationId == Guid.Empty
                || !ManagedRootIdentity.IsUsable
                || !coordinatorLease.IsMutationReservationHeldBy(coordinator)
                || (existingRecord != null
                    && (!existingRecord.PhysicalIdentity.IsUsable
                        || existingRecord.PhysicalIdentity.VolumeSerialNumber != ManagedRootIdentity.VolumeSerialNumber))
                || (targetNameSlot != null && targetNameSlot.ManagedRootIdentity != ManagedRootIdentity)
                || (stagedSource != null && !stagedSource.Validate(ManagedRootIdentity))
                || (newRecordPublicationPlan != null
                    && (targetNameSlot == null
                        || !newRecordPublicationPlan.Validate(
                            operationId,
                            targetNameSlot.ManagedRelativePath,
                            ManagedRootIdentity))))
            {
                throw new ArgumentException("The managed-folder mutation authority session is invalid.");
            }
        }

        public bool Validate(CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                if (nativeSession == null
                    || coordinatorLease == null
                    || !coordinatorLease.IsMutationReservationHeldBy(coordinator))
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (StagedSource != null && !StagedSource.Validate(ManagedRootIdentity))
                    return false;

                try
                {
                    nativeSession.ValidateCompleteAndStable(cancellationToken);
                    return validateLogicalAuthority(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal bool HasCoordinatorAuthority(SkinManagedFolderOperationCoordinator coordinator)
            => coordinatorLease?.IsMutationReservationHeldBy(coordinator) == true;

        public SkinManagedFolderMutationJournal CreatePreparedJournal(CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                if (!Validate(cancellationToken))
                    throw new InvalidOperationException("The held managed-folder mutation authority is no longer valid.");

                return createPreparedJournal();
            }
        }

        public SkinManagedFolderDurableMutationReceipt PersistPreparedJournal(
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                if (!Validate(cancellationToken))
                    throw new InvalidOperationException("The held managed-folder mutation authority is no longer valid.");

                SkinManagedFolderMutationJournal journal = createPreparedJournal();

                try
                {
                    journalStore.Write(journal);
                    SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();

                    if (!loaded.IsLoaded || !loaded.Journal!.IsExactSameJournal(journal))
                        throw new InvalidOperationException("The prepared managed-folder mutation journal was not durable.");

                    durableJournal = journal;
                    return new SkinManagedFolderDurableMutationReceipt(this, journalStore, journal);
                }
                catch (OperationCanceledException)
                {
                    coordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());
                    throw;
                }
                catch
                {
                    // A failed durable write may already have reached the canonical slot. The recovery freeze is
                    // intentionally sticky until startup recovery proves the exact intent terminal.
                    coordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());
                    throw new SkinManagedFolderMutationJournalException();
                }
            }
        }

        private SkinManagedFolderMutationJournal createPreparedJournal()
            => Kind switch
            {
                SkinManagedFolderMutationKind.Rename
                    when ExistingRecord != null && TargetNameSlot != null =>
                    SkinManagedFolderMutationJournal.CreatePreparedRename(
                        OperationId,
                        ExistingRecord.RecordId,
                        ManagedRootIdentity,
                        ExistingRecord.ManagedRelativePath,
                        ExistingRecord.PhysicalIdentity,
                        TargetNameSlot.ManagedRelativePath),

                SkinManagedFolderMutationKind.Delete
                    when ExistingRecord != null =>
                    SkinManagedFolderMutationJournal.CreatePreparedDelete(
                        OperationId,
                        ExistingRecord.RecordId,
                        ManagedRootIdentity,
                        ExistingRecord.ManagedRelativePath,
                        ExistingRecord.PhysicalIdentity),

                SkinManagedFolderMutationKind.StagedImport
                    when TargetNameSlot != null
                         && StagedSource != null
                         && NewRecordPublicationPlan != null
                         && NewRecordPublicationPlan.Validate(
                             OperationId,
                             TargetNameSlot.ManagedRelativePath,
                             ManagedRootIdentity) =>
                    SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                        OperationId,
                        ManagedRootIdentity,
                        TargetNameSlot.ManagedRelativePath,
                        StagedSource.PhysicalIdentity,
                        StagedSource.StagedRootIdentity,
                        StagedSource.ContentRevision,
                        StagedSource.TreeFingerprint),

                _ => throw new InvalidOperationException("The held managed-folder mutation authority is incomplete."),
            };

        internal T RunWithDurableReceipt<T>(
            SkinManagedFolderDurableMutationReceipt receipt,
            T rejected,
            Func<T> action,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (sessionGate)
            {
                if (receipt == null
                    || !Validate(cancellationToken)
                    || !receipt.ValidateHeld(this, journalStore))
                {
                    return rejected;
                }

                T result = action();
                return receipt.ValidateHeld(this, journalStore) ? result : rejected;
            }
        }

        /// <summary>
        /// Applies the held rename only while the exact durable Prepared receipt remains authoritative, then durably
        /// records the identity-preserving filesystem result.
        /// </summary>
        internal SkinManagedFolderPhysicalIdentity ApplyCapturedRenameWithDurableReceipt(
            SkinManagedFolderDurableMutationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                if (Kind != SkinManagedFolderMutationKind.Rename
                    || ExistingRecord == null
                    || TargetNameSlot == null
                    || nativeSession == null
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.Prepared } prepared
                    || receipt == null
                    || !Validate(cancellationToken)
                    || !receipt.ValidateHeld(this, journalStore))
                {
                    throw new InvalidOperationException("The held managed-folder rename authority is no longer valid.");
                }

                SkinManagedFolderPhysicalIdentity targetIdentity;

                try
                {
                    // The native contract observes cancellation only before the physical move. Once the move becomes
                    // visible it completes final verification without the caller token and reports any later failure as
                    // an outcome-uncertain native rejection.
                    targetIdentity = nativeSession.RenameCapturedSourceToTarget(TargetNameSlot, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                    throw new SkinManagedFolderMutationJournalException();
                }

                try
                {
                    SkinManagedFolderMutationJournal filesystemApplied =
                        prepared.WithFilesystemApplied(targetIdentity);
                    writeAndConfirm(filesystemApplied);
                    durableJournal = filesystemApplied;
                    return targetIdentity;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                    throw new SkinManagedFolderMutationJournalException();
                }
            }
        }

        /// <summary>
        /// Moves the held operation-owned provisional package into its managed target, captures the exact final capsule,
        /// and durably records the scanner-equivalent publication fingerprint.
        /// </summary>
        internal SkinManagedFolderPhysicalIdentity ApplyCapturedStagedImportWithDurableReceipt(
            SkinManagedFolderDurableMutationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                if (Kind != SkinManagedFolderMutationKind.StagedImport
                    || TargetNameSlot == null
                    || StagedSource == null
                    || NewRecordPublicationPlan == null
                    || nativeSession == null
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.Prepared } prepared
                    || receipt == null
                    || !Validate(cancellationToken)
                    || !receipt.ValidateHeld(this, journalStore))
                {
                    throw new InvalidOperationException("The held staged-import authority is no longer valid.");
                }

                SkinManagedFolderStagedImportFilesystemResult? filesystemResult = null;

                try
                {
                    filesystemResult = nativeSession.MoveCapturedStagedSourceToTarget(
                        TargetNameSlot,
                        StagedSource.ContentRevision,
                        StagedSource.TreeFingerprint,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                    throw new SkinManagedFolderMutationJournalException();
                }

                using (filesystemResult)
                {
                    try
                    {
                        if (filesystemResult.TargetIdentity != StagedSource.PhysicalIdentity
                            || !string.Equals(
                                filesystemResult.TreeFingerprint,
                                StagedSource.TreeFingerprint,
                                StringComparison.Ordinal)
                            || !SkinManagedFolderPackageMetadataReader.TryRead(
                                filesystemResult.Capsule,
                                out SkinManagedFolderPackageMetadata? metadata)
                            || !string.Equals(
                                metadata!.ContentRevision,
                                StagedSource.ContentRevision,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("The final staged-import package changed.");
                        }

                        SkinManagedFolderNewRecordPublicationData publication =
                            NewRecordPublicationPlan.CreatePublicationData(metadata);
                        SkinManagedFolderMutationJournal filesystemApplied =
                            prepared.WithFilesystemApplied(
                                filesystemResult.TargetIdentity,
                                publication.Fingerprint);
                        writeAndConfirm(filesystemApplied);
                        durableJournal = filesystemApplied;
                        stagedImportPublicationData = publication;
                        return filesystemResult.TargetIdentity;
                    }
                    catch
                    {
                        coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                        throw new SkinManagedFolderMutationJournalException();
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the staged import's one-shot Realm publisher only from an exact durable FilesystemApplied state.
        /// </summary>
        internal bool TryPublishStagedImportRealm(
            Func<SkinManagedFolderNewRecordPublicationData, bool> publishRealm,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publishRealm);

            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind != SkinManagedFolderMutationKind.StagedImport
                    || stagedImportPublisherAttempted
                    || coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.FilesystemApplied } filesystemApplied
                    || stagedImportPublicationData is not { } publication
                    || !string.Equals(
                        filesystemApplied.NewRecordPublicationFingerprint,
                        publication.Fingerprint,
                        StringComparison.Ordinal)
                    || !isExactDurableJournal(filesystemApplied)
                    || !isCapturedStagedImportTargetStable(
                        filesystemApplied,
                        publication,
                        cancellationToken))
                {
                    return false;
                }

                stagedImportPublisherAttempted = true;

                try
                {
                    if (!publishRealm(publication))
                    {
                        coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                        return false;
                    }

                    if (!isCapturedStagedImportTargetStable(
                            filesystemApplied,
                            publication,
                            cancellationToken))
                    {
                        coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                        return false;
                    }

                    SkinManagedFolderMutationJournal realmApplied = filesystemApplied.WithRealmApplied();
                    writeAndConfirm(realmApplied);
                    durableJournal = realmApplied;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        internal bool TryCommitStagedImport(CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind != SkinManagedFolderMutationKind.StagedImport
                    || coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.RealmApplied } realmApplied
                    || stagedImportPublicationData is not { } publication
                    || !isExactDurableJournal(realmApplied)
                    || !isCapturedStagedImportTargetStable(
                        realmApplied,
                        publication,
                        cancellationToken))
                {
                    return false;
                }

                try
                {
                    SkinManagedFolderMutationJournal committed = realmApplied.WithCommitted();
                    writeAndConfirm(committed);
                    durableJournal = committed;
                    journalStore.Delete(committed);

                    if (journalStore.Load().Status != SkinManagedFolderMutationJournalLoadStatus.Missing)
                        throw new InvalidOperationException("The committed staged-import journal remains visible.");

                    durableJournal = null;
                    coordinator.UnfreezeRecoveryPaths(committed.GetAffectedManagedRelativePaths());
                    return true;
                }
                catch (OperationCanceledException)
                {
                    coordinator.FreezeRecoveryPaths(realmApplied.GetAffectedManagedRelativePaths());
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(realmApplied.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        /// <summary>
        /// Cleans only the exact operation-owned provisional source before terminally rolling back a Prepared import.
        /// </summary>
        internal bool TryRollbackPreparedStagedImport(
            SkinManagedFolderDurableMutationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind != SkinManagedFolderMutationKind.StagedImport
                    || TargetNameSlot == null
                    || StagedSource == null
                    || nativeSession == null
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.Prepared } prepared
                    || receipt == null
                    || !receipt.ValidateHeld(this, journalStore))
                {
                    return false;
                }

                try
                {
                    nativeSession.CleanupExactStagedSource(
                        OperationId,
                        TargetNameSlot.ManagedRelativePath,
                        StagedSource.StagedRootIdentity,
                        StagedSource.PhysicalIdentity,
                        cancellationToken);

                    SkinManagedFolderMutationJournal rolledBack = prepared.WithRolledBack();
                    writeAndConfirm(rolledBack);
                    durableJournal = rolledBack;
                    journalStore.Delete(rolledBack);

                    if (journalStore.Load().Status != SkinManagedFolderMutationJournalLoadStatus.Missing)
                        throw new InvalidOperationException("The rolled-back staged-import journal remains visible.");

                    durableJournal = null;
                    coordinator.UnfreezeRecoveryPaths(rolledBack.GetAffectedManagedRelativePaths());
                    return true;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        /// <summary>
        /// Runs the rename's authoritative Realm path update from an exact durable FilesystemApplied state and records
        /// the completed Realm phase. The action must update only the already-authoritative record.
        /// </summary>
        internal bool TryApplyRenameRealm(
            Func<bool> applyRealm,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(applyRealm);

            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind != SkinManagedFolderMutationKind.Rename
                    || coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.FilesystemApplied } filesystemApplied
                    || !isExactDurableJournal(filesystemApplied)
                    || !isCapturedRenameTargetStable(filesystemApplied, cancellationToken))
                {
                    return false;
                }

                try
                {
                    if (!applyRealm())
                    {
                        coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                        return false;
                    }

                    if (!isCapturedRenameTargetStable(filesystemApplied, cancellationToken))
                    {
                        coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                        return false;
                    }

                    SkinManagedFolderMutationJournal realmApplied = filesystemApplied.WithRealmApplied();
                    writeAndConfirm(realmApplied);
                    durableJournal = realmApplied;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(filesystemApplied.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        /// <summary>
        /// Durably commits and compare-deletes a completed rename intent. The held intent is cleared only after the
        /// canonical journal slot is proven missing.
        /// </summary>
        internal bool TryCommitRename(CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind != SkinManagedFolderMutationKind.Rename
                    || coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true
                    || durableJournal is not { Phase: SkinManagedFolderMutationPhase.RealmApplied } realmApplied
                    || !isExactDurableJournal(realmApplied)
                    || !isCapturedRenameTargetStable(realmApplied, cancellationToken))
                {
                    return false;
                }

                try
                {
                    SkinManagedFolderMutationJournal committed = realmApplied.WithCommitted();
                    writeAndConfirm(committed);
                    durableJournal = committed;
                    journalStore.Delete(committed);

                    if (journalStore.Load().Status != SkinManagedFolderMutationJournalLoadStatus.Missing)
                        throw new InvalidOperationException("The committed managed-folder rename journal remains visible.");

                    durableJournal = null;
                    coordinator.UnfreezeRecoveryPaths(committed.GetAffectedManagedRelativePaths());
                    return true;
                }
                catch (OperationCanceledException)
                {
                    coordinator.FreezeRecoveryPaths(realmApplied.GetAffectedManagedRelativePaths());
                    throw;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(realmApplied.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        private bool isCapturedRenameTargetStable(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (nativeSession == null
                || journal.SourceManagedRelativePath == null
                || journal.TargetManagedRelativePath == null
                || journal.SourceIdentity == null)
            {
                return false;
            }

            try
            {
                SkinManagedFolderRenameInspection inspection = nativeSession.InspectRenameState(
                    journal.SourceManagedRelativePath,
                    journal.TargetManagedRelativePath,
                    journal.SourceIdentity.Value,
                    cancellationToken);
                return inspection.Status == SkinManagedFolderRenameInspectionStatus.TargetOnly;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private bool isCapturedStagedImportTargetStable(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderNewRecordPublicationData publication,
            CancellationToken cancellationToken)
        {
            if (nativeSession == null
                || journal.TargetManagedRelativePath == null
                || journal.StagedRootIdentity == null
                || journal.StagedSourceIdentity == null
                || journal.TargetIdentity == null)
            {
                return false;
            }

            try
            {
                SkinManagedFolderStagedImportInspection inspection = nativeSession.InspectStagedImportState(
                    journal.OperationId,
                    journal.TargetManagedRelativePath,
                    journal.StagedRootIdentity.Value,
                    journal.StagedSourceIdentity.Value,
                    cancellationToken);

                if (inspection.Status != SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                    || inspection.ManagedRootIdentity != journal.ManagedRootIdentity
                    || inspection.TargetIdentity != journal.StagedSourceIdentity
                    || inspection.TargetIdentity != journal.TargetIdentity
                    || inspection.PackageMetadata == null
                    || !string.Equals(
                        inspection.TreeFingerprint,
                        journal.StagedSourceTreeFingerprint,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                SkinManagedFolderNewRecordPublicationData recaptured =
                    NewRecordPublicationPlan!.CreatePublicationData(inspection.PackageMetadata);
                return string.Equals(recaptured.Fingerprint, publication.Fingerprint, StringComparison.Ordinal)
                       && string.Equals(
                           journal.NewRecordPublicationFingerprint,
                           publication.Fingerprint,
                           StringComparison.Ordinal);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private bool isExactDurableJournal(SkinManagedFolderMutationJournal expected)
        {
            try
            {
                SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();
                return loaded.IsLoaded && loaded.Journal!.IsExactSameJournal(expected);
            }
            catch
            {
                return false;
            }
        }

        private void writeAndConfirm(SkinManagedFolderMutationJournal journal)
        {
            journalStore.Write(journal);
            SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();

            if (!loaded.IsLoaded || !loaded.Journal!.IsExactSameJournal(journal))
                throw new SkinManagedFolderMutationJournalException();
        }

        internal bool TryAbortPreparedJournal(
            SkinManagedFolderDurableMutationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Kind == SkinManagedFolderMutationKind.StagedImport
                    || durableJournal == null
                    || durableJournal.Phase != SkinManagedFolderMutationPhase.Prepared
                    || receipt == null
                    || !receipt.ValidateHeld(this, journalStore))
                {
                    return false;
                }

                SkinManagedFolderMutationJournal prepared = durableJournal;
                SkinManagedFolderMutationJournal rolledBack = prepared.WithRolledBack();

                try
                {
                    journalStore.Write(rolledBack);
                    journalStore.Delete(rolledBack);

                    if (journalStore.Load().Status != SkinManagedFolderMutationJournalLoadStatus.Missing)
                        throw new InvalidOperationException("The rolled-back managed-folder mutation journal remains visible.");

                    durableJournal = null;
                    coordinator.UnfreezeRecoveryPaths(rolledBack.GetAffectedManagedRelativePaths());
                    return true;
                }
                catch
                {
                    coordinator.FreezeRecoveryPaths(prepared.GetAffectedManagedRelativePaths());
                    return false;
                }
            }
        }

        public void Dispose()
        {
            lock (sessionGate)
            {
                try
                {
                    nativeSession?.Dispose();
                }
                finally
                {
                    if (durableJournal != null)
                        coordinator.FreezeRecoveryPaths(durableJournal.GetAffectedManagedRelativePaths());

                    nativeSession = null;
                    coordinatorLease?.Dispose();
                    coordinatorLease = null;
                }
            }
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderMutationAuthoritySession)}:{Kind}";
    }

    internal sealed class SkinManagedFolderDurableMutationReceipt
    {
        private readonly SkinManagedFolderMutationAuthoritySession session;
        private readonly ISkinManagedFolderMutationJournalStore journalStore;
        private readonly SkinManagedFolderMutationJournal journal;

        internal SkinManagedFolderDurableMutationReceipt(
            SkinManagedFolderMutationAuthoritySession session,
            ISkinManagedFolderMutationJournalStore journalStore,
            SkinManagedFolderMutationJournal journal)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        }

        internal bool ValidateHeld(
            SkinManagedFolderMutationAuthoritySession candidateSession,
            ISkinManagedFolderMutationJournalStore candidateStore)
        {
            if (!ReferenceEquals(session, candidateSession)
                || !ReferenceEquals(journalStore, candidateStore))
            {
                return false;
            }

            try
            {
                SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();
                return loaded.IsLoaded && loaded.Journal!.IsExactSameJournal(journal);
            }
            catch
            {
                return false;
            }
        }

        public override string ToString() => nameof(SkinManagedFolderDurableMutationReceipt);
    }

    internal sealed class SkinManagedFolderMutationAuthorityResult
    {
        public SkinManagedFolderMutationAuthorityRejectionReason RejectionReason { get; }
        public SkinManagedFolderMutationAuthoritySession? Session { get; }
        public bool IsSuccess => RejectionReason == SkinManagedFolderMutationAuthorityRejectionReason.None && Session != null;

        private SkinManagedFolderMutationAuthorityResult(
            SkinManagedFolderMutationAuthorityRejectionReason rejectionReason,
            SkinManagedFolderMutationAuthoritySession? session)
        {
            RejectionReason = rejectionReason;
            Session = session;
        }

        public static SkinManagedFolderMutationAuthorityResult Success(SkinManagedFolderMutationAuthoritySession session)
            => new SkinManagedFolderMutationAuthorityResult(
                SkinManagedFolderMutationAuthorityRejectionReason.None,
                session ?? throw new ArgumentNullException(nameof(session)));

        public static SkinManagedFolderMutationAuthorityResult Reject(
            SkinManagedFolderMutationAuthorityRejectionReason rejectionReason)
        {
            if (!Enum.IsDefined(rejectionReason)
                || rejectionReason == SkinManagedFolderMutationAuthorityRejectionReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectionReason));
            }

            return new SkinManagedFolderMutationAuthorityResult(rejectionReason, null);
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderMutationAuthorityResult)}:{RejectionReason}";
    }

    /// <summary>
    /// Qualifies future managed mutations against Realm and a held native root without performing filesystem writes.
    /// </summary>
    internal sealed class SkinManagedFolderMutationAuthority
    {
        private readonly RealmAccess realm;
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly ISkinManagedFolderMutationNativeAuthority nativeAuthority;
        private readonly ISkinManagedFolderMutationJournalStore journalStore;

        public SkinManagedFolderMutationAuthority(
            RealmAccess realm,
            Storage storage,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationNativeAuthority nativeAuthority,
            ISkinManagedFolderMutationJournalStore journalStore)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            ArgumentNullException.ThrowIfNull(storage);
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.nativeAuthority = nativeAuthority ?? throw new ArgumentNullException(nameof(nativeAuthority));
            this.journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        }

        public SkinManagedFolderMutationAuthorityResult OpenRename(
            Guid operationId,
            Guid recordId,
            string targetChildName,
            CancellationToken cancellationToken = default)
            => openExisting(
                operationId,
                recordId,
                SkinManagedFolderMutationKind.Rename,
                targetChildName,
                cancellationToken);

        public SkinManagedFolderMutationAuthorityResult OpenDelete(
            Guid operationId,
            Guid recordId,
            CancellationToken cancellationToken = default)
            => openExisting(
                operationId,
                recordId,
                SkinManagedFolderMutationKind.Delete,
                null,
                cancellationToken);

        public SkinManagedFolderMutationAuthorityResult OpenStagedImport(
            Guid operationId,
            string targetChildName,
            CancellationToken cancellationToken = default)
        {
            if (operationId == Guid.Empty
                || SkinFilesystemStorageResolver.IsFixedSkinId(operationId))
            {
                return SkinManagedFolderMutationAuthorityResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.StagedSourceRejected);
            }

            if (!SkinManagedFolderPath.TryCreateFromChildName(targetChildName, out string targetPath))
            {
                return SkinManagedFolderMutationAuthorityResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.InvalidTargetNameSlot);
            }

            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease = null;
            ISkinManagedFolderMutationNativeSession? nativeSession = null;

            try
            {
                coordinatorLease = coordinator.EnterMutation(cancellationToken);

                if (coordinator.IsMutationBlocked)
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.RecoveryPending,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                if (hasUnresolvedExternalFilesystemDeclaration())
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                if (hasRealmPathConflict(targetPath) || hasRealmRecordIdConflict(operationId))
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                nativeSession = nativeAuthority.Open(cancellationToken);
                SkinManagedFolderTargetNameSlot target = nativeSession.CaptureAbsentTargetNameSlot(targetPath, cancellationToken);
                SkinManagedFolderStagedSourceAuthority stagedSource;

                using (SkinManagedFolderStagedSourceCapture stagedCapture =
                       nativeSession.CaptureStagedSource(operationId, cancellationToken))
                {
                    if (!stagedCapture.IsUsableFor(nativeSession.ManagedRootIdentity)
                        || !SkinManagedFolderPackageMetadataReader.TryRead(
                            stagedCapture.Capsule,
                            out SkinManagedFolderPackageMetadata? stagedMetadata)
                        || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(
                            SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO))
                    {
                        return rejectAndRelease(
                            SkinManagedFolderMutationAuthorityRejectionReason.StagedSourceRejected,
                            ref coordinatorLease,
                            ref nativeSession);
                    }

                    stagedSource = new SkinManagedFolderStagedSourceAuthority(
                        operationId,
                        stagedCapture.SourceIdentity,
                        stagedCapture.StagedRootIdentity,
                        nativeSession.ManagedRootIdentity,
                        stagedMetadata!.ContentRevision,
                        stagedCapture.TreeFingerprint);
                }

                var publicationPlan = new SkinManagedFolderNewRecordPublicationPlan(
                    operationId,
                    target.ManagedRelativePath,
                    nativeSession.ManagedRootIdentity);
                nativeSession.ValidateCompleteAndStable(cancellationToken);

                if (hasRealmPathConflict(targetPath)
                    || hasRealmRecordIdConflict(operationId)
                    || hasUnresolvedExternalFilesystemDeclaration()
                    || !stagedSource.Validate(nativeSession.ManagedRootIdentity)
                    || target.ManagedRootIdentity != nativeSession.ManagedRootIdentity)
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                var session = new SkinManagedFolderMutationAuthoritySession(
                    operationId,
                    SkinManagedFolderMutationKind.StagedImport,
                    null,
                    target,
                    stagedSource,
                    publicationPlan,
                    coordinator,
                    journalStore,
                    coordinatorLease,
                    nativeSession,
                    token => !coordinator.IsMutationBlocked
                             && !hasUnresolvedExternalFilesystemDeclaration()
                             && !hasRealmPathConflict(targetPath)
                             && !hasRealmRecordIdConflict(operationId)
                             && stagedSource.Validate(target.ManagedRootIdentity));
                coordinatorLease = null;
                nativeSession = null;
                return SkinManagedFolderMutationAuthorityResult.Success(session);
            }
            catch (OperationCanceledException)
            {
                release(ref coordinatorLease, ref nativeSession);
                throw;
            }
            catch
            {
                return rejectAndRelease(
                    SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected,
                    ref coordinatorLease,
                    ref nativeSession);
            }
        }

        private SkinManagedFolderMutationAuthorityResult openExisting(
            Guid operationId,
            Guid recordId,
            SkinManagedFolderMutationKind kind,
            string? targetChildName,
            CancellationToken cancellationToken)
        {
            if (operationId == Guid.Empty || recordId == Guid.Empty)
            {
                return SkinManagedFolderMutationAuthorityResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordIneligible);
            }

            string? targetPath = null;

            if (kind == SkinManagedFolderMutationKind.Rename
                && !SkinManagedFolderPath.TryCreateFromChildName(targetChildName, out targetPath))
            {
                return SkinManagedFolderMutationAuthorityResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.InvalidTargetNameSlot);
            }

            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease = null;
            ISkinManagedFolderMutationNativeSession? nativeSession = null;

            try
            {
                coordinatorLease = coordinator.EnterMutation(cancellationToken);

                if (coordinator.IsMutationBlocked)
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.RecoveryPending,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                if (hasUnresolvedExternalFilesystemDeclaration())
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordPathConflict,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                ExistingRecordQualification? qualification = qualifyExistingRecord(recordId);

                if (qualification == null)
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordMissing,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                if (!qualification.IsEligible)
                {
                    return rejectAndRelease(
                        qualification.HasPathConflict
                            ? SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordPathConflict
                            : SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordIneligible,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                if (targetPath != null
                    && (string.Equals(qualification.ManagedRelativePath, targetPath, StringComparison.OrdinalIgnoreCase)
                        || hasRealmPathConflict(targetPath)))
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                nativeSession = nativeAuthority.Open(cancellationToken);
                SkinManagedFolderPhysicalIdentity sourceIdentity = nativeSession.CaptureExistingSource(
                    qualification.ManagedRelativePath!,
                    cancellationToken);
                SkinManagedFolderTargetNameSlot? target = targetPath == null
                    ? null
                    : nativeSession.CaptureAbsentTargetNameSlot(targetPath, cancellationToken);
                nativeSession.ValidateCompleteAndStable(cancellationToken);

                ExistingRecordQualification? finalQualification = qualifyExistingRecord(recordId);

                if (finalQualification == null
                    || !finalQualification.IsEligible
                    || !qualification.Matches(finalQualification)
                    || (targetPath != null && hasRealmPathConflict(targetPath))
                    || (target != null && target.ManagedRootIdentity != nativeSession.ManagedRootIdentity))
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordIneligible,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                var existing = new SkinManagedFolderExistingRecordAuthority(
                    recordId,
                    qualification.ManagedRelativePath!,
                    sourceIdentity);
                var session = new SkinManagedFolderMutationAuthoritySession(
                    operationId,
                    kind,
                    existing,
                    target,
                    null,
                    null,
                    coordinator,
                    journalStore,
                    coordinatorLease,
                    nativeSession,
                    token =>
                    {
                        token.ThrowIfCancellationRequested();

                        if (coordinator.IsMutationBlocked || hasUnresolvedExternalFilesystemDeclaration())
                            return false;

                        ExistingRecordQualification? current = qualifyExistingRecord(recordId);
                        return current != null
                               && current.IsEligible
                               && qualification.Matches(current)
                               && (targetPath == null || !hasRealmPathConflict(targetPath));
                    });
                coordinatorLease = null;
                nativeSession = null;
                return SkinManagedFolderMutationAuthorityResult.Success(session);
            }
            catch (OperationCanceledException)
            {
                release(ref coordinatorLease, ref nativeSession);
                throw;
            }
            catch
            {
                return rejectAndRelease(
                    SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected,
                    ref coordinatorLease,
                    ref nativeSession);
            }
        }

        private ExistingRecordQualification? qualifyExistingRecord(Guid recordId)
            => realm.Run(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(recordId);

                if (record == null)
                    return null;

                bool pathValid = SkinManagedFolderPath.TryNormalise(record.FilesystemStoragePath, out string normalisedPath)
                                 && string.Equals(record.FilesystemStoragePath, normalisedPath, StringComparison.Ordinal);
                int matchingPaths = pathValid
                    ? r.All<SkinInfo>()
                       .AsEnumerable()
                       .Count(candidate => SkinManagedFolderPath.TryNormalise(candidate.FilesystemStoragePath, out string candidatePath)
                                           && string.Equals(candidatePath, normalisedPath, StringComparison.OrdinalIgnoreCase))
                    : 0;
                bool eligible = record.IsManaged
                                && pathValid
                                && matchingPaths == 1
                                && record.Files.Count == 0
                                && !record.IsExternalFilesystemStorage
                                && !record.Protected
                                && !record.DeletePending
                                && !SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
                                && string.Equals(record.FilesystemStorageAuthorityOwner, SkinManagedFolderScanner.AUTHORITY_OWNER, StringComparison.Ordinal)
                                && SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
                                && !string.IsNullOrEmpty(record.Hash);

                return new ExistingRecordQualification(
                    record.ID,
                    pathValid ? normalisedPath : null,
                    record.Name,
                    record.Creator,
                    record.InstantiationInfo,
                    record.Hash,
                    record.DeletePending,
                    eligible,
                    pathValid && matchingPaths != 1);
            });

        private bool hasRealmPathConflict(string managedRelativePath)
            => realm.Run(r =>
            {
                r.Refresh();
                return r.All<SkinInfo>()
                        .AsEnumerable()
                        .Any(candidate => SkinManagedFolderPath.TryNormalise(candidate.FilesystemStoragePath, out string candidatePath)
                                          && string.Equals(candidatePath, managedRelativePath, StringComparison.OrdinalIgnoreCase));
            });

        private bool hasRealmRecordIdConflict(Guid recordId)
            => realm.Run(r =>
            {
                r.Refresh();
                return r.Find<SkinInfo>(recordId) != null;
            });

        private bool hasUnresolvedExternalFilesystemDeclaration()
            => realm.Run(r =>
            {
                r.Refresh();
                return r.All<SkinInfo>().AsEnumerable().Any(candidate => candidate.IsExternalFilesystemStorage);
            });

        private static SkinManagedFolderMutationAuthorityResult rejectAndRelease(
            SkinManagedFolderMutationAuthorityRejectionReason reason,
            ref SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            ref ISkinManagedFolderMutationNativeSession? nativeSession)
        {
            release(ref coordinatorLease, ref nativeSession);
            return SkinManagedFolderMutationAuthorityResult.Reject(reason);
        }

        private static void release(
            ref SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            ref ISkinManagedFolderMutationNativeSession? nativeSession)
        {
            try
            {
                try
                {
                    nativeSession?.Dispose();
                }
                catch
                {
                }
            }
            finally
            {
                nativeSession = null;
                coordinatorLease?.Dispose();
                coordinatorLease = null;
            }
        }

        private sealed class ExistingRecordQualification
        {
            public Guid RecordId { get; }
            public string? ManagedRelativePath { get; }
            public string Name { get; }
            public string Creator { get; }
            public string InstantiationInfo { get; }
            public string Hash { get; }
            public bool DeletePending { get; }
            public bool IsEligible { get; }
            public bool HasPathConflict { get; }

            public ExistingRecordQualification(
                Guid recordId,
                string? managedRelativePath,
                string name,
                string creator,
                string instantiationInfo,
                string hash,
                bool deletePending,
                bool isEligible,
                bool hasPathConflict)
            {
                RecordId = recordId;
                ManagedRelativePath = managedRelativePath;
                Name = name;
                Creator = creator;
                InstantiationInfo = instantiationInfo;
                Hash = hash;
                DeletePending = deletePending;
                IsEligible = isEligible;
                HasPathConflict = hasPathConflict;
            }

            public bool Matches(ExistingRecordQualification other)
                => RecordId == other.RecordId
                   && string.Equals(ManagedRelativePath, other.ManagedRelativePath, StringComparison.Ordinal)
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && string.Equals(Creator, other.Creator, StringComparison.Ordinal)
                   && string.Equals(InstantiationInfo, other.InstantiationInfo, StringComparison.Ordinal)
                   && string.Equals(Hash, other.Hash, StringComparison.Ordinal)
                   && DeletePending == other.DeletePending
                   && IsEligible == other.IsEligible
                   && HasPathConflict == other.HasPathConflict;
        }
    }
}
