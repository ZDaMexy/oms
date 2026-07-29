// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    internal class SkinManagedFolderRenameOperationTest : RealmTest
    {
        private const string source_path = "chartskin/source";
        private const string target_path = "chartskin/target";

        private static readonly SkinManagedFolderPhysicalIdentity root_identity =
            new SkinManagedFolderPhysicalIdentity(91, 92, 93);

        private static readonly SkinManagedFolderPhysicalIdentity source_identity =
            new SkinManagedFolderPhysicalIdentity(91, 94, 95);

        [Test]
        public void TestSuccessfulRenameDurablyClosesEveryPhaseAndOnlyChangesManagedPath()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult result =
                    operation.Execute(Guid.NewGuid(), recordId, "target");
                SkinInfo record = realm.Realm.Find<SkinInfo>(recordId)!;

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Succeeded));
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.AuthorityRejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.None));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(native.RenameCalls, Is.EqualTo(1));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(record.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(record.Name, Is.EqualTo("Author display name"));
                    Assert.That(record.Creator, Is.EqualTo("Author creator"));
                    Assert.That(record.Hash, Is.EqualTo("content-revision"));
                    Assert.That(record.InstantiationInfo, Is.EqualTo(SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO));
                    Assert.That(record.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(record.DeletePending, Is.False);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });

                string diagnostic = result.ToString();
                Assert.Multiple(() =>
                {
                    Assert.That(diagnostic, Does.Not.Contain(source_path));
                    Assert.That(diagnostic, Does.Not.Contain(target_path));
                    Assert.That(diagnostic, Does.Not.Contain(recordId.ToString()));
                    Assert.That(diagnostic, Does.Not.Contain(source_identity.VolumeSerialNumber.ToString()));
                });
            });
        }

        [Test]
        public void TestCancellationBeforeVisibleMoveRollsBackPreparedIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority { CancelBeforeVisibleMove = true };
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult result =
                    operation.Execute(Guid.NewGuid(), recordId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Cancelled));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.RolledBack,
                        }));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(source_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestCancellationAfterVisibleMoveCannotTurnRenameIntoAbort()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var cancellation = new CancellationTokenSource();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority
                {
                    AfterVisibleMove = cancellation.Cancel,
                };
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult result =
                    operation.Execute(Guid.NewGuid(), recordId, "target", cancellation.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Succeeded));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(
                        store.Writes.Select(journal => journal.Phase),
                        Is.EqualTo(new[]
                        {
                            SkinManagedFolderMutationPhase.Prepared,
                            SkinManagedFolderMutationPhase.FilesystemApplied,
                            SkinManagedFolderMutationPhase.RealmApplied,
                            SkinManagedFolderMutationPhase.Committed,
                        }));
                });
            });
        }

        [Test]
        public void TestExactPreparedReceiptIsRequiredImmediatelyBeforePhysicalRename()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                SkinManagedFolderMutationAuthorityResult opened =
                    authority.OpenRename(Guid.NewGuid(), recordId, "target");

                using SkinManagedFolderMutationAuthoritySession session = opened.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                SkinManagedFolderMutationJournal prepared = store.Current.Journal!;
                store.SetCurrent(
                    new SkinManagedFolderMutationJournalLoadResult(
                        SkinManagedFolderMutationJournalLoadStatus.Loaded,
                        prepared.WithRolledBack()));

                Assert.Throws<InvalidOperationException>(
                    () => session.ApplyCapturedRenameWithDurableReceipt(receipt));
                Assert.Multiple(() =>
                {
                    Assert.That(native.RenameCalls, Is.Zero);
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.SourceOnly));
                });
            });
        }

        [Test]
        public void TestFilesystemPhaseWriteFaultRetainsPreparedIntentAndFreezesBothPaths()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = new PhaseFaultMutationJournalStore(SkinManagedFolderMutationPhase.FilesystemApplied);
                var native = new FakeRenameNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult result =
                    operation.Execute(Guid.NewGuid(), recordId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.FilesystemOutcomeUncertain));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(source_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [TestCase(
            SkinManagedFolderMutationPhase.FilesystemApplied,
            SkinManagedFolderRenameOperationStatus.FilesystemOutcomeUncertain,
            SkinManagedFolderMutationPhase.Prepared)]
        [TestCase(
            SkinManagedFolderMutationPhase.RealmApplied,
            SkinManagedFolderRenameOperationStatus.RealmOutcomeUncertain,
            SkinManagedFolderMutationPhase.FilesystemApplied)]
        [TestCase(
            SkinManagedFolderMutationPhase.Committed,
            SkinManagedFolderRenameOperationStatus.CommitOutcomeUncertain,
            SkinManagedFolderMutationPhase.RealmApplied)]
        public void TestEveryPostVisiblePhaseWriteFaultIsRecoveredIdempotently(
            SkinManagedFolderMutationPhase faultPhase,
            SkinManagedFolderRenameOperationStatus expectedOperationStatus,
            SkinManagedFolderMutationPhase expectedDurablePhase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = new PhaseFaultMutationJournalStore(faultPhase);
                var native = new FakeRenameNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult operationResult =
                    operation.Execute(Guid.NewGuid(), recordId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(operationResult.Status, Is.EqualTo(expectedOperationStatus));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.Phase, Is.EqualTo(expectedDurablePhase));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.TargetOnly));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });

                var handler = new SkinManagedFolderRenameRecoveryHandler(realm, native);
                SkinManagedFolderMutationRecoveryResult recoveryResult =
                    new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(recoveryResult.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [Test]
        public void TestRealmTargetRaceAfterPhysicalMoveRetainsFilesystemJournalAndFreezes()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority
                {
                    AfterVisibleMove = () => addRecord(realm, target_path),
                };
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult operationResult =
                    operation.Execute(Guid.NewGuid(), recordId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(operationResult.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.RealmOutcomeUncertain));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(source_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });

                var handler = new SkinManagedFolderRenameRecoveryHandler(realm, native);
                SkinManagedFolderMutationRecoveryResult recoveryResult =
                    new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(recoveryResult.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestRenameRejectsTargetIdentityDiscontinuityAndKeepsPreparedRecoveryIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm, source_path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var store = emptyStore();
                var native = new FakeRenameNativeAuthority { ReturnWrongTargetIdentity = true };
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    store);
                var operation = new SkinManagedFolderRenameOperation(realm, authority);

                SkinManagedFolderRenameOperationResult result =
                    operation.Execute(Guid.NewGuid(), recordId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.FilesystemOutcomeUncertain));
                    Assert.That(native.Status, Is.EqualTo(SkinManagedFolderRenameInspectionStatus.IdentityMismatch));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(source_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [TestCase(
            SkinManagedFolderRenameInspectionStatus.SourceOnly,
            false,
            SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
            false,
            TestName = "TestRecoveryAlreadyRolledBackWhenPhysicalAndRealmAreAtSource")]
        [TestCase(
            SkinManagedFolderRenameInspectionStatus.SourceOnly,
            true,
            SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
            false,
            TestName = "TestRecoveryRollsRealmBackWhenPhysicalIsAtSource")]
        [TestCase(
            SkinManagedFolderRenameInspectionStatus.TargetOnly,
            false,
            SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
            true,
            TestName = "TestRecoveryRollsRealmForwardWhenPhysicalIsAtTarget")]
        [TestCase(
            SkinManagedFolderRenameInspectionStatus.TargetOnly,
            true,
            SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
            true,
            TestName = "TestRecoveryAlreadyCommittedWhenPhysicalAndRealmAreAtTarget")]
        public void TestRecoveryConvergesRealmToExactPhysicalSlot(
            SkinManagedFolderRenameInspectionStatus physicalStatus,
            bool realmStartsAtTarget,
            SkinManagedFolderMutationRecoveryStatus expectedStatus,
            bool expectedTarget)
        {
            RunTestWithRealm((realm, _) =>
            {
                SkinManagedFolderMutationJournal journal = createPreparedRename();
                Guid recordId = journal.RecordId!.Value;
                addRecord(realm, realmStartsAtTarget ? target_path : source_path, recordId);
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeRenameNativeAuthority { Status = physicalStatus };
                var handler = new SkinManagedFolderRenameRecoveryHandler(realm, native);
                var recovery = new SkinManagedFolderMutationRecovery(store, coordinator, handler);

                SkinManagedFolderMutationRecoveryResult result = recovery.Recover();
                SkinInfo record = realm.Realm.Find<SkinInfo>(recordId)!;

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(expectedStatus));
                    Assert.That(result.IsResolved, Is.True);
                    Assert.That(record.FilesystemStoragePath, Is.EqualTo(expectedTarget ? target_path : source_path));
                    Assert.That(record.Name, Is.EqualTo("Author display name"));
                    Assert.That(record.Creator, Is.EqualTo("Author creator"));
                    Assert.That(record.Hash, Is.EqualTo("content-revision"));
                    Assert.That(record.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderRenameInspectionStatus.Both)]
        [TestCase(SkinManagedFolderRenameInspectionStatus.Neither)]
        [TestCase(SkinManagedFolderRenameInspectionStatus.IdentityMismatch)]
        public void TestAmbiguousPhysicalRecoveryRetainsJournalAndExactFreeze(
            SkinManagedFolderRenameInspectionStatus physicalStatus)
        {
            RunTestWithRealm((realm, _) =>
            {
                SkinManagedFolderMutationJournal journal = createPreparedRename();
                Guid recordId = journal.RecordId!.Value;
                addRecord(realm, source_path, recordId);
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var native = new FakeRenameNativeAuthority { Status = physicalStatus };
                var handler = new SkinManagedFolderRenameRecoveryHandler(realm, native);

                SkinManagedFolderMutationRecoveryResult result =
                    new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.IsLoaded, Is.True);
                    Assert.That(store.Current.Journal!.IsExactSameJournal(journal), Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(recordId)!.FilesystemStoragePath, Is.EqualTo(source_path));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                });
            });
        }

        [Test]
        public void TestRecoveryHandlerRejectsNonRenameWithoutOpeningNativeAuthority()
        {
            RunTestWithRealm((realm, _) =>
            {
                SkinManagedFolderMutationJournal journal = SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    source_identity);
                var native = new FakeRenameNativeAuthority();
                var handler = new SkinManagedFolderRenameRecoveryHandler(realm, native);

                SkinManagedFolderMutationRecoveryInspection inspection =
                    handler.Inspect(journal, CancellationToken.None);
                SkinManagedFolderMutationRecoveryActionResult forward =
                    handler.TryRollForward(journal, CancellationToken.None);
                SkinManagedFolderMutationRecoveryActionResult rollback =
                    handler.TryRollBack(journal, CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(inspection.Decision, Is.EqualTo(SkinManagedFolderMutationRecoveryDecision.Ambiguous));
                    Assert.That(forward.IsSuccess, Is.False);
                    Assert.That(rollback.IsSuccess, Is.False);
                    Assert.That(native.OpenCalls, Is.Zero);
                    Assert.That(handler.ToString(), Is.EqualTo(nameof(SkinManagedFolderRenameRecoveryHandler)));
                });
            });
        }

        private static Guid addRecord(
            RealmAccess realm,
            string path,
            Guid? recordId = null)
        {
            var record = new SkinInfo(
                "Author display name",
                "Author creator",
                SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = recordId ?? Guid.NewGuid(),
                Hash = "content-revision",
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = false,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                Protected = false,
                DeletePending = false,
            };
            realm.Write(r => r.Add(record));
            return record.ID;
        }

        private static SkinManagedFolderMutationJournal createPreparedRename()
            => SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path);

        private static MemoryMutationJournalStore emptyStore()
            => new MemoryMutationJournalStore(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing));

        private sealed class PhaseFaultMutationJournalStore : ISkinManagedFolderMutationJournalStore
        {
            private readonly SkinManagedFolderMutationPhase faultPhase;
            private bool faultInjected;

            public SkinManagedFolderMutationJournalLoadResult Current { get; private set; } =
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing);

            public List<SkinManagedFolderMutationJournal> Writes { get; } =
                new List<SkinManagedFolderMutationJournal>();

            public PhaseFaultMutationJournalStore(SkinManagedFolderMutationPhase faultPhase)
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

            public void Delete(SkinManagedFolderMutationJournal expectedJournal)
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

        private sealed class FakeRenameNativeAuthority : ISkinManagedFolderMutationNativeAuthority
        {
            public SkinManagedFolderRenameInspectionStatus Status { get; set; } =
                SkinManagedFolderRenameInspectionStatus.SourceOnly;

            public bool CancelBeforeVisibleMove { get; init; }

            public bool ReturnWrongTargetIdentity { get; init; }

            public Action? AfterVisibleMove { get; init; }

            public int OpenCalls { get; private set; }
            public int RenameCalls { get; private set; }

            public ISkinManagedFolderMutationNativeSession Open(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OpenCalls++;
                return new Session(this);
            }

            private sealed class Session : ISkinManagedFolderMutationNativeSession
            {
                private readonly FakeRenameNativeAuthority owner;

                public SkinManagedFolderPhysicalIdentity ManagedRootIdentity => root_identity;

                public Session(FakeRenameNativeAuthority owner)
                {
                    this.owner = owner;
                }

                public SkinManagedFolderPhysicalIdentity CaptureExistingSource(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool sourcePresent = owner.Status is SkinManagedFolderRenameInspectionStatus.SourceOnly
                        or SkinManagedFolderRenameInspectionStatus.Both;
                    bool targetPresent = owner.Status is SkinManagedFolderRenameInspectionStatus.TargetOnly
                        or SkinManagedFolderRenameInspectionStatus.Both;

                    if ((string.Equals(managedRelativePath, source_path, StringComparison.Ordinal) && sourcePresent)
                        || (string.Equals(managedRelativePath, target_path, StringComparison.Ordinal) && targetPresent))
                    {
                        return source_identity;
                    }

                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }

                public SkinManagedFolderStagedSourceCapture CaptureStagedSource(
                    Guid operationId,
                    CancellationToken cancellationToken)
                    => throw new SkinManagedFolderMutationNativeAuthorityException();

                public SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool sourceAbsent = owner.Status is SkinManagedFolderRenameInspectionStatus.TargetOnly
                        or SkinManagedFolderRenameInspectionStatus.Neither;
                    bool targetAbsent = owner.Status is SkinManagedFolderRenameInspectionStatus.SourceOnly
                        or SkinManagedFolderRenameInspectionStatus.Neither;

                    if ((string.Equals(managedRelativePath, source_path, StringComparison.Ordinal) && sourceAbsent)
                        || (string.Equals(managedRelativePath, target_path, StringComparison.Ordinal) && targetAbsent))
                    {
                        return new SkinManagedFolderTargetNameSlot(managedRelativePath, root_identity);
                    }

                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }

                public SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    CancellationToken cancellationToken)
                {
                    owner.RenameCalls++;

                    if (owner.CancelBeforeVisibleMove)
                        throw new OperationCanceledException(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (owner.Status != SkinManagedFolderRenameInspectionStatus.SourceOnly
                        || !string.Equals(targetNameSlot.ManagedRelativePath, target_path, StringComparison.Ordinal)
                        || targetNameSlot.ManagedRootIdentity != root_identity)
                    {
                        throw new SkinManagedFolderMutationNativeAuthorityException();
                    }

                    owner.Status = owner.ReturnWrongTargetIdentity
                        ? SkinManagedFolderRenameInspectionStatus.IdentityMismatch
                        : SkinManagedFolderRenameInspectionStatus.TargetOnly;
                    owner.AfterVisibleMove?.Invoke();
                    return owner.ReturnWrongTargetIdentity
                        ? new SkinManagedFolderPhysicalIdentity(91, 96, 97)
                        : source_identity;
                }

                public SkinManagedFolderRenameInspection InspectRenameState(
                    string sourceManagedRelativePath,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.Equals(sourceManagedRelativePath, source_path, StringComparison.Ordinal)
                        || !string.Equals(targetManagedRelativePath, target_path, StringComparison.Ordinal)
                        || expectedSourceIdentity != source_identity)
                    {
                        return new SkinManagedFolderRenameInspection(
                            SkinManagedFolderRenameInspectionStatus.IdentityMismatch);
                    }

                    return new SkinManagedFolderRenameInspection(owner.Status);
                }

                public void ValidateCompleteAndStable(CancellationToken cancellationToken)
                    => cancellationToken.ThrowIfCancellationRequested();

                public void Dispose()
                {
                }
            }
        }
    }
}
