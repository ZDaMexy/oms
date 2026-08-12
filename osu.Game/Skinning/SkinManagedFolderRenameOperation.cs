// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Database;
using Realms;

namespace osu.Game.Skinning
{
    internal enum SkinManagedFolderRenameOperationStatus
    {
        Succeeded,
        AuthorityRejected,
        Busy,
        Shutdown,
        Cancelled,
        PreparedJournalOutcomeUncertain,
        FilesystemOutcomeUncertain,
        RealmOutcomeUncertain,
        CommitOutcomeUncertain,
    }

    /// <summary>
    /// Non-sensitive result of one managed-folder rename request.
    /// </summary>
    internal sealed class SkinManagedFolderRenameOperationResult
    {
        public SkinManagedFolderRenameOperationStatus Status { get; }

        public SkinManagedFolderMutationAuthorityRejectionReason AuthorityRejectionReason { get; }

        public bool IsSuccess => Status == SkinManagedFolderRenameOperationStatus.Succeeded;

        private SkinManagedFolderRenameOperationResult(
            SkinManagedFolderRenameOperationStatus status,
            SkinManagedFolderMutationAuthorityRejectionReason authorityRejectionReason =
                SkinManagedFolderMutationAuthorityRejectionReason.None)
        {
            Status = status;
            AuthorityRejectionReason = authorityRejectionReason;
        }

        public static SkinManagedFolderRenameOperationResult Success()
            => new SkinManagedFolderRenameOperationResult(SkinManagedFolderRenameOperationStatus.Succeeded);

        public static SkinManagedFolderRenameOperationResult Reject(
            SkinManagedFolderMutationAuthorityRejectionReason reason)
            => new SkinManagedFolderRenameOperationResult(
                SkinManagedFolderRenameOperationStatus.AuthorityRejected,
                reason);

        public static SkinManagedFolderRenameOperationResult Failure(
            SkinManagedFolderRenameOperationStatus status)
        {
            if (status is SkinManagedFolderRenameOperationStatus.Succeeded
                or SkinManagedFolderRenameOperationStatus.AuthorityRejected)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new SkinManagedFolderRenameOperationResult(status);
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderRenameOperationResult)}:{Status}";
    }

    /// <summary>
    /// Executes the directory-only managed chartskin rename against the existing shared authority and journal.
    /// </summary>
    /// <remarks>
    /// The author-controlled display metadata and package bytes are intentionally left unchanged. Once the first
    /// physical step has been attempted, cancellation can no longer turn an uncertain result into a clean abort; the
    /// durable journal remains for the production recovery handler.
    /// </remarks>
    internal sealed class SkinManagedFolderRenameOperation
    {
        private readonly RealmAccess realm;
        private readonly SkinManagedFolderMutationAuthority authority;

        public SkinManagedFolderRenameOperation(
            RealmAccess realm,
            SkinManagedFolderMutationAuthority authority)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public SkinManagedFolderRenameOperationResult Execute(
            Guid operationId,
            Guid recordId,
            string targetChildName,
            CancellationToken cancellationToken = default)
        {
            SkinManagedFolderMutationAuthorityResult opened;

            try
            {
                opened = authority.OpenRename(operationId, recordId, targetChildName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.Cancelled);
            }

            if (!opened.IsSuccess)
                return SkinManagedFolderRenameOperationResult.Reject(opened.RejectionReason);

            using SkinManagedFolderMutationAuthoritySession session = opened.Session!;
            SkinManagedFolderDurableMutationReceipt receipt;

            try
            {
                receipt = session.PersistPreparedJournal(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.Cancelled);
            }
            catch
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.PreparedJournalOutcomeUncertain);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return session.TryAbortPreparedJournal(receipt, CancellationToken.None)
                    ? SkinManagedFolderRenameOperationResult.Failure(
                        SkinManagedFolderRenameOperationStatus.Cancelled)
                    : SkinManagedFolderRenameOperationResult.Failure(
                        SkinManagedFolderRenameOperationStatus.PreparedJournalOutcomeUncertain);
            }

            try
            {
                session.ApplyCapturedRenameWithDurableReceipt(receipt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return session.TryAbortPreparedJournal(receipt, CancellationToken.None)
                    ? SkinManagedFolderRenameOperationResult.Failure(
                        SkinManagedFolderRenameOperationStatus.Cancelled)
                    : SkinManagedFolderRenameOperationResult.Failure(
                        SkinManagedFolderRenameOperationStatus.PreparedJournalOutcomeUncertain);
            }
            catch
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.FilesystemOutcomeUncertain);
            }

            // After the physical rename is attempted, complete or retain the durable intent without observing caller
            // cancellation. Recovery, not cancellation, owns every uncertain post-filesystem outcome.
            if (!session.TryApplyRenameRealm(
                    () => applyRealmPathRename(session),
                    CancellationToken.None))
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.RealmOutcomeUncertain);
            }

            if (!session.TryCommitRename(CancellationToken.None))
            {
                return SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.CommitOutcomeUncertain);
            }

            return SkinManagedFolderRenameOperationResult.Success();
        }

        private bool applyRealmPathRename(SkinManagedFolderMutationAuthoritySession session)
        {
            SkinManagedFolderExistingRecordAuthority existing = session.ExistingRecord!;
            SkinManagedFolderTargetNameSlot target = session.TargetNameSlot!;

            return realm.Write(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(existing.RecordId);

                if (record == null
                    || !isEligibleRecordAt(record, existing.ManagedRelativePath)
                    || !session.ExactlyMatchesExternalRegistryDeclarations(r.All<SkinInfo>())
                    || hasPathConflict(r, record.ID, existing.ManagedRelativePath)
                    || hasPathConflict(r, record.ID, target.ManagedRelativePath))
                {
                    return false;
                }

                // Directory name is workspace storage identity. The author-controlled Name/Creator/skin.ini metadata,
                // package revision and scanner owner remain untouched.
                record.FilesystemStoragePath = target.ManagedRelativePath;
                return true;
            });
        }

        private static bool isEligibleRecordAt(SkinInfo record, string expectedManagedRelativePath)
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

        private static bool hasPathConflict(
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
    }

    /// <summary>
    /// Production recovery policy for directory-only managed chartskin rename journals.
    /// </summary>
    internal sealed class SkinManagedFolderRenameRecoveryHandler
        : ISkinManagedFolderMutationRecoveryHandler,
          ISkinManagedFolderMutationHeldRecoveryHandler
    {
        private readonly RealmAccess realm;
        private readonly ISkinManagedFolderMutationNativeAuthority nativeAuthority;

        public SkinManagedFolderRenameRecoveryHandler(
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
            if (!tryValidateRenameJournal(journal))
                return ambiguousInspection();

            using ISkinManagedFolderMutationNativeSession native = nativeAuthority.Open(cancellationToken);
            return inspect(journal, native, null, cancellationToken);
        }

        public bool CanHandle(SkinManagedFolderMutationKind kind)
            => kind == SkinManagedFolderMutationKind.Rename;

        public SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryValidateRenameJournal(journal)
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

            return (physical.Status, realmState) switch
            {
                (SkinManagedFolderRenameInspectionStatus.SourceOnly, RealmState.Source) =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack,
                        native.ManagedRootIdentity),

                (SkinManagedFolderRenameInspectionStatus.SourceOnly, RealmState.Target) =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.RollBack,
                        native.ManagedRootIdentity),

                (SkinManagedFolderRenameInspectionStatus.TargetOnly, RealmState.Source) =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.RollForward,
                        native.ManagedRootIdentity,
                        journal.SourceIdentity),

                (SkinManagedFolderRenameInspectionStatus.TargetOnly, RealmState.Target) =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted,
                        native.ManagedRootIdentity,
                        journal.SourceIdentity),

                _ => ambiguousInspection(),
            };
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => tryReconcileRealmToPhysical(
                journal,
                SkinManagedFolderRenameInspectionStatus.TargetOnly,
                RealmState.Target,
                journal.TargetManagedRelativePath,
                journal.SourceIdentity,
                cancellationToken,
                null);

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => tryReconcileRealmToPhysical(
                journal,
                SkinManagedFolderRenameInspectionStatus.SourceOnly,
                RealmState.Source,
                journal.SourceManagedRelativePath,
                null,
                cancellationToken,
                null);

        public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => tryReconcileRealmToPhysical(
                journal,
                SkinManagedFolderRenameInspectionStatus.TargetOnly,
                RealmState.Target,
                journal.TargetManagedRelativePath,
                journal.SourceIdentity,
                cancellationToken,
                authority);

        public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => tryReconcileRealmToPhysical(
                journal,
                SkinManagedFolderRenameInspectionStatus.SourceOnly,
                RealmState.Source,
                journal.SourceManagedRelativePath,
                null,
                cancellationToken,
                authority);

        public override string ToString() => nameof(SkinManagedFolderRenameRecoveryHandler);

        private SkinManagedFolderMutationRecoveryActionResult tryReconcileRealmToPhysical(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderRenameInspectionStatus requiredPhysicalState,
            RealmState finalRealmState,
            string? finalRealmPath,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            CancellationToken cancellationToken,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority)
        {
            if (!tryValidateRenameJournal(journal) || finalRealmPath == null)
                return failedAction();

            if (authority == null)
            {
                using ISkinManagedFolderMutationNativeSession opened = nativeAuthority.Open(cancellationToken);
                return reconcileRealmToPhysical(
                    journal,
                    requiredPhysicalState,
                    finalRealmState,
                    finalRealmPath,
                    targetIdentity,
                    opened,
                    null,
                    cancellationToken);
            }

            if (!authority.Validate(cancellationToken))
                return failedAction();

            return reconcileRealmToPhysical(
                journal,
                requiredPhysicalState,
                finalRealmState,
                finalRealmPath,
                targetIdentity,
                authority.NativeSession,
                authority,
                cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryActionResult reconcileRealmToPhysical(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderRenameInspectionStatus requiredPhysicalState,
            RealmState finalRealmState,
            string finalRealmPath,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            ISkinManagedFolderMutationNativeSession native,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return failedAction();

            SkinManagedFolderRenameInspection before = native.InspectRenameState(
                journal.SourceManagedRelativePath!,
                journal.TargetManagedRelativePath!,
                journal.SourceIdentity!.Value,
                cancellationToken);

            if (before.Status != requiredPhysicalState)
                return failedAction();

            bool realmApplied = applyRecoveryRealmPath(
                journal,
                finalRealmPath,
                authority,
                cancellationToken);

            if (!realmApplied)
                return failedAction();

            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderRenameInspection after = native.InspectRenameState(
                journal.SourceManagedRelativePath!,
                journal.TargetManagedRelativePath!,
                journal.SourceIdentity!.Value,
                cancellationToken);

            if (after.Status != requiredPhysicalState
                || inspectRealmState(journal, authority) != finalRealmState
                || !validateAuthority(authority, cancellationToken))
            {
                return failedAction();
            }

            return new SkinManagedFolderMutationRecoveryActionResult(
                true,
                native.ManagedRootIdentity,
                targetIdentity);
        }

        private RealmState inspectRealmState(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority)
            => realm.Run(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(journal.RecordId!.Value);

                if (record == null
                    || !externalDeclarationsMatch(r, authority)
                    || !isEligibleRecoveryRecord(record)
                    || hasPathConflict(r, record.ID, journal.SourceManagedRelativePath!)
                    || hasPathConflict(r, record.ID, journal.TargetManagedRelativePath!))
                {
                    return RealmState.Ambiguous;
                }

                if (string.Equals(
                        record.FilesystemStoragePath,
                        journal.SourceManagedRelativePath,
                        StringComparison.Ordinal))
                {
                    return RealmState.Source;
                }

                return string.Equals(
                    record.FilesystemStoragePath,
                    journal.TargetManagedRelativePath,
                    StringComparison.Ordinal)
                    ? RealmState.Target
                    : RealmState.Ambiguous;
            });

        private bool applyRecoveryRealmPath(
            SkinManagedFolderMutationJournal journal,
            string expectedPath,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (!validateAuthority(authority, cancellationToken))
                return false;

            bool applied = realm.Write(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(journal.RecordId!.Value);

                if (record == null
                    || !externalDeclarationsMatch(r, authority)
                    || !isEligibleRecoveryRecord(record)
                    || hasPathConflict(r, record.ID, journal.SourceManagedRelativePath!)
                    || hasPathConflict(r, record.ID, journal.TargetManagedRelativePath!))
                {
                    return false;
                }

                bool atSource = string.Equals(
                    record.FilesystemStoragePath,
                    journal.SourceManagedRelativePath,
                    StringComparison.Ordinal);
                bool atTarget = string.Equals(
                    record.FilesystemStoragePath,
                    journal.TargetManagedRelativePath,
                    StringComparison.Ordinal);

                if (!atSource && !atTarget)
                    return false;

                record.FilesystemStoragePath = expectedPath;
                return true;
            });

            return applied && validateAuthority(authority, cancellationToken);
        }

        private static bool externalDeclarationsMatch(
            Realm realm,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority)
            => authority?.ExactlyMatchesRealmDeclarations(realm.All<SkinInfo>())
               ?? !hasUnresolvedExternalDeclaration(realm);

        private static bool validateAuthority(
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
            => authority?.Validate(cancellationToken) ?? true;

        private static bool isEligibleRecoveryRecord(SkinInfo record)
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
               && string.Equals(record.FilesystemStoragePath, normalisedPath, StringComparison.Ordinal);

        private static bool hasUnresolvedExternalDeclaration(Realm realm)
            => realm.All<SkinInfo>().AsEnumerable().Any(candidate => candidate.IsExternalFilesystemStorage);

        private static bool hasPathConflict(
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

        private static bool tryValidateRenameJournal(SkinManagedFolderMutationJournal journal)
            => journal != null
               && journal.IsValid()
               && journal.Kind == SkinManagedFolderMutationKind.Rename
               && journal.RecordId is { } recordId
               && recordId != Guid.Empty
               && journal.SourceManagedRelativePath != null
               && journal.TargetManagedRelativePath != null
               && journal.SourceIdentity is { IsUsable: true };

        private static SkinManagedFolderMutationRecoveryInspection ambiguousInspection()
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);

        private static SkinManagedFolderMutationRecoveryActionResult failedAction()
            => new SkinManagedFolderMutationRecoveryActionResult(false);

        private enum RealmState
        {
            Ambiguous,
            Source,
            Target,
        }
    }
}
