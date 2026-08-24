// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public partial class WindowsSkinManagedPackageCaptureContractTest
    {
        [TestCase(@"\Device\HarddiskVolume3", true)]
        [TestCase(@"\device\harddiskvolume42", true)]
        [TestCase(@"\Device\HarddiskVolume", false)]
        [TestCase(@"\Device\HarddiskVolume3\folder", false)]
        [TestCase(@"\Device\HarddiskVolumeShadowCopy3", false)]
        [TestCase(@"\Device\HarddiskVolume3suffix", false)]
        [TestCase(@"\Device\Mup", false)]
        [TestCase(@"\??\C:\folder", false)]
        public void TestOnlyExactPhysicalVolumeTargetsAccepted(string target, bool expected)
            => Assert.That(NativeWindowsSkinPackageCaptureFileSystem.IsExactLocalVolumeTarget(target), Is.EqualTo(expected));

        [TestCase(4, 4, 8, 12)]
        [TestCase(8, 8, 16, 20)]
        public void TestFileRenameInfoRelativeLayout(
            int pointerSize,
            int expectedRootOffset,
            int expectedLengthOffset,
            int expectedNameOffset)
        {
            (int rootOffset, int lengthOffset, int nameOffset) =
                NativeMethods.GetFileRenameInfoOffsets(pointerSize);

            Assert.That(
                (rootOffset, lengthOffset, nameOffset),
                Is.EqualTo((expectedRootOffset, expectedLengthOffset, expectedNameOffset)));
        }

        [TestCase(4, 2, 18)]
        [TestCase(4, 14, 30)]
        [TestCase(8, 2, 26)]
        [TestCase(8, 14, 38)]
        public void TestFileRenameInfoBufferMeetsMinimumStructureSize(
            int pointerSize,
            int fileNameBytes,
            int expectedBufferSize)
            => Assert.That(
                NativeMethods.GetFileRenameInfoBufferSize(pointerSize, fileNameBytes),
                Is.EqualTo(expectedBufferSize));

        [Test]
        public void TestHeldMutationTreeRenamesWithIdentityContinuity()
        {
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            FakeNode file = nested.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                sourceIdentity = session.CaptureExistingSource("chartskin/package", CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot("chartskin/renamed", CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(package.FileSystem.OpenCount(nested), Is.GreaterThan(0));
                    Assert.That(package.FileSystem.OpenCount(file), Is.GreaterThan(0));
                    Assert.That(
                        session.RenameCapturedSourceToTarget(target, CancellationToken.None),
                        Is.EqualTo(sourceIdentity));
                    Assert.That(package.Package.Name, Is.EqualTo("renamed"));
                    Assert.That(session.ToString(), Does.Not.Contain("package"));
                });
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [TestCase((int)MutationTreeHazard.Reparse)]
        [TestCase((int)MutationTreeHazard.HardLink)]
        [TestCase((int)MutationTreeHazard.DuplicateIdentity)]
        [TestCase((int)MutationTreeHazard.BusyWriter)]
        public void TestMutationTreeHazardsRejectedBeforeRename(int hazardValue)
        {
            var hazard = (MutationTreeHazard)hazardValue;
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            FakeNode first = nested.AddFile("first.bin", new byte[] { 1 });
            FakeNode second = nested.AddFile("second.bin", new byte[] { 2 });

            switch (hazard)
            {
                case MutationTreeHazard.Reparse:
                    nested.IsReparsePoint = true;
                    break;

                case MutationTreeHazard.HardLink:
                    first.NumberOfLinks = 2;
                    break;

                case MutationTreeHazard.DuplicateIdentity:
                    second.FileId = first.FileId;
                    break;

                case MutationTreeHazard.BusyWriter:
                    first.IsBusy = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(hazard));
            }

            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureExistingSource("chartskin/package", CancellationToken.None));
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.RenameBegin, package.Package), Is.Zero);
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestMutationTreeRejectsDepthThirtyThreeWithoutRenameAndReleasesHandles()
        {
            FakePackage package = createPackage();
            FakeNode parent = package.Package;
            FakeNode? overDepth = null;

            for (int depth = 1; depth <= SkinPackageRevisionCapsuleLimits.Default.MaxDepth + 1; depth++)
            {
                parent = parent.AddDirectory($"depth-{depth:D2}");

                if (depth == SkinPackageRevisionCapsuleLimits.Default.MaxDepth + 1)
                    overDepth = parent;
            }

            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureExistingSource("chartskin/package", CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(overDepth, Is.Not.Null);
                    Assert.That(package.FileSystem.OpenCount(overDepth!), Is.Zero);
                    Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.RenameBegin, package.Package), Is.Zero);
                });
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestMutationTreeRejectsNestedChildBeyondWideEntryBudgetBeforeOpeningIt()
        {
            FakePackage package = createPackage();
            FakeNode firstDirectory = package.Package.AddDirectory("0000-directory");
            FakeNode nestedChild = firstDirectory.AddFile("nested.bin", new byte[] { 1 });

            for (int i = 1; i < SkinPackageRevisionCapsuleLimits.Default.MaxEntryCount; i++)
                package.Package.AddFile($"{i:D4}.bin", new byte[] { 1 });

            Assert.That(
                package.Package.Children,
                Has.Count.EqualTo(SkinPackageRevisionCapsuleLimits.Default.MaxEntryCount));

            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureExistingSource("chartskin/package", CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(package.FileSystem.OpenCount(firstDirectory), Is.GreaterThan(0));
                    Assert.That(package.FileSystem.OpenCount(nestedChild), Is.Zero);
                    Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.RenameBegin, package.Package), Is.Zero);
                });
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestLiveDeleteRejectsDirectoryCreationTimeDriftBeforeDisposition()
        {
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            nested.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(
                @"C:\data",
                package.FileSystem);
            const string source_path = "chartskin/package";
            const string tombstone_path = "chartskin/.oms-delete-contract";

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderPhysicalIdentity sourceIdentity =
                    session.CaptureExistingSource(source_path, CancellationToken.None);
                string manifest = session.GetCapturedDeleteSourceNodeManifest(
                    CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot(
                        tombstone_path,
                        CancellationToken.None);
                session.RenameCapturedSourceToTarget(target, CancellationToken.None);
                nested.CreationTime++;

                Assert.Multiple(() =>
                {
                    Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                        () => session.CleanupExactDeleteTombstone(
                            source_path,
                            tombstone_path,
                            sourceIdentity,
                            manifest,
                            CancellationToken.None));
                    Assert.That(package.Package.Name, Is.EqualTo(
                        ".oms-delete-contract"));
                    Assert.That(package.FileSystem.OperationIndex(
                        FakeOperationKind.Delete,
                        nested), Is.Zero);
                    Assert.That(package.FileSystem.OperationIndex(
                        FakeOperationKind.Delete,
                        package.Package), Is.Zero);
                });
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestDeleteExclusiveRecaptureBlocksRootAndChildRenameDuringDisposition()
        {
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            nested.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            FakeNode outside = package.DataRoot.AddDirectory("outside");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(
                @"C:\data",
                package.FileSystem);
            const string source_path = "chartskin/package";
            const string tombstone_path = "chartskin/.oms-delete-exclusive";
            bool rootRenameBlocked = false;
            bool childRenameBlocked = false;
            bool attempted = false;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderPhysicalIdentity sourceIdentity =
                    session.CaptureExistingSource(source_path, CancellationToken.None);
                string manifest = session.GetCapturedDeleteSourceNodeManifest(
                    CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot(
                        tombstone_path,
                        CancellationToken.None);
                session.RenameCapturedSourceToTarget(target, CancellationToken.None);
                package.FileSystem.OnOperation = operation =>
                {
                    if (attempted || operation.Kind != FakeOperationKind.Delete)
                        return;

                    attempted = true;
                    rootRenameBlocked = !package.FileSystem.TryMoveDirectChild(
                        package.ManagedRoot,
                        package.Package,
                        outside,
                        "escaped-root");
                    childRenameBlocked = !package.FileSystem.TryMoveDirectChild(
                        package.Package,
                        nested,
                        package.ManagedRoot,
                        "escaped-child");
                };

                session.CleanupExactDeleteTombstone(
                    source_path,
                    tombstone_path,
                    sourceIdentity,
                    manifest,
                    CancellationToken.None);
            }

            Assert.Multiple(() =>
            {
                Assert.That(attempted, Is.True);
                Assert.That(rootRenameBlocked, Is.True);
                Assert.That(childRenameBlocked, Is.True);
                Assert.That(package.ManagedRoot.Children, Is.Empty);
                Assert.That(outside.Children, Is.Empty);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestLiveDeleteRejectsChildRemovedBeforeExclusiveRecaptureWithoutDeletingAnything()
        {
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            FakeNode file = nested.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            FakeNode outside = package.DataRoot.AddDirectory("outside");
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(
                @"C:\data",
                package.FileSystem);
            const string source_path = "chartskin/package";
            const string tombstone_path = "chartskin/.oms-delete-live-gap";
            bool relocated = false;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderPhysicalIdentity sourceIdentity =
                    session.CaptureExistingSource(source_path, CancellationToken.None);
                string manifest = session.GetCapturedDeleteSourceNodeManifest(
                    CancellationToken.None);
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot(
                        tombstone_path,
                        CancellationToken.None);
                session.RenameCapturedSourceToTarget(target, CancellationToken.None);
                package.FileSystem.OnOperation = operation =>
                {
                    if (relocated
                        || operation.Kind != FakeOperationKind.OpenChild
                        || operation.Node != package.ManagedRoot
                        || operation.Mode
                           != WindowsSkinPackageOpenMode.DeleteExclusiveDirectory)
                    {
                        return;
                    }

                    relocated = package.FileSystem.TryMoveDirectChild(
                        package.Package,
                        nested,
                        outside,
                        "relocated-child");

                    if (!relocated)
                        throw new InvalidOperationException("The live-gap relocation was unexpectedly blocked.");
                };

                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CleanupExactDeleteTombstone(
                        source_path,
                        tombstone_path,
                        sourceIdentity,
                        manifest,
                        CancellationToken.None));
            }

            Assert.Multiple(() =>
            {
                Assert.That(relocated, Is.True);
                Assert.That(package.Package.Name, Is.EqualTo(
                    ".oms-delete-live-gap"));
                Assert.That(outside.Children, Has.Count.EqualTo(1));
                Assert.That(outside.Children[0], Is.SameAs(nested));
                Assert.That(nested.Children.Single(), Is.SameAs(file));
                Assert.That(package.FileSystem.OperationIndex(
                    FakeOperationKind.Delete,
                    package.Package), Is.Zero);
                Assert.That(package.FileSystem.OperationIndex(
                    FakeOperationKind.Delete,
                    nested), Is.Zero);
                Assert.That(package.FileSystem.OperationIndex(
                    FakeOperationKind.Delete,
                    file), Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestTargetRaceIsNoReplaceAndKeepsSourceIdentity()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            SkinManagedFolderPhysicalIdentity sourceIdentity =
                session.CaptureExistingSource("chartskin/package", CancellationToken.None);
            SkinManagedFolderTargetNameSlot target =
                session.CaptureAbsentTargetNameSlot("chartskin/renamed", CancellationToken.None);
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.RenameBegin && operation.Node == package.Package)
                    package.ManagedRoot.AddDirectory("renamed");
            };

            Assert.Multiple(() =>
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.RenameCapturedSourceToTarget(target, CancellationToken.None));
                Assert.That(package.Package.Name, Is.EqualTo("package"));
                Assert.That(toMutationIdentity(package.Package), Is.EqualTo(sourceIdentity));
                Assert.That(package.ManagedRoot.Children.Count(child => child.Name == "renamed"), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPostVisibleCancellationDoesNotReplaceFinalVerification()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);
            using var cancellation = new CancellationTokenSource();

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            SkinManagedFolderPhysicalIdentity sourceIdentity =
                session.CaptureExistingSource("chartskin/package", CancellationToken.None);
            SkinManagedFolderTargetNameSlot target =
                session.CaptureAbsentTargetNameSlot("chartskin/renamed", CancellationToken.None);
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.RenameEnd && operation.Node == package.Package)
                    cancellation.Cancel();
            };

            Assert.Multiple(() =>
            {
                Assert.That(
                    session.RenameCapturedSourceToTarget(target, cancellation.Token),
                    Is.EqualTo(sourceIdentity));
                Assert.That(cancellation.IsCancellationRequested, Is.True);
                Assert.That(package.Package.Name, Is.EqualTo("renamed"));
            });
        }

        [Test]
        public void TestPostVisibleNestedMutationFailsFinalRecapture()
        {
            FakePackage package = createPackage();
            FakeNode nested = package.Package.AddDirectory("nested");
            FakeNode file = nested.AddFile("skin.ini", new byte[] { 1 });
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            session.CaptureExistingSource("chartskin/package", CancellationToken.None);
            SkinManagedFolderTargetNameSlot target =
                session.CaptureAbsentTargetNameSlot("chartskin/renamed", CancellationToken.None);
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.RenameEnd && operation.Node == package.Package)
                    file.ChangeTime++;
            };

            Assert.Multiple(() =>
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.RenameCapturedSourceToTarget(target, CancellationToken.None));
                Assert.That(package.Package.Name, Is.EqualTo("renamed"));
                Assert.That(package.ManagedRoot.Children.Single(child => child.Name == "renamed"), Is.SameAs(package.Package));
            });
        }

        [TestCase((int)RenameInspectionSetup.SourceOnly, (int)SkinManagedFolderRenameInspectionStatus.SourceOnly)]
        [TestCase((int)RenameInspectionSetup.TargetOnly, (int)SkinManagedFolderRenameInspectionStatus.TargetOnly)]
        [TestCase((int)RenameInspectionSetup.Both, (int)SkinManagedFolderRenameInspectionStatus.Both)]
        [TestCase((int)RenameInspectionSetup.Neither, (int)SkinManagedFolderRenameInspectionStatus.Neither)]
        [TestCase((int)RenameInspectionSetup.IdentityMismatch, (int)SkinManagedFolderRenameInspectionStatus.IdentityMismatch)]
        public void TestRestartInspectionClassifiesHeldRootState(
            int setupValue,
            int expectedStatusValue)
        {
            var setup = (RenameInspectionSetup)setupValue;
            var expectedStatus = (SkinManagedFolderRenameInspectionStatus)expectedStatusValue;
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            SkinManagedFolderPhysicalIdentity expectedIdentity = toMutationIdentity(package.Package);

            switch (setup)
            {
                case RenameInspectionSetup.SourceOnly:
                    break;

                case RenameInspectionSetup.TargetOnly:
                    package.FileSystem.MoveDirectChild(package.ManagedRoot, package.Package, "renamed");
                    break;

                case RenameInspectionSetup.Both:
                    package.ManagedRoot.AddDirectory("renamed");
                    break;

                case RenameInspectionSetup.Neither:
                    Assert.That(package.ManagedRoot.Children.Remove(package.Package), Is.True);
                    break;

                case RenameInspectionSetup.IdentityMismatch:
                    Assert.That(package.ManagedRoot.Children.Remove(package.Package), Is.True);
                    package.ManagedRoot.AddDirectory("package");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(setup));
            }

            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None);
            SkinManagedFolderRenameInspection inspection = session.InspectRenameState(
                "chartskin/package",
                "chartskin/renamed",
                expectedIdentity,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(inspection.Status, Is.EqualTo(expectedStatus));
                Assert.That(inspection.ToString(), Is.EqualTo($"SkinManagedFolderRenameInspection:{expectedStatus}"));
                Assert.That(inspection.ToString(), Does.Not.Contain("package"));
                Assert.That(inspection.ToString(), Does.Not.Contain(expectedIdentity.FileIdPart0.ToString()));
            });
        }

        [Test]
        public void TestRepeatedBothInspectionReleasesTreeHandlesPerClassification()
        {
            FakePackage package = createPackage();
            package.Package.AddDirectory("source-nested").AddFile("skin.ini", new byte[] { 1 });
            FakeNode target = package.ManagedRoot.AddDirectory("renamed");
            target.AddDirectory("target-nested").AddFile("skin.ini", new byte[] { 2 });
            SkinManagedFolderPhysicalIdentity expectedIdentity = toMutationIdentity(package.Package);
            var authority = new WindowsSkinManagedFolderMutationNativeAuthority(@"C:\data", package.FileSystem);

            using (ISkinManagedFolderMutationNativeSession session = authority.Open(CancellationToken.None))
            {
                Assert.That(
                    session.InspectRenameState(
                        "chartskin/package",
                        "chartskin/renamed",
                        expectedIdentity,
                        CancellationToken.None).Status,
                    Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Both));
                int heldAuthorityHandleCount = package.FileSystem.ActiveHandleCount;

                for (int i = 0; i < 2; i++)
                {
                    Assert.That(
                        session.InspectRenameState(
                            "chartskin/package",
                            "chartskin/renamed",
                            expectedIdentity,
                            CancellationToken.None).Status,
                        Is.EqualTo(SkinManagedFolderRenameInspectionStatus.Both));
                    Assert.That(package.FileSystem.ActiveHandleCount, Is.EqualTo(heldAuthorityHandleCount));
                }
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestMutationTreeRejectsNonAdjacentNfcCaseAliases()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("a\u030A", new byte[] { 1 });
            package.Package.AddFile("b", new byte[] { 2 });
            package.Package.AddFile("\u00E5", new byte[] { 3 });

            using (WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                       @"C:\data",
                       package.FileSystem,
                       CancellationToken.None))
            {
                WindowsSkinPackageCaptureFileSystemException? exception = Assert.Throws<WindowsSkinPackageCaptureFileSystemException>(
                    () => session.CaptureExistingMutationSource("chartskin/package", CancellationToken.None));
                Assert.That(exception!.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias));
            }

            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.RenameBegin, package.Package), Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestExternalHeldCaptureProducesPairedManifestCapsuleAndProof()
        {
            FakeExternalPackage package = createExternalPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            FakeNode nested = package.Package.AddDirectory("Nested");
            nested.AddDirectory("empty");
            nested.AddFile("note.png", new byte[] { 4, 5 });

            SkinExternalPackageCaptureResult result = package.CaptureHeld();

            Assert.That(result.IsSuccess, Is.True);
            ISkinExternalPackageCaptureSession session = result.Session!;

            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.ActiveHandleCount, Is.GreaterThan(0));
                Assert.That(session.HeldHandleCount, Is.EqualTo(package.FileSystem.ActiveHandleCount));
                Assert.That(session.PhysicalProof.HeldNodeCount, Is.EqualTo(3));
                Assert.That(session.PhysicalProof.Digest, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(session.PhysicalTreeFingerprint, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(session.CaptureFingerprint, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(
                    session.LogicalManifest.Entries.Select(entry => (entry.RelativePath, entry.Kind, entry.Length)),
                    Is.EqualTo(new[]
                    {
                        ("Nested", SkinExternalPackageLogicalEntryKind.Directory, 0L),
                        ("Nested/empty", SkinExternalPackageLogicalEntryKind.Directory, 0L),
                        ("Nested/note.png", SkinExternalPackageLogicalEntryKind.File, 2L),
                        ("skin.ini", SkinExternalPackageLogicalEntryKind.File, 3L),
                    }));
                Assert.That(session.LogicalManifest.Digest, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(session.LogicalManifest.CanonicalByteCount, Is.LessThanOrEqualTo(SkinExternalPackageCaptureLimits.DEFAULT_MAX_LOGICAL_MANIFEST_BYTES));
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.RenameBegin, package.Package), Is.Zero);
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.Delete, package.Package), Is.Zero);
            });

            session.Validate();
            using SkinPackageRevisionCapsule capsule = session.TakeCapsule();

            Assert.Multiple(() =>
            {
                Assert.That(capsule.ContentRevision, Is.EqualTo(session.LogicalManifest.ContentRevision));
                Assert.That(capsule.FileCount, Is.EqualTo(session.LogicalManifest.FileCount));
                Assert.That(capsule.TotalBytes, Is.EqualTo(session.LogicalManifest.TotalFileBytes));
                Assert.Throws<InvalidOperationException>(() => session.TakeCapsule());
            });

            session.Dispose();
            session.Dispose();
            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestExternalManifestAndCaptureFingerprintBindEmptyDirectories()
        {
            FakeExternalPackage package = createExternalPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });

            SkinExternalPackageCaptureResult first = package.CaptureHeld();
            string firstRevision;
            string firstManifest;
            string firstCapture;

            using (ISkinExternalPackageCaptureSession session = first.Session!)
            using (SkinPackageRevisionCapsule capsule = session.TakeCapsule())
            {
                firstRevision = capsule.ContentRevision;
                firstManifest = session.LogicalManifest.Digest;
                firstCapture = session.CaptureFingerprint;
            }

            package.Package.AddDirectory("empty");
            SkinExternalPackageCaptureResult second = package.CaptureHeld();

            using (ISkinExternalPackageCaptureSession session = second.Session!)
            using (SkinPackageRevisionCapsule capsule = session.TakeCapsule())
            {
                Assert.Multiple(() =>
                {
                    Assert.That(capsule.ContentRevision, Is.EqualTo(firstRevision));
                    Assert.That(session.LogicalManifest.Digest, Is.Not.EqualTo(firstManifest));
                    Assert.That(session.CaptureFingerprint, Is.Not.EqualTo(firstCapture));
                    Assert.That(
                        session.LogicalManifest.Entries.Single(entry => entry.RelativePath == "empty").Kind,
                        Is.EqualTo(SkinExternalPackageLogicalEntryKind.Directory));
                });
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestExternalProofOnlySessionDoesNotReadPackageBytes()
        {
            FakeExternalPackage package = createExternalPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });

            SkinExternalFolderAuthorityCaptureResult result = package.OpenAuthority();

            Assert.That(result.IsSuccess, Is.True);

            using (ISkinExternalFolderAuthoritySession session = result.Session!)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(session.HeldHandleCount, Is.EqualTo(3));
                    Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
                    Assert.That(package.FileSystem.OpenCount(package.Package.Children.Single()), Is.Zero);
                });

                session.Validate();
            }

            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [TestCase("volume")]
        [TestCase("ancestor")]
        [TestCase("package")]
        [TestCase("nested")]
        public void TestExternalCaptureRejectsReparseAtEveryHeldLevel(string level)
        {
            FakeExternalPackage package = createExternalPackage();
            FakeNode target = level switch
            {
                "volume" => package.VolumeRoot,
                "ancestor" => package.ExternalRoot,
                "package" => package.Package,
                "nested" => package.Package.AddDirectory("nested"),
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
            target.IsReparsePoint = true;

            SkinExternalPackageCaptureResult result = package.CaptureHeld();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [TestCase((int)MutationTreeHazard.HardLink)]
        [TestCase((int)MutationTreeHazard.DuplicateIdentity)]
        [TestCase((int)MutationTreeHazard.BusyWriter)]
        public void TestExternalCaptureRejectsFileIdentityAndWriterHazards(int hazardValue)
        {
            var hazard = (MutationTreeHazard)hazardValue;
            FakeExternalPackage package = createExternalPackage();
            FakeNode first = package.Package.AddFile("first.bin", new byte[] { 1 });
            FakeNode second = package.Package.AddFile("second.bin", new byte[] { 2 });

            switch (hazard)
            {
                case MutationTreeHazard.HardLink:
                    first.NumberOfLinks = 2;
                    break;

                case MutationTreeHazard.DuplicateIdentity:
                    second.FileId = first.FileId;
                    break;

                case MutationTreeHazard.BusyWriter:
                    first.IsBusy = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(hazard));
            }

            SkinExternalPackageCaptureResult result = package.CaptureHeld();
            SkinManagedPackageCaptureRejectionReason expected = hazard switch
            {
                MutationTreeHazard.HardLink => SkinManagedPackageCaptureRejectionReason.HardLinkedFile,
                MutationTreeHazard.DuplicateIdentity => SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity,
                _ => SkinManagedPackageCaptureRejectionReason.SourceBusy,
            };

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(expected));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestExternalCaptureAndHeldValidationRejectInventoryAndIdentityDrift()
        {
            FakeExternalPackage inventoryPackage = createExternalPackage();
            inventoryPackage.Package.AddFile("skin.ini", new byte[] { 1 });
            inventoryPackage.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    inventoryPackage.Package.AddFile("late.ini", new byte[] { 2 });
            };

            SkinExternalPackageCaptureResult inventory = inventoryPackage.CaptureHeld();

            Assert.Multiple(() =>
            {
                Assert.That(inventory.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.InventoryChanged));
                Assert.That(inventoryPackage.FileSystem.ActiveHandleCount, Is.Zero);
            });

            FakeExternalPackage identityPackage = createExternalPackage();
            FakeNode heldFile = identityPackage.Package.AddFile("skin.ini", new byte[] { 1 });
            SkinExternalPackageCaptureResult held = identityPackage.CaptureHeld();
            Assert.That(held.IsSuccess, Is.True);

            using (ISkinExternalPackageCaptureSession session = held.Session!)
            {
                heldFile.ChangeTime++;
                WindowsSkinPackageCaptureFileSystemException? exception = Assert.Throws<WindowsSkinPackageCaptureFileSystemException>(
                    () => session.Validate());
                Assert.That(exception!.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture));
            }

            Assert.That(identityPackage.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestExternalCaptureBudgetsFailClosedAndReleaseHandles()
        {
            FakeExternalPackage depthPackage = createExternalPackage();
            depthPackage.Package.AddFile("skin.ini", new byte[] { 1 });
            var depthLimits = new SkinExternalPackageCaptureLimits(
                SkinPackageRevisionCapsuleLimits.Default,
                maxAuthorityDepth: 1,
                maxHeldHandleCount: 20,
                maxLogicalManifestBytes: 1024);
            SkinExternalPackageCaptureResult depth = depthPackage.CaptureHeld(depthLimits);

            FakeExternalPackage handlePackage = createExternalPackage();
            handlePackage.Package.AddFile("skin.ini", new byte[] { 1 });
            var handleLimits = new SkinExternalPackageCaptureLimits(
                SkinPackageRevisionCapsuleLimits.Default,
                maxAuthorityDepth: 8,
                maxHeldHandleCount: 3,
                maxLogicalManifestBytes: 1024);
            SkinExternalPackageCaptureResult handles = handlePackage.CaptureHeld(handleLimits);

            FakeExternalPackage manifestPackage = createExternalPackage();
            manifestPackage.Package.AddFile("skin.ini", new byte[] { 1 });
            var manifestLimits = new SkinExternalPackageCaptureLimits(
                SkinPackageRevisionCapsuleLimits.Default,
                maxAuthorityDepth: 8,
                maxHeldHandleCount: 20,
                maxLogicalManifestBytes: 1);
            SkinExternalPackageCaptureResult manifest = manifestPackage.CaptureHeld(manifestLimits);

            Assert.Multiple(() =>
            {
                Assert.That(depth.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.AuthorityDepthBudgetExceeded));
                Assert.That(depthPackage.FileSystem.OperationCount, Is.Zero);
                Assert.That(depthPackage.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(handles.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.HeldHandleBudgetExceeded));
                Assert.That(handlePackage.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(handlePackage.FileSystem.OpenCount(handlePackage.Package.Children.Single()), Is.Zero);
                Assert.That(manifest.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.LogicalManifestBudgetExceeded));
                Assert.That(manifestPackage.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestDeterministicNestedCaptureAndOwnership()
        {
            FakePackage first = createPackage();
            FakeNode nested = first.Package.AddDirectory("nested");
            nested.AddDirectory("empty");
            nested.AddFile("b.bin", new byte[] { 2, 3 });
            first.Package.AddFile("a.bin", new byte[] { 1 });

            FakePackage second = createPackage();
            second.Package.AddFile("a.bin", new byte[] { 1 });
            FakeNode secondNested = second.Package.AddDirectory("nested");
            secondNested.AddFile("b.bin", new byte[] { 2, 3 });
            secondNested.AddDirectory("empty");

            SkinManagedPackageCaptureResult firstResult = first.Capture();
            SkinManagedPackageCaptureResult secondResult = second.Capture();

            Assert.Multiple(() =>
            {
                Assert.That(firstResult.IsSuccess, Is.True);
                Assert.That(secondResult.IsSuccess, Is.True);
                Assert.That(firstResult.Capsule!.ContentRevision, Is.EqualTo(secondResult.Capsule!.ContentRevision));
                Assert.That(first.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(second.FileSystem.ActiveHandleCount, Is.Zero);
            });

            using SkinPackageRevisionCapsule firstCapsule = firstResult.Capsule!;
            using SkinPackageRevisionCapsule secondCapsule = secondResult.Capsule!;
            using var resources = firstCapsule.CreateResourceView();
            Assert.That(resources.Get("nested/b.bin"), Is.EqualTo(new byte[] { 2, 3 }));
        }

        [Test]
        public void TestPhysicalTreeFingerprintIsStableLowercaseAndIncludesCapsuleRevision()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });

            SkinManagedPackageCaptureResult first = package.Capture();
            SkinManagedPackageCaptureResult second = package.Capture();
            file.Content[1] = 9;
            SkinManagedPackageCaptureResult contentChanged = package.Capture();

            using SkinPackageRevisionCapsule firstCapsule = first.Capsule!;
            using SkinPackageRevisionCapsule secondCapsule = second.Capsule!;
            using SkinPackageRevisionCapsule changedCapsule = contentChanged.Capsule!;

            Assert.Multiple(() =>
            {
                Assert.That(first.IsSuccess, Is.True);
                Assert.That(second.IsSuccess, Is.True);
                Assert.That(contentChanged.IsSuccess, Is.True);
                Assert.That(first.PhysicalTreeFingerprint, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(second.PhysicalTreeFingerprint, Is.EqualTo(first.PhysicalTreeFingerprint));
                Assert.That(secondCapsule.ContentRevision, Is.EqualTo(firstCapsule.ContentRevision));
                Assert.That(changedCapsule.ContentRevision, Is.Not.EqualTo(firstCapsule.ContentRevision));
                Assert.That(contentChanged.PhysicalTreeFingerprint, Is.Not.EqualTo(first.PhysicalTreeFingerprint));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestPhysicalTreeFingerprintOmitsOnlyRootRenameTimestamps()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1 });

            SkinManagedPackageCaptureResult baseline = package.Capture();
            package.Package.LastWriteTime++;
            package.Package.ChangeTime++;
            SkinManagedPackageCaptureResult rootTimestampsAdvanced = package.Capture();
            file.ChangeTime++;
            SkinManagedPackageCaptureResult descendantMetadataChanged = package.Capture();

            using SkinPackageRevisionCapsule baselineCapsule = baseline.Capsule!;
            using SkinPackageRevisionCapsule rootAdvancedCapsule = rootTimestampsAdvanced.Capsule!;
            using SkinPackageRevisionCapsule descendantChangedCapsule = descendantMetadataChanged.Capsule!;

            Assert.Multiple(() =>
            {
                Assert.That(rootAdvancedCapsule.ContentRevision, Is.EqualTo(baselineCapsule.ContentRevision));
                Assert.That(rootTimestampsAdvanced.PhysicalTreeFingerprint, Is.EqualTo(baseline.PhysicalTreeFingerprint));
                Assert.That(descendantChangedCapsule.ContentRevision, Is.EqualTo(baselineCapsule.ContentRevision));
                Assert.That(descendantMetadataChanged.PhysicalTreeFingerprint, Is.Not.EqualTo(baseline.PhysicalTreeFingerprint));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestPhysicalTreeFingerprintCoversOrdinalEmptyDirectoryInventory()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            FakeNode empty = package.Package.AddDirectory("empty");

            SkinManagedPackageCaptureResult baseline = package.Capture();
            empty.Name = "Empty";
            SkinManagedPackageCaptureResult recased = package.Capture();

            using SkinPackageRevisionCapsule baselineCapsule = baseline.Capsule!;
            using SkinPackageRevisionCapsule recasedCapsule = recased.Capsule!;

            Assert.Multiple(() =>
            {
                Assert.That(recasedCapsule.ContentRevision, Is.EqualTo(baselineCapsule.ContentRevision));
                Assert.That(recased.PhysicalTreeFingerprint, Is.Not.EqualTo(baseline.PhysicalTreeFingerprint));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestPhysicalTreeFingerprintCoversDirectoryInventoryBoundaries()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            FakeNode left = package.Package.AddDirectory("left");
            FakeNode right = package.Package.AddDirectory("right");
            FakeNode empty = left.AddDirectory("empty");

            SkinManagedPackageCaptureResult baseline = package.Capture();
            Assert.That(left.Children.Remove(empty), Is.True);
            right.Children.Add(empty);
            SkinManagedPackageCaptureResult reparented = package.Capture();

            using SkinPackageRevisionCapsule baselineCapsule = baseline.Capsule!;
            using SkinPackageRevisionCapsule reparentedCapsule = reparented.Capsule!;

            Assert.Multiple(() =>
            {
                Assert.That(reparentedCapsule.ContentRevision, Is.EqualTo(baselineCapsule.ContentRevision));
                Assert.That(reparented.PhysicalTreeFingerprint, Is.Not.EqualTo(baseline.PhysicalTreeFingerprint));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestProvisionalChildCaptureReturnsSamePhysicalTreeFingerprint()
        {
            FakePackage package = createPackage();
            package.Package.AddDirectory("empty");
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });

            SkinManagedPackageCaptureResult normal = package.Capture();
            SkinManagedPackageCaptureResult provisional = captureProvisional(package);

            using SkinPackageRevisionCapsule normalCapsule = normal.Capsule!;
            using SkinPackageRevisionCapsule provisionalCapsule = provisional.Capsule!;

            Assert.Multiple(() =>
            {
                Assert.That(provisional.IsSuccess, Is.True);
                Assert.That(provisional.PhysicalTreeFingerprint, Is.EqualTo(normal.PhysicalTreeFingerprint));
                Assert.That(provisionalCapsule.ContentRevision, Is.EqualTo(normalCapsule.ContentRevision));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestDecomposedPackageDirectoryRemainsStableAcrossFinalValidation()
        {
            FakePackage package = createPackage("e\u0301");
            package.Package.AddFile("skin.ini", new byte[] { 1 });

            SkinManagedPackageCaptureResult result = package.Capture();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            result.Capsule!.Dispose();
        }

        [Test]
        public void TestRequestCannotBeForgedOutsideResolver()
        {
            FakePackage package = createPackage();

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => new SkinManagedPackageCaptureRequest(@"C:\data", "package", new object()));
                Assert.Throws<InvalidOperationException>(() => new SkinExternalPackageCaptureRequest(
                    @"C:\external\package",
                    'C',
                    new[] { "external", "package" },
                    new object()));
                Assert.That(package.FileSystem.OperationCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestTypedResultFactoriesRejectUndefinedReasons()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => SkinManagedPackageCaptureResult.Reject((SkinManagedPackageCaptureRejectionReason)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => SkinManagedPackageCaptureResult.RejectCapsule((SkinPackageRevisionCapsuleRejectionReason)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => new WindowsSkinPackageCaptureFileSystemException((SkinManagedPackageCaptureRejectionReason)999));
            });
        }

        [Test]
        public void TestAlternatePathAliasRejected()
        {
            FakePackage package = createPackage();
            package.FileSystem.AddAlias(package.VolumeRoot, "DATA~1", package.DataRoot);
            SkinManagedPackageCaptureRequest aliasRequest = issueRequest(@"C:\DATA~1", "package");

            SkinManagedPackageCaptureResult result = new WindowsSkinManagedPackageCapture(package.FileSystem).Capture(aliasRequest);

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
        }

        [Test]
        public void TestPackageRemovedAfterRequestRejectedBeforeRead()
        {
            FakePackage package = createPackage();
            Assert.That(package.ManagedRoot.Children.Remove(package.Package), Is.True);

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OpenCount(package.Package), Is.Zero);
                Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
            });
        }

        [TestCase("volume")]
        [TestCase("data")]
        [TestCase("managed")]
        [TestCase("package")]
        [TestCase("nested")]
        public void TestReparseAtEveryAuthorityAndCapturedLevelRejectedWithoutFollowing(string level)
        {
            FakePackage package = createPackage();
            FakeNode target = level switch
            {
                "volume" => package.VolumeRoot,
                "data" => package.DataRoot,
                "managed" => package.ManagedRoot,
                "package" => package.Package,
                "nested" => package.Package.AddDirectory("junction"),
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
            target.IsReparsePoint = true;

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (level != "volume")
                Assert.That(package.FileSystem.OpenCount(target), Is.Zero);
        }

        [Test]
        public void TestEnumerationToOpenIdentitySwapRejectedBeforeRead()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package && operation.Index == 1)
                    file.FileId++;
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestMetadataMutationDuringReadRejected()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream && operation.Node == file)
                    file.ChangeTime++;
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            Assert.That(package.FileSystem.ReadStreamCount, Is.EqualTo(1));
        }

        [Test]
        public void TestFinalInventoryMutationRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    package.Package.AddFile("late.ini", new byte[] { 9 });
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestFinalPackageRootIdentitySwapRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind != FakeOperationKind.CreateReadStream)
                    return;

                package.ManagedRoot.Children.Remove(package.Package);
                FakeNode replacement = package.ManagedRoot.AddDirectory("package");
                replacement.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
        }

        [Test]
        public void TestFinalAuthorityAliasRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    package.VolumeRoot.AddDirectory("DATA");
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
        }

        [Test]
        public void TestMutationAfterEarlierFinalEnumerationCaughtByLastMetadataPass()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("root.bin", new byte[] { 1 });
            FakeNode nested = package.Package.AddDirectory("nested");
            nested.AddFile("nested.bin", new byte[] { 2 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate
                    && operation.Node == nested
                    && operation.Index == 2)
                {
                    package.Package.ChangeTime++;
                }
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
        }

        [Test]
        public void TestHardLinkAndDuplicateIdentityRejected()
        {
            FakePackage hardLinkPackage = createPackage();
            hardLinkPackage.Package.AddFile("skin.ini", new byte[] { 1 }).NumberOfLinks = 2;
            assertRejected(
                hardLinkPackage,
                hardLinkPackage.Capture(),
                SkinManagedPackageCaptureRejectionReason.HardLinkedFile);

            FakePackage duplicatePackage = createPackage();
            FakeNode first = duplicatePackage.Package.AddFile("a.ini", new byte[] { 1 });
            FakeNode second = duplicatePackage.Package.AddFile("b.ini", new byte[] { 2 });
            second.FileId = first.FileId;
            assertRejected(
                duplicatePackage,
                duplicatePackage.Capture(),
                SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity);
        }

        [Test]
        public void TestBusySourceMappedWithoutRead()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1 });
            file.IsBusy = true;

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.SourceBusy);
            Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestCapsuleNameCollisionAndBudgetReasonsPreserved()
        {
            FakePackage collisionPackage = createPackage();
            collisionPackage.Package.AddFile("é.ini", new byte[] { 1 });
            collisionPackage.Package.AddFile("e\u0301.ini", new byte[] { 2 });

            SkinManagedPackageCaptureResult collision = collisionPackage.Capture();
            assertCapsuleRejected(collisionPackage, collision, SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath);
            Assert.That(collisionPackage.FileSystem.ReadStreamCount, Is.Zero);

            FakePackage budgetPackage = createPackage();
            budgetPackage.Package.AddFile("large.bin", new byte[] { 1, 2, 3 });
            var limits = new SkinPackageRevisionCapsuleLimits(10, 10, 4, 100, 2, 10);

            SkinManagedPackageCaptureResult budget = budgetPackage.Capture(limits);
            assertCapsuleRejected(budgetPackage, budget, SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded);
            Assert.That(budgetPackage.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestEnumerationBudgetStopsAtLimitPlusOneBeforeOpeningSources()
        {
            FakePackage package = createPackage();

            for (int i = 0; i < 100; i++)
                package.Package.AddFile($"{i:D3}.bin", new byte[] { 1 });

            var limits = new SkinPackageRevisionCapsuleLimits(2, 100, 4, 100, 10, 1000);
            SkinManagedPackageCaptureResult result = package.Capture(limits);

            assertCapsuleRejected(package, result, SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.EnumerateEntry, package.Package), Is.EqualTo(3));
                Assert.That(package.FileSystem.OpenedCapturedFileCount, Is.Zero);
                Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationBeforeAndDuringCaptureCleansHandles()
        {
            FakePackage before = createPackage();
            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => before.Capture(cancellationToken: alreadyCancelled.Token));
            Assert.That(before.FileSystem.ActiveHandleCount, Is.Zero);

            FakePackage during = createPackage();
            during.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            using var cancellation = new CancellationTokenSource();
            during.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == during.Package && operation.Index == 1)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => during.Capture(cancellationToken: cancellation.Token));
            Assert.That(during.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestCancellationInsideWideEnumerationStopsImmediately()
        {
            FakePackage package = createPackage();

            for (int i = 0; i < 100; i++)
                package.Package.AddFile($"{i:D3}.bin", new byte[] { 1 });

            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.EnumerateEntry
                    && operation.Node == package.Package
                    && operation.Index == 3)
                {
                    cancellation.Cancel();
                }
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.EnumerateEntry, package.Package), Is.EqualTo(3));
                Assert.That(package.FileSystem.OpenedCapturedFileCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationBetweenBoundedReadChunksCleansBacking()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("large.bin", new byte[] { 1, 2, 3, 4, 5 });
            package.FileSystem.ReadChunkSize = 1;
            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.ReadSource && operation.Index == 2)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.ReadSource, package.Package.Children[0]), Is.EqualTo(2));
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationAfterCapsuleBuildCleansHandles()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package && operation.Index == 2)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.ReadStreamCount, Is.EqualTo(1));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestUnexpectedExceptionPropagatesAfterCleanup()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package)
                    throw new InvalidOperationException("sentinel");
            };

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => package.Capture());
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo("sentinel"));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestHandleDisposeFailureStillClosesAllHandlesAndDisposesCapsule()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            file.ThrowOnDispose = true;

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => package.Capture());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo("dispose-sentinel"));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestSafeStringsDoNotExposeSensitiveValues()
        {
            const string secret = "private-package-name";
            SkinManagedPackageCaptureRequest request = issueRequest(@"C:\private-root", secret);
            var entry = new WindowsSkinPackageDirectoryEntry(secret, FakeNode.CreateFile(secret, new byte[] { 1 }).Snapshot(forEnumeration: true));
            var exception = new WindowsSkinPackageCaptureFileSystemException(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            Assert.Multiple(() =>
            {
                Assert.That(request.ToString(), Does.Not.Contain(secret));
                Assert.That(request.ToString(), Does.Not.Contain("private-root"));
                Assert.That(entry.ToString(), Does.Not.Contain(secret));
                Assert.That(entry.Metadata.ToString(), Does.Not.Contain(secret));
                Assert.That(entry.Metadata.Identity.ToString(), Does.Not.Contain(secret));
                Assert.That(exception.ToString(), Does.Not.Contain(secret));
            });
        }

        [Test]
        public void TestManagedRootDiscoverySeparatesObservedFromValidAndUsesCapsuleMetadata()
        {
            FakePackage package = createPackage("valid-package");
            package.Package.AddFile("SKIN.INI", Encoding.UTF8.GetBytes("[General]\nName: Capsule Name\nAuthor: Capsule Creator\n"));
            package.ManagedRoot.AddDirectory("missing-ini").AddFile("asset.png", new byte[] { 1 });
            package.ManagedRoot.AddFile("occupied-by-file", new byte[] { 2 });
            FakeNode reparse = package.ManagedRoot.AddDirectory("reparse-package");
            reparse.IsReparsePoint = true;

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                Assert.That(snapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.None));
                Assert.That(snapshot.ObservedManagedRelativePaths, Is.EquivalentTo(new[]
                {
                    "chartskin/valid-package",
                    "chartskin/missing-ini",
                    "chartskin/occupied-by-file",
                    "chartskin/reparse-package",
                }));
                Assert.That(snapshot.ValidDiscoveries, Has.Count.EqualTo(1));
                Assert.That(snapshot.ValidDiscoveries[0].ManagedRelativePath, Is.EqualTo("chartskin/valid-package"));
                Assert.That(snapshot.ValidDiscoveries[0].Name, Is.EqualTo("Capsule Name"));
                Assert.That(snapshot.ValidDiscoveries[0].Creator, Is.EqualTo("Capsule Creator"));
                Assert.That(snapshot.ValidDiscoveries[0].ContentRevision, Is.Not.Empty);
                Assert.That(snapshot.ToString(), Does.Not.Contain("valid-package"));
                Assert.That(snapshot.ValidDiscoveries[0].ToString(), Does.Not.Contain("Capsule Name"));
                Assert.That(snapshot.ValidDiscoveries[0].ToString(), Does.Not.Contain(snapshot.ValidDiscoveries[0].ContentRevision));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestManagedRootDiscoveryInvalidUtf8RemainsObservedButNotValid()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 0xC3, 0x28 });

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                Assert.That(snapshot.ObservedManagedRelativePaths, Is.EqualTo(new[] { "chartskin/package" }));
                Assert.That(snapshot.ValidDiscoveries, Is.Empty);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestManagedRootDiscoveryRetainsCaseCollisionAsObservedButNotValid()
        {
            FakePackage package = createPackage("Package");
            package.Package.AddFile("skin.ini", Encoding.UTF8.GetBytes("Name: First"));
            package.ManagedRoot.AddDirectory("package").AddFile("skin.ini", Encoding.UTF8.GetBytes("Name: Second"));

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                Assert.That(snapshot.ObservedManagedRelativePaths, Has.Count.EqualTo(1));
                Assert.That(snapshot.ObservedManagedRelativePaths[0], Is.EqualTo("chartskin/Package"));
                Assert.That(snapshot.ValidDiscoveries, Is.Empty);
                Assert.That(package.FileSystem.OpenCount(package.Package), Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestManagedRootDiscoveryFinalInventoryRacePublishesNoPartialSnapshot()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", Encoding.UTF8.GetBytes("Name: Stable Before Race"));
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    package.ManagedRoot.AddDirectory("late-package");
            };

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.False);
                Assert.That(snapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.RootUnstable));
                Assert.That(snapshot.ObservedManagedRelativePaths, Is.Empty);
                Assert.That(snapshot.ValidDiscoveries, Is.Empty);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestManagedRootDiscoveryMissingRootIsIncompleteAndNonAuthoritative()
        {
            FakePackage package = createPackage();
            Assert.That(package.DataRoot.Children.Remove(package.ManagedRoot), Is.True);

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.False);
                Assert.That(snapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.RootUnavailable));
                Assert.That(snapshot.ObservedManagedRelativePaths, Is.Empty);
                Assert.That(snapshot.ValidDiscoveries, Is.Empty);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestManagedRootDiscoveryCancellationCleansHeldAuthorityHandles()
        {
            FakePackage package = createPackage();
            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.ManagedRoot)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() =>
                new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover(cancellation.Token));
            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestManagedRootDiscoveryUnexpectedFailureDoesNotExposeSensitiveException()
        {
            const string secret = @"C:\private-root\private-package";
            FakePackage package = createPackage();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.ManagedRoot)
                    throw new InvalidOperationException(secret);
            };

            SkinManagedFolderDiscoverySnapshot snapshot = new WindowsSkinManagedFolderDiscoverySource(@"C:\data", package.FileSystem).Discover();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsComplete, Is.False);
                Assert.That(snapshot.FailureReason, Is.EqualTo(SkinManagedFolderScanFailureReason.NativeFailure));
                Assert.That(snapshot.ToString(), Does.Not.Contain(secret));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        private static FakePackage createPackage(string packageName = "package")
        {
            FakeNode volumeRoot = FakeNode.CreateDirectory("C:");
            FakeNode dataRoot = volumeRoot.AddDirectory("data");
            FakeNode managedRoot = dataRoot.AddDirectory(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            FakeNode packageRoot = managedRoot.AddDirectory(packageName);
            var fileSystem = new FakeFileSystem(volumeRoot);
            SkinManagedPackageCaptureRequest request = issueRequest(@"C:\data", packageName);
            return new FakePackage(fileSystem, request, volumeRoot, dataRoot, managedRoot, packageRoot);
        }

        private static FakeExternalPackage createExternalPackage(string packageName = "package")
        {
            FakeNode volumeRoot = FakeNode.CreateDirectory("C:");
            FakeNode externalRoot = volumeRoot.AddDirectory("external");
            FakeNode packageRoot = externalRoot.AddDirectory(packageName);
            var fileSystem = new FakeFileSystem(volumeRoot);
            SkinExternalPackageCaptureRequest request = issueExternalRequest($@"C:\external\{packageName}");
            return new FakeExternalPackage(fileSystem, request, volumeRoot, externalRoot, packageRoot);
        }

        private static SkinManagedPackageCaptureRequest issueRequest(string dataRoot, string packageName)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo { FilesystemStoragePath = $"chartskin/{packageName}" },
                dataRoot,
                AllDirectoriesFilesystemInfoProvider.Instance);

            Assert.That(resolution.ManagedCaptureRequest, Is.Not.Null);
            return resolution.ManagedCaptureRequest!;
        }

        private static SkinExternalPackageCaptureRequest issueExternalRequest(string absolutePath)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo
                {
                    FilesystemStoragePath = absolutePath,
                    IsExternalFilesystemStorage = true,
                },
                @"C:\data",
                AllDirectoriesFilesystemInfoProvider.Instance);

            Assert.That(resolution.ExternalCaptureRequest, Is.Not.Null);
            return resolution.ExternalCaptureRequest!;
        }

        private static SkinManagedFolderPhysicalIdentity toMutationIdentity(FakeNode node)
        {
            WindowsSkinPackagePhysicalIdentity identity = node.Snapshot(forEnumeration: false).Identity;
            return new SkinManagedFolderPhysicalIdentity(
                identity.VolumeSerialNumber,
                identity.FileIdPart0,
                identity.FileIdPart1);
        }

        private static SkinManagedPackageCaptureResult captureProvisional(FakePackage package)
        {
            using IWindowsSkinPackageCaptureHandle volumeRoot = package.FileSystem.OpenLocalVolumeRoot('C');
            using IWindowsSkinPackageCaptureHandle dataRoot = package.FileSystem.OpenChildNoFollow(
                volumeRoot,
                "data",
                WindowsSkinPackageOpenMode.AuthorityDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
            using IWindowsSkinPackageCaptureHandle managedRoot = package.FileSystem.OpenChildNoFollow(
                dataRoot,
                SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY,
                WindowsSkinPackageOpenMode.AuthorityDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
            WindowsSkinPackageDirectoryEntry candidate = package.FileSystem.Enumerate(
                managedRoot,
                1,
                CancellationToken.None).Single();

            return new WindowsSkinManagedPackageCapture(package.FileSystem).CaptureProvisionalChild(
                managedRoot,
                candidate,
                cancellationToken: CancellationToken.None);
        }

        private static void assertRejected(
            FakePackage package,
            SkinManagedPackageCaptureResult result,
            SkinManagedPackageCaptureRejectionReason reason)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(reason));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.None));
                Assert.That(result.Capsule, Is.Null);
                Assert.That(result.PhysicalTreeFingerprint, Is.Null);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        private static void assertCapsuleRejected(
            FakePackage package,
            SkinManagedPackageCaptureResult result,
            SkinPackageRevisionCapsuleRejectionReason reason)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.CapsuleRejected));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(reason));
                Assert.That(result.Capsule, Is.Null);
                Assert.That(result.PhysicalTreeFingerprint, Is.Null);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        private sealed record FakePackage(
            FakeFileSystem FileSystem,
            SkinManagedPackageCaptureRequest Request,
            FakeNode VolumeRoot,
            FakeNode DataRoot,
            FakeNode ManagedRoot,
            FakeNode Package)
        {
            public SkinManagedPackageCaptureResult Capture(
                SkinPackageRevisionCapsuleLimits? limits = null,
                CancellationToken cancellationToken = default)
                => new WindowsSkinManagedPackageCapture(FileSystem).Capture(Request, limits, cancellationToken);
        }

        private sealed record FakeExternalPackage(
            FakeFileSystem FileSystem,
            SkinExternalPackageCaptureRequest Request,
            FakeNode VolumeRoot,
            FakeNode ExternalRoot,
            FakeNode Package)
        {
            public SkinExternalPackageCaptureResult CaptureHeld(
                SkinExternalPackageCaptureLimits? limits = null,
                CancellationToken cancellationToken = default)
                => new WindowsSkinManagedPackageCapture(FileSystem).CaptureExternalHeld(Request, limits, cancellationToken);

            public SkinExternalFolderAuthorityCaptureResult OpenAuthority(
                SkinExternalPackageCaptureLimits? limits = null,
                CancellationToken cancellationToken = default)
                => new WindowsSkinManagedPackageCapture(FileSystem).OpenExternalAuthority(Request, limits, cancellationToken);
        }

        private enum FakeOperationKind
        {
            OpenVolumeRoot,
            Enumerate,
            EnumerateEntry,
            OpenChild,
            QueryMetadata,
            RenameBegin,
            RenameEnd,
            Delete,
            CreateReadStream,
            ReadSource,
        }

        private enum MutationTreeHazard
        {
            Reparse,
            HardLink,
            DuplicateIdentity,
            BusyWriter,
        }

        private enum RenameInspectionSetup
        {
            SourceOnly,
            TargetOnly,
            Both,
            Neither,
            IdentityMismatch,
        }

        private readonly record struct FakeOperation(
            FakeOperationKind Kind,
            FakeNode Node,
            int Index,
            WindowsSkinPackageOpenMode? Mode = null);

        private sealed class FakeFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly FakeNode volumeRoot;
            private readonly Dictionary<(FakeOperationKind Kind, FakeNode Node), int> operationIndexes = new Dictionary<(FakeOperationKind, FakeNode), int>();
            private readonly Dictionary<(FakeNode Parent, string Alias), FakeNode> aliases = new Dictionary<(FakeNode, string), FakeNode>();
            private readonly Dictionary<FakeNode, int> openCounts = new Dictionary<FakeNode, int>();
            private readonly List<FakeHandle> activeHandles = new List<FakeHandle>();

            public Action<FakeOperation>? OnOperation { get; set; }

            public int ActiveHandleCount { get; private set; }

            public int OperationCount { get; private set; }

            public int ReadStreamCount { get; private set; }

            public int OpenedCapturedFileCount { get; private set; }

            public int HandleOpenCount { get; private set; }

            public int HandleDisposeCallCount { get; private set; }

            public byte[]? LastReadBuffer { get; private set; }

            public bool LastReadStreamDisposed { get; private set; }

            public int ReadChunkSize { get; set; } = int.MaxValue;

            public FakeFileSystem(FakeNode volumeRoot)
            {
                this.volumeRoot = volumeRoot;
            }

            public void AddAlias(FakeNode parent, string alias, FakeNode target) => aliases.Add((parent, alias), target);

            public int OpenCount(FakeNode node) => openCounts.GetValueOrDefault(node);

            public int OperationIndex(FakeOperationKind kind, FakeNode node) => operationIndexes.GetValueOrDefault((kind, node));

            public void MoveDirectChild(FakeNode parent, FakeNode child, string targetName)
            {
                if (!parent.Children.Remove(child))
                    throw new InvalidOperationException("The fake source is not a child of the expected parent.");

                child.Name = targetName;
                parent.Children.Add(child);
            }

            public bool TryMoveDirectChild(
                FakeNode sourceParent,
                FakeNode child,
                FakeNode targetParent,
                string targetName)
            {
                if (!sourceParent.Children.Contains(child)
                    || targetParent.Children.Any(candidate => namesEqual(candidate.Name, targetName)))
                {
                    throw new InvalidOperationException("The fake rename request is invalid.");
                }

                if (activeHandles.Any(handle =>
                        !handle.IsDisposed
                        && ReferenceEquals(handle.Node, child)
                        && handle.Mode is WindowsSkinPackageOpenMode.DeleteExclusiveDirectory
                            or WindowsSkinPackageOpenMode.DeleteExclusiveFile))
                {
                    return false;
                }

                sourceParent.Children.Remove(child);
                child.Name = targetName;
                targetParent.Children.Add(child);
                return true;
            }

            public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
            {
                invoke(FakeOperationKind.OpenVolumeRoot, volumeRoot);

                if (driveLetter != 'C')
                    throw failure(SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

                return open(volumeRoot);
            }

            public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
                IWindowsSkinPackageCaptureHandle directory,
                int maxEntries,
                CancellationToken cancellationToken)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(maxEntries);
                cancellationToken.ThrowIfCancellationRequested();
                FakeNode node = getNode(directory);

                if (node.Kind != WindowsSkinPackageEntryKind.Directory)
                    throw failure(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

                var snapshot = new List<WindowsSkinPackageDirectoryEntry>();

                foreach (FakeNode child in node.Children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    invoke(FakeOperationKind.EnumerateEntry, node);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (snapshot.Count >= maxEntries)
                        throw failure(SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);

                    snapshot.Add(new WindowsSkinPackageDirectoryEntry(child.Name, child.Snapshot(forEnumeration: true)));
                }

                invoke(FakeOperationKind.Enumerate, node);
                cancellationToken.ThrowIfCancellationRequested();
                return snapshot;
            }

            public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                WindowsSkinPackageOpenMode mode,
                SkinManagedPackageCaptureRejectionReason unavailableReason)
            {
                FakeNode parentNode = getNode(parent);
                invoke(FakeOperationKind.OpenChild, parentNode, mode);
                FakeNode? child = parentNode.Children.SingleOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

                if (child == null)
                    aliases.TryGetValue((parentNode, name), out child);

                if (child == null)
                    throw failure(unavailableReason);

                WindowsSkinPackageEntryKind expectedKind = mode == WindowsSkinPackageOpenMode.CapturedFile
                                                            || mode == WindowsSkinPackageOpenMode.MutationSourceVerificationFile
                                                            || mode == WindowsSkinPackageOpenMode.ProvisionalFile
                                                            || mode == WindowsSkinPackageOpenMode.DeleteExclusiveFile
                    ? WindowsSkinPackageEntryKind.File
                    : WindowsSkinPackageEntryKind.Directory;

                if (child.Kind != expectedKind)
                    throw failure(unavailableReason);

                if (child.IsBusy)
                    throw failure(SkinManagedPackageCaptureRejectionReason.SourceBusy);

                if (mode == WindowsSkinPackageOpenMode.CapturedFile)
                    OpenedCapturedFileCount++;

                return open(child, mode);
            }

            public WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle)
            {
                FakeNode node = getNode(handle);
                invoke(FakeOperationKind.QueryMetadata, node);
                return node.Snapshot(forEnumeration: false);
            }

            public void RenameChildNoReplace(
                IWindowsSkinPackageCaptureHandle source,
                IWindowsSkinPackageCaptureHandle targetParent,
                string targetName)
            {
                FakeNode sourceNode = getNode(source);
                FakeNode parentNode = getNode(targetParent);
                invoke(FakeOperationKind.RenameBegin, sourceNode);

                if (parentNode.Children.Any(child => namesEqual(child.Name, targetName)))
                    throw failure(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

                FakeNode[] currentParents = enumerateNodes(volumeRoot)
                                            .Where(candidate => candidate.Children.Contains(sourceNode))
                                            .ToArray();

                if (currentParents.Length != 1)
                    throw failure(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                Assert.That(currentParents[0].Children.Remove(sourceNode), Is.True);
                sourceNode.Name = targetName;
                parentNode.Children.Add(sourceNode);
                invoke(FakeOperationKind.RenameEnd, sourceNode);
            }

            public void DeleteNoFollow(IWindowsSkinPackageCaptureHandle handle)
            {
                FakeHandle fakeHandle = getHandle(handle);
                FakeNode node = fakeHandle.Node;
                invoke(FakeOperationKind.Delete, node);

                if (node.DeletePending)
                    throw failure(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);

                if (node.Kind == WindowsSkinPackageEntryKind.Directory && node.Children.Count != 0)
                    throw failure(SkinManagedPackageCaptureRejectionReason.InventoryChanged);

                FakeNode[] currentParents = enumerateNodes(volumeRoot)
                                            .Where(candidate => candidate.Children.Contains(node))
                                            .ToArray();

                if (currentParents.Length != 1)
                    throw failure(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                node.DeletePending = true;
                fakeHandle.DeleteOnClose = true;
            }

            public Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file)
            {
                FakeNode node = getNode(file);
                invoke(FakeOperationKind.CreateReadStream, node);
                ReadStreamCount++;
                return new TrackingReadStream(
                    node.Content,
                    buffer => LastReadBuffer = buffer,
                    () => invoke(FakeOperationKind.ReadSource, node),
                    ReadChunkSize,
                    () => LastReadStreamDisposed = true);
            }

            private FakeHandle open(
                FakeNode node,
                WindowsSkinPackageOpenMode? mode = null)
            {
                ActiveHandleCount++;
                HandleOpenCount++;
                openCounts[node] = openCounts.GetValueOrDefault(node) + 1;
                var handle = new FakeHandle(this, node, mode);
                activeHandles.Add(handle);
                return handle;
            }

            private FakeNode getNode(IWindowsSkinPackageCaptureHandle handle)
                => getHandle(handle).Node;

            private static FakeHandle getHandle(IWindowsSkinPackageCaptureHandle handle)
            {
                if (handle is not FakeHandle fake)
                    throw new ArgumentException(nameof(handle));

                ObjectDisposedException.ThrowIf(fake.IsDisposed, fake);

                return fake;
            }

            private void invoke(
                FakeOperationKind kind,
                FakeNode node,
                WindowsSkinPackageOpenMode? mode = null)
            {
                OperationCount++;
                int index = operationIndexes.GetValueOrDefault((kind, node)) + 1;
                operationIndexes[(kind, node)] = index;
                OnOperation?.Invoke(new FakeOperation(kind, node, index, mode));
            }

            private void close(FakeHandle handle)
            {
                ActiveHandleCount--;
                Assert.That(ActiveHandleCount, Is.GreaterThanOrEqualTo(0));
                Assert.That(activeHandles.Remove(handle), Is.True);

                if (!handle.DeleteOnClose)
                    return;

                FakeNode[] currentParents = enumerateNodes(volumeRoot)
                                            .Where(candidate => candidate.Children.Contains(handle.Node))
                                            .ToArray();

                Assert.That(currentParents, Has.Length.EqualTo(1));
                Assert.That(currentParents[0].Children.Remove(handle.Node), Is.True);
            }

            private static IEnumerable<FakeNode> enumerateNodes(FakeNode root)
            {
                yield return root;

                foreach (FakeNode child in root.Children)
                {
                    foreach (FakeNode descendant in enumerateNodes(child))
                        yield return descendant;
                }
            }

            private static bool namesEqual(string left, string right)
            {
                try
                {
                    return string.Equals(
                        left.Normalize(NormalizationForm.FormC),
                        right.Normalize(NormalizationForm.FormC),
                        StringComparison.OrdinalIgnoreCase);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            private static WindowsSkinPackageCaptureFileSystemException failure(SkinManagedPackageCaptureRejectionReason reason)
                => new WindowsSkinPackageCaptureFileSystemException(reason);

            private sealed class FakeHandle : IWindowsSkinPackageCaptureHandle
            {
                private readonly FakeFileSystem owner;

                public FakeNode Node { get; }

                public bool IsDisposed { get; private set; }

                public bool DeleteOnClose { get; set; }

                public WindowsSkinPackageOpenMode? Mode { get; }

                public FakeHandle(
                    FakeFileSystem owner,
                    FakeNode node,
                    WindowsSkinPackageOpenMode? mode)
                {
                    this.owner = owner;
                    Node = node;
                    Mode = mode;
                }

                public void Dispose()
                {
                    owner.HandleDisposeCallCount++;

                    if (IsDisposed)
                        return;

                    IsDisposed = true;
                    owner.close(this);

                    if (Node.ThrowOnDispose)
                        throw new InvalidOperationException("dispose-sentinel");
                }

                public override string ToString() => nameof(FakeHandle);
            }

            private sealed class TrackingReadStream : Stream
            {
                private readonly byte[] content;
                private readonly Action<byte[]> onReadBuffer;
                private readonly Action onRead;
                private readonly Action onDisposed;
                private readonly int maxReadSize;
                private int position;

                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => content.Length;

                public override long Position
                {
                    get => position;
                    set => throw new NotSupportedException();
                }

                public TrackingReadStream(
                    byte[] content,
                    Action<byte[]> onReadBuffer,
                    Action onRead,
                    int maxReadSize,
                    Action onDisposed)
                {
                    this.content = content;
                    this.onReadBuffer = onReadBuffer;
                    this.onRead = onRead;
                    this.maxReadSize = maxReadSize;
                    this.onDisposed = onDisposed;
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    onReadBuffer(buffer);
                    onRead();
                    int read = Math.Min(Math.Min(count, maxReadSize), content.Length - position);
                    content.AsSpan(position, read).CopyTo(buffer.AsSpan(offset, read));
                    position += read;
                    return read;
                }

                public override int ReadByte()
                {
                    if (position >= content.Length)
                        return -1;

                    return content[position++];
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                        onDisposed();

                    base.Dispose(disposing);
                }

                public override void Flush() => throw new NotSupportedException();
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            }
        }

        private sealed class AllDirectoriesFilesystemInfoProvider : SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider
        {
            public static AllDirectoriesFilesystemInfoProvider Instance { get; } = new AllDirectoriesFilesystemInfoProvider();

            public FileAttributes GetAttributes(string path) => FileAttributes.Directory;
        }

        private sealed class FakeNode
        {
            private static ulong next_id = 10;

            public string Name { get; set; }

            public WindowsSkinPackageEntryKind Kind { get; }

            public List<FakeNode> Children { get; } = new List<FakeNode>();

            public byte[] Content { get; }

            public ulong FileId { get; set; }

            public long ChangeTime { get; set; }

            public long LastWriteTime { get; set; }

            public long CreationTime { get; set; }

            public uint NumberOfLinks { get; set; } = 1;

            public bool IsReparsePoint { get; set; }

            public bool IsBusy { get; set; }

            public bool ThrowOnDispose { get; set; }

            public bool DeletePending { get; set; }

            private FakeNode(string name, WindowsSkinPackageEntryKind kind, byte[] content)
            {
                Name = name;
                Kind = kind;
                Content = content;
                FileId = next_id++;
                CreationTime = checked((long)FileId + 100);
                ChangeTime = checked((long)FileId + 1000);
                LastWriteTime = checked((long)FileId + 200);
            }

            public static FakeNode CreateDirectory(string name) => new FakeNode(name, WindowsSkinPackageEntryKind.Directory, Array.Empty<byte>());

            public static FakeNode CreateFile(string name, byte[] content) => new FakeNode(name, WindowsSkinPackageEntryKind.File, (byte[])content.Clone());

            public FakeNode AddDirectory(string name)
            {
                FakeNode child = CreateDirectory(name);
                Children.Add(child);
                return child;
            }

            public FakeNode AddFile(string name, byte[] content)
            {
                FakeNode child = CreateFile(name, content);
                Children.Add(child);
                return child;
            }

            public WindowsSkinPackageEntryMetadata Snapshot(bool forEnumeration)
            {
                uint attributes = Kind == WindowsSkinPackageEntryKind.Directory ? 0x10u : 0x80u;
                uint reparseTag = 0;

                if (IsReparsePoint)
                {
                    attributes |= 0x400;
                    reparseTag = 0xA0000003;
                }

                return new WindowsSkinPackageEntryMetadata(
                    new WindowsSkinPackagePhysicalIdentity(1, FileId, FileId * 17),
                    Kind,
                    Content.LongLength,
                    CreationTime,
                    LastWriteTime,
                    ChangeTime,
                    attributes,
                    reparseTag,
                    forEnumeration ? 0 : NumberOfLinks,
                    DeletePending);
            }
        }
    }
}
