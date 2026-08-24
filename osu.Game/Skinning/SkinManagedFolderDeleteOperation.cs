// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Database;
using Realms;

namespace osu.Game.Skinning
{
    internal enum SkinManagedFolderDeleteOperationStatus
    {
        Succeeded,
        AuthorityRejected,
        Busy,
        Shutdown,
        WrongThread,
        Cancelled,
        FallbackRejected,
        PreparedJournalOutcomeUncertain,
        FilesystemOutcomeUncertain,
        PhysicalDeleteOutcomeUncertain,
        RealmOutcomeUncertain,
        CommitOutcomeUncertain,
    }

    /// <summary>
    /// Non-sensitive result of one managed-folder delete request.
    /// </summary>
    internal sealed class SkinManagedFolderDeleteOperationResult
    {
        public SkinManagedFolderDeleteOperationStatus Status { get; }

        public SkinManagedFolderMutationAuthorityRejectionReason AuthorityRejectionReason { get; }

        public SkinManagedFolderProtectedFallbackCommitResult FallbackCommitResult { get; }

        public bool IsSuccess => Status == SkinManagedFolderDeleteOperationStatus.Succeeded;

        private SkinManagedFolderDeleteOperationResult(
            SkinManagedFolderDeleteOperationStatus status,
            SkinManagedFolderMutationAuthorityRejectionReason authorityRejectionReason =
                SkinManagedFolderMutationAuthorityRejectionReason.None,
            SkinManagedFolderProtectedFallbackCommitResult fallbackCommitResult =
                SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected)
        {
            Status = status;
            AuthorityRejectionReason = authorityRejectionReason;
            FallbackCommitResult = fallbackCommitResult;
        }

        public static SkinManagedFolderDeleteOperationResult Success(
            SkinManagedFolderProtectedFallbackCommitResult fallbackCommitResult)
            => new SkinManagedFolderDeleteOperationResult(
                SkinManagedFolderDeleteOperationStatus.Succeeded,
                fallbackCommitResult: fallbackCommitResult);

        public static SkinManagedFolderDeleteOperationResult Reject(
            SkinManagedFolderMutationAuthorityRejectionReason reason)
            => new SkinManagedFolderDeleteOperationResult(
                SkinManagedFolderDeleteOperationStatus.AuthorityRejected,
                reason);

        public static SkinManagedFolderDeleteOperationResult FallbackRejected(
            SkinManagedFolderProtectedFallbackCommitResult result)
            => new SkinManagedFolderDeleteOperationResult(
                SkinManagedFolderDeleteOperationStatus.FallbackRejected,
                fallbackCommitResult: result);

        public static SkinManagedFolderDeleteOperationResult Failure(
            SkinManagedFolderDeleteOperationStatus status)
        {
            if (status is SkinManagedFolderDeleteOperationStatus.Succeeded
                or SkinManagedFolderDeleteOperationStatus.AuthorityRejected
                or SkinManagedFolderDeleteOperationStatus.FallbackRejected)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new SkinManagedFolderDeleteOperationResult(status);
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderDeleteOperationResult)}:{Status}";
    }

    internal delegate SkinManagedFolderProtectedFallbackCommitResult SkinManagedFolderDeleteFallbackCommit(
        SkinManagedFolderMutationAuthoritySession authority,
        SkinManagedFolderDurableMutationReceipt durableReceipt,
        CancellationToken cancellationToken);

    /// <summary>
    /// A delete source qualified by one still-held C1 logical/native authority. No journal or filesystem mutation has
    /// occurred while this object is merely prepared.
    /// </summary>
    internal sealed class SkinManagedFolderPreparedDelete : IDisposable
    {
        private readonly SkinManagedFolderDeleteOperation owner;
        private SkinManagedFolderMutationAuthoritySession? authority;
        private int executionClaimed;

        public SkinManagedFolderDeleteOperationResult? FailureResult { get; }

        public string? SourceContentRevision { get; }

        public bool IsSuccess => authority != null && FailureResult == null;

        internal SkinManagedFolderPreparedDelete(
            SkinManagedFolderDeleteOperation owner,
            SkinManagedFolderMutationAuthoritySession authority,
            string? sourceContentRevision)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            SourceContentRevision = sourceContentRevision;
        }

        internal SkinManagedFolderPreparedDelete(
            SkinManagedFolderDeleteOperation owner,
            SkinManagedFolderDeleteOperationResult failureResult)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            FailureResult = failureResult ?? throw new ArgumentNullException(nameof(failureResult));
        }

        /// <summary>
        /// Performs the potentially failing held-authority validation outside the update-thread publication commit.
        /// </summary>
        internal bool ValidateHeldSource(CancellationToken cancellationToken = default)
        {
            SkinManagedFolderMutationAuthoritySession? held = Volatile.Read(ref authority);

            try
            {
                return held != null && held.Validate(cancellationToken);
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

        /// <summary>
        /// No-I/O commit receipt check. The exact source tree is still protected by the authority retained here.
        /// </summary>
        internal bool HoldsContentRevision(string contentRevision)
            => Volatile.Read(ref authority) != null
               && !string.IsNullOrEmpty(SourceContentRevision)
               && string.Equals(SourceContentRevision, contentRevision, StringComparison.Ordinal);

        internal bool TryClaimExecution(
            SkinManagedFolderDeleteOperation candidateOwner,
            out SkinManagedFolderMutationAuthoritySession session)
        {
            session = null!;

            if (!ReferenceEquals(owner, candidateOwner)
                || Interlocked.CompareExchange(ref executionClaimed, 1, 0) != 0)
            {
                return false;
            }

            session = Volatile.Read(ref authority)!;
            return session != null;
        }

        public void Dispose()
            => Interlocked.Exchange(ref authority, null)?.Dispose();
    }

    /// <summary>
    /// Executes one managed-folder delete through an operation-derived tombstone, exact no-follow cleanup and Realm
    /// convergence. The fallback callback must publish a coherent protected pair before the first physical move.
    /// </summary>
    internal sealed class SkinManagedFolderDeleteOperation
    {
        private readonly RealmAccess realm;
        private readonly SkinManagedFolderMutationAuthority authority;
        private readonly SkinManagedFolderDeleteFallbackCommit commitFallback;

        public SkinManagedFolderDeleteOperation(
            RealmAccess realm,
            SkinManagedFolderMutationAuthority authority,
            SkinManagedFolderDeleteFallbackCommit commitFallback)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.commitFallback = commitFallback ?? throw new ArgumentNullException(nameof(commitFallback));
        }

        public SkinManagedFolderDeleteOperationResult Execute(
            Guid operationId,
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            using SkinManagedFolderPreparedDelete prepared = Prepare(
                operationId,
                recordId,
                captureSourceRevision: false,
                cancellationToken);

            return prepared.IsSuccess
                ? ExecutePrepared(prepared, cancellationToken)
                : prepared.FailureResult!;
        }

        /// <summary>
        /// Opens and holds the exact C1 delete authority. Optional source revision capture is used by C2 current
        /// deletion to prove the live owner before any participant publication or durable intent.
        /// </summary>
        internal SkinManagedFolderPreparedDelete Prepare(
            Guid operationId,
            Guid recordId,
            bool captureSourceRevision,
            CancellationToken cancellationToken = default)
        {
            SkinManagedFolderMutationAuthorityResult opened;

            try
            {
                opened = authority.OpenDelete(operationId, recordId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new SkinManagedFolderPreparedDelete(
                    this,
                    SkinManagedFolderDeleteOperationResult.Failure(
                        SkinManagedFolderDeleteOperationStatus.Cancelled));
            }

            if (!opened.IsSuccess)
            {
                return new SkinManagedFolderPreparedDelete(
                    this,
                    SkinManagedFolderDeleteOperationResult.Reject(opened.RejectionReason));
            }

            SkinManagedFolderMutationAuthoritySession session = opened.Session!;
            string? sourceContentRevision = null;

            try
            {
                if (captureSourceRevision)
                {
                    using SkinPackageRevisionCapsule capsule =
                        session.CaptureExistingSourceRevision(cancellationToken);
                    sourceContentRevision = capsule.ContentRevision;
                }

                return new SkinManagedFolderPreparedDelete(this, session, sourceContentRevision);
            }
            catch (OperationCanceledException)
            {
                session.Dispose();
                return new SkinManagedFolderPreparedDelete(
                    this,
                    SkinManagedFolderDeleteOperationResult.Failure(
                        SkinManagedFolderDeleteOperationStatus.Cancelled));
            }
            catch
            {
                session.Dispose();
                return new SkinManagedFolderPreparedDelete(
                    this,
                    SkinManagedFolderDeleteOperationResult.Reject(
                        SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected));
            }
        }

        /// <summary>
        /// Persists and executes a delete using the same authority returned by <see cref="Prepare"/>. This is the
        /// first method in the current-delete path which may create a journal or mutate the source.
        /// </summary>
        internal SkinManagedFolderDeleteOperationResult ExecutePrepared(
            SkinManagedFolderPreparedDelete prepared,
            CancellationToken cancellationToken = default,
            bool requireProtectedFallback = false)
        {
            if (prepared == null
                || !prepared.TryClaimExecution(this, out SkinManagedFolderMutationAuthoritySession session))
            {
                return SkinManagedFolderDeleteOperationResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected);
            }

            SkinManagedFolderDurableMutationReceipt receipt;

            try
            {
                receipt = session.PersistPreparedJournal(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.Cancelled);
            }
            catch
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }

            SkinManagedFolderProtectedFallbackCommitResult fallbackResult;

            try
            {
                fallbackResult = commitFallback(session, receipt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return abortPrepared(
                    session,
                    receipt,
                    SkinManagedFolderDeleteOperationStatus.Cancelled);
            }
            catch
            {
                return abortPrepared(
                    session,
                    receipt,
                    SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }

            if (fallbackResult is not (SkinManagedFolderProtectedFallbackCommitResult.Committed
                or SkinManagedFolderProtectedFallbackCommitResult.NotRequired))
            {
                return session.TryAbortPreparedJournal(receipt, CancellationToken.None)
                    ? SkinManagedFolderDeleteOperationResult.FallbackRejected(fallbackResult)
                    : SkinManagedFolderDeleteOperationResult.Failure(
                        SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }

            SkinManagedFolderDurableMutationReceipt confirmedReceipt;

            try
            {
                confirmedReceipt = session.PersistDeleteFallbackDisposition(
                    receipt,
                    fallbackResult,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return abortPrepared(
                    session,
                    receipt,
                    SkinManagedFolderDeleteOperationStatus.Cancelled);
            }
            catch (InvalidOperationException)
            {
                return session.TryAbortPreparedJournal(
                    receipt,
                    CancellationToken.None)
                    ? SkinManagedFolderDeleteOperationResult.FallbackRejected(
                        SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected)
                    : SkinManagedFolderDeleteOperationResult.Failure(
                        SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }
            catch
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return abortPrepared(
                    session,
                    confirmedReceipt,
                    SkinManagedFolderDeleteOperationStatus.Cancelled);
            }

            try
            {
                bool applied = session.ApplyCapturedDeleteWithDurableReceipt(
                    confirmedReceipt,
                    () => (!requireProtectedFallback
                           && fallbackResult != SkinManagedFolderProtectedFallbackCommitResult.Committed)
                          || isProtectedFallbackValid(),
                    cancellationToken);

                if (!applied)
                {
                    return session.TryAbortPreparedJournal(
                        confirmedReceipt,
                        CancellationToken.None)
                        ? SkinManagedFolderDeleteOperationResult.FallbackRejected(
                            SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected)
                        : SkinManagedFolderDeleteOperationResult.Failure(
                            SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
                }
            }
            catch (OperationCanceledException)
            {
                return abortPrepared(
                    session,
                    confirmedReceipt,
                    SkinManagedFolderDeleteOperationStatus.Cancelled);
            }
            catch
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.FilesystemOutcomeUncertain);
            }

            // The source is now outside its player-visible slot. From here the durable journal and recovery handler,
            // rather than caller cancellation, own cleanup and convergence.
            if (!session.TryDeleteCapturedTombstone(CancellationToken.None))
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.PhysicalDeleteOutcomeUncertain);
            }

            if (!session.TryApplyDeleteRealm(
                    () => applyRealmDelete(
                        session,
                        requireProtectedFallback
                        || fallbackResult == SkinManagedFolderProtectedFallbackCommitResult.Committed),
                    CancellationToken.None))
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.RealmOutcomeUncertain);
            }

            if (!session.TryCommitDelete(CancellationToken.None))
            {
                return SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.CommitOutcomeUncertain);
            }

            return SkinManagedFolderDeleteOperationResult.Success(fallbackResult);
        }

        private bool isProtectedFallbackValid()
            => realm.Run(r =>
            {
                r.Refresh();
                return IsExactProtectedFallbackRecord(
                    r.Find<SkinInfo>(SkinInfo.OMS_SKIN));
            });

        private static SkinManagedFolderDeleteOperationResult abortPrepared(
            SkinManagedFolderMutationAuthoritySession session,
            SkinManagedFolderDurableMutationReceipt receipt,
            SkinManagedFolderDeleteOperationStatus cleanStatus)
            => session.TryAbortPreparedJournal(receipt, CancellationToken.None)
                ? SkinManagedFolderDeleteOperationResult.Failure(cleanStatus)
                : SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);

        private bool applyRealmDelete(
            SkinManagedFolderMutationAuthoritySession session,
            bool requireProtectedFallback)
        {
            SkinManagedFolderExistingRecordAuthority existing = session.ExistingRecord!;
            string tombstonePath = session.TargetNameSlot!.ManagedRelativePath;

            return realm.Write(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(existing.RecordId);

                if ((requireProtectedFallback
                     && !IsExactProtectedFallbackRecord(r.Find<SkinInfo>(SkinInfo.OMS_SKIN)))
                    || record == null
                    || !IsEligibleRecordAt(record, existing.ManagedRelativePath)
                    || !string.Equals(
                        SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(record),
                        existing.RecordFingerprint,
                        StringComparison.Ordinal)
                    || !session.ExactlyMatchesExternalRegistryDeclarations(r.All<SkinInfo>())
                    || HasPathConflict(r, record.ID, existing.ManagedRelativePath)
                    || HasPathConflict(r, record.ID, tombstonePath))
                {
                    return false;
                }

                r.Remove(record);
                return true;
            });
        }

        internal static bool IsEligibleRecordAt(
            SkinInfo record,
            string expectedManagedRelativePath)
            => record.IsManaged
               && record.Files.Count == 0
               && !record.IsExternalFilesystemStorage
               && !record.Protected
               && !record.DeletePending
               && !SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
               && string.Equals(
                   record.FilesystemStorageAuthorityOwner,
                   SkinManagedFolderScanner.AUTHORITY_OWNER,
                   StringComparison.Ordinal)
               && SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
               && !string.IsNullOrEmpty(record.Hash)
               && SkinManagedFolderPath.TryNormalise(record.FilesystemStoragePath, out string normalisedPath)
               && string.Equals(record.FilesystemStoragePath, normalisedPath, StringComparison.Ordinal)
               && string.Equals(normalisedPath, expectedManagedRelativePath, StringComparison.Ordinal);

        internal static bool HasUnresolvedExternalDeclaration(Realm realm)
            => realm.All<SkinInfo>().AsEnumerable().Any(candidate => candidate.IsExternalFilesystemStorage);

        internal static bool HasPathConflict(
            Realm realm,
            Guid authoritativeRecordId,
            string managedRelativePath)
            => realm.All<SkinInfo>()
                    .AsEnumerable()
                    .Any(candidate => candidate.ID != authoritativeRecordId
                                      && SkinManagedFolderPath.TryNormalise(
                                          candidate.FilesystemStoragePath,
                                          out string candidatePath)
                                      && string.Equals(
                                          candidatePath,
                                          managedRelativePath,
                                          StringComparison.OrdinalIgnoreCase));

        internal static bool IsExactProtectedFallbackRecord(SkinInfo? record)
        {
            if (record == null)
                return false;

            SkinInfo expected = OmsSkin.CreateInfo();

            return record.ID == expected.ID
                   && string.Equals(record.Name, expected.Name, StringComparison.Ordinal)
                   && string.Equals(record.Creator, expected.Creator, StringComparison.Ordinal)
                   && string.Equals(record.InstantiationInfo, expected.InstantiationInfo, StringComparison.Ordinal)
                   && string.Equals(record.Hash, expected.Hash, StringComparison.Ordinal)
                   && record.Protected == expected.Protected
                   && record.DeletePending == expected.DeletePending
                   && record.Files.Count == 0
                   && string.Equals(
                       record.FilesystemStoragePath,
                       expected.FilesystemStoragePath,
                       StringComparison.Ordinal)
                   && record.IsExternalFilesystemStorage == expected.IsExternalFilesystemStorage
                   && string.Equals(
                       record.FilesystemStorageAuthorityOwner,
                       expected.FilesystemStorageAuthorityOwner,
                       StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Production recovery policy for operation-tombstoned managed-folder deletes.
    /// </summary>
    internal sealed class SkinManagedFolderDeleteRecoveryHandler
        : ISkinManagedFolderMutationRecoveryHandler,
          ISkinManagedFolderMutationHeldRecoveryHandler
    {
        private readonly RealmAccess realm;
        private readonly ISkinManagedFolderMutationNativeAuthority nativeAuthority;

        public SkinManagedFolderDeleteRecoveryHandler(
            RealmAccess realm,
            ISkinManagedFolderMutationNativeAuthority nativeAuthority)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.nativeAuthority = nativeAuthority ?? throw new ArgumentNullException(nameof(nativeAuthority));
        }

        public SkinManagedFolderMutationRecoveryInspection Inspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal))
                return ambiguousInspection();

            using ISkinManagedFolderMutationNativeSession native = nativeAuthority.Open(cancellationToken);
            return inspect(journal, native, null, cancellationToken);
        }

        public bool CanHandle(SkinManagedFolderMutationKind kind)
            => kind == SkinManagedFolderMutationKind.Delete;

        public SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal)
                || authority == null
                || !authority.Validate(cancellationToken))
            {
                return ambiguousInspection();
            }

            return inspect(journal, authority.NativeSession, authority, cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryInspection inspect(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationNativeSession native,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return ambiguousInspection();

            SkinManagedFolderRenameInspection physical = native.InspectRenameState(
                journal.SourceManagedRelativePath!,
                journal.TargetManagedRelativePath!,
                journal.SourceIdentity!.Value,
                cancellationToken);
            RealmState realmState = inspectRealmState(journal, authority);

            if (!validateAuthority(authority, cancellationToken))
                return ambiguousInspection();

            if (journal.Phase == SkinManagedFolderMutationPhase.Prepared
                && physical.Status == SkinManagedFolderRenameInspectionStatus.SourceOnly
                && realmState == RealmState.Present)
            {
                return new SkinManagedFolderMutationRecoveryInspection(
                    SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack,
                    native.ManagedRootIdentity);
            }

            if ((journal.Phase is SkinManagedFolderMutationPhase.Prepared
                    or SkinManagedFolderMutationPhase.FilesystemApplied)
                && physical.Status == SkinManagedFolderRenameInspectionStatus.TargetOnly
                && realmState == RealmState.Present
                && isFallbackDispositionSatisfied(journal, authority, cancellationToken))
            {
                return new SkinManagedFolderMutationRecoveryInspection(
                    SkinManagedFolderMutationRecoveryDecision.RollForward,
                    native.ManagedRootIdentity,
                    NewRecordPublicationFingerprint: journal.NewRecordPublicationFingerprint);
            }

            if (journal.Phase == SkinManagedFolderMutationPhase.FilesystemApplied
                && physical.Status == SkinManagedFolderRenameInspectionStatus.Neither
                && realmState == RealmState.Present
                && isFallbackDispositionSatisfied(journal, authority, cancellationToken))
            {
                return new SkinManagedFolderMutationRecoveryInspection(
                    SkinManagedFolderMutationRecoveryDecision.RollForward,
                    native.ManagedRootIdentity,
                    NewRecordPublicationFingerprint: journal.NewRecordPublicationFingerprint);
            }

            if ((journal.Phase is SkinManagedFolderMutationPhase.FilesystemApplied
                    or SkinManagedFolderMutationPhase.RealmApplied)
                && physical.Status == SkinManagedFolderRenameInspectionStatus.Neither
                && realmState == RealmState.Absent
                && isFallbackDispositionSatisfied(journal, authority, cancellationToken))
            {
                return new SkinManagedFolderMutationRecoveryInspection(
                    SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted,
                    native.ManagedRootIdentity,
                    NewRecordPublicationFingerprint: journal.NewRecordPublicationFingerprint);
            }

            return ambiguousInspection();
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal))
                return failedAction();

            using ISkinManagedFolderMutationNativeSession native = nativeAuthority.Open(cancellationToken);
            return tryRollForward(journal, native, null, cancellationToken);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal)
                || authority == null
                || !authority.Validate(cancellationToken))
            {
                return failedAction();
            }

            return tryRollForward(
                journal,
                authority.NativeSession,
                authority,
                cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryActionResult tryRollForward(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationNativeSession native,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return failedAction();

            if (!isFallbackDispositionSatisfied(journal, authority, cancellationToken))
                return failedAction();

            SkinManagedFolderRenameInspection before = native.InspectRenameState(
                journal.SourceManagedRelativePath!,
                journal.TargetManagedRelativePath!,
                journal.SourceIdentity!.Value,
                cancellationToken);

            if (before.Status == SkinManagedFolderRenameInspectionStatus.TargetOnly)
            {
                if (!validateAuthority(authority, cancellationToken))
                    return failedAction();

                native.CleanupExactDeleteTombstone(
                    journal.SourceManagedRelativePath!,
                    journal.TargetManagedRelativePath!,
                    journal.SourceIdentity.Value,
                    journal.DeleteSourceNodeManifest!,
                    cancellationToken);

                if (!validateAuthority(authority, cancellationToken))
                    return failedAction();
            }
            else if (before.Status != SkinManagedFolderRenameInspectionStatus.Neither)
                return failedAction();

            cancellationToken.ThrowIfCancellationRequested();

            if (!isPhysicalDeleteComplete(native, journal, cancellationToken)
                || !applyRecoveryRealmDelete(journal, authority, cancellationToken)
                || !isPhysicalDeleteComplete(native, journal, cancellationToken)
                || inspectRealmState(journal, authority) != RealmState.Absent
                || !validateAuthority(authority, cancellationToken))
            {
                return failedAction();
            }

            return new SkinManagedFolderMutationRecoveryActionResult(
                true,
                native.ManagedRootIdentity,
                NewRecordPublicationFingerprint: journal.NewRecordPublicationFingerprint);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal)
                || journal.Phase != SkinManagedFolderMutationPhase.Prepared)
            {
                return failedAction();
            }

            using ISkinManagedFolderMutationNativeSession native = nativeAuthority.Open(cancellationToken);
            return tryRollBack(journal, native, null, cancellationToken);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryValidateDeleteJournal(journal)
                || journal.Phase != SkinManagedFolderMutationPhase.Prepared
                || authority == null
                || !authority.Validate(cancellationToken))
            {
                return failedAction();
            }

            return tryRollBack(
                journal,
                authority.NativeSession,
                authority,
                cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryActionResult tryRollBack(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationNativeSession native,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return failedAction();

            SkinManagedFolderRenameInspection physical = native.InspectRenameState(
                journal.SourceManagedRelativePath!,
                journal.TargetManagedRelativePath!,
                journal.SourceIdentity!.Value,
                cancellationToken);

            return physical.Status == SkinManagedFolderRenameInspectionStatus.SourceOnly
                   && inspectRealmState(journal, authority) == RealmState.Present
                   && validateAuthority(authority, cancellationToken)
                ? new SkinManagedFolderMutationRecoveryActionResult(true, native.ManagedRootIdentity)
                : failedAction();
        }

        public override string ToString() => nameof(SkinManagedFolderDeleteRecoveryHandler);

        private RealmState inspectRealmState(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority)
            => realm.Run(r =>
            {
                r.Refresh();

                if (!externalDeclarationsMatch(r, authority))
                    return RealmState.Ambiguous;

                SkinInfo? record = r.Find<SkinInfo>(journal.RecordId!.Value);

                if (record == null)
                {
                    return hasAnyPathConflict(r, journal.SourceManagedRelativePath!)
                           || hasAnyPathConflict(r, journal.TargetManagedRelativePath!)
                        ? RealmState.Ambiguous
                        : RealmState.Absent;
                }

                return SkinManagedFolderDeleteOperation.IsEligibleRecordAt(
                           record,
                           journal.SourceManagedRelativePath!)
                       && string.Equals(
                           SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(record),
                           journal.NewRecordPublicationFingerprint,
                           StringComparison.Ordinal)
                       && !SkinManagedFolderDeleteOperation.HasPathConflict(
                           r,
                           record.ID,
                           journal.SourceManagedRelativePath!)
                       && !SkinManagedFolderDeleteOperation.HasPathConflict(
                           r,
                           record.ID,
                           journal.TargetManagedRelativePath!)
                    ? RealmState.Present
                    : RealmState.Ambiguous;
            });

        private bool applyRecoveryRealmDelete(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (!validateAuthority(authority, cancellationToken))
                return false;

            bool deleted = realm.Write(r =>
            {
                r.Refresh();

                if (!externalDeclarationsMatch(r, authority))
                    return false;

                if (journal.DeleteFallbackDisposition
                        == SkinManagedFolderDeleteFallbackDisposition.ProtectedPairCommitted
                    && !SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(
                        r.Find<SkinInfo>(SkinInfo.OMS_SKIN)))
                {
                    return false;
                }

                SkinInfo? record = r.Find<SkinInfo>(journal.RecordId!.Value);

                if (record == null)
                {
                    return !hasAnyPathConflict(r, journal.SourceManagedRelativePath!)
                           && !hasAnyPathConflict(r, journal.TargetManagedRelativePath!);
                }

                if (!SkinManagedFolderDeleteOperation.IsEligibleRecordAt(
                        record,
                        journal.SourceManagedRelativePath!)
                    || !string.Equals(
                        SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(record),
                        journal.NewRecordPublicationFingerprint,
                        StringComparison.Ordinal)
                    || SkinManagedFolderDeleteOperation.HasPathConflict(
                        r,
                        record.ID,
                        journal.SourceManagedRelativePath!)
                    || SkinManagedFolderDeleteOperation.HasPathConflict(
                        r,
                        record.ID,
                        journal.TargetManagedRelativePath!))
                {
                    return false;
                }

                r.Remove(record);
                return true;
            });

            return deleted && validateAuthority(authority, cancellationToken);
        }

        private static bool externalDeclarationsMatch(
            Realm realm,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority)
            => authority?.ExactlyMatchesRealmDeclarations(realm.All<SkinInfo>())
               ?? !SkinManagedFolderDeleteOperation.HasUnresolvedExternalDeclaration(realm);

        private static bool validateAuthority(
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
            => authority?.Validate(cancellationToken) ?? true;

        private static bool isPhysicalDeleteComplete(
            ISkinManagedFolderMutationNativeSession native,
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => native.InspectRenameState(
                    journal.SourceManagedRelativePath!,
                    journal.TargetManagedRelativePath!,
                    journal.SourceIdentity!.Value,
                    cancellationToken)
                .Status == SkinManagedFolderRenameInspectionStatus.Neither;

        private static bool hasAnyPathConflict(Realm realm, string managedRelativePath)
            => realm.All<SkinInfo>()
                    .AsEnumerable()
                    .Any(candidate => SkinManagedFolderPath.TryNormalise(
                                          candidate.FilesystemStoragePath,
                                          out string candidatePath)
                                      && string.Equals(
                                          candidatePath,
                                          managedRelativePath,
                                          StringComparison.OrdinalIgnoreCase));

        private bool isFallbackDispositionSatisfied(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (!validateAuthority(authority, cancellationToken))
                return false;

            if (journal.DeleteFallbackDisposition
                == SkinManagedFolderDeleteFallbackDisposition.NotRequired)
            {
                return true;
            }

            return journal.DeleteFallbackDisposition
                       == SkinManagedFolderDeleteFallbackDisposition.ProtectedPairCommitted
                   && realm.Run(r =>
                   {
                       r.Refresh();
                       return SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(
                           r.Find<SkinInfo>(SkinInfo.OMS_SKIN));
                   });
        }

        private static bool tryValidateDeleteJournal(SkinManagedFolderMutationJournal journal)
            => journal != null
               && journal.IsValid()
               && journal.Kind == SkinManagedFolderMutationKind.Delete
               && journal.RecordId is { } recordId
               && recordId != Guid.Empty
               && journal.SourceManagedRelativePath != null
               && string.Equals(
                   journal.TargetManagedRelativePath,
                   SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(journal.OperationId),
                   StringComparison.Ordinal)
               && journal.SourceIdentity is { IsUsable: true };

        private static SkinManagedFolderMutationRecoveryInspection ambiguousInspection()
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);

        private static SkinManagedFolderMutationRecoveryActionResult failedAction()
            => new SkinManagedFolderMutationRecoveryActionResult(false);

        private enum RealmState
        {
            Ambiguous,
            Present,
            Absent,
        }
    }
}
