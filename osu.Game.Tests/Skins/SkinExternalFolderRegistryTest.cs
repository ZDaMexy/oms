// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinExternalFolderRegistryTest : RealmTest
    {
        [Test]
        public void TestEmptyExactSetHasStableZeroGenerationAndHeldLeaseValidation()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);
                SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult first = service.CaptureExactSet(lease);
                SkinExternalFolderRegistryCaptureResult second = service.CaptureExactSet(lease);

                Assert.That(first.IsSuccess, Is.True);
                Assert.That(second.IsSuccess, Is.True);

                using SkinExternalFolderRegistrySnapshot firstSnapshot = first.Snapshot!;
                using SkinExternalFolderRegistrySnapshot secondSnapshot = second.Snapshot!;

                Assert.Multiple(() =>
                {
                    Assert.That(firstSnapshot.IsEmpty, Is.True);
                    Assert.That(firstSnapshot.Count, Is.Zero);
                    Assert.That(firstSnapshot.HeldHandleCount, Is.Zero);
                    Assert.That(firstSnapshot.ExternalRegistryGeneration, Is.Zero);
                    Assert.That(firstSnapshot.ExternalRegistryDigest, Does.Match("^[0-9a-f]{64}$"));
                    Assert.That(firstSnapshot.ExternalRegistryDigest, Is.EqualTo(SkinExternalFolderRegistry.EmptyRegistryDigest));
                    Assert.That(secondSnapshot.ExternalRegistryGeneration, Is.EqualTo(firstSnapshot.ExternalRegistryGeneration));
                    Assert.That(secondSnapshot.ExternalRegistryDigest, Is.EqualTo(firstSnapshot.ExternalRegistryDigest));
                    Assert.That(firstSnapshot.Validate(lease), Is.True);
                    Assert.That(capture.OpenCount, Is.Zero);
                });

                lease.Dispose();
                Assert.That(firstSnapshot.Validate(lease), Is.False);
            });
        }

        [Test]
        public void TestExactSetDigestIsDeterministicAndAllProofsRemainHeld()
        {
            RunTestWithRealm((realm, storage) =>
            {
                string firstPath = createExternalDirectory(storage, "first");
                string secondPath = createExternalDirectory(storage, "second");
                Guid firstId = addExternalRecord(realm, firstPath);
                Guid secondId = addExternalRecord(realm, secondPath);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult first = service.CaptureExactSet(lease);

                Assert.That(first.IsSuccess, Is.True);

                using SkinExternalFolderRegistrySnapshot firstSnapshot = first.Snapshot!;

                Assert.Multiple(() =>
                {
                    Assert.That(firstSnapshot.Count, Is.EqualTo(2));
                    Assert.That(firstSnapshot.ExternalRegistryGeneration, Is.GreaterThan(0));
                    Assert.That(firstSnapshot.ExternalRegistryDigest, Does.Match("^[0-9a-f]{64}$"));
                    Assert.That(firstSnapshot.HeldHandleCount, Is.EqualTo(6));
                    Assert.That(capture.ActiveSessions, Is.EqualTo(2));
                    Assert.That(firstSnapshot.Validate(lease), Is.True);
                    Assert.That(firstSnapshot.TryGetPhysicalProof(firstId, out SkinFolderPhysicalAncestryProof? firstProof), Is.True);
                    Assert.That(firstSnapshot.TryGetPhysicalProof(secondId, out SkinFolderPhysicalAncestryProof? secondProof), Is.True);
                    Assert.That(firstProof!.Overlaps(secondProof!), Is.False);
                    Assert.That(firstSnapshot.ContainsRecordId(firstId), Is.True);
                    Assert.That(firstSnapshot.ContainsRecordId(secondId), Is.True);
                    Assert.That(
                        realm.Run(r => firstSnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())),
                        Is.True);
                });

                SkinExternalFolderRegistryCaptureResult second = service.CaptureExactSet(lease);
                Assert.That(second.IsSuccess, Is.True);

                using (SkinExternalFolderRegistrySnapshot secondSnapshot = second.Snapshot!)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(secondSnapshot.ExternalRegistryGeneration, Is.EqualTo(firstSnapshot.ExternalRegistryGeneration));
                        Assert.That(secondSnapshot.ExternalRegistryDigest, Is.EqualTo(firstSnapshot.ExternalRegistryDigest));
                        Assert.That(capture.ActiveSessions, Is.EqualTo(4));
                    });
                }

                Assert.That(capture.ActiveSessions, Is.EqualTo(2));
            });
        }

        [TestCase(null)]
        [TestCase("foreign-owner")]
        [TestCase("oms.skin.external-folder.registry.v0")]
        public void TestNullForeignAndOldOwnerRejectBeforeNativeCapture(string? owner)
        {
            RunTestWithRealm((realm, storage) =>
            {
                addExternalRecord(realm, createExternalDirectory(storage, "untrusted"), owner);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult result = service.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.UntrustedOwner));
                    Assert.That(capture.OpenCount, Is.Zero);
                    Assert.That(capture.ActiveSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestLexicalAncestorOverlapRejectsBeforeNativeCapture()
        {
            RunTestWithRealm((realm, storage) =>
            {
                string parent = createExternalDirectory(storage, "parent");
                string child = Path.Combine(parent, "child");
                Directory.CreateDirectory(child);
                addExternalRecord(realm, parent);
                addExternalRecord(realm, child);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult result = service.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.LexicalOverlap));
                    Assert.That(capture.OpenCount, Is.Zero);
                    Assert.That(capture.ActiveSessions, Is.Zero);
                });
            });
        }

        [Test]
        public void TestPhysicalDuplicateAndManagedOverlapRejectAndReleaseEverySession()
        {
            RunTestWithRealm((realm, storage) =>
            {
                string firstPath = createExternalDirectory(storage, "physical-first");
                string secondPath = createExternalDirectory(storage, "physical-second");
                addExternalRecord(realm, firstPath);
                addExternalRecord(realm, secondPath);
                SkinFolderPhysicalAncestryProof shared = createProof(100);
                var duplicateCapture = new FakeCaptureService(_ => shared);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var duplicateService = new SkinExternalFolderRegistryService(realm, storage, coordinator, duplicateCapture);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult duplicate = duplicateService.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(duplicate.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.PhysicalOverlap));
                    Assert.That(duplicateCapture.OpenCount, Is.EqualTo(2));
                    Assert.That(duplicateCapture.ActiveSessions, Is.Zero);
                    Assert.That(duplicateCapture.DisposedSessions, Is.EqualTo(2));
                });

                var managedCapture = new FakeCaptureService();
                var managedService = new SkinExternalFolderRegistryService(realm, storage, coordinator, managedCapture);
                SkinFolderPhysicalAncestryProof firstExternal = managedCapture.GetProofFor(firstPath);
                SkinExternalFolderRegistryCaptureResult managedOverlap = managedService.CaptureExactSet(
                    lease,
                    new[] { firstExternal });

                Assert.Multiple(() =>
                {
                    Assert.That(managedOverlap.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.ManagedAuthorityOverlap));
                    Assert.That(managedCapture.ActiveSessions, Is.Zero);
                    Assert.That(managedCapture.DisposedSessions, Is.EqualTo(managedCapture.OpenCount));
                });
            });
        }

        [Test]
        public void TestRecordSetAndPhysicalDriftInvalidateHeldSnapshot()
        {
            RunTestWithRealm((realm, storage) =>
            {
                string path = createExternalDirectory(storage, "drift");
                Guid recordId = addExternalRecord(realm, path);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult result = service.CaptureExactSet(lease);
                using SkinExternalFolderRegistrySnapshot snapshot = result.Snapshot!;

                Assert.That(snapshot.Validate(lease), Is.True);
                capture.FailValidation = true;
                Assert.That(snapshot.Validate(lease), Is.False);
                capture.FailValidation = false;

                realm.Write(r => r.Find<SkinInfo>(recordId)!.FilesystemStoragePath = path + Path.DirectorySeparatorChar);

                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.Validate(lease), Is.False);
                    Assert.That(
                        realm.Run(r => snapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())),
                        Is.False);
                    Assert.That(snapshot.ExternalRegistryGeneration, Is.GreaterThan(0));
                    Assert.That(capture.ActiveSessions, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestRecordAndAggregateBudgetsRejectWithoutLeakingProofs()
        {
            RunTestWithRealm((realm, storage) =>
            {
                string firstPath = createExternalDirectory(storage, "budget-one");
                string secondPath = createExternalDirectory(storage, "budget-two");
                addExternalRecord(realm, firstPath);
                addExternalRecord(realm, secondPath);
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var captureLimits = new SkinExternalPackageCaptureLimits(
                    SkinPackageRevisionCapsuleLimits.Default,
                    maxAuthorityDepth: 8,
                    maxHeldHandleCount: 8,
                    maxLogicalManifestBytes: 1024);
                var countLimits = new SkinExternalFolderRegistryLimits(
                    captureLimits,
                    maxRecordCount: 1,
                    maxManagedProofCount: 1,
                    maxTotalProofNodeCount: 20,
                    maxTotalHeldHandleCount: 20,
                    maxTotalPathCharacters: 4096);
                var countService = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture, countLimits);

                using SkinManagedFolderOperationCoordinator.Lease lease = coordinator.Enter();
                SkinExternalFolderRegistryCaptureResult count = countService.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(count.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.RecordCountBudgetExceeded));
                    Assert.That(capture.OpenCount, Is.Zero);
                });

                var pathCapture = new FakeCaptureService();
                var pathLimits = new SkinExternalFolderRegistryLimits(
                    captureLimits,
                    maxRecordCount: 2,
                    maxManagedProofCount: 1,
                    maxTotalProofNodeCount: 20,
                    maxTotalHeldHandleCount: 20,
                    maxTotalPathCharacters: firstPath.Length * 2 - 1);
                var pathService = new SkinExternalFolderRegistryService(realm, storage, coordinator, pathCapture, pathLimits);
                SkinExternalFolderRegistryCaptureResult path = pathService.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(path.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.AggregatePathBudgetExceeded));
                    Assert.That(pathCapture.OpenCount, Is.Zero);
                });

                var aggregateLimits = new SkinExternalFolderRegistryLimits(
                    captureLimits,
                    maxRecordCount: 2,
                    maxManagedProofCount: 1,
                    maxTotalProofNodeCount: 5,
                    maxTotalHeldHandleCount: 5,
                    maxTotalPathCharacters: 4096);
                var aggregateService = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture, aggregateLimits);
                SkinExternalFolderRegistryCaptureResult aggregate = aggregateService.CaptureExactSet(lease);

                Assert.Multiple(() =>
                {
                    Assert.That(aggregate.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.AggregateProofBudgetExceeded));
                    Assert.That(capture.ActiveSessions, Is.Zero);
                    Assert.That(capture.DisposedSessions, Is.EqualTo(2));
                });
            });
        }

        [Test]
        public void TestMissingCoordinatorLeaseFailsBeforeRealmOrNativeWork()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var coordinator = new SkinManagedFolderOperationCoordinator();
                var capture = new FakeCaptureService();
                var service = new SkinExternalFolderRegistryService(realm, storage, coordinator, capture);
                SkinExternalFolderRegistryCaptureResult result = service.CaptureExactSet(null);

                Assert.Multiple(() =>
                {
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinExternalFolderRegistryRejectionReason.CoordinatorLeaseMissing));
                    Assert.That(capture.OpenCount, Is.Zero);
                });
            });
        }

        private static string createExternalDirectory(Storage storage, string child)
        {
            string path = storage.GetFullPath(Path.Combine("external-registry", child));
            Directory.CreateDirectory(path);
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static Guid addExternalRecord(
            RealmAccess realm,
            string path,
            string? owner = SkinExternalFolderRegistry.AUTHORITY_OWNER)
        {
            Guid id = Guid.NewGuid();
            realm.Write(r => r.Add(new SkinInfo("External", "Author")
            {
                ID = id,
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = owner,
            }));
            return id;
        }

        private static SkinFolderPhysicalAncestryProof createProof(ulong rootId)
            => new SkinFolderPhysicalAncestryProof(new[]
            {
                new SkinManagedFolderPhysicalIdentity(1, 1, 1),
                new SkinManagedFolderPhysicalIdentity(1, rootId + 1000, 1),
                new SkinManagedFolderPhysicalIdentity(1, rootId, 1),
            });

        private sealed class FakeCaptureService : ISkinExternalFolderCaptureService
        {
            private readonly Func<SkinExternalPackageCaptureRequest, SkinFolderPhysicalAncestryProof> proofFactory;
            private readonly Dictionary<string, SkinFolderPhysicalAncestryProof> proofs = new Dictionary<string, SkinFolderPhysicalAncestryProof>(StringComparer.OrdinalIgnoreCase);
            private ulong nextRootId = 10;

            public int OpenCount { get; private set; }

            public int ActiveSessions { get; private set; }

            public int DisposedSessions { get; private set; }

            public bool FailValidation { get; set; }

            public FakeCaptureService(Func<SkinExternalPackageCaptureRequest, SkinFolderPhysicalAncestryProof>? proofFactory = null)
            {
                this.proofFactory = proofFactory ?? (request => GetProofFor(request.NormalisedAbsolutePath));
            }

            public SkinFolderPhysicalAncestryProof GetProofFor(string normalisedAbsolutePath)
            {
                if (!proofs.TryGetValue(normalisedAbsolutePath, out SkinFolderPhysicalAncestryProof? proof))
                {
                    proof = createProof(nextRootId++);
                    proofs.Add(normalisedAbsolutePath, proof);
                }

                return proof;
            }

            public SkinExternalFolderAuthorityCaptureResult OpenAuthority(
                SkinExternalPackageCaptureRequest? request,
                SkinExternalPackageCaptureLimits? limits = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OpenCount++;

                if (request == null)
                {
                    return SkinExternalFolderAuthorityCaptureResult.Reject(
                        SkinManagedPackageCaptureRejectionReason.InvalidRequest);
                }

                ActiveSessions++;
                return SkinExternalFolderAuthorityCaptureResult.Success(new FakeAuthoritySession(
                    this,
                    proofFactory(request)));
            }

            public SkinExternalPackageCaptureResult CaptureHeld(
                SkinExternalPackageCaptureRequest? request,
                SkinExternalPackageCaptureLimits? limits = null,
                CancellationToken cancellationToken = default)
                => SkinExternalPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            private sealed class FakeAuthoritySession : ISkinExternalFolderAuthoritySession
            {
                private readonly FakeCaptureService owner;
                private bool disposed;

                public SkinFolderPhysicalAncestryProof PhysicalProof { get; }

                public int HeldHandleCount => disposed ? 0 : PhysicalProof.HeldNodeCount;

                public FakeAuthoritySession(
                    FakeCaptureService owner,
                    SkinFolderPhysicalAncestryProof physicalProof)
                {
                    this.owner = owner;
                    PhysicalProof = physicalProof;
                }

                public void Validate(CancellationToken cancellationToken = default)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(disposed, this);

                    if (owner.FailValidation)
                    {
                        throw new WindowsSkinPackageCaptureFileSystemException(
                            SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                    }
                }

                public void Dispose()
                {
                    if (disposed)
                        return;

                    disposed = true;
                    owner.ActiveSessions--;
                    owner.DisposedSessions++;
                }

                public override string ToString() => nameof(FakeAuthoritySession);
            }
        }
    }
}
