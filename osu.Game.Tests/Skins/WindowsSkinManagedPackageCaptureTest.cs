// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class WindowsSkinManagedPackageCaptureTest
    {
        private string dataRoot = null!;
        private string packageRoot = null!;
        private NativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            dataRoot = Path.Combine(Path.GetTempPath(), $"oms-skin-native-capture-{Guid.NewGuid():N}");
            packageRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, "package");
            Directory.CreateDirectory(packageRoot);
            storage = new NativeStorage(dataRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dataRoot))
                Directory.Delete(dataRoot, true);
        }

        [Test]
        public void TestNativeLayoutsMatchWindowsAbi()
            => Assert.That(NativeMethods.HasExpectedLayouts, Is.True);

        [Test]
        public void TestNestedPackageCapturedAndHandlesReleased()
        {
            Directory.CreateDirectory(Path.Combine(packageRoot, "nested", "empty"));
            File.WriteAllBytes(Path.Combine(packageRoot, "skin.ini"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(packageRoot, "nested", "texture.png"), new byte[] { 4, 5, 6, 7 });

            SkinManagedPackageCaptureResult result = capture();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.None));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.None));
                Assert.That(result.Capsule, Is.Not.Null);
                Assert.That(result.Capsule!.FileCount, Is.EqualTo(2));
                Assert.That(result.Capsule.TotalBytes, Is.EqualTo(7));
                Assert.That(result.ToString(), Does.Not.Contain(dataRoot));
                Assert.That(result.ToString(), Does.Not.Contain("texture.png"));
            });

            using SkinPackageRevisionCapsule capsule = result.Capsule!;
            using var resources = capsule.CreateResourceView();
            Assert.That(resources.Get("skin.ini"), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(resources.Get("nested/texture.png"), Is.EqualTo(new byte[] { 4, 5, 6, 7 }));

            string moved = packageRoot + "-moved";
            Directory.Move(packageRoot, moved);
            Directory.Move(moved, packageRoot);
        }

        [Test]
        public void TestManagedFolderDiscoveryUsesNativeStableInventoryAndReleasesHandles()
        {
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\nName: Native Discovery\nAuthor: OMS Test\n",
                new UTF8Encoding(false, true));

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(storage).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                Assert.That(snapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.None));
                Assert.That(snapshot.ObservedManagedRelativePaths, Is.EqualTo(new[] { "chartskin/package" }));
                Assert.That(snapshot.ValidDiscoveries, Has.Count.EqualTo(1));
                Assert.That(snapshot.ValidDiscoveries[0].ManagedRelativePath, Is.EqualTo("chartskin/package"));
                Assert.That(snapshot.ValidDiscoveries[0].Name, Is.EqualTo("Native Discovery"));
                Assert.That(snapshot.ValidDiscoveries[0].Creator, Is.EqualTo("OMS Test"));
                Assert.That(snapshot.ValidDiscoveries[0].ContentRevision, Is.Not.Empty);
            });

            string moved = packageRoot + "-moved";
            Assert.DoesNotThrow(() => Directory.Move(packageRoot, moved));
            Assert.DoesNotThrow(() => Directory.Delete(moved, true));
        }

        [Test]
        public void TestMutationAuthorityFixesExistingIdentityAndAbsentTargetSlot()
        {
            File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), "held mutation authority");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity rootIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                rootIdentity = session.ManagedRootIdentity;
                sourceIdentity = session.CaptureExistingSource("chartskin/package", CancellationToken.None);
                SkinManagedFolderTargetNameSlot target = session.CaptureAbsentTargetNameSlot(
                    "chartskin/renamed",
                    CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(rootIdentity.IsUsable, Is.True);
                    Assert.That(sourceIdentity.IsUsable, Is.True);
                    Assert.That(sourceIdentity, Is.Not.EqualTo(rootIdentity));
                    Assert.That(target.ManagedRelativePath, Is.EqualTo("chartskin/renamed"));
                    Assert.That(target.ManagedRootIdentity, Is.EqualTo(rootIdentity));
                    Assert.That(target.ToString(), Does.Not.Contain(dataRoot));
                    Assert.That(sourceIdentity.ToString(), Does.Not.Contain(dataRoot));
                    Assert.Throws<IOException>(() => Directory.Move(packageRoot, packageRoot + "-blocked"));
                });

                Assert.DoesNotThrow(() => session.ValidateCompleteAndStable(CancellationToken.None));
            }

            string moved = packageRoot + "-moved";
            Assert.DoesNotThrow(() => Directory.Move(packageRoot, moved));
            Assert.DoesNotThrow(() => Directory.Move(moved, packageRoot));
        }

        [Test]
        public void TestMutationRenameHoldsCompleteTree()
        {
            string nested = Path.Combine(packageRoot, "nested");
            string file = Path.Combine(nested, "skin.ini");
            string renamedRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, "renamed");
            Directory.CreateDirectory(nested);
            File.WriteAllText(file, "held rename");
            using (WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                SkinManagedFolderPhysicalIdentity sourceIdentity =
                    session.CaptureExistingMutationSource("chartskin/package", CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentMutationTargetNameSlot("chartskin/renamed", CancellationToken.None);

                Assert.That(
                    session.RenameCapturedMutationSourceToTarget(target, CancellationToken.None),
                    Is.EqualTo(sourceIdentity));

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Directory.Exists(renamedRoot), Is.True);
                    Assert.That(File.ReadAllText(Path.Combine(renamedRoot, "nested", "skin.ini")), Is.EqualTo("held rename"));
                });

            }

            string renamedFile = Path.Combine(renamedRoot, "nested", "skin.ini");
            Assert.DoesNotThrow(() => File.WriteAllText(renamedFile, "released"));
        }

        [Test]
        public void TestMutationRenameRestartInspectionIsIdentityAware()
        {
            File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), "restart inspection");
            string renamedRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, "renamed");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
                sourceIdentity = session.CaptureExistingSource("chartskin/package", CancellationToken.None);

            assertInspection(SkinManagedFolderRenameInspectionStatus.SourceOnly);
            Directory.Move(packageRoot, renamedRoot);
            assertInspection(SkinManagedFolderRenameInspectionStatus.TargetOnly);
            Directory.CreateDirectory(packageRoot);
            assertInspection(SkinManagedFolderRenameInspectionStatus.Both);
            Directory.Delete(packageRoot);
            Directory.Delete(renamedRoot, true);
            assertInspection(SkinManagedFolderRenameInspectionStatus.Neither);
            Directory.CreateDirectory(packageRoot);
            assertInspection(SkinManagedFolderRenameInspectionStatus.IdentityMismatch);

            void assertInspection(SkinManagedFolderRenameInspectionStatus expectedStatus)
            {
                using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
                SkinManagedFolderRenameInspection inspection = session.InspectRenameState(
                    "chartskin/package",
                    "chartskin/renamed",
                    sourceIdentity,
                    CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(inspection.Status, Is.EqualTo(expectedStatus));
                    Assert.That(inspection.ToString(), Is.EqualTo($"SkinManagedFolderRenameInspection:{expectedStatus}"));
                    Assert.That(inspection.ToString(), Does.Not.Contain(dataRoot));
                    Assert.That(inspection.ToString(), Does.Not.Contain(sourceIdentity.FileIdPart0.ToString()));
                });
            }
        }

        [Test]
        public void TestMutationRenameRejectsNestedBusyWriter()
        {
            string file = Path.Combine(packageRoot, "skin.ini");
            File.WriteAllText(file, "busy");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureExistingSource("chartskin/package", CancellationToken.None));
            }
        }

        [Test]
        public void TestMutationRenameRejectsNestedHardLink()
        {
            string source = Path.Combine(packageRoot, "skin.ini");
            string alias = Path.Combine(packageRoot, "alias.ini");
            File.WriteAllText(source, "hardlink");

            if (!HardLinkHelper.TryCreateHardLink(alias, source))
                Assert.Ignore("The test volume does not support hard links.");

            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.CaptureExistingSource("chartskin/package", CancellationToken.None));
        }

        [Test]
        public void TestMutationTargetSlotCollisionAndLateCreationFailClosed()
        {
            File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), "held mutation authority");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);

            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.CaptureAbsentTargetNameSlot("chartskin/PACKAGE", CancellationToken.None));

            SkinManagedFolderTargetNameSlot target = session.CaptureAbsentTargetNameSlot(
                "chartskin/new-package",
                CancellationToken.None);
            Assert.That(target.ManagedRelativePath, Is.EqualTo("chartskin/new-package"));

            Directory.CreateDirectory(Path.Combine(dataRoot, "chartskin", "new-package"));
            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.ValidateCompleteAndStable(CancellationToken.None));
        }

        [Test]
        public void TestMutationRenameNativeTargetRaceDoesNotReplace()
        {
            string file = Path.Combine(packageRoot, "skin.ini");
            string targetRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, "raced");
            File.WriteAllText(file, "source");
            var raceFileSystem = new RenameRaceFileSystem(
                new NativeWindowsSkinPackageCaptureFileSystem(),
                () => Directory.CreateDirectory(targetRoot));
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(dataRoot, raceFileSystem);
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                sourceIdentity = session.CaptureExistingSource("chartskin/package", CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot("chartskin/raced", CancellationToken.None);

                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.RenameCapturedSourceToTarget(target, CancellationToken.None));
            }

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(packageRoot), Is.True);
                Assert.That(File.ReadAllText(file), Is.EqualTo("source"));
                Assert.That(Directory.Exists(targetRoot), Is.True);
            });

            using ISkinManagedFolderMutationNativeSession verification = authority.Open(CancellationToken.None);
            Assert.That(
                verification.CaptureExistingSource("chartskin/package", CancellationToken.None),
                Is.EqualTo(sourceIdentity));
        }

        [Test]
        public void TestMutationAuthorityCapturesOnlyExactOperationDerivedStagedSource()
        {
            Guid operationId = Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab");
            string stagingRoot = Path.Combine(dataRoot, "skin-mutation-staging");
            string stagedSource = Path.Combine(stagingRoot, operationId.ToString("N"));
            Directory.CreateDirectory(stagedSource);
            File.WriteAllText(Path.Combine(stagedSource, "skin.ini"), "held staged source");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                using SkinManagedFolderStagedSourceCapture capture =
                    session.CaptureStagedSource(operationId, CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(capture.StagedRootIdentity.IsUsable, Is.True);
                    Assert.That(capture.SourceIdentity.IsUsable, Is.True);
                    Assert.That(
                        capture.IsUsableFor(session.ManagedRootIdentity),
                        Is.True);
                    Assert.That(capture.StagedRootIdentity, Is.Not.EqualTo(capture.SourceIdentity));
                    Assert.Throws<IOException>(() => Directory.Move(stagedSource, stagedSource + "-blocked"));
                    Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                        () => session.CaptureStagedSource(Guid.NewGuid(), CancellationToken.None));
                    Assert.DoesNotThrow(() => session.ValidateCompleteAndStable(CancellationToken.None));
                });
            }

            string moved = stagedSource + "-moved";
            Assert.DoesNotThrow(() => Directory.Move(stagedSource, moved));
            Assert.DoesNotThrow(() => Directory.Move(moved, stagedSource));
        }

        [Test]
        public void TestMutationAuthorityRejectsCaseAliasForOperationDerivedStagedSlot()
        {
            Guid operationId = Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab");
            string upperOperationSlot = operationId.ToString("N").ToUpperInvariant();
            Directory.CreateDirectory(Path.Combine(dataRoot, "skin-mutation-staging", upperOperationSlot));
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.CaptureStagedSource(operationId, CancellationToken.None));
        }

        [Test]
        public void TestEmptyPackageRejectedWithoutLeakingHandles()
        {
            SkinManagedPackageCaptureResult result = capture();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.CapsuleRejected));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.EmptyPackage));
                Assert.That(result.Capsule, Is.Null);
            });

            string moved = packageRoot + "-moved";
            Directory.Move(packageRoot, moved);
            Directory.Move(moved, packageRoot);
        }

        [Test]
        public void TestEntryBudgetStopsNativeEnumerationBeforeCapture()
        {
            for (int i = 0; i < 10; i++)
                File.WriteAllBytes(Path.Combine(packageRoot, $"{i:D2}.bin"), new byte[] { 1 });

            var limits = new SkinPackageRevisionCapsuleLimits(2, 10, 4, 100, 10, 100);
            SkinManagedPackageCaptureResult result = capture(limits);

            Assert.Multiple(() =>
            {
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.CapsuleRejected));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded));
                Assert.That(result.Capsule, Is.Null);
            });

            string moved = packageRoot + "-moved";
            Directory.Move(packageRoot, moved);
            Directory.Move(moved, packageRoot);
        }

        [Test]
        public void TestBusyFileRejectedAndReleaseAllowsCapture()
        {
            string file = Path.Combine(packageRoot, "skin.ini");
            File.WriteAllText(file, "busy");

            using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SkinManagedPackageCaptureResult rejected = capture();
                Assert.That(rejected.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.SourceBusy));
            }

            using SkinPackageRevisionCapsule capsule = capture().Capsule!;
            Assert.That(capsule, Is.Not.Null);
        }

        [Test]
        public void TestHeldHandlesBlockWriterAndRenameWhileLateEntryRejectsCapture()
        {
            string file = Path.Combine(packageRoot, "skin.ini");
            string moved = Path.Combine(packageRoot, "moved.ini");
            string late = Path.Combine(packageRoot, "late.ini");
            File.WriteAllText(file, "held");

            using var enteredRead = new ManualResetEventSlim();
            using var releaseRead = new ManualResetEventSlim();
            var blockingFileSystem = new BlockingReadFileSystem(
                new NativeWindowsSkinPackageCaptureFileSystem(),
                enteredRead,
                releaseRead);
            var capture = new WindowsSkinManagedPackageCapture(blockingFileSystem);
            Task<SkinManagedPackageCaptureResult> task = Task.Run(() => capture.Capture(resolveRequest()));

            bool reachedReadGate = enteredRead.Wait(TimeSpan.FromSeconds(10));

            if (!reachedReadGate)
            {
                releaseRead.Set();
                Assert.Fail("Capture did not reach the held-handle read gate.");
            }

            try
            {
                Assert.Throws<IOException>(() => File.WriteAllText(file, "writer must be blocked"));
                Assert.Throws<IOException>(() => File.Move(file, moved));
                Assert.DoesNotThrow(() => File.WriteAllText(late, "late inventory mutation"));
            }
            finally
            {
                releaseRead.Set();
            }

            Assert.That(task.Wait(TimeSpan.FromSeconds(10)), Is.True, "Capture did not finish after releasing the read gate.");
            SkinManagedPackageCaptureResult result = task.GetResultSafely();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture)
                                                          .Or.EqualTo(SkinManagedPackageCaptureRejectionReason.InventoryChanged));
                Assert.That(File.ReadAllText(file), Is.EqualTo("held"));
            });

            Assert.DoesNotThrow(() => File.Move(file, moved));
            File.Move(moved, file);
        }

        [Test]
        public void TestHardLinkedFileRejected()
        {
            string source = Path.Combine(packageRoot, "skin.ini");
            string alias = Path.Combine(packageRoot, "alias.ini");
            File.WriteAllText(source, "hardlink");

            if (!HardLinkHelper.TryCreateHardLink(alias, source))
                Assert.Ignore("The test volume does not support hard links.");

            SkinManagedPackageCaptureResult result = capture();
            Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.HardLinkedFile));
            Assert.That(File.ReadAllText(source), Is.EqualTo("hardlink"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPackageAndNestedJunctionRejectedWithoutFollowingTarget(bool packageRootIsJunction)
        {
            SkinManagedPackageCaptureRequest request = resolveRequest();
            string externalTarget = Path.Combine(dataRoot, "external-target");
            string marker = Path.Combine(externalTarget, "marker.txt");
            Directory.CreateDirectory(externalTarget);
            File.WriteAllText(marker, "must survive");

            string junction;

            if (packageRootIsJunction)
            {
                Directory.Delete(packageRoot);
                junction = packageRoot;
            }
            else
            {
                junction = Path.Combine(packageRoot, "nested-junction");
            }

            try
            {
                createDirectoryJunctionOrIgnore(junction, externalTarget);
                SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(request);

                Assert.Multiple(() =>
                {
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered));
                    Assert.That(result.Capsule, Is.Null);
                    Assert.That(File.ReadAllText(marker), Is.EqualTo("must survive"));
                });
            }
            finally
            {
                deleteReparsePointIfPresent(junction);

                if (!Directory.Exists(packageRoot))
                    Directory.CreateDirectory(packageRoot);
            }
        }

        [Test]
        public void TestDistinctEightDotThreeDataRootAliasCanonicalisedBeforeNativeCapture()
        {
            File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), "canonical");
            var shortPath = new StringBuilder(32768);
            uint length = GetShortPathNameW(dataRoot, shortPath, shortPath.Capacity);

            if (length == 0 || length >= shortPath.Capacity)
                Assert.Ignore("The test volume did not provide a usable short path.");

            string alias = shortPath.ToString();

            if (string.Equals(alias, dataRoot, StringComparison.OrdinalIgnoreCase))
                Assert.Ignore("8.3 short-name generation is disabled for this test directory.");

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo { FilesystemStoragePath = "chartskin/package" },
                alias,
                PhysicalTestFilesystemInfoProvider.Instance);
            Assert.That(resolution.ManagedCaptureRequest, Is.Not.Null);
            SkinManagedPackageCaptureRequest request = resolution.ManagedCaptureRequest!;
            Assert.That(request.NormalisedDataRootAbsolutePath, Is.EqualTo(dataRoot).IgnoreCase);
            Assert.That(request.NormalisedDataRootAbsolutePath, Is.Not.EqualTo(Path.TrimEndingDirectorySeparator(alias)).IgnoreCase);
            SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(request);

            Assert.That(result.IsSuccess, Is.True);
            result.Capsule!.Dispose();
        }

        [Test]
        public void TestExternalHeldNativeCaptureRetainsProofAndEmptyDirectoryManifest()
        {
            string externalRoot = Path.Combine(dataRoot, "external-author");
            string movedRoot = externalRoot + "-moved";
            Directory.CreateDirectory(Path.Combine(externalRoot, "nested", "empty"));
            string ini = Path.Combine(externalRoot, "skin.ini");
            File.WriteAllBytes(ini, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(externalRoot, "nested", "note.png"), new byte[] { 4, 5 });
            SkinExternalPackageCaptureRequest request = resolveExternalRequest(externalRoot);
            var service = new SkinExternalFolderCaptureService();
            SkinExternalPackageCaptureResult result = service.CaptureHeld(request);

            Assert.That(result.IsSuccess, Is.True);
            ISkinExternalPackageCaptureSession session = result.Session!;

            Assert.Multiple(() =>
            {
                Assert.That(session.HeldHandleCount, Is.GreaterThan(0));
                Assert.That(session.LogicalManifest.Entries.Any(entry =>
                    entry.RelativePath == "nested/empty"
                    && entry.Kind == SkinExternalPackageLogicalEntryKind.Directory), Is.True);
                Assert.That(session.CaptureFingerprint, Does.Match("^[0-9a-f]{64}$"));
                Assert.Throws<IOException>(() => Directory.Move(externalRoot, movedRoot));
                Assert.That(File.ReadAllBytes(ini), Is.EqualTo(new byte[] { 1, 2, 3 }));
            });

            session.Validate();
            using SkinPackageRevisionCapsule capsule = session.TakeCapsule();
            using var resources = capsule.CreateResourceView();
            Assert.That(resources.Get("nested/note.png"), Is.EqualTo(new byte[] { 4, 5 }));
            session.Dispose();

            Assert.DoesNotThrow(() => Directory.Move(externalRoot, movedRoot));
            Assert.DoesNotThrow(() => Directory.Move(movedRoot, externalRoot));
        }

        [Test]
        public void TestExternalHeldNativeCaptureRejectsBusyWriter()
        {
            string busyRoot = Path.Combine(dataRoot, "external-busy");
            Directory.CreateDirectory(busyRoot);
            string busyFile = Path.Combine(busyRoot, "skin.ini");
            File.WriteAllText(busyFile, "busy");
            SkinExternalPackageCaptureRequest busyRequest = resolveExternalRequest(busyRoot);
            var service = new SkinExternalFolderCaptureService();

            using (File.Open(busyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SkinExternalPackageCaptureResult busy = service.CaptureHeld(busyRequest);
                Assert.That(busy.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.SourceBusy));
            }
        }

        [Test]
        public void TestExternalHeldNativeCaptureRejectsHardLink()
        {
            string hardLinkRoot = Path.Combine(dataRoot, "external-hardlink");
            Directory.CreateDirectory(hardLinkRoot);
            string source = Path.Combine(hardLinkRoot, "skin.ini");
            string alias = Path.Combine(hardLinkRoot, "alias.ini");
            File.WriteAllText(source, "hardlink");

            if (!HardLinkHelper.TryCreateHardLink(alias, source))
                Assert.Ignore("The test volume does not support hard links.");

            SkinExternalPackageCaptureResult hardLink = new SkinExternalFolderCaptureService().CaptureHeld(resolveExternalRequest(hardLinkRoot));

            Assert.Multiple(() =>
            {
                Assert.That(hardLink.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.HardLinkedFile));
                Assert.That(File.ReadAllText(source), Is.EqualTo("hardlink"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestExternalHeldNativeCaptureRejectsPackageAndNestedJunction(bool packageRootIsJunction)
        {
            string externalRoot = Path.Combine(dataRoot, "external-junction-package");
            Directory.CreateDirectory(externalRoot);
            File.WriteAllText(Path.Combine(externalRoot, "skin.ini"), "original");
            SkinExternalPackageCaptureRequest request = resolveExternalRequest(externalRoot);
            string target = Path.Combine(dataRoot, "external-junction-target");
            Directory.CreateDirectory(target);
            string marker = Path.Combine(target, "marker.txt");
            File.WriteAllText(marker, "must survive");
            string junction;

            if (packageRootIsJunction)
            {
                Directory.Delete(externalRoot, true);
                junction = externalRoot;
            }
            else
            {
                junction = Path.Combine(externalRoot, "nested-junction");
            }

            try
            {
                createDirectoryJunctionOrIgnore(junction, target);
                SkinExternalPackageCaptureResult result = new SkinExternalFolderCaptureService().CaptureHeld(request);

                Assert.Multiple(() =>
                {
                    Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered));
                    Assert.That(result.Session, Is.Null);
                    Assert.That(File.ReadAllText(marker), Is.EqualTo("must survive"));
                });
            }
            finally
            {
                deleteReparsePointIfPresent(junction);

                if (!Directory.Exists(externalRoot))
                    Directory.CreateDirectory(externalRoot);
            }
        }

        [Test]
        public void TestNullRequestRejectedBeforeNativeIo()
        {
            SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(null);

            Assert.Multiple(() =>
            {
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.InvalidRequest));
                Assert.That(result.Capsule, Is.Null);
                Assert.That(result.ToString(), Is.EqualTo("SkinManagedPackageCaptureResult:InvalidRequest:None"));
            });
        }

        private SkinManagedPackageCaptureResult capture(SkinPackageRevisionCapsuleLimits? limits = null)
        {
            return SkinManagedPackageCapture.Capture(resolveRequest(), limits);
        }

        private SkinManagedPackageCaptureRequest resolveRequest(string? root = null)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/package",
            }, root == null ? storage : new NativeStorage(root));

            Assert.That(resolution.ManagedCaptureRequest, Is.Not.Null);
            return resolution.ManagedCaptureRequest!;
        }

        private SkinExternalPackageCaptureRequest resolveExternalRequest(string externalRoot)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(new SkinInfo
            {
                FilesystemStoragePath = externalRoot,
                IsExternalFilesystemStorage = true,
            }, storage);

            Assert.That(resolution.ExternalCaptureRequest, Is.Not.Null);
            return resolution.ExternalCaptureRequest!;
        }

        private static void createDirectoryJunctionOrIgnore(string linkPath, string targetPath)
        {
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = powershell,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-Command");
            process.StartInfo.ArgumentList.Add("& { param($linkPath, $targetPath) $null = New-Item -ItemType Junction -Path $linkPath -Target $targetPath -ErrorAction Stop }");
            process.StartInfo.ArgumentList.Add(linkPath);
            process.StartInfo.ArgumentList.Add(targetPath);

            Assert.That(process.Start(), Is.True);
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                Assert.Ignore($"Directory junctions are unavailable in this Windows environment: {standardOutput}{Environment.NewLine}{standardError}");

            Assert.That(File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint), Is.True);
        }

        private static void deleteReparsePointIfPresent(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);

                if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                    return;

                if (attributes.HasFlag(FileAttributes.Directory))
                    Directory.Delete(path);
                else
                    File.Delete(path);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, int bufferLength);

        private sealed class PhysicalTestFilesystemInfoProvider : SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider
        {
            public static PhysicalTestFilesystemInfoProvider Instance { get; } = new PhysicalTestFilesystemInfoProvider();

            public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
        }

        private sealed class BlockingReadFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly IWindowsSkinPackageCaptureFileSystem inner;
            private readonly ManualResetEventSlim enteredRead;
            private readonly ManualResetEventSlim releaseRead;
            private int blocked;

            public BlockingReadFileSystem(
                IWindowsSkinPackageCaptureFileSystem inner,
                ManualResetEventSlim enteredRead,
                ManualResetEventSlim releaseRead)
            {
                this.inner = inner;
                this.enteredRead = enteredRead;
                this.releaseRead = releaseRead;
            }

            public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
                => inner.OpenLocalVolumeRoot(driveLetter);

            public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
                IWindowsSkinPackageCaptureHandle directory,
                int maxEntries,
                CancellationToken cancellationToken)
                => inner.Enumerate(directory, maxEntries, cancellationToken);

            public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                WindowsSkinPackageOpenMode mode,
                SkinManagedPackageCaptureRejectionReason unavailableReason)
                => inner.OpenChildNoFollow(parent, name, mode, unavailableReason);

            public WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle)
                => inner.QueryMetadata(handle);

            public void RenameChildNoReplace(
                IWindowsSkinPackageCaptureHandle source,
                IWindowsSkinPackageCaptureHandle targetParent,
                string targetName)
                => inner.RenameChildNoReplace(source, targetParent, targetName);

            public void DeleteNoFollow(IWindowsSkinPackageCaptureHandle handle)
                => inner.DeleteNoFollow(handle);

            public Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file)
            {
                if (Interlocked.Exchange(ref blocked, 1) == 0)
                {
                    enteredRead.Set();

                    if (!releaseRead.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("The test did not release the native read gate.");
                }

                return inner.CreateNonOwningReadStream(file);
            }
        }

        private sealed class RenameRaceFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly IWindowsSkinPackageCaptureFileSystem inner;
            private readonly Action beforeRename;

            public RenameRaceFileSystem(
                IWindowsSkinPackageCaptureFileSystem inner,
                Action beforeRename)
            {
                this.inner = inner;
                this.beforeRename = beforeRename;
            }

            public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
                => inner.OpenLocalVolumeRoot(driveLetter);

            public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
                IWindowsSkinPackageCaptureHandle directory,
                int maxEntries,
                CancellationToken cancellationToken)
                => inner.Enumerate(directory, maxEntries, cancellationToken);

            public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                WindowsSkinPackageOpenMode mode,
                SkinManagedPackageCaptureRejectionReason unavailableReason)
                => inner.OpenChildNoFollow(parent, name, mode, unavailableReason);

            public WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle)
                => inner.QueryMetadata(handle);

            public void RenameChildNoReplace(
                IWindowsSkinPackageCaptureHandle source,
                IWindowsSkinPackageCaptureHandle targetParent,
                string targetName)
            {
                beforeRename();
                inner.RenameChildNoReplace(source, targetParent, targetName);
            }

            public void DeleteNoFollow(IWindowsSkinPackageCaptureHandle handle)
                => inner.DeleteNoFollow(handle);

            public Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file)
                => inner.CreateNonOwningReadStream(file);
        }
    }
}
