// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Database;
using Realms;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Recovery policy for the single v3 external-to-managed copy intent.
    /// </summary>
    internal sealed class SkinManagedFolderManagedCopyRecoveryHandler
        : ISkinManagedFolderMutationRecoveryHandler,
          ISkinManagedFolderMutationHeldRecoveryHandler
    {
        private readonly RealmAccess realm;

        public SkinManagedFolderManagedCopyRecoveryHandler(
            RealmAccess realm,
            ISkinManagedFolderMutationNativeAuthority nativeAuthority)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            ArgumentNullException.ThrowIfNull(nativeAuthority);
        }

        public bool CanHandle(SkinManagedFolderMutationKind kind)
            => kind == SkinManagedFolderMutationKind.ManagedCopy;

        public SkinManagedFolderMutationRecoveryInspection Inspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => ambiguousInspection();

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => failedAction();

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => failedAction();

        public SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryGetManifest(journal, out SkinManagedCopyLogicalManifest? manifest)
                || authority == null
                || !authority.Validate(cancellationToken)
                || authority.NativeSession.ManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return ambiguousInspection();
            }

            return inspect(journal, manifest!, authority, cancellationToken);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryGetManifest(journal, out SkinManagedCopyLogicalManifest? manifest)
                || authority == null
                || !authority.Validate(cancellationToken)
                || authority.NativeSession.ManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return failedAction();
            }

            return journal.Phase switch
            {
                SkinManagedFolderMutationPhase.ProvisionalReady =>
                    moveProvisionalToTarget(journal, manifest!, authority, cancellationToken),

                SkinManagedFolderMutationPhase.FilesystemApplied =>
                    publishRealmRecord(journal, authority, cancellationToken),

                SkinManagedFolderMutationPhase.RealmApplied =>
                    confirmCommitted(journal, authority, cancellationToken),

                _ => failedAction(),
            };
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (!tryGetManifest(journal, out SkinManagedCopyLogicalManifest? manifest)
                || authority == null
                || !authority.Validate(cancellationToken)
                || authority.NativeSession.ManagedRootIdentity != journal.ManagedRootIdentity
                || inspectRealmState(journal, null, authority) != RealmState.PublishedAbsent)
            {
                return failedAction();
            }

            ISkinManagedFolderMutationNativeSession native = authority.NativeSession;
            SkinManagedCopyProvisionalInspection physical = native.InspectManagedCopyProvisionalState(
                journal.OperationId,
                journal.TargetManagedRelativePath!,
                journal.StagedRootIdentity!.Value,
                journal.StagedSourceIdentity,
                manifest!,
                journal.StagedSourceContentRevision!,
                cancellationToken);

            if (journal.Phase == SkinManagedFolderMutationPhase.Prepared)
            {
                if (physical.Status != SkinManagedCopyProvisionalInspectionStatus.Absent)
                    return failedAction();
            }
            else if (journal.Phase == SkinManagedFolderMutationPhase.Copying
                     && journal.StagedSourceIdentity is { } provisionalIdentity)
            {
                if (physical.Status != SkinManagedCopyProvisionalInspectionStatus.Empty)
                    return failedAction();

                native.CleanupExactManagedCopyProvisional(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.StagedRootIdentity.Value,
                    provisionalIdentity,
                    manifest!,
                    journal.StagedSourceContentRevision!,
                    cancellationToken);
            }
            else
                return failedAction();

            return authority.Validate(cancellationToken)
                   && inspectRealmState(journal, null, authority) == RealmState.PublishedAbsent
                ? new SkinManagedFolderMutationRecoveryActionResult(
                    true,
                    native.ManagedRootIdentity)
                : failedAction();
        }

        private SkinManagedFolderMutationRecoveryInspection inspect(
            SkinManagedFolderMutationJournal journal,
            SkinManagedCopyLogicalManifest manifest,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            if (journal.Phase is SkinManagedFolderMutationPhase.Prepared
                or SkinManagedFolderMutationPhase.Copying)
            {
                RealmState earlyRealmState = inspectRealmState(journal, null, authority);

                if (earlyRealmState != RealmState.PublishedAbsent)
                    return ambiguousInspection();

                SkinManagedCopyProvisionalInspection provisional =
                    authority.NativeSession.InspectManagedCopyProvisionalState(
                        journal.OperationId,
                        journal.TargetManagedRelativePath!,
                        journal.StagedRootIdentity!.Value,
                        journal.StagedSourceIdentity,
                        manifest,
                        journal.StagedSourceContentRevision!,
                        cancellationToken);

                if (!authority.Validate(cancellationToken))
                    return ambiguousInspection();

                if (journal.Phase == SkinManagedFolderMutationPhase.Prepared)
                {
                    return provisional.Status == SkinManagedCopyProvisionalInspectionStatus.Absent
                        ? inspection(SkinManagedFolderMutationRecoveryDecision.RollBack, journal)
                        : ambiguousInspection();
                }

                if (provisional.Status == SkinManagedCopyProvisionalInspectionStatus.Empty)
                    return inspection(SkinManagedFolderMutationRecoveryDecision.RollBack, journal);

                SkinManagedFolderNewRecordPublicationData? provisionalPublication =
                    tryCreatePublication(journal, provisional.PackageMetadata);

                return provisional.Status == SkinManagedCopyProvisionalInspectionStatus.Complete
                       && provisional.ProvisionalIdentity == journal.StagedSourceIdentity
                       && provisionalPublication != null
                       && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                           provisional.TreeFingerprint)
                    ? new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.RollForward,
                        journal.ManagedRootIdentity,
                        journal.StagedSourceIdentity,
                        provisionalPublication.Fingerprint,
                        provisional.TreeFingerprint)
                    : ambiguousInspection();
            }

            SkinManagedFolderStagedImportInspection physical = inspectPublishedPhysicalState(
                journal,
                authority,
                cancellationToken);
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(journal, physical.PackageMetadata);

            if (!isExactPublishedPhysicalState(journal, physical, publication)
                || !authority.Validate(cancellationToken))
            {
                return ambiguousInspection();
            }

            RealmState realmState = inspectRealmState(journal, publication, authority);

            return journal.Phase switch
            {
                SkinManagedFolderMutationPhase.ProvisionalReady
                    when realmState == RealmState.PublishedAbsent
                         && physical.Status is (SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                             or SkinManagedFolderStagedImportInspectionStatus.TargetOnly) =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.RollForward,
                        journal.ManagedRootIdentity,
                        journal.StagedSourceIdentity,
                        publication!.Fingerprint,
                        physical.TreeFingerprint),

                SkinManagedFolderMutationPhase.FilesystemApplied
                    when physical.Status == SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                         && realmState == RealmState.PublishedAbsent =>
                    new SkinManagedFolderMutationRecoveryInspection(
                        SkinManagedFolderMutationRecoveryDecision.RollForward,
                        journal.ManagedRootIdentity,
                        journal.StagedSourceIdentity,
                        publication!.Fingerprint,
                        physical.TreeFingerprint),

                SkinManagedFolderMutationPhase.FilesystemApplied
                    when physical.Status == SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                         && realmState == RealmState.PublishedExact =>
                    committedInspection(journal, physical),

                SkinManagedFolderMutationPhase.RealmApplied
                    when physical.Status == SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                         && realmState == RealmState.PublishedExact =>
                    committedInspection(journal, physical),

                _ => ambiguousInspection(),
            };
        }

        private SkinManagedFolderMutationRecoveryActionResult moveProvisionalToTarget(
            SkinManagedFolderMutationJournal journal,
            SkinManagedCopyLogicalManifest manifest,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            ISkinManagedFolderMutationNativeSession native = authority.NativeSession;
            SkinManagedFolderStagedImportInspection before = inspectPublishedPhysicalState(
                journal,
                authority,
                cancellationToken);
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(journal, before.PackageMetadata);

            if (!isExactPublishedPhysicalState(journal, before, publication)
                || inspectRealmState(journal, publication, authority) != RealmState.PublishedAbsent)
            {
                return failedAction();
            }

            if (before.Status == SkinManagedFolderStagedImportInspectionStatus.SourceOnly)
            {
                SkinManagedCopyProvisionalInspection held = native.InspectManagedCopyProvisionalState(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.StagedRootIdentity!.Value,
                    journal.StagedSourceIdentity,
                    manifest,
                    journal.StagedSourceContentRevision!,
                    cancellationToken);

                if (held.Status != SkinManagedCopyProvisionalInspectionStatus.Complete
                    || held.ProvisionalIdentity != journal.StagedSourceIdentity
                    || !string.Equals(held.TreeFingerprint, journal.StagedSourceTreeFingerprint, StringComparison.Ordinal))
                {
                    return failedAction();
                }

                SkinManagedFolderTargetNameSlot target = native.CaptureAbsentTargetNameSlot(
                    journal.TargetManagedRelativePath!,
                    cancellationToken);

                using SkinManagedFolderStagedImportFilesystemResult moved =
                    native.MoveCapturedStagedSourceToTarget(
                        target,
                        journal.StagedSourceContentRevision!,
                        journal.StagedSourceTreeFingerprint!,
                        cancellationToken);

                if (moved.TargetIdentity != journal.StagedSourceIdentity
                    || !string.Equals(moved.TreeFingerprint, journal.StagedSourceTreeFingerprint, StringComparison.Ordinal)
                    || !string.Equals(moved.Capsule.ContentRevision, journal.StagedSourceContentRevision, StringComparison.Ordinal)
                    || !SkinManagedFolderPackageMetadataReader.TryRead(
                        moved.Capsule,
                        out SkinManagedFolderPackageMetadata? movedMetadata))
                {
                    return failedAction();
                }

                publication = tryCreatePublication(journal, movedMetadata);
            }

            if (publication == null
                || !string.Equals(
                    publication.Fingerprint,
                    journal.NewRecordPublicationFingerprint,
                    StringComparison.Ordinal)
                || !authority.Validate(CancellationToken.None))
            {
                return failedAction();
            }

            SkinManagedFolderStagedImportInspection after = inspectPublishedPhysicalState(
                journal,
                authority,
                CancellationToken.None);

            return isExactPublishedPhysicalState(journal, after, publication)
                   && after.Status == SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                   && inspectRealmState(journal, publication, authority) == RealmState.PublishedAbsent
                   && authority.Validate(CancellationToken.None)
                ? successfulAction(journal)
                : failedAction();
        }

        private SkinManagedFolderMutationRecoveryActionResult publishRealmRecord(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderStagedImportInspection physical = inspectPublishedPhysicalState(
                journal,
                authority,
                cancellationToken);
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(journal, physical.PackageMetadata);

            if (!isExactPublishedPhysicalState(journal, physical, publication)
                || physical.Status != SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                || !authority.Validate(cancellationToken))
            {
                return failedAction();
            }

            RealmState before = inspectRealmState(journal, publication, authority);

            if (before == RealmState.PublishedAbsent)
            {
                bool added = realm.Write(r =>
                {
                    r.Refresh();

                    if (!authority.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())
                        || !hasExactExternalRecord(r, journal)
                        || hasTargetPathConflict(r, journal)
                        || r.Find<SkinInfo>(journal.OperationId) != null)
                    {
                        return false;
                    }

                    SkinInfo record = publication!.CreateRecord();
                    r.Add(record);
                    return publication.IsExactRecord(record);
                });

                if (!added)
                    return failedAction();
            }
            else if (before != RealmState.PublishedExact)
                return failedAction();

            return authority.Validate(cancellationToken)
                   && inspectRealmState(journal, publication, authority) == RealmState.PublishedExact
                ? successfulAction(journal)
                : failedAction();
        }

        private SkinManagedFolderMutationRecoveryActionResult confirmCommitted(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderStagedImportInspection physical = inspectPublishedPhysicalState(
                journal,
                authority,
                cancellationToken);
            SkinManagedFolderNewRecordPublicationData? publication =
                tryCreatePublication(journal, physical.PackageMetadata);

            return isExactPublishedPhysicalState(journal, physical, publication)
                   && physical.Status == SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                   && inspectRealmState(journal, publication, authority) == RealmState.PublishedExact
                   && authority.Validate(cancellationToken)
                ? successfulAction(journal)
                : failedAction();
        }

        private SkinManagedFolderStagedImportInspection inspectPublishedPhysicalState(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderStagedImportInspection physical =
                authority.NativeSession.InspectStagedImportState(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.StagedRootIdentity!.Value,
                    journal.StagedSourceIdentity!.Value,
                    cancellationToken);

            if (journal.Phase == SkinManagedFolderMutationPhase.ProvisionalReady
                && physical.Status == SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                && tryGetManifest(journal, out SkinManagedCopyLogicalManifest? manifest))
            {
                SkinManagedCopyProvisionalInspection held =
                    authority.NativeSession.InspectManagedCopyProvisionalState(
                        journal.OperationId,
                        journal.TargetManagedRelativePath!,
                        journal.StagedRootIdentity.Value,
                        journal.StagedSourceIdentity,
                        manifest!,
                        journal.StagedSourceContentRevision!,
                        cancellationToken);

                if (held.Status != SkinManagedCopyProvisionalInspectionStatus.Complete
                    || held.ProvisionalIdentity != journal.StagedSourceIdentity
                    || !string.Equals(held.TreeFingerprint, physical.TreeFingerprint, StringComparison.Ordinal))
                {
                    return new SkinManagedFolderStagedImportInspection(
                        SkinManagedFolderStagedImportInspectionStatus.IdentityMismatch,
                        journal.ManagedRootIdentity);
                }
            }

            return physical;
        }

        private RealmState inspectRealmState(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderNewRecordPublicationData? publication,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority)
            => realm.Run(r =>
            {
                r.Refresh();

                if (!authority.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())
                    || !hasExactExternalRecord(r, journal)
                    || hasTargetPathConflict(r, journal))
                {
                    return RealmState.Conflict;
                }

                SkinInfo? published = r.Find<SkinInfo>(journal.OperationId);

                if (published == null)
                    return RealmState.PublishedAbsent;

                return publication != null && publication.IsExactRecord(published)
                    ? RealmState.PublishedExact
                    : RealmState.Conflict;
            });

        private static bool hasExactExternalRecord(Realm r, SkinManagedFolderMutationJournal journal)
        {
            SkinInfo? external = r.Find<SkinInfo>(journal.RecordId!.Value);
            return external?.IsExternalFilesystemStorage == true
                   && external.Files.Count == 0
                   && !external.Protected
                   && !external.DeletePending
                   && !SkinFilesystemStorageResolver.IsFixedSkinId(external.ID)
                   && string.Equals(
                       external.FilesystemStorageAuthorityOwner,
                       SkinExternalFolderRegistry.AUTHORITY_OWNER,
                       StringComparison.Ordinal)
                   && string.Equals(
                       SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(external),
                       journal.ManagedCopyExternalRecordFingerprint,
                       StringComparison.Ordinal);
        }

        private static bool hasTargetPathConflict(Realm r, SkinManagedFolderMutationJournal journal)
            => r.All<SkinInfo>().AsEnumerable().Any(candidate =>
                candidate.ID != journal.OperationId
                && SkinManagedFolderPath.TryNormalise(
                    candidate.FilesystemStoragePath,
                    out string candidatePath)
                && string.Equals(
                    candidatePath,
                    journal.TargetManagedRelativePath,
                    StringComparison.OrdinalIgnoreCase));

        private static bool isExactPublishedPhysicalState(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderStagedImportInspection physical,
            SkinManagedFolderNewRecordPublicationData? publication)
            => physical.ManagedRootIdentity == journal.ManagedRootIdentity
               && physical.Status is (SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                   or SkinManagedFolderStagedImportInspectionStatus.TargetOnly)
               && (physical.Status != SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                   || physical.TargetIdentity == journal.StagedSourceIdentity)
               && physical.PackageMetadata != null
               && string.Equals(
                   physical.PackageMetadata.ContentRevision,
                   journal.StagedSourceContentRevision,
                   StringComparison.Ordinal)
               && string.Equals(
                   physical.TreeFingerprint,
                   journal.StagedSourceTreeFingerprint,
                   StringComparison.Ordinal)
               && publication != null
               && string.Equals(
                   publication.Fingerprint,
                   journal.NewRecordPublicationFingerprint,
                   StringComparison.Ordinal);

        private static SkinManagedFolderNewRecordPublicationData? tryCreatePublication(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderPackageMetadata? metadata)
        {
            if (metadata == null)
                return null;

            try
            {
                return new SkinManagedFolderNewRecordPublicationPlan(
                    journal.OperationId,
                    journal.TargetManagedRelativePath!,
                    journal.ManagedRootIdentity).CreatePublicationData(metadata);
            }
            catch
            {
                return null;
            }
        }

        private static bool tryGetManifest(
            SkinManagedFolderMutationJournal journal,
            out SkinManagedCopyLogicalManifest? manifest)
        {
            manifest = null;
            return journal != null
                   && journal.Kind == SkinManagedFolderMutationKind.ManagedCopy
                   && journal.Version == SkinManagedFolderMutationJournal.CURRENT_VERSION
                   && journal.IsValid()
                   && SkinManagedCopyLogicalManifest.TryParse(
                       journal.ManagedCopyLogicalManifest!,
                       journal.ManagedCopyLogicalManifestDigest!,
                       out manifest);
        }

        private static SkinManagedFolderMutationRecoveryInspection committedInspection(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderStagedImportInspection physical)
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted,
                journal.ManagedRootIdentity,
                journal.StagedSourceIdentity,
                journal.NewRecordPublicationFingerprint,
                physical.TreeFingerprint);

        private static SkinManagedFolderMutationRecoveryInspection inspection(
            SkinManagedFolderMutationRecoveryDecision decision,
            SkinManagedFolderMutationJournal journal)
            => new SkinManagedFolderMutationRecoveryInspection(
                decision,
                journal.ManagedRootIdentity);

        private static SkinManagedFolderMutationRecoveryInspection ambiguousInspection()
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);

        private static SkinManagedFolderMutationRecoveryActionResult successfulAction(
            SkinManagedFolderMutationJournal journal)
            => new SkinManagedFolderMutationRecoveryActionResult(
                true,
                journal.ManagedRootIdentity,
                journal.StagedSourceIdentity,
                journal.NewRecordPublicationFingerprint);

        private static SkinManagedFolderMutationRecoveryActionResult failedAction()
            => new SkinManagedFolderMutationRecoveryActionResult(false);

        private enum RealmState
        {
            Conflict,
            PublishedAbsent,
            PublishedExact,
        }

        public override string ToString()
            => nameof(SkinManagedFolderManagedCopyRecoveryHandler);
    }
}
