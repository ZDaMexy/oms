// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    internal class SkinManagedFolderStagedImportOperationTest : RealmTest
    {
        private const string target_path = "chartskin/storage-slot";
        private static readonly string tree_fingerprint = new string('a', 64);

        private static readonly SkinManagedFolderPhysicalIdentity root_identity =
            new SkinManagedFolderPhysicalIdentity(301, 302, 303);

        private static readonly SkinManagedFolderPhysicalIdentity staging_root_identity =
            new SkinManagedFolderPhysicalIdentity(301, 304, 305);

        private static readonly SkinManagedFolderPhysicalIdentity source_identity =
            new SkinManagedFolderPhysicalIdentity(301, 306, 307);

        [Test]
        public void TestSuccessfulImportClosesJournalAndPublishesExactFinalCapsuleRecord()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var store = emptyStore();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation =
                    new SkinManagedFolderStagedImportOperation(realm, authority);

                SkinManagedFolderStagedImportOperationResult result =
                    operation.Execute(operationId, "storage-slot");
                realm.Realm.Refresh();
                SkinInfo? record = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus.Succeeded));
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(native.MoveCalls, Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(
                        store.Writes[1].TargetIdentity,
                        Is.EqualTo(source_identity));
                    Assert.That(
                        SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                            store.Writes[1].NewRecordPublicationFingerprint),
                        Is.True);
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(record, Is.Not.Null);
                    Assert.That(record!.ID, Is.EqualTo(operationId));
                    Assert.That(record.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(record.Name, Is.EqualTo("Capsule display name"));
                    Assert.That(record.Creator, Is.EqualTo("Capsule creator"));
                    Assert.That(record.Hash, Is.EqualTo(native.ContentRevision));
                    Assert.That(
                        record.InstantiationInfo,
                        Is.EqualTo(
                            SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO));
                    Assert.That(record.Files.Count, Is.Zero);
                    Assert.That(record.IsExternalFilesystemStorage, Is.False);
                    Assert.That(record.Protected, Is.False);
                    Assert.That(record.DeletePending, Is.False);
                    Assert.That(
                        record.FilesystemStorageAuthorityOwner,
                        Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Count(candidate => candidate.ID == operationId),
                        Is.EqualTo(1));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });

                string diagnostic = result.ToString();
                Assert.Multiple(() =>
                {
                    Assert.That(diagnostic, Does.Not.Contain(target_path));
                    Assert.That(
                        diagnostic,
                        Does.Not.Contain(operationId.ToString()));
                    Assert.That(
                        diagnostic,
                        Does.Not.Contain(
                            source_identity.VolumeSerialNumber.ToString()));
                    Assert.That(
                        native.LastExpectedRevision,
                        Is.EqualTo(native.ContentRevision));
                });
            });
        }

        [Test]
        public void TestLivePublishRejectsFinalTreeFingerprintDriftAndFreezesIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var store = emptyStore();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority
                {
                    TargetInspectionTreeFingerprint = new string('c', 64),
                };
                var operation = createOperation(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult result =
                    operation.Execute(operationId, "storage-slot");
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus
                                .RealmOutcomeUncertain));
                    Assert.That(native.MoveCalls, Is.EqualTo(1));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(
                            SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestCancellationBeforeMoveCleansOnlyExactProvisionalAndRollsBack()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var store = emptyStore();
                var native = new FakeStagedNativeAuthority
                {
                    CancelBeforeVisibleMove = true,
                };
                var operation = createOperation(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult result =
                    operation.Execute(operationId, "storage-slot");

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus.Cancelled));
                    Assert.That(native.MoveCalls, Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.Neither));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                });
            });
        }

        [Test]
        public void TestCancellationAfterMoveCannotAbortTargetOrPublication()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var cancellation = new CancellationTokenSource();
                var store = emptyStore();
                var native = new FakeStagedNativeAuthority
                {
                    AfterVisibleMove = cancellation.Cancel,
                };
                var operation = createOperation(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult result =
                    operation.Execute(
                        operationId,
                        "storage-slot",
                        cancellation.Token);

                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus.Succeeded));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Not.Null);
                });
            });
        }

        [Test]
        public void TestExactPreparedReceiptIsRequiredImmediatelyBeforeStagedMove()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var store = emptyStore();
                var native = new FakeStagedNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    store);
                SkinManagedFolderMutationAuthorityResult opened =
                    authority.OpenStagedImport(operationId, "storage-slot");

                using SkinManagedFolderMutationAuthoritySession session =
                    opened.Session!;
                SkinManagedFolderDurableMutationReceipt receipt =
                    session.PersistPreparedJournal();
                SkinManagedFolderMutationJournal prepared =
                    store.Current.Journal!;
                store.SetCurrent(
                    new SkinManagedFolderMutationJournalLoadResult(
                        SkinManagedFolderMutationJournalLoadStatus.Loaded,
                        prepared.WithRolledBack()));

                Assert.Throws<InvalidOperationException>(
                    () => session.ApplyCapturedStagedImportWithDurableReceipt(
                        receipt));
                Assert.Multiple(() =>
                {
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.SourceOnly));
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(SkinManagedFolderMutationPhase.RolledBack));
                });
            });
        }

        [TestCase(
            SkinManagedFolderMutationPhase.FilesystemApplied,
            SkinManagedFolderStagedImportOperationStatus.FilesystemOutcomeUncertain,
            SkinManagedFolderMutationPhase.Prepared,
            false)]
        [TestCase(
            SkinManagedFolderMutationPhase.RealmApplied,
            SkinManagedFolderStagedImportOperationStatus.RealmOutcomeUncertain,
            SkinManagedFolderMutationPhase.FilesystemApplied,
            true)]
        [TestCase(
            SkinManagedFolderMutationPhase.Committed,
            SkinManagedFolderStagedImportOperationStatus.CommitOutcomeUncertain,
            SkinManagedFolderMutationPhase.RealmApplied,
            true)]
        public void TestEveryPostVisiblePhaseWriteFaultRetainsDurableIntentAndRecovers(
            SkinManagedFolderMutationPhase faultPhase,
            SkinManagedFolderStagedImportOperationStatus expectedOperationStatus,
            SkinManagedFolderMutationPhase expectedDurablePhase,
            bool recordPublishedBeforeFault)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = new PhaseFaultMutationJournalStore(faultPhase);
                var native = new FakeStagedNativeAuthority();
                var operation = createOperation(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult operationResult =
                    operation.Execute(operationId, "storage-slot");
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        operationResult.Status,
                        Is.EqualTo(expectedOperationStatus));
                    Assert.That(native.MoveCalls, Is.EqualTo(1));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(expectedDurablePhase));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId) != null,
                        Is.EqualTo(recordPublishedBeforeFault));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });

                SkinManagedFolderMutationRecoveryResult recoveryResult =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();
                SkinInfo? recovered = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        recoveryResult.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(recovered, Is.Not.Null);
                    Assert.That(
                        createPublication(operationId, native.Metadata)
                            .IsExactRecord(recovered!),
                        Is.True);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestTerminalDeleteConfirmationFailureRetainsCommittedIntentAndRecovers()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                store.DeleteNoOpCallsRemaining = 1;
                var native = new FakeStagedNativeAuthority();
                var operation = createOperation(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult operationResult =
                    operation.Execute(operationId, "storage-slot");

                Assert.Multiple(() =>
                {
                    Assert.That(
                        operationResult.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus
                                .CommitOutcomeUncertain));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(SkinManagedFolderMutationPhase.Committed));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                    Assert.That(native.MoveCalls, Is.EqualTo(1));
                });

                SkinManagedFolderMutationRecoveryResult recoveryResult =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        recoveryResult.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RemovedTerminalJournal));
                    Assert.That(store.DeleteCalls, Is.EqualTo(2));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestFinalRealmConflictAfterMoveRetainsFilesystemAppliedIntentAndForeignRecord()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid foreignId = Guid.NewGuid();
                var store = emptyStore();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority
                {
                    AfterVisibleMove = () => addForeignRecord(
                        realm,
                        foreignId,
                        target_path),
                };
                var operation = createOperation(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);

                SkinManagedFolderStagedImportOperationResult result =
                    operation.Execute(operationId, "storage-slot");
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus
                                .RealmOutcomeUncertain));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(
                            SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(foreignId),
                        Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestTargetOnlyAbsentRecoveryRollsForwardExactlyOnce()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                };
                var recovery = createRecovery(
                    realm,
                    store,
                    coordinator,
                    native);

                SkinManagedFolderMutationRecoveryResult first =
                    recovery.Recover();
                SkinManagedFolderMutationRecoveryResult second =
                    recovery.Recover();
                realm.Realm.Refresh();
                SkinInfo? record = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        first.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredForward));
                    Assert.That(
                        second.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.NoJournal));
                    Assert.That(record, Is.Not.Null);
                    Assert.That(record!.ID, Is.EqualTo(operationId));
                    Assert.That(record.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Count(candidate => candidate.ID == operationId),
                        Is.EqualTo(1));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestPreparedTargetOnlyRecoveryFilesystemCheckpointFailureDoesNotPublish()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(prepared)
                {
                    ThrowOnWrite = true,
                };
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                };

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(
                        realm,
                        store,
                        coordinator,
                        native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .JournalIoFailure));
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(prepared),
                        Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                    Assert.That(
                        coordinator.IsPathFrozen("chartskin/unrelated"),
                        Is.True);
                });
            });
        }

        [Test]
        public void TestTargetOnlyRecoveryRealmCheckpointFailureRestartsWithoutDuplicateRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(prepared)
                {
                    ThrowOnceOnWritePhase =
                        SkinManagedFolderMutationPhase.RealmApplied,
                };
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                };
                var firstCoordinator =
                    new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult first =
                    createRecovery(
                        realm,
                        store,
                        firstCoordinator,
                        native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        first.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .JournalIoFailure));
                    Assert.That(
                        store.Current.Journal!.Phase,
                        Is.EqualTo(
                            SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Count(candidate => candidate.ID == operationId),
                        Is.EqualTo(1));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                        }));
                    Assert.That(
                        firstCoordinator.IsPathFrozen(
                            "chartskin/unrelated"),
                        Is.True);
                });

                var restartedCoordinator =
                    new SkinManagedFolderOperationCoordinator();
                SkinManagedFolderMutationRecoveryResult restarted =
                    createRecovery(
                        realm,
                        store,
                        restartedCoordinator,
                        native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        restarted.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredForward));
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Count(candidate => candidate.ID == operationId),
                        Is.EqualTo(1));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(restartedCoordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestTargetOnlyExactRecoveryRecognisesAlreadyCommittedWithoutRealmRewrite()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                };
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                SkinManagedFolderNewRecordPublicationData publication =
                    createPublication(operationId, native.Metadata);
                realm.Write(r => r.Add(publication.CreateRecord()));
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var handler = new SkinManagedFolderStagedImportRecoveryHandler(
                    realm,
                    native);

                SkinManagedFolderMutationRecoveryInspection inspection =
                    handler.Inspect(prepared, CancellationToken.None);
                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();
                SkinInfo? retained = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        inspection.Decision,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryDecision
                                .AlreadyCommitted));
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredForward));
                    Assert.That(retained, Is.Not.Null);
                    Assert.That(publication.IsExactRecord(retained!), Is.True);
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Count(candidate => candidate.ID == operationId),
                        Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestSourceOnlyExactRecordRecoveryDeletesOnlyPlanAndProvisional()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority();
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                SkinManagedFolderNewRecordPublicationData publication =
                    createPublication(operationId, native.Metadata);
                realm.Write(r => r.Add(publication.CreateRecord()));
                Guid unrelatedId = Guid.NewGuid();
                addForeignRecord(realm, unrelatedId, "chartskin/unrelated");
                var store = new MemoryMutationJournalStore(prepared);
                var recovery = createRecovery(
                    realm,
                    store,
                    new SkinManagedFolderOperationCoordinator(),
                    native);

                SkinManagedFolderMutationRecoveryResult result =
                    recovery.Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredRollback));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.Neither));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(unrelatedId),
                        Is.Not.Null);
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestSourceOnlyAbsentRecoveryRollsBackOnlyExactProvisional()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority();
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var handler = new SkinManagedFolderStagedImportRecoveryHandler(
                    realm,
                    native);

                SkinManagedFolderMutationRecoveryInspection inspection =
                    handler.Inspect(prepared, CancellationToken.None);
                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        inspection.Decision,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryDecision.RollBack));
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredRollback));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(
                        native.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.Neither));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestNeitherExactDurableRecordRecoveryRemovesRecordWithoutPhysicalDelete()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.Neither,
                };
                SkinManagedFolderNewRecordPublicationData publication =
                    createPublication(operationId, native.Metadata);
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId)
                        .WithFilesystemApplied(
                            source_identity,
                            publication.Fingerprint)
                        .WithRealmApplied();
                realm.Write(r => r.Add(publication.CreateRecord()));
                var store = new MemoryMutationJournalStore(journal);
                var recovery = createRecovery(
                    realm,
                    store,
                    new SkinManagedFolderOperationCoordinator(),
                    native);

                SkinManagedFolderMutationRecoveryResult result =
                    recovery.Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredRollback));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestNeitherAbsentRecoveryRecognisesAlreadyRolledBackWithoutCleanup()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.Neither,
                };
                SkinManagedFolderMutationJournal prepared =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var handler = new SkinManagedFolderStagedImportRecoveryHandler(
                    realm,
                    native);

                SkinManagedFolderMutationRecoveryInspection inspection =
                    handler.Inspect(prepared, CancellationToken.None);
                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        inspection.Decision,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryDecision
                                .AlreadyRolledBack));
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredRollback));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(
                        store.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderStagedImportInspectionStatus.Both)]
        [TestCase(SkinManagedFolderStagedImportInspectionStatus.IdentityMismatch)]
        [TestCase(SkinManagedFolderStagedImportInspectionStatus.RootIdentityMismatch)]
        public void TestAmbiguousPhysicalRecoveryRetainsJournalAndFreezesTarget(
            SkinManagedFolderStagedImportInspectionStatus status)
        {
            RunTestWithRealm((realm, _) =>
            {
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(Guid.NewGuid());
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeStagedNativeAuthority { Status = status };
                var recovery = createRecovery(
                    realm,
                    store,
                    coordinator,
                    native);

                SkinManagedFolderMutationRecoveryResult result =
                    recovery.Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(journal),
                        Is.True);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestForeignRealmDriftNeverGetsDeletedDuringRollback()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority();
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId);
                addForeignRecord(realm, operationId, target_path);
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var recovery = createRecovery(
                    realm,
                    store,
                    coordinator,
                    native);

                SkinManagedFolderMutationRecoveryResult result =
                    recovery.Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Not.Null);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [TestCase(RecoveryRealmConflictCase.SameId)]
        [TestCase(RecoveryRealmConflictCase.SamePath)]
        [TestCase(RecoveryRealmConflictCase.Name)]
        [TestCase(RecoveryRealmConflictCase.Creator)]
        [TestCase(RecoveryRealmConflictCase.Hash)]
        [TestCase(RecoveryRealmConflictCase.Owner)]
        public void TestRecoveryRealmConflictMatrixRemainsAmbiguousAndUntouched(
            RecoveryRealmConflictCase conflictCase)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid retainedId;
                var native = new FakeStagedNativeAuthority();
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId);
                SkinManagedFolderNewRecordPublicationData publication =
                    createPublication(operationId, native.Metadata);

                if (conflictCase == RecoveryRealmConflictCase.SameId)
                {
                    retainedId = operationId;
                    addForeignRecord(
                        realm,
                        retainedId,
                        "chartskin/foreign-same-id");
                }
                else if (conflictCase == RecoveryRealmConflictCase.SamePath)
                {
                    retainedId = Guid.NewGuid();
                    addForeignRecord(realm, retainedId, target_path);
                }
                else
                {
                    SkinInfo drifted = publication.CreateRecord();
                    retainedId = drifted.ID;

                    switch (conflictCase)
                    {
                        case RecoveryRealmConflictCase.Name:
                            drifted.Name = "Foreign name";
                            break;

                        case RecoveryRealmConflictCase.Creator:
                            drifted.Creator = "Foreign creator";
                            break;

                        case RecoveryRealmConflictCase.Hash:
                            drifted.Hash = "foreign-revision";
                            break;

                        case RecoveryRealmConflictCase.Owner:
                            drifted.FilesystemStorageAuthorityOwner =
                                "foreign-owner";
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(conflictCase),
                                conflictCase,
                                null);
                    }

                    realm.Write(r => r.Add(drifted));
                }

                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(retainedId),
                        Is.Not.Null);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(journal),
                        Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestRecoveryRejectsDurablePublicationFingerprintDriftWithoutDeletingRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                };
                SkinManagedFolderNewRecordPublicationData exactPublication =
                    createPublication(operationId, native.Metadata);
                SkinManagedFolderNewRecordPublicationData foreignPublication =
                    createPublication(
                        operationId,
                        new SkinManagedFolderPackageMetadata(
                            "Foreign fingerprint name",
                            native.Metadata.Creator,
                            native.Metadata.ContentRevision));
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId)
                        .WithFilesystemApplied(
                            source_identity,
                            foreignPublication.Fingerprint)
                        .WithRealmApplied();
                realm.Write(r => r.Add(exactPublication.CreateRecord()));
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();
                SkinInfo? retained = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(retained, Is.Not.Null);
                    Assert.That(
                        exactPublication.IsExactRecord(retained!),
                        Is.True);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(journal),
                        Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestRecoveryRejectsTargetMetadataDriftWithoutDeletingRealmRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                    TargetInspectionMetadata =
                        new SkinManagedFolderPackageMetadata(
                            "Changed target name",
                            "Changed target creator",
                            "changed-target-revision"),
                };
                SkinManagedFolderNewRecordPublicationData durablePublication =
                    createPublication(operationId, native.Metadata);
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId)
                        .WithFilesystemApplied(
                            source_identity,
                            durablePublication.Fingerprint)
                        .WithRealmApplied();
                realm.Write(r => r.Add(durablePublication.CreateRecord()));
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();
                SkinInfo? retained = realm.Realm.Find<SkinInfo>(operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(retained, Is.Not.Null);
                    Assert.That(
                        durablePublication.IsExactRecord(retained!),
                        Is.True);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(journal),
                        Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestPreparedTargetOnlyRecoveryRejectsPhysicalTreeFingerprintDrift()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeStagedNativeAuthority
                {
                    Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly,
                    TargetInspectionTreeFingerprint = new string('d', 64),
                };
                SkinManagedFolderMutationJournal journal =
                    createPreparedJournal(operationId);
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();
                realm.Realm.Refresh();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        result.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(operationId),
                        Is.Null);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(
                        store.Current.Journal!.IsExactSameJournal(journal),
                        Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        private static SkinManagedFolderStagedImportOperation createOperation(
            RealmAccess realm,
            Framework.Platform.Storage storage,
            SkinManagedFolderOperationCoordinator coordinator,
            FakeStagedNativeAuthority native,
            ISkinManagedFolderMutationJournalStore store)
            => new SkinManagedFolderStagedImportOperation(
                realm,
                new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store));

        private static SkinManagedFolderMutationRecovery createRecovery(
            RealmAccess realm,
            ISkinManagedFolderMutationJournalStore store,
            SkinManagedFolderOperationCoordinator coordinator,
            FakeStagedNativeAuthority native)
            => new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                new SkinManagedFolderMutationRecoveryHandlerRouter(
                    (
                        SkinManagedFolderMutationKind.StagedImport,
                        new SkinManagedFolderStagedImportRecoveryHandler(
                            realm,
                            native))));

        private static SkinManagedFolderMutationJournal createPreparedJournal(
            Guid operationId)
        {
            using SkinPackageRevisionCapsule capsule = createCapsule();
            return SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                operationId,
                root_identity,
                target_path,
                source_identity,
                staging_root_identity,
                capsule.ContentRevision,
                tree_fingerprint);
        }

        private static SkinManagedFolderNewRecordPublicationData createPublication(
            Guid operationId,
            SkinManagedFolderPackageMetadata metadata)
            => new SkinManagedFolderNewRecordPublicationPlan(
                operationId,
                target_path,
                root_identity).CreatePublicationData(metadata);

        private static MemoryMutationJournalStore emptyStore()
            => new MemoryMutationJournalStore(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing));

        private static void addForeignRecord(
            RealmAccess realm,
            Guid id,
            string path)
            => realm.Write(r => r.Add(new SkinInfo(
                "Foreign",
                "Foreign",
                SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = id,
                Hash = "foreign-revision",
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = false,
                FilesystemStorageAuthorityOwner = "foreign-owner",
                Protected = false,
                DeletePending = false,
            }));

        private static SkinPackageRevisionCapsule createCapsule()
        {
            byte[] skinIni = Encoding.UTF8.GetBytes(
                "[General]\n"
                + "Name: Capsule display name\n"
                + "Author: Capsule creator\n");
            SkinPackageRevisionCapsuleCreationResult result =
                SkinPackageRevisionCapsuleFactory.Create(new[]
                {
                    SkinPackageCapturedEntry.CreateFile("skin.ini", skinIni),
                    SkinPackageCapturedEntry.CreateFile(
                        "notes/note.png",
                        new byte[] { 1, 2, 3, 4 }),
                });

            if (!result.IsSuccess)
                throw new InvalidOperationException();

            return result.Capsule!;
        }

        public enum RecoveryRealmConflictCase
        {
            SameId,
            SamePath,
            Name,
            Creator,
            Hash,
            Owner,
        }

        private sealed class PhaseFaultMutationJournalStore
            : ISkinManagedFolderMutationJournalStore
        {
            private readonly SkinManagedFolderMutationPhase faultPhase;
            private bool faultInjected;

            public SkinManagedFolderMutationJournalLoadResult Current
            {
                get;
                private set;
            } = new SkinManagedFolderMutationJournalLoadResult(
                SkinManagedFolderMutationJournalLoadStatus.Missing);

            public List<SkinManagedFolderMutationJournal> Writes { get; } =
                new List<SkinManagedFolderMutationJournal>();

            public PhaseFaultMutationJournalStore(
                SkinManagedFolderMutationPhase faultPhase)
            {
                this.faultPhase = faultPhase;
            }

            public SkinManagedFolderMutationJournalLoadResult Load() => Current;

            public void Write(SkinManagedFolderMutationJournal journal)
            {
                if (journal.Phase == faultPhase && !faultInjected)
                {
                    faultInjected = true;
                    throw new IOException("Injected phase write failure.");
                }

                Writes.Add(journal);
                Current = new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Loaded,
                    journal);
            }

            public void Delete(
                SkinManagedFolderMutationJournal expectedJournal)
            {
                if (!Current.IsLoaded
                    || !Current.Journal!.IsExactSameJournal(expectedJournal)
                    || expectedJournal.Phase is not (
                        SkinManagedFolderMutationPhase.Committed
                        or SkinManagedFolderMutationPhase.RolledBack))
                {
                    throw new InvalidOperationException();
                }

                Current = new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing);
            }
        }

        private sealed class FakeStagedNativeAuthority
            : ISkinManagedFolderMutationNativeAuthority
        {
            public SkinManagedFolderStagedImportInspectionStatus Status { get; set; } =
                SkinManagedFolderStagedImportInspectionStatus.SourceOnly;

            public bool CancelBeforeVisibleMove { get; init; }

            public Action? AfterVisibleMove { get; init; }

            public int MoveCalls { get; private set; }

            public int CleanupCalls { get; private set; }

            public string? LastExpectedRevision { get; private set; }

            public string ContentRevision { get; }

            public SkinManagedFolderPackageMetadata Metadata { get; }

            public SkinManagedFolderPackageMetadata? TargetInspectionMetadata
            {
                get;
                init;
            }

            public string TargetInspectionTreeFingerprint { get; init; } =
                tree_fingerprint;

            public FakeStagedNativeAuthority()
            {
                using SkinPackageRevisionCapsule capsule = createCapsule();
                ContentRevision = capsule.ContentRevision;
                Metadata = new SkinManagedFolderPackageMetadata(
                    "Capsule display name",
                    "Capsule creator",
                    ContentRevision);
            }

            public ISkinManagedFolderMutationNativeSession Open(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new Session(this);
            }

            private sealed class Session
                : ISkinManagedFolderMutationNativeSession
            {
                private readonly FakeStagedNativeAuthority owner;

                public SkinManagedFolderPhysicalIdentity ManagedRootIdentity
                    => root_identity;

                public Session(FakeStagedNativeAuthority owner)
                {
                    this.owner = owner;
                }

                public SkinManagedFolderPhysicalIdentity CaptureExistingSource(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public SkinManagedFolderStagedSourceCapture CaptureStagedSource(
                    Guid operationId,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (operationId == Guid.Empty
                        || owner.Status
                        != SkinManagedFolderStagedImportInspectionStatus.SourceOnly)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    return new SkinManagedFolderStagedSourceCapture(
                        staging_root_identity,
                        source_identity,
                        tree_fingerprint,
                        createCapsule());
                }

                public SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.Equals(
                            managedRelativePath,
                            target_path,
                            StringComparison.Ordinal)
                        || owner.Status
                        is SkinManagedFolderStagedImportInspectionStatus.TargetOnly
                            or SkinManagedFolderStagedImportInspectionStatus.Both)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    return new SkinManagedFolderTargetNameSlot(
                        managedRelativePath,
                        root_identity);
                }

                public SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public SkinManagedFolderStagedImportFilesystemResult
                    MoveCapturedStagedSourceToTarget(
                        SkinManagedFolderTargetNameSlot targetNameSlot,
                        string expectedContentRevision,
                        string expectedTreeFingerprint,
                        CancellationToken cancellationToken)
                {
                    owner.MoveCalls++;

                    if (owner.CancelBeforeVisibleMove)
                        throw new OperationCanceledException(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status
                        != SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                        || targetNameSlot.ManagedRootIdentity != root_identity
                        || !string.Equals(
                            targetNameSlot.ManagedRelativePath,
                            target_path,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            expectedContentRevision,
                            owner.ContentRevision,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            expectedTreeFingerprint,
                            tree_fingerprint,
                            StringComparison.Ordinal))
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    owner.LastExpectedRevision = expectedContentRevision;
                    owner.Status =
                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly;
                    owner.AfterVisibleMove?.Invoke();
                    return new SkinManagedFolderStagedImportFilesystemResult(
                        source_identity,
                        tree_fingerprint,
                        createCapsule());
                }

                public SkinManagedFolderRenameInspection InspectRenameState(
                    string sourceManagedRelativePath,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public SkinManagedFolderStagedImportInspection
                    InspectStagedImportState(
                        Guid operationId,
                        string targetManagedRelativePath,
                        SkinManagedFolderPhysicalIdentity
                            expectedStagedRootIdentity,
                        SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                        CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (operationId == Guid.Empty
                        || !string.Equals(
                            targetManagedRelativePath,
                            target_path,
                            StringComparison.Ordinal)
                        || expectedStagedRootIdentity != staging_root_identity
                        || expectedSourceIdentity != source_identity)
                    {
                        return new SkinManagedFolderStagedImportInspection(
                            SkinManagedFolderStagedImportInspectionStatus
                                .IdentityMismatch,
                            root_identity);
                    }

                    return owner.Status switch
                    {
                        SkinManagedFolderStagedImportInspectionStatus.SourceOnly =>
                            new SkinManagedFolderStagedImportInspection(
                                owner.Status,
                                root_identity,
                                packageMetadata: owner.Metadata,
                                treeFingerprint: tree_fingerprint),

                        SkinManagedFolderStagedImportInspectionStatus.TargetOnly =>
                            new SkinManagedFolderStagedImportInspection(
                                owner.Status,
                                root_identity,
                                source_identity,
                                owner.TargetInspectionMetadata ?? owner.Metadata,
                                owner.TargetInspectionTreeFingerprint),

                        _ => new SkinManagedFolderStagedImportInspection(
                            owner.Status,
                            root_identity),
                    };
                }

                public void CleanupExactStagedSource(
                    Guid operationId,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status
                        != SkinManagedFolderStagedImportInspectionStatus.SourceOnly
                        || operationId == Guid.Empty
                        || !string.Equals(
                            targetManagedRelativePath,
                            target_path,
                            StringComparison.Ordinal)
                        || expectedStagedRootIdentity != staging_root_identity
                        || expectedSourceIdentity != source_identity)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    owner.CleanupCalls++;
                    owner.Status =
                        SkinManagedFolderStagedImportInspectionStatus.Neither;
                }

                public void ValidateCompleteAndStable(
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status
                        is SkinManagedFolderStagedImportInspectionStatus
                            .IdentityMismatch
                            or SkinManagedFolderStagedImportInspectionStatus
                                .RootIdentityMismatch)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
