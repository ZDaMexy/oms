// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinManagedFolderMutationAuthorityTest : RealmTest
    {
        private const string source_path = "chartskin/source";

        private static readonly SkinManagedFolderPhysicalIdentity root_identity = new SkinManagedFolderPhysicalIdentity(11, 12, 13);
        private static readonly SkinManagedFolderPhysicalIdentity source_identity = new SkinManagedFolderPhysicalIdentity(11, 22, 23);
        private static readonly SkinManagedFolderPhysicalIdentity staged_root_identity = new SkinManagedFolderPhysicalIdentity(11, 31, 32);
        private static readonly SkinManagedFolderPhysicalIdentity staged_identity = new SkinManagedFolderPhysicalIdentity(11, 33, 34);
        private static readonly string staged_tree_fingerprint = new string('b', 64);

        [Test]
        public void TestEligibleExistingRecordBindsExactNativePhysicalIdentityAndKeepsSessionHeld()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                addRecord(realm, path: "chartskin/distinct");
                var native = new FakeNativeAuthority();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    createEmptyJournalStore());

                SkinManagedFolderMutationAuthorityResult result = authority.OpenDelete(Guid.NewGuid(), recordId);
                SkinManagedFolderMutationAuthoritySession session = result.Session!;
                SkinManagedFolderMutationJournal prepared = session.CreatePreparedJournal();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.None));
                    Assert.That(session.Kind, Is.EqualTo(SkinManagedFolderMutationKind.Delete));
                    Assert.That(session.ExistingRecord, Is.Not.Null);
                    Assert.That(session.ExistingRecord!.RecordId, Is.EqualTo(recordId));
                    Assert.That(session.ExistingRecord.ManagedRelativePath, Is.EqualTo(source_path));
                    Assert.That(session.ExistingRecord.PhysicalIdentity, Is.EqualTo(source_identity));
                    Assert.That(session.TargetNameSlot, Is.Not.Null);
                    Assert.That(
                        session.TargetNameSlot!.ManagedRelativePath,
                        Is.EqualTo(SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(session.OperationId)));
                    Assert.That(session.NewRecordPublicationPlan, Is.Null);
                    Assert.That(prepared.Kind, Is.EqualTo(SkinManagedFolderMutationKind.Delete));
                    Assert.That(prepared.OperationId, Is.EqualTo(session.OperationId));
                    Assert.That(prepared.RecordId, Is.EqualTo(recordId));
                    Assert.That(prepared.SourceIdentity, Is.EqualTo(source_identity));
                    Assert.That(prepared.NewRecordPublicationFingerprint, Is.EqualTo(session.ExistingRecord.RecordFingerprint));
                    Assert.That(native.CapturedSourcePaths, Is.EqualTo(new[] { source_path }));
                    Assert.That(native.ActiveSessions, Is.EqualTo(1));
                    Assert.That(native.DisposedSessions, Is.Zero);
                    Assert.That(session.Validate(), Is.True);
                });

                session.Dispose();
                session.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(session.Validate(), Is.False);
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(native.DisposedSessions, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestMissingExistingRecordRejectedBeforeNativeOpen()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenDelete(Guid.NewGuid(), Guid.NewGuid());

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordMissing));
                    Assert.That(result.Session, Is.Null);
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [TestCase(IneligibleRecordCase.OwnerMissing)]
        [TestCase(IneligibleRecordCase.OwnerCaseMismatch)]
        [TestCase(IneligibleRecordCase.FolderMissing)]
        [TestCase(IneligibleRecordCase.FolderOutsideManagedRoot)]
        [TestCase(IneligibleRecordCase.FolderNotDirectChild)]
        [TestCase(IneligibleRecordCase.FolderNotNfcCanonical)]
        [TestCase(IneligibleRecordCase.RealmFilesPresent)]
        [TestCase(IneligibleRecordCase.ExternalFolder)]
        [TestCase(IneligibleRecordCase.Protected)]
        [TestCase(IneligibleRecordCase.FixedId)]
        [TestCase(IneligibleRecordCase.DeletePending)]
        [TestCase(IneligibleRecordCase.InstantiationNotAllowlisted)]
        [TestCase(IneligibleRecordCase.HashMissing)]
        [TestCase(IneligibleRecordCase.PathNotUnique)]
        public void TestExistingRecordEligibilityMatrixRejectsBeforeNativeOpen(IneligibleRecordCase recordCase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                SkinInfo record = createEligibleRecord();

                switch (recordCase)
                {
                    case IneligibleRecordCase.OwnerMissing:
                        record.FilesystemStorageAuthorityOwner = null;
                        break;

                    case IneligibleRecordCase.OwnerCaseMismatch:
                        record.FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER.ToUpperInvariant();
                        break;

                    case IneligibleRecordCase.FolderMissing:
                        record.FilesystemStoragePath = null;
                        break;

                    case IneligibleRecordCase.FolderOutsideManagedRoot:
                        record.FilesystemStoragePath = "files/source";
                        break;

                    case IneligibleRecordCase.FolderNotDirectChild:
                        record.FilesystemStoragePath = "chartskin/nested/source";
                        break;

                    case IneligibleRecordCase.FolderNotNfcCanonical:
                        record.FilesystemStoragePath = "chartskin/Cafe\u0301";
                        break;

                    case IneligibleRecordCase.RealmFilesPresent:
                        record.Files.Add(new RealmNamedFileUsage(
                            new RealmFile { Hash = $"legacy-{Guid.NewGuid():N}" },
                            "skin.ini"));
                        break;

                    case IneligibleRecordCase.ExternalFolder:
                        record.IsExternalFilesystemStorage = true;
                        break;

                    case IneligibleRecordCase.Protected:
                        record.Protected = true;
                        break;

                    case IneligibleRecordCase.FixedId:
                        record.ID = SkinInfo.RANDOM_SKIN;
                        break;

                    case IneligibleRecordCase.DeletePending:
                        record.DeletePending = true;
                        break;

                    case IneligibleRecordCase.InstantiationNotAllowlisted:
                        record.InstantiationInfo = typeof(LegacySkin).AssemblyQualifiedName!;
                        break;

                    case IneligibleRecordCase.HashMissing:
                        record.Hash = string.Empty;
                        break;

                    case IneligibleRecordCase.PathNotUnique:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(recordCase), recordCase, null);
                }

                realm.Write(r =>
                {
                    r.Add(record);

                    if (recordCase == IneligibleRecordCase.PathNotUnique)
                    {
                        r.Add(createEligibleRecord(
                            id: Guid.NewGuid(),
                            path: "CHARTSKIN/SOURCE"));
                    }
                });

                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);
                SkinManagedFolderMutationAuthorityResult result = authority.OpenDelete(Guid.NewGuid(), record.ID);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(recordCase == IneligibleRecordCase.PathNotUnique
                            ? SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordPathConflict
                            : SkinManagedFolderMutationAuthorityRejectionReason.ExistingRecordIneligible));
                    Assert.That(result.Session, Is.Null);
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestRenameTargetIsNfcCanonicalisedAndCasePreserved()
        {
            const string decomposed_target = "Cafe\u0301-TARGET";
            const string expected_target_path = "chartskin/Caf\u00e9-TARGET";

            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenRename(
                    Guid.NewGuid(),
                    recordId,
                    decomposed_target);

                using SkinManagedFolderMutationAuthoritySession session = result.Session!;

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(session.Kind, Is.EqualTo(SkinManagedFolderMutationKind.Rename));
                    Assert.That(session.TargetNameSlot, Is.Not.Null);
                    Assert.That(session.TargetNameSlot!.ManagedRelativePath, Is.EqualTo(expected_target_path));
                    Assert.That(session.TargetNameSlot.ManagedRootIdentity, Is.EqualTo(root_identity));
                    Assert.That(native.CapturedTargetPaths, Is.EqualTo(new[] { expected_target_path }));
                });
            });
        }

        [TestCase("")]
        [TestCase(".")]
        [TestCase("nested/target")]
        [TestCase("target\\child")]
        public void TestRenameInvalidTargetNameRejectedBeforeNativeOpen(string targetChildName)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenRename(
                    Guid.NewGuid(),
                    recordId,
                    targetChildName);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.InvalidTargetNameSlot));
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [TestCase("target", "chartskin/TARGET")]
        [TestCase("Cafe\u0301", "chartskin/Caf\u00e9")]
        public void TestRenameTargetRealmCollisionUsesCaseInsensitiveNfcIdentity(
            string targetChildName,
            string occupiedPath)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                addRecord(realm, path: occupiedPath);
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenRename(
                    Guid.NewGuid(),
                    recordId,
                    targetChildName);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied));
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [TestCase("source")]
        [TestCase("SOURCE")]
        public void TestRenameCannotReuseSourceNameSlot(string targetChildName)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenRename(
                    Guid.NewGuid(),
                    recordId,
                    targetChildName);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied));
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestRenameNativeTargetCollisionRejectsAndReleasesHeldSession()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority { RejectTargetCapture = true };
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenRename(
                    Guid.NewGuid(),
                    recordId,
                    "native-occupied");

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected));
                    Assert.That(result.Session, Is.Null);
                    Assert.That(native.OpenedSessions, Is.EqualTo(1));
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(native.DisposedSessions, Is.EqualTo(1));
                    Assert.That(native.CapturedSourcePaths, Is.EqualTo(new[] { source_path }));
                    Assert.That(native.CapturedTargetPaths, Is.EqualTo(new[] { "chartskin/native-occupied" }));
                });
            });
        }

        [TestCase(PostOpenRealmDriftCase.Owner)]
        [TestCase(PostOpenRealmDriftCase.Hash)]
        [TestCase(PostOpenRealmDriftCase.DeletePending)]
        [TestCase(PostOpenRealmDriftCase.TargetPathCollision)]
        public void TestPostOpenRealmDriftInvalidatesHeldAuthorityBeforeJournalPublication(PostOpenRealmDriftCase driftCase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);
                SkinManagedFolderMutationAuthorityResult result = driftCase == PostOpenRealmDriftCase.TargetPathCollision
                    ? authority.OpenRename(Guid.NewGuid(), recordId, "target")
                    : authority.OpenDelete(Guid.NewGuid(), recordId);

                Assert.That(result.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = result.Session!;

                realm.Write(r =>
                {
                    SkinInfo record = r.Find<SkinInfo>(recordId)!;

                    switch (driftCase)
                    {
                        case PostOpenRealmDriftCase.Owner:
                            record.FilesystemStorageAuthorityOwner = "foreign-owner";
                            break;

                        case PostOpenRealmDriftCase.Hash:
                            record.Hash = "changed-content-revision";
                            break;

                        case PostOpenRealmDriftCase.DeletePending:
                            record.DeletePending = true;
                            break;

                        case PostOpenRealmDriftCase.TargetPathCollision:
                            r.Add(createEligibleRecord(path: "chartskin/target"));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(driftCase), driftCase, null);
                    }
                });

                Assert.Multiple(() =>
                {
                    Assert.That(session.Validate(), Is.False);
                    Assert.Throws<InvalidOperationException>(() => session.PersistPreparedJournal());
                    Assert.That(journalStore.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(journalStore.Writes, Is.Empty);
                    Assert.That(journalStore.DeleteCalls, Is.Zero);
                    Assert.That(native.ActiveSessions, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestStagedImportSourceComesOnlyFromOperationDerivedHeldNativeAuthority()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenStagedImport(operationId, "target-a");
                using SkinManagedFolderMutationAuthoritySession session = result.Session!;

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(native.CapturedStagedOperationIds, Is.EqualTo(new[] { operationId }));
                    Assert.That(session.StagedSource, Is.Not.Null);
                    Assert.That(session.StagedSource!.OperationId, Is.EqualTo(operationId));
                    Assert.That(
                        session.StagedSource.RelativePath,
                        Is.EqualTo(SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(operationId)));
                    Assert.That(session.StagedSource.PhysicalIdentity, Is.EqualTo(staged_identity));
                    Assert.That(session.StagedSource.StagedRootIdentity, Is.EqualTo(staged_root_identity));
                    Assert.That(session.StagedSource.Validate(root_identity), Is.True);
                });
            });
        }

        [TestCase("")]
        [TestCase(".")]
        [TestCase("nested/target")]
        [TestCase("target\\child")]
        public void TestStagedImportInvalidTargetRejectedBeforeNativeOpenOrJournal(
            string targetChildName)
        {
            RunTestWithRealm((realm, storage) =>
            {
                var native = new FakeNativeAuthority();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);

                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(
                        Guid.NewGuid(),
                        targetChildName);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(
                            SkinManagedFolderMutationAuthorityRejectionReason
                                .InvalidTargetNameSlot));
                    Assert.That(native.OpenedSessions, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(journalStore.Writes, Is.Empty);
                });
            });
        }

        [TestCase("target", "chartskin/TARGET")]
        [TestCase("Cafe\u0301", "chartskin/Caf\u00e9")]
        public void TestStagedImportTargetRealmCollisionUsesCaseInsensitiveNfcIdentity(
            string targetChildName,
            string occupiedPath)
        {
            RunTestWithRealm((realm, storage) =>
            {
                addRecord(realm, path: occupiedPath);
                var native = new FakeNativeAuthority();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);

                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(
                        Guid.NewGuid(),
                        targetChildName);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(
                            SkinManagedFolderMutationAuthorityRejectionReason
                                .TargetNameSlotOccupied));
                    Assert.That(native.OpenedSessions, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(journalStore.Writes, Is.Empty);
                });
            });
        }

        [Test]
        public void TestStagedImportRecordIdCollisionRejectsBeforeNativeOpenOrJournal()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                addRecord(
                    realm,
                    operationId,
                    "chartskin/foreign-operation-id");
                var native = new FakeNativeAuthority();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);

                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(operationId, "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(
                            SkinManagedFolderMutationAuthorityRejectionReason
                                .TargetNameSlotOccupied));
                    Assert.That(native.OpenedSessions, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(journalStore.Writes, Is.Empty);
                });
            });
        }

        [Test]
        public void TestStagedImportPhysicalTargetCollisionRejectsBeforeJournalOrMove()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var native = new FakeNativeAuthority
                {
                    RejectTargetCapture = true,
                };
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);

                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(Guid.NewGuid(), "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(
                            SkinManagedFolderMutationAuthorityRejectionReason
                                .NativeAuthorityRejected));
                    Assert.That(
                        native.CapturedTargetPaths,
                        Is.EqualTo(new[] { "chartskin/target" }));
                    Assert.That(native.CapturedStagedOperationIds, Is.Empty);
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(journalStore.Writes, Is.Empty);
                });
            });
        }

        [TestCase(PostOpenStagedRealmDriftCase.RecordId)]
        [TestCase(PostOpenStagedRealmDriftCase.TargetPath)]
        [TestCase(PostOpenStagedRealmDriftCase.ExternalDeclaration)]
        public void TestPostOpenStagedRealmDriftInvalidatesAuthorityBeforeJournalOrMove(
            PostOpenStagedRealmDriftCase driftCase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                var native = new FakeNativeAuthority();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);
                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(operationId, "target");

                Assert.That(result.IsSuccess, Is.True);
                using SkinManagedFolderMutationAuthoritySession session =
                    result.Session!;

                realm.Write(r =>
                {
                    switch (driftCase)
                    {
                        case PostOpenStagedRealmDriftCase.RecordId:
                            r.Add(createEligibleRecord(
                                operationId,
                                "chartskin/foreign-operation-id"));
                            break;

                        case PostOpenStagedRealmDriftCase.TargetPath:
                            r.Add(createEligibleRecord(
                                path: "chartskin/target"));
                            break;

                        case PostOpenStagedRealmDriftCase.ExternalDeclaration:
                            SkinInfo external = createEligibleRecord(
                                path: "chartskin/external");
                            external.IsExternalFilesystemStorage = true;
                            r.Add(external);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(driftCase),
                                driftCase,
                                null);
                    }
                });

                Assert.Multiple(() =>
                {
                    Assert.That(session.Validate(), Is.False);
                    Assert.Throws<InvalidOperationException>(
                        () => session.PersistPreparedJournal());
                    Assert.That(
                        journalStore.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(journalStore.Writes, Is.Empty);
                    Assert.That(journalStore.DeleteCalls, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(native.ActiveSessions, Is.EqualTo(1));
                });
            });
        }

        [TestCase(InvalidStagedCapsuleCase.MissingSkinIni)]
        [TestCase(InvalidStagedCapsuleCase.InvalidUtf8)]
        [TestCase(InvalidStagedCapsuleCase.UnsafeMetadata)]
        public void TestStagedImportRejectsInvalidCapsuleMetadataBeforeJournalOrMove(
            InvalidStagedCapsuleCase invalidCase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                var native = new FakeNativeAuthority();
                native.StagedCapture.Dispose();
                native.StagedCapture = new SkinManagedFolderStagedSourceCapture(
                    staged_root_identity,
                    staged_identity,
                    staged_tree_fingerprint,
                    createInvalidStagedCapsule(invalidCase));
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    new SkinManagedFolderOperationCoordinator(),
                    native,
                    journalStore);

                SkinManagedFolderMutationAuthorityResult result =
                    authority.OpenStagedImport(Guid.NewGuid(), "target");

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(
                        result.RejectionReason,
                        Is.EqualTo(
                            SkinManagedFolderMutationAuthorityRejectionReason
                                .StagedSourceRejected));
                    Assert.That(
                        native.CapturedStagedOperationIds,
                        Has.Length.EqualTo(1));
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(native.MoveCalls, Is.Zero);
                    Assert.That(journalStore.Writes, Is.Empty);
                    Assert.That(
                        journalStore.Current.Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestStagedImportRejectsFixedExistingOrInvalidNativeSourceAuthority()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid existingId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult fixedId =
                    authority.OpenStagedImport(SkinInfo.OMS_SKIN, "target-a");
                SkinManagedFolderMutationAuthorityResult existingIdResult =
                    authority.OpenStagedImport(existingId, "target-b");

                native.StagedCapture.Dispose();
                native.StagedCapture = new SkinManagedFolderStagedSourceCapture(
                    new SkinManagedFolderPhysicalIdentity(99, 31, 32),
                    new SkinManagedFolderPhysicalIdentity(99, 33, 34),
                    staged_tree_fingerprint,
                    createStagedCapsule());
                SkinManagedFolderMutationAuthorityResult invalidNative =
                    authority.OpenStagedImport(Guid.NewGuid(), "target-c");

                Assert.Multiple(() =>
                {
                    Assert.That(fixedId.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.StagedSourceRejected));
                    Assert.That(existingIdResult.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.TargetNameSlotOccupied));
                    Assert.That(invalidNative.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.StagedSourceRejected));
                    Assert.That(native.ActiveSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestSuccessfulStagedImportPublishesOnlyExactAuthorityAndDoesNotCreateRealmRecord()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid operationId = Guid.NewGuid();
                int initialRecordCount = realm.Realm.All<SkinInfo>().Count();
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenStagedImport(
                    operationId,
                    "Imported");

                SkinManagedFolderMutationAuthoritySession session = result.Session!;
                SkinManagedFolderMutationJournal prepared = session.CreatePreparedJournal();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(session.Kind, Is.EqualTo(SkinManagedFolderMutationKind.StagedImport));
                    Assert.That(session.ExistingRecord, Is.Null);
                    Assert.That(session.TargetNameSlot!.ManagedRelativePath, Is.EqualTo("chartskin/Imported"));
                    Assert.That(session.StagedSource, Is.Not.Null);
                    Assert.That(session.NewRecordPublicationPlan, Is.Not.Null);
                    Assert.That(session.NewRecordPublicationPlan!.PlannedRecordId, Is.EqualTo(operationId));
                    Assert.That(prepared.Kind, Is.EqualTo(SkinManagedFolderMutationKind.StagedImport));
                    Assert.That(prepared.RecordId, Is.EqualTo(operationId));
                    Assert.That(prepared.ManagedRootIdentity, Is.EqualTo(root_identity));
                    Assert.That(prepared.TargetManagedRelativePath, Is.EqualTo("chartskin/Imported"));
                    Assert.That(prepared.StagedSourceIdentity, Is.EqualTo(staged_identity));
                    Assert.That(prepared.StagedRootIdentity, Is.EqualTo(staged_root_identity));
                    Assert.That(
                        prepared.NewRecordPublicationPlanVersion,
                        Is.EqualTo(SkinManagedFolderMutationJournal.NEW_RECORD_PUBLICATION_PLAN_VERSION));
                    Assert.That(realm.Realm.All<SkinInfo>().Count(), Is.EqualTo(initialRecordCount));
                    Assert.That(native.ActiveSessions, Is.EqualTo(1));
                    Assert.That(session.Validate(), Is.True);
                });

                session.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(realm.Realm.All<SkinInfo>().Count(), Is.EqualTo(initialRecordCount));
                });
            });
        }

        [Test]
        public void TestRecoveryFreezeRejectsMutationBeforeOpeningNativeAuthority()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                coordinator.FreezePaths(new[] { source_path });
                var native = new FakeNativeAuthority();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    createEmptyJournalStore());

                SkinManagedFolderMutationAuthorityResult result = authority.OpenDelete(Guid.NewGuid(), recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.RecoveryPending));
                    Assert.That(native.OpenedSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestDisposingSessionWithPreparedDurableIntentLeavesExactRecoveryFreeze()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var journalStore = createEmptyJournalStore();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    new FakeNativeAuthority(),
                    journalStore);
                SkinManagedFolderMutationAuthoritySession session =
                    authority.OpenDelete(Guid.NewGuid(), recordId).Session!;

                session.PersistPreparedJournal();
                session.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(journalStore.Current.IsLoaded, Is.True);
                    Assert.That(
                        journalStore.Current.Journal!.Phase,
                        Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.False);
                    Assert.That(coordinator.IsMutationBlocked, Is.True);
                });
            });
        }

        [Test]
        public void TestExternalRealmDeclarationOverlappingManagedNamespaceRejectsAuthority()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                realm.Write(r => r.Add(new SkinInfo("External overlap", "Foreign", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                {
                    FilesystemStoragePath = storage.GetFullPath("chartskin/external-claim"),
                    IsExternalFilesystemStorage = true,
                }));
                var native = new FakeNativeAuthority();
                var authority = createAuthority(realm, storage, native);

                SkinManagedFolderMutationAuthorityResult result = authority.OpenDelete(Guid.NewGuid(), recordId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.ExternalRegistryRejected));
                    Assert.That(native.OpenedSessions, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestHeldMutationSessionBlocksScannerAndSelectionParticipantsUntilDisposed()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    createEmptyJournalStore());
                SkinManagedFolderMutationAuthorityResult authorityResult = authority.OpenDelete(
                    Guid.NewGuid(),
                    recordId);
                SkinManagedFolderMutationAuthoritySession session = authorityResult.Session!;
                using var discoveryCompleted = new ManualResetEventSlim();
                using var selectionEntered = new ManualResetEventSlim();
                var discovery = new SkinManagedFolderDiscovery(
                    "chartskin/scanned",
                    "Scanned",
                    "Scanner",
                    "scanner-revision");
                var scanner = new SkinManagedFolderScanner(
                    realm,
                    new SignallingDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            new[] { discovery.ManagedRelativePath },
                            new[] { discovery }),
                        discoveryCompleted),
                    coordinator);

                Task<SkinManagedFolderScanResult> scanTask = Task.Run(() => scanner.Scan());
                Task selectionParticipant = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease selectionLease = coordinator.Enter();
                    selectionEntered.Set();
                });

                bool scannerPreparedWhileMutationHeld = discoveryCompleted.Wait(TimeSpan.FromMilliseconds(250));
                bool scannerCompletedWhileMutationHeld = scanTask.Wait(TimeSpan.FromMilliseconds(250));
                bool selectionEnteredWhileMutationHeld = selectionEntered.Wait(TimeSpan.FromMilliseconds(250));

                Assert.That(Task.Run(session.Dispose).Wait(TimeSpan.FromSeconds(10)), Is.True);

                bool scannerCompletedAfterDispose = scanTask.Wait(TimeSpan.FromSeconds(10));
                bool selectionCompletedAfterDispose = selectionParticipant.Wait(TimeSpan.FromSeconds(10));
                SkinManagedFolderScanResult scanResult = scanTask.GetAwaiter().GetResult();
                realm.Run(r => r.Refresh());

                Assert.Multiple(() =>
                {
                    Assert.That(authorityResult.IsSuccess, Is.True);
                    Assert.That(scannerPreparedWhileMutationHeld, Is.False);
                    Assert.That(scannerCompletedWhileMutationHeld, Is.False);
                    Assert.That(selectionEnteredWhileMutationHeld, Is.False);
                    Assert.That(scannerCompletedAfterDispose, Is.True);
                    Assert.That(selectionCompletedAfterDispose, Is.True);
                    Assert.That(discoveryCompleted.IsSet, Is.True);
                    Assert.That(selectionEntered.IsSet, Is.True);
                    Assert.That(scanResult.IsSuccess, Is.True);
                    Assert.That(scanResult.Added, Is.EqualTo(1));
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(
                        realm.Realm.All<SkinInfo>()
                             .Any(record => record.FilesystemStoragePath == discovery.ManagedRelativePath),
                        Is.True);
                });
            });
        }

        [Test]
        public void TestNativeDisposeFailureStillReleasesCoordinatorAcrossThreads()
        {
            RunTestWithRealm((realm, storage) =>
            {
                Guid recordId = addRecord(realm);
                var native = new FakeNativeAuthority { ThrowOnDispose = true };
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var authority = new SkinManagedFolderMutationAuthority(
                    realm,
                    storage,
                    coordinator,
                    native,
                    createEmptyJournalStore());
                SkinManagedFolderMutationAuthoritySession session = authority.OpenDelete(Guid.NewGuid(), recordId).Session!;
                Exception? disposeFailure = null;

                Task disposeTask = Task.Run(() =>
                {
                    try
                    {
                        session.Dispose();
                    }
                    catch (Exception exception)
                    {
                        disposeFailure = exception;
                    }
                });

                Assert.That(disposeTask.Wait(TimeSpan.FromSeconds(10)), Is.True);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using SkinManagedFolderOperationCoordinator.Lease subsequent = coordinator.Enter(cancellation.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(disposeFailure, Is.TypeOf<InvalidOperationException>());
                    Assert.That(native.ActiveSessions, Is.Zero);
                    Assert.That(native.DisposedSessions, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestAuthorityDiagnosticsDoNotExposePathsPhysicalIdsOrOperationRecordIds()
        {
            RunTestWithRealm((realm, storage) =>
            {
                const string secret_source_path = "chartskin/authority-secret-source";
                const string secret_target_path = "chartskin/authority-secret-target";
                Guid operationId = Guid.Parse("a52bab4e-cbb3-4ab1-b793-10acec825de4");
                Guid recordId = Guid.Parse("391c7de9-0a4e-4e54-9a2e-2e36bf8cb54b");
                var secretIdentity = new SkinManagedFolderPhysicalIdentity(11, 123456789, 456789123);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                using SkinManagedFolderOperationCoordinator.Lease coordinatorLease = coordinator.EnterMutation();
                var registry = new SkinExternalFolderRegistryService(
                    realm,
                    storage,
                    coordinator,
                    new SkinExternalFolderCaptureService());
                SkinExternalFolderRegistryCaptureResult registryCapture = registry.CaptureExactSet(coordinatorLease);
                Assert.That(registryCapture.IsSuccess, Is.True);
                var native = new FakeNativeAuthority();
                ISkinManagedFolderMutationNativeSession nativeSession = native.Open(CancellationToken.None);
                var target = new SkinManagedFolderTargetNameSlot(secret_target_path, root_identity);
                var existing = new SkinManagedFolderExistingRecordAuthority(
                    recordId,
                    secret_source_path,
                    secretIdentity,
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                var store = createEmptyJournalStore();
                using var session = new SkinManagedFolderMutationAuthoritySession(
                    operationId,
                    SkinManagedFolderMutationKind.Rename,
                    existing,
                    target,
                    null,
                    null,
                    coordinator,
                    store,
                    coordinatorLease,
                    nativeSession,
                    registryCapture.Snapshot!,
                    _ => true);
                SkinManagedFolderMutationAuthorityResult success = SkinManagedFolderMutationAuthorityResult.Success(session);
                SkinManagedFolderMutationAuthorityResult rejection = SkinManagedFolderMutationAuthorityResult.Reject(
                    SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected);
                var exception = new SkinManagedFolderMutationNativeAuthorityException();
                SkinManagedFolderMutationJournal journal = SkinManagedFolderMutationJournal.CreatePreparedRename(
                    operationId,
                    recordId,
                    root_identity,
                    secret_source_path,
                    secretIdentity,
                    secret_target_path);

                string[] diagnostics =
                {
                secretIdentity.ToString(),
                target.ToString(),
                existing.ToString(),
                session.ToString(),
                success.ToString(),
                rejection.ToString(),
                exception.ToString(),
                journal.ToString(),
            };
                string[] secrets =
                {
                secret_source_path,
                secret_target_path,
                operationId.ToString(),
                operationId.ToString("N"),
                recordId.ToString(),
                recordId.ToString("N"),
                secretIdentity.VolumeSerialNumber.ToString(),
                secretIdentity.FileIdPart0.ToString(),
                secretIdentity.FileIdPart1.ToString(),
            };

                Assert.Multiple(() =>
                {
                    foreach (string diagnostic in diagnostics)
                    {
                        foreach (string secret in secrets)
                            Assert.That(diagnostic, Does.Not.Contain(secret));
                    }
                });
            });
        }

        private static SkinManagedFolderMutationAuthority createAuthority(
            RealmAccess realm,
            Framework.Platform.Storage storage,
            FakeNativeAuthority native)
            => new SkinManagedFolderMutationAuthority(
                realm,
                storage,
                new SkinManagedFolderOperationCoordinator(),
                native,
                createEmptyJournalStore());

        private static MemoryMutationJournalStore createEmptyJournalStore()
            => new MemoryMutationJournalStore(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing));

        private static Guid addRecord(
            RealmAccess realm,
            Guid? id = null,
            string? path = source_path)
        {
            SkinInfo record = createEligibleRecord(id, path);
            realm.Write(r => r.Add(record));
            return record.ID;
        }

        private static SkinInfo createEligibleRecord(
            Guid? id = null,
            string? path = source_path)
            => new SkinInfo("Managed", "Scanner", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = id ?? Guid.NewGuid(),
                Hash = "content-revision",
                FilesystemStoragePath = path,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                IsExternalFilesystemStorage = false,
                Protected = false,
                DeletePending = false,
            };

        private static SkinPackageRevisionCapsule createStagedCapsule()
        {
            SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(new[]
            {
                SkinPackageCapturedEntry.CreateFile(
                    "skin.ini",
                    Encoding.UTF8.GetBytes("[General]\nName: Staged package\nAuthor: OMS Test\n")),
            });

            return result.IsSuccess && result.Capsule != null
                ? result.Capsule
                : throw new InvalidOperationException("The staged test capsule could not be created.");
        }

        private static SkinPackageRevisionCapsule createInvalidStagedCapsule(
            InvalidStagedCapsuleCase invalidCase)
        {
            SkinPackageCapturedEntry entry = invalidCase switch
            {
                InvalidStagedCapsuleCase.MissingSkinIni =>
                    SkinPackageCapturedEntry.CreateFile(
                        "notes.txt",
                        Encoding.UTF8.GetBytes("No metadata file.")),

                InvalidStagedCapsuleCase.InvalidUtf8 =>
                    SkinPackageCapturedEntry.CreateFile(
                        "skin.ini",
                        new byte[] { 0xc3, 0x28 }),

                InvalidStagedCapsuleCase.UnsafeMetadata =>
                    SkinPackageCapturedEntry.CreateFile(
                        "skin.ini",
                        Encoding.UTF8.GetBytes(
                            "[General]\nName: Unsafe\u0001Name\nAuthor: OMS Test\n")),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(invalidCase),
                    invalidCase,
                    null),
            };
            SkinPackageRevisionCapsuleCreationResult result =
                SkinPackageRevisionCapsuleFactory.Create(new[] { entry });

            return result.IsSuccess && result.Capsule != null
                ? result.Capsule
                : throw new InvalidOperationException(
                    "The invalid staged test capsule could not be created.");
        }

        public enum IneligibleRecordCase
        {
            OwnerMissing,
            OwnerCaseMismatch,
            FolderMissing,
            FolderOutsideManagedRoot,
            FolderNotDirectChild,
            FolderNotNfcCanonical,
            RealmFilesPresent,
            ExternalFolder,
            Protected,
            FixedId,
            DeletePending,
            InstantiationNotAllowlisted,
            HashMissing,
            PathNotUnique,
        }

        public enum PostOpenRealmDriftCase
        {
            Owner,
            Hash,
            DeletePending,
            TargetPathCollision,
        }

        public enum PostOpenStagedRealmDriftCase
        {
            RecordId,
            TargetPath,
            ExternalDeclaration,
        }

        public enum InvalidStagedCapsuleCase
        {
            MissingSkinIni,
            InvalidUtf8,
            UnsafeMetadata,
        }

        private sealed class FakeNativeAuthority : ISkinManagedFolderMutationNativeAuthority
        {
            private readonly object sync = new object();
            private readonly List<string> capturedSourcePaths = new List<string>();
            private readonly List<string> capturedTargetPaths = new List<string>();
            private readonly List<Guid> capturedStagedOperationIds = new List<Guid>();
            private int activeSessions;
            private int disposedSessions;
            private int openedSessions;

            public bool RejectTargetCapture { get; set; }
            public bool ThrowOnDispose { get; set; }
            public SkinManagedFolderStagedSourceCapture StagedCapture { get; set; } =
                new SkinManagedFolderStagedSourceCapture(
                    staged_root_identity,
                    staged_identity,
                    staged_tree_fingerprint,
                    createStagedCapsule());

            public int ActiveSessions => Volatile.Read(ref activeSessions);
            public int DisposedSessions => Volatile.Read(ref disposedSessions);
            public int OpenedSessions => Volatile.Read(ref openedSessions);
            public int MoveCalls { get; private set; }

            public string[] CapturedSourcePaths
            {
                get
                {
                    lock (sync)
                        return capturedSourcePaths.ToArray();
                }
            }

            public string[] CapturedTargetPaths
            {
                get
                {
                    lock (sync)
                        return capturedTargetPaths.ToArray();
                }
            }

            public Guid[] CapturedStagedOperationIds
            {
                get
                {
                    lock (sync)
                        return capturedStagedOperationIds.ToArray();
                }
            }

            public ISkinManagedFolderMutationNativeSession Open(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref openedSessions);
                Interlocked.Increment(ref activeSessions);
                return new FakeNativeSession(this);
            }

            private void captureSource(string managedRelativePath)
            {
                lock (sync)
                    capturedSourcePaths.Add(managedRelativePath);
            }

            private void captureTarget(string managedRelativePath)
            {
                lock (sync)
                    capturedTargetPaths.Add(managedRelativePath);
            }

            private void captureStaged(Guid operationId)
            {
                lock (sync)
                    capturedStagedOperationIds.Add(operationId);
            }

            private void sessionDisposed()
            {
                Interlocked.Decrement(ref activeSessions);
                Interlocked.Increment(ref disposedSessions);
            }

            private sealed class FakeNativeSession : ISkinManagedFolderMutationNativeSession
            {
                private readonly FakeNativeAuthority owner;
                private int disposed;

                public SkinManagedFolderPhysicalIdentity ManagedRootIdentity => root_identity;

                public SkinFolderPhysicalAncestryProof ManagedRootAncestryProof { get; } =
                    new SkinFolderPhysicalAncestryProof(new[]
                    {
                        new SkinManagedFolderPhysicalIdentity(11, 1, 1),
                        root_identity,
                    });

                public FakeNativeSession(FakeNativeAuthority owner)
                {
                    this.owner = owner;
                }

                public SkinManagedFolderPhysicalIdentity CaptureExistingSource(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    owner.captureSource(managedRelativePath);
                    return source_identity;
                }

                public string GetCapturedDeleteSourceNodeManifest(
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    return SkinManagedFolderDeleteManifest.Create(
                        new[] { new string('a', 64) });
                }

                public SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
                    string managedRelativePath,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    owner.captureTarget(managedRelativePath);

                    if (owner.RejectTargetCapture)
                        throw new SkinManagedFolderMutationNativeAuthorityException();

                    return new SkinManagedFolderTargetNameSlot(managedRelativePath, ManagedRootIdentity);
                }

                public SkinManagedFolderStagedSourceCapture CaptureStagedSource(
                    Guid operationId,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    owner.captureStaged(operationId);
                    return owner.StagedCapture;
                }

                public SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    throw new NotSupportedException();
                }

                public SkinManagedFolderStagedImportFilesystemResult MoveCapturedStagedSourceToTarget(
                    SkinManagedFolderTargetNameSlot targetNameSlot,
                    string expectedContentRevision,
                    string expectedTreeFingerprint,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    owner.MoveCalls++;
                    throw new NotSupportedException();
                }

                public SkinManagedFolderRenameInspection InspectRenameState(
                    string sourceManagedRelativePath,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    throw new NotSupportedException();
                }

                public SkinManagedFolderStagedImportInspection InspectStagedImportState(
                    Guid operationId,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    throw new NotSupportedException();
                }

                public void CleanupExactStagedSource(
                    Guid operationId,
                    string targetManagedRelativePath,
                    SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                    SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                    CancellationToken cancellationToken)
                {
                    ensureHeld(cancellationToken);
                    throw new NotSupportedException();
                }

                public void ValidateCompleteAndStable(CancellationToken cancellationToken)
                    => ensureHeld(cancellationToken);

                public void Dispose()
                {
                    if (Interlocked.Exchange(ref disposed, 1) == 0)
                    {
                        owner.sessionDisposed();

                        if (owner.ThrowOnDispose)
                            throw new InvalidOperationException("injected native dispose failure");
                    }
                }

                private void ensureHeld(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                }
            }
        }

        private sealed class TrackingDisposable : IDisposable
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public void Dispose() => Interlocked.Increment(ref disposeCount);
        }

        private sealed class SignallingDiscoverySource : ISkinManagedFolderDiscoverySource
        {
            private readonly SkinManagedFolderDiscoverySnapshot snapshot;
            private readonly ManualResetEventSlim discoveryCompleted;

            public SignallingDiscoverySource(
                SkinManagedFolderDiscoverySnapshot snapshot,
                ManualResetEventSlim discoveryCompleted)
            {
                this.snapshot = snapshot;
                this.discoveryCompleted = discoveryCompleted;
            }

            public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                discoveryCompleted.Set();
                return snapshot;
            }
        }
    }
}
