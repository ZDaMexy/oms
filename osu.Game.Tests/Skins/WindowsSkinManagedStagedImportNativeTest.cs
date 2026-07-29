// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class WindowsSkinManagedStagedImportNativeTest
    {
        private Guid operationId;
        private string dataRoot = null!;
        private string managedRoot = null!;
        private string stagingRoot = null!;
        private string stagedSource = null!;
        private string targetRoot = null!;
        private NativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            operationId = Guid.NewGuid();
            dataRoot = Path.Combine(
                Path.GetTempPath(),
                $"oms-skin-staged-native-{Guid.NewGuid():N}");
            managedRoot = Path.Combine(
                dataRoot,
                SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            stagingRoot = Path.Combine(dataRoot, "skin-mutation-staging");
            stagedSource = Path.Combine(
                stagingRoot,
                operationId.ToString("N"));
            targetRoot = Path.Combine(managedRoot, "published-slot");
            Directory.CreateDirectory(managedRoot);
            Directory.CreateDirectory(stagedSource);
            createValidPackage(stagedSource);
            storage = new NativeStorage(dataRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dataRoot))
                Directory.Delete(dataRoot, true);
        }

        [Test]
        public void TestSameVolumeNonEmptyMovePreservesIdentityBytesAndFinalCapsule()
        {
            byte[] initialSkinIni =
                File.ReadAllBytes(Path.Combine(stagedSource, "skin.ini"));
            byte[] initialTexture =
                File.ReadAllBytes(
                    Path.Combine(stagedSource, "nested", "note.png"));
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;
            string revision;

            using (WindowsSkinManagedAuthoritySession session =
                   WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentMutationTargetNameSlot(
                        "chartskin/published-slot",
                        CancellationToken.None);

                using SkinManagedFolderStagedSourceCapture staged =
                    session.CaptureStagedMutationSource(
                        operationId,
                        CancellationToken.None);
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
                revision = staged.Capsule.ContentRevision;

                using SkinManagedFolderStagedImportFilesystemResult moved =
                    session.MoveCapturedStagedMutationSourceToTarget(
                        target,
                        revision,
                        staged.TreeFingerprint,
                        CancellationToken.None);
                using var movedResources = moved.Capsule.CreateResourceView();

                Assert.Multiple(() =>
                {
                    Assert.That(moved.TargetIdentity, Is.EqualTo(sourceIdentity));
                    Assert.That(
                        moved.Capsule.ContentRevision,
                        Is.EqualTo(revision));
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                    Assert.That(
                        movedResources.Get("skin.ini"),
                        Is.EqualTo(initialSkinIni));
                    Assert.That(
                        movedResources.Get("nested/note.png"),
                        Is.EqualTo(initialTexture));
                    Assert.DoesNotThrow(
                        () => session.ValidateCompleteAndStable(
                            CancellationToken.None));
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(
                    File.ReadAllBytes(Path.Combine(targetRoot, "skin.ini")),
                    Is.EqualTo(initialSkinIni));
                Assert.That(
                    File.ReadAllBytes(
                        Path.Combine(targetRoot, "nested", "note.png")),
                    Is.EqualTo(initialTexture));
            });

            using (ISkinManagedFolderMutationNativeSession restarted =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderStagedImportInspection inspection =
                    restarted.InspectStagedImportState(
                        operationId,
                        "chartskin/published-slot",
                        stagingIdentity,
                        sourceIdentity,
                        CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        inspection.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus
                                .TargetOnly));
                    Assert.That(
                        inspection.TargetIdentity,
                        Is.EqualTo(sourceIdentity));
                    Assert.That(inspection.PackageMetadata, Is.Not.Null);
                    Assert.That(
                        inspection.PackageMetadata!.Name,
                        Is.EqualTo("Native staged display"));
                    Assert.That(
                        inspection.PackageMetadata.Creator,
                        Is.EqualTo("OMS native test"));
                    Assert.That(
                        inspection.PackageMetadata.ContentRevision,
                        Is.EqualTo(revision));
                });
            }

            string movedAside = targetRoot + "-released";
            Assert.DoesNotThrow(() => Directory.Move(targetRoot, movedAside));
            Assert.DoesNotThrow(() => Directory.Move(movedAside, targetRoot));
        }

        [Test]
        public void TestExactNonEmptyProvisionalCleanupDeletesOnlyStagedSource()
        {
            string unrelated = Path.Combine(stagingRoot, "unrelated");
            Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "keep");
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot(
                        "chartskin/published-slot",
                        CancellationToken.None);
                using SkinManagedFolderStagedSourceCapture staged =
                    session.CaptureStagedSource(
                        operationId,
                        CancellationToken.None);

                session.CleanupExactStagedSource(
                    operationId,
                    target.ManagedRelativePath,
                    staged.StagedRootIdentity,
                    staged.SourceIdentity,
                    CancellationToken.None);

                SkinManagedFolderStagedImportInspection inspection =
                    session.InspectStagedImportState(
                        operationId,
                        target.ManagedRelativePath,
                        staged.StagedRootIdentity,
                        staged.SourceIdentity,
                        CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        inspection.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus.Neither));
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(Directory.Exists(unrelated), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(unrelated, "keep.txt")),
                        Is.EqualTo("keep"));
                });
            }
        }

        [Test]
        public void TestRestartCleanupCompletesPartiallyDeletedExactSource()
        {
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            using (SkinManagedFolderStagedSourceCapture staged =
                   session.CaptureStagedSource(
                       operationId,
                       CancellationToken.None))
            {
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
            }

            File.Delete(Path.Combine(stagedSource, "skin.ini"));
            Directory.Delete(Path.Combine(stagedSource, "nested"), true);

            using (ISkinManagedFolderMutationNativeSession restarted =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderStagedImportInspection before =
                    restarted.InspectStagedImportState(
                        operationId,
                        "chartskin/published-slot",
                        stagingIdentity,
                        sourceIdentity,
                        CancellationToken.None);
                Assert.Multiple(() =>
                {
                    Assert.That(
                        before.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus
                                .SourceOnly));
                    Assert.That(before.PackageMetadata, Is.Null);
                });

                restarted.CleanupExactStagedSource(
                    operationId,
                    "chartskin/published-slot",
                    stagingIdentity,
                    sourceIdentity,
                    CancellationToken.None);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(stagedSource), Is.False);
                Assert.That(Directory.Exists(targetRoot), Is.False);
            });
        }

        [Test]
        public void TestLateTargetCreationFailsClosedWithoutReplacingEitherTree()
        {
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using ISkinManagedFolderMutationNativeSession session =
                authority.Open(CancellationToken.None);
            SkinManagedFolderTargetNameSlot target =
                session.CaptureAbsentTargetNameSlot(
                    "chartskin/published-slot",
                    CancellationToken.None);
            using SkinManagedFolderStagedSourceCapture staged =
                session.CaptureStagedSource(
                    operationId,
                    CancellationToken.None);
            using var stagedResources = staged.Capsule.CreateResourceView();

            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "foreign.txt"), "foreign");

            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.MoveCapturedStagedSourceToTarget(
                    target,
                    staged.Capsule.ContentRevision,
                    staged.TreeFingerprint,
                    CancellationToken.None));
            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(stagedSource), Is.True);
                Assert.That(
                    stagedResources.Get("nested/note.png"),
                    Is.EqualTo(
                        new UTF8Encoding(false).GetBytes("native-bytes")));
                Assert.That(Directory.Exists(targetRoot), Is.True);
                Assert.That(
                    File.ReadAllText(Path.Combine(targetRoot, "foreign.txt")),
                    Is.EqualTo("foreign"));
            });
        }

        [Test]
        public void TestStagedCaptureRejectsBusyWriter()
        {
            string skinIni = Path.Combine(stagedSource, "skin.ini");
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using (File.Open(
                       skinIni,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureStagedSource(
                        operationId,
                        CancellationToken.None));
            }
        }

        [Test]
        public void TestStagedCaptureRejectsHardLinkDuplicateIdentity()
        {
            string skinIni = Path.Combine(stagedSource, "skin.ini");
            string hardLink = Path.Combine(stagedSource, "hard-link.ini");

            if (!HardLinkHelper.TryCreateHardLink(hardLink, skinIni))
                Assert.Ignore("Hard-link creation is unavailable.");

            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);

            using ISkinManagedFolderMutationNativeSession session =
                authority.Open(CancellationToken.None);
            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => session.CaptureStagedSource(
                    operationId,
                    CancellationToken.None));
        }

        [Test]
        public void TestStagedCaptureRejectsReparse()
        {
            string reparseTarget = Path.Combine(dataRoot, "reparse-target");
            string reparseChild = Path.Combine(stagedSource, "reparse-child");
            Directory.CreateDirectory(reparseTarget);

            try
            {
                if (!tryCreateDirectoryJunction(reparseChild, reparseTarget))
                    Assert.Ignore("Directory junction creation is unavailable.");

                var authority =
                    new WindowsSkinManagedFolderMutationNativeAuthority(
                        storage);
                using ISkinManagedFolderMutationNativeSession session =
                    authority.Open(CancellationToken.None);
                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => session.CaptureStagedSource(
                        operationId,
                        CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(reparseChild))
                    Directory.Delete(reparseChild);
            }
        }

        [TestCase("target-only")]
        [TestCase("both")]
        [TestCase("source-mismatch")]
        [TestCase("staging-root-mismatch")]
        public void TestCleanupRejectsNonSourceOnlyStatesWithoutDeletingEitherTree(
            string state)
        {
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            using (SkinManagedFolderStagedSourceCapture staged =
                   session.CaptureStagedSource(
                       operationId,
                       CancellationToken.None))
            {
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
            }

            string? originalStagingRoot = null;
            string? originalStagedSource = null;

            switch (state)
            {
                case "target-only":
                    Directory.Move(stagedSource, targetRoot);
                    break;

                case "both":
                    Directory.CreateDirectory(targetRoot);
                    createValidPackage(targetRoot);
                    break;

                case "source-mismatch":
                    Directory.Delete(stagedSource, true);
                    Directory.CreateDirectory(stagedSource);
                    createValidPackage(stagedSource);
                    break;

                case "staging-root-mismatch":
                    originalStagingRoot = stagingRoot + "-original";
                    Directory.Move(stagingRoot, originalStagingRoot);
                    originalStagedSource = Path.Combine(
                        originalStagingRoot,
                        operationId.ToString("N"));
                    Directory.CreateDirectory(stagedSource);
                    createValidPackage(stagedSource);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }

            using ISkinManagedFolderMutationNativeSession cleanup =
                authority.Open(CancellationToken.None);
            Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                () => cleanup.CleanupExactStagedSource(
                    operationId,
                    "chartskin/published-slot",
                    stagingIdentity,
                    sourceIdentity,
                    CancellationToken.None));

            Assert.Multiple(() =>
            {
                switch (state)
                {
                    case "target-only":
                        Assert.That(Directory.Exists(stagedSource), Is.False);
                        Assert.That(Directory.Exists(targetRoot), Is.True);
                        Assert.That(
                            File.ReadAllText(
                                Path.Combine(
                                    targetRoot,
                                    "nested",
                                    "note.png")),
                            Is.EqualTo("native-bytes"));
                        break;

                    case "both":
                        Assert.That(Directory.Exists(stagedSource), Is.True);
                        Assert.That(Directory.Exists(targetRoot), Is.True);
                        break;

                    case "source-mismatch":
                        Assert.That(Directory.Exists(stagedSource), Is.True);
                        Assert.That(Directory.Exists(targetRoot), Is.False);
                        break;

                    case "staging-root-mismatch":
                        Assert.That(Directory.Exists(stagedSource), Is.True);
                        Assert.That(
                            Directory.Exists(originalStagedSource),
                            Is.True);
                        Assert.That(Directory.Exists(targetRoot), Is.False);
                        break;
                }
            });
        }

        [Test]
        public void TestRestartInspectionRejectsCaseAliasOfExactTarget()
        {
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            {
                SkinManagedFolderTargetNameSlot target =
                    session.CaptureAbsentTargetNameSlot(
                        "chartskin/published-slot",
                        CancellationToken.None);
                using SkinManagedFolderStagedSourceCapture staged =
                    session.CaptureStagedSource(
                        operationId,
                        CancellationToken.None);
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
                using SkinManagedFolderStagedImportFilesystemResult moved =
                    session.MoveCapturedStagedSourceToTarget(
                        target,
                        staged.Capsule.ContentRevision,
                        staged.TreeFingerprint,
                        CancellationToken.None);
            }

            string temporary = targetRoot + "-temporary";
            string alias = Path.Combine(managedRoot, "PUBLISHED-SLOT");
            Directory.Move(targetRoot, temporary);
            Directory.Move(temporary, alias);

            using ISkinManagedFolderMutationNativeSession restarted =
                authority.Open(CancellationToken.None);
            SkinManagedFolderStagedImportInspection inspection =
                restarted.InspectStagedImportState(
                    operationId,
                    "chartskin/published-slot",
                    stagingIdentity,
                    sourceIdentity,
                    CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(
                    inspection.Status,
                    Is.EqualTo(
                        SkinManagedFolderStagedImportInspectionStatus
                            .IdentityMismatch));
                Assert.That(Directory.Exists(alias), Is.True);
                Assert.That(
                    File.ReadAllText(
                        Path.Combine(alias, "nested", "note.png")),
                    Is.EqualTo("native-bytes"));
            });
        }

        [Test]
        public void TestRestartInspectionCoversSourceTargetBothNeitherAndMismatch()
        {
            var authority =
                new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (ISkinManagedFolderMutationNativeSession session =
                   authority.Open(CancellationToken.None))
            using (SkinManagedFolderStagedSourceCapture staged =
                   session.CaptureStagedSource(
                       operationId,
                       CancellationToken.None))
            {
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
            }

            assertStatus(
                SkinManagedFolderStagedImportInspectionStatus.SourceOnly);
            Directory.Move(stagedSource, targetRoot);
            assertStatus(
                SkinManagedFolderStagedImportInspectionStatus.TargetOnly);
            Directory.CreateDirectory(stagedSource);
            createValidPackage(stagedSource);
            assertStatus(SkinManagedFolderStagedImportInspectionStatus.Both);
            Directory.Delete(stagedSource, true);
            Directory.Delete(targetRoot, true);
            assertStatus(SkinManagedFolderStagedImportInspectionStatus.Neither);
            Directory.CreateDirectory(stagedSource);
            createValidPackage(stagedSource);
            assertStatus(
                SkinManagedFolderStagedImportInspectionStatus.IdentityMismatch);

            void assertStatus(
                SkinManagedFolderStagedImportInspectionStatus expected)
            {
                using WindowsSkinManagedAuthoritySession session =
                    WindowsSkinManagedAuthoritySession.Open(
                        dataRoot,
                        new NativeWindowsSkinPackageCaptureFileSystem(),
                        CancellationToken.None);
                SkinManagedFolderStagedImportInspection inspection =
                    session.InspectStagedMutationImportState(
                        operationId,
                        "chartskin/published-slot",
                        stagingIdentity,
                        sourceIdentity,
                        CancellationToken.None);
                Assert.That(inspection.Status, Is.EqualTo(expected));
                Assert.That(inspection.ToString(), Does.Not.Contain(dataRoot));
            }
        }

        [Test]
        public void TestRestartInspectionRejectsReplacedRootsAndTargetIdentity()
        {
            SkinManagedFolderPhysicalIdentity managedIdentity;
            SkinManagedFolderPhysicalIdentity stagingIdentity;
            SkinManagedFolderPhysicalIdentity sourceIdentity;

            using (WindowsSkinManagedAuthoritySession session =
                   WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            using (SkinManagedFolderStagedSourceCapture staged =
                   session.CaptureStagedMutationSource(
                       operationId,
                       CancellationToken.None))
            {
                managedIdentity = session.ManagedRootIdentity;
                stagingIdentity = staged.StagedRootIdentity;
                sourceIdentity = staged.SourceIdentity;
            }

            string originalManagedRoot = managedRoot + "-original";
            Directory.Move(managedRoot, originalManagedRoot);
            Directory.CreateDirectory(managedRoot);

            using (WindowsSkinManagedAuthoritySession replacedManaged =
                   WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                SkinManagedFolderStagedImportInspection inspection =
                    replacedManaged.InspectStagedMutationImportState(
                        operationId,
                        "chartskin/published-slot",
                        stagingIdentity,
                        sourceIdentity,
                        CancellationToken.None);
                Assert.Multiple(() =>
                {
                    Assert.That(
                        replacedManaged.ManagedRootIdentity,
                        Is.Not.EqualTo(managedIdentity));
                    Assert.That(
                        inspection.Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportInspectionStatus
                                .SourceOnly));
                });
            }

            Directory.Delete(managedRoot);
            Directory.Move(originalManagedRoot, managedRoot);

            string originalStagingRoot = stagingRoot + "-original";
            Directory.Move(stagingRoot, originalStagingRoot);
            Directory.CreateDirectory(stagedSource);
            createValidPackage(stagedSource);

            assertInspection(
                SkinManagedFolderStagedImportInspectionStatus
                    .RootIdentityMismatch);

            Directory.Delete(stagingRoot, true);
            Directory.Move(originalStagingRoot, stagingRoot);
            Directory.Move(stagedSource, targetRoot);
            Directory.Delete(targetRoot, true);
            Directory.CreateDirectory(targetRoot);
            createValidPackage(targetRoot);

            assertInspection(
                SkinManagedFolderStagedImportInspectionStatus
                    .IdentityMismatch);

            void assertInspection(
                SkinManagedFolderStagedImportInspectionStatus expected)
            {
                using WindowsSkinManagedAuthoritySession session =
                    WindowsSkinManagedAuthoritySession.Open(
                        dataRoot,
                        new NativeWindowsSkinPackageCaptureFileSystem(),
                        CancellationToken.None);
                SkinManagedFolderStagedImportInspection inspection =
                    session.InspectStagedMutationImportState(
                        operationId,
                        "chartskin/published-slot",
                        stagingIdentity,
                        sourceIdentity,
                        CancellationToken.None);
                Assert.That(inspection.Status, Is.EqualTo(expected));
            }
        }

        private static void createValidPackage(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\nName: Native staged display\nAuthor: OMS native test\n",
                new UTF8Encoding(false, true));
            File.WriteAllText(
                Path.Combine(root, "nested", "note.png"),
                "native-bytes",
                new UTF8Encoding(false));
        }

        private static bool tryCreateDirectoryJunction(
            string linkPath,
            string targetPath)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/J");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(targetPath);

            using System.Diagnostics.Process? process =
                System.Diagnostics.Process.Start(startInfo);

            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0
                   && Directory.Exists(linkPath)
                   && File.GetAttributes(linkPath)
                          .HasFlag(FileAttributes.ReparsePoint);
        }
    }
}
