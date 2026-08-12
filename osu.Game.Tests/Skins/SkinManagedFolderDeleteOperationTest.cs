// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    internal class SkinManagedFolderDeleteOperationTest : RealmTest
    {
        private const string source_path = "chartskin/source";

        private static readonly SkinManagedFolderPhysicalIdentity root_identity =
            new SkinManagedFolderPhysicalIdentity(91, 92, 93);

        private static readonly SkinManagedFolderPhysicalIdentity source_identity =
            new SkinManagedFolderPhysicalIdentity(91, 94, 95);

        [Test]
        public void TestSuccessfulDeleteDurablyClosesEveryPhaseAndHardRemovesExactRealmRecord()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.Succeeded));
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(
                        store.Writes.Select(journal => journal.DeleteFallbackDisposition),
                        Is.EqualTo(new SkinManagedFolderDeleteFallbackDisposition?[]
                        {
                            null,
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired,
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired,
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired,
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired,
                        }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN), Is.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.False);
                });
            });
        }

        [Test]
        [Platform("Win")]
        public void TestExactNonEmptyExternalSetAllowsDeleteAndBindsPreparedJournal()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                Guid externalId = SkinExternalExactSetTestHelper.AddServiceOwnedRecord(
                    realm,
                    storage,
                    $"delete-admission-{Guid.NewGuid():N}");
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);
                SkinManagedFolderMutationJournal? prepared = store.Writes.FirstOrDefault();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.Succeeded));
                    Assert.That(prepared, Is.Not.Null);
                    Assert.That(prepared?.ExternalRegistryGeneration ?? 0, Is.GreaterThan(0));
                    Assert.That(prepared?.ExternalCollisionDisposition,
                        Is.EqualTo(SkinExternalCollisionDisposition.ExactRegisteredExternalSet));
                    Assert.That(realm.Realm.Find<SkinInfo>(externalId), Is.Not.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        [Platform("Win")]
        public void TestFinalDeleteRealmTransactionRejectsExternalDeclarationDrift()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                Guid externalId = SkinExternalExactSetTestHelper.AddServiceOwnedRecord(
                    realm,
                    storage,
                    $"delete-drift-{Guid.NewGuid():N}");
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId)
                {
                    OnNeitherInspection = () =>
                        SkinExternalExactSetTestHelper.DriftDeclaration(realm, externalId),
                };
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status,
                        Is.EqualTo(SkinManagedFolderDeleteOperationStatus.RealmOutcomeUncertain));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(store.Current.Journal?.Phase,
                        Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [Test]
        public void TestFallbackRejectionRollsBackBeforePhysicalDetach()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var operation = createOperation(
                    realm,
                    storage,
                    store,
                    native,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.FallbackRejected));
                    Assert.That(result.FallbackCommitResult, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                });
            });
        }

        [Test]
        public void TestCancellationAfterFallbackAndBeforeDetachRollsBackPreparedIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var cancellation = new CancellationTokenSource();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var operation = createOperation(
                    realm,
                    storage,
                    store,
                    native,
                    (_, _, _) =>
                    {
                        cancellation.Cancel();
                        return SkinManagedFolderProtectedFallbackCommitResult.NotRequired;
                    });

                SkinManagedFolderDeleteOperationResult result =
                    operation.Execute(operationId, recordId, cancellation.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.Cancelled));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                });
            });
        }

        [Test]
        public void TestCancellationAfterConfirmedDispositionAndBeforeDetachRollsBackPreparedIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var cancellation = new CancellationTokenSource();
                var store = emptyStore();
                store.AfterWrite = journal =>
                {
                    if (journal.Phase == SkinManagedFolderMutationPhase.Prepared
                        && journal.DeleteFallbackDisposition
                           == SkinManagedFolderDeleteFallbackDisposition.NotRequired)
                    {
                        cancellation.Cancel();
                    }
                };
                var native = new FakeDeleteNativeAuthority(operationId);
                var operation = createOperation(
                    realm,
                    storage,
                    store,
                    native,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result =
                    operation.Execute(operationId, recordId, cancellation.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderDeleteOperationStatus.Cancelled));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(
                        SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                });
            });
        }

        [Test]
        public void TestCancellationAfterPhysicalDetachDoesNotOverrideDurableConvergence()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var cancellation = new CancellationTokenSource();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId)
                {
                    AfterRename = cancellation.Cancel,
                };
                var operation = createOperation(
                    realm,
                    storage,
                    store,
                    native,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result =
                    operation.Execute(operationId, recordId, cancellation.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.Succeeded));
                    Assert.That(cancellation.IsCancellationRequested, Is.True);
                    Assert.That(native.RenameCalls, Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestCommittedFallbackDriftAfterPhysicalDetachBlocksRealmRemoval()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId)
                {
                    AfterRename = () => realm.Write(r =>
                        r.Find<SkinInfo>(SkinInfo.OMS_SKIN)!.Protected = false),
                };
                var operation = createOperation(
                    realm,
                    storage,
                    store,
                    native,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.Committed);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.RealmOutcomeUncertain));
                    Assert.That(native.RenameCalls, Is.EqualTo(1));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN)?.Protected, Is.False);
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal?.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                });
            });
        }

        [Test]
        public void TestCommittedFallbackRealmDriftBeforeDetachSafelyAbortsPreparedIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) =>
                    {
                        realm.Write(r =>
                            r.Find<SkinInfo>(SkinInfo.OMS_SKIN)!.Protected = false);
                        return SkinManagedFolderProtectedFallbackCommitResult.Committed;
                    });

                SkinManagedFolderDeleteOperationResult result =
                    operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderDeleteOperationStatus.FallbackRejected));
                    Assert.That(result.FallbackCommitResult, Is.EqualTo(
                        SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.False);
                });
            });
        }

        [Test]
        public void TestRecordFingerprintDriftAfterPreparedIntentFailsClosedBeforeDetach()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) =>
                    {
                        realm.Write(r => r.Find<SkinInfo>(recordId)!.Name = "drifted name");
                        return SkinManagedFolderProtectedFallbackCommitResult.NotRequired;
                    });

                SkinManagedFolderDeleteOperationResult result = operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderDeleteOperationStatus.FallbackRejected));
                    Assert.That(result.FallbackCommitResult, Is.EqualTo(
                        SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected));
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.Name, Is.EqualTo("drifted name"));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.False);
                });
            });
        }

        [Test]
        public void TestReceiptDriftBeforeDetachRemainsFrozenAndNeverRenames()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeDeleteNativeAuthority(operationId);
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) =>
                    {
                        SkinManagedFolderMutationJournal externallyRewritten =
                            store.Current.Journal!.WithDeleteFallbackDisposition(
                                SkinManagedFolderDeleteFallbackDisposition.NotRequired);
                        store.SetCurrent(new SkinManagedFolderMutationJournalLoadResult(
                            SkinManagedFolderMutationJournalLoadStatus.Loaded,
                            externallyRewritten));
                        return SkinManagedFolderProtectedFallbackCommitResult.NotRequired;
                    });

                SkinManagedFolderDeleteOperationResult result =
                    operation.Execute(operationId, recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.DeleteFallbackDisposition,
                        Is.EqualTo(SkinManagedFolderDeleteFallbackDisposition.NotRequired));
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [Test]
        public void TestPreparedSourceOnlyRecoveryRollsBackWithoutPhysicalCleanup()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                SkinManagedFolderMutationJournal prepared = createPreparedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(operationId);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[] { SkinManagedFolderMutationPhase.RolledBack }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.False);
                });
            });
        }

        [Test]
        public void TestPreparedTargetOnlyRecoveryDeletesTombstoneAndConvergesRealmForward()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal prepared = createFallbackConfirmedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.TargetOnly);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.False);
                });
            });
        }

        [Test]
        public void TestNonCurrentPreparedTargetOnlyRecoveryUsesDurableNotRequiredDisposition()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                SkinManagedFolderMutationJournal prepared =
                    createPreparedDelete(realm, operationId, recordId)
                        .WithDeleteFallbackDisposition(
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.TargetOnly);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(
                        SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN), Is.Null);
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderRenameInspectionStatus.TargetOnly)]
        [TestCase(SkinManagedFolderRenameInspectionStatus.Neither)]
        public void TestUnconfirmedPreparedPhysicalProgressIsAmbiguousAndNeverDeletes(
            SkinManagedFolderRenameInspectionStatus physicalStatus)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                SkinManagedFolderMutationJournal prepared =
                    createPreparedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    physicalStatus);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(store.Current.Journal!.IsExactSameJournal(prepared),
                        Is.True);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [Test]
        public void TestConfirmedPreparedNeitherRecoveryIsAmbiguousAndNeverDeletesRealmRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal prepared =
                    createFallbackConfirmedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(store.Current.Journal!.IsExactSameJournal(prepared), Is.True);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [Test]
        public void TestFilesystemAppliedNeitherRecoveryHardRemovesExactRealmRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal filesystemApplied =
                    createFallbackConfirmedDelete(realm, operationId, recordId).WithFilesystemApplied();
                var store = new MemoryMutationJournalStore(filesystemApplied);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN), Is.Not.Null);
                });
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestFilesystemAppliedTargetOnlyRecoveryCleansTombstoneAndConvergesRealmForward(
            bool protectedFallbackCommitted)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);

                if (protectedFallbackCommitted)
                    addProtectedFallback(realm);

                SkinManagedFolderMutationJournal prepared = protectedFallbackCommitted
                    ? createFallbackConfirmedDelete(realm, operationId, recordId)
                    : createPreparedDelete(realm, operationId, recordId)
                        .WithDeleteFallbackDisposition(
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired);
                SkinManagedFolderMutationJournal filesystemApplied =
                    prepared.WithFilesystemApplied();
                var store = new MemoryMutationJournalStore(filesystemApplied);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.TargetOnly);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.CleanupCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(
                        SkinManagedFolderRenameInspectionStatus.Neither));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(
                        realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN) != null,
                        Is.EqualTo(protectedFallbackCommitted));
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestNonCurrentFilesystemAppliedRecoveryDoesNotRequireProtectedFallbackRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                SkinManagedFolderMutationJournal filesystemApplied =
                    createPreparedDelete(realm, operationId, recordId)
                        .WithDeleteFallbackDisposition(
                            SkinManagedFolderDeleteFallbackDisposition.NotRequired)
                        .WithFilesystemApplied();
                var store = new MemoryMutationJournalStore(filesystemApplied);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Current.Status, Is.EqualTo(
                        SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN), Is.Null);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestFilesystemAppliedRealmFingerprintDriftIsAmbiguousAndNeverRemovesRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal filesystemApplied =
                    createFallbackConfirmedDelete(realm, operationId, recordId).WithFilesystemApplied();
                realm.Write(r => r.Find<SkinInfo>(recordId)!.Creator = "drifted creator");
                var store = new MemoryMutationJournalStore(filesystemApplied);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.Journal!.IsExactSameJournal(filesystemApplied), Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.Creator, Is.EqualTo("drifted creator"));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [TestCase(SkinManagedFolderMutationPhase.FilesystemApplied)]
        [TestCase(SkinManagedFolderMutationPhase.RealmApplied)]
        public void TestNeitherAndRealmAbsentRecoveryCompletesRemainingCheckpoints(
            SkinManagedFolderMutationPhase retainedPhase)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal retained =
                    createFallbackConfirmedDelete(realm, operationId, recordId).WithFilesystemApplied();

                if (retainedPhase == SkinManagedFolderMutationPhase.RealmApplied)
                    retained = retained.WithRealmApplied();

                realm.Write(r => r.Remove(r.Find<SkinInfo>(recordId)!));
                var store = new MemoryMutationJournalStore(retained);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                SkinManagedFolderMutationPhase[] expectedWrites = retainedPhase == SkinManagedFolderMutationPhase.FilesystemApplied
                    ? new[]
                    {
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }
                    : new[] { SkinManagedFolderMutationPhase.Committed };

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Writes.Select(journal => journal.Phase), Is.EqualTo(expectedWrites));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderMutationPhase.FilesystemApplied)]
        [TestCase(SkinManagedFolderMutationPhase.RealmApplied)]
        public void TestNeitherAndRealmAbsentRecoveryWithInvalidFallbackRemainsAmbiguous(
            SkinManagedFolderMutationPhase retainedPhase)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm, valid: false);
                SkinManagedFolderMutationJournal retained =
                    createFallbackConfirmedDelete(realm, operationId, recordId).WithFilesystemApplied();

                if (retainedPhase == SkinManagedFolderMutationPhase.RealmApplied)
                    retained = retained.WithRealmApplied();

                realm.Write(r => r.Remove(r.Find<SkinInfo>(recordId)!));
                var store = new MemoryMutationJournalStore(retained);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.Neither);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal?.IsExactSameJournal(retained), Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN)?.Protected, Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderRenameInspectionStatus.Both)]
        [TestCase(SkinManagedFolderRenameInspectionStatus.IdentityMismatch)]
        public void TestAmbiguousPhysicalRecoveryRetainsExactDeleteIntent(
            SkinManagedFolderRenameInspectionStatus physicalStatus)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm);
                SkinManagedFolderMutationJournal prepared = createFallbackConfirmedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(operationId, physicalStatus);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.IsExactSameJournal(prepared), Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [Test]
        public void TestInvalidProtectedFallbackMakesForwardRecoveryAmbiguousBeforeCleanup()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm, valid: false);
                SkinManagedFolderMutationJournal prepared = createFallbackConfirmedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.TargetOnly);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.IsExactSameJournal(prepared), Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(SkinInfo.OMS_SKIN)!.Protected, Is.False);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        [TestCase("name")]
        [TestCase("creator")]
        [TestCase("hash")]
        [TestCase("owner")]
        public void TestNonCanonicalProtectedFallbackMetadataMakesForwardRecoveryAmbiguous(
            string drift)
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid operationId = Guid.NewGuid();
                Guid recordId = addManagedRecord(realm);
                addProtectedFallback(realm, fallback =>
                {
                    switch (drift)
                    {
                        case "name":
                            fallback.Name = "foreign fallback name";
                            break;

                        case "creator":
                            fallback.Creator = "foreign fallback creator";
                            break;

                        case "hash":
                            fallback.Hash = "foreign-fallback-hash";
                            break;

                        case "owner":
                            fallback.FilesystemStorageAuthorityOwner = "foreign-owner";
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(drift));
                    }
                });
                SkinManagedFolderMutationJournal prepared = createFallbackConfirmedDelete(realm, operationId, recordId);
                var store = new MemoryMutationJournalStore(prepared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeDeleteNativeAuthority(
                    operationId,
                    SkinManagedFolderRenameInspectionStatus.TargetOnly);

                SkinManagedFolderMutationRecoveryResult result =
                    createRecovery(realm, store, coordinator, native).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal?.IsExactSameJournal(prepared), Is.True);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(native.CleanupCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId), Is.Not.Null);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(native.TombstonePath), Is.True);
                });
            });
        }

        private static SkinManagedFolderDeleteOperation createOperation(
            RealmAccess realm,
            Framework.Platform.Storage storage,
            MemoryMutationJournalStore store,
            FakeDeleteNativeAuthority native,
            SkinManagedFolderDeleteFallbackCommit fallback)
        {
            var authority = new SkinManagedFolderMutationAuthority(
                realm,
                storage,
                new SkinManagedFolderOperationCoordinator(),
                native,
                store);
            return new SkinManagedFolderDeleteOperation(realm, authority, fallback);
        }

        private static SkinManagedFolderMutationRecovery createRecovery(
            RealmAccess realm,
            MemoryMutationJournalStore store,
            SkinManagedFolderOperationCoordinator coordinator,
            FakeDeleteNativeAuthority native)
            => new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                new SkinManagedFolderDeleteRecoveryHandler(realm, native));

        private static Guid addManagedRecord(RealmAccess realm)
        {
            var record = new SkinInfo(
                "Author display name",
                "Author creator",
                SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = Guid.NewGuid(),
                Hash = "content-revision",
                FilesystemStoragePath = source_path,
                IsExternalFilesystemStorage = false,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                Protected = false,
                DeletePending = false,
            };
            realm.Write(r => r.Add(record));
            return record.ID;
        }

        private static void addProtectedFallback(RealmAccess realm, bool valid = true)
        {
            SkinInfo fallback = OmsSkin.CreateInfo();
            fallback.Protected = valid;
            realm.Write(r => r.Add(fallback));
        }

        private static void addProtectedFallback(
            RealmAccess realm,
            Action<SkinInfo> mutate)
        {
            SkinInfo fallback = OmsSkin.CreateInfo();
            mutate(fallback);
            realm.Write(r => r.Add(fallback));
        }

        private static SkinManagedFolderMutationJournal createPreparedDelete(
            RealmAccess realm,
            Guid operationId,
            Guid recordId)
        {
            string fingerprint = realm.Run(r =>
                SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(
                    r.Find<SkinInfo>(recordId)!));

            return SkinManagedFolderMutationJournal.CreatePreparedDelete(
                operationId,
                recordId,
                root_identity,
                source_path,
                source_identity,
                fingerprint,
                SkinManagedFolderDeleteManifest.Create(
                    new[] { new string('a', 64) }));
        }

        private static SkinManagedFolderMutationJournal createFallbackConfirmedDelete(
            RealmAccess realm,
            Guid operationId,
            Guid recordId)
            => createPreparedDelete(realm, operationId, recordId)
                .WithDeleteFallbackDisposition(
                    SkinManagedFolderDeleteFallbackDisposition.ProtectedPairCommitted);

        private static MemoryMutationJournalStore emptyStore()
            => new MemoryMutationJournalStore(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing));

        private sealed class FakeDeleteNativeAuthority : ISkinManagedFolderMutationNativeAuthority
        {
            public string TombstonePath { get; }

            public SkinManagedFolderRenameInspectionStatus Status { get; private set; }

            public int RenameCalls { get; private set; }

            public int CleanupCalls { get; private set; }

            public Action? AfterRename { get; init; }

            public Action? OnNeitherInspection { get; init; }

            private int neitherInspectionCallbackInvoked;

            public FakeDeleteNativeAuthority(
                Guid operationId,
                SkinManagedFolderRenameInspectionStatus status = SkinManagedFolderRenameInspectionStatus.SourceOnly)
            {
                TombstonePath = SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(operationId);
                Status = status;
            }

            public ISkinManagedFolderMutationNativeSession Open(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new Session(this);
            }

            private sealed class Session : ISkinManagedFolderMutationNativeSession
            {
                private readonly FakeDeleteNativeAuthority owner;

                public SkinManagedFolderPhysicalIdentity ManagedRootIdentity => root_identity;

                public SkinFolderPhysicalAncestryProof ManagedRootAncestryProof { get; } =
                    new SkinFolderPhysicalAncestryProof(new[]
                    {
                        new SkinManagedFolderPhysicalIdentity(91, 1, 1),
                        root_identity,
                    });

                public Session(FakeDeleteNativeAuthority owner)
                {
                    this.owner = owner;
                }

                public SkinManagedFolderPhysicalIdentity CaptureExistingSource(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status is not (SkinManagedFolderRenameInspectionStatus.SourceOnly
                        or SkinManagedFolderRenameInspectionStatus.Both)
                        || !string.Equals(managedRelativePath, source_path, StringComparison.Ordinal))
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    return source_identity;
                }

                public string GetCapturedDeleteSourceNodeManifest(
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return SkinManagedFolderDeleteManifest.Create(
                        new[] { new string('a', 64) });
                }

                public SkinManagedFolderStagedSourceCapture CaptureStagedSource(
                    Guid operationId,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status is not (SkinManagedFolderRenameInspectionStatus.SourceOnly
                        or SkinManagedFolderRenameInspectionStatus.Neither)
                        || !string.Equals(managedRelativePath, owner.TombstonePath, StringComparison.Ordinal))
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    return new SkinManagedFolderTargetNameSlot(managedRelativePath, root_identity);
                }

                public SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    CancellationToken cancellationToken)
                {
                    owner.RenameCalls++;
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status != SkinManagedFolderRenameInspectionStatus.SourceOnly
                        || !string.Equals(targetNameSlot.ManagedRelativePath, owner.TombstonePath, StringComparison.Ordinal)
                        || targetNameSlot.ManagedRootIdentity != root_identity)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    owner.Status = SkinManagedFolderRenameInspectionStatus.TargetOnly;
                    owner.AfterRename?.Invoke();
                    return source_identity;
                }

                public SkinManagedFolderStagedImportFilesystemResult MoveCapturedStagedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    string expectedContentRevision,
                    string expectedTreeFingerprint,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public SkinManagedFolderRenameInspection InspectRenameState(
                    string sourceManagedRelativePath,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status == SkinManagedFolderRenameInspectionStatus.Neither
                        && Interlocked.Exchange(ref owner.neitherInspectionCallbackInvoked, 1) == 0)
                    {
                        owner.OnNeitherInspection?.Invoke();
                    }

                    if (!string.Equals(sourceManagedRelativePath, source_path, StringComparison.Ordinal)
                        || !string.Equals(targetManagedRelativePath, owner.TombstonePath, StringComparison.Ordinal)
                        || expectedSourceIdentity != source_identity)
                    {
                        return new SkinManagedFolderRenameInspection(
                            SkinManagedFolderRenameInspectionStatus.IdentityMismatch);
                    }

                    return new SkinManagedFolderRenameInspection(owner.Status);
                }

                public void CleanupExactDeleteTombstone(
                    string sourceManagedRelativePath,
                    string tombstoneManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    string expectedSourceNodeManifest,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    owner.CleanupCalls++;

                    if (owner.Status != SkinManagedFolderRenameInspectionStatus.TargetOnly
                        || !string.Equals(sourceManagedRelativePath, source_path, StringComparison.Ordinal)
                        || !string.Equals(tombstoneManagedRelativePath, owner.TombstonePath, StringComparison.Ordinal)
                        || expectedSourceIdentity != source_identity
                        || !string.Equals(
                            expectedSourceNodeManifest,
                            SkinManagedFolderDeleteManifest.Create(
                                new[] { new string('a', 64) }),
                            StringComparison.Ordinal))
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    owner.Status = SkinManagedFolderRenameInspectionStatus.Neither;
                }

                public SkinManagedFolderStagedImportInspection InspectStagedImportState(
                    Guid operationId,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public void CleanupExactStagedSource(
                    Guid operationId,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                    => throw new NotSupportedException();

                public void ValidateCompleteAndStable(CancellationToken cancellationToken)
                    => cancellationToken.ThrowIfCancellationRequested();

                public void Dispose()
                {
                }
            }
        }
    }
}
