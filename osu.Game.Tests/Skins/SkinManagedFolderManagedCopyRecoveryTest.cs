// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class SkinManagedFolderManagedCopyRecoveryTest : RealmTest
    {
        private const string target_path = "chartskin/recovered-managed-copy";
        private const string registry_digest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        private static readonly byte[] skin_ini =
            "[General]\nName: Recovered copy\nAuthor: OMS tests\n"u8.ToArray();

        [Test]
        public void TestPreparedWithoutProvisionalRollsBackWithoutFilesystemMutation()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(realm, storage, package, createProvisional: false);
                var store = new MemoryMutationJournalStore(intent.Prepared);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out _);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(intent.OperationId), Is.Null);
                });
            });
        }

        [Test]
        public void TestPreparedWithUnboundProvisionalIsAmbiguousAndPreserved()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(realm, storage, package, createProvisional: true);
                var store = new MemoryMutationJournalStore(intent.Prepared);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out SkinManagedFolderOperationCoordinator coordinator);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.True);
                    Assert.That(coordinator.IsMutationBlocked, Is.True);
                });
            });
        }

        [Test]
        public void TestCopyingEmptyDurableRootRollsBackExactly()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(realm, storage, package, createProvisional: true);
                SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value);
                var store = new MemoryMutationJournalStore(copying);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out _);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                    Assert.That(store.Writes.Select(write => write.Phase),
                        Is.EqualTo(new[] { SkinManagedFolderMutationPhase.RolledBack }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                });
            });
        }

        [Test]
        public void TestCopyingNonEmptyPartialHasNoDurableChildOwnershipAndFreezes()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(realm, storage, package, createProvisional: true);
                File.WriteAllBytes(Path.Combine(intent.OperationRoot, "skin.ini"), skin_ini[..1]);
                SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value);
                var store = new MemoryMutationJournalStore(copying);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out SkinManagedFolderOperationCoordinator coordinator);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(File.Exists(Path.Combine(intent.OperationRoot, "skin.ini")), Is.True);
                    Assert.That(coordinator.IsMutationBlocked, Is.True);
                });
            });
        }

        [Test]
        public void TestCompleteCopyingRecoversThroughProvisionalMoveRealmAndTerminalDelete()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(
                    realm,
                    storage,
                    package,
                    createProvisional: true,
                    writeComplete: true);
                SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value);
                var store = new MemoryMutationJournalStore(copying);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out SkinManagedFolderOperationCoordinator coordinator);
                SkinInfo? published = realm.Realm.Find<SkinInfo>(intent.OperationId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Writes.Select(write => write.Phase), Is.EqualTo(new[]
                    {
                        SkinManagedFolderMutationPhase.ProvisionalReady,
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                    Assert.That(File.ReadAllBytes(Path.Combine(storage.GetFullPath(string.Empty), target_path, "skin.ini")),
                        Is.EqualTo(skin_ini));
                    Assert.That(published, Is.Not.Null);
                    Assert.That(published!.ID, Is.EqualTo(intent.OperationId));
                    Assert.That(published.FilesystemStoragePath, Is.EqualTo(target_path));
                    Assert.That(
                        SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(published),
                        Is.EqualTo(store.Writes[0].NewRecordPublicationFingerprint));
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestExternalRecordFingerprintDriftPreventsCompleteCopyRecovery()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using TestPackage package = createPackage();
                TestIntent intent = prepareIntent(
                    realm,
                    storage,
                    package,
                    createProvisional: true,
                    writeComplete: true);
                SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value);
                realm.Write(r => r.Find<SkinInfo>(intent.ExternalRecordId)!.Name = "changed after durable intent");
                var store = new MemoryMutationJournalStore(copying);

                SkinManagedFolderMutationRecoveryResult result = recover(
                    realm,
                    storage,
                    store,
                    out _);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(intent.OperationId), Is.Null);
                });
            });
        }

        [Test]
        public void TestProductionAuthorityRecoversCompleteCopyAgainstHeldExactExternalSet()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: true);
                SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value);
                var store = new MemoryMutationJournalStore(copying);

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                Assert.Multiple(() =>
                {
                    Assert.That(intent.Prepared.ExternalRegistryGeneration, Is.GreaterThan(0));
                    Assert.That(intent.Prepared.ExternalCollisionDisposition,
                        Is.EqualTo(SkinExternalCollisionDisposition.ExactRegisteredExternalSet));
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Writes.Select(write => write.Phase), Is.EqualTo(new[]
                    {
                        SkinManagedFolderMutationPhase.ProvisionalReady,
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                    Assert.That(File.ReadAllBytes(Path.Combine(
                        storage.GetFullPath(string.Empty),
                        target_path,
                        "skin.ini")), Is.EqualTo(skin_ini));
                    Assert.That(realm.Realm.Find<SkinInfo>(intent.OperationId), Is.Not.Null);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestProductionAuthorityRollsBackOnlyHeldEmptyProvisional()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: false);
                var store = new MemoryMutationJournalStore(intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value));

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                    Assert.That(store.Writes.Select(write => write.Phase),
                        Is.EqualTo(new[] { SkinManagedFolderMutationPhase.RolledBack }));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [TestCase("Complete", true)]
        [TestCase("Empty", true)]
        [TestCase("Partial", false)]
        public void TestProductionAuthoritySupportInspectionIsReadOnlyAndOnlyOffersUniqueRetry(
            string provisionalState,
            bool expectedCanRetry)
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: provisionalState == "Complete");

                if (provisionalState == "Partial")
                    File.WriteAllBytes(Path.Combine(intent.OperationRoot, "skin.ini"), skin_ini[..1]);

                var store = new MemoryMutationJournalStore(intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value));
                SkinManagedFolderMutationRecovery recovery = createProductionRecovery(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                FolderSkinJournalSupportSnapshot support = recovery.InspectSupportSnapshot();

                Assert.Multiple(() =>
                {
                    Assert.That(support.CanRetry, Is.EqualTo(expectedCanRetry));
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(intent.OperationId), Is.Null);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [Test]
        public void TestProductionAuthorityRejectsExternalRegistryGenerationDrift()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: true);
                string addedPath = createExternalSource(storage, $"added-{Guid.NewGuid():N}");
                addExternalRecord(realm, addedPath);
                var store = new MemoryMutationJournalStore(intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value));

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                assertProductionDriftBlocked(result, store, coordinator, intent);
            });
        }

        [Test]
        public void TestProductionAuthorityRejectsExternalRecordFingerprintDrift()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: true);
                realm.Write(r => r.Find<SkinInfo>(intent.ExternalRecordId)!.Name = "drifted record");
                var store = new MemoryMutationJournalStore(intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value));

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                assertProductionDriftBlocked(result, store, coordinator, intent);
            });
        }

        [Test]
        public void TestProductionAuthorityRejectsExternalPhysicalRootReplacement()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: true);
                Directory.Delete(intent.ExternalPath, recursive: true);
                Directory.CreateDirectory(intent.ExternalPath);
                File.WriteAllBytes(Path.Combine(intent.ExternalPath, "skin.ini"), skin_ini);
                var store = new MemoryMutationJournalStore(intent.Prepared.WithCopying(
                    intent.ProvisionalIdentity!.Value));

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                assertProductionDriftBlocked(result, store, coordinator, intent);
            });
        }

        [TestCase("ProvisionalReady")]
        [TestCase("FilesystemApplied")]
        [TestCase("FilesystemAppliedPublished")]
        [TestCase("RealmApplied")]
        public void TestProductionAuthorityClosesRestartedForwardPhases(string restartPhase)
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                ProductionIntent intent = prepareProductionIntent(
                    realm,
                    storage,
                    coordinator,
                    native,
                    registry,
                    createProvisional: true,
                    writeComplete: true);
                SkinManagedFolderMutationJournal restarted = createForwardRestartJournal(
                    realm,
                    native,
                    intent,
                    restartPhase);
                var store = new MemoryMutationJournalStore(restarted);

                SkinManagedFolderMutationRecoveryResult result = recoverProduction(
                    realm,
                    coordinator,
                    native,
                    registry,
                    store);

                SkinManagedFolderMutationPhase[] expectedWrites = restartPhase switch
                {
                    "ProvisionalReady" => new[]
                    {
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    },
                    "FilesystemApplied" or "FilesystemAppliedPublished" => new[]
                    {
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    },
                    "RealmApplied" => new[] { SkinManagedFolderMutationPhase.Committed },
                    _ => throw new AssertionException("unknown restart phase"),
                };

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                    Assert.That(store.Writes.Select(write => write.Phase), Is.EqualTo(expectedWrites));
                    Assert.That(store.DeleteCalls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(intent.OperationRoot), Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(intent.OperationId), Is.Not.Null);
                    Assert.That(coordinator.IsMutationBlocked, Is.False);
                });
            });
        }

        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION)]
        public void TestProductionAuthorityBlocksPreC1RecoveryWhenRealExternalSetIsNonEmpty(int version)
        {
            RunTestWithRealm((realm, storage) =>
            {
                string externalPath = createExternalSource(storage, $"legacy-{version}-{Guid.NewGuid():N}");
                addExternalRecord(realm, externalPath);
                Directory.CreateDirectory(Path.Combine(
                    storage.GetFullPath(string.Empty),
                    SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                var coordinator = new SkinManagedFolderOperationCoordinator();
                ISkinManagedFolderMutationNativeAuthority native = createNativeAuthority(storage);
                SkinExternalFolderRegistryService registry = createProductionRegistry(
                    realm,
                    storage,
                    coordinator);
                var productionAuthority = new SkinManagedFolderMutationRecoveryAuthority(
                    coordinator,
                    native,
                    registry);
                SkinManagedFolderMutationJournal current = SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new SkinManagedFolderPhysicalIdentity(1, 10, 11),
                    "chartskin/legacy-source",
                    new SkinManagedFolderPhysicalIdentity(1, 12, 13),
                    "chartskin/legacy-target");
                using var fixtureStorage = SkinManagedFolderMutationJournalTest.createStorage();
                SkinManagedFolderMutationJournalTest.writeVersionFixture(fixtureStorage, current, version);
                var durableStore = new SkinManagedFolderMutationJournalStore(fixtureStorage);
                SkinManagedFolderMutationJournal legacy = durableStore.Load().Journal!;

                using (SkinManagedFolderOperationCoordinator.Lease lease = coordinator.EnterMutation())
                using (ISkinManagedFolderMutationRecoveryAuthoritySession? session = productionAuthority.TryOpen(lease))
                {
                    Assert.That(session, Is.Not.Null, "the real external exact set must be capturable");
                    Assert.That(session!.Validate(), Is.True);
                    Assert.That(session.IsExactFor(legacy), Is.False);
                }

                var store = new MemoryMutationJournalStore(legacy);
                var handler = new NeverInspectHeldHandler(SkinManagedFolderMutationKind.Rename);
                SkinManagedFolderMutationRecoveryResult result = new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    productionAuthority).Recover();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(handler.InspectCalls, Is.Zero);
                    Assert.That(store.Writes, Is.Empty);
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(coordinator.IsMutationBlocked, Is.True);
                });
            });
        }

        private static ISkinManagedFolderMutationNativeAuthority createNativeAuthority(
            osu.Framework.Platform.Storage storage)
            => new WindowsSkinManagedFolderMutationNativeAuthority(
                storage.GetFullPath(string.Empty),
                new NativeWindowsSkinPackageCaptureFileSystem());

        private static SkinExternalFolderRegistryService createProductionRegistry(
            RealmAccess realm,
            osu.Framework.Platform.Storage storage,
            SkinManagedFolderOperationCoordinator coordinator)
            => new SkinExternalFolderRegistryService(
                realm,
                storage,
                coordinator,
                new SkinExternalFolderCaptureService());

        private static SkinManagedFolderMutationRecoveryResult recoverProduction(
            RealmAccess realm,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationNativeAuthority native,
            SkinExternalFolderRegistryService registry,
            MemoryMutationJournalStore store)
            => createProductionRecovery(
                realm,
                coordinator,
                native,
                registry,
                store).Recover();

        private static SkinManagedFolderMutationRecovery createProductionRecovery(
            RealmAccess realm,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationNativeAuthority native,
            SkinExternalFolderRegistryService registry,
            MemoryMutationJournalStore store)
        {
            var handler = new SkinManagedFolderManagedCopyRecoveryHandler(realm, native);
            var authority = new SkinManagedFolderMutationRecoveryAuthority(
                coordinator,
                native,
                registry);
            return new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                handler,
                authority);
        }

        private static SkinManagedFolderMutationJournal createForwardRestartJournal(
            RealmAccess realm,
            ISkinManagedFolderMutationNativeAuthority native,
            ProductionIntent intent,
            string restartPhase)
        {
            SkinManagedFolderMutationJournal copying = intent.Prepared.WithCopying(
                intent.ProvisionalIdentity!.Value);

            using ISkinManagedFolderMutationNativeSession session = native.Open(CancellationToken.None);
            SkinManagedCopyLogicalManifest.TryParse(
                copying.ManagedCopyLogicalManifest!,
                copying.ManagedCopyLogicalManifestDigest!,
                out SkinManagedCopyLogicalManifest? manifest);
            Assert.That(manifest, Is.Not.Null);
            SkinManagedCopyProvisionalInspection inspected = session.InspectManagedCopyProvisionalState(
                copying.OperationId,
                copying.TargetManagedRelativePath!,
                copying.StagedRootIdentity!.Value,
                copying.StagedSourceIdentity,
                manifest!,
                copying.StagedSourceContentRevision!,
                CancellationToken.None);
            Assert.That(inspected.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.Complete));
            SkinManagedFolderNewRecordPublicationData publication =
                new SkinManagedFolderNewRecordPublicationPlan(
                    copying.OperationId,
                    copying.TargetManagedRelativePath!,
                    copying.ManagedRootIdentity)
                .CreatePublicationData(inspected.PackageMetadata!);
            SkinManagedFolderMutationJournal provisional = copying.WithProvisionalReady(
                copying.StagedSourceIdentity!.Value,
                inspected.TreeFingerprint!,
                publication.Fingerprint);

            if (restartPhase == "ProvisionalReady")
                return provisional;

            SkinManagedFolderTargetNameSlot target = session.CaptureAbsentTargetNameSlot(
                copying.TargetManagedRelativePath!,
                CancellationToken.None);

            using (SkinManagedFolderStagedImportFilesystemResult moved =
                   session.MoveCapturedStagedSourceToTarget(
                       target,
                       copying.StagedSourceContentRevision!,
                       inspected.TreeFingerprint!,
                       CancellationToken.None))
            {
                Assert.That(moved.TargetIdentity, Is.EqualTo(copying.StagedSourceIdentity));
            }

            SkinManagedFolderMutationJournal filesystem = provisional.WithFilesystemApplied(
                copying.StagedSourceIdentity,
                publication.Fingerprint);

            if (restartPhase == "FilesystemApplied")
                return filesystem;

            realm.Write(r => r.Add(publication.CreateRecord()));

            return restartPhase switch
            {
                "FilesystemAppliedPublished" => filesystem,
                "RealmApplied" => filesystem.WithRealmApplied(),
                _ => throw new AssertionException("unknown restart phase"),
            };
        }

        private static ProductionIntent prepareProductionIntent(
            RealmAccess realm,
            osu.Framework.Platform.Storage storage,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationNativeAuthority native,
            SkinExternalFolderRegistryService registry,
            bool createProvisional,
            bool writeComplete)
        {
            string dataRoot = storage.GetFullPath(string.Empty);
            Directory.CreateDirectory(Path.Combine(
                dataRoot,
                SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
            Guid operationId = Guid.NewGuid();
            string externalPath = createExternalSource(
                storage,
                $"managed-copy-{operationId:N}");
            Guid externalRecordId = addExternalRecord(realm, externalPath);
            SkinInfo external = realm.Realm.Find<SkinInfo>(externalRecordId)!.Detach();
            SkinFilesystemStorageResolution sourceResolution =
                SkinFilesystemStorageResolver.ResolveExisting(external, storage);
            Assert.That(sourceResolution.ExternalCaptureRequest, Is.Not.Null);
            var captureService = new SkinExternalFolderCaptureService();
            SkinExternalPackageCaptureResult sourceCapture = captureService.CaptureHeld(
                sourceResolution.ExternalCaptureRequest);
            Assert.That(sourceCapture.IsSuccess, Is.True, sourceCapture.ToString());

            using ISkinExternalPackageCaptureSession sourceSession = sourceCapture.Session!;
            using SkinPackageRevisionCapsule capsule = sourceSession.TakeCapsule();
            SkinManagedCopyLogicalManifest logicalManifest =
                SkinManagedCopyLogicalManifest.Create(sourceSession.LogicalManifest);
            string externalCaptureFingerprint = sourceSession.CaptureFingerprint;
            string externalRecordFingerprint =
                SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(external);
            SkinExternalRegistryJournalBinding binding;

            using (SkinManagedFolderOperationCoordinator.Lease lease = coordinator.EnterMutation())
            using (ISkinManagedFolderMutationNativeSession managed = native.Open(CancellationToken.None))
            {
                SkinExternalFolderRegistryCaptureResult captured = registry.CaptureExactSet(
                    lease,
                    new[] { managed.ManagedRootAncestryProof });
                Assert.That(captured.IsSuccess, Is.True, captured.ToString());

                using SkinExternalFolderRegistrySnapshot snapshot = captured.Snapshot!;
                Assert.That(snapshot.Count, Is.EqualTo(1));
                Assert.That(snapshot.ContainsRecordId(externalRecordId), Is.True);
                Assert.That(snapshot.Validate(lease), Is.True);
                binding = new SkinExternalRegistryJournalBinding(
                    snapshot.ExternalRegistryGeneration,
                    snapshot.ExternalRegistryDigest,
                    SkinExternalCollisionDisposition.ExactRegisteredExternalSet);
            }

            SkinManagedFolderPhysicalIdentity stagedRootIdentity;
            SkinManagedFolderPhysicalIdentity? provisionalIdentity = null;

            using (ISkinManagedFolderMutationNativeSession writer = native.Open(CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(
                    operationId,
                    CancellationToken.None);

                if (createProvisional)
                {
                    provisionalIdentity = writer.CreateManagedCopyProvisionalRoot(
                        operationId,
                        CancellationToken.None);

                    if (writeComplete)
                    {
                        writer.WriteManagedCopyProvisional(
                            operationId,
                            capsule,
                            logicalManifest,
                            () => { },
                            CancellationToken.None);
                    }
                }

                SkinManagedFolderMutationJournal prepared =
                    SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                        operationId,
                        externalRecordId,
                        writer.ManagedRootIdentity,
                        target_path,
                        stagedRootIdentity,
                        capsule.ContentRevision,
                        externalRecordFingerprint,
                        externalCaptureFingerprint,
                        logicalManifest,
                        binding);
                return new ProductionIntent(
                    prepared,
                    externalRecordId,
                    operationId,
                    provisionalIdentity,
                    Path.Combine(
                        dataRoot,
                        "skin-mutation-staging",
                        operationId.ToString("N")),
                    externalPath);
            }
        }

        private static string createExternalSource(
            osu.Framework.Platform.Storage storage,
            string childName)
        {
            string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                storage.GetFullPath(Path.Combine("external-recovery", childName))));
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "skin.ini"), skin_ini);
            return path;
        }

        private static Guid addExternalRecord(RealmAccess realm, string externalPath)
        {
            Guid id = Guid.NewGuid();
            realm.Write(r => r.Add(new SkinInfo("External", "OMS tests")
            {
                ID = id,
                FilesystemStoragePath = externalPath,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
            }));
            return id;
        }

        private static void assertProductionDriftBlocked(
            SkinManagedFolderMutationRecoveryResult result,
            MemoryMutationJournalStore store,
            SkinManagedFolderOperationCoordinator coordinator,
            ProductionIntent intent)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(Directory.Exists(intent.OperationRoot), Is.True);
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        private static SkinManagedFolderMutationRecoveryResult recover(
            RealmAccess realm,
            osu.Framework.Platform.Storage storage,
            MemoryMutationJournalStore store,
            out SkinManagedFolderOperationCoordinator coordinator)
        {
            coordinator = new SkinManagedFolderOperationCoordinator();
            var native = new WindowsSkinManagedFolderMutationNativeAuthority(
                storage.GetFullPath(string.Empty),
                new NativeWindowsSkinPackageCaptureFileSystem());
            var handler = new SkinManagedFolderManagedCopyRecoveryHandler(realm, native);
            var authority = new TestRecoveryAuthority(coordinator, native);
            return new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                handler,
                authority).Recover();
        }

        private static TestIntent prepareIntent(
            RealmAccess realm,
            osu.Framework.Platform.Storage storage,
            TestPackage package,
            bool createProvisional,
            bool writeComplete = false)
        {
            string dataRoot = storage.GetFullPath(string.Empty);
            Directory.CreateDirectory(Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
            Guid operationId = Guid.NewGuid();
            Guid externalRecordId = Guid.NewGuid();
            string externalPath = Path.Combine(dataRoot, "external-source");
            realm.Write(r => r.Add(new SkinInfo("External", "OMS tests")
            {
                ID = externalRecordId,
                FilesystemStoragePath = externalPath,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
            }));
            SkinInfo external = realm.Realm.Find<SkinInfo>(externalRecordId)!.Detach();
            string externalFingerprint =
                SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(external);
            SkinManagedFolderPhysicalIdentity stagedRootIdentity;
            SkinManagedFolderPhysicalIdentity? provisionalIdentity = null;

            using (WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(operationId, CancellationToken.None);

                if (createProvisional)
                {
                    provisionalIdentity = writer.CreateManagedCopyProvisionalRoot(operationId, CancellationToken.None);

                    if (writeComplete)
                    {
                        writer.WriteManagedCopyProvisional(
                            operationId,
                            package.Capsule,
                            package.Manifest,
                            () => { },
                            CancellationToken.None);
                    }
                }

                var prepared = SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                    operationId,
                    externalRecordId,
                    writer.ManagedRootIdentity,
                    target_path,
                    stagedRootIdentity,
                    package.Capsule.ContentRevision,
                    externalFingerprint,
                    new string('d', 64),
                    package.Manifest,
                    new SkinExternalRegistryJournalBinding(
                        12,
                        registry_digest,
                        SkinExternalCollisionDisposition.ExactRegisteredExternalSet));
                return new TestIntent(
                    prepared,
                    externalRecordId,
                    operationId,
                    provisionalIdentity,
                    Path.Combine(dataRoot, "skin-mutation-staging", operationId.ToString("N")));
            }
        }

        private static TestPackage createPackage()
        {
            SkinPackageCapturedEntry[] entries =
            {
                SkinPackageCapturedEntry.CreateFile(
                    "skin.ini",
                    skin_ini.Length,
                    () => new MemoryStream(skin_ini, writable: false)),
            };
            SkinPackageRevisionCapsuleCreationResult created = SkinPackageRevisionCapsuleFactory.Create(entries);
            Assert.That(created.IsSuccess, Is.True);
            SkinPackageRevisionCapsule capsule = created.Capsule!;

            try
            {
                Assert.That(SkinExternalPackageLogicalManifest.TryCreate(
                    entries,
                    capsule,
                    SkinExternalPackageCaptureLimits.DEFAULT_MAX_LOGICAL_MANIFEST_BYTES,
                    out SkinExternalPackageLogicalManifest? externalManifest), Is.True);
                return new TestPackage(
                    capsule,
                    SkinManagedCopyLogicalManifest.Create(externalManifest!));
            }
            catch
            {
                capsule.Dispose();
                throw;
            }
        }

        private sealed record TestIntent(
            SkinManagedFolderMutationJournal Prepared,
            Guid ExternalRecordId,
            Guid OperationId,
            SkinManagedFolderPhysicalIdentity? ProvisionalIdentity,
            string OperationRoot);

        private sealed record ProductionIntent(
            SkinManagedFolderMutationJournal Prepared,
            Guid ExternalRecordId,
            Guid OperationId,
            SkinManagedFolderPhysicalIdentity? ProvisionalIdentity,
            string OperationRoot,
            string ExternalPath);

        private sealed class TestPackage : IDisposable
        {
            public SkinPackageRevisionCapsule Capsule { get; }

            public SkinManagedCopyLogicalManifest Manifest { get; }

            public TestPackage(
                SkinPackageRevisionCapsule capsule,
                SkinManagedCopyLogicalManifest manifest)
            {
                Capsule = capsule;
                Manifest = manifest;
            }

            public void Dispose() => Capsule.Dispose();
        }

        private sealed class TestRecoveryAuthority : ISkinManagedFolderMutationRecoveryAuthority
        {
            private readonly SkinManagedFolderOperationCoordinator coordinator;
            private readonly ISkinManagedFolderMutationNativeAuthority native;

            public TestRecoveryAuthority(
                SkinManagedFolderOperationCoordinator coordinator,
                ISkinManagedFolderMutationNativeAuthority native)
            {
                this.coordinator = coordinator;
                this.native = native;
            }

            public ISkinManagedFolderMutationRecoveryAuthoritySession? TryOpen(
                SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
                CancellationToken cancellationToken = default)
            {
                if (coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true)
                    return null;

                return new Session(native.Open(cancellationToken));
            }

            private sealed class Session : ISkinManagedFolderMutationRecoveryAuthoritySession
            {
                private ISkinManagedFolderMutationNativeSession? native;

                public ISkinManagedFolderMutationNativeSession NativeSession
                    => native ?? throw new ObjectDisposedException(nameof(Session));

                public Session(ISkinManagedFolderMutationNativeSession native)
                {
                    this.native = native;
                }

                public bool IsExactFor(SkinManagedFolderMutationJournal journal)
                    => native != null && journal.Kind == SkinManagedFolderMutationKind.ManagedCopy;

                public bool Validate(CancellationToken cancellationToken = default)
                {
                    try
                    {
                        NativeSession.ValidateCompleteAndStable(cancellationToken);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                public bool ExactlyMatchesRealmDeclarations(System.Collections.Generic.IEnumerable<SkinInfo> records)
                    => native != null;

                public void Dispose()
                {
                    Interlocked.Exchange(ref native, null)?.Dispose();
                }
            }
        }

        private sealed class NeverInspectHeldHandler
            : ISkinManagedFolderMutationRecoveryHandler,
              ISkinManagedFolderMutationHeldRecoveryHandler
        {
            private readonly SkinManagedFolderMutationKind kind;

            public int InspectCalls { get; private set; }

            public NeverInspectHeldHandler(SkinManagedFolderMutationKind kind)
            {
                this.kind = kind;
            }

            public bool CanHandle(SkinManagedFolderMutationKind candidate)
                => candidate == kind;

            public SkinManagedFolderMutationRecoveryInspection Inspect(
                SkinManagedFolderMutationJournal journal,
                CancellationToken cancellationToken)
                => throw new AssertionException("legacy recovery must be rejected before inspection");

            public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
                SkinManagedFolderMutationJournal journal,
                CancellationToken cancellationToken)
                => throw new AssertionException("legacy recovery must be rejected before mutation");

            public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
                SkinManagedFolderMutationJournal journal,
                CancellationToken cancellationToken)
                => throw new AssertionException("legacy recovery must be rejected before mutation");

            public SkinManagedFolderMutationRecoveryInspection InspectHeld(
                SkinManagedFolderMutationJournal journal,
                ISkinManagedFolderMutationRecoveryAuthoritySession authority,
                CancellationToken cancellationToken)
            {
                InspectCalls++;
                throw new AssertionException("legacy recovery must be rejected before held inspection");
            }

            public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
                SkinManagedFolderMutationJournal journal,
                ISkinManagedFolderMutationRecoveryAuthoritySession authority,
                CancellationToken cancellationToken)
                => throw new AssertionException("legacy recovery must be rejected before held mutation");

            public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
                SkinManagedFolderMutationJournal journal,
                ISkinManagedFolderMutationRecoveryAuthoritySession authority,
                CancellationToken cancellationToken)
                => throw new AssertionException("legacy recovery must be rejected before held mutation");
        }
    }
}
