// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
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
    public class SkinManagedFolderScannerTest : RealmTest
    {
        private const string managed_path = "chartskin/package";

        [Test]
        public void TestCompleteValidSnapshotAddsExactManagedRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                var discovery = new SkinManagedFolderDiscovery(managed_path, "Managed name", "Managed creator", "revision-1");
                SkinManagedFolderScanResult result = scan(realm, complete(discovery));

                SkinInfo record = realm.Realm.All<SkinInfo>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Added, Is.EqualTo(1));
                    Assert.That(result.Updated, Is.Zero);
                    Assert.That(result.Revived, Is.Zero);
                    Assert.That(result.SoftDeleted, Is.Zero);
                    Assert.That(result.Conflicts, Is.Zero);

                    Assert.That(record.ID, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(record.Name, Is.EqualTo("Managed name"));
                    Assert.That(record.Creator, Is.EqualTo("Managed creator"));
                    Assert.That(record.InstantiationInfo, Is.EqualTo(SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO));
                    Assert.That(record.Hash, Is.EqualTo("revision-1"));
                    Assert.That(record.FilesystemStoragePath, Is.EqualTo(managed_path));
                    Assert.That(record.IsExternalFilesystemStorage, Is.False);
                    Assert.That(record.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(record.Files, Has.Count.Zero);
                    Assert.That(record.Protected, Is.False);
                    Assert.That(record.DeletePending, Is.False);
                });
            });
        }

        [Test]
        public void TestRepeatedSnapshotIsIdempotentAndMetadataChangeUpdates()
        {
            RunTestWithRealm((realm, _) =>
            {
                var source = new MutableSource(complete(new SkinManagedFolderDiscovery(managed_path, "Initial", "Creator A", "revision-1")));
                var scanner = new SkinManagedFolderScanner(realm, source);

                SkinManagedFolderScanResult first = scanner.Scan();
                Guid id = realm.Realm.All<SkinInfo>().Single().ID;
                SkinManagedFolderScanResult identical = scanner.Scan();

                source.Snapshot = complete(new SkinManagedFolderDiscovery(managed_path, "Updated", "Creator B", "revision-2"));
                SkinManagedFolderScanResult changed = scanner.Scan();

                SkinInfo record = realm.Realm.All<SkinInfo>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(first.Added, Is.EqualTo(1));
                    Assert.That(identical.IsSuccess, Is.True);
                    Assert.That(totalMutations(identical), Is.Zero);
                    Assert.That(changed.Updated, Is.EqualTo(1));
                    Assert.That(changed.Added, Is.Zero);
                    Assert.That(changed.Revived, Is.Zero);
                    Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(1));
                    Assert.That(record.ID, Is.EqualTo(id));
                    Assert.That(record.Name, Is.EqualTo("Updated"));
                    Assert.That(record.Creator, Is.EqualTo("Creator B"));
                    Assert.That(record.Hash, Is.EqualTo("revision-2"));
                    Assert.That(record.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                });
            });
        }

        [Test]
        public void TestOwnedDeletePendingRecordIsRevived()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = addRecord(
                    realm,
                    managed_path,
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    name: "Stale name",
                    creator: "Stale creator",
                    hash: "stale-revision",
                    deletePending: true);

                SkinManagedFolderScanResult result = scan(
                    realm,
                    complete(new SkinManagedFolderDiscovery(managed_path, "Current name", "Current creator", "current-revision")));

                SkinInfo record = realm.Realm.Find<SkinInfo>(id)!;

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Added, Is.Zero);
                    Assert.That(result.Updated, Is.Zero);
                    Assert.That(result.Revived, Is.EqualTo(1));
                    Assert.That(record.DeletePending, Is.False);
                    Assert.That(record.Name, Is.EqualTo("Current name"));
                    Assert.That(record.Creator, Is.EqualTo("Current creator"));
                    Assert.That(record.Hash, Is.EqualTo("current-revision"));
                    Assert.That(record.InstantiationInfo, Is.EqualTo(SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO));
                    Assert.That(record.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                });
            });
        }

        [Test]
        public void TestObservedInvalidPackagePreventsNegativeReconciliation()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = addRecord(realm, managed_path, SkinManagedFolderScanner.AUTHORITY_OWNER);

                SkinManagedFolderScanResult result = scan(
                    realm,
                    SkinManagedFolderDiscoverySnapshot.Complete(new[] { managed_path }, Array.Empty<SkinManagedFolderDiscovery>()));

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(totalMutations(result), Is.Zero);
                    Assert.That(result.Conflicts, Is.Zero);
                    Assert.That(realm.Realm.Find<SkinInfo>(id)!.DeletePending, Is.False);
                });
            });
        }

        [Test]
        public void TestCompleteAbsenceSoftDeletesOnlyExactOwnedEligibleRecord()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid owned = addRecord(realm, "chartskin/owned", SkinManagedFolderScanner.AUTHORITY_OWNER);
                Guid unknown = addRecord(realm, "chartskin/unknown", null);
                Guid foreign = addRecord(realm, "chartskin/foreign", "foreign-scanner:v1");
                Guid external = addRecord(
                    realm,
                    "chartskin/external",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    isExternal: true);
                Guid mixedStorage = addRecord(
                    realm,
                    "chartskin/mixed-storage",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    withRealmFile: true);
                Guid protectedRecord = addRecord(
                    realm,
                    "chartskin/protected",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    isProtected: true);
                Guid fixedId = addRecord(
                    realm,
                    "chartskin/fixed-id",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    id: SkinInfo.RANDOM_SKIN);
                Guid ordinaryOsk = addRecord(realm, null, null, withRealmFile: true);

                SkinManagedFolderScanResult result = scan(
                    realm,
                    SkinManagedFolderDiscoverySnapshot.Complete(Array.Empty<string>(), Array.Empty<SkinManagedFolderDiscovery>()));

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.SoftDeleted, Is.EqualTo(1));
                    Assert.That(result.Added, Is.Zero);
                    Assert.That(result.Updated, Is.Zero);
                    Assert.That(result.Revived, Is.Zero);
                    Assert.That(result.Conflicts, Is.EqualTo(4));
                    Assert.That(realm.Realm.Find<SkinInfo>(owned)!.DeletePending, Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(unknown)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(foreign)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(external)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(mixedStorage)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(mixedStorage)!.Files, Has.Count.EqualTo(1));
                    Assert.That(realm.Realm.Find<SkinInfo>(protectedRecord)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(fixedId)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(ordinaryOsk)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(ordinaryOsk)!.Files, Has.Count.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestUnknownForeignAndSamePathConflictsAreNeverClaimed()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid unknown = addRecord(realm, "chartskin/unknown", null, name: "Unknown original");
                Guid foreign = addRecord(realm, "chartskin/foreign", "foreign-scanner:v1", name: "Foreign original");
                Guid collisionOwned = addRecord(
                    realm,
                    "chartskin/collision",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    name: "Owned collision original");
                Guid collisionForeign = addRecord(realm, "CHARTSKIN/COLLISION", "foreign-scanner:v2", name: "Foreign collision original");

                var discoveries = new[]
                {
                    new SkinManagedFolderDiscovery("chartskin/unknown", "Claim attempt", "Scanner", "new-1"),
                    new SkinManagedFolderDiscovery("chartskin/foreign", "Rewrite attempt", "Scanner", "new-2"),
                    new SkinManagedFolderDiscovery("chartskin/collision", "Collision attempt", "Scanner", "new-3"),
                };

                SkinManagedFolderScanResult result = scan(
                    realm,
                    SkinManagedFolderDiscoverySnapshot.Complete(discoveries.Select(d => d.ManagedRelativePath), discoveries));

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Conflicts, Is.EqualTo(3));
                    Assert.That(totalMutations(result), Is.Zero);
                    Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(4));
                    Assert.That(realm.Realm.Find<SkinInfo>(unknown)!.Name, Is.EqualTo("Unknown original"));
                    Assert.That(realm.Realm.Find<SkinInfo>(unknown)!.FilesystemStorageAuthorityOwner, Is.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(foreign)!.Name, Is.EqualTo("Foreign original"));
                    Assert.That(realm.Realm.Find<SkinInfo>(foreign)!.FilesystemStorageAuthorityOwner, Is.EqualTo("foreign-scanner:v1"));
                    Assert.That(realm.Realm.Find<SkinInfo>(collisionOwned)!.Name, Is.EqualTo("Owned collision original"));
                    Assert.That(realm.Realm.Find<SkinInfo>(collisionOwned)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(collisionForeign)!.Name, Is.EqualTo("Foreign collision original"));
                });
            });
        }

        [Test]
        public void TestIncompleteExceptionalNullAndCancelledScansDoNotWrite()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid guarded = addRecord(realm, "chartskin/guarded", SkinManagedFolderScanner.AUTHORITY_OWNER, name: "Original");

                SkinManagedFolderScanResult incomplete = scan(
                    realm,
                    SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.RootUnavailable));
                SkinManagedFolderScanResult exceptional = new SkinManagedFolderScanner(
                    realm,
                    new DelegateSource(_ => throw new InvalidOperationException("sensitive preparation failure"))).Scan();
                SkinManagedFolderScanResult nullSnapshot = new SkinManagedFolderScanner(
                    realm,
                    new DelegateSource(_ => null!)).Scan();

                using var cancellation = new CancellationTokenSource();
                var cancellingScanner = new SkinManagedFolderScanner(
                    realm,
                    new DelegateSource(_ =>
                    {
                        cancellation.Cancel();
                        return SkinManagedFolderDiscoverySnapshot.Complete(
                            Array.Empty<string>(),
                            Array.Empty<SkinManagedFolderDiscovery>());
                    }));

                Assert.Throws<OperationCanceledException>(() => cancellingScanner.Scan(cancellation.Token));

                SkinInfo record = realm.Realm.Find<SkinInfo>(guarded)!;

                Assert.Multiple(() =>
                {
                    Assert.That(incomplete.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.RootUnavailable));
                    Assert.That(exceptional.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.PreparationFailed));
                    Assert.That(nullSnapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.PreparationFailed));
                    Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(1));
                    Assert.That(record.Name, Is.EqualTo("Original"));
                    Assert.That(record.DeletePending, Is.False);
                });
            });
        }

        [Test]
        public void TestInvalidSnapshotsAreRejectedWithoutWrites()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid guarded = addRecord(realm, "chartskin/guarded", SkinManagedFolderScanner.AUTHORITY_OWNER, name: "Original");
                var package = new SkinManagedFolderDiscovery(managed_path, "Sensitive name", "Sensitive creator", "sensitive-revision");

                var invalidSnapshots = new[]
                {
                    SkinManagedFolderDiscoverySnapshot.Complete(
                        new[] { managed_path, "CHARTSKIN/PACKAGE" },
                        Array.Empty<SkinManagedFolderDiscovery>()),
                    SkinManagedFolderDiscoverySnapshot.Complete(
                        new[] { managed_path },
                        new[]
                        {
                            package,
                            new SkinManagedFolderDiscovery("CHARTSKIN/PACKAGE", "Other", "Other", "other-revision"),
                        }),
                    SkinManagedFolderDiscoverySnapshot.Complete(
                        new[] { "chartskin/other" },
                        new[] { package }),
                    SkinManagedFolderDiscoverySnapshot.Complete(
                        new[] { "chartskin/nested/package" },
                        Array.Empty<SkinManagedFolderDiscovery>()),
                };

                foreach (SkinManagedFolderDiscoverySnapshot snapshot in invalidSnapshots)
                {
                    SkinManagedFolderScanResult result = scan(realm, snapshot);

                    Assert.Multiple(() =>
                    {
                        Assert.That(result.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.SnapshotRejected));
                        Assert.That(totalMutations(result), Is.Zero);
                        Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(1));
                        Assert.That(realm.Realm.Find<SkinInfo>(guarded)!.Name, Is.EqualTo("Original"));
                        Assert.That(realm.Realm.Find<SkinInfo>(guarded)!.DeletePending, Is.False);
                    });
                }
            });
        }

        [Test]
        public void TestCancellationBeforeCommitRollsBackAllReconciliationMutations()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid existing = addRecord(
                    realm,
                    "chartskin/previous",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    name: "Previous");
                var discovery = new SkinManagedFolderDiscovery(managed_path, "New", "Creator", "revision-new");
                using var cancellation = new CancellationTokenSource();
                var scanner = new SkinManagedFolderScanner(realm, new MutableSource(complete(discovery)))
                {
                    ReconciliationBeforeCommit = cancellation.Cancel,
                };

                Assert.Throws<OperationCanceledException>(() => scanner.Scan(cancellation.Token));

                Assert.Multiple(() =>
                {
                    Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(1));
                    Assert.That(realm.Realm.Find<SkinInfo>(existing), Is.Not.Null);
                    Assert.That(realm.Realm.Find<SkinInfo>(existing)!.Name, Is.EqualTo("Previous"));
                    Assert.That(realm.Realm.Find<SkinInfo>(existing)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.All<SkinInfo>().Any(info => info.FilesystemStoragePath == managed_path), Is.False);
                });
            });
        }

        [Test]
        public void TestFrozenPathsSkipAllReconciliationWhileOtherPathsProceed()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid frozenUpdate = addRecord(realm, "chartskin/frozen-update", SkinManagedFolderScanner.AUTHORITY_OWNER, name: "Frozen original");
                Guid frozenRevive = addRecord(
                    realm,
                    "chartskin/frozen-revive",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    name: "Frozen deleted",
                    deletePending: true);
                Guid frozenAbsent = addRecord(realm, "chartskin/frozen-absent", SkinManagedFolderScanner.AUTHORITY_OWNER);
                Guid normalUpdate = addRecord(realm, "chartskin/normal-update", SkinManagedFolderScanner.AUTHORITY_OWNER, name: "Normal original");
                Guid normalRevive = addRecord(
                    realm,
                    "chartskin/normal-revive",
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    name: "Normal deleted",
                    deletePending: true);
                Guid normalAbsent = addRecord(realm, "chartskin/normal-absent", SkinManagedFolderScanner.AUTHORITY_OWNER);

                var discoveries = new[]
                {
                    new SkinManagedFolderDiscovery("chartskin/frozen-add", "Frozen add", "Scanner", "frozen-add-revision"),
                    new SkinManagedFolderDiscovery("chartskin/frozen-update", "Frozen changed", "Scanner", "frozen-update-revision"),
                    new SkinManagedFolderDiscovery("chartskin/frozen-revive", "Frozen revived", "Scanner", "frozen-revive-revision"),
                    new SkinManagedFolderDiscovery("chartskin/normal-add", "Normal add", "Scanner", "normal-add-revision"),
                    new SkinManagedFolderDiscovery("chartskin/normal-update", "Normal changed", "Scanner", "normal-update-revision"),
                    new SkinManagedFolderDiscovery("chartskin/normal-revive", "Normal revived", "Scanner", "normal-revive-revision"),
                };
                var coordinator = new SkinManagedFolderOperationCoordinator();
                coordinator.FreezePaths(new[]
                {
                    "chartskin/frozen-add",
                    "chartskin/frozen-update",
                    "chartskin/frozen-revive",
                    "chartskin/frozen-absent",
                });

                SkinManagedFolderScanResult result = new SkinManagedFolderScanner(
                    realm,
                    new MutableSource(SkinManagedFolderDiscoverySnapshot.Complete(
                        discoveries.Select(discovery => discovery.ManagedRelativePath),
                        discoveries)),
                    coordinator).Scan();

                SkinInfo[] records = realm.Realm.All<SkinInfo>().ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Added, Is.EqualTo(1));
                    Assert.That(result.Updated, Is.EqualTo(1));
                    Assert.That(result.Revived, Is.EqualTo(1));
                    Assert.That(result.SoftDeleted, Is.EqualTo(1));
                    Assert.That(result.Conflicts, Is.EqualTo(4));

                    Assert.That(records.Any(record => string.Equals(record.FilesystemStoragePath, "chartskin/frozen-add", StringComparison.OrdinalIgnoreCase)), Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(frozenUpdate)!.Name, Is.EqualTo("Frozen original"));
                    Assert.That(realm.Realm.Find<SkinInfo>(frozenUpdate)!.Hash, Is.EqualTo("original-revision"));
                    Assert.That(realm.Realm.Find<SkinInfo>(frozenRevive)!.Name, Is.EqualTo("Frozen deleted"));
                    Assert.That(realm.Realm.Find<SkinInfo>(frozenRevive)!.DeletePending, Is.True);
                    Assert.That(realm.Realm.Find<SkinInfo>(frozenAbsent)!.DeletePending, Is.False);

                    Assert.That(records.Single(record => string.Equals(record.FilesystemStoragePath, "chartskin/normal-add", StringComparison.OrdinalIgnoreCase)).Name, Is.EqualTo("Normal add"));
                    Assert.That(realm.Realm.Find<SkinInfo>(normalUpdate)!.Name, Is.EqualTo("Normal changed"));
                    Assert.That(realm.Realm.Find<SkinInfo>(normalUpdate)!.Hash, Is.EqualTo("normal-update-revision"));
                    Assert.That(realm.Realm.Find<SkinInfo>(normalRevive)!.Name, Is.EqualTo("Normal revived"));
                    Assert.That(realm.Realm.Find<SkinInfo>(normalRevive)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(normalAbsent)!.DeletePending, Is.True);
                });
            });
        }

        [Test]
        public void TestCoordinatorSerialisesScannerRealmCommitWithAnotherParticipant()
        {
            RunTestWithRealm((realm, _) =>
            {
                var reconciliationEntered = new ManualResetEventSlim();
                var releaseReconciliation = new ManualResetEventSlim();
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var discovery = new SkinManagedFolderDiscovery(managed_path, "Managed name", "Managed creator", "revision-1");
                var scanner = new SkinManagedFolderScanner(
                    realm,
                    new MutableSource(complete(discovery)),
                    coordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        reconciliationEntered.Set();
                        releaseReconciliation.Wait(TimeSpan.FromSeconds(10));
                    },
                };

                Task<SkinManagedFolderScanResult> scanTask = Task.Run(() => scanner.Scan());
                bool reconciliationStarted = reconciliationEntered.Wait(TimeSpan.FromSeconds(10));
                bool participantCancelledWhileScannerHeldLease = false;
                int recordsBeforeScannerCommit;

                try
                {
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

                    try
                    {
                        using SkinManagedFolderOperationCoordinator.Lease unexpectedLease = coordinator.Enter(cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        participantCancelledWhileScannerHeldLease = true;
                    }

                    recordsBeforeScannerCommit = realm.Realm.All<SkinInfo>().Count();
                }
                finally
                {
                    releaseReconciliation.Set();
                }

                bool scanCompleted = scanTask.Wait(TimeSpan.FromSeconds(10));
                SkinManagedFolderScanResult result = scanTask.GetAwaiter().GetResult();
                realm.Run(r => r.Refresh());

                using SkinManagedFolderOperationCoordinator.Lease participantAfterCommit = coordinator.Enter();

                Assert.Multiple(() =>
                {
                    Assert.That(reconciliationStarted, Is.True);
                    Assert.That(participantCancelledWhileScannerHeldLease, Is.True);
                    Assert.That(recordsBeforeScannerCommit, Is.Zero);
                    Assert.That(scanCompleted, Is.True);
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Added, Is.EqualTo(1));
                    Assert.That(realm.Realm.All<SkinInfo>(), Has.Count.EqualTo(1));
                });

                reconciliationEntered.Dispose();
                releaseReconciliation.Dispose();
            });
        }

        [Test]
        public void TestFrozenPathMatchingNormalisesRootCaseChildCaseAndUnicode()
        {
            const string decomposed = "CHARTSKIN/Cafe\u0301";
            const string composed = "chartskin/Caf\u00e9";
            const string upper_composed = "ChartSkin/CAF\u00c9";
            const string distinct = "chartskin/Cafeteria";

            var coordinator = new SkinManagedFolderOperationCoordinator();
            coordinator.FreezePaths(new[] { decomposed });

            Assert.Multiple(() =>
            {
                Assert.That(coordinator.IsPathFrozen(composed), Is.True);
                Assert.That(coordinator.IsPathFrozen(upper_composed), Is.True);
                Assert.That(coordinator.IsPathFrozen(distinct), Is.False);
                Assert.That(coordinator.IsPathFrozen("chartskin/nested/package"), Is.True);
            });

            coordinator.UnfreezePaths(new[] { upper_composed });

            Assert.Multiple(() =>
            {
                Assert.That(coordinator.IsPathFrozen(decomposed), Is.False);
                Assert.That(coordinator.IsPathFrozen(composed), Is.False);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestDiagnosticsDoNotExposeUserControlledValues()
        {
            RunTestWithRealm((realm, _) =>
            {
                const string secret_path = "chartskin/secret-folder";
                const string secret_name = "secret-name";
                const string secret_creator = "secret-creator";
                const string secret_revision = "secret-revision";

                var discovery = new SkinManagedFolderDiscovery(secret_path, secret_name, secret_creator, secret_revision);
                SkinManagedFolderDiscoverySnapshot snapshot = complete(discovery);
                SkinManagedFolderScanResult result = scan(realm, snapshot);

                string[] diagnostics = { discovery.ToString(), snapshot.ToString(), result.ToString() };
                string[] secrets = { secret_path, secret_name, secret_creator, secret_revision };

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);

                    foreach (string diagnostic in diagnostics)
                    {
                        foreach (string secret in secrets)
                            Assert.That(diagnostic, Does.Not.Contain(secret));
                    }
                });
            });
        }

        private static SkinManagedFolderScanResult scan(RealmAccess realm, SkinManagedFolderDiscoverySnapshot snapshot)
            => new SkinManagedFolderScanner(realm, new MutableSource(snapshot)).Scan();

        private static SkinManagedFolderDiscoverySnapshot complete(SkinManagedFolderDiscovery discovery)
            => SkinManagedFolderDiscoverySnapshot.Complete(new[] { discovery.ManagedRelativePath }, new[] { discovery });

        private static int totalMutations(SkinManagedFolderScanResult result)
            => result.Added + result.Updated + result.Revived + result.SoftDeleted;

        private static Guid addRecord(
            RealmAccess realm,
            string? path,
            string? owner,
            string name = "Original",
            string creator = "Original creator",
            string hash = "original-revision",
            bool deletePending = false,
            bool isExternal = false,
            bool withRealmFile = false,
            bool isProtected = false,
            Guid? id = null)
        {
            var record = new SkinInfo(name, creator, SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = id ?? Guid.NewGuid(),
                Hash = hash,
                FilesystemStoragePath = path,
                FilesystemStorageAuthorityOwner = owner,
                IsExternalFilesystemStorage = isExternal,
                Protected = isProtected,
                DeletePending = deletePending,
            };

            if (withRealmFile)
            {
                record.Files.Add(new RealmNamedFileUsage(
                    new RealmFile { Hash = $"legacy-file-{Guid.NewGuid():N}" },
                    "skin.ini"));
            }

            realm.Write(r => r.Add(record));
            return record.ID;
        }

        private sealed class MutableSource : ISkinManagedFolderDiscoverySource
        {
            public SkinManagedFolderDiscoverySnapshot Snapshot { get; set; }

            public MutableSource(SkinManagedFolderDiscoverySnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default) => Snapshot;
        }

        private sealed class DelegateSource : ISkinManagedFolderDiscoverySource
        {
            private readonly Func<CancellationToken, SkinManagedFolderDiscoverySnapshot> discover;

            public DelegateSource(Func<CancellationToken, SkinManagedFolderDiscoverySnapshot> discover)
            {
                this.discover = discover;
            }

            public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default) => discover(cancellationToken);
        }
    }
}
