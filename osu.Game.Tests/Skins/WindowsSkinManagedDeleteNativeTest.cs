// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class WindowsSkinManagedDeleteNativeTest : RealmTest
    {
        private const string source_relative_path = "chartskin/delete-source";

        [Test]
        public void TestNativeDeleteCaptureDetachAndCleanupRetainsExactTree()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                string phase = "open";

                try
                {
                    using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                        fixture.DataRoot,
                        new NativeWindowsSkinPackageCaptureFileSystem(),
                        CancellationToken.None);
                    phase = "capture-source";
                    SkinManagedFolderPhysicalIdentity sourceIdentity = session.CaptureExistingMutationSource(source_relative_path);
                    phase = "capture-manifest";
                    string manifest = session.GetCapturedMutationDeleteSourceNodeManifest();
                    phase = "capture-target-slot";
                    SkinManagedFolderTargetNameSlot target = session.CaptureAbsentMutationTargetNameSlot(
                        SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(Guid.NewGuid()));
                    phase = "detach";
                    Assert.That(session.RenameCapturedMutationSourceToTarget(target), Is.EqualTo(sourceIdentity));
                    phase = "cleanup";
                    session.CleanupExactMutationDeleteTombstone(
                        source_relative_path,
                        target.ManagedRelativePath,
                        sourceIdentity,
                        manifest);

                    Assert.Multiple(() =>
                    {
                        Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                        Assert.That(Directory.Exists(getFullPath(fixture.DataRoot, target.ManagedRelativePath)), Is.False);
                        Assert.That(File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")), Is.EqualTo("keep-sibling"));
                    });
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception)
                {
                    Logger.Log($"Native delete phase={phase}; rejection={exception.RejectionReason}", LoggingTarget.Runtime, LogLevel.Important);
                    throw;
                }
            });
        }

        [Test]
        public void TestDeleteAfterPeerDirectoryIndexRefreshKeepsCanonicalPeerUnchanged()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                Directory.SetLastWriteTimeUtc(fixture.SiblingRoot, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                using var probe = new DirectoryIndexRefreshFileSystem(new NativeWindowsSkinPackageCaptureFileSystem(), fixture.SiblingRoot, "sibling");
                using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(fixture.DataRoot, probe);
                probe.ReleasePreparedDeleteHandleAndVerifyIndexRefresh();

                try
                {
                    SkinManagedFolderPhysicalIdentity sourceIdentity = session.CaptureExistingMutationSource(source_relative_path);
                    string manifest = session.GetCapturedMutationDeleteSourceNodeManifest();
                    SkinManagedFolderTargetNameSlot target = session.CaptureAbsentMutationTargetNameSlot(
                        SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(Guid.NewGuid()));
                    session.RenameCapturedMutationSourceToTarget(target);
                    session.CleanupExactMutationDeleteTombstone(source_relative_path, target.ManagedRelativePath, sourceIdentity, manifest);

                    Assert.Multiple(() =>
                    {
                        Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                        Assert.That(Directory.Exists(getFullPath(fixture.DataRoot, target.ManagedRelativePath)), Is.False);
                        Assert.That(File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")), Is.EqualTo("keep-sibling"));
                        Assert.That(File.ReadAllText(Path.Combine(fixture.SiblingRoot, "cache-refresh.txt")), Is.EqualTo("peer-cache-bytes"));
                    });
                }
                finally
                {
                    probe.AssertCanonicalUnchanged();
                }
            });
        }

        [Test]
        public void TestCapturedSourceManifestAcceptsNestedDirectoryIndexRefreshWithUnchangedCanonicalTree()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                string nested = Path.Combine(fixture.SourceRoot, "nested");
                Directory.SetLastWriteTimeUtc(nested, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                using var probe = new DirectoryIndexRefreshFileSystem(new NativeWindowsSkinPackageCaptureFileSystem(), nested, "delete-source/nested");
                using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(fixture.DataRoot, probe);
                session.CaptureExistingMutationSource(source_relative_path);
                probe.ReleasePreparedDeleteHandleAndVerifyIndexRefresh();

                try
                {
                    Assert.That(SkinManagedFolderDeleteManifest.IsValid(session.GetCapturedMutationDeleteSourceNodeManifest()), Is.True);
                    Assert.That(File.ReadAllText(Path.Combine(nested, "cache-refresh.txt")), Is.EqualTo("peer-cache-bytes"));
                    Assert.That(File.ReadAllText(Path.Combine(nested, "deeper", "note.png")), Is.EqualTo("nested-native-bytes"));
                    Assert.That(File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")), Is.EqualTo("keep-sibling"));
                }
                finally
                {
                    probe.AssertCanonicalUnchanged();
                }
            });
        }

        [TestCase("directory-creation")]
        [TestCase("directory-attributes")]
        [TestCase("file-write-time")]
        [TestCase("file-change-time")]
        public void TestDeleteSourceCaptureRejectsPeerMetadataChangesOutsideCachedDirectoryModificationTimes(string change)
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                string peerFile = Path.Combine(fixture.ManagedRoot, "peer.txt");
                File.WriteAllText(peerFile, "peer-file-bytes");
                NativeMethods.FILE_BASIC_INFO peerFileBefore = readBasicInfo(peerFile);
                using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                    fixture.DataRoot,
                    new NativeWindowsSkinPackageCaptureFileSystem());

                switch (change)
                {
                    case "directory-creation":
                        Directory.SetCreationTimeUtc(fixture.SiblingRoot, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                        break;

                    case "directory-attributes":
                        File.SetAttributes(fixture.SiblingRoot, File.GetAttributes(fixture.SiblingRoot) | FileAttributes.Hidden);
                        break;

                    case "file-write-time":
                        File.SetLastWriteTimeUtc(peerFile, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                        break;

                    case "file-change-time":
                        File.SetAttributes(peerFile, (FileAttributes)peerFileBefore.FileAttributes ^ FileAttributes.Hidden);
                        File.SetAttributes(peerFile, (FileAttributes)peerFileBefore.FileAttributes);
                        NativeMethods.FILE_BASIC_INFO peerFileAfter = readBasicInfo(peerFile);
                        Assert.Multiple(() =>
                        {
                            Assert.That(peerFileAfter.CreationTime == peerFileBefore.CreationTime, Is.True);
                            Assert.That(peerFileAfter.LastWriteTime == peerFileBefore.LastWriteTime, Is.True);
                            Assert.That(peerFileAfter.FileAttributes == peerFileBefore.FileAttributes, Is.True);
                            Assert.That(peerFileAfter.ChangeTime != peerFileBefore.ChangeTime, Is.True);
                        });
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(change));
                }

                WindowsSkinPackageCaptureFileSystemException? rejected = Assert.Throws<WindowsSkinPackageCaptureFileSystemException>(
                    () => session.CaptureExistingMutationSource(source_relative_path));

                Assert.Multiple(() =>
                {
                    Assert.That(rejected!.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.InventoryChanged));
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.True);
                    Assert.That(File.ReadAllText(Path.Combine(fixture.SourceRoot, "nested", "deeper", "note.png")), Is.EqualTo("nested-native-bytes"));
                    Assert.That(File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")), Is.EqualTo("keep-sibling"));
                    Assert.That(File.ReadAllText(peerFile), Is.EqualTo("peer-file-bytes"));
                    Assert.That(Directory.GetFileSystemEntries(fixture.ManagedRoot).Select(Path.GetFileName), Is.EquivalentTo(new[] { "delete-source", "sibling", "peer.txt" }));
                });
            });
        }

        [Test]
        public void TestDetachedNestedTreeCleanupPreservesSiblingAndManagedRoot()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                DetachedDelete detached = detachToTombstone(realm, storage, fixture);
                string tombstoneRoot = getFullPath(fixture.DataRoot, detached.TombstoneRelativePath);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(Directory.Exists(Path.Combine(tombstoneRoot, "empty")), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(tombstoneRoot, "nested", "deeper", "note.png")),
                        Is.EqualTo("nested-native-bytes"));
                });

                using ISkinManagedFolderMutationNativeSession cleanup =
                    new WindowsSkinManagedFolderMutationNativeAuthority(storage).Open(CancellationToken.None);
                cleanup.CleanupExactDeleteTombstone(
                    source_relative_path,
                    detached.TombstoneRelativePath,
                    detached.SourceIdentity,
                    detached.SourceNodeManifest,
                    CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.False);
                    Assert.That(Directory.Exists(fixture.ManagedRoot), Is.True);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));
                    Assert.That(
                        Directory.GetFileSystemEntries(fixture.ManagedRoot)
                                 .Select(Path.GetFileName),
                        Is.EquivalentTo(new[] { "sibling" }));
                });
            });
        }

        [Test]
        public void TestSameSessionDetachAndCleanupUsesRecapturedDeleteHandles()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                Guid operationId = Guid.NewGuid();
                var authority = createAuthority(realm, storage);
                SkinManagedFolderMutationAuthorityResult opened = authority.OpenDelete(
                    operationId,
                    fixture.RecordId,
                    CancellationToken.None);

                Assert.That(opened.IsSuccess, Is.True, opened.RejectionReason.ToString());

                using SkinManagedFolderMutationAuthoritySession session = opened.Session!;
                string tombstoneRoot = getFullPath(
                    fixture.DataRoot,
                    session.TargetNameSlot!.ManagedRelativePath);
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                receipt = session.PersistDeleteFallbackDisposition(
                    receipt,
                    SkinManagedFolderProtectedFallbackCommitResult.NotRequired,
                    CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        session.ApplyCapturedDeleteWithDurableReceipt(
                            receipt,
                            () => true,
                            CancellationToken.None),
                        Is.True);
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                });

                Assert.That(session.TryDeleteCapturedTombstone(CancellationToken.None), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.False);
                    Assert.That(Directory.Exists(fixture.ManagedRoot), Is.True);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));
                });
            });
        }

        [Test]
        public void TestDeleteExclusiveRecaptureBlocksNativeRootAndChildRename()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                Guid operationId = Guid.NewGuid();
                string tombstoneRelativePath =
                    SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(operationId);
                string tombstoneRoot = getFullPath(
                    fixture.DataRoot,
                    tombstoneRelativePath);
                string nestedRoot = Path.Combine(tombstoneRoot, "nested");
                string escapedRoot = Path.Combine(fixture.DataRoot, "escaped-delete-root");
                string escapedChild = Path.Combine(fixture.SiblingRoot, "escaped-delete-child");
                var probe = new DeleteRenameProbeFileSystem(
                    new NativeWindowsSkinPackageCaptureFileSystem(),
                    tombstoneRoot,
                    escapedRoot,
                    nestedRoot,
                    escapedChild);
                var authority = createAuthority(
                    realm,
                    storage,
                    new WindowsSkinManagedFolderMutationNativeAuthority(
                        fixture.DataRoot,
                        probe));
                SkinManagedFolderMutationAuthorityResult opened = authority.OpenDelete(
                    operationId,
                    fixture.RecordId,
                    CancellationToken.None);

                Assert.That(opened.IsSuccess, Is.True, opened.RejectionReason.ToString());

                using (SkinManagedFolderMutationAuthoritySession session = opened.Session!)
                {
                    SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                    receipt = session.PersistDeleteFallbackDisposition(
                        receipt,
                        SkinManagedFolderProtectedFallbackCommitResult.NotRequired,
                        CancellationToken.None);
                    Assert.That(
                        session.ApplyCapturedDeleteWithDurableReceipt(
                            receipt,
                            () => true,
                            CancellationToken.None),
                        Is.True);
                    Assert.That(session.TryDeleteCapturedTombstone(CancellationToken.None), Is.True);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(probe.Attempted, Is.True);
                    Assert.That(isSharingViolation(probe.RootRenameException), Is.True,
                        probe.RootRenameException?.ToString());
                    Assert.That(isSharingViolation(probe.ChildRenameException), Is.True,
                        probe.ChildRenameException?.ToString());
                    Assert.That(Directory.Exists(tombstoneRoot), Is.False);
                    Assert.That(Directory.Exists(escapedRoot), Is.False);
                    Assert.That(Directory.Exists(escapedChild), Is.False);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));
                });
            });
        }

        [Test]
        public void TestForeignAdditionDuringDispositionIsPreservedAndKeepsDurableRecoveryIntent()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                Guid operationId = Guid.NewGuid();
                string tombstoneRelativePath =
                    SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(operationId);
                string tombstoneRoot = getFullPath(
                    fixture.DataRoot,
                    tombstoneRelativePath);
                string foreignFile = Path.Combine(tombstoneRoot, "foreign-race.txt");
                var probe = new DeleteForeignAdditionProbeFileSystem(
                    new NativeWindowsSkinPackageCaptureFileSystem(),
                    foreignFile);
                var authority = createAuthority(
                    realm,
                    storage,
                    new WindowsSkinManagedFolderMutationNativeAuthority(
                        fixture.DataRoot,
                        probe));
                var operation = new SkinManagedFolderDeleteOperation(
                    realm,
                    authority,
                    (_, _, _) => SkinManagedFolderProtectedFallbackCommitResult.NotRequired);

                SkinManagedFolderDeleteOperationResult result = operation.Execute(
                    operationId,
                    fixture.RecordId,
                    CancellationToken.None);
                SkinManagedFolderMutationJournalLoadResult retained =
                    new SkinManagedFolderMutationJournalStore(storage).Load();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Status, Is.EqualTo(
                        SkinManagedFolderDeleteOperationStatus.PhysicalDeleteOutcomeUncertain));
                    Assert.That(probe.AdditionException, Is.Null);
                    Assert.That(probe.DeleteCalls, Is.GreaterThan(0));
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(File.ReadAllText(foreignFile), Is.EqualTo("foreign-race"));
                    Assert.That(File.Exists(Path.Combine(tombstoneRoot, "skin.ini")), Is.False);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));
                    Assert.That(realm.Realm.Find<SkinInfo>(fixture.RecordId), Is.Not.Null);
                    Assert.That(retained.IsLoaded, Is.True);
                    Assert.That(retained.Journal!.Phase, Is.EqualTo(
                        SkinManagedFolderMutationPhase.FilesystemApplied));
                });
            });
        }

        [Test]
        public void TestRestartResumesCleanupOfExactPartiallyDeletedTombstone()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                DetachedDelete detached = detachToTombstone(realm, storage, fixture);
                string tombstoneRoot = getFullPath(fixture.DataRoot, detached.TombstoneRelativePath);

                File.Delete(Path.Combine(tombstoneRoot, "nested", "deeper", "note.png"));
                Directory.Delete(Path.Combine(tombstoneRoot, "nested", "deeper"));
                Directory.Delete(Path.Combine(tombstoneRoot, "empty"));

                using ISkinManagedFolderMutationNativeSession cleanup =
                    new WindowsSkinManagedFolderMutationNativeAuthority(storage).Open(CancellationToken.None);
                cleanup.CleanupExactDeleteTombstone(
                    source_relative_path,
                    detached.TombstoneRelativePath,
                    detached.SourceIdentity,
                    detached.SourceNodeManifest,
                    CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(tombstoneRoot), Is.False);
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(fixture.ManagedRoot), Is.True);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));
                });
            });
        }

        [TestCase("identity-mismatch")]
        [TestCase("source-collision")]
        public void TestRestartCleanupFailsClosedForTombstoneMismatchOrCollision(string state)
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                DetachedDelete detached = detachToTombstone(realm, storage, fixture);
                string tombstoneRoot = getFullPath(fixture.DataRoot, detached.TombstoneRelativePath);
                string? originalTombstone = null;

                switch (state)
                {
                    case "identity-mismatch":
                        originalTombstone = Path.Combine(fixture.ManagedRoot, "original-tombstone");
                        Directory.Move(tombstoneRoot, originalTombstone);
                        Directory.CreateDirectory(tombstoneRoot);
                        File.WriteAllText(Path.Combine(tombstoneRoot, "foreign.txt"), "foreign-tombstone");
                        break;

                    case "source-collision":
                        Directory.CreateDirectory(fixture.SourceRoot);
                        File.WriteAllText(Path.Combine(fixture.SourceRoot, "foreign.txt"), "foreign-source");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }

                using ISkinManagedFolderMutationNativeSession cleanup =
                    new WindowsSkinManagedFolderMutationNativeAuthority(storage).Open(CancellationToken.None);

                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => cleanup.CleanupExactDeleteTombstone(
                        source_relative_path,
                        detached.TombstoneRelativePath,
                        detached.SourceIdentity,
                        detached.SourceNodeManifest,
                        CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(Directory.Exists(fixture.ManagedRoot), Is.True);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(Path.Combine(fixture.SiblingRoot, "keep.txt")),
                        Is.EqualTo("keep-sibling"));

                    if (state == "identity-mismatch")
                    {
                        Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                        Assert.That(Directory.Exists(originalTombstone), Is.True);
                        Assert.That(
                            File.ReadAllText(Path.Combine(tombstoneRoot, "foreign.txt")),
                            Is.EqualTo("foreign-tombstone"));
                        Assert.That(
                            File.ReadAllText(Path.Combine(originalTombstone!, "nested", "deeper", "note.png")),
                            Is.EqualTo("nested-native-bytes"));
                    }
                    else
                    {
                        Assert.That(Directory.Exists(fixture.SourceRoot), Is.True);
                        Assert.That(
                            File.ReadAllText(Path.Combine(fixture.SourceRoot, "foreign.txt")),
                            Is.EqualTo("foreign-source"));
                        Assert.That(
                            File.ReadAllText(Path.Combine(tombstoneRoot, "nested", "deeper", "note.png")),
                            Is.EqualTo("nested-native-bytes"));
                    }
                });
            });
        }

        [Test]
        public void TestLiveCleanupRejectsAddedOrdinaryFileWithoutDeletingForeignOrCapturedNodes()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                Guid operationId = Guid.NewGuid();
                var authority = createAuthority(realm, storage);
                SkinManagedFolderMutationAuthorityResult opened = authority.OpenDelete(
                    operationId,
                    fixture.RecordId,
                    CancellationToken.None);

                Assert.That(opened.IsSuccess, Is.True, opened.RejectionReason.ToString());

                using SkinManagedFolderMutationAuthoritySession session = opened.Session!;
                string tombstoneRelativePath = session.TargetNameSlot!.ManagedRelativePath;
                string tombstoneRoot = getFullPath(fixture.DataRoot, tombstoneRelativePath);
                string foreignFile = Path.Combine(tombstoneRoot, "foreign-live.txt");
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                receipt = session.PersistDeleteFallbackDisposition(
                    receipt,
                    SkinManagedFolderProtectedFallbackCommitResult.NotRequired,
                    CancellationToken.None);
                Assert.That(
                    session.ApplyCapturedDeleteWithDurableReceipt(
                        receipt,
                        () => true,
                        CancellationToken.None),
                    Is.True);

                File.WriteAllText(foreignFile, "foreign-live", new UTF8Encoding(false));

                Assert.That(session.TryDeleteCapturedTombstone(CancellationToken.None), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(File.ReadAllText(foreignFile), Is.EqualTo("foreign-live"));
                    Assert.That(
                        File.ReadAllText(Path.Combine(tombstoneRoot, "skin.ini")),
                        Does.Contain("Native delete display"));
                    Assert.That(
                        File.ReadAllText(Path.Combine(tombstoneRoot, "nested", "deeper", "note.png")),
                        Is.EqualTo("nested-native-bytes"));
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                });
            });
        }

        [Test]
        public void TestRestartCleanupRejectsAddedOrdinaryFileWithoutDeletingForeignOrCapturedNodes()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                DetachedDelete detached = detachToTombstone(realm, storage, fixture);
                string tombstoneRoot = getFullPath(fixture.DataRoot, detached.TombstoneRelativePath);
                string foreignFile = Path.Combine(tombstoneRoot, "foreign-restart.txt");
                File.WriteAllText(foreignFile, "foreign-restart", new UTF8Encoding(false));

                using ISkinManagedFolderMutationNativeSession cleanup =
                    new WindowsSkinManagedFolderMutationNativeAuthority(storage).Open(CancellationToken.None);

                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => cleanup.CleanupExactDeleteTombstone(
                        source_relative_path,
                        detached.TombstoneRelativePath,
                        detached.SourceIdentity,
                        detached.SourceNodeManifest,
                        CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(File.ReadAllText(foreignFile), Is.EqualTo("foreign-restart"));
                    Assert.That(
                        File.ReadAllText(Path.Combine(tombstoneRoot, "skin.ini")),
                        Does.Contain("Native delete display"));
                    Assert.That(
                        File.ReadAllText(Path.Combine(tombstoneRoot, "nested", "deeper", "note.png")),
                        Is.EqualTo("nested-native-bytes"));
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                });
            });
        }

        [Test]
        public void TestRestartCleanupRejectsSameNameReplacementAndPreservesIt()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                DetachedDelete detached = detachToTombstone(realm, storage, fixture);
                string tombstoneRoot = getFullPath(fixture.DataRoot, detached.TombstoneRelativePath);
                string replacedFile = Path.Combine(tombstoneRoot, "nested", "deeper", "note.png");
                const string original_contents = "nested-native-bytes";
                const string replacement_contents = "foreign-node-bytes!";

                Assert.That(
                    Encoding.UTF8.GetByteCount(replacement_contents),
                    Is.EqualTo(Encoding.UTF8.GetByteCount(original_contents)));

                File.Delete(replacedFile);
                File.WriteAllText(
                    replacedFile,
                    replacement_contents,
                    new UTF8Encoding(false));

                using ISkinManagedFolderMutationNativeSession cleanup =
                    new WindowsSkinManagedFolderMutationNativeAuthority(storage).Open(CancellationToken.None);

                Assert.Throws<SkinManagedFolderMutationNativeAuthorityException>(
                    () => cleanup.CleanupExactDeleteTombstone(
                        source_relative_path,
                        detached.TombstoneRelativePath,
                        detached.SourceIdentity,
                        detached.SourceNodeManifest,
                        CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.False);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.True);
                    Assert.That(
                        File.ReadAllText(replacedFile),
                        Is.EqualTo(replacement_contents));
                    Assert.That(File.Exists(Path.Combine(tombstoneRoot, "skin.ini")), Is.True);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                });
            });
        }

        [Test]
        public void TestOpenDeleteRejectsHardLinkWithoutMovingOrDeletingAnything()
        {
            RunTestWithRealm((realm, storage) =>
            {
                DeleteFixture fixture = createFixture(realm, storage);
                string skinIni = Path.Combine(fixture.SourceRoot, "skin.ini");
                string hardLink = Path.Combine(fixture.SourceRoot, "hard-link.ini");

                if (!HardLinkHelper.TryCreateHardLink(hardLink, skinIni))
                    Assert.Ignore("Hard-link creation is unavailable.");

                Guid operationId = Guid.NewGuid();
                var authority = createAuthority(realm, storage);
                SkinManagedFolderMutationAuthorityResult opened = authority.OpenDelete(
                    operationId,
                    fixture.RecordId,
                    CancellationToken.None);
                string tombstoneRoot = getFullPath(
                    fixture.DataRoot,
                    SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(operationId));

                Assert.Multiple(() =>
                {
                    Assert.That(opened.IsSuccess, Is.False);
                    Assert.That(
                        opened.RejectionReason,
                        Is.EqualTo(SkinManagedFolderMutationAuthorityRejectionReason.NativeAuthorityRejected));
                    Assert.That(Directory.Exists(fixture.SourceRoot), Is.True);
                    Assert.That(File.Exists(skinIni), Is.True);
                    Assert.That(File.Exists(hardLink), Is.True);
                    Assert.That(Directory.Exists(tombstoneRoot), Is.False);
                    Assert.That(Directory.Exists(fixture.SiblingRoot), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(storage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        private static DetachedDelete detachToTombstone(
            RealmAccess realm,
            Storage storage,
            DeleteFixture fixture)
        {
            Guid operationId = Guid.NewGuid();
            var authority = createAuthority(realm, storage);
            SkinManagedFolderMutationAuthorityResult opened = authority.OpenDelete(
                operationId,
                fixture.RecordId,
                CancellationToken.None);

            Assert.That(opened.IsSuccess, Is.True, opened.RejectionReason.ToString());

            SkinManagedFolderPhysicalIdentity sourceIdentity;
            string sourceNodeManifest;
            string tombstoneRelativePath;

            using (SkinManagedFolderMutationAuthoritySession session = opened.Session!)
            {
                sourceIdentity = session.ExistingRecord!.PhysicalIdentity;
                sourceNodeManifest = session.ExistingRecord.DeleteSourceNodeManifest!;
                tombstoneRelativePath = session.TargetNameSlot!.ManagedRelativePath;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                receipt = session.PersistDeleteFallbackDisposition(
                    receipt,
                    SkinManagedFolderProtectedFallbackCommitResult.NotRequired,
                    CancellationToken.None);
                Assert.That(
                    session.ApplyCapturedDeleteWithDurableReceipt(
                        receipt,
                        () => true,
                        CancellationToken.None),
                    Is.True);
            }

            return new DetachedDelete(sourceIdentity, sourceNodeManifest, tombstoneRelativePath);
        }

        private static SkinManagedFolderMutationAuthority createAuthority(
            RealmAccess realm,
            Storage storage)
            => createAuthority(
                realm,
                storage,
                new WindowsSkinManagedFolderMutationNativeAuthority(storage));

        private static SkinManagedFolderMutationAuthority createAuthority(
            RealmAccess realm,
            Storage storage,
            ISkinManagedFolderMutationNativeAuthority nativeAuthority)
            => new SkinManagedFolderMutationAuthority(
                realm,
                storage,
                new SkinManagedFolderOperationCoordinator(),
                nativeAuthority,
                new SkinManagedFolderMutationJournalStore(storage));

        private static bool isSharingViolation(Exception? exception)
            => exception is IOException && (exception.HResult & 0xffff) == 32;

        private static NativeMethods.FILE_BASIC_INFO readBasicInfo(string file)
        {
            using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.FILE_BASIC_INFO>());

            try
            {
                Assert.That(NativeMethods.GetFileInformationByHandleEx(handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo,
                    buffer, (uint)Marshal.SizeOf<NativeMethods.FILE_BASIC_INFO>()), Is.True);
                return Marshal.PtrToStructure<NativeMethods.FILE_BASIC_INFO>(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static DeleteFixture createFixture(RealmAccess realm, Storage storage)
        {
            string dataRoot = storage.GetFullPath(string.Empty);
            string managedRoot = Path.Combine(
                dataRoot,
                SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            string sourceRoot = Path.Combine(managedRoot, "delete-source");
            string siblingRoot = Path.Combine(managedRoot, "sibling");

            Directory.CreateDirectory(Path.Combine(sourceRoot, "nested", "deeper"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "empty"));
            Directory.CreateDirectory(siblingRoot);
            File.WriteAllText(
                Path.Combine(sourceRoot, "skin.ini"),
                "[General]\nName: Native delete display\nAuthor: OMS native test\n",
                new UTF8Encoding(false, true));
            File.WriteAllText(
                Path.Combine(sourceRoot, "nested", "deeper", "note.png"),
                "nested-native-bytes",
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(siblingRoot, "keep.txt"),
                "keep-sibling",
                new UTF8Encoding(false));

            Guid recordId = Guid.NewGuid();
            realm.Write(r => r.Add(new SkinInfo(
                "Native delete display",
                "OMS native test",
                SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                ID = recordId,
                Hash = new string('A', 64),
                FilesystemStoragePath = source_relative_path,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
            }));

            return new DeleteFixture(
                recordId,
                dataRoot,
                managedRoot,
                sourceRoot,
                siblingRoot);
        }

        private static string getFullPath(string dataRoot, string relativePath)
            => Path.Combine(
                dataRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

        private sealed record DeleteFixture(
            Guid RecordId,
            string DataRoot,
            string ManagedRoot,
            string SourceRoot,
            string SiblingRoot);

        private sealed record DetachedDelete(
            SkinManagedFolderPhysicalIdentity SourceIdentity,
            string SourceNodeManifest,
            string TombstoneRelativePath);

        private sealed class DirectoryIndexRefreshFileSystem : IWindowsSkinPackageCaptureFileSystem, IDisposable
        {
            private readonly IWindowsSkinPackageCaptureFileSystem inner;
            private readonly string directoryRoot;
            private readonly string[] directoryParts;
            private readonly List<IWindowsSkinPackageCaptureHandle> ancestors = new();
            private IWindowsSkinPackageCaptureHandle? preparedDeleteHandle;
            private IWindowsSkinPackageCaptureHandle? canonicalReadHandle;
            private IWindowsSkinPackageCaptureHandle? directoryParent;
            private WindowsSkinPackagePhysicalIdentity parentIdentity;
            private WindowsSkinPackageEntryMetadata canonicalMetadata;
            private WindowsSkinPackageDirectoryEntry? initialIndexEntry;

            public DirectoryIndexRefreshFileSystem(IWindowsSkinPackageCaptureFileSystem inner, string directoryRoot, string directoryRelativePath)
            {
                this.inner = inner;
                this.directoryRoot = directoryRoot;
                directoryParts = directoryRelativePath.Split('/');
            }

            public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
                => inner.OpenLocalVolumeRoot(driveLetter);

            public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
                IWindowsSkinPackageCaptureHandle directory,
                int maxEntries,
                CancellationToken cancellationToken)
            {
                IReadOnlyList<WindowsSkinPackageDirectoryEntry> current = inner.Enumerate(directory, maxEntries, cancellationToken);

                if (directoryParent != null && initialIndexEntry == null && inner.QueryMetadata(directory).Identity == parentIdentity)
                    initialIndexEntry = current.Single(entry => string.Equals(entry.Name, directoryParts[^1], StringComparison.Ordinal));

                return current;
            }

            public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                WindowsSkinPackageOpenMode mode,
                SkinManagedPackageCaptureRejectionReason unavailableReason)
            {
                IWindowsSkinPackageCaptureHandle handle = inner.OpenChildNoFollow(parent, name, mode, unavailableReason);

                try
                {
                    if (directoryParent == null
                        && mode == WindowsSkinPackageOpenMode.AuthorityDirectory
                        && string.Equals(name, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, StringComparison.Ordinal))
                    {
                        IWindowsSkinPackageCaptureHandle current = handle;
                        foreach (string part in directoryParts.SkipLast(1))
                        {
                            current = inner.OpenChildNoFollow(current, part, WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory, unavailableReason);
                            ancestors.Add(current);
                        }

                        directoryParent = current;
                        parentIdentity = inner.QueryMetadata(current).Identity;
                        preparedDeleteHandle = inner.OpenChildNoFollow(current, directoryParts[^1], WindowsSkinPackageOpenMode.ProvisionalDirectory, unavailableReason);
                        File.WriteAllText(Path.Combine(directoryRoot, "cache-refresh.txt"), "peer-cache-bytes");
                        canonicalReadHandle = inner.OpenChildNoFollow(current, directoryParts[^1], WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory, unavailableReason);
                        canonicalMetadata = inner.QueryMetadata(canonicalReadHandle);
                    }

                    return handle;
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }

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

            public void ReleasePreparedDeleteHandleAndVerifyIndexRefresh()
            {
                Assert.That(initialIndexEntry, Is.Not.Null);
                Assert.That(preparedDeleteHandle, Is.Not.Null);
                preparedDeleteHandle!.Dispose();
                preparedDeleteHandle = null;
                AssertCanonicalUnchanged();

                WindowsSkinPackageDirectoryEntry current = inner.Enumerate(directoryParent!, 65536, CancellationToken.None)
                    .Single(entry => string.Equals(entry.Name, directoryParts[^1], StringComparison.Ordinal));
                List<string> fields = changedFields(initialIndexEntry!.Metadata, current.Metadata);
                Logger.Log($"Native directory-index refresh fields: {string.Join(",", fields)}", LoggingTarget.Runtime, LogLevel.Important);

                Assert.Multiple(() =>
                {
                    Assert.That(fields, Is.Not.Empty, "Closing the prepared DELETE handle must expose a real directory-index refresh.");
                    Assert.That(fields, Is.SubsetOf(new[] { nameof(current.Metadata.LastWriteTime), nameof(current.Metadata.ChangeTime) }));
                    Assert.That(current.Metadata.LastWriteTime == canonicalMetadata.LastWriteTime, Is.True, "Refreshed directory index must match canonical held last-write time.");
                    Assert.That(current.Metadata.ChangeTime == canonicalMetadata.ChangeTime, Is.True, "Refreshed directory index must match canonical held change time.");
                });
            }

            public void AssertCanonicalUnchanged()
            {
                WindowsSkinPackageEntryMetadata current = inner.QueryMetadata(canonicalReadHandle!);
                List<string> fields = changedFields(canonicalMetadata, current);
                Logger.Log($"Native directory-index canonical changed fields: {string.Join(",", fields)}", LoggingTarget.Runtime, LogLevel.Important);
                Assert.That(fields, Is.Empty, "The same held directory must retain its complete canonical metadata.");
            }

            public void Dispose()
            {
                preparedDeleteHandle?.Dispose();
                canonicalReadHandle?.Dispose();
                for (int i = ancestors.Count - 1; i >= 0; i--)
                    ancestors[i].Dispose();
            }

            private static List<string> changedFields(WindowsSkinPackageEntryMetadata previous, WindowsSkinPackageEntryMetadata current)
            {
                var fields = new List<string>();
                if (previous.Identity != current.Identity) fields.Add(nameof(current.Identity));
                if (previous.Kind != current.Kind) fields.Add(nameof(current.Kind));
                if (previous.Length != current.Length) fields.Add(nameof(current.Length));
                if (previous.CreationTime != current.CreationTime) fields.Add(nameof(current.CreationTime));
                if (previous.LastWriteTime != current.LastWriteTime) fields.Add(nameof(current.LastWriteTime));
                if (previous.ChangeTime != current.ChangeTime) fields.Add(nameof(current.ChangeTime));
                if (previous.FileAttributes != current.FileAttributes) fields.Add(nameof(current.FileAttributes));
                if (previous.ReparseTag != current.ReparseTag) fields.Add(nameof(current.ReparseTag));
                if (previous.NumberOfLinks != current.NumberOfLinks) fields.Add(nameof(current.NumberOfLinks));
                if (previous.DeletePending != current.DeletePending) fields.Add(nameof(current.DeletePending));

                return fields;
            }
        }

        private sealed class DeleteRenameProbeFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly IWindowsSkinPackageCaptureFileSystem inner;
            private readonly string rootSource;
            private readonly string rootTarget;
            private readonly string childSource;
            private readonly string childTarget;
            private int attempted;

            public bool Attempted => Volatile.Read(ref attempted) != 0;

            public Exception? RootRenameException { get; private set; }

            public Exception? ChildRenameException { get; private set; }

            public DeleteRenameProbeFileSystem(
                IWindowsSkinPackageCaptureFileSystem inner,
                string rootSource,
                string rootTarget,
                string childSource,
                string childTarget)
            {
                this.inner = inner;
                this.rootSource = rootSource;
                this.rootTarget = rootTarget;
                this.childSource = childSource;
                this.childTarget = childTarget;
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

            public WindowsSkinPackageEntryMetadata QueryMetadata(
                IWindowsSkinPackageCaptureHandle handle)
                => inner.QueryMetadata(handle);

            public void RenameChildNoReplace(
                IWindowsSkinPackageCaptureHandle source,
                IWindowsSkinPackageCaptureHandle targetParent,
                string targetName)
                => inner.RenameChildNoReplace(source, targetParent, targetName);

            public void DeleteNoFollow(IWindowsSkinPackageCaptureHandle handle)
            {
                if (Interlocked.Exchange(ref attempted, 1) == 0)
                {
                    ChildRenameException = tryRenameAndRestore(
                        childSource,
                        childTarget);
                    RootRenameException = tryRenameAndRestore(
                        rootSource,
                        rootTarget);
                }

                inner.DeleteNoFollow(handle);
            }

            public Stream CreateNonOwningReadStream(
                IWindowsSkinPackageCaptureHandle file)
                => inner.CreateNonOwningReadStream(file);

            private static Exception? tryRenameAndRestore(
                string source,
                string target)
            {
                try
                {
                    Directory.Move(source, target);
                    Directory.Move(target, source);
                    return null;
                }
                catch (Exception exception) when (
                    exception is IOException
                        or UnauthorizedAccessException)
                {
                    return exception;
                }
            }
        }

        private sealed class DeleteForeignAdditionProbeFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly IWindowsSkinPackageCaptureFileSystem inner;
            private readonly string foreignFile;
            private int attempted;

            public int DeleteCalls { get; private set; }

            public Exception? AdditionException { get; private set; }

            public DeleteForeignAdditionProbeFileSystem(
                IWindowsSkinPackageCaptureFileSystem inner,
                string foreignFile)
            {
                this.inner = inner;
                this.foreignFile = foreignFile;
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

            public WindowsSkinPackageEntryMetadata QueryMetadata(
                IWindowsSkinPackageCaptureHandle handle)
                => inner.QueryMetadata(handle);

            public void RenameChildNoReplace(
                IWindowsSkinPackageCaptureHandle source,
                IWindowsSkinPackageCaptureHandle targetParent,
                string targetName)
                => inner.RenameChildNoReplace(source, targetParent, targetName);

            public void DeleteNoFollow(IWindowsSkinPackageCaptureHandle handle)
            {
                if (Interlocked.Exchange(ref attempted, 1) == 0)
                {
                    try
                    {
                        File.WriteAllText(
                            foreignFile,
                            "foreign-race",
                            new UTF8Encoding(false));
                    }
                    catch (Exception exception)
                    {
                        AdditionException = exception;
                    }
                }

                DeleteCalls++;
                inner.DeleteNoFollow(handle);
            }

            public Stream CreateNonOwningReadStream(
                IWindowsSkinPackageCaptureHandle file)
                => inner.CreateNonOwningReadStream(file);
        }
    }
}
