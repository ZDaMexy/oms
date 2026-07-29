// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Database;
using Realms;

namespace osu.Game.Skinning
{
    internal enum SkinManagedFolderStagedImportOperationStatus
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
    /// Non-sensitive result of one internal staged chartskin import request.
    /// </summary>
    internal sealed class SkinManagedFolderStagedImportOperationResult
    {
        public SkinManagedFolderStagedImportOperationStatus Status { get; }

        public SkinManagedFolderMutationAuthorityRejectionReason AuthorityRejectionReason { get; }

        public bool IsSuccess => Status == SkinManagedFolderStagedImportOperationStatus.Succeeded;

        private SkinManagedFolderStagedImportOperationResult(
            SkinManagedFolderStagedImportOperationStatus status,
            SkinManagedFolderMutationAuthorityRejectionReason authorityRejectionReason =
                SkinManagedFolderMutationAuthorityRejectionReason.None)
        {
            Status = status;
            AuthorityRejectionReason = authorityRejectionReason;
        }

        public static SkinManagedFolderStagedImportOperationResult Success()
            => new SkinManagedFolderStagedImportOperationResult(
                SkinManagedFolderStagedImportOperationStatus.Succeeded);

        public static SkinManagedFolderStagedImportOperationResult Reject(
            SkinManagedFolderMutationAuthorityRejectionReason reason)
            => new SkinManagedFolderStagedImportOperationResult(
                SkinManagedFolderStagedImportOperationStatus.AuthorityRejected,
                reason);

        public static SkinManagedFolderStagedImportOperationResult Failure(
            SkinManagedFolderStagedImportOperationStatus status)
        {
            if (status is SkinManagedFolderStagedImportOperationStatus.Succeeded
                or SkinManagedFolderStagedImportOperationStatus.AuthorityRejected)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new SkinManagedFolderStagedImportOperationResult(status);
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderStagedImportOperationResult)}:{Status}";
    }

    /// <summary>
    /// Publishes one operation-owned provisional package into an absent managed-folder slot.
    /// </summary>
    /// <remarks>
    /// The source is the fixed OMS staging child derived from the operation ID. It is a disposable
    /// provisional copy; no external source path or delete capability is accepted. Cancellation is observed only
    /// before the physical move. Once the move is attempted, the journal and recovery policy own convergence.
    /// </remarks>
    internal sealed class SkinManagedFolderStagedImportOperation
    {
        private readonly RealmAccess realm;
        private readonly SkinManagedFolderMutationAuthority authority;

        internal Action AuthorityOpened { get; set; } = () => { };

        public SkinManagedFolderStagedImportOperation(
            RealmAccess realm,
            SkinManagedFolderMutationAuthority authority)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public SkinManagedFolderStagedImportOperationResult Execute(
            Guid operationId,
            string targetChildName,
            CancellationToken cancellationToken = default)
        {
            SkinManagedFolderMutationAuthorityResult opened;

            try
            {
                opened = authority.OpenStagedImport(
                    operationId,
                    targetChildName,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return failure(SkinManagedFolderStagedImportOperationStatus.Cancelled);
            }

            if (!opened.IsSuccess)
                return SkinManagedFolderStagedImportOperationResult.Reject(opened.RejectionReason);

            using SkinManagedFolderMutationAuthoritySession session = opened.Session!;
            SkinManagedFolderDurableMutationReceipt receipt;

            try
            {
                AuthorityOpened();
                receipt = session.PersistPreparedJournal(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return failure(SkinManagedFolderStagedImportOperationStatus.Cancelled);
            }
            catch
            {
                return failure(
                    SkinManagedFolderStagedImportOperationStatus.PreparedJournalOutcomeUncertain);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return session.TryRollbackPreparedStagedImport(receipt, CancellationToken.None)
                    ? failure(SkinManagedFolderStagedImportOperationStatus.Cancelled)
                    : failure(
                        SkinManagedFolderStagedImportOperationStatus.PreparedJournalOutcomeUncertain);
            }

            try
            {
                session.ApplyCapturedStagedImportWithDurableReceipt(receipt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return session.TryRollbackPreparedStagedImport(receipt, CancellationToken.None)
                    ? failure(SkinManagedFolderStagedImportOperationStatus.Cancelled)
                    : failure(
                        SkinManagedFolderStagedImportOperationStatus.PreparedJournalOutcomeUncertain);
            }
            catch
            {
                return failure(
                    SkinManagedFolderStagedImportOperationStatus.FilesystemOutcomeUncertain);
            }

            // The move is externally visible. Do not observe caller cancellation from this point onward.
            if (!session.TryPublishStagedImportRealm(
                    publishExactNewRecord,
                    CancellationToken.None))
            {
                return failure(
                    SkinManagedFolderStagedImportOperationStatus.RealmOutcomeUncertain);
            }

            if (!session.TryCommitStagedImport(CancellationToken.None))
            {
                return failure(
                    SkinManagedFolderStagedImportOperationStatus.CommitOutcomeUncertain);
            }

            return SkinManagedFolderStagedImportOperationResult.Success();
        }

        private bool publishExactNewRecord(
            SkinManagedFolderNewRecordPublicationData publication)
        {
            bool applied = realm.Write(r =>
            {
                r.Refresh();

                if (hasUnresolvedExternalDeclaration(r)
                    || r.Find<SkinInfo>(publication.RecordId) != null
                    || hasPathConflict(r, publication.RecordId, publication.ManagedRelativePath))
                {
                    return false;
                }

                SkinInfo record = publication.CreateRecord();
                r.Add(record);
                return publication.IsExactRecord(record);
            });

            if (!applied)
                return false;

            return realm.Run(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(publication.RecordId);
                return record != null
                       && publication.IsExactRecord(record)
                       && !hasUnresolvedExternalDeclaration(r)
                       && !hasPathConflict(
                           r,
                           publication.RecordId,
                           publication.ManagedRelativePath);
            });
        }

        private static bool hasUnresolvedExternalDeclaration(Realm realm)
            => realm.All<SkinInfo>().AsEnumerable().Any(
                candidate => candidate.IsExternalFilesystemStorage);

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

        private static SkinManagedFolderStagedImportOperationResult failure(
            SkinManagedFolderStagedImportOperationStatus status)
            => SkinManagedFolderStagedImportOperationResult.Failure(status);
    }

    /// <summary>
    /// Production recovery policy for one fixed operation-owned staged import.
    /// </summary>
    internal sealed class SkinManagedFolderStagedImportRecoveryHandler
        : ISkinManagedFolderMutationRecoveryHandler
    {
        private readonly RealmAccess realm;
        private readonly ISkinManagedFolderMutationNativeAuthority nativeAuthority;

        public SkinManagedFolderStagedImportRecoveryHandler(
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
            if (!tryValidateJournal(journal))
                return ambiguousInspection();

            using ISkinManagedFolderMutationNativeSession native =
                nativeAuthority.Open(cancellationToken);

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return ambiguousInspection();

            SkinManagedFolderStagedImportInspection physical =
                inspectPhysical(native, journal, cancellationToken);

            if (physical.Status is SkinManagedFolderStagedImportInspectionStatus.Both
                or SkinManagedFolderStagedImportInspectionStatus.IdentityMismatch
                or SkinManagedFolderStagedImportInspectionStatus.RootIdentityMismatch
                || physical.ManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return ambiguousInspection();
            }

            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(
                    journal,
                    physical.PackageMetadata,
                    physical.TreeFingerprint);
            RealmState realmState = inspectRealmState(
                journal,
                publication,
                requireDurableFingerprint: physical.Status
                    == SkinManagedFolderStagedImportInspectionStatus.Neither);

            return (physical.Status, realmState) switch
            {
                (SkinManagedFolderStagedImportInspectionStatus.TargetOnly, RealmState.Absent)
                    when publication != null =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.RollForward,
                        native.ManagedRootIdentity,
                        physical.TargetIdentity,
                        publication.Fingerprint),

                (SkinManagedFolderStagedImportInspectionStatus.TargetOnly, RealmState.Exact)
                    when publication != null =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted,
                        native.ManagedRootIdentity,
                        physical.TargetIdentity,
                        publication.Fingerprint),

                (SkinManagedFolderStagedImportInspectionStatus.SourceOnly, RealmState.Absent) =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.RollBack,
                        native.ManagedRootIdentity),

                (SkinManagedFolderStagedImportInspectionStatus.SourceOnly, RealmState.Exact) =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.RollBack,
                        native.ManagedRootIdentity),

                (SkinManagedFolderStagedImportInspectionStatus.Neither, RealmState.Absent) =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack,
                        native.ManagedRootIdentity),

                (SkinManagedFolderStagedImportInspectionStatus.Neither, RealmState.Exact) =>
                    inspection(
                        SkinManagedFolderMutationRecoveryDecision.RollBack,
                        native.ManagedRootIdentity),

                _ => ambiguousInspection(),
            };
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (!tryValidateJournal(journal))
                return failedAction();

            using ISkinManagedFolderMutationNativeSession native =
                nativeAuthority.Open(cancellationToken);

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return failedAction();

            SkinManagedFolderStagedImportInspection before =
                inspectPhysical(native, journal, cancellationToken);
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(
                    journal,
                    before.PackageMetadata,
                    before.TreeFingerprint);

            if (before.Status != SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                || before.TargetIdentity != journal.StagedSourceIdentity
                || publication == null
                || !matchesDurableFingerprintIfPresent(journal, publication))
            {
                return failedAction();
            }

            RealmState realmState = inspectRealmState(
                journal,
                publication,
                requireDurableFingerprint: false);

            if (realmState == RealmState.Absent)
            {
                if (!publishRecoveryRecord(publication))
                    return failedAction();
            }
            else if (realmState != RealmState.Exact)
                return failedAction();

            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderStagedImportInspection after =
                inspectPhysical(native, journal, cancellationToken);

            if (after.Status != SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                || after.TargetIdentity != journal.StagedSourceIdentity
                || after.PackageMetadata == null)
            {
                return failedAction();
            }

            SkinManagedFolderNewRecordPublicationData? recaptured =
                tryCreatePublication(
                    journal,
                    after.PackageMetadata,
                    after.TreeFingerprint);

            if (recaptured == null
                || !string.Equals(
                    recaptured.Fingerprint,
                    publication.Fingerprint,
                    StringComparison.Ordinal)
                || inspectRealmState(
                    journal,
                    publication,
                    requireDurableFingerprint: false) != RealmState.Exact)
            {
                return failedAction();
            }

            return new SkinManagedFolderMutationRecoveryActionResult(
                true,
                native.ManagedRootIdentity,
                after.TargetIdentity,
                publication.Fingerprint);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            if (!tryValidateJournal(journal))
                return failedAction();

            using ISkinManagedFolderMutationNativeSession native =
                nativeAuthority.Open(cancellationToken);

            if (native.ManagedRootIdentity != journal.ManagedRootIdentity)
                return failedAction();

            SkinManagedFolderStagedImportInspection before =
                inspectPhysical(native, journal, cancellationToken);

            if (before.Status is not (
                    SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                    or SkinManagedFolderStagedImportInspectionStatus.Neither))
            {
                return failedAction();
            }

            bool requireDurableFingerprint =
                before.Status == SkinManagedFolderStagedImportInspectionStatus.Neither;
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(
                    journal,
                    before.PackageMetadata,
                    before.TreeFingerprint);
            RealmState realmState = inspectRealmState(
                journal,
                publication,
                requireDurableFingerprint);

            if (realmState == RealmState.Conflict)
                return failedAction();

            if (realmState == RealmState.Exact
                && !deleteExactRecoveryRecord(
                    journal,
                    publication,
                    requireDurableFingerprint))
            {
                return failedAction();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (before.Status == SkinManagedFolderStagedImportInspectionStatus.SourceOnly)
            {
                native.CleanupExactStagedSource(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.StagedRootIdentity!.Value,
                    journal.StagedSourceIdentity!.Value,
                    cancellationToken);
            }

            SkinManagedFolderStagedImportInspection after =
                inspectPhysical(native, journal, cancellationToken);

            if (after.Status != SkinManagedFolderStagedImportInspectionStatus.Neither
                || inspectRealmState(
                    journal,
                    null,
                    requireDurableFingerprint: true) != RealmState.Absent)
            {
                return failedAction();
            }

            return new SkinManagedFolderMutationRecoveryActionResult(
                true,
                native.ManagedRootIdentity);
        }

        public override string ToString()
            => nameof(SkinManagedFolderStagedImportRecoveryHandler);

        private SkinManagedFolderStagedImportInspection inspectPhysical(
            ISkinManagedFolderMutationNativeSession native,
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => native.InspectStagedImportState(
                journal.OperationId,
                journal.TargetManagedRelativePath!,
                journal.StagedRootIdentity!.Value,
                journal.StagedSourceIdentity!.Value,
                cancellationToken);

        private static SkinManagedFolderNewRecordPublicationData? tryCreatePublication(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderPackageMetadata? metadata,
            string? treeFingerprint)
        {
            if (metadata == null
                || !string.Equals(
                    metadata.ContentRevision,
                    journal.StagedSourceContentRevision,
                    StringComparison.Ordinal)
                || !string.Equals(
                    treeFingerprint,
                    journal.StagedSourceTreeFingerprint,
                    StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var plan = new SkinManagedFolderNewRecordPublicationPlan(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.ManagedRootIdentity);
                SkinManagedFolderNewRecordPublicationData publication =
                    plan.CreatePublicationData(metadata);
                return matchesDurableFingerprintIfPresent(journal, publication)
                    ? publication
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private RealmState inspectRealmState(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderNewRecordPublicationData? publication,
            bool requireDurableFingerprint)
            => realm.Run(r =>
            {
                r.Refresh();

                if (hasUnresolvedExternalDeclaration(r))
                    return RealmState.Conflict;

                SkinInfo? record = r.Find<SkinInfo>(journal.OperationId);
                bool pathConflict = hasPathConflict(
                    r,
                    journal.OperationId,
                    journal.TargetManagedRelativePath!);

                if (record == null)
                    return pathConflict ? RealmState.Conflict : RealmState.Absent;

                if (pathConflict)
                    return RealmState.Conflict;

                bool exact = publication != null
                    ? publication.IsExactRecord(record)
                    : isExactDurableRecord(journal, record, requireDurableFingerprint);
                return exact ? RealmState.Exact : RealmState.Conflict;
            });

        private bool publishRecoveryRecord(
            SkinManagedFolderNewRecordPublicationData publication)
        {
            bool added = realm.Write(r =>
            {
                r.Refresh();

                if (hasUnresolvedExternalDeclaration(r)
                    || r.Find<SkinInfo>(publication.RecordId) != null
                    || hasPathConflict(
                        r,
                        publication.RecordId,
                        publication.ManagedRelativePath))
                {
                    return false;
                }

                SkinInfo record = publication.CreateRecord();
                r.Add(record);
                return publication.IsExactRecord(record);
            });

            return added && realm.Run(r =>
            {
                r.Refresh();
                SkinInfo? record = r.Find<SkinInfo>(publication.RecordId);
                return record != null
                       && publication.IsExactRecord(record)
                       && !hasUnresolvedExternalDeclaration(r)
                       && !hasPathConflict(
                           r,
                           publication.RecordId,
                           publication.ManagedRelativePath);
            });
        }

        private bool deleteExactRecoveryRecord(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderNewRecordPublicationData? publication,
            bool requireDurableFingerprint)
            => realm.Write(r =>
            {
                r.Refresh();

                if (hasUnresolvedExternalDeclaration(r)
                    || hasPathConflict(
                        r,
                        journal.OperationId,
                        journal.TargetManagedRelativePath!))
                {
                    return false;
                }

                SkinInfo? record = r.Find<SkinInfo>(journal.OperationId);

                if (record == null)
                    return true;

                bool exact = publication != null
                    ? publication.IsExactRecord(record)
                    : isExactDurableRecord(journal, record, requireDurableFingerprint);

                if (!exact)
                    return false;

                r.Remove(record);
                return true;
            });

        private static bool isExactDurableRecord(
            SkinManagedFolderMutationJournal journal,
            SkinInfo record,
            bool requireDurableFingerprint)
        {
            string? fingerprint = journal.NewRecordPublicationFingerprint;

            if (requireDurableFingerprint
                && !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(fingerprint))
            {
                return false;
            }

            return SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(fingerprint)
                   && record.IsManaged
                   && record.ID == journal.OperationId
                   && record.Files.Count == 0
                   && string.Equals(
                       record.FilesystemStoragePath,
                       journal.TargetManagedRelativePath,
                       StringComparison.Ordinal)
                   && !record.IsExternalFilesystemStorage
                   && string.Equals(
                       record.FilesystemStorageAuthorityOwner,
                       SkinManagedFolderScanner.AUTHORITY_OWNER,
                       StringComparison.Ordinal)
                   && !record.Protected
                   && !record.DeletePending
                   && SkinManagedFolderFactory.IsInstantiationInfoAllowed(
                       record.InstantiationInfo)
                   && string.Equals(
                       SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(record),
                       fingerprint,
                       StringComparison.Ordinal);
        }

        private static bool matchesDurableFingerprintIfPresent(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderNewRecordPublicationData publication)
            => journal.NewRecordPublicationFingerprint == null
               || string.Equals(
                   journal.NewRecordPublicationFingerprint,
                   publication.Fingerprint,
                   StringComparison.Ordinal);

        private static bool hasUnresolvedExternalDeclaration(Realm realm)
            => realm.All<SkinInfo>().AsEnumerable().Any(
                candidate => candidate.IsExternalFilesystemStorage);

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

        private static bool tryValidateJournal(
            SkinManagedFolderMutationJournal journal)
            => journal != null
               && journal.IsValid()
               && journal.Version == SkinManagedFolderMutationJournal.CURRENT_VERSION
               && journal.Kind == SkinManagedFolderMutationKind.StagedImport
               && journal.RecordId == journal.OperationId
               && journal.TargetManagedRelativePath != null
               && journal.StagedSourceIdentity is { IsUsable: true }
               && journal.StagedRootIdentity is { IsUsable: true };

        private static SkinManagedFolderMutationRecoveryInspection inspection(
            SkinManagedFolderMutationRecoveryDecision decision,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            SkinManagedFolderPhysicalIdentity? targetIdentity = null,
            string? fingerprint = null)
            => new SkinManagedFolderMutationRecoveryInspection(
                decision,
                managedRootIdentity,
                targetIdentity,
                fingerprint);

        private static SkinManagedFolderMutationRecoveryInspection ambiguousInspection()
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);

        private static SkinManagedFolderMutationRecoveryActionResult failedAction()
            => new SkinManagedFolderMutationRecoveryActionResult(false);

        private enum RealmState
        {
            Absent,
            Exact,
            Conflict,
        }
    }
}
