// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class WindowsSkinManagedCopyNativeTest
    {
        private const string record_fingerprint = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string capture_fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static readonly byte[] skin_ini_bytes = "[General]\nName: Managed copy native\nAuthor: OMS tests\n"u8.ToArray();
        private static readonly byte[] note_bytes = { 1, 3, 3, 7, 0, 255 };

        private Guid operationId;
        private string dataRoot = null!;
        private string managedRoot = null!;
        private string stagingRoot = null!;
        private string operationRoot = null!;
        private string sourceRoot = null!;

        [SetUp]
        public void SetUp()
        {
            operationId = Guid.NewGuid();
            dataRoot = Path.Combine(Path.GetTempPath(), $"oms-skin-managed-copy-native-{Guid.NewGuid():N}");
            managedRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            stagingRoot = Path.Combine(dataRoot, "skin-mutation-staging");
            operationRoot = Path.Combine(stagingRoot, operationId.ToString("N"));
            sourceRoot = Path.Combine(Path.GetTempPath(), $"oms-skin-managed-copy-source-{Guid.NewGuid():N}");
            Directory.CreateDirectory(managedRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, true);

            if (Directory.Exists(dataRoot))
                Directory.Delete(dataRoot, true);
        }

        [Test]
        public void TestPreparedAndCopyingAreDurableBeforeCapsuleOnlyProvisionalWrite()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            var store = new SkinManagedFolderMutationJournalStore(new NativeStorage(dataRoot));

            SkinManagedFolderPhysicalIdentity stagedRootIdentity = session.PrepareManagedCopyStaging(
                operationId,
                CancellationToken.None);
            SkinManagedFolderMutationJournal prepared = createPreparedJournal(
                session.ManagedRootIdentity,
                stagedRootIdentity,
                package);
            store.Write(prepared);
            assertLoadedExact(store, prepared);

            Assert.That(Directory.Exists(operationRoot), Is.False,
                "The operation root must not precede the durable Prepared owner.");

            SkinManagedFolderPhysicalIdentity provisionalRootIdentity = session.CreateManagedCopyProvisionalRoot(
                operationId,
                CancellationToken.None);
            SkinManagedFolderMutationJournal copying = prepared.WithCopying(provisionalRootIdentity);
            store.Write(copying);
            assertLoadedExact(store, copying);
            int firstWriteCalls = 0;

            session.WriteManagedCopyProvisional(
                operationId,
                package.Capsule,
                package.Manifest,
                () =>
                {
                    firstWriteCalls++;
                    assertLoadedExact(store, copying);
                },
                CancellationToken.None);
            session.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(sourceRoot), Is.False,
                    "All destination bytes must come from the immutable capsule, not a reopened source path.");
                Assert.That(firstWriteCalls, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(Path.Combine(operationRoot, "skin.ini")), Is.EqualTo(skin_ini_bytes));
                Assert.That(File.ReadAllBytes(Path.Combine(operationRoot, "nested", "note.png")), Is.EqualTo(note_bytes));
                Assert.That(Directory.Exists(Path.Combine(operationRoot, "empty")), Is.True);
                Assert.That(Directory.GetFileSystemEntries(Path.Combine(operationRoot, "empty")), Is.Empty);
            });
        }

        [Test]
        public void TestExistingDestinationNodeIsNeverOverwritten()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using WindowsSkinManagedAuthoritySession session = openAndCreateProvisionalRoot();
            byte[] sentinel = { 9, 8, 7, 6 };
            File.WriteAllBytes(Path.Combine(operationRoot, "skin.ini"), sentinel);

            Assert.That(
                () => session.WriteManagedCopyProvisional(
                    operationId,
                    package.Capsule,
                    package.Manifest,
                    () => { },
                    CancellationToken.None),
                Throws.TypeOf<WindowsSkinPackageCaptureFileSystemException>());

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(Path.Combine(operationRoot, "skin.ini")), Is.EqualTo(sentinel));
                Assert.That(Directory.Exists(operationRoot), Is.True);
            });
        }

        [Test]
        public void TestCancellationBeforeFirstDestinationWriteCleansExactProvisional()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using var cancellation = new CancellationTokenSource();
            var fileSystem = new CancelAfterFirstFileCreateFileSystem(cancellation, operationRoot, addForeignEntry: false);
            using WindowsSkinManagedAuthoritySession session = openAndCreateProvisionalRoot(fileSystem);
            int firstWriteCalls = 0;

            Assert.That(
                () => session.WriteManagedCopyProvisional(
                    operationId,
                    package.Capsule,
                    package.Manifest,
                    () => firstWriteCalls++,
                    cancellation.Token),
                Throws.TypeOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(firstWriteCalls, Is.Zero);
                Assert.That(cancellation.IsCancellationRequested, Is.True);
                Assert.That(Directory.Exists(operationRoot), Is.False,
                    "A wholly proven pre-write subset must be removed exactly.");
                Assert.That(Directory.Exists(stagingRoot), Is.True);
            });
        }

        [Test]
        public void TestCallerCancellationAfterFirstDestinationWriteIsIgnored()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using var cancellation = new CancellationTokenSource();
            using WindowsSkinManagedAuthoritySession session = openAndCreateProvisionalRoot();
            int firstWriteCalls = 0;

            Assert.DoesNotThrow(() => session.WriteManagedCopyProvisional(
                operationId,
                package.Capsule,
                package.Manifest,
                () =>
                {
                    firstWriteCalls++;
                    cancellation.Cancel();
                },
                cancellation.Token));
            session.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(firstWriteCalls, Is.EqualTo(1));
                Assert.That(cancellation.IsCancellationRequested, Is.True);
                Assert.That(File.ReadAllBytes(Path.Combine(operationRoot, "skin.ini")), Is.EqualTo(skin_ini_bytes));
                Assert.That(File.ReadAllBytes(Path.Combine(operationRoot, "nested", "note.png")), Is.EqualTo(note_bytes));
                Assert.That(Directory.Exists(Path.Combine(operationRoot, "empty")), Is.True);
            });
        }

        [Test]
        public void TestHeldExternalCaptureValidationUsesTheExplicitTokenAfterCapture()
        {
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllBytes(Path.Combine(sourceRoot, "skin.ini"), skin_ini_bytes);
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo
                {
                    FilesystemStoragePath = sourceRoot,
                    IsExternalFilesystemStorage = true,
                },
                new NativeStorage(dataRoot));
            Assert.That(resolution.ExternalCaptureRequest, Is.Not.Null);

            using var captureCancellation = new CancellationTokenSource();
            SkinExternalPackageCaptureResult result = new SkinExternalFolderCaptureService().CaptureHeld(
                resolution.ExternalCaptureRequest,
                cancellationToken: captureCancellation.Token);
            Assert.That(result.IsSuccess, Is.True);
            using ISkinExternalPackageCaptureSession session = result.Session!;

            captureCancellation.Cancel();
            Assert.DoesNotThrow(() => session.Validate(CancellationToken.None),
                "Cancelling the original capture caller must not poison a held durable proof.");

            using var validationCancellation = new CancellationTokenSource();
            validationCancellation.Cancel();
            Assert.That(
                () => session.Validate(validationCancellation.Token),
                Throws.TypeOf<OperationCanceledException>(),
                "A newly supplied cancelled token must still cancel validation.");
        }

        [Test]
        public void TestLiveWriterCanMoveRecapturedCompleteTreeUnderSameHeldSession()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedFolderTargetNameSlot target = session.CaptureAbsentMutationTargetNameSlot(
                "chartskin/live-copy",
                CancellationToken.None);
            session.PrepareManagedCopyStaging(operationId, CancellationToken.None);
            SkinManagedFolderPhysicalIdentity provisionalIdentity = session.CreateManagedCopyProvisionalRoot(
                operationId,
                CancellationToken.None);
            session.WriteManagedCopyProvisional(
                operationId,
                package.Capsule,
                package.Manifest,
                () => { },
                CancellationToken.None);

            using SkinManagedFolderStagedSourceCapture captured = session.CaptureStagedMutationSource(
                operationId,
                CancellationToken.None);
            using SkinManagedFolderStagedImportFilesystemResult moved = session.MoveCapturedStagedMutationSourceToTarget(
                target,
                captured.Capsule.ContentRevision,
                captured.TreeFingerprint,
                CancellationToken.None);

            for (int i = 0; i < 5; i++)
                Assert.DoesNotThrow(() => session.ValidateCompleteAndStable(CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(moved.TargetIdentity, Is.EqualTo(provisionalIdentity));
                Assert.That(moved.Capsule.ContentRevision, Is.EqualTo(package.Capsule.ContentRevision));
                Assert.That(Directory.Exists(operationRoot), Is.False);
                Assert.That(Directory.Exists(Path.Combine(managedRoot, "live-copy")), Is.True);
            });
        }

        [Test]
        public void TestForeignAdditionPreventsPreWriteCleanupAndPreservesIntentRoot()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using var cancellation = new CancellationTokenSource();
            var fileSystem = new CancelAfterFirstFileCreateFileSystem(cancellation, operationRoot, addForeignEntry: true);
            using WindowsSkinManagedAuthoritySession session = openAndCreateProvisionalRoot(fileSystem);
            int firstWriteCalls = 0;

            Assert.That(
                () => session.WriteManagedCopyProvisional(
                    operationId,
                    package.Capsule,
                    package.Manifest,
                    () => firstWriteCalls++,
                    cancellation.Token),
                Throws.TypeOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(firstWriteCalls, Is.Zero);
                Assert.That(Directory.Exists(operationRoot), Is.True,
                    "Unproven foreign content must make cleanup fail closed.");
                Assert.That(File.Exists(Path.Combine(operationRoot, "foreign.keep")), Is.True);
            });
        }

        [Test]
        public void TestRecoveryPreparedRequiresOperationRootToBeAbsent()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            SkinManagedFolderPhysicalIdentity stagedRootIdentity;

            using (WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(operationId, CancellationToken.None);
            }

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection inspection = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                null,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.That(inspection.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.Absent));
            Assert.DoesNotThrow(() => recovery.ValidateCompleteAndStable(CancellationToken.None));
        }

        [Test]
        public void TestRecoveryPreparedWithUnboundOperationRootIsAmbiguous()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            SkinManagedFolderPhysicalIdentity stagedRootIdentity;

            using (WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(operationId, CancellationToken.None);
                writer.CreateManagedCopyProvisionalRoot(operationId, CancellationToken.None);
            }

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection inspection = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                null,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.That(inspection.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.IdentityMismatch));
            Assert.That(Directory.Exists(operationRoot), Is.True);
        }

        [Test]
        public void TestRecoveryCopyingEmptyRootCanBeCleanedByDurableRootIdentity()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            (SkinManagedFolderPhysicalIdentity stagedRootIdentity, SkinManagedFolderPhysicalIdentity provisionalIdentity) =
                createRecoveryProvisional();

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection inspection = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.That(inspection.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.Empty));

            recovery.CleanupExactManagedCopyProvisional(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.That(Directory.Exists(operationRoot), Is.False);
            Assert.DoesNotThrow(() => recovery.ValidateCompleteAndStable(CancellationToken.None));
        }

        [Test]
        public void TestRecoveryNonEmptyPartialTreeIsNeverCleanedWithoutDurableChildIdentity()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            (SkinManagedFolderPhysicalIdentity stagedRootIdentity, SkinManagedFolderPhysicalIdentity provisionalIdentity) =
                createRecoveryProvisional();
            File.WriteAllBytes(Path.Combine(operationRoot, "skin.ini"), skin_ini_bytes[..1]);

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection inspection = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.That(inspection.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.Partial));
            Assert.That(
                () => recovery.CleanupExactManagedCopyProvisional(
                    operationId,
                    "chartskin/recovered-copy",
                    stagedRootIdentity,
                    provisionalIdentity,
                    package.Manifest,
                    package.Capsule.ContentRevision,
                    CancellationToken.None),
                Throws.TypeOf<WindowsSkinPackageCaptureFileSystemException>());
            Assert.That(File.Exists(Path.Combine(operationRoot, "skin.ini")), Is.True);
        }

        [Test]
        public void TestRecoveryCompleteTreeIsRecapturedAgainstCapsuleAndManifest()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            SkinManagedFolderPhysicalIdentity stagedRootIdentity;
            SkinManagedFolderPhysicalIdentity provisionalIdentity;

            using (WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(operationId, CancellationToken.None);
                provisionalIdentity = writer.CreateManagedCopyProvisionalRoot(operationId, CancellationToken.None);
                writer.WriteManagedCopyProvisional(
                    operationId,
                    package.Capsule,
                    package.Manifest,
                    () => { },
                    CancellationToken.None);
            }

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection inspection = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(inspection.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.Complete));
                Assert.That(inspection.ProvisionalIdentity, Is.EqualTo(provisionalIdentity));
                Assert.That(inspection.PackageMetadata, Is.Not.Null);
                Assert.That(inspection.PackageMetadata!.ContentRevision, Is.EqualTo(package.Capsule.ContentRevision));
                Assert.That(inspection.TreeFingerprint, Has.Length.EqualTo(64));
            });
        }

        [Test]
        public void TestRecoveryCompleteTreeMovesUnderTheSameHeldSession()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            SkinManagedFolderPhysicalIdentity stagedRootIdentity;
            SkinManagedFolderPhysicalIdentity provisionalIdentity;

            using (WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                stagedRootIdentity = writer.PrepareManagedCopyStaging(operationId, CancellationToken.None);
                provisionalIdentity = writer.CreateManagedCopyProvisionalRoot(operationId, CancellationToken.None);
                writer.WriteManagedCopyProvisional(
                    operationId,
                    package.Capsule,
                    package.Manifest,
                    () => { },
                    CancellationToken.None);
            }

            using WindowsSkinManagedAuthoritySession recovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection held = recovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);
            SkinManagedFolderTargetNameSlot target = recovery.CaptureAbsentMutationTargetNameSlot(
                "chartskin/recovered-copy",
                CancellationToken.None);

            using SkinManagedFolderStagedImportFilesystemResult moved =
                recovery.MoveCapturedStagedMutationSourceToTarget(
                    target,
                    package.Capsule.ContentRevision,
                    held.TreeFingerprint!,
                    CancellationToken.None);

            SkinManagedFolderStagedImportInspection after = recovery.InspectStagedMutationImportState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                CancellationToken.None);
            SkinManagedFolderStagedImportInspection repeated = recovery.InspectStagedMutationImportState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(after.Status, Is.EqualTo(SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                Assert.That(after.TargetIdentity, Is.EqualTo(provisionalIdentity));
                Assert.That(repeated.Status, Is.EqualTo(SkinManagedFolderStagedImportInspectionStatus.TargetOnly));
                Assert.That(Directory.Exists(operationRoot), Is.False);
                Assert.That(Directory.Exists(Path.Combine(managedRoot, "recovered-copy")), Is.True);
            });
            Assert.DoesNotThrow(() => recovery.ValidateCompleteAndStable(CancellationToken.None));
        }

        [Test]
        public void TestRecoveryForeignOrReplacedProvisionalFailsClosed()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            (SkinManagedFolderPhysicalIdentity stagedRootIdentity, SkinManagedFolderPhysicalIdentity provisionalIdentity) =
                createRecoveryProvisional();
            File.WriteAllText(Path.Combine(operationRoot, "foreign.keep"), "foreign");

            using (WindowsSkinManagedAuthoritySession foreignRecovery = WindowsSkinManagedAuthoritySession.Open(
                       dataRoot,
                       new NativeWindowsSkinPackageCaptureFileSystem(),
                       CancellationToken.None))
            {
                SkinManagedCopyProvisionalInspection foreign = foreignRecovery.InspectManagedCopyProvisionalState(
                    operationId,
                    "chartskin/recovered-copy",
                    stagedRootIdentity,
                    provisionalIdentity,
                    package.Manifest,
                    package.Capsule.ContentRevision,
                    CancellationToken.None);
                Assert.That(foreign.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.ManifestMismatch));
            }

            Directory.Delete(operationRoot, true);
            Directory.CreateDirectory(operationRoot);

            using WindowsSkinManagedAuthoritySession replacedRecovery = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedCopyProvisionalInspection replaced = replacedRecovery.InspectManagedCopyProvisionalState(
                operationId,
                "chartskin/recovered-copy",
                stagedRootIdentity,
                provisionalIdentity,
                package.Manifest,
                package.Capsule.ContentRevision,
                CancellationToken.None);
            Assert.That(replaced.Status, Is.EqualTo(SkinManagedCopyProvisionalInspectionStatus.IdentityMismatch));
        }

        [Test]
        public void TestSameNameSameKindReplacementPreventsPreWriteCleanup()
        {
            using CapturedPackage package = captureSourceThenDeleteIt();
            using var cancellation = new CancellationTokenSource();
            string replacedPath = Path.Combine(operationRoot, "nested", "note.png");
            string displacedPath = Path.Combine(Path.GetTempPath(), $"oms-managed-copy-displaced-{Guid.NewGuid():N}.tmp");
            byte[] foreignBytes = { 4, 2, 4, 2 };
            var fileSystem = new CancelAfterFirstFileCreateFileSystem(
                cancellation,
                operationRoot,
                addForeignEntry: false,
                beforeCancel: () =>
                {
                    File.Move(replacedPath, displacedPath);
                    File.WriteAllBytes(replacedPath, foreignBytes);
                });
            using WindowsSkinManagedAuthoritySession session = openAndCreateProvisionalRoot(fileSystem);
            int firstWriteCalls = 0;

            try
            {
                Assert.That(
                    () => session.WriteManagedCopyProvisional(
                        operationId,
                        package.Capsule,
                        package.Manifest,
                        () => firstWriteCalls++,
                        cancellation.Token),
                    Throws.TypeOf<OperationCanceledException>());
                session.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(firstWriteCalls, Is.Zero);
                    Assert.That(Directory.Exists(operationRoot), Is.True,
                        "A same-name replacement is not an OMS-created node and must prevent cleanup.");
                    Assert.That(File.ReadAllBytes(replacedPath), Is.EqualTo(foreignBytes));
                    Assert.That(File.Exists(displacedPath), Is.True);
                });
            }
            finally
            {
                if (File.Exists(displacedPath))
                    File.Delete(displacedPath);
            }
        }

        private WindowsSkinManagedAuthoritySession openAndCreateProvisionalRoot(
            IWindowsSkinPackageCaptureFileSystem? fileSystem = null)
        {
            WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                fileSystem ?? new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);

            try
            {
                session.PrepareManagedCopyStaging(operationId, CancellationToken.None);
                session.CreateManagedCopyProvisionalRoot(operationId, CancellationToken.None);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        private (SkinManagedFolderPhysicalIdentity StagedRoot, SkinManagedFolderPhysicalIdentity Provisional)
            createRecoveryProvisional()
        {
            using WindowsSkinManagedAuthoritySession writer = WindowsSkinManagedAuthoritySession.Open(
                dataRoot,
                new NativeWindowsSkinPackageCaptureFileSystem(),
                CancellationToken.None);
            SkinManagedFolderPhysicalIdentity stagedRoot = writer.PrepareManagedCopyStaging(
                operationId,
                CancellationToken.None);
            SkinManagedFolderPhysicalIdentity provisional = writer.CreateManagedCopyProvisionalRoot(
                operationId,
                CancellationToken.None);
            return (stagedRoot, provisional);
        }

        private CapturedPackage captureSourceThenDeleteIt()
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "empty"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "nested"));
            File.WriteAllBytes(Path.Combine(sourceRoot, "skin.ini"), skin_ini_bytes);
            File.WriteAllBytes(Path.Combine(sourceRoot, "nested", "note.png"), note_bytes);

            SkinPackageCapturedEntry?[] entries =
            {
                SkinPackageCapturedEntry.CreateDirectory("empty"),
                SkinPackageCapturedEntry.CreateDirectory("nested"),
                createFile("skin.ini"),
                createFile("nested/note.png"),
            };
            SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(entries);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Capsule, Is.Not.Null);
            SkinPackageRevisionCapsule capsule = result.Capsule!;

            try
            {
                Assert.That(
                    SkinExternalPackageLogicalManifest.TryCreate(
                        entries,
                        capsule,
                        SkinExternalPackageCaptureLimits.DEFAULT_MAX_LOGICAL_MANIFEST_BYTES,
                        out SkinExternalPackageLogicalManifest? externalManifest),
                    Is.True);
                Assert.That(externalManifest, Is.Not.Null);
                SkinManagedCopyLogicalManifest manifest = SkinManagedCopyLogicalManifest.Create(externalManifest!);
                Directory.Delete(sourceRoot, true);
                return new CapturedPackage(capsule, manifest);
            }
            catch
            {
                capsule.Dispose();
                throw;
            }

            SkinPackageCapturedEntry createFile(string relativePath)
            {
                string path = Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return SkinPackageCapturedEntry.CreateFile(
                    relativePath,
                    new FileInfo(path).Length,
                    () => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
        }

        private SkinManagedFolderMutationJournal createPreparedJournal(
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            SkinManagedFolderPhysicalIdentity stagedRootIdentity,
            CapturedPackage package)
            => SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                operationId,
                Guid.NewGuid(),
                managedRootIdentity,
                "chartskin/managed-copy",
                stagedRootIdentity,
                package.Capsule.ContentRevision,
                record_fingerprint,
                capture_fingerprint,
                package.Manifest,
                new SkinExternalRegistryJournalBinding(
                    1,
                    capture_fingerprint,
                    SkinExternalCollisionDisposition.ExactRegisteredExternalSet));

        private static void assertLoadedExact(
            SkinManagedFolderMutationJournalStore store,
            SkinManagedFolderMutationJournal expected)
        {
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
            Assert.That(loaded.Journal, Is.Not.Null);
            Assert.That(loaded.Journal!.IsExactSameJournal(expected), Is.True);
        }

        private sealed class CapturedPackage : IDisposable
        {
            public SkinPackageRevisionCapsule Capsule { get; }

            public SkinManagedCopyLogicalManifest Manifest { get; }

            public CapturedPackage(
                SkinPackageRevisionCapsule capsule,
                SkinManagedCopyLogicalManifest manifest)
            {
                Capsule = capsule;
                Manifest = manifest;
            }

            public void Dispose() => Capsule.Dispose();
        }

        private sealed class CancelAfterFirstFileCreateFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly NativeWindowsSkinPackageCaptureFileSystem inner = new NativeWindowsSkinPackageCaptureFileSystem();
            private readonly CancellationTokenSource cancellation;
            private readonly string operationRoot;
            private readonly bool addForeignEntry;
            private readonly Action? beforeCancel;
            private bool cancelled;

            public CancelAfterFirstFileCreateFileSystem(
                CancellationTokenSource cancellation,
                string operationRoot,
                bool addForeignEntry,
                Action? beforeCancel = null)
            {
                this.cancellation = cancellation;
                this.operationRoot = operationRoot;
                this.addForeignEntry = addForeignEntry;
                this.beforeCancel = beforeCancel;
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
                => inner.CreateNonOwningReadStream(file);

            public IWindowsSkinPackageCaptureHandle CreateChildNoFollowNoReplace(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                bool directory)
            {
                IWindowsSkinPackageCaptureHandle created = inner.CreateChildNoFollowNoReplace(parent, name, directory);

                if (!directory && !cancelled)
                {
                    cancelled = true;
                    beforeCancel?.Invoke();

                    if (addForeignEntry)
                        File.WriteAllText(Path.Combine(operationRoot, "foreign.keep"), "foreign");

                    cancellation.Cancel();
                }

                return created;
            }

            public Stream CreateNonOwningWriteStream(IWindowsSkinPackageCaptureHandle file)
                => inner.CreateNonOwningWriteStream(file);
        }
    }
}
