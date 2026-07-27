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

        void ValidateCompleteAndStable(CancellationToken cancellationToken);
    }

    internal readonly record struct SkinManagedFolderStagedSourceCapture(
        SkinManagedFolderPhysicalIdentity StagedRootIdentity,
        SkinManagedFolderPhysicalIdentity SourceIdentity)
    {
        public bool IsUsableFor(SkinManagedFolderPhysicalIdentity managedRootIdentity)
            => StagedRootIdentity.IsUsable
               && SourceIdentity.IsUsable
               && StagedRootIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber
               && SourceIdentity.VolumeSerialNumber == managedRootIdentity.VolumeSerialNumber;

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

        internal SkinManagedFolderStagedSourceAuthority(
            Guid operationId,
            SkinManagedFolderPhysicalIdentity physicalIdentity,
            SkinManagedFolderPhysicalIdentity stagedRootIdentity,
            SkinManagedFolderPhysicalIdentity managedRootIdentity)
        {
            if (operationId == Guid.Empty
                || !new SkinManagedFolderStagedSourceCapture(stagedRootIdentity, physicalIdentity).IsUsableFor(managedRootIdentity))
            {
                throw new ArgumentException("The staged source authority is invalid.");
            }

            OperationId = operationId;
            RelativePath = SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(operationId);
            PhysicalIdentity = physicalIdentity;
            StagedRootIdentity = stagedRootIdentity;
        }

        public bool Validate(SkinManagedFolderPhysicalIdentity managedRootIdentity)
            => string.Equals(Authority, SkinManagedFolderMutationJournal.STAGED_SOURCE_AUTHORITY, StringComparison.Ordinal)
               && string.Equals(
                   RelativePath,
                    SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(OperationId),
                    StringComparison.Ordinal)
               && new SkinManagedFolderStagedSourceCapture(StagedRootIdentity, PhysicalIdentity).IsUsableFor(managedRootIdentity);

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
                        StagedSource.StagedRootIdentity),

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

        internal bool TryAbortPreparedJournal(
            SkinManagedFolderDurableMutationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            lock (sessionGate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (durableJournal == null
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
                SkinManagedFolderStagedSourceCapture stagedCapture = nativeSession.CaptureStagedSource(operationId, cancellationToken);

                if (!stagedCapture.IsUsableFor(nativeSession.ManagedRootIdentity))
                {
                    return rejectAndRelease(
                        SkinManagedFolderMutationAuthorityRejectionReason.StagedSourceRejected,
                        ref coordinatorLease,
                        ref nativeSession);
                }

                var stagedSource = new SkinManagedFolderStagedSourceAuthority(
                    operationId,
                    stagedCapture.SourceIdentity,
                    stagedCapture.StagedRootIdentity,
                    nativeSession.ManagedRootIdentity);
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
