// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Models;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ManiaDrawableHoldNote = osu.Game.Rulesets.Mania.Objects.Drawables.DrawableHoldNote;
using ManiaDrawableNote = osu.Game.Rulesets.Mania.Objects.Drawables.DrawableNote;
using ManiaHoldNote = osu.Game.Rulesets.Mania.Objects.HoldNote;
using ManiaNote = osu.Game.Rulesets.Mania.Objects.Note;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Product-path tests for selecting a visible managed BMS skin folder through <see cref="SkinManager"/>.
    /// Every package and Realm record in this fixture lives in the test scene's fresh storage.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class BmsManagedFolderSelectionProductTest : OsuTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private SkinManager manager = null!;
        private int sourceChangedCount;
        private readonly HashSet<string> externalPackageRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated skin manager", () =>
            {
                string journalPath = LocalStorage.GetFullPath(
                    SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME);

                if (File.Exists(journalPath))
                    File.Delete(journalPath);

                Realm.Write(realm =>
                {
                    SkinInfo? oms = realm.Find<SkinInfo>(SkinInfo.OMS_SKIN);

                    if (oms != null)
                        oms.FilesystemStorageAuthorityOwner = null;

                    foreach (SkinInfo external in realm.All<SkinInfo>()
                                                       .Where(record => record.IsExternalFilesystemStorage)
                                                       .ToArray())
                    {
                        realm.Remove(external);
                    }
                });

                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                externalPackageRoots.Clear();
                manager.SourceChanged += () => sourceChangedCount++;
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("shutdown skin manager", () =>
            {
                manager?.ShutdownManagedFolderMutations();
                deleteExternalPackageRoots();
            });
        }

        [TearDown]
        public void CleanUpExternalPackageRoots() => deleteExternalPackageRoots();

        [Test]
        public void TestManagedFolderSelectionPublishesImmutableSourceBoundBmsSkin()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Skin? selected = null;

            AddStep("create managed folder and Realm record", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            });

            AddStep("request managed folder selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for asynchronous selection", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);

            AddStep("assert one coherent publication", () =>
            {
                selected = manager.CurrentSkin.Value;

                Assert.Multiple(() =>
                {
                    Assert.That(selected, Is.TypeOf<BmsLegacySkin>());
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(selected!.SkinInfo.ID, Is.EqualTo(candidate.ID));
                    Assert.That(selected.SkinInfo.Value.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(manager.CanModify(candidate), Is.False);
                    Assert.That(candidate.PerformRead(info => manager.Delete(info)), Is.False);
                    Assert.That(candidate.PerformRead(info => info.Files.Count), Is.Zero);
                });
            });

            AddStep("delete and rewrite captured source files", () =>
            {
                File.WriteAllText(
                    Path.Combine(packageRoot, "skin.ini"),
                    "[Bms]\nKeymode: 7K\nNoteImage1: changed/on-disk\n");
                File.Delete(Path.Combine(packageRoot, "notes", "note.png"));
                File.Delete(Path.Combine(packageRoot, "notes", "head.png"));
                File.WriteAllBytes(Path.Combine(packageRoot, "notes", "body.png"), new byte[] { 0, 1, 2, 3 });
                File.WriteAllBytes(Path.Combine(packageRoot, "notes", "tail.png"), new byte[] { 4, 5, 6, 7 });
            });

            AddStep("resolve every native note component from capsule", () =>
            {
                var transformer = new BmsSkinTransformer(selected!);
                Drawable? note = resolve(transformer, BmsNoteSkinElements.Note);
                Drawable? head = resolve(transformer, BmsNoteSkinElements.LongNoteHead);
                Drawable? body = resolve(transformer, BmsNoteSkinElements.LongNoteBody);
                Drawable? tail = resolve(transformer, BmsNoteSkinElements.LongNoteTail);

                Assert.Multiple(() =>
                {
                    assertStaticSourceBound(note, typeof(BmsSourceBoundNoteDrawable));
                    assertStaticSourceBound(head, typeof(BmsSourceBoundNoteDrawable));
                    assertStaticSourceBound(body, typeof(BmsSourceBoundLongNoteBodyDrawable));
                    assertStaticSourceBound(tail, typeof(BmsSourceBoundNoteDrawable));
                    Assert.That(body!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestDeleteFoundationRejectsCurrentBeforeC2PublicationWithoutDeleting()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> selectionA = null!;
            Skin selected = null!;
            SkinCurrentRevision revisionA = null!;

            AddStep("create eligible managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
            });

            AddStep("select managed folder", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("reject C1 fallback callback before C2 publication", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                selected = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                SkinManagedFolderMutationAuthorityResult authority = manager.ManagedFolderMutationAuthority.OpenDelete(
                    Guid.NewGuid(),
                    candidate.ID);

                Assert.That(authority.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = authority.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                SkinManagedFolderProtectedFallbackCommitResult result =
                    manager.CommitProtectedFallbackPairForDelete(session, receipt);

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(selected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(candidate.PerformRead(info => info.DeletePending), Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestDeleteFoundationCannotDisableSelectionToBypassPublication()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;

            AddStep("create eligible managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
            });

            AddStep("select managed folder", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("reject disabled bypass and fallback without C2 transaction", () =>
            {
                SkinManagedFolderMutationAuthorityResult authority = manager.ManagedFolderMutationAuthority.OpenDelete(
                    Guid.NewGuid(),
                    candidate.ID);

                Assert.That(authority.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = authority.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                InvalidOperationException disabled = Assert.Throws<InvalidOperationException>(
                    () => manager.CurrentSkinInfo.Disabled = true)!;
                SkinManagedFolderProtectedFallbackCommitResult result =
                    manager.CommitProtectedFallbackPairForDelete(session, receipt);

                Assert.Multiple(() =>
                {
                    Assert.That(disabled.Message, Is.EqualTo(SkinSelectionBindable.DISABLE_DISABLED_DIAGNOSTIC));
                    Assert.That(manager.CurrentSkinInfo.Disabled, Is.False);
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(candidate.ID));
                    Assert.That(candidate.PerformRead(info => info.DeletePending), Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedDeleteRejectsNonCanonicalProtectedFallbackBeforePhysicalDetach()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task<bool>? deleteTask = null;

            AddStep("create current delete target", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
            });
            AddUntilStep("wait for current delete target capture stability", () =>
                candidate.PerformRead(info =>
                {
                    SkinFilesystemStorageResolution resolution =
                        SkinFilesystemStorageResolver.ResolveExisting(info, LocalStorage);
                    SkinManagedPackageCaptureResult capture =
                        manager.ManagedFolderCapture(
                            resolution.ManagedCaptureRequest!,
                            CancellationToken.None);
                    capture.Capsule?.Dispose();
                    return capture.IsSuccess;
                }));
            AddStep("select current delete target", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for current fallback-validation selection outcome", () =>
                (manager.CurrentSkinInfo.Value.ID == candidate.ID
                 && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID)
                || manager.LastSelectionRejectionReason != SkinSelectionRejectionReason.None);
            AddStep("require current fallback-validation target", () =>
                Assert.That(
                    manager.CurrentSkin.Value.SkinInfo.ID,
                    Is.EqualTo(candidate.ID),
                    $"selection rejected: {manager.LastSelectionRejectionReason}"));
            AddStep("drift protected fallback owner", () =>
                Realm.Write(r =>
                    r.Find<SkinInfo>(SkinInfo.OMS_SKIN)!
                     .FilesystemStorageAuthorityOwner = "foreign-owner"));
            AddStep("request delete with noncanonical fallback", () =>
                deleteTask = manager.DeleteSkinAsync(candidate.ID));
            AddUntilStep("wait for fallback rejection", () =>
                deleteTask?.IsCompleted == true);
            AddStep("assert rejection preceded physical detach", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.Status,
                        Is.EqualTo(SkinManagedFolderDeleteOperationStatus.FallbackRejected));
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.FallbackInvalid));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(candidate.ID));
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) != null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });

                Realm.Write(r =>
                    r.Find<SkinInfo>(SkinInfo.OMS_SKIN)!
                     .FilesystemStorageAuthorityOwner = null);
            });
        }

        [Test]
        public void TestDeleteFoundationKeepsDurableIntentWhenFallbackIsNotRequired()
        {
            Live<SkinInfo> candidate = null!;

            AddStep("create unselected eligible managed folder", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
            });

            AddStep("confirm no fallback change is required", () =>
            {
                SkinManagedFolderMutationAuthorityResult authority = manager.ManagedFolderMutationAuthority.OpenDelete(
                    Guid.NewGuid(),
                    candidate.ID);

                Assert.That(authority.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = authority.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                SkinManagedFolderProtectedFallbackCommitResult result =
                    manager.CommitProtectedFallbackPairForDelete(session, receipt);
                SkinManagedFolderMutationJournalLoadResult journal =
                    new SkinManagedFolderMutationJournalStore(LocalStorage).Load();

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(journal.IsLoaded, Is.True);
                    Assert.That(journal.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                });

                Assert.That(session.TryAbortPreparedJournal(receipt), Is.True, "foundation test must leave no unresolved delete intent");
            });
        }

        [Test]
        public void TestDeleteFoundationBackingProjectionTamperCannotImpersonateProtectedFallback()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> selectionA = null!;
            Skin selected = null!;
            SkinCurrentRevision revisionA = null!;
            FolderInventorySnapshot sourceA = default;

            AddStep("create and select eligible managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("tamper only the guarded owner backing projection", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                selected = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                sourceA = captureFolderInventory(packageRoot);

                // CommitPrepared is an internal projection primitive, not publication authority. Even a friend
                // assembly cannot make its backing value observable through the immutable current pair.
                ((SkinInstanceBindable)manager.CurrentSkin).CommitPrepared(manager.DefaultOmsSkin);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(selected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(selected));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                });
            });
            AddStep("C1 callback rejects delete before C2 fallback publication", () =>
            {
                SkinManagedFolderMutationAuthorityResult authority = manager.ManagedFolderMutationAuthority.OpenDelete(
                    Guid.NewGuid(),
                    candidate.ID);

                Assert.That(authority.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = authority.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                SkinManagedFolderProtectedFallbackCommitResult result =
                    manager.CommitProtectedFallbackPairForDelete(session, receipt);

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(selected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedDeleteProductionCallerCommitsFallbackAndConvergesPhysicalAndRealmState()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task<bool>? deleteTask = null;

            AddStep("create and select deletable managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("request dedicated asynchronous delete", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CanModify(candidate), Is.False);
                    Assert.That(manager.CanDelete(candidate), Is.True);
                });

                deleteTask = manager.DeleteSkinAsync(candidate.ID);
                Assert.That(deleteTask.IsCompleted, Is.False, "the update-thread caller must not wait for native deletion");
            });
            AddUntilStep("wait for managed delete", () => deleteTask?.IsCompleted == true);
            AddStep("assert coherent fallback and delete convergence", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        deleteTask!.GetAwaiter().GetResult(),
                        Is.True,
                        $"{manager.LastManagedFolderDeleteResult}; "
                        + $"fallback={manager.LastManagedFolderDeleteResult.FallbackCommitResult}");
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired),
                        "C2 publishes the protected fallback before re-entering C1.");
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) == null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedDeleteFallbackReentrantSelectionNeverSplitsPairAndLatestWins()
        {
            string deletedRoot = string.Empty;
            Live<SkinInfo> deleted = null!;
            Live<SkinInfo> selectable = null!;
            Task<bool>? deleteTask = null;
            SkinSelectionRejectionReason reentrantRejection = SkinSelectionRejectionReason.None;
            bool reentrantAttempted = false;
            int captureCalls = 0;

            AddStep("create current delete target and later selection", () =>
            {
                (deletedRoot, deleted) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (_, selectable) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                deleted.PerformWrite(info => info.Hash = "delete-revision");
                selectable.PerformWrite(info => info.Hash = "selection-revision");
                manager.CurrentSkinInfo.Value = deleted;
            });
            AddUntilStep("wait for current delete target", () =>
                manager.CurrentSkin.Value.SkinInfo.ID == deleted.ID);
            AddStep("install fallback reentrant selection", () =>
            {
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    return nativeCapture(request, cancellationToken);
                };
                manager.SourceChanged += () =>
                {
                    if (reentrantAttempted
                        || manager.CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN)
                    {
                        return;
                    }

                    reentrantAttempted = true;
                    manager.CurrentSkinInfo.Value = selectable;
                    reentrantRejection = manager.LastSelectionRejectionReason;
                };
            });
            AddStep("delete current target", () =>
                deleteTask = manager.DeleteSkinAsync(deleted.ID));
            // Keep the durable prepare/fallback phase and the post-fallback physical/Realm convergence as separate
            // deterministic gates. A broad visual-test process can legitimately spend most of one default step budget
            // capturing the exact Windows directory before SourceChanged is published; one combined wall-clock gate
            // would then time out despite both bounded production phases making forward progress.
            AddUntilStep("wait for reentrant fallback publication", () => reentrantAttempted);
            AddUntilStep("wait for reentrant selection linearisation", () =>
                reentrantRejection == SkinSelectionRejectionReason.ManagedFolderOperationInProgress
                || Volatile.Read(ref captureCalls) > 0);
            AddUntilStep("wait for reentrant delete convergence", () =>
                deleteTask?.IsCompleted == true);
            AddStep("assert reentrant request could not split fallback pair", () =>
            {
                bool rejectedDuringDelete = reentrantRejection
                                            == SkinSelectionRejectionReason.ManagedFolderOperationInProgress;

                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(reentrantAttempted, Is.True);
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(manager.CurrentSkinInfo.Value.ID));
                    Assert.That(Directory.Exists(deletedRoot), Is.False);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(deleted.ID) == null), Is.True);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(selectable.ID) != null), Is.True);
                });

                if (rejectedDuringDelete)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(captureCalls, Is.Zero);
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    });
                }
                else
                    Assert.That(captureCalls, Is.EqualTo(1));
            });
            AddStep("explicit post-delete latest selection", () =>
            {
                if (reentrantRejection == SkinSelectionRejectionReason.ManagedFolderOperationInProgress)
                    manager.CurrentSkinInfo.Value = selectable;
            });
            AddUntilStep("wait for explicit latest selection", () =>
                manager.CurrentSkin.Value.SkinInfo.ID == selectable.ID);
            AddAssert("latest selection captured once", () => captureCalls == 1);
        }

        [Test]
        public void TestManagedDeleteFallbackSourceChangeCanReentrantlyShutdownAndJoinWorker()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Skin? deletedSkin = null;
            Task<bool>? deleteTask = null;
            Task? shutdownTask = null;
            bool shutdownEntered = false;
            bool shutdownCompletedInsideCallback = false;
            Live<SkinInfo>? selectionA = null;
            SkinCurrentRevision? revisionA = null;

            AddStep("create current delete target", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for current shutdown target", () =>
                manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("install source-change shutdown reentry", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                deletedSkin = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                manager.SourceChanged += () =>
                {
                    if (shutdownEntered
                        || manager.CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN)
                    {
                        return;
                    }

                    shutdownEntered = true;
                    shutdownTask = Task.Run(manager.ShutdownManagedFolderMutations);
                    shutdownCompletedInsideCallback = shutdownTask.Wait(TimeSpan.FromSeconds(10));
                };
            });
            AddStep("delete current target into reentrant shutdown", () =>
                deleteTask = manager.DeleteSkinAsync(candidate.ID));
            AddUntilStep("wait for shutdown and delete terminal state", () =>
                shutdownTask?.IsCompleted == true && deleteTask?.IsCompleted == true);
            AddUntilStep("wait for shutdown revision reap", () =>
                revisionA?.Retired.IsCompleted == true);
            AddStep("assert source callback did not form a join cycle", () =>
            {
                bool deleted = deleteTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(shutdownEntered, Is.True);
                    Assert.That(shutdownCompletedInsideCallback, Is.True);
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(manager.CurrentSkinInfo.Value.ID));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });

                if (deleted)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                        Assert.That(manager.CurrentRevision, Is.Not.SameAs(revisionA));
                        Assert.That(revisionA!.Retired.IsCompleted, Is.True);
                        Assert.That(Directory.Exists(packageRoot), Is.False);
                        Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) == null), Is.True);
                    });
                }
                else
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(deletedSkin));
                        Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                        Assert.That(revisionA!.Retired.IsCompleted, Is.True);
                        Assert.That(Directory.Exists(packageRoot), Is.True);
                        Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) != null), Is.True);
                    });
                }

            });
        }

        [Test]
        public void TestManagedDeleteRealSettingsButtonAndDialogReturnBeforeConvergence()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            DialogOverlay dialogOverlay = null!;
            SkinSection.DeleteSkinButton deleteButton = null!;

            AddStep("create and select managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("assert authoritative delete affordance", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkin.Disabled, Is.False);
                    Assert.That(manager.CanDelete(manager.CurrentSkin.Value.SkinInfo), Is.True);
                });
            });
            AddStep("mount real settings delete caller", () =>
            {
                var callerHost = new ManagedDeleteSettingsCallerHost(manager);
                dialogOverlay = callerHost.DialogOverlay;
                deleteButton = callerHost.DeleteButton;
                Add(callerHost);
            });
            AddUntilStep("wait for real settings caller load", () => deleteButton.IsLoaded);
            AddUntilStep("wait for independent delete affordance", () => deleteButton.Enabled.Value);
            AddStep("open real delete dialog", () => deleteButton.TriggerClick());
            AddUntilStep(
                "wait for real delete dialog",
                () => dialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog);
            AddStep(
                "confirm without blocking update thread",
                () => dialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep(
                "wait for physical and Realm convergence",
                () => !Directory.Exists(packageRoot)
                      && Realm.Run(r => r.Find<SkinInfo>(candidate.ID) == null));
            AddAssert("protected pair is coherent", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && manager.CurrentSkin.Value is OmsSkin
                && manager.CurrentSkin.Value.SkinInfo.ID == SkinInfo.OMS_SKIN);
        }

        [Test]
        public void TestFolderWorkspaceManagedRowDialogRechecksNonCurrentToCurrentThroughC2Fallback()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            Skin selected = null!;
            SkinCurrentRevision revisionA = null!;

            AddStep("create non-current managed workspace row", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-current-transition");
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for managed workspace delete row", () =>
                callerHost.IsLoaded
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open detached row delete dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for detached row delete dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("make dialog target current before confirmation", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for target current pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("confirm detached row delete", () =>
            {
                selected = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>();
            });
            AddUntilStep("observe workspace delete request", () =>
                callerHost.Workspace.OperationInProgress
                || manager.IsManagedFolderDeleteRunning
                || manager.LastManagedFolderDeleteResult != null);
            AddRepeatStep("allow workspace delete progress", () => { }, 300);
            AddUntilStep("wait for workspace delete request to settle", () =>
                !callerHost.Workspace.OperationInProgress
                && !manager.IsManagedFolderDeleteRunning);
            AddUntilStep("wait for workspace row delete convergence", () =>
                !Directory.Exists(packageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) == null)
                && revisionA.Retired.IsCompleted);
            AddStep("assert confirmation re-read used C2 fallback before C1", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired),
                        "C2 publishes the protected fallback before C1 re-enters under the held delete authority.");
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(selected));
                    Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(callerHost.Workspace.OperationInProgress, Is.False);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestFolderWorkspaceManagedRowDialogRechecksCurrentToNonCurrentAsNotRequired()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;

            AddStep("create and select current workspace target", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-not-required-transition");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for current workspace target", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount full skin section", () =>
            {
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for current managed workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open current row delete dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for current row dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("switch away before confirmation", () =>
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo);
            AddUntilStep("wait for protected non-current pair", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && manager.CurrentSkin.Value.SkinInfo.ID == SkinInfo.OMS_SKIN);
            AddStep("confirm now-non-current row delete", () =>
            {
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>();
            });
            AddRepeatStep("allow non-current row delete progress", () => { }, 300);
            AddUntilStep("wait for non-current row delete convergence", () =>
                !callerHost.Workspace.OperationInProgress
                && !manager.IsManagedFolderDeleteRunning
                && !Directory.Exists(packageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) == null));
            AddStep("assert fallback was not required", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestFolderWorkspaceManagedRowDialogUsesAuthoritativePairAfterBackingProjectionTamper()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            Live<SkinInfo> selectionA = null!;
            Skin selected = null!;
            SkinCurrentRevision revisionA = null!;
            FolderInventorySnapshot sourceA = default;

            AddStep("create and select authority-projection workspace target", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-split-transition");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for authority-projection target", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount authority-projection full skin section", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                selected = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                sourceA = captureFolderInventory(packageRoot);
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for authority-projection workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open authority-projection row dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for authority-projection dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("tamper owner backing projection before confirmation", () =>
            {
                ((SkinInstanceBindable)manager.CurrentSkin).CommitPrepared(manager.DefaultOmsSkin);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(selected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(selected));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                });
            });
            AddStep("confirm authoritative current row dialog", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("observe authoritative current delete", () =>
                callerHost.Workspace.OperationInProgress
                || manager.IsManagedFolderDeleteRunning
                || manager.LastManagedFolderDeleteResult != null);
            AddUntilStep("wait for authoritative current delete", () =>
                !callerHost.Workspace.OperationInProgress
                && !manager.IsManagedFolderDeleteRunning
                && manager.LastManagedFolderDeleteResult?.IsSuccess == true
                && !Directory.Exists(packageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) == null)
                && revisionA.Retired.IsCompleted);
            AddStep("assert fallback detach and managed delete used authoritative pair", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastManagedFolderDeleteResult.IsSuccess, Is.True);
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [TestCase("missing")]
        [TestCase("same-label-different-id")]
        [TestCase("owner")]
        [TestCase("path")]
        [TestCase("hash")]
        [TestCase("delete-pending")]
        [TestCase("external-generation")]
        public void TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation(string mutation)
        {
            string packageRoot = string.Empty;
            Guid recordId = Guid.Empty;
            Guid replacementId = Guid.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;

            AddStep("create detached-record workspace row", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-detached-record");
                recordId = candidate.ID;
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for detached-record workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == recordId)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open detached-record dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == recordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for detached-record dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == recordId);
            AddStep("mutate authority after dialog open", () =>
            {
                var dialog = (SkinSection.SkinDeleteDialog)callerHost.DialogOverlay.CurrentDialog!;
                Assert.That(dialog.BodyText.ToString(), Is.EqualTo("managed folder"));

                Realm.Write(realm =>
                {
                    SkinInfo target = realm.Find<SkinInfo>(recordId)!;

                    switch (mutation)
                    {
                        case "missing":
                            realm.Remove(target);
                            break;

                        case "same-label-different-id":
                            replacementId = Guid.NewGuid();
                            string name = target.Name;
                            string creator = target.Creator;
                            string instantiation = target.InstantiationInfo;
                            string hash = target.Hash;
                            string path = target.FilesystemStoragePath!;
                            realm.Remove(target);
                            realm.Add(new SkinInfo(name, creator, instantiation)
                            {
                                ID = replacementId,
                                Hash = hash,
                                FilesystemStoragePath = path,
                                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                            });
                            break;

                        case "owner":
                            target.FilesystemStorageAuthorityOwner = "foreign.workspace.owner";
                            break;

                        case "path":
                            target.FilesystemStoragePath = "chartskin/non-canonical/child";
                            break;

                        case "hash":
                            target.Hash = string.Empty;
                            break;

                        case "delete-pending":
                            target.DeletePending = true;
                            break;

                        case "external-generation":
                            realm.Add(new SkinInfo("external drift", "OMS tests", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                            {
                                ID = Guid.NewGuid(),
                                Hash = "external-generation-drift",
                                FilesystemStoragePath = Path.Combine(Path.GetTempPath(), $"external-drift-{Guid.NewGuid():N}"),
                                IsExternalFilesystemStorage = true,
                                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
                            });
                            break;

                        default:
                            throw new AssertionException($"Unknown detached record mutation {mutation}.");
                    }
                });
            });
            AddStep("confirm detached-record dialog", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for detached-record rejection", () =>
                !callerHost.Workspace.OperationInProgress
                && !manager.IsManagedFolderDeleteRunning);
            AddStep("assert detached id failed closed", () =>
            {
                bool targetExpected = mutation is not ("missing" or "same-label-different-id");

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(recordId) != null), Is.EqualTo(targetExpected));
                    Assert.That(
                        replacementId == Guid.Empty || Realm.Run(realm => realm.Find<SkinInfo>(replacementId) != null),
                        Is.True,
                        "A same-labelled replacement must never be treated as the confirmed record.");
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [TestCase("stale-source")]
        [TestCase("same-label-different-id")]
        [TestCase("owner")]
        [TestCase("path")]
        [TestCase("duplicate-path")]
        [TestCase("freeze")]
        public void TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch(string mutation)
        {
            string packageRoot = string.Empty;
            string managedPath = string.Empty;
            Guid recordId = Guid.Empty;
            Guid replacementId = Guid.Empty;
            int externalLaunches = 0;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            FolderSkinWorkspace.FolderSkinWorkspaceRow detachedRow = null!;

            AddStep("create managed open-folder row", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-open-drift");
                managedPath = candidate.PerformRead(info => info.FilesystemStoragePath!);
                recordId = candidate.ID;
                manager.OpenFolderExternally = _ => Interlocked.Increment(ref externalLaunches);
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for managed open-folder row", () =>
            {
                detachedRow = callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == recordId)!;
                return detachedRow?.ActionButtons[0].Enabled.Value == true;
            });
            AddStep("drift authority and click detached open row", () =>
            {
                switch (mutation)
                {
                    case "stale-source":
                        Directory.Delete(packageRoot, recursive: true);
                        break;

                    case "same-label-different-id":
                        Realm.Write(realm =>
                        {
                            SkinInfo target = realm.Find<SkinInfo>(recordId)!;
                            replacementId = Guid.NewGuid();
                            string name = target.Name;
                            string creator = target.Creator;
                            string instantiation = target.InstantiationInfo;
                            string hash = target.Hash;
                            string path = target.FilesystemStoragePath!;
                            realm.Remove(target);
                            realm.Add(new SkinInfo(name, creator, instantiation)
                            {
                                ID = replacementId,
                                Hash = hash,
                                FilesystemStoragePath = path,
                                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                            });
                        });
                        break;

                    case "owner":
                        candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = "foreign.workspace.owner");
                        break;

                    case "path":
                        candidate.PerformWrite(info => info.FilesystemStoragePath = "chartskin/non-canonical/child");
                        break;

                    case "duplicate-path":
                        Realm.Write(realm => realm.Add(new SkinInfo(
                            "duplicate managed declaration",
                            "OMS tests",
                            typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                        {
                            ID = Guid.NewGuid(),
                            Hash = "duplicate-managed-path",
                            FilesystemStoragePath = managedPath,
                            FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                        }));
                        break;

                    case "freeze":
                        manager.ManagedFolderOperationCoordinator.FreezePaths(new[] { managedPath });
                        break;

                    default:
                        throw new AssertionException($"Unknown open-folder mutation {mutation}.");
                }

                detachedRow.ActionButtons[0].TriggerClick();
            });
            AddUntilStep("wait for managed open-folder rejection", () => !callerHost.Workspace.OperationInProgress);
            AddStep("assert no stale path reached the host", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(externalLaunches, Is.Zero);
                        Assert.That(replacementId == Guid.Empty
                                    || Realm.Run(realm => realm.Find<SkinInfo>(replacementId) != null), Is.True);
                        Assert.That(
                            new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                            Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    });
                }
                finally
                {
                    if (mutation == "freeze")
                        manager.ManagedFolderOperationCoordinator.UnfreezePaths(new[] { managedPath });
                }
            });
        }

        [Test]
        public void TestFolderWorkspaceManagedRowDeleteDialogCancelIsSideEffectFree()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;

            AddStep("create cancellable managed workspace row", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-cancel");
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for cancellable workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open cancellable workspace dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for cancellable workspace dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("cancel workspace dialog", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogCancelButton>());
            AddUntilStep("wait for workspace dialog dismissal", () => callerHost.DialogOverlay.CurrentDialog == null);
            AddStep("assert cancellation performed no mutation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(callerHost.Workspace.OperationInProgress, Is.False);
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) != null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestFullSkinSectionCurrentDeleteKeepsOrdinaryRealmPackageRegression()
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? importTask = null;
            Live<SkinInfo> ordinary = null!;
            FullSkinSettingsCallerHost callerHost = null!;

            AddStep("import real ordinary Realm package", () =>
            {
                archive = createCurrentMutationOsk();
                importTask = manager.Import(new ImportTask(archive, $"full-settings-delete-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for real ordinary Realm import", () => importTask?.IsCompleted == true);
            AddStep("select real ordinary Realm package", () =>
            {
                ordinary = importTask!.GetAwaiter().GetResult();
                manager.CurrentSkinInfo.Value = ordinary;
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for ordinary current delete button", () =>
                manager.CurrentSkinInfo.Value.ID == ordinary.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == ordinary.ID
                && callerHost.CurrentDeleteButton.Enabled.Value);
            AddStep("open ordinary current delete dialog", () => callerHost.CurrentDeleteButton.TriggerClick());
            AddUntilStep("wait for ordinary current delete dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == ordinary.ID);
            AddStep("confirm ordinary current delete", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for ordinary soft delete", () =>
                ordinary.PerformRead(info => info.DeletePending)
                && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && manager.CurrentSkin.Value.SkinInfo.ID == SkinInfo.OMS_SKIN);
            AddStep("assert ordinary package never entered folder mutation", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(ordinary.ID) != null), Is.True);
                        Assert.That(ordinary.PerformRead(info => info.FilesystemStoragePath), Is.Null);
                        Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                        Assert.That(
                            new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                            Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    });
                }
                finally
                {
                    archive.Dispose();
                }
            });
        }

        [Test]
        public void TestFolderWorkspaceManagedRowDeleteDoubleConfirmDisablesReentryAndShutdownJoins()
        {
            string firstRoot = string.Empty;
            string secondRoot = string.Empty;
            Live<SkinInfo> first = null!;
            Live<SkinInfo> second = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            SkinSection.SkinDeleteDialog dialog = null!;

            AddStep("create two managed workspace rows", () =>
            {
                (firstRoot, first) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (secondRoot, second) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                first.PerformWrite(info => info.Hash = "workspace-double-confirm-first");
                second.PerformWrite(info => info.Hash = "workspace-double-confirm-second");
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for two managed workspace rows", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == first.ID)
                          ?.ActionButtons[2].Enabled.Value == true
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == second.ID)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open first managed row dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == first.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for first managed row dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog current
                && current.RecordId == first.ID);
            AddStep("hold coordinator and double-confirm", () =>
            {
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                dialog = (SkinSection.SkinDeleteDialog)callerHost.DialogOverlay.CurrentDialog!;
                dialog.PerformAction<PopupDialogDangerousButton>();
                dialog.PerformAction<PopupDialogDangerousButton>();
            });
            AddUntilStep("wait for one observed workspace operation", () =>
                callerHost.Workspace.OperationInProgress
                && manager.IsManagedFolderDeleteRunning);
            AddStep("assert every row is disabled against reentry", () =>
            {
                Assert.That(callerHost.Workspace.Rows.SelectMany(row => row.ActionButtons)
                                      .All(button => !button.Enabled.Value), Is.True);
                callerHost.Workspace.Rows.Single(row => row.RecordId == second.ID)
                          .ActionButtons[2]
                          .TriggerClick();
            });
            AddStep("shutdown joins the single observed row operation", () =>
            {
                try
                {
                    manager.ShutdownManagedFolderMutations();
                }
                finally
                {
                    heldLease!.Dispose();
                    heldLease = null;
                }
            });
            AddUntilStep("wait for workspace shutdown observation", () =>
                !callerHost.Workspace.OperationInProgress
                && !manager.IsManagedFolderDeleteRunning);
            AddStep("assert double-click reentry and shutdown left both targets", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(firstRoot), Is.True);
                    Assert.That(Directory.Exists(secondRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(first.ID) != null), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(second.ID) != null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestFolderWorkspaceOperationFailureLogsOnlyStableGenericText()
        {
            const string sensitive_exception_text = "C:\\private-author-workspace\\native-exception-body";
            FullSkinSettingsCallerHost callerHost = null!;
            Task? operationTask = null;
            var entries = new ConcurrentQueue<LogEntry>();

            void capture(LogEntry entry) => entries.Enqueue(entry);

            AddStep("mount workspace for redacted log boundary", () =>
                Add(callerHost = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("wait for workspace redacted log boundary", () => callerHost.Workspace.IsLoaded);
            AddStep("invoke failing UI operation", () =>
            {
                Logger.NewEntry += capture;
                MethodInfo method = typeof(FolderSkinWorkspace).GetMethod(
                    "performOperationAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new AssertionException("Missing workspace operation observer.");
                var failingOperation = (Func<Task<bool>>)(() =>
                    Task.FromException<bool>(new IOException(sensitive_exception_text)));
                operationTask = (Task)method.Invoke(callerHost.Workspace, new object[] { failingOperation, false })!;
            });
            AddUntilStep("wait for redacted operation log", () => operationTask?.IsCompleted == true);
            AddStep("unsubscribe and assert exception body was discarded", () =>
            {
                Logger.NewEntry -= capture;
                string[] messages = entries.Select(entry => entry.Message).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(messages, Has.Some.EqualTo("A folder skin workspace operation failed."));
                    Assert.That(messages.Any(message => message.Contains(sensitive_exception_text, StringComparison.Ordinal)), Is.False);
                    Assert.That(messages.Any(message => message.Contains("IOException", StringComparison.Ordinal)), Is.False);
                });
            });
        }

        [Test]
        public void TestCurrentDeleteJournalCompletionDynamicallyRefreshesRedactedWorkspaceSupport()
        {
            const string sensitive_exception_text = "C:\\private-author-workspace\\native-fault-detail";
            string packageRoot = string.Empty;
            string journalPath = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            SettingsNote supportNote = null!;
            Action? queuedFallback = null;

            AddStep("create current target with deferred fallback", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "workspace-dynamic-support");
                manager.ManagedFolderDeleteFallbackSchedule = action => queuedFallback = action;
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dynamic-support current target", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount dynamic-support full skin section", () =>
                Add(callerHost = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("wait for initial redacted support", () =>
            {
                supportNote = callerHost.Workspace.ChildrenOfType<SettingsNote>().Single();
                return supportNote.Current.Value?.Text.ToString().Contains("No pending recovery", StringComparison.Ordinal) == true;
            });
            AddUntilStep("wait for current delete button", () => callerHost.CurrentDeleteButton.Enabled.Value);
            AddStep("open current delete support dialog", () => callerHost.CurrentDeleteButton.TriggerClick());
            AddUntilStep("wait for current delete support dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("confirm current delete support dialog", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for durable prepared journal", () =>
                queuedFallback != null
                && new SkinManagedFolderMutationJournalStore(LocalStorage).Load().IsLoaded);
            AddStep("replace durable journal with sensitive invalid payload", () =>
            {
                journalPath = LocalStorage.GetFullPath(SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME);
                File.WriteAllText(journalPath, $"invalid journal {sensitive_exception_text}");
                queuedFallback!();
            });
            AddUntilStep("wait for journal-retaining delete completion", () =>
                !manager.IsManagedFolderDeleteRunning
                && manager.LastManagedFolderDeleteResult != null);
            AddUntilStep("wait for manager-event support refresh", () =>
                supportNote.Current.Value?.Text.ToString().Contains("state=invalid", StringComparison.Ordinal) == true);
            AddStep("assert support projection is dynamic and redacted", () =>
            {
                string support = supportNote.Current.Value!.Text.ToString();

                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastManagedFolderDeleteResult.IsSuccess, Is.False);
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
                    Assert.That(support, Does.Contain("Recovery needs support"));
                    Assert.That(support, Does.Contain("state=invalid"));
                    Assert.That(support, Does.Not.Contain(sensitive_exception_text));
                    Assert.That(support, Does.Not.Contain(packageRoot));
                    Assert.That(support, Does.Not.Contain(journalPath));
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) != null), Is.True);
                });

                File.Delete(journalPath);
            });
        }

        [Test]
        public void TestManagedDeleteProductionCallerDeletesNonCurrentWithoutChangingProtectedPair()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task<bool>? deleteTask = null;
            Live<SkinInfo>? originalInfo = null;
            Skin? originalSkin = null;

            AddStep("create non-current deletable managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
            });
            AddStep("request non-current managed delete", () => deleteTask = manager.DeleteSkinAsync(candidate.ID));
            AddUntilStep("wait for non-current delete", () => deleteTask?.IsCompleted == true);
            AddStep("assert protected pair was not replaced", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) == null), Is.True);
                });
            });
        }

        [Test]
        public void TestManagedDeleteAffordanceFailsClosedForEveryNonAuthoritativeDeclaration()
        {
            Live<SkinInfo> candidate = null!;
            string originalPath = string.Empty;

            AddStep("create exact managed delete candidate", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info =>
                {
                    info.Hash = "registered-revision";
                    originalPath = info.FilesystemStoragePath!;
                });
                Assert.That(manager.CanDelete(candidate), Is.True);
            });
            AddStep("reject foreign and null owner", () =>
            {
                candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = "foreign-owner");
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = null);
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info =>
                    info.FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER);
            });
            AddStep("reject invalid path and external declaration", () =>
            {
                candidate.PerformWrite(info => info.FilesystemStoragePath = "chartskin/nested/invalid");
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.FilesystemStoragePath = originalPath);
                candidate.PerformWrite(info => info.IsExternalFilesystemStorage = true);
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.IsExternalFilesystemStorage = false);
            });
            AddStep("reject protected pending hash and factory drift", () =>
            {
                candidate.PerformWrite(info => info.Protected = true);
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.Protected = false);
                candidate.PerformWrite(info => info.DeletePending = true);
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.DeletePending = false);
                candidate.PerformWrite(info => info.Hash = string.Empty);
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                candidate.PerformWrite(info => info.InstantiationInfo = "foreign.Type, foreign.Assembly");
                Assert.That(manager.CanDelete(candidate), Is.False);
                candidate.PerformWrite(info =>
                    info.InstantiationInfo = typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            });
            AddStep("reject duplicate path and fixed protected fallback", () =>
            {
                var duplicate = new SkinInfo(
                    "duplicate",
                    "OMS tests",
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    Hash = "duplicate-revision",
                    FilesystemStoragePath = originalPath,
                    FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                };
                Realm.Write(r => r.Add(duplicate));
                Assert.That(manager.CanDelete(candidate), Is.False);
                Realm.Write(r => r.Remove(r.Find<SkinInfo>(duplicate.ID)!));

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CanDelete(manager.DefaultOmsSkin.SkinInfo), Is.False);
                    Assert.That(manager.CanModify(candidate), Is.False);
                    Assert.That(manager.CanDelete(candidate), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                });
            });
        }

        [Test]
        public void TestRealmPackageSettingsDeleteKeepsLegacySoftDeleteAndDefaultSemantics()
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? importTask = null;
            Live<SkinInfo> realmPackage = null!;
            Task<bool>? deleteTask = null;
            FullSkinSettingsCallerHost callerHost = null!;

            AddStep("import real ordinary Realm package", () =>
            {
                archive = createCurrentMutationOsk();
                importTask = manager.Import(new ImportTask(archive, $"settings-delete-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for ordinary Realm import", () => importTask?.IsCompleted == true);
            AddStep("select real ordinary Realm package", () =>
            {
                realmPackage = importTask!.GetAwaiter().GetResult();
                manager.CurrentSkinInfo.Value = realmPackage;
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for ordinary package settings controls", () =>
                callerHost.RenameButton.IsLoaded
                && callerHost.ExportButton.IsLoaded
                && callerHost.LayoutEditorButton.IsLoaded);
            AddStep("assert current ordinary package mutation UI is fail-closed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(manager.CanModify(realmPackage), Is.False);
                    Assert.That(manager.CanExport(realmPackage), Is.True);
                    Assert.That(callerHost.RenameButton.Enabled.Value, Is.False);
                    Assert.That(callerHost.LayoutEditorButton.Enabled.Value, Is.False);
                    Assert.That(callerHost.ExportButton.Enabled.Value, Is.True);
                    Assert.That(manager.CanDelete(realmPackage), Is.True);
                });
            });
            AddStep("confirm ordinary settings delete", () => deleteTask = manager.DeleteSkinAsync(realmPackage.ID));
            AddUntilStep("wait for ordinary settings delete", () => deleteTask?.IsCompleted == true);
            AddStep("assert legacy soft delete and protected default", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(deleteTask!.IsCompletedSuccessfully, Is.True);
                        Assert.That(deleteTask.GetAwaiter().GetResult(), Is.True);
                        Assert.That(realmPackage.PerformRead(info => info.DeletePending), Is.True);
                        Assert.That(Realm.Run(r => r.Find<SkinInfo>(realmPackage.ID) != null), Is.True);
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                        Assert.That(
                            new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                            Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    });
                }
                finally
                {
                    archive.Dispose();
                }
            });
        }

        [Test]
        public void TestManagedDeleteQueuedFallbackIsClaimedByShutdownAndLateCallbackIsNoOp()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task<bool>? deleteTask = null;
            Action? queuedFallback = null;

            AddStep("create non-current deletable managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.ManagedFolderDeleteFallbackSchedule = action => queuedFallback = action;
            });
            AddStep("request delete without running queued fallback", () =>
            {
                deleteTask = manager.DeleteSkinAsync(candidate.ID);
                Assert.That(deleteTask.IsCompleted, Is.False);
            });
            AddUntilStep("wait for queued fallback", () => queuedFallback != null);
            AddStep("shutdown joins delete without update scheduler", () => manager.ShutdownManagedFolderMutations());
            AddStep("invoke late fallback callback", () => queuedFallback!());
            AddStep("assert shutdown owned cleanup exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.IsCompleted, Is.True);
                    Assert.That(deleteTask.GetAwaiter().GetResult(), Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) != null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedDeleteSerialisesScannerAndSelectionFailsClosedWithoutRetry()
        {
            string deletedRoot = string.Empty;
            Live<SkinInfo> deleted = null!;
            Live<SkinInfo> selectable = null!;
            Task<bool>? deleteTask = null;
            Task<SkinManagedFolderScanResult>? scanTask = null;
            var scannerBeforeCommit = new ManualResetEventSlim();
            var releaseScanner = new ManualResetEventSlim();
            int captureCalls = 0;

            AddStep("create delete and selection candidates", () =>
            {
                (deletedRoot, deleted) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (_, selectable) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                deleted.PerformWrite(info => info.Hash = "delete-revision");
                selectable.PerformWrite(info => info.Hash = "selection-revision");
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    return nativeCapture(request, cancellationToken);
                };

                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new WindowsSkinManagedFolderDiscoverySource(LocalStorage),
                    manager.ManagedFolderOperationCoordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        scannerBeforeCommit.Set();
                        Assert.That(releaseScanner.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    },
                };
                scanTask = Task.Run(() => scanner.Scan());
            });
            AddUntilStep("wait for scanner commit boundary", () => scannerBeforeCommit.IsSet);
            AddStep("reject manual selection during scanner boundary", () =>
            {
                manager.CurrentSkinInfo.Value = selectable;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        manager.LastSelectionRejectionReason,
                        Is.EqualTo(SkinSelectionRejectionReason.ManagedFolderOperationInProgress));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(scanTask!.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(deletedRoot), Is.True);
                });
            });
            AddStep("start delete behind scanner commit", () =>
                deleteTask = manager.DeleteSkinAsync(deleted.ID));
            AddUntilStep("wait for queued delete worker", () => manager.IsManagedFolderDeleteRunning);
            AddStep("assert delete has not crossed scanner commit", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(deletedRoot), Is.True);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(deleted.ID) != null), Is.True);
                });

                releaseScanner.Set();
            });
            AddUntilStep("wait for scanner then delete convergence", () =>
                deleteTask?.IsCompleted == true && scanTask?.IsCompleted == true);
            AddStep("assert no stale scanner resurrection or selection retry", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        deleteTask!.GetAwaiter().GetResult(),
                        Is.True,
                        $"{manager.LastManagedFolderDeleteResult}; "
                        + $"fallback={manager.LastManagedFolderDeleteResult.FallbackCommitResult}");
                    Assert.That(scanTask!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    Assert.That(Directory.Exists(deletedRoot), Is.False);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(deleted.ID) == null), Is.True);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(selectable.ID) != null), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(captureCalls, Is.Zero);
                });
            });
            AddStep("explicit post-delete selection may capture", () =>
                manager.CurrentSkinInfo.Value = selectable);
            AddUntilStep("wait for explicit post-delete selection", () =>
                manager.CurrentSkin.Value.SkinInfo.ID == selectable.ID);
            AddStep("release race fixtures", () =>
            {
                Assert.That(captureCalls, Is.EqualTo(1));
                scannerBeforeCommit.Dispose();
                releaseScanner.Dispose();
            });
        }

        [Test]
        public void TestInvalidInstantiationInfoIsRejectedSynchronouslyWithoutChangingCurrent()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            int captureCalls = 0;

            AddStep("create invalid managed folder record", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, "missing.Type, missing.Assembly");
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (_, _) =>
                {
                    captureCalls++;
                    throw new AssertionException("A rejected type must not reach native capture.");
                };
            });

            AddStep("request invalid selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddStep("assert synchronous rejection preserves publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.InstantiationInfoNotAllowed));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(captureCalls, Is.Zero);
                });
            });
        }

        [Test]
        public void TestUnregisteredFolderCannotBeSelected()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            int captureCalls = 0;

            AddStep("create unregistered managed folder", () =>
            {
                string folderName = $"unregistered-{Guid.NewGuid():N}";
                string relativePath = $"chartskin/{folderName}";
                string packageRoot = LocalStorage.GetFullPath(relativePath);
                Directory.CreateDirectory(packageRoot);
                createCompletePackage(packageRoot);
                candidate = new SkinInfo("unregistered", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    FilesystemStoragePath = relativePath,
                }.ToLiveUnmanaged();
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (_, _) =>
                {
                    captureCalls++;
                    throw new AssertionException("An unregistered folder must not reach native capture.");
                };
            });

            AddStep("request unregistered selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddStep("assert unregistered selection is rejected", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.UnmanagedFilesystemRecord));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [TestCase(null)]
        [TestCase("foreign-scanner:v1")]
        public void TestManagedFolderWithoutExactScannerOwnerCannotBeSelected(string? authorityOwner)
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            int captureCalls = 0;

            AddStep("create folder with non-authoritative owner", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = authorityOwner);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (_, _) =>
                {
                    captureCalls++;
                    throw new AssertionException("A folder without the exact scanner owner must not reach native capture.");
                };
            });

            AddStep("request non-authoritative selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddStep("assert owner rejection preserves publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.UnmanagedFilesystemRecord));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestFrozenManagedFolderCannotBeSelected()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            string managedPath = string.Empty;
            int captureCalls = 0;

            AddStep("create and freeze managed folder", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                managedPath = candidate.PerformRead(info => info.FilesystemStoragePath!);
                manager.ManagedFolderOperationCoordinator.FreezePaths(new[] { managedPath });
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (_, _) =>
                {
                    captureCalls++;
                    throw new AssertionException("A recovery-frozen folder must not reach native capture.");
                };
            });

            AddStep("request frozen selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddStep("assert recovery rejection preserves publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.MutationRecoveryPending));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
            AddStep("unfreeze managed folder", () => manager.ManagedFolderOperationCoordinator.UnfreezePaths(new[] { managedPath }));
        }

        [Test]
        public void TestConstructorRecoveryFreezesConfiguredManagedSelection()
        {
            Live<SkinInfo> candidate = null!;
            SkinManager? recoveringManager = null;
            var store = new SkinManagedFolderMutationJournalStore(LocalStorage);
            SkinManagedFolderMutationJournal? journal = null;

            AddStep("create candidate and unresolved journal", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                string managedPath = candidate.PerformRead(info => info.FilesystemStoragePath!);
                journal = SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    candidate.ID,
                    new SkinManagedFolderPhysicalIdentity(101, 201, 202),
                    managedPath,
                    new SkinManagedFolderPhysicalIdentity(101, 102, 103),
                    candidate.PerformRead(SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint),
                    SkinManagedFolderDeleteManifest.Create(
                        new[] { new string('a', 64) }));
                store.Write(journal);
            });
            AddStep("construct manager and apply configured selection", () =>
            {
                recoveringManager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler)
                {
                    ManagedFolderCapture = (_, _) => throw new AssertionException("frozen startup selection reached native capture")
                };
                recoveringManager.SetSkinFromConfiguration(candidate.ID.ToString());
            });
            AddStep("assert recovery ran before configured selection", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        recoveringManager!.InitialManagedFolderMutationRecoveryResult.Status,
                        Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(
                        recoveringManager.LastSelectionRejectionReason,
                        Is.EqualTo(SkinSelectionRejectionReason.MutationRecoveryPending));
                    Assert.That(recoveringManager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(recoveringManager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                });

                SkinManagedFolderMutationJournal rolledBack = journal!.WithRolledBack();
                store.Write(rolledBack);
                store.Delete(rolledBack);
            });
        }

        [Test]
        public void TestConfigurationSelectionUsesManagedFolderPreparationPath()
        {
            Live<SkinInfo> candidate = null!;

            AddStep("create configured managed folder record", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            });

            AddStep("select folder from configured ID", () => manager.SetSkinFromConfiguration(candidate.ID.ToString()));
            AddUntilStep("wait for configured folder selection", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("assert configured selection publishes once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestConfiguredManagedSelectionRetriesAfterBenignStartupScanner()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var scannerBeforeCommit = new ManualResetEventSlim();
            var releaseScanner = new ManualResetEventSlim();
            var finalBoundaryContended = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> manualCandidate = null!;
            Task<SkinManagedFolderScanResult>? scannerTask = null;
            var scannerStopwatch = new Stopwatch();
            int updateThreadHeartbeat = 0;
            int captureCalls = 0;
            int factoryCalls = 0;
            SkinPackageRevisionCapsule? firstPreparedCapsule = null;
            string? firstCapturedRevision = null;
            string? secondCapturedRevision = null;
            string? firstFactoryName = null;
            string? secondFactoryName = null;
            const int additional_package_count = 12;

            AddStep("create configured candidate and block capture", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                SkinFilesystemStorageResolution resolution = candidate.PerformRead(
                    info => SkinFilesystemStorageResolver.ResolveExisting(info, LocalStorage));
                SkinManagedPackageCaptureResult revisionCapture = SkinManagedPackageCapture.Capture(
                    resolution.ManagedCaptureRequest!);
                Assert.That(revisionCapture.IsSuccess, Is.True);
                string revision = revisionCapture.Capsule!.ContentRevision;
                revisionCapture.Capsule.Dispose();
                candidate.PerformWrite(info => info.Hash = revision);

                (_, manualCandidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                SkinFilesystemStorageResolution manualResolution = manualCandidate.PerformRead(
                    info => SkinFilesystemStorageResolver.ResolveExisting(info, LocalStorage));
                SkinManagedPackageCaptureResult manualRevisionCapture = SkinManagedPackageCapture.Capture(
                    manualResolution.ManagedCaptureRequest!);
                Assert.That(manualRevisionCapture.IsSuccess, Is.True);
                string manualRevision = manualRevisionCapture.Capsule!.ContentRevision;
                manualRevisionCapture.Capsule.Dispose();
                manualCandidate.PerformWrite(info => info.Hash = manualRevision);

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);

                    if (call == 1)
                    {
                        firstPreparedCapsule = result.Capsule;
                        firstCapturedRevision = result.Capsule?.ContentRevision;
                    }
                    else if (call == 2)
                        secondCapturedRevision = result.Capsule?.ContentRevision;

                    return result;
                };
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    int call = Interlocked.Increment(ref factoryCalls);

                    if (call == 1)
                        firstFactoryName = snapshot.Name;
                    else if (call == 2)
                        secondFactoryName = snapshot.Name;

                    return SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                };
                manager.ManagedFolderSelectionFinalBoundaryContended = () =>
                {
                    finalBoundaryContended.Set();
                    releaseScanner.Set();
                };
            });
            AddStep("start configured selection", () =>
                manager.SetSkinFromConfiguration(candidate.ID.ToString()));
            AddUntilStep("wait for configured capture", () => captureEntered.IsSet);
            AddStep("start benign startup scanner", () =>
            {
                string path = candidate.PerformRead(info => info.FilesystemStoragePath!);
                string name = candidate.PerformRead(info => info.Name);
                string creator = candidate.PerformRead(info => info.Creator);
                string revision = candidate.PerformRead(info => info.Hash);
                string manualPath = manualCandidate.PerformRead(info => info.FilesystemStoragePath!);
                string manualName = manualCandidate.PerformRead(info => info.Name);
                string manualCreator = manualCandidate.PerformRead(info => info.Creator);
                string manualRevision = manualCandidate.PerformRead(info => info.Hash);
                string[] alreadyObservedPaths = Realm.Run(realm => realm.All<SkinInfo>()
                    .AsEnumerable()
                    .Where(info => string.Equals(
                        info.FilesystemStorageAuthorityOwner,
                        SkinManagedFolderScanner.AUTHORITY_OWNER,
                        StringComparison.Ordinal))
                    .Select(info => info.FilesystemStoragePath)
                    .Where(existingPath => !string.IsNullOrEmpty(existingPath))
                    .Cast<string>()
                    .ToArray());
                string[] additionalPaths = Enumerable.Range(0, additional_package_count)
                                                     .Select(index => $"chartskin/startup-extra-{index:D2}")
                                                     .ToArray();
                SkinManagedFolderDiscovery[] additionalDiscoveries = additionalPaths
                                                                     .Select((extraPath, index) =>
                                                                         new SkinManagedFolderDiscovery(
                                                                             extraPath,
                                                                             $"startup extra {index:D2}",
                                                                             "OMS tests",
                                                                             $"STARTUP-REVISION-{index:D2}"))
                                                                     .ToArray();
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            alreadyObservedPaths.Concat(additionalPaths)
                                                .Distinct(StringComparer.OrdinalIgnoreCase),
                            new[]
                            {
                                new SkinManagedFolderDiscovery(path, "scanner refreshed configured", creator, revision),
                                new SkinManagedFolderDiscovery(manualPath, manualName, manualCreator, manualRevision)
                            }
                                .Concat(additionalDiscoveries))),
                    manager.ManagedFolderOperationCoordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        scannerBeforeCommit.Set();
                        Assert.That(releaseScanner.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    }
                };

                scannerTask = Task.Run(() =>
                {
                    scannerStopwatch.Start();

                    try
                    {
                        using SkinManagedFolderOperationCoordinator.Lease startupLease =
                            manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                        return scanner.Scan();
                    }
                    finally
                    {
                        scannerStopwatch.Stop();
                    }
                });
            });
            AddUntilStep("wait for scanner commit boundary", () => scannerBeforeCommit.IsSet);
            AddStep("request manual managed skin during startup scanner", () =>
                manager.CurrentSkinInfo.Value = manualCandidate);
            AddStep("assert manual startup request stays fail closed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.ManagedFolderOperationInProgress));
                    Assert.That(captureCalls, Is.EqualTo(1));
                });
            });
            AddStep("release capture into startup scanner boundary", releaseCapture.Set);
            AddUntilStep("wait for final boundary contention", () => finalBoundaryContended.IsSet);
            AddStep("prove update thread remains live", () => updateThreadHeartbeat++);
            AddUntilStep("wait for benign scanner", () => scannerTask?.IsCompleted == true);
            AddUntilStep("wait for configured selection retry", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("assert benign startup contention converged", () =>
            {
                SkinManagedFolderScanResult result = scannerTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(result.Added, Is.EqualTo(additional_package_count));
                    Assert.That(result.Updated, Is.EqualTo(1));
                    Assert.That(result.Revived, Is.Zero);
                    Assert.That(result.SoftDeleted, Is.Zero);
                    Assert.That(result.Conflicts, Is.Zero);
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(factoryCalls, Is.EqualTo(2));
                    Assert.That(firstCapturedRevision, Is.EqualTo(secondCapturedRevision));
                    Assert.That(firstFactoryName, Is.EqualTo("managed folder"));
                    Assert.That(secondFactoryName, Is.EqualTo("scanner refreshed configured"));
                    Assert.That(firstPreparedCapsule, Is.Not.Null);
                    Assert.That(
                        () => firstPreparedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                    Assert.That(updateThreadHeartbeat, Is.EqualTo(1));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(
                        manager.CurrentSkinInfo.Value.PerformRead(info => info.Name),
                        Is.EqualTo("scanner refreshed configured"));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.Value.Name, Is.EqualTo("scanner refreshed configured"));
                });

                TestContext.Progress.WriteLine(
                    $"startup scanner: observed={additional_package_count + 2}, elapsed={scannerStopwatch.Elapsed.TotalMilliseconds:F1}ms");

                captureEntered.Dispose();
                releaseCapture.Dispose();
                scannerBeforeCommit.Dispose();
                releaseScanner.Dispose();
                finalBoundaryContended.Dispose();
            });
        }

        [TestCase(StartupScannerCompletionTiming.BeforeCaptureCompletion)]
        [TestCase(StartupScannerCompletionTiming.DuringFactory)]
        public void TestConfiguredManagedSelectionRetriesAfterCompletedStartupScanner(
            StartupScannerCompletionTiming timing)
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startScanner = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Task<SkinManagedFolderScanResult>? scannerTask = null;
            SkinPackageRevisionCapsule? firstPreparedCapsule = null;
            int captureCalls = 0;
            int factoryCalls = 0;

            AddStep("create configured candidate for completed scanner", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                SkinFilesystemStorageResolution resolution = candidate.PerformRead(
                    info => SkinFilesystemStorageResolver.ResolveExisting(info, LocalStorage));
                SkinManagedPackageCaptureResult revisionCapture = SkinManagedPackageCapture.Capture(
                    resolution.ManagedCaptureRequest!);
                Assert.That(revisionCapture.IsSuccess, Is.True);
                string revision = revisionCapture.Capsule!.ContentRevision;
                revisionCapture.Capsule.Dispose();
                candidate.PerformWrite(info => info.Hash = revision);

                string path = candidate.PerformRead(info => info.FilesystemStoragePath!);
                string creator = candidate.PerformRead(info => info.Creator);
                string[] observedPaths = Realm.Run(realm => realm.All<SkinInfo>()
                    .AsEnumerable()
                    .Where(info => string.Equals(
                        info.FilesystemStorageAuthorityOwner,
                        SkinManagedFolderScanner.AUTHORITY_OWNER,
                        StringComparison.Ordinal))
                    .Select(info => info.FilesystemStoragePath)
                    .Where(existingPath => !string.IsNullOrEmpty(existingPath))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            observedPaths,
                            new[]
                            {
                                new SkinManagedFolderDiscovery(
                                    path,
                                    "completed startup scanner",
                                    creator,
                                    revision)
                            })),
                    manager.ManagedFolderOperationCoordinator);
                scannerTask = Task.Run(() =>
                {
                    Assert.That(startScanner.Wait(TimeSpan.FromSeconds(30)), Is.True);

                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    return scanner.Scan();
                });

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);

                    if (call == 1)
                    {
                        captureEntered.Set();
                        Assert.That(
                            releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                            Is.True);
                    }

                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);

                    if (call == 1)
                        firstPreparedCapsule = result.Capsule;

                    return result;
                };
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    Interlocked.Increment(ref factoryCalls);

                    if (timing == StartupScannerCompletionTiming.DuringFactory
                        && factoryCalls == 1)
                    {
                        startScanner.Set();
                        Assert.That(scannerTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    }

                    return SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("start configured candidate before completed scanner", () =>
                manager.SetSkinFromConfiguration(candidate.ID.ToString()));
            AddUntilStep("wait for completed-scanner first capture", () => captureEntered.IsSet);
            AddStep("start selected completed-scanner ordering", () =>
            {
                if (timing == StartupScannerCompletionTiming.BeforeCaptureCompletion)
                    startScanner.Set();
                else
                    releaseCapture.Set();
            });
            AddUntilStep("wait for startup scanner to complete", () => scannerTask?.IsCompleted == true);
            AddStep("release capture after early scanner", () =>
            {
                if (timing == StartupScannerCompletionTiming.BeforeCaptureCompletion)
                    releaseCapture.Set();
            });
            AddUntilStep("wait for completed-scanner retry", () => retryWaiting.IsSet);
            AddUntilStep("wait for completed-scanner selection", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("assert completed startup sequence was retried", () =>
            {
                SkinManagedFolderScanResult scan = scannerTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(scan.IsSuccess, Is.True);
                    Assert.That(scan.Updated, Is.EqualTo(1));
                    Assert.That(scan.SoftDeleted, Is.Zero);
                    Assert.That(scan.Conflicts, Is.Zero);
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(
                        factoryCalls,
                        Is.EqualTo(timing == StartupScannerCompletionTiming.DuringFactory ? 2 : 1));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(
                        manager.CurrentSkinInfo.Value.PerformRead(info => info.Name),
                        Is.EqualTo("completed startup scanner"));
                    Assert.That(firstPreparedCapsule, Is.Not.Null);
                    Assert.That(
                        () => firstPreparedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startScanner.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestCompletedGenericMutationCannotBorrowStartupRetryAuthority()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinPackageRevisionCapsule? capturedCapsule = null;
            int captureCalls = 0;

            AddStep("create candidate and block preparation", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);

                    SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(
                        request,
                        cancellationToken: cancellationToken);
                    capturedCapsule = result.Capsule;
                    return result;
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("request blocked candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for blocked capture", () => captureEntered.IsSet);
            AddStep("cross startup and then generic mutation", () =>
            {
                using (manager.ManagedFolderOperationCoordinator.EnterStartupSequence())
                {
                }

                using (manager.ManagedFolderOperationCoordinator.EnterMutation())
                    candidate.PerformWrite(info => info.Name = "generic mutation won");
            });
            AddStep("release capture after both completed boundaries", releaseCapture.Set);
            AddUntilStep("wait for fail-closed rejection", () =>
                manager.LastSelectionRejectionReason
                == SkinSelectionRejectionReason.CapturedCandidateChanged);
            AddStep("assert generic mutation suppressed startup retry", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(retryWaiting.IsSet, Is.False);
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(capturedCapsule, Is.Not.Null);
                    Assert.That(
                        () => capturedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestGenericMutationBeforeDirectStartupBoundaryCannotRetry()
        {
            var genericMutationCompleted = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var finalBoundaryContended = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinPackageRevisionCapsule? preparedCapsule = null;
            Task? startupTask = null;
            int captureCalls = 0;

            AddStep("create direct-boundary mutation ordering", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    return nativeCapture(request, cancellationToken);
                };
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(
                        snapshot,
                        resources,
                        capsule);
                    preparedCapsule = capsule;

                    using (manager.ManagedFolderOperationCoordinator.EnterMutation())
                        candidate.PerformWrite(info => info.Name = "generic mutation before startup boundary");

                    genericMutationCompleted.Set();
                    Assert.That(startupHeld.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    return result;
                };
                manager.ManagedFolderSelectionFinalBoundaryContended = () =>
                {
                    finalBoundaryContended.Set();
                    releaseStartup.Set();
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
                startupTask = Task.Run(() =>
                {
                    Assert.That(
                        genericMutationCompleted.Wait(TimeSpan.FromSeconds(30)),
                        Is.True);

                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddStep("request direct-boundary candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for direct startup boundary", () => finalBoundaryContended.IsSet);
            AddUntilStep("wait for direct fail-closed rejection", () =>
                manager.LastSelectionRejectionReason
                == SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
            AddStep("assert direct contention did not borrow startup retry", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(retryWaiting.IsSet, Is.False);
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(preparedCapsule, Is.Not.Null);
                    Assert.That(
                        () => preparedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                });

                genericMutationCompleted.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                finalBoundaryContended.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestGenericMutationBeforeDeferredStartupRetryCannotRecapture()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            var retryScheduled = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinPackageRevisionCapsule? preparedCapsule = null;
            Action? deferredRetry = null;
            Task? startupTask = null;
            int captureCalls = 0;
            int completionScheduleCalls = 0;

            AddStep("create deferred-retry mutation ordering", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);
                    preparedCapsule ??= result.Capsule;
                    return result;
                };
                Action<Action> nativeSchedule = manager.ManagedFolderCompletionSchedule;
                manager.ManagedFolderCompletionSchedule = completion =>
                {
                    int call = Interlocked.Increment(ref completionScheduleCalls);

                    if (call == 2)
                    {
                        deferredRetry = completion;
                        retryScheduled.Set();
                        return;
                    }

                    nativeSchedule(completion);
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
                startupTask = Task.Run(() =>
                {
                    Assert.That(captureEntered.Wait(TimeSpan.FromSeconds(30)), Is.True);

                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddStep("request deferred-retry candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for held deferred startup", () => startupHeld.IsSet);
            AddStep("release first capture into startup boundary", releaseCapture.Set);
            AddUntilStep("wait for startup retry registration", () => retryWaiting.IsSet);
            AddStep("release deferred startup", releaseStartup.Set);
            AddUntilStep("wait for deferred retry callback", () => retryScheduled.IsSet);
            AddStep("cross generic mutation before deferred callback", () =>
            {
                using (manager.ManagedFolderOperationCoordinator.EnterMutation())
                    candidate.PerformWrite(info => info.Name = "generic mutation before deferred retry");
            });
            AddStep("run deferred callback after generic mutation", () => deferredRetry!());
            AddUntilStep("wait for deferred fail-closed rejection", () =>
                manager.LastSelectionRejectionReason
                == SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
            AddStep("assert deferred retry did not recapture", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(completionScheduleCalls, Is.EqualTo(2));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(preparedCapsule, Is.Not.Null);
                    Assert.That(
                        () => preparedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                retryWaiting.Dispose();
                retryScheduled.Dispose();
            });
        }

        [Test]
        public void TestRealmPackageSelectionIsUnaffectedBySlowStartupScanner()
        {
            var scannerBeforeCommit = new ManualResetEventSlim();
            var releaseScanner = new ManualResetEventSlim();
            Live<SkinInfo> realmPackage = null!;
            Task<SkinManagedFolderScanResult>? scannerTask = null;
            int captureCalls = 0;
            int factoryCalls = 0;
            int updateThreadHeartbeat = 0;

            AddStep("create Realm package and instrument managed preparation", () =>
            {
                realmPackage = createRealmPackageCandidate();
                manager.ManagedFolderCapture = (_, _) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    throw new AssertionException("Realm package selection reached managed capture");
                };
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    return SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                };
            });
            AddStep("select configured Realm package before scanner", () =>
                manager.SetSkinFromConfiguration(realmPackage.ID.ToString()));
            AddStep("assert Realm package committed synchronously", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(factoryCalls, Is.Zero);
                });
            });
            AddStep("start slow empty startup scanner", () =>
            {
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            Array.Empty<string>(),
                            Array.Empty<SkinManagedFolderDiscovery>())),
                    manager.ManagedFolderOperationCoordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        scannerBeforeCommit.Set();
                        Assert.That(releaseScanner.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    }
                };

                scannerTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    return scanner.Scan();
                });
            });
            AddUntilStep("wait for slow Realm-safe scanner", () => scannerBeforeCommit.IsSet);
            AddStep("prove update thread remains live with Realm skin", () => updateThreadHeartbeat++);
            AddStep("release slow scanner", releaseScanner.Set);
            AddUntilStep("wait for Realm-safe scanner", () => scannerTask?.IsCompleted == true);
            AddStep("assert Realm package pair never changed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(scannerTask!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(captureCalls, Is.Zero);
                    Assert.That(factoryCalls, Is.Zero);
                    Assert.That(updateThreadHeartbeat, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                });

                scannerBeforeCommit.Dispose();
                releaseScanner.Dispose();
            });
        }

        [Test]
        public void TestStartupRetryWaitsForStagedImportAndRecaptures()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var startupRetryWaiting = new ManualResetEventSlim();
            var retryScheduled = new ManualResetEventSlim();
            var importAuthorityOpened = new ManualResetEventSlim();
            var releaseImport = new ManualResetEventSlim();
            var stagedImportRetryWaiting = new ManualResetEventSlim();
            Guid operationId = Guid.Empty;
            string importTargetName = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task? startupTask = null;
            Task<SkinManagedFolderStagedImportOperationResult>? importTask = null;
            Action? deferredRetry = null;
            SkinPackageRevisionCapsule? firstPreparedCapsule = null;
            int captureCalls = 0;
            int completionScheduleCalls = 0;

            AddStep("create startup candidate and staged import", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                operationId = Guid.NewGuid();
                importTargetName = $"startup-overlap-{Guid.NewGuid():N}";
                string stagedSource = LocalStorage.GetFullPath($"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);

                    if (call == 1)
                    {
                        captureEntered.Set();
                        Assert.That(
                            releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                            Is.True);
                    }

                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);

                    if (call == 1)
                        firstPreparedCapsule = result.Capsule;

                    return result;
                };

                Action<Action> nativeSchedule = manager.ManagedFolderCompletionSchedule;
                manager.ManagedFolderCompletionSchedule = completion =>
                {
                    int call = Interlocked.Increment(ref completionScheduleCalls);

                    if (call == 2)
                    {
                        deferredRetry = completion;
                        retryScheduled.Set();
                        return;
                    }

                    nativeSchedule(completion);
                };
                manager.ManagedFolderSelectionWaitingForStartup = startupRetryWaiting.Set;
                manager.ManagedFolderSelectionWaitingForStagedImport = stagedImportRetryWaiting.Set;
                manager.ManagedFolderStagedImportAuthorityOpened = () =>
                {
                    importAuthorityOpened.Set();
                    Assert.That(releaseImport.Wait(TimeSpan.FromSeconds(30)), Is.True);
                };
            });
            AddStep("start configured selection for staged overlap", () =>
                manager.SetSkinFromConfiguration(candidate.ID.ToString()));
            AddUntilStep("wait for staged-overlap capture", () => captureEntered.IsSet);
            AddStep("hold startup ahead of staged import", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for staged-overlap startup", () => startupHeld.IsSet);
            AddStep("queue staged import behind startup", () =>
                importTask = manager.ImportManagedFolderAsync(operationId, importTargetName));
            AddUntilStep("wait for staged import worker", () => manager.IsManagedFolderStagedImportRunning);
            AddStep("release first capture into startup", releaseCapture.Set);
            AddUntilStep("wait for startup retry registration", () => startupRetryWaiting.IsSet);
            AddStep("release startup to staged import", releaseStartup.Set);
            AddUntilStep("wait for deferred startup retry callback", () => retryScheduled.IsSet);
            AddUntilStep("wait for staged import authority", () => importAuthorityOpened.IsSet);
            AddStep("run deferred retry against exact staged import", () => deferredRetry!());
            AddUntilStep("wait for typed staged import retry", () => stagedImportRetryWaiting.IsSet);
            AddStep("release staged import", releaseImport.Set);
            AddUntilStep("wait for staged import and recaptured selection", () =>
                importTask?.IsCompleted == true
                && manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("assert staged import preserved pending selection", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(importTask!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(completionScheduleCalls, Is.EqualTo(4));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(firstPreparedCapsule, Is.Not.Null);
                    Assert.That(
                        () => firstPreparedCapsule!.CreateResourceView(),
                        Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                startupRetryWaiting.Dispose();
                retryScheduled.Dispose();
                importAuthorityOpened.Dispose();
                releaseImport.Dispose();
                stagedImportRetryWaiting.Dispose();
            });
        }

        [Test]
        public void TestNewerRealmSelectionCancelsStartupScannerRetry()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var realmRequestReachedBoundary = new ManualResetEventSlim();
            var finalBoundaryContended = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            var retryScheduled = new ManualResetEventSlim();
            Live<SkinInfo> managedCandidate = null!;
            Live<SkinInfo> realmPackage = null!;
            SkinPackageRevisionCapsule? firstCapsule = null;
            Task? startupTask = null;
            Action? deferredRetry = null;
            int captureCalls = 0;

            AddStep("create managed and newer Realm candidates", () =>
            {
                (_, managedCandidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                realmPackage = createRealmPackageCandidate();

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);
                    firstCapsule ??= result.Capsule;
                    return result;
                };
                manager.SelectionRequestBeforeCommitLock = target =>
                {
                    if (target.ID == realmPackage.ID)
                        realmRequestReachedBoundary.Set();
                };
                manager.ManagedFolderSelectionFinalBoundaryContended = () =>
                    finalBoundaryContended.Set();
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("start managed preparation", () =>
                manager.SetSkinFromConfiguration(managedCandidate.ID.ToString()));
            AddUntilStep("wait for managed capture", () => captureEntered.IsSet);
            AddStep("hold startup sequence", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for startup sequence", () => startupHeld.IsSet);
            AddStep("release managed capture", releaseCapture.Set);
            AddUntilStep("wait for startup final contention", () => finalBoundaryContended.IsSet);
            AddUntilStep("wait for registered startup retry", () => retryWaiting.IsSet);
            AddStep("defer startup retry completion", () => manager.ManagedFolderCompletionSchedule = completion =>
            {
                deferredRetry = completion;
                retryScheduled.Set();
            });
            AddStep("release startup after retry registration", releaseStartup.Set);
            AddUntilStep("wait for deferred startup retry", () => retryScheduled.IsSet && startupTask?.IsCompleted == true);
            AddStep("request newer Realm selection on update thread", () => manager.CurrentSkinInfo.Value = realmPackage);
            AddUntilStep("wait for Realm request boundary", () => realmRequestReachedBoundary.IsSet);
            AddUntilStep("wait for newer Realm selection", () =>
                manager.CurrentSkinInfo.Value.ID == realmPackage.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == realmPackage.ID);
            AddStep("run stale startup retry", () => deferredRetry!());
            AddStep("assert latest accepted selection wins", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(firstCapsule, Is.Not.Null);
                    Assert.That(() => firstCapsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                realmRequestReachedBoundary.Dispose();
                finalBoundaryContended.Dispose();
                retryWaiting.Dispose();
                retryScheduled.Dispose();
            });
        }

        [Test]
        public void TestShutdownAfterStartupJoinReapsQueuedRecapture()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            var secondCompletionQueued = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            SkinPackageRevisionCapsule? firstCapsule = null;
            SkinPackageRevisionCapsule? secondCapsule = null;
            Action? deferredSecondCompletion = null;
            Task? startupTask = null;
            int captureCalls = 0;
            int completionScheduleCalls = 0;

            AddStep("create candidate and retry shutdown gates", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);

                    if (call == 1)
                        firstCapsule = result.Capsule;
                    else if (call == 2)
                        secondCapsule = result.Capsule;

                    return result;
                };
                Action<Action> nativeSchedule = manager.ManagedFolderCompletionSchedule;
                manager.ManagedFolderCompletionSchedule = completion =>
                {
                    int call = Interlocked.Increment(ref completionScheduleCalls);

                    if (call == 3)
                    {
                        deferredSecondCompletion = completion;
                        secondCompletionQueued.Set();
                        return;
                    }

                    nativeSchedule(completion);
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("start configured preparation", () =>
                manager.SetSkinFromConfiguration(candidate.ID.ToString()));
            AddUntilStep("wait for shutdown capture", () => captureEntered.IsSet);
            AddStep("hold startup sequence for shutdown", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for shutdown startup sequence", () => startupHeld.IsSet);
            AddStep("release capture into shutdown wait", releaseCapture.Set);
            AddUntilStep("wait for startup retry registration", () => retryWaiting.IsSet);
            AddAssert("retry has not recaptured before startup completion", () => captureCalls == 1);
            AddStep("release and join startup before shutdown", releaseStartup.Set);
            AddUntilStep("wait for joined startup worker", () => startupTask?.IsCompleted == true);
            AddUntilStep("wait for queued second completion", () => secondCompletionQueued.IsSet);
            AddStep("shutdown managed folder workers", () => manager.ShutdownManagedFolderMutations());
            AddStep("run stale queued completion after shutdown", () => deferredSecondCompletion!());
            AddStep("assert shutdown reclaimed both preparations", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask!.IsCompletedSuccessfully, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(completionScheduleCalls, Is.EqualTo(3));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(firstCapsule, Is.Not.Null);
                    Assert.That(() => firstCapsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                    Assert.That(secondCapsule, Is.Not.Null);
                    Assert.That(() => secondCapsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                retryWaiting.Dispose();
                secondCompletionQueued.Dispose();
            });
        }

        [TestCase(StartupRetryInvalidation.Owner)]
        [TestCase(StartupRetryInvalidation.Freeze)]
        [TestCase(StartupRetryInvalidation.FactoryAllowlist)]
        [TestCase(StartupRetryInvalidation.RecordDeleted)]
        public void TestStartupRetryRevalidatesAuthoritativeCandidate(StartupRetryInvalidation invalidation)
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            SkinPackageRevisionCapsule? firstCapsule = null;
            Task? startupTask = null;
            int captureCalls = 0;
            SkinSelectionRejectionReason expectedReason = invalidation switch
            {
                StartupRetryInvalidation.Owner => SkinSelectionRejectionReason.UnmanagedFilesystemRecord,
                StartupRetryInvalidation.Freeze => SkinSelectionRejectionReason.MutationRecoveryPending,
                StartupRetryInvalidation.FactoryAllowlist => SkinSelectionRejectionReason.InstantiationInfoNotAllowed,
                StartupRetryInvalidation.RecordDeleted => SkinSelectionRejectionReason.CapturedCandidateChanged,
                _ => throw new ArgumentOutOfRangeException(nameof(invalidation)),
            };

            AddStep("create candidate for startup revalidation", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    Interlocked.Increment(ref captureCalls);
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);
                    firstCapsule ??= result.Capsule;
                    return result;
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("start candidate requiring revalidation", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for revalidation capture", () => captureEntered.IsSet);
            AddStep("hold revalidation startup sequence", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for revalidation startup sequence", () => startupHeld.IsSet);
            AddStep("release first preparation into startup wait", releaseCapture.Set);
            AddUntilStep("wait for deferred revalidation", () => retryWaiting.IsSet);
            AddStep("invalidate authoritative candidate", () =>
            {
                switch (invalidation)
                {
                    case StartupRetryInvalidation.Owner:
                        candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = "foreign-owner");
                        break;

                    case StartupRetryInvalidation.Freeze:
                        string path = candidate.PerformRead(info => info.FilesystemStoragePath!);
                        manager.ManagedFolderOperationCoordinator.FreezePaths(new[] { path });
                        break;

                    case StartupRetryInvalidation.FactoryAllowlist:
                        candidate.PerformWrite(info => info.InstantiationInfo = "missing.Type, missing.Assembly");
                        break;

                    case StartupRetryInvalidation.RecordDeleted:
                        Realm.Write(realm =>
                        {
                            SkinInfo record = realm.Find<SkinInfo>(candidate.ID)!;
                            realm.Remove(record);
                        });
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(invalidation));
                }
            });
            AddStep("release startup for authoritative retry", releaseStartup.Set);
            AddUntilStep("wait for authoritative retry rejection", () =>
                manager.LastSelectionRejectionReason == expectedReason);
            AddStep("assert stale factory and capsule never published", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(firstCapsule, Is.Not.Null);
                    Assert.That(() => firstCapsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestCompletionSchedulingFailureCannotDeadlockShutdownBehindMutation()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var mutationHeld = new ManualResetEventSlim();
            var releaseMutation = new ManualResetEventSlim();
            var schedulingFailed = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            SkinPackageRevisionCapsule? capsule = null;
            Task? mutationTask = null;
            Task? shutdownTask = null;

            AddStep("create scheduling-failure candidate", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    SkinManagedPackageCaptureResult result = nativeCapture(request, cancellationToken);
                    capsule = result.Capsule;
                    return result;
                };
                manager.ManagedFolderCompletionSchedule = _ =>
                {
                    schedulingFailed.Set();
                    throw new InvalidOperationException("synthetic scheduling failure");
                };
            });
            AddStep("start scheduling-failure selection", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for scheduling-failure capture", () => captureEntered.IsSet);
            AddStep("hold generic mutation during scheduling failure", () =>
            {
                mutationTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease mutationLease =
                        manager.ManagedFolderOperationCoordinator.EnterMutation();
                    mutationHeld.Set();
                    Assert.That(releaseMutation.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for scheduling-failure mutation", () => mutationHeld.IsSet);
            AddStep("release capture into throwing scheduler", releaseCapture.Set);
            AddUntilStep("wait for scheduling failure", () => schedulingFailed.IsSet);
            AddStep("shutdown while generic mutation remains held", () =>
                shutdownTask = Task.Run(() => manager.ShutdownManagedFolderMutations()));
            AddUntilStep("wait for nonblocking shutdown", () => shutdownTask?.IsCompleted == true);
            AddStep("release generic mutation after shutdown", releaseMutation.Set);
            AddUntilStep("wait for scheduling-failure mutation worker", () => mutationTask?.IsCompleted == true);
            AddStep("assert failed scheduling released prepared owner", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdownTask!.IsCompletedSuccessfully, Is.True);
                    Assert.That(mutationTask!.IsCompletedSuccessfully, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(capsule, Is.Not.Null);
                    Assert.That(() => capsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                mutationHeld.Dispose();
                releaseMutation.Dispose();
                schedulingFailed.Dispose();
            });
        }

        [Test]
        public void TestStartupRetryRecapturesAuthoritativePath()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Task? startupTask = null;
            string replacementPath = string.Empty;
            string? firstCaptureDirectory = null;
            string? secondCaptureDirectory = null;
            int captureCalls = 0;

            AddStep("create original and replacement managed paths", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                string replacementChild = $"retry-path-{Guid.NewGuid():N}";
                replacementPath = $"chartskin/{replacementChild}";
                string replacementRoot = LocalStorage.GetFullPath(replacementPath);
                Directory.CreateDirectory(replacementRoot);
                createCompletePackage(replacementRoot);

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);

                    if (call == 1)
                        firstCaptureDirectory = request.PackageDirectoryName;
                    else if (call == 2)
                        secondCaptureDirectory = request.PackageDirectoryName;

                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                        Is.True);
                    return nativeCapture(request, cancellationToken);
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
            });
            AddStep("start original path preparation", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for original path capture", () => captureEntered.IsSet);
            AddStep("hold path revalidation startup sequence", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for path startup sequence", () => startupHeld.IsSet);
            AddStep("release original capture into startup wait", releaseCapture.Set);
            AddUntilStep("wait for path retry registration", () => retryWaiting.IsSet);
            AddStep("publish replacement authoritative path", () =>
                candidate.PerformWrite(info => info.FilesystemStoragePath = replacementPath));
            AddStep("release startup for replacement capture", releaseStartup.Set);
            AddUntilStep("wait for replacement path publication", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("assert retry used only the refreshed path", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(firstCaptureDirectory, Is.Not.Null);
                    Assert.That(secondCaptureDirectory, Is.EqualTo(replacementPath["chartskin/".Length..]));
                    Assert.That(secondCaptureDirectory, Is.Not.EqualTo(firstCaptureDirectory));
                    Assert.That(
                        manager.CurrentSkinInfo.Value.PerformRead(info => info.FilesystemStoragePath),
                        Is.EqualTo(replacementPath));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.Value.FilesystemStoragePath, Is.EqualTo(replacementPath));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestNativeCaptureFailureIsRejectedWithoutChangingCurrent()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;

            AddStep("create hard-linked managed folder record", () =>
            {
                (string packageRoot, Live<SkinInfo> live) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate = live;

                if (!HardLinkHelper.TryCreateHardLink(
                        Path.Combine(packageRoot, "hardlink.ini"),
                        Path.Combine(packageRoot, "skin.ini")))
                {
                    Assert.Ignore("The test volume does not support hard links.");
                }

                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
            });

            AddStep("request uncapturable selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for capture rejection", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.CaptureRejected);
            AddStep("assert failed capture preserves publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestCaptureExceptionIsReducedToStableFailureWithoutChangingCurrent()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;

            AddStep("create candidate with faulting capture", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (_, _) => throw new InvalidOperationException("sensitive-local-path-must-not-escape");
            });

            AddStep("request faulting candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for stable failure", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.PreparationFailed);
            AddStep("assert fault did not publish", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestCompletionScheduleFailureDisposesCapturedCapsule()
        {
            var captureFinished = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinPackageRevisionCapsule? capturedCapsule = null;

            AddStep("create candidate with rejecting scheduler", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(request, cancellationToken: cancellationToken);
                    capturedCapsule = result.Capsule;
                    captureFinished.Set();
                    return result;
                };
                manager.ManagedFolderCompletionSchedule = _ => throw new InvalidOperationException("scheduler unavailable");
            });

            AddStep("request candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for capture", () => captureFinished.IsSet);
            AddUntilStep("wait for capsule cleanup", () =>
            {
                if (capturedCapsule == null)
                    return false;

                try
                {
                    capturedCapsule.CreateResourceView();
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            });
            AddStep("assert rejected scheduling did not publish", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });

                captureFinished.Dispose();
            });
        }

        [Test]
        public void TestCandidateMutationWhileCaptureIsInFlightRejectsAndDisposesCapsule()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinPackageRevisionCapsule? capturedCapsule = null;

            AddStep("create candidate and block capture", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    captureEntered.Set();
                    releaseCapture.Wait(cancellationToken);
                    SkinManagedPackageCaptureResult result = SkinManagedPackageCapture.Capture(request, cancellationToken: cancellationToken);
                    capturedCapsule = result.Capsule;
                    return result;
                };
            });

            AddStep("request blocked selection", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for capture to enter", () => captureEntered.IsSet);
            AddStep("repeat committed selection is a no-op", () => manager.CurrentSkinInfo.Value = originalInfo);
            AddStep("mutate candidate authority before publication", () => candidate.PerformWrite(info => info.DeletePending = true));
            AddStep("release capture", releaseCapture.Set);
            AddUntilStep("wait for stale candidate rejection", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.CapturedCandidateChanged);
            AddStep("assert stale capsule and publication are retired", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(capturedCapsule, Is.Not.Null);
                    Assert.That(() => capturedCapsule!.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
            });
        }

        [Test]
        public void TestCandidateMutationDuringFactoryCannotPublishPreparedSkin()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;

            AddStep("create candidate and mutating factory", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                    candidate.PerformWrite(info => info.Name = "changed during factory");
                    return result;
                };
            });

            AddStep("request candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for post-factory rejection", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.CapturedCandidateChanged);
            AddStep("assert prepared skin was never published", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestAuthorityOwnerMutationDuringFactoryCannotPublishPreparedSkin()
        {
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;

            AddStep("create candidate and owner-mutating factory", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                    candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = "changed-owner");
                    return result;
                };
            });

            AddStep("request candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for owner-change rejection", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.CapturedCandidateChanged);
            AddStep("assert prepared skin was never published", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestMutationParticipantBeforeFinalBoundaryCannotPublishStalePreparedSkin()
        {
            var factoryCompleted = new ManualResetEventSlim();
            var mutationHeld = new ManualResetEventSlim();
            var releaseMutation = new ManualResetEventSlim();
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Task? mutationTask = null;

            AddStep("create candidate and boundary mutation", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderFactoryCreate = (snapshot, resources, capsule) =>
                {
                    SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(snapshot, resources, capsule);
                    factoryCompleted.Set();
                    Assert.That(mutationHeld.Wait(TimeSpan.FromSeconds(10)), Is.True);
                    return result;
                };
                manager.ManagedFolderSelectionFinalBoundaryContended =
                    () => releaseMutation.Set();
                mutationTask = Task.Run(() =>
                {
                    Assert.That(factoryCompleted.Wait(TimeSpan.FromSeconds(10)), Is.True);

                    using (manager.ManagedFolderOperationCoordinator.Enter())
                    {
                        candidate.PerformWrite(info => info.FilesystemStorageAuthorityOwner = "changed-before-final-boundary");
                        mutationHeld.Set();
                        Assert.That(releaseMutation.Wait(TimeSpan.FromSeconds(10)), Is.True);
                    }
                });
            });

            AddStep("request candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for boundary rejection", () => manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
            AddStep("assert stale prepared skin was retired", () =>
            {
                Assert.That(mutationTask!.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });

                factoryCompleted.Dispose();
                mutationHeld.Dispose();
                releaseMutation.Dispose();
            });
        }

        [Test]
        public void TestReentrantRejectedRequestDoesNotLoseItsReason()
        {
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var startupHeld = new ManualResetEventSlim();
            var releaseStartup = new ManualResetEventSlim();
            var retryWaiting = new ManualResetEventSlim();
            Live<SkinInfo> valid = null!;
            Live<SkinInfo> invalid = null!;
            Task? startupTask = null;
            int captureCalls = 0;

            AddStep("create valid and invalid candidates", () =>
            {
                (_, valid) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (_, invalid) = createCandidate(createCompletePackage, "missing.Type, missing.Assembly");
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref captureCalls);

                    if (call == 1)
                    {
                        captureEntered.Set();
                        Assert.That(
                            releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                            Is.True);
                    }

                    return nativeCapture(request, cancellationToken);
                };
                manager.ManagedFolderSelectionWaitingForStartup = retryWaiting.Set;
                manager.SourceChanged += () =>
                {
                    if (manager.CurrentSkinInfo.Value.ID == valid.ID)
                        manager.CurrentSkinInfo.Value = invalid;
                };
            });

            AddStep("request valid candidate", () => manager.CurrentSkinInfo.Value = valid);
            AddUntilStep("wait for reentrant first capture", () => captureEntered.IsSet);
            AddStep("hold startup before reentrant publication", () =>
            {
                startupTask = Task.Run(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease startupLease =
                        manager.ManagedFolderOperationCoordinator.EnterStartupSequence();
                    startupHeld.Set();
                    Assert.That(releaseStartup.Wait(TimeSpan.FromSeconds(30)), Is.True);
                });
            });
            AddUntilStep("wait for reentrant startup", () => startupHeld.IsSet);
            AddStep("release reentrant first capture", releaseCapture.Set);
            AddUntilStep("wait for reentrant retry registration", () => retryWaiting.IsSet);
            AddStep("release reentrant startup", releaseStartup.Set);
            AddUntilStep("wait for valid publication", () => manager.CurrentSkin.Value.SkinInfo.ID == valid.ID);
            AddStep("assert reentrant rejection remains observable", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(2));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(valid.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(valid.ID));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.InstantiationInfoNotAllowed));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                startupHeld.Dispose();
                releaseStartup.Dispose();
                retryWaiting.Dispose();
            });
        }

        [Test]
        public void TestReentrantUpdateThreadRealmSelectionSupersedesManagedCommitAtomically()
        {
            Live<SkinInfo> managed = null!;
            Live<SkinInfo> realmPackage = null!;
            Skin? supersededManagedSkin = null;
            bool realmRequestCompleted = false;

            AddStep("create managed and Realm candidates", () =>
            {
                (_, managed) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                realmPackage = createRealmPackageCandidate();

                manager.SourceChanged += () =>
                {
                    if (manager.CurrentSkinInfo.Value.ID == managed.ID)
                        supersededManagedSkin = manager.CurrentSkin.Value;
                };

                manager.ManagedFolderBeforeCommit = () =>
                {
                    manager.CurrentSkinInfo.Value = realmPackage;
                    realmRequestCompleted = true;
                };
            });

            AddStep("request managed candidate", () => manager.CurrentSkinInfo.Value = managed);
            AddUntilStep("wait for reentrant Realm request", () => realmRequestCompleted);
            AddUntilStep("wait for Realm package to remain current", () =>
                manager.CurrentSkinInfo.Value.ID == realmPackage.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == realmPackage.ID);
            AddStep("assert latest request wins", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(realmRequestCompleted, Is.True);
                    Assert.That(supersededManagedSkin, Is.Null,
                        "the stale managed owner must never become current before the reentrant latest request");
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestOffThreadManagedRequestDoesNotCancelPendingSelection()
        {
            Live<SkinInfo> first = null!;
            Live<SkinInfo> second = null!;
            Task? offThreadRequest = null;
            Exception? offThreadException = null;
            var captureStarted = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();

            AddStep("create two managed candidates and block first capture", () =>
            {
                (_, first) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (_, second) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                var capture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    captureStarted.Set();
                    Assert.That(releaseCapture.Wait(TimeSpan.FromSeconds(5), cancellationToken), Is.True);
                    return capture(request, cancellationToken);
                };
            });

            AddStep("request first candidate", () => manager.CurrentSkinInfo.Value = first);
            AddUntilStep("wait for first capture", () => captureStarted.IsSet);
            AddStep("request second candidate off update thread", () => offThreadRequest = Task.Run(() =>
            {
                try
                {
                    manager.CurrentSkinInfo.Value = second;
                }
                catch (Exception exception)
                {
                    offThreadException = exception;
                }
            }));
            AddUntilStep("wait for off-thread rejection", () => offThreadRequest?.IsCompleted == true);
            AddStep("assert request had no cancellation side effect", () =>
            {
                Assert.That(offThreadException, Is.TypeOf<InvalidOperationException>());
                releaseCapture.Set();
            });
            AddUntilStep("wait for original candidate", () =>
                manager.CurrentSkinInfo.Value.ID == first.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == first.ID);
            AddStep("assert original request committed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });

                captureStarted.Dispose();
                releaseCapture.Dispose();
            });
        }

        [Test]
        public void TestManagedFolderRenameKeepsActiveCapsuleAndRecapturesFromNewPath()
        {
            string sourceRoot = string.Empty;
            string targetRoot = string.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            string originalName = string.Empty;
            string originalCreator = string.Empty;
            string originalHash = string.Empty;
            byte[] originalSkinIni = null!;
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> realmPackage = null!;
            Skin? activeManagedSkin = null;
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            string? recapturedChildName = null;

            AddStep("create rename candidate and Realm package", () =>
            {
                (sourceRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                targetChildName = $"renamed-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                targetRoot = LocalStorage.GetFullPath(targetManagedPath);
                originalSkinIni = File.ReadAllBytes(Path.Combine(sourceRoot, "skin.ini"));
                candidate.PerformRead(info =>
                {
                    originalName = info.Name;
                    originalCreator = info.Creator;
                    originalHash = info.Hash;
                });
                realmPackage = createRealmPackageCandidate();
            });

            AddStep("select managed candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for managed candidate", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("rename selected managed folder", () =>
            {
                activeManagedSkin = manager.CurrentSkin.Value;
                renameTask = manager.RenameManagedFolderAsync(candidate.ID, targetChildName);
            });
            AddUntilStep("wait for rename", () => renameTask?.IsCompleted == true);
            AddStep("assert directory-only rename preserves active capsule", () =>
            {
                SkinManagedFolderRenameOperationResult result = renameTask!.GetAwaiter().GetResult();
                var transformer = new BmsSkinTransformer(activeManagedSkin!);
                Drawable? note = resolve(transformer, BmsNoteSkinElements.Note);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(Directory.Exists(sourceRoot), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                    Assert.That(File.ReadAllBytes(Path.Combine(targetRoot, "skin.ini")), Is.EqualTo(originalSkinIni));
                    Assert.That(candidate.PerformRead(info => info.FilesystemStoragePath), Is.EqualTo(targetManagedPath));
                    Assert.That(candidate.PerformRead(info => info.Name), Is.EqualTo(originalName));
                    Assert.That(candidate.PerformRead(info => info.Creator), Is.EqualTo(originalCreator));
                    Assert.That(candidate.PerformRead(info => info.Hash), Is.EqualTo(originalHash));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(activeManagedSkin));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    assertStaticSourceBound(note, typeof(BmsSourceBoundNoteDrawable));
                });
            });

            AddStep("select Realm package", () => manager.CurrentSkinInfo.Value = realmPackage);
            AddUntilStep("wait for Realm package", () =>
                manager.CurrentSkinInfo.Value.ID == realmPackage.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == realmPackage.ID);
            AddStep("request renamed candidate", () =>
            {
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    recapturedChildName = request.PackageDirectoryName;
                    return nativeCapture(request, cancellationToken);
                };
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for recaptured candidate", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("assert future capture uses renamed path", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(activeManagedSkin));
                    Assert.That(recapturedChildName, Is.EqualTo(targetChildName));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.Value.FilesystemStoragePath, Is.EqualTo(targetManagedPath));
                    Assert.That(Directory.Exists(sourceRoot), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                    Assert.That(sourceChangedCount, Is.EqualTo(3));
                });
            });
        }

        [Test]
        public void TestManagedFolderRenameCancelsPendingOldPathSelection()
        {
            string sourceRoot = string.Empty;
            string targetRoot = string.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            var captureEntered = new ManualResetEventSlim();
            var captureExited = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();

            AddStep("create candidate and block old-path capture", () =>
            {
                (sourceRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                targetChildName = $"renamed-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                targetRoot = LocalStorage.GetFullPath(targetManagedPath);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    captureEntered.Set();

                    try
                    {
                        Assert.That(releaseCapture.Wait(TimeSpan.FromSeconds(30), cancellationToken), Is.True);
                        return nativeCapture(request, cancellationToken);
                    }
                    finally
                    {
                        captureExited.Set();
                    }
                };
            });

            AddStep("request candidate", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for old-path capture", () => captureEntered.IsSet);
            AddStep("rename while selection is pending", () =>
                renameTask = manager.RenameManagedFolderAsync(candidate.ID, targetChildName));
            AddUntilStep("wait for rename completion", () => renameTask?.IsCompleted == true);
            AddUntilStep("wait for pending capture cancellation", () => captureExited.IsSet);
            AddStep("assert stale selection never publishes", () =>
            {
                SkinManagedFolderRenameOperationResult result = renameTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(candidate.PerformRead(info => info.FilesystemStoragePath), Is.EqualTo(targetManagedPath));
                    Assert.That(Directory.Exists(sourceRoot), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                });

                releaseCapture.Set();
                captureEntered.Dispose();
                captureExited.Dispose();
                releaseCapture.Dispose();
            });
        }

        [Test]
        public void TestManagedFolderRenameWaitsForScannerSnapshotCommit()
        {
            string sourceRoot = string.Empty;
            string targetRoot = string.Empty;
            string sourceManagedPath = string.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            Live<SkinInfo> candidate = null!;
            Task<SkinManagedFolderScanResult>? scanTask = null;
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            var scannerBeforeCommit = new ManualResetEventSlim();
            var releaseScanner = new ManualResetEventSlim();

            AddStep("create candidate and blocking scanner snapshot", () =>
            {
                (sourceRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                sourceManagedPath = candidate.PerformRead(info => info.FilesystemStoragePath!);
                targetChildName = $"renamed-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                targetRoot = LocalStorage.GetFullPath(targetManagedPath);
                var discovery = new SkinManagedFolderDiscovery(
                    sourceManagedPath,
                    candidate.PerformRead(info => info.Name),
                    candidate.PerformRead(info => info.Creator),
                    candidate.PerformRead(info => info.Hash));
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            new[] { sourceManagedPath },
                            new[] { discovery })),
                    manager.ManagedFolderOperationCoordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        scannerBeforeCommit.Set();
                        Assert.That(releaseScanner.Wait(TimeSpan.FromSeconds(30)), Is.True);
                    },
                };
                scanTask = Task.Run(() => scanner.Scan());
            });

            AddUntilStep("wait for scanner commit boundary", () => scannerBeforeCommit.IsSet);
            AddStep("start rename behind scanner lease", () =>
                renameTask = manager.RenameManagedFolderAsync(candidate.ID, targetChildName));
            AddUntilStep("wait for rename worker", () => manager.IsManagedFolderRenameRunning);
            AddStep("assert rename has not crossed scanner commit", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(renameTask!.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(sourceRoot), Is.True);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(candidate.PerformRead(info => info.FilesystemStoragePath), Is.EqualTo(sourceManagedPath));
                });

                releaseScanner.Set();
            });
            AddUntilStep("wait for scanner completion", () => scanTask?.IsCompleted == true);
            AddUntilStep("wait for serialized rename", () => renameTask?.IsCompleted == true);
            AddStep("assert scanner then rename committed", () =>
            {
                SkinManagedFolderScanResult scan = scanTask!.GetAwaiter().GetResult();
                SkinManagedFolderRenameOperationResult rename = renameTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(scan.IsSuccess, Is.True);
                    Assert.That(rename.IsSuccess, Is.True);
                    Assert.That(candidate.PerformRead(info => info.FilesystemStoragePath), Is.EqualTo(targetManagedPath));
                    Assert.That(Directory.Exists(sourceRoot), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                });

                scannerBeforeCommit.Dispose();
                releaseScanner.Dispose();
            });
        }

        [Test]
        public void TestAmbiguousRenameRestartFreezesSelectionAndScannerNegativeCleanup()
        {
            string sourceRoot = string.Empty;
            string sourceManagedPath = string.Empty;
            string targetManagedPath = string.Empty;
            Live<SkinInfo> candidate = null!;
            SkinManager? recoveringManager = null;
            SkinManagedFolderMutationJournal? preparedJournal = null;
            SkinManagedFolderScanResult? scanResult = null;
            SkinManagedFolderMutationJournalStore? store = null;

            AddStep("persist prepared rename and create ambiguous target", () =>
            {
                store = new SkinManagedFolderMutationJournalStore(LocalStorage);
                (sourceRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                sourceManagedPath = candidate.PerformRead(info => info.FilesystemStoragePath!);
                string targetChildName = $"renamed-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                SkinManagedFolderMutationAuthorityResult opened = manager.ManagedFolderMutationAuthority.OpenRename(
                    Guid.NewGuid(),
                    candidate.ID,
                    targetChildName);

                Assert.That(opened.IsSuccess, Is.True);

                using (SkinManagedFolderMutationAuthoritySession session = opened.Session!)
                {
                    session.PersistPreparedJournal();
                    SkinManagedFolderMutationJournalLoadResult loaded = store!.Load();
                    Assert.That(loaded.IsLoaded, Is.True);
                    preparedJournal = loaded.Journal;
                }

                Directory.CreateDirectory(LocalStorage.GetFullPath(targetManagedPath));
            });

            AddStep("construct manager with ambiguous production recovery", () =>
            {
                recoveringManager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler)
                {
                    ManagedFolderCapture = (_, _) => throw new AssertionException("recovery-frozen selection reached native capture")
                };
            });
            AddStep("request recovery-frozen candidate and scan complete absence", () =>
            {
                recoveringManager!.CurrentSkinInfo.Value = candidate;
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            Array.Empty<string>(),
                            Array.Empty<SkinManagedFolderDiscovery>())),
                    recoveringManager.ManagedFolderOperationCoordinator);
                scanResult = scanner.Scan();
            });
            AddStep("assert journal and record remain frozen", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            recoveringManager!.InitialManagedFolderMutationRecoveryResult.Status,
                            Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                        Assert.That(
                            recoveringManager.LastSelectionRejectionReason,
                            Is.EqualTo(SkinSelectionRejectionReason.MutationRecoveryPending));
                        Assert.That(recoveringManager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(recoveringManager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                        Assert.That(recoveringManager.ManagedFolderOperationCoordinator.IsPathFrozen(sourceManagedPath), Is.True);
                        Assert.That(recoveringManager.ManagedFolderOperationCoordinator.IsPathFrozen(targetManagedPath), Is.True);
                        Assert.That(scanResult!.IsSuccess, Is.True);
                        Assert.That(scanResult.Conflicts, Is.EqualTo(1));
                        Assert.That(candidate.PerformRead(info => info.DeletePending), Is.False);
                        Assert.That(store!.Load().IsLoaded, Is.True);
                    });
                }
                finally
                {
                    SkinManagedFolderMutationJournal rolledBack = preparedJournal!.WithRolledBack();
                    store!.Write(rolledBack);
                    store.Delete(rolledBack);
                }
            });
        }

        [Test]
        public void TestManagedFolderRenameShutdownCancelsAndJoinsWorker()
        {
            Live<SkinInfo> candidate = null!;
            SkinManagedFolderOperationCoordinator.Lease? heldScannerLease = null;
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            Task<SkinManagedFolderRenameOperationResult>? afterShutdown = null;

            AddStep("hold shared coordinator and start rename", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                heldScannerLease = manager.ManagedFolderOperationCoordinator.Enter();
                renameTask = manager.RenameManagedFolderAsync(candidate.ID, $"renamed-{Guid.NewGuid():N}");
            });
            AddUntilStep("wait for blocked rename worker", () => manager.IsManagedFolderRenameRunning);
            AddStep("shutdown and synchronously join rename", () =>
            {
                try
                {
                    manager.ShutdownManagedFolderRename();
                }
                finally
                {
                    heldScannerLease!.Dispose();
                }

                afterShutdown = manager.RenameManagedFolderAsync(candidate.ID, $"renamed-{Guid.NewGuid():N}");
            });
            AddStep("assert worker joined and restart rejected", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(renameTask!.IsCompleted, Is.True);
                    Assert.That(renameTask.GetAwaiter().GetResult().Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Cancelled));
                    Assert.That(manager.IsManagedFolderRenameRunning, Is.False);
                    Assert.That(afterShutdown!.IsCompleted, Is.True);
                    Assert.That(afterShutdown.GetAwaiter().GetResult().Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Shutdown));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedFolderStagedImportPublishesWithoutSelectionAndScannerDoesNotDuplicate()
        {
            Guid operationId = Guid.Empty;
            string stagedSource = string.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            string targetRoot = string.Empty;
            Live<SkinInfo> initialInfo = null!;
            Skin initialSkin = null!;
            Live<SkinInfo> imported = null!;
            Task<SkinManagedFolderStagedImportOperationResult>? importTask = null;
            SkinManagedFolderScanResult? scanResult = null;

            AddStep("create fixed provisional package", () =>
            {
                operationId = Guid.NewGuid();
                stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                targetChildName = $"imported-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                targetRoot = LocalStorage.GetFullPath(targetManagedPath);
                Directory.CreateDirectory(
                    LocalStorage.GetFullPath(
                        SkinFilesystemStorageResolver
                            .MANAGED_ROOT_DIRECTORY));
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);
                initialInfo = manager.CurrentSkinInfo.Value;
                initialSkin = manager.CurrentSkin.Value;
            });

            AddStep("start internal staged import", () =>
                importTask = manager.ImportManagedFolderAsync(
                    operationId,
                    targetChildName));
            AddUntilStep(
                "wait for staged import",
                () => importTask?.IsCompleted == true);
            AddStep("assert publication does not select", () =>
            {
                SkinManagedFolderStagedImportOperationResult result =
                    importTask!.GetAwaiter().GetResult();
                imported = manager.Query(info => info.ID == operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(imported, Is.Not.Null);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(initialInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(initialSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                    Assert.That(
                        imported.PerformRead(info => info.ID),
                        Is.EqualTo(operationId));
                    Assert.That(
                        imported.PerformRead(info => info.FilesystemStoragePath),
                        Is.EqualTo(targetManagedPath));
                    Assert.That(
                        imported.PerformRead(info => info.Name),
                        Is.EqualTo("managed folder product test"));
                    Assert.That(
                        imported.PerformRead(info => info.Creator),
                        Is.EqualTo("OMS tests"));
                    Assert.That(
                        imported.PerformRead(info => info.Hash),
                        Is.Not.Empty);
                    Assert.That(
                        imported.PerformRead(info => info.InstantiationInfo),
                        Is.EqualTo(
                            SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO));
                    Assert.That(
                        imported.PerformRead(info => info.Files.Count),
                        Is.Zero);
                    Assert.That(
                        imported.PerformRead(
                            info => info.IsExternalFilesystemStorage),
                        Is.False);
                    Assert.That(
                        imported.PerformRead(
                            info => info.FilesystemStorageAuthorityOwner),
                        Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage)
                            .Load()
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });

            AddStep("run production scanner after handoff", () =>
            {
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new WindowsSkinManagedFolderDiscoverySource(LocalStorage),
                    manager.ManagedFolderOperationCoordinator);
                scanResult = scanner.Scan();
            });
            AddStep("assert scanner reuses exact imported record", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(scanResult!.IsSuccess, Is.True);
                    Assert.That(scanResult.Added, Is.Zero);
                    Assert.That(
                        Realm.Run(realm => realm.All<SkinInfo>()
                            .Count(info => info.ID == operationId)),
                        Is.EqualTo(1));
                    Assert.That(
                        manager.Query(info => info.ID == operationId).ID,
                        Is.EqualTo(imported.ID));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });

            AddStep("explicitly select imported package", () =>
                manager.CurrentSkinInfo.Value = imported);
            AddUntilStep("wait for explicit imported selection", () =>
                manager.CurrentSkinInfo.Value.ID == operationId
                && manager.CurrentSkin.Value.SkinInfo.ID == operationId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("assert explicit selection loads final target capsule", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        manager.CurrentSkin.Value,
                        Is.TypeOf<BmsLegacySkin>());
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason,
                        Is.EqualTo(SkinSelectionRejectionReason.None));
                });
            });
        }

        [Test]
        public void TestUnrelatedPendingSelectionWaitsForStagedImportAndStillCommits()
        {
            Guid operationId = Guid.Empty;
            Live<SkinInfo> candidate = null!;
            Task<SkinManagedFolderStagedImportOperationResult>? importTask = null;
            var captureEntered = new ManualResetEventSlim();
            var releaseCapture = new ManualResetEventSlim();
            var importAuthorityOpened = new ManualResetEventSlim();
            var releaseImport = new ManualResetEventSlim();
            var selectionWaited = new ManualResetEventSlim();

            AddStep("create unrelated candidate and staged package", () =>
            {
                (_, candidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                operationId = Guid.NewGuid();
                string stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);

                var nativeCapture = manager.ManagedFolderCapture;
                manager.ManagedFolderCapture = (request, cancellationToken) =>
                {
                    captureEntered.Set();
                    Assert.That(
                        releaseCapture.Wait(
                            TimeSpan.FromSeconds(30),
                            cancellationToken),
                        Is.True);
                    return nativeCapture(request, cancellationToken);
                };
                manager.ManagedFolderStagedImportAuthorityOpened = () =>
                {
                    importAuthorityOpened.Set();
                    Assert.That(
                        releaseImport.Wait(TimeSpan.FromSeconds(30)),
                        Is.True);
                };
                manager.ManagedFolderSelectionWaitingForStagedImport = () =>
                {
                    selectionWaited.Set();
                    releaseImport.Set();
                    Assert.That(
                        importTask!.Wait(TimeSpan.FromSeconds(30)),
                        Is.True);
                };
            });

            AddStep("request unrelated pending selection", () =>
                manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep(
                "wait for pending capture",
                () => captureEntered.IsSet);
            AddStep("start staged import and hold its authority", () =>
                importTask = manager.ImportManagedFolderAsync(
                    operationId,
                    $"imported-{Guid.NewGuid():N}"));
            AddUntilStep(
                "wait for staged authority",
                () => importAuthorityOpened.IsSet);
            AddStep("release pending capture into mutation boundary", () =>
                releaseCapture.Set());
            AddUntilStep(
                "wait for selection to observe import reservation",
                () => selectionWaited.IsSet);
            AddUntilStep(
                "wait for import and unrelated selection",
                () => importTask?.IsCompleted == true
                      && manager.CurrentSkinInfo.Value.ID == candidate.ID
                      && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("assert import caused no selection churn", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        importTask!.GetAwaiter().GetResult().IsSuccess,
                        Is.True);
                    Assert.That(
                        manager.CurrentSkin.Value,
                        Is.TypeOf<BmsLegacySkin>());
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(
                        manager.LastSelectionRejectionReason,
                        Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(
                        manager.Query(info => info.ID == operationId),
                        Is.Not.Null);
                });

                captureEntered.Dispose();
                releaseCapture.Dispose();
                importAuthorityOpened.Dispose();
                releaseImport.Dispose();
                selectionWaited.Dispose();
            });
        }

        [Test]
        public void TestManagedFolderStagedImportWaitsForScannerSnapshotCommit()
        {
            Guid operationId = Guid.Empty;
            string stagedSource = string.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            Task<SkinManagedFolderScanResult>? scanTask = null;
            Task<SkinManagedFolderStagedImportOperationResult>? importTask = null;
            var scannerBeforeCommit = new ManualResetEventSlim();
            var releaseScanner = new ManualResetEventSlim();

            AddStep("create staged package and blocking scanner", () =>
            {
                operationId = Guid.NewGuid();
                stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(
                    LocalStorage.GetFullPath(
                        SkinFilesystemStorageResolver
                            .MANAGED_ROOT_DIRECTORY));
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);
                targetChildName = $"imported-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            Array.Empty<string>(),
                            Array.Empty<SkinManagedFolderDiscovery>())),
                    manager.ManagedFolderOperationCoordinator)
                {
                    ReconciliationBeforeCommit = () =>
                    {
                        scannerBeforeCommit.Set();
                        Assert.That(
                            releaseScanner.Wait(TimeSpan.FromSeconds(30)),
                            Is.True);
                    },
                };
                scanTask = Task.Run(() => scanner.Scan());
            });

            AddUntilStep(
                "wait for scanner commit boundary",
                () => scannerBeforeCommit.IsSet);
            AddStep("start import behind scanner lease", () =>
                importTask = manager.ImportManagedFolderAsync(
                    operationId,
                    targetChildName));
            AddUntilStep(
                "wait for blocked import worker",
                () => manager.IsManagedFolderStagedImportRunning);
            AddStep("assert import has not crossed scanner commit", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(importTask!.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(stagedSource), Is.True);
                    Assert.That(
                        Directory.Exists(
                            LocalStorage.GetFullPath(targetManagedPath)),
                        Is.False);
                    Assert.That(
                        manager.Query(info => info.ID == operationId),
                        Is.Null);
                });

                releaseScanner.Set();
            });
            AddUntilStep(
                "wait for scanner then import",
                () => scanTask?.IsCompleted == true
                      && importTask?.IsCompleted == true);
            AddStep("assert serialized import committed", () =>
            {
                SkinManagedFolderStagedImportOperationResult import =
                    importTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        scanTask!.GetAwaiter().GetResult().IsSuccess,
                        Is.True);
                    Assert.That(
                        import.IsSuccess,
                        Is.True,
                        $"{import};Authority={import.AuthorityRejectionReason}");
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(
                        Directory.Exists(
                            LocalStorage.GetFullPath(targetManagedPath)),
                        Is.True);
                    Assert.That(
                        manager.Query(info => info.ID == operationId),
                        Is.Not.Null);
                });

                scannerBeforeCommit.Dispose();
                releaseScanner.Dispose();
            });
        }

        [Test]
        public void TestAmbiguousStagedImportRestartFreezesSelectionAndScannerNegativeCleanup()
        {
            Guid operationId = Guid.Empty;
            string targetManagedPath = string.Empty;
            Live<SkinInfo> conflictingRecord = null!;
            SkinManager? recoveringManager = null;
            SkinManagedFolderMutationJournal? preparedJournal = null;
            SkinManagedFolderScanResult? scanResult = null;
            SkinManagedFolderMutationJournalStore? store = null;

            AddStep("persist prepared import then create foreign target", () =>
            {
                operationId = Guid.NewGuid();
                string targetChildName =
                    $"ambiguous-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                string stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(
                    LocalStorage.GetFullPath(
                        SkinFilesystemStorageResolver
                            .MANAGED_ROOT_DIRECTORY));
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);
                store = new SkinManagedFolderMutationJournalStore(
                    LocalStorage);

                SkinManagedFolderMutationAuthorityResult opened =
                    manager.ManagedFolderMutationAuthority.OpenStagedImport(
                        operationId,
                        targetChildName);
                Assert.That(opened.IsSuccess, Is.True);

                using (SkinManagedFolderMutationAuthoritySession session =
                       opened.Session!)
                {
                    session.PersistPreparedJournal();
                    SkinManagedFolderMutationJournalLoadResult loaded =
                        store.Load();
                    Assert.That(loaded.IsLoaded, Is.True);
                    preparedJournal = loaded.Journal;
                }

                string targetRoot =
                    LocalStorage.GetFullPath(targetManagedPath);
                Directory.CreateDirectory(targetRoot);
                createCompletePackage(targetRoot);
                var conflict = new SkinInfo(
                    "foreign target",
                    "OMS tests",
                    SkinManagedFolderFactory
                        .ALLOWED_INSTANTIATION_INFO)
                {
                    ID = operationId,
                    Hash = "foreign-revision",
                    FilesystemStoragePath = targetManagedPath,
                    FilesystemStorageAuthorityOwner =
                        SkinManagedFolderScanner.AUTHORITY_OWNER,
                };
                Realm.Write(realm => realm.Add(conflict));
                conflictingRecord =
                    manager.Query(info => info.ID == operationId);
                Assert.That(conflictingRecord, Is.Not.Null);
            });

            AddStep("construct manager with ambiguous import recovery", () =>
            {
                recoveringManager = new SkinManager(
                    LocalStorage,
                    Realm,
                    host,
                    Resources,
                    Audio,
                    Scheduler)
                {
                    ManagedFolderCapture = (_, _) =>
                        throw new AssertionException(
                            "recovery-frozen selection reached native capture"),
                };
            });
            AddStep("request frozen target and scan complete absence", () =>
            {
                recoveringManager!.CurrentSkinInfo.Value =
                    conflictingRecord;
                var scanner = new SkinManagedFolderScanner(
                    Realm,
                    new FixedManagedFolderDiscoverySource(
                        SkinManagedFolderDiscoverySnapshot.Complete(
                            Array.Empty<string>(),
                            Array.Empty<SkinManagedFolderDiscovery>())),
                    recoveringManager.ManagedFolderOperationCoordinator);
                scanResult = scanner.Scan();
            });
            AddStep("assert foreign target and record remain frozen", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            recoveringManager!
                                .InitialManagedFolderMutationRecoveryResult
                                .Status,
                            Is.EqualTo(
                                SkinManagedFolderMutationRecoveryStatus
                                    .Ambiguous));
                        Assert.That(
                            recoveringManager.LastSelectionRejectionReason,
                            Is.EqualTo(
                                SkinSelectionRejectionReason
                                    .MutationRecoveryPending));
                        Assert.That(
                            recoveringManager.CurrentSkinInfo.Value.ID,
                            Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(
                            recoveringManager.CurrentSkin.Value,
                            Is.TypeOf<OmsSkin>());
                        Assert.That(
                            recoveringManager
                                .ManagedFolderOperationCoordinator
                                .IsPathFrozen(targetManagedPath),
                            Is.True);
                        Assert.That(scanResult!.IsSuccess, Is.True);
                        Assert.That(scanResult.Conflicts, Is.EqualTo(1));
                        Assert.That(
                            conflictingRecord.PerformRead(
                                info => info.DeletePending),
                            Is.False);
                        Assert.That(store!.Load().IsLoaded, Is.True);
                    });
                }
                finally
                {
                    SkinManagedFolderMutationJournal rolledBack =
                        preparedJournal!.WithRolledBack();
                    store!.Write(rolledBack);
                    store.Delete(rolledBack);
                }
            });
        }

        [Test]
        public void TestManagedFolderMutationShutdownJoinsImportAndRejectsBothKinds()
        {
            Guid operationId = Guid.Empty;
            Live<SkinInfo> renameCandidate = null!;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            Task<SkinManagedFolderStagedImportOperationResult>? importTask = null;
            Task<SkinManagedFolderStagedImportOperationResult>? importAfterShutdown = null;
            Task<SkinManagedFolderRenameOperationResult>? renameAfterShutdown = null;

            AddStep("hold coordinator and start staged import", () =>
            {
                operationId = Guid.NewGuid();
                string stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);
                (_, renameCandidate) = createCandidate(
                    createCompletePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                renameCandidate.PerformWrite(
                    info => info.Hash = "registered-revision");
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                importTask = manager.ImportManagedFolderAsync(
                    operationId,
                    $"imported-{Guid.NewGuid():N}");
            });
            AddUntilStep(
                "wait for blocked staged import",
                () => manager.IsManagedFolderStagedImportRunning);
            AddStep("shutdown and synchronously join all mutations", () =>
            {
                try
                {
                    manager.ShutdownManagedFolderMutations();
                }
                finally
                {
                    heldLease!.Dispose();
                }

                importAfterShutdown = manager.ImportManagedFolderAsync(
                    Guid.NewGuid(),
                    $"imported-{Guid.NewGuid():N}");
                renameAfterShutdown = manager.RenameManagedFolderAsync(
                    renameCandidate.ID,
                    $"renamed-{Guid.NewGuid():N}");
            });
            AddStep("assert unified shutdown state", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(importTask!.IsCompleted, Is.True);
                    Assert.That(
                        importTask.GetAwaiter().GetResult().Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus.Cancelled));
                    Assert.That(
                        manager.IsManagedFolderStagedImportRunning,
                        Is.False);
                    Assert.That(
                        importAfterShutdown!.GetAwaiter().GetResult().Status,
                        Is.EqualTo(
                            SkinManagedFolderStagedImportOperationStatus.Shutdown));
                    Assert.That(
                        renameAfterShutdown!.GetAwaiter().GetResult().Status,
                        Is.EqualTo(
                            SkinManagedFolderRenameOperationStatus.Shutdown));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage)
                            .Load()
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestStagedImportRestartRecoveryRollsTargetForward()
        {
            Guid operationId = Guid.Empty;
            string targetChildName = string.Empty;
            string targetManagedPath = string.Empty;
            string stagedSource = string.Empty;
            SkinManager? recoveringManager = null;

            AddStep("leave durable filesystem-applied staged import", () =>
            {
                operationId = Guid.NewGuid();
                targetChildName = $"recovered-{Guid.NewGuid():N}";
                targetManagedPath = $"chartskin/{targetChildName}";
                stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(
                    LocalStorage.GetFullPath(
                        SkinFilesystemStorageResolver
                            .MANAGED_ROOT_DIRECTORY));
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);

                SkinManagedFolderMutationAuthorityResult opened =
                    manager.ManagedFolderMutationAuthority.OpenStagedImport(
                        operationId,
                        targetChildName);
                Assert.That(opened.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session =
                    opened.Session!;
                SkinManagedFolderDurableMutationReceipt receipt =
                    session.PersistPreparedJournal();
                session.ApplyCapturedStagedImportWithDurableReceipt(receipt);

                SkinManagedFolderMutationJournalLoadResult journal =
                    new SkinManagedFolderMutationJournalStore(LocalStorage).Load();
                Assert.Multiple(() =>
                {
                    Assert.That(journal.IsLoaded, Is.True);
                    Assert.That(
                        journal.Journal!.Phase,
                        Is.EqualTo(
                            SkinManagedFolderMutationPhase.FilesystemApplied));
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(
                        Directory.Exists(
                            LocalStorage.GetFullPath(targetManagedPath)),
                        Is.True);
                });
            });

            AddStep("construct manager and run production startup recovery", () =>
                recoveringManager = new SkinManager(
                    LocalStorage,
                    Realm,
                    host,
                    Resources,
                    Audio,
                    Scheduler));
            AddStep("assert target-forward recovery published once", () =>
            {
                Live<SkinInfo> recovered =
                    recoveringManager!.Query(info => info.ID == operationId);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        recoveringManager.InitialManagedFolderMutationRecoveryResult
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredForward));
                    Assert.That(recovered, Is.Not.Null);
                    Assert.That(
                        recovered.PerformRead(
                            info => info.FilesystemStoragePath),
                        Is.EqualTo(targetManagedPath));
                    Assert.That(
                        Realm.Run(realm => realm.All<SkinInfo>()
                            .Count(info => info.ID == operationId)),
                        Is.EqualTo(1));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage)
                            .Load()
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestStagedImportRestartRecoveryCleansPreparedSource()
        {
            Guid operationId = Guid.Empty;
            string stagedSource = string.Empty;
            SkinManager? recoveringManager = null;

            AddStep("leave durable prepared staged import", () =>
            {
                operationId = Guid.NewGuid();
                stagedSource = LocalStorage.GetFullPath(
                    $"skin-mutation-staging/{operationId:N}");
                Directory.CreateDirectory(
                    LocalStorage.GetFullPath(
                        SkinFilesystemStorageResolver
                            .MANAGED_ROOT_DIRECTORY));
                Directory.CreateDirectory(stagedSource);
                createCompletePackage(stagedSource);

                SkinManagedFolderMutationAuthorityResult opened =
                    manager.ManagedFolderMutationAuthority.OpenStagedImport(
                        operationId,
                        $"rolled-back-{Guid.NewGuid():N}");
                Assert.That(opened.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session =
                    opened.Session!;
                session.PersistPreparedJournal();
                Assert.That(
                    new SkinManagedFolderMutationJournalStore(LocalStorage)
                        .Load()
                        .Journal!
                        .Phase,
                    Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
            });

            AddStep("construct manager and run prepared rollback recovery", () =>
                recoveringManager = new SkinManager(
                    LocalStorage,
                    Realm,
                    host,
                    Resources,
                    Audio,
                    Scheduler));
            AddStep("assert only provisional source was removed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        recoveringManager!.InitialManagedFolderMutationRecoveryResult
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationRecoveryStatus
                                .RecoveredRollback));
                    Assert.That(Directory.Exists(stagedSource), Is.False);
                    Assert.That(
                        recoveringManager.Query(info => info.ID == operationId),
                        Is.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage)
                            .Load()
                            .Status,
                        Is.EqualTo(
                            SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestFilesystemMutationFreezeCannotBeBypassedThroughBaseOrInterfaces()
        {
            Live<SkinInfo> candidate = null!;
            SkinInfo spoof = null!;
            string packageRoot = string.Empty;
            FolderInventorySnapshot sourceBefore = default;
            Live<SkinInfo> selectionBefore = null!;
            Skin ownerBefore = null!;
            SkinCurrentRevision revisionBefore = null!;

            AddStep("create managed folder record", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                sourceBefore = captureFolderInventory(packageRoot);
                selectionBefore = manager.CurrentSkinInfo.Value;
                ownerBefore = manager.CurrentSkin.Value;
                revisionBefore = manager.CurrentRevision;
            });

            AddStep("exercise inherited mutation surfaces", () => candidate.PerformRead(info =>
            {
                var baseManager = (ModelManager<SkinInfo>)manager;
                var modelManager = (IModelManager<SkinInfo>)manager;
                var fileManager = (IModelFileManager<SkinInfo, RealmNamedFileUsage>)manager;
                var usage = new RealmNamedFileUsage(new RealmFile { Hash = "unused" }, "skin.ini");

                Assert.Multiple(() =>
                {
                    Assert.That(baseManager.Delete(info), Is.False);
                    Assert.That(modelManager.Delete(info), Is.False);
                    Assert.That(() => fileManager.AddFile(info, new MemoryStream(new byte[] { 1 }), "added.bin"), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => fileManager.ReplaceFile(info, usage, new MemoryStream(new byte[] { 2 })), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => fileManager.DeleteFile(info, usage), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => manager.AddFile(info, new MemoryStream(new byte[] { 3 }), "added.bin", info.Realm!), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => manager.DeleteFile(info, usage, info.Realm!), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(
                        () => manager.ImportAsUpdate(
                            new ProgressNotification(),
                            new ImportTask(new MemoryStream(new byte[] { 4 }), "update.osk"),
                            info),
                        Throws.TypeOf<InvalidOperationException>());
                });

                Assert.That(info.DeletePending, Is.False);
                Assert.That(info.Files, Is.Empty);
            }));

            AddStep("create same-id non-folder spoof", () => spoof = new SkinInfo
            {
                ID = candidate.ID,
                Name = "spoof",
            });

            AddStep("reject spoofed delete and external edit", () =>
            {
                var baseManager = (ModelManager<SkinInfo>)manager;
                var modelManager = (IModelManager<SkinInfo>)manager;
                var fileManager = (IModelFileManager<SkinInfo, RealmNamedFileUsage>)manager;
                var usage = new RealmNamedFileUsage(new RealmFile { Hash = "unused" }, "skin.ini");
                var authoritativeRealm = candidate.PerformRead(info => info.Realm!);

                Assert.Multiple(() =>
                {
                    Assert.That(baseManager.Delete(spoof), Is.False);
                    Assert.That(modelManager.Delete(spoof), Is.False);
                    Assert.That(() => manager.BeginExternalEditing(spoof), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => fileManager.AddFile(spoof, new MemoryStream(new byte[] { 6 }), "added.bin"), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => fileManager.ReplaceFile(spoof, usage, new MemoryStream(new byte[] { 7 })), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => fileManager.DeleteFile(spoof, usage), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => manager.AddFile(spoof, new MemoryStream(new byte[] { 8 }), "added.bin", authoritativeRealm), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => manager.ReplaceFile(spoof, usage, new MemoryStream(new byte[] { 9 }), authoritativeRealm), Throws.TypeOf<InvalidOperationException>());
                    Assert.That(() => manager.DeleteFile(spoof, usage, authoritativeRealm), Throws.TypeOf<InvalidOperationException>());
                });
            });

            AddStep("reject spoofed undelete", () =>
            {
                candidate.PerformWrite(info => info.DeletePending = true);
                ((IModelManager<SkinInfo>)manager).Undelete(spoof);
                Assert.That(candidate.PerformRead(info => info.DeletePending), Is.True);
                candidate.PerformWrite(info => info.DeletePending = false);
            });

            AddStep("reject spoofed package update", () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(() => manager.ImportAsUpdate(
                    new ProgressNotification(),
                    new ImportTask(new MemoryStream(new byte[] { 5 }), "spoof-update.osk"),
                    spoof));

                Assert.That(exception!.Message, Is.EqualTo(SkinAuthoringAvailability.UPDATE_IMPORT_DISABLED_DIAGNOSTIC));
            });
            AddStep("assert authoritative folder record is unchanged", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionBefore));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerBefore));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionBefore));
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceBefore));
                });

                candidate.PerformRead(info => Assert.Multiple(() =>
                {
                    Assert.That(info.Name, Is.EqualTo("managed folder"));
                    Assert.That(info.Files, Is.Empty);
                    Assert.That(info.DeletePending, Is.False);
                    Assert.That(info.FilesystemStoragePath, Does.StartWith("chartskin/"));
                    Assert.That(info.FilesystemStorageAuthorityOwner, Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                }));
            });
        }

        [Test]
        public void TestExternalFolderWorkspaceRegistrationIsNonSelectingConcurrentSafeAndIdempotent()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            Task<bool>? firstRegistration = null;
            Task<bool>? concurrentRegistration = null;
            Task<bool>? idempotentRegistration = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;

            AddStep("create external package and start blocked registration", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                firstRegistration = manager.RegisterExternalFolderAsync(packageRoot);
                concurrentRegistration = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddStep("observe concurrent rejection and release registration", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(firstRegistration!.IsCompleted, Is.False);
                        Assert.That(concurrentRegistration!.IsCompleted, Is.True);
                        Assert.That(concurrentRegistration.GetAwaiter().GetResult(), Is.False);
                        Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    });
                }
                finally
                {
                    heldLease!.Dispose();
                    heldLease = null;
                }
            });
            AddUntilStep("wait for first registration", () => firstRegistration?.IsCompleted == true);
            AddStep("retry exact committed path", () =>
            {
                Assert.That(firstRegistration!.GetAwaiter().GetResult(), Is.True);
                idempotentRegistration = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for idempotent registration", () => idempotentRegistration?.IsCompleted == true);
            AddStep("query workspace and dropdown consumers", () =>
            {
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for workspace records", () => workspaceTask?.IsCompleted == true);
            AddUntilStep("wait for dropdown records", () => dropdownTask?.IsCompleted == true);
            AddStep("assert one visible immutable external record without selection", () =>
            {
                FolderSkinWorkspaceRecord row = workspaceTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External);
                Live<SkinInfo> dropdownRecord = dropdownTask!.GetAwaiter().GetResult()
                                                             .Single(record => record.ID == row.RecordId);
                int committedExternalRecords = Realm.Run(realm => realm.All<SkinInfo>()
                    .Count(record => record.IsExternalFilesystemStorage));

                Assert.Multiple(() =>
                {
                    Assert.That(idempotentRegistration!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(committedExternalRecords, Is.EqualTo(1));
                    Assert.That(row.RecordId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(row.DisplayLabel, Is.EqualTo("managed folder product test"));
                    Assert.That(row.CanUnregister, Is.True);
                    Assert.That(dropdownRecord.PerformRead(record => record.Name), Is.EqualTo(row.DisplayLabel));
                    Assert.That(dropdownRecord.PerformRead(record => record.IsExternalFilesystemStorage), Is.True);
                    Assert.That(dropdownRecord.PerformRead(record => record.FilesystemStorageAuthorityOwner),
                        Is.EqualTo(SkinExternalFolderRegistry.AUTHORITY_OWNER));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                });
            });
        }

        [Test]
        public void TestExternalFolderDropdownExcludesInexactRecordsButKeepsStaleRegisteredRecordVisible()
        {
            string packageRoot = string.Empty;
            Guid registeredId = Guid.Empty;
            Guid foreignOwnerId = Guid.NewGuid();
            Guid missingOwnerId = Guid.NewGuid();
            Guid filefulOwnerId = Guid.NewGuid();
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? staleDropdownTask = null;
            Task<IList<Live<SkinInfo>>>? filteredDropdownTask = null;

            AddStep("register exact external dropdown record", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for exact external registration", () => registrationTask?.IsCompleted == true);
            AddStep("remove source and query dropdown", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                Directory.Delete(packageRoot, recursive: true);
                staleDropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for stale workspace record", () => workspaceTask?.IsCompleted == true);
            AddUntilStep("wait for stale dropdown record", () => staleDropdownTask?.IsCompleted == true);
            AddStep("select still-visible stale external record", () =>
            {
                registeredId = workspaceTask!.GetAwaiter().GetResult()
                                             .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                             .RecordId;
                Live<SkinInfo> staleRecord = staleDropdownTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.ID == registeredId);
                manager.CurrentSkinInfo.Value = staleRecord;
            });
            AddUntilStep("wait for stale source rejection", () =>
                manager.LastSelectionRejectionReason != SkinSelectionRejectionReason.None);
            AddStep("add structurally inexact external records", () =>
            {
                Realm.Write(realm =>
                {
                    realm.Add(new SkinInfo("foreign owner", "OMS tests", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                    {
                        ID = foreignOwnerId,
                        Hash = "foreign-owner-hash",
                        FilesystemStoragePath = Path.Combine(Path.GetTempPath(), $"foreign-owner-{Guid.NewGuid():N}"),
                        IsExternalFilesystemStorage = true,
                        FilesystemStorageAuthorityOwner = "foreign.owner",
                    });
                    realm.Add(new SkinInfo("missing owner", "OMS tests", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                    {
                        ID = missingOwnerId,
                        Hash = "missing-owner-hash",
                        FilesystemStoragePath = Path.Combine(Path.GetTempPath(), $"missing-owner-{Guid.NewGuid():N}"),
                        IsExternalFilesystemStorage = true,
                        FilesystemStorageAuthorityOwner = null,
                    });

                    var fileful = new SkinInfo("fileful owner token", "OMS tests", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                    {
                        ID = filefulOwnerId,
                        Hash = "fileful-owner-hash",
                        FilesystemStoragePath = Path.Combine(Path.GetTempPath(), $"fileful-owner-{Guid.NewGuid():N}"),
                        IsExternalFilesystemStorage = true,
                        FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
                    };
                    fileful.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = $"file-{Guid.NewGuid():N}" }, "payload.bin"));
                    realm.Add(fileful);
                });

                filteredDropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for structurally filtered dropdown", () => filteredDropdownTask?.IsCompleted == true);
            AddStep("assert only exact stale record remains visible", () =>
            {
                Guid[] visibleIds = filteredDropdownTask!.GetAwaiter().GetResult().Select(record => record.ID).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(visibleIds, Does.Contain(registeredId));
                    Assert.That(visibleIds, Does.Not.Contain(foreignOwnerId));
                    Assert.That(visibleIds, Does.Not.Contain(missingOwnerId));
                    Assert.That(visibleIds, Does.Not.Contain(filefulOwnerId));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(manager.LastSelectionRejectionReason, Is.Not.EqualTo(SkinSelectionRejectionReason.None));
                });
            });
        }

        [Test]
        public void TestUnresolvedManagedFolderJournalBlocksExternalUnregisterInWorkspaceAndCommit()
        {
            string externalRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            Live<SkinInfo> managedCandidate = null!;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? unregisterTask = null;
            SkinManagedFolderMutationJournal? unresolvedJournal = null;
            SkinManagedFolderMutationJournalStore? journalStore = null;

            AddStep("register external before recovery freeze", () =>
            {
                externalRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for external before recovery freeze", () => registrationTask?.IsCompleted == true);
            AddStep("persist unresolved managed-folder journal", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                externalRecordId = Realm.Run(realm => realm.All<SkinInfo>()
                                                           .Single(record => record.IsExternalFilesystemStorage)
                                                           .ID);
                (_, managedCandidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                journalStore = new SkinManagedFolderMutationJournalStore(LocalStorage);
                string managedPath = managedCandidate.PerformRead(record => record.FilesystemStoragePath!);
                unresolvedJournal = SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    managedCandidate.ID,
                    new SkinManagedFolderPhysicalIdentity(501, 502, 503),
                    managedPath,
                    new SkinManagedFolderPhysicalIdentity(501, 602, 603),
                    managedCandidate.PerformRead(SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint),
                    SkinManagedFolderDeleteManifest.Create(new[] { new string('d', 64) }));
                journalStore!.Write(unresolvedJournal);

                manager.ShutdownManagedFolderMutations();
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                manager.SourceChanged += () => sourceChangedCount++;
            });
            AddStep("query recovery-frozen workspace", () =>
            {
                Assert.That(
                    manager.InitialManagedFolderMutationRecoveryResult.Status,
                    Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(manager.ManagedFolderOperationCoordinator.IsMutationBlocked, Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for recovery-frozen workspace", () => workspaceTask?.IsCompleted == true);
            AddStep("attempt unregister through manager commit surface", () =>
            {
                FolderSkinWorkspaceRecord external = workspaceTask!.GetAwaiter().GetResult()
                                                                   .Single(record => record.RecordId == externalRecordId);
                Assert.That(external.CanUnregister, Is.False);
                unregisterTask = manager.UnregisterExternalFolderAsync(externalRecordId);
            });
            AddUntilStep("wait for blocked external unregister", () => unregisterTask?.IsCompleted == true);
            AddStep("assert unresolved journal retained external registration", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(unregisterTask!.GetAwaiter().GetResult(), Is.False);
                        Assert.That(manager.Query(record => record.ID == externalRecordId), Is.Not.Null);
                        Assert.That(Directory.Exists(externalRoot), Is.True);
                        Assert.That(journalStore!.Load().IsLoaded, Is.True);
                    });
                }
                finally
                {
                    SkinManagedFolderMutationJournal rolledBack = unresolvedJournal!.WithRolledBack();
                    journalStore!.Write(rolledBack);
                    journalStore.Delete(rolledBack);
                }
            });
        }

        [Test]
        public void TestFolderWorkspaceManagerRejectsUntrimmedTargetNames()
        {
            const string target_name = "  authority sensitive target  ";
            string managedPath = string.Empty;
            string packageRoot = string.Empty;
            Guid externalId = Guid.Empty;
            Live<SkinInfo> managed = null!;
            string[] managedFolderRecordsBefore = Array.Empty<string>();
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? importTask = null;

            AddStep("create managed rename target", () =>
            {
                (_, managed) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                managed.PerformWrite(info =>
                {
                    info.Hash = "untrimmed-rename-source";
                    managedPath = info.FilesystemStoragePath!;
                });
                renameTask = manager.RenameManagedFolderAsync(managed.ID, target_name);
            });
            AddUntilStep("wait for untrimmed rename rejection", () => renameTask?.IsCompleted == true);
            AddStep("register external import source", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(renameTask!.GetAwaiter().GetResult().IsSuccess, Is.False);
                    Assert.That(managed.PerformRead(info => info.FilesystemStoragePath), Is.EqualTo(managedPath));
                    Assert.That(Directory.Exists(LocalStorage.GetFullPath(managedPath)), Is.True);
                });

                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external import source", () => registrationTask?.IsCompleted == true);
            AddStep("query external import record", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for external import record", () => workspaceTask?.IsCompleted == true);
            AddStep("submit exact untrimmed import name", () =>
            {
                externalId = workspaceTask!.GetAwaiter().GetResult()
                                           .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                           .RecordId;
                managedFolderRecordsBefore = Realm.Run(realm => realm.All<SkinInfo>()
                    .Where(record => !record.IsExternalFilesystemStorage
                                     && !string.IsNullOrEmpty(record.FilesystemStoragePath))
                    .AsEnumerable()
                    .Select(record => $"{record.ID:N}|{record.FilesystemStoragePath}")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());
                importTask = manager.ImportManagedCopyAsync(externalId, target_name);
            });
            AddStep("assert untrimmed import rejected before mutation", () =>
            {
                string[] managedFolderRecordsAfter = Realm.Run(realm => realm.All<SkinInfo>()
                    .Where(record => !record.IsExternalFilesystemStorage
                                     && !string.IsNullOrEmpty(record.FilesystemStoragePath))
                    .AsEnumerable()
                    .Select(record => $"{record.ID:N}|{record.FilesystemStoragePath}")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());

                Assert.Multiple(() =>
                {
                    Assert.That(importTask!.IsCompleted, Is.True);
                    Assert.That(importTask.GetAwaiter().GetResult(), Is.False);
                    Assert.That(managedFolderRecordsAfter, Is.EqualTo(managedFolderRecordsBefore));
                    Assert.That(Directory.Exists(LocalStorage.GetFullPath($"chartskin/{target_name}")), Is.False);
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                });
            });
        }

        [Test]
        public void TestExternalFolderWorkspaceExplicitSelectionIsFreshAndImplicitSelectorsExcludeIt()
        {
            string packageRoot = string.Empty;
            Guid recordId = Guid.Empty;
            string displayLabel = string.Empty;
            Guid implicitRequestId = Guid.Empty;
            string registrationHash = string.Empty;
            Live<SkinInfo> dropdownRecord = null!;
            Skin? firstSelection = null;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;

            AddStep("create and register external package", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external registration", () => registrationTask?.IsCompleted == true);
            AddStep("query path-free workspace row", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for path-free workspace row", () => workspaceTask?.IsCompleted == true);
            AddStep("query dropdown", () =>
            {
                FolderSkinWorkspaceRecord row = workspaceTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External);
                recordId = row.RecordId;
                displayLabel = row.DisplayLabel;
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("select visible external dropdown item", () =>
            {
                dropdownRecord = dropdownTask!.GetAwaiter().GetResult().Single(record => record.ID == recordId);
                Assert.That(dropdownRecord.PerformRead(record => record.Name), Is.EqualTo(displayLabel));
                registrationHash = dropdownRecord.PerformRead(record => record.Hash);
                manager.CurrentSkinInfo.Value = dropdownRecord;
            });
            AddUntilStep("wait for first external selection", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("switch away and mutate source", () =>
            {
                firstSelection = manager.CurrentSkin.Value;
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;
                string skinIni = File.ReadAllText(Path.Combine(packageRoot, "skin.ini"));
                File.WriteAllText(
                    Path.Combine(packageRoot, "skin.ini"),
                    skinIni.Replace("Name: managed folder product test", "Name: refreshed external observation")
                           .Replace("Author: OMS tests", "Author: refreshed author")
                           .Replace("LongNoteBodyWidth: 0.4", "LongNoteBodyWidth: 0.7"));
            });
            AddStep("reselect external dropdown item", () => manager.CurrentSkinInfo.Value = dropdownRecord);
            AddUntilStep("wait for fresh external selection", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId
                && !ReferenceEquals(manager.CurrentSkin.Value, firstSelection));
            AddStep("assert reselection captured fresh source", () =>
            {
                Drawable? body = resolve(new BmsSkinTransformer(manager.CurrentSkin.Value), BmsNoteSkinElements.LongNoteBody);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<BmsLegacySkin>());
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(firstSelection));
                    Assert.That(body, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(body!.Width, Is.EqualTo(0.7f).Within(0.0001f));
                    Assert.That(dropdownRecord.PerformRead(record => record.Name), Is.EqualTo("refreshed external observation"));
                    Assert.That(dropdownRecord.PerformRead(record => record.Creator), Is.EqualTo("refreshed author"));
                    Assert.That(dropdownRecord.PerformRead(record => record.Hash), Is.Not.Empty);
                    Assert.That(dropdownRecord.PerformRead(record => record.Hash), Is.Not.EqualTo(registrationHash));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.Value.Name, Is.EqualTo("refreshed external observation"));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.Value.Creator, Is.EqualTo("refreshed author"));
                    Assert.That(
                        manager.CurrentSkin.Value.SkinInfo.Value.Hash,
                        Is.EqualTo(dropdownRecord.PerformRead(record => record.Hash)));
                });
            });
            AddStep("random excludes external", () =>
            {
                implicitRequestId = Guid.Empty;
                manager.SelectionRequestBeforeCommitLock = target => implicitRequestId = target.ID;
                manager.SelectRandomSkin();
                Assert.Multiple(() =>
                {
                    Assert.That(implicitRequestId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(implicitRequestId, Is.Not.EqualTo(recordId));
                });
            });
            AddStep("explicitly reselect before next", () => manager.CurrentSkinInfo.Value = dropdownRecord);
            AddUntilStep("wait for external before next", () => manager.CurrentSkinInfo.Value.ID == recordId && manager.CurrentSkin.Value.SkinInfo.ID == recordId);
            AddStep("next excludes external", () =>
            {
                implicitRequestId = Guid.Empty;
                manager.SelectNextSkin();
                Assert.Multiple(() =>
                {
                    Assert.That(implicitRequestId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(implicitRequestId, Is.Not.EqualTo(recordId));
                });
            });
            AddStep("explicitly reselect before previous", () => manager.CurrentSkinInfo.Value = dropdownRecord);
            AddUntilStep("wait for external before previous", () => manager.CurrentSkinInfo.Value.ID == recordId && manager.CurrentSkin.Value.SkinInfo.ID == recordId);
            AddStep("previous excludes external", () =>
            {
                implicitRequestId = Guid.Empty;
                manager.SelectPreviousSkin();
                Assert.Multiple(() =>
                {
                    Assert.That(implicitRequestId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(implicitRequestId, Is.Not.EqualTo(recordId));
                });
                manager.SelectionRequestBeforeCommitLock = _ => { };
            });
        }

        [Test]
        public void TestExternalFolderSelectionHoldsTargetPhysicalRootUntilFinalCommit()
        {
            string packageRoot = string.Empty;
            string replacementRoot = string.Empty;
            Guid recordId = Guid.Empty;
            Live<SkinInfo> dropdownRecord = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Action? deferredCompletion = null;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;

            AddStep("register target external package", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for target registration", () => registrationTask?.IsCompleted == true);
            AddStep("defer target selection completion", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for target dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("request held target selection", () =>
            {
                dropdownRecord = dropdownTask!.GetAwaiter().GetResult()
                                              .Single(record => record.PerformRead(info => info.IsExternalFilesystemStorage));
                recordId = dropdownRecord.ID;
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCompletionSchedule = completion => deferredCompletion = completion;
                manager.CurrentSkinInfo.Value = dropdownRecord;
            });
            AddUntilStep("wait for held target completion", () => deferredCompletion != null);
            AddStep("prove target root replacement is denied while proof is held", () =>
            {
                replacementRoot = $"{packageRoot}-replacement";
                Exception? replacementFailure = null;

                try
                {
                    Directory.Move(packageRoot, replacementRoot);
                }
                catch (Exception exception)
                {
                    replacementFailure = exception;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(replacementFailure, Is.TypeOf<IOException>().Or.TypeOf<UnauthorizedAccessException>());
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Directory.Exists(replacementRoot), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                });
            });
            AddStep("run held target completion", () => deferredCompletion!());
            AddUntilStep("wait for target commit", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId);
            AddStep("restore target completion scheduler", () =>
                manager.ManagedFolderCompletionSchedule = completion => Scheduler.Add(completion));
        }

        [Test]
        public void TestExternalFolderSelectionHoldsEveryRegistryPhysicalRootUntilFinalCommit()
        {
            string targetRoot = string.Empty;
            string unrelatedRoot = string.Empty;
            string unrelatedReplacementRoot = string.Empty;
            Guid targetId = Guid.Empty;
            Live<SkinInfo> targetRecord = null!;
            Action? deferredCompletion = null;
            Task<bool>? targetRegistrationTask = null;
            Task<bool>? unrelatedRegistrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;

            AddStep("register target and unrelated external packages", () =>
            {
                targetRoot = createExternalPackage(createCompletePackage);
                unrelatedRoot = createExternalPackage(createCompletePackage);
                targetRegistrationTask = manager.RegisterExternalFolderAsync(targetRoot);
            });
            AddUntilStep("wait for target external registration", () => targetRegistrationTask?.IsCompleted == true);
            AddStep("register unrelated external package", () =>
            {
                Assert.That(targetRegistrationTask!.GetAwaiter().GetResult(), Is.True);
                unrelatedRegistrationTask = manager.RegisterExternalFolderAsync(unrelatedRoot);
            });
            AddUntilStep("wait for unrelated external registration", () => unrelatedRegistrationTask?.IsCompleted == true);
            AddStep("query both external dropdown records", () =>
            {
                Assert.That(unrelatedRegistrationTask!.GetAwaiter().GetResult(), Is.True);
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for both external dropdown records", () => dropdownTask?.IsCompleted == true);
            AddStep("defer target selection completion", () =>
            {
                targetRecord = dropdownTask!.GetAwaiter().GetResult()
                                            .Single(record => record.PerformRead(info =>
                                                string.Equals(info.FilesystemStoragePath, targetRoot, StringComparison.OrdinalIgnoreCase)));
                targetId = targetRecord.ID;
                manager.ManagedFolderCompletionSchedule = completion => deferredCompletion = completion;
                manager.CurrentSkinInfo.Value = targetRecord;
            });
            AddUntilStep("wait for exact registry completion", () => deferredCompletion != null);
            AddStep("prove unrelated root replacement is denied by exact-set proof", () =>
            {
                unrelatedReplacementRoot = $"{unrelatedRoot}-replacement";
                Exception? replacementFailure = null;

                try
                {
                    Directory.Move(unrelatedRoot, unrelatedReplacementRoot);
                }
                catch (Exception exception)
                {
                    replacementFailure = exception;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(replacementFailure, Is.TypeOf<IOException>().Or.TypeOf<UnauthorizedAccessException>());
                    Assert.That(Directory.Exists(unrelatedRoot), Is.True);
                    Assert.That(Directory.Exists(unrelatedReplacementRoot), Is.False);
                });
            });
            AddStep("run exact registry completion", () => deferredCompletion!());
            AddUntilStep("wait for exact registry target commit", () =>
                manager.CurrentSkinInfo.Value.ID == targetId
                && manager.CurrentSkin.Value.SkinInfo.ID == targetId);
            AddStep("restore exact registry completion scheduler", () =>
                manager.ManagedFolderCompletionSchedule = completion => Scheduler.Add(completion));
        }

        [Test]
        public void TestExternalFolderSelectionRejectsACompletedGenericMutationBeforeFinalCommit()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> targetRecord = null!;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Action? deferredCompletion = null;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;

            AddStep("register external epoch candidate", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external epoch registration", () => registrationTask?.IsCompleted == true);
            AddStep("query external epoch candidate", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for external epoch dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("defer external epoch completion", () =>
            {
                targetRecord = dropdownTask!.GetAwaiter().GetResult()
                                            .Single(record => record.PerformRead(info => info.IsExternalFilesystemStorage));
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                manager.ManagedFolderCompletionSchedule = completion => deferredCompletion = completion;
                manager.CurrentSkinInfo.Value = targetRecord;
            });
            AddUntilStep("wait for external epoch completion", () => deferredCompletion != null);
            AddStep("cross completed generic mutation reservation", () =>
            {
                using (manager.ManagedFolderOperationCoordinator.EnterMutation())
                {
                }
            });
            AddStep("run stale external epoch completion", () => deferredCompletion!());
            AddStep("assert generic mutation epoch rejects external commit", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(manager.LastSelectionRejectionReason,
                        Is.EqualTo(SkinSelectionRejectionReason.CapturedCandidateChanged));
                });
                manager.ManagedFolderCompletionSchedule = completion => Scheduler.Add(completion);
            });
        }

        [Test]
        public void TestExternalFolderSelectionLatestRequestWinsWhileCaptureAuthorityIsHeld()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> targetRecord = null!;
            Live<SkinInfo> fallback = null!;
            long generationBeforeFallback = 0;
            FieldInfo? generationField = null;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            using var captureOpened = new ManualResetEventSlim();
            using var releaseCapture = new ManualResetEventSlim();

            AddStep("register latest-wins external candidate", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for latest-wins registration", () => registrationTask?.IsCompleted == true);
            AddStep("query latest-wins external candidate", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for latest-wins dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("block external capture after exact authority opens", () =>
            {
                targetRecord = dropdownTask!.GetAwaiter().GetResult()
                                            .Single(record => record.PerformRead(info => info.IsExternalFilesystemStorage));
                fallback = manager.DefaultClassicSkin.SkinInfo;
                generationField = typeof(SkinManager).GetField(
                    "selectionGeneration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(generationField, Is.Not.Null);
                manager.ExternalFolderSelectionCaptureAuthorityOpened = () =>
                {
                    captureOpened.Set();
                    if (!releaseCapture.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Timed out waiting to release external capture authority.");
                };
                manager.CurrentSkinInfo.Value = targetRecord;
            });
            AddUntilStep("wait for external capture authority", () => captureOpened.IsSet);
            AddStep("request distinct Realm fallback while external capture is held", () =>
            {
                generationBeforeFallback = (long)generationField!.GetValue(manager)!;
                manager.CurrentSkinInfo.Value = fallback;
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(fallback.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(fallback.ID));
                    Assert.That((long)generationField.GetValue(manager)!, Is.GreaterThan(generationBeforeFallback));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                });
            });
            AddStep("release stale external capture", () => releaseCapture.Set());
            AddUntilStep("wait for latest Realm request to remain committed", () =>
                manager.CurrentSkinInfo.Value.ID == fallback.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == fallback.ID
                && (long)generationField!.GetValue(manager)! > generationBeforeFallback);
            AddStep("assert latest request was accepted and restore hook", () =>
            {
                Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                manager.ExternalFolderSelectionCaptureAuthorityOpened = () => { };
            });
        }

        [Test]
        public void TestExternalFolderSelectionShutdownCancelsJoinsAndReleasesCapturedAuthority()
        {
            string packageRoot = string.Empty;
            string movedRoot = string.Empty;
            Live<SkinInfo> targetRecord = null!;
            FieldInfo? generationField = null;
            long generationBeforeShutdown = 0;
            Task? shutdownTask = null;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            using var captureOpened = new ManualResetEventSlim();
            using var releaseCapture = new ManualResetEventSlim();

            AddStep("register shutdown external candidate", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for shutdown external registration", () => registrationTask?.IsCompleted == true);
            AddStep("query shutdown external candidate", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for shutdown external dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("block external physical authority capture", () =>
            {
                targetRecord = dropdownTask!.GetAwaiter().GetResult()
                                            .Single(record => record.PerformRead(info => info.IsExternalFilesystemStorage));
                generationField = typeof(SkinManager).GetField(
                    "selectionGeneration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(generationField, Is.Not.Null);
                manager.ExternalFolderSelectionCaptureAuthorityOpened = () =>
                {
                    captureOpened.Set();
                    if (!releaseCapture.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Timed out waiting to release external capture authority during shutdown.");
                };
                manager.CurrentSkinInfo.Value = targetRecord;
            });
            AddUntilStep("wait for shutdown physical authority", () => captureOpened.IsSet);
            AddStep("start selection shutdown while authority is held", () =>
            {
                generationBeforeShutdown = (long)generationField!.GetValue(manager)!;
                shutdownTask = Task.Run(manager.ShutdownManagedFolderMutations);
            });
            AddUntilStep("wait for shutdown cancellation boundary", () =>
                (long)generationField!.GetValue(manager)! > generationBeforeShutdown);
            AddStep("release cancelled physical capture", () => releaseCapture.Set());
            AddUntilStep("wait for selection shutdown join", () => shutdownTask?.IsCompleted == true);
            AddStep("assert shutdown reclaimed physical owners", () =>
            {
                shutdownTask!.GetAwaiter().GetResult();
                movedRoot = packageRoot + ".moved";
                Directory.Move(packageRoot, movedRoot);
                Directory.Move(movedRoot, packageRoot);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                });
                manager.ExternalFolderSelectionCaptureAuthorityOpened = () => { };
            });
        }

        [Test]
        public void TestExternalFolderWorkspaceConfiguredRestartUsesRealBmsAndLegacyManiaRenderersWithoutMutatingSource()
        {
            string packageRoot = string.Empty;
            string configuredSelection = string.Empty;
            Guid recordId = Guid.Empty;
            string displayLabel = string.Empty;
            string sourcePhysicalDigestBeforeRegistration = string.Empty;
            string sourcePhysicalDigestAfterUnregister = string.Empty;
            FolderInventorySnapshot sourceBeforeRegistration = default;
            FolderInventorySnapshot sourceBeforeUnregister = default;
            FolderInventorySnapshot sourceAfterUnregister = default;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            Task<bool>? unregisterTask = null;
            Skin firstSelectedSkin = null!;
            Skin restartedSelectedSkin = null!;
            JourneyRendererHost firstRenderer = null!;
            JourneyRendererHost restartedRenderer = null!;
            Drawable firstBmsOrdinaryArtifact = null!;
            Drawable firstBmsBodyArtifact = null!;
            Drawable firstManiaNoteArtifact = null!;
            Drawable firstManiaBodyArtifact = null!;

            AddStep("create immutable external renderer package", () =>
            {
                packageRoot = createExternalPackage(createRendererJourneyPackage);
                sourceBeforeRegistration = captureFolderInventory(packageRoot);
                sourcePhysicalDigestBeforeRegistration = captureExternalRootPhysicalDigest(packageRoot);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external renderer registration", () => registrationTask?.IsCompleted == true);
            AddStep("query workspace and dropdown records", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for renderer workspace record", () => workspaceTask?.IsCompleted == true);
            AddUntilStep("wait for renderer dropdown record", () => dropdownTask?.IsCompleted == true);
            AddStep("select external renderer from dropdown", () =>
            {
                FolderSkinWorkspaceRecord row = workspaceTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External);
                recordId = row.RecordId;
                displayLabel = row.DisplayLabel;
                configuredSelection = recordId.ToString();

                Live<SkinInfo> dropdownRecord = dropdownTask!.GetAwaiter().GetResult()
                                                            .Single(record => record.ID == recordId);
                Assert.Multiple(() =>
                {
                    Assert.That(displayLabel, Is.EqualTo("external renderer journey"));
                    Assert.That(dropdownRecord.PerformRead(record => record.Name), Is.EqualTo(displayLabel));
                    Assert.That(dropdownRecord.PerformRead(record => record.IsExternalFilesystemStorage), Is.True);
                });

                manager.CurrentSkinInfo.Value = dropdownRecord;
            });
            AddUntilStep("wait for external renderer selection", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount first selected-skin renderer host", () =>
            {
                firstSelectedSkin = manager.CurrentSkin.Value;
                // Keep both rulesets safely in the future while the complete renderer graph and skin artifacts materialise.
                Add(firstRenderer = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 60_000));
            });
            AddUntilStep("wait for first renderer host load", () => firstRenderer.IsLoaded);
            AddStep("mount first BMS production provider", () => firstRenderer.ShowBms());
            AddUntilStep("wait for first BMS renderer artifacts", () => firstRenderer.BmsArtifactsLoaded);
            AddStep("assert first BMS production renderer artifacts", () =>
            {
                assertJourneyBmsRendererArtifacts(firstRenderer);
                firstBmsOrdinaryArtifact = firstRenderer.BmsOrdinaryArtifact;
                firstBmsBodyArtifact = firstRenderer.BmsBodyArtifact;
            });
            AddStep("mount first mania production provider", () => firstRenderer.ShowMania());
            addBoundedJourneyManiaArtifactWait("first mania renderer artifacts", () => firstRenderer);
            AddStep("assert first mania production renderer artifacts", () =>
            {
                assertJourneyManiaRendererArtifacts(firstRenderer);
                firstManiaNoteArtifact = firstRenderer.ManiaNoteArtifact;
                firstManiaBodyArtifact = firstRenderer.ManiaBodyArtifact;
            });
            AddStep("retire first production renderer trees", () => firstRenderer.Expire());
            AddUntilStep("wait for first renderer retirement", () => firstRenderer.Parent == null);
            AddStep("restart skin manager with configured external id", () =>
            {
                manager.ShutdownManagedFolderMutations();

                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                manager.SourceChanged += () => sourceChangedCount++;
                manager.SetSkinFromConfiguration(configuredSelection);
            });
            AddUntilStep("wait for configured external restart selection", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount restarted production renderer trees", () =>
            {
                restartedSelectedSkin = manager.CurrentSkin.Value;
                Assert.That(restartedSelectedSkin, Is.Not.SameAs(firstSelectedSkin));
                Add(restartedRenderer = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 60_000));
            });
            AddUntilStep("wait for restarted renderer host load", () => restartedRenderer.IsLoaded);
            AddStep("mount restarted BMS production provider", () => restartedRenderer.ShowBms());
            AddUntilStep("wait for restarted BMS renderer artifacts", () => restartedRenderer.BmsArtifactsLoaded);
            AddStep("assert fresh configured BMS artifacts", () =>
            {
                assertJourneyBmsRendererArtifacts(restartedRenderer);
                Assert.Multiple(() =>
                {
                    Assert.That(restartedRenderer.BmsOrdinaryArtifact, Is.Not.SameAs(firstBmsOrdinaryArtifact));
                    Assert.That(restartedRenderer.BmsBodyArtifact, Is.Not.SameAs(firstBmsBodyArtifact));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(recordId));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(recordId));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                });
            });
            AddStep("mount restarted mania production provider", () => restartedRenderer.ShowMania());
            addBoundedJourneyManiaArtifactWait("restarted mania renderer artifacts", () => restartedRenderer);
            AddStep("assert fresh configured mania artifacts", () =>
            {
                assertJourneyManiaRendererArtifacts(restartedRenderer);
                Assert.Multiple(() =>
                {
                    Assert.That(restartedRenderer.ManiaNoteArtifact, Is.Not.SameAs(firstManiaNoteArtifact));
                    Assert.That(restartedRenderer.ManiaBodyArtifact, Is.Not.SameAs(firstManiaBodyArtifact));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(recordId));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(recordId));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                });
            });
            AddStep("retire restarted production renderer trees", () => restartedRenderer.Expire());
            AddUntilStep("wait for restarted renderer retirement", () => restartedRenderer.Parent == null);
            AddStep("switch away and unregister external renderer", () =>
            {
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                });

                sourceBeforeUnregister = captureFolderInventory(packageRoot);
                unregisterTask = manager.UnregisterExternalFolderAsync(recordId);
            });
            AddUntilStep("wait for external renderer unregister", () => unregisterTask?.IsCompleted == true);
            AddStep("assert unregister preserved every source byte", () =>
            {
                sourceAfterUnregister = captureFolderInventory(packageRoot);
                sourcePhysicalDigestAfterUnregister = captureExternalRootPhysicalDigest(packageRoot);

                Assert.Multiple(() =>
                {
                    Assert.That(unregisterTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.Query(record => record.ID == recordId), Is.Null);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(sourceBeforeUnregister, Is.EqualTo(sourceBeforeRegistration));
                    Assert.That(sourceAfterUnregister, Is.EqualTo(sourceBeforeRegistration));
                    Assert.That(sourceAfterUnregister.InventoryDigest, Is.EqualTo(sourceBeforeRegistration.InventoryDigest));
                    Assert.That(sourceAfterUnregister.BytesDigest, Is.EqualTo(sourceBeforeRegistration.BytesDigest));
                    Assert.That(sourcePhysicalDigestAfterUnregister, Is.EqualTo(sourcePhysicalDigestBeforeRegistration));
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestExternalFolderWorkspaceUnregistersNonCurrentRecordWithoutSourceAuthority(bool sourceMissing)
        {
            const string drift_marker = "\n// external source drift retained\n";
            string packageRoot = string.Empty;
            Guid recordId = Guid.Empty;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? unregisterTask = null;

            AddStep("create and register non-current external package", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external registration", () => registrationTask?.IsCompleted == true);
            AddStep("query external record id", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for external record id", () => workspaceTask?.IsCompleted == true);
            AddStep("remove or drift external source", () =>
            {
                recordId = workspaceTask!.GetAwaiter().GetResult()
                                         .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                         .RecordId;

                if (sourceMissing)
                    Directory.Delete(packageRoot, recursive: true);
                else
                    File.AppendAllText(Path.Combine(packageRoot, "skin.ini"), drift_marker);

                unregisterTask = manager.UnregisterExternalFolderAsync(recordId);
            });
            AddStep("assert pure Realm unregister completed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(unregisterTask!.IsCompleted, Is.True);
                    Assert.That(unregisterTask.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.Query(record => record.ID == recordId), Is.Null);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(Directory.Exists(packageRoot), Is.EqualTo(!sourceMissing));

                    if (!sourceMissing)
                        Assert.That(File.ReadAllText(Path.Combine(packageRoot, "skin.ini")), Does.EndWith(drift_marker));
                });
            });
        }

        [Test]
        public void TestExternalFolderWorkspaceAuthorityProjectionTamperCannotSplitCurrentUnregister()
        {
            string packageRoot = string.Empty;
            Guid recordId = Guid.Empty;
            Live<SkinInfo> dropdownRecord = null!;
            Bindable<Skin> ownerProjection = null!;
            Skin selected = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevisionLease oldHolder = null!;
            FolderInventorySnapshot sourceA = default;
            string physicalDigestA = string.Empty;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            Task<bool>? currentUnregister = null;

            AddStep("create and register external package", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddUntilStep("wait for external registration", () => registrationTask?.IsCompleted == true);
            AddStep("query external consumers", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for workspace record", () => workspaceTask?.IsCompleted == true);
            AddUntilStep("wait for dropdown record", () => dropdownTask?.IsCompleted == true);
            AddStep("select external dropdown record", () =>
            {
                recordId = workspaceTask!.GetAwaiter().GetResult()
                                         .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                         .RecordId;
                dropdownRecord = dropdownTask!.GetAwaiter().GetResult().Single(record => record.ID == recordId);
                manager.CurrentSkinInfo.Value = dropdownRecord;
            });
            AddUntilStep("wait for current external pair", () =>
                manager.CurrentSkinInfo.Value.ID == recordId
                && manager.CurrentSkin.Value.SkinInfo.ID == recordId);
            AddStep("tamper only the guarded backing projection", () =>
            {
                selected = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                oldHolder = manager.AcquireCurrentRevisionHolderLease();
                ownerProjection = manager.CurrentSkin.GetBoundCopy();
                sourceA = captureFolderInventory(packageRoot);
                physicalDigestA = captureExternalRootPhysicalDigest(packageRoot);

                // CommitPrepared is the manager's private projection primitive. Even an InternalsVisibleTo caller
                // cannot use it to replace the immutable PublishedCurrentSkinPair read by public root/copy getters.
                ((SkinInstanceBindable)manager.CurrentSkin).CommitPrepared(manager.DefaultOmsSkin);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(dropdownRecord));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(selected));
                    Assert.That(ownerProjection.Value, Is.SameAs(selected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(selected));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(manager.Query(record => record.ID == recordId), Is.Not.Null);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(captureExternalRootPhysicalDigest(packageRoot), Is.EqualTo(physicalDigestA));
                });

                currentUnregister = manager.UnregisterExternalFolderAsync(recordId);
            });
            AddUntilStep("wait for protected fallback behind exact old holder", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("assert Realm and source wait for exact old detach", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(currentUnregister!.IsCompleted, Is.False);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(manager.Query(record => record.ID == recordId), Is.Not.Null);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(captureExternalRootPhysicalDigest(packageRoot), Is.EqualTo(physicalDigestA));
                });

                oldHolder.Dispose();
            });
            AddUntilStep("wait for exact old consumers to detach", () =>
                revisionA.ConsumersDetached.IsCompleted);
            AddUntilStep("wait for current unregister task", () =>
                currentUnregister?.IsCompleted == true);
            AddStep("assert current unregister succeeded", () =>
                Assert.That(currentUnregister!.GetAwaiter().GetResult(), Is.True));
            AddUntilStep("wait for pure Realm remove", () =>
                manager.Query(record => record.ID == recordId) == null);
            AddUntilStep("wait for exact old revision retire", () =>
                revisionA.Retired.IsCompleted);
            AddStep("assert authoritative current unregister never touched source", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(currentUnregister!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(captureExternalRootPhysicalDigest(packageRoot), Is.EqualTo(physicalDigestA));
                });
            });
        }

        [Test]
        public void TestExternalFolderWorkspaceShutdownJoinsObservedTaskAndRejectsReentry()
        {
            string packageRoot = string.Empty;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            Task<bool>? registrationTask = null;
            Task<bool>? afterShutdown = null;

            AddStep("hold coordinator and start external registration", () =>
            {
                packageRoot = createExternalPackage(createCompletePackage);
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
                Assert.That(registrationTask.IsCompleted, Is.False);
            });
            AddStep("shutdown and synchronously join workspace worker", () =>
            {
                try
                {
                    manager.ShutdownManagedFolderMutations();
                }
                finally
                {
                    heldLease!.Dispose();
                    heldLease = null;
                }

                afterShutdown = manager.RegisterExternalFolderAsync(packageRoot);
            });
            AddStep("assert task observation and fail-closed reentry", () =>
            {
                int externalRecords = Realm.Run(realm => realm.All<SkinInfo>()
                    .Count(record => record.IsExternalFilesystemStorage));

                Assert.Multiple(() =>
                {
                    Assert.That(registrationTask!.IsCompleted, Is.True);
                    Assert.That(registrationTask.GetAwaiter().GetResult(), Is.False);
                    Assert.That(afterShutdown!.IsCompleted, Is.True);
                    Assert.That(afterShutdown.GetAwaiter().GetResult(), Is.False);
                    Assert.That(externalRecords, Is.Zero);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(manager.CurrentSkin.Value.SkinInfo.ID));
                });
            });
        }

        [Test]
        public void TestFolderWorkspaceReadWorkersAreCancelledAndJoinedOnShutdown()
        {
            var recordsEntered = new ManualResetEventSlim();
            var recordsCancelled = new ManualResetEventSlim();
            var supportEntered = new ManualResetEventSlim();
            var supportCancelled = new ManualResetEventSlim();
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? recordsTask = null;
            Task<FolderSkinJournalSupportSnapshot>? supportTask = null;
            Task? shutdownTask = null;

            AddStep("start tracked workspace read workers", () =>
            {
                manager.FolderWorkspaceRecordsReadStarted = token =>
                {
                    recordsEntered.Set();
                    token.WaitHandle.WaitOne();
                    recordsCancelled.Set();
                    token.ThrowIfCancellationRequested();
                };
                manager.FolderWorkspaceJournalSupportReadStarted = token =>
                {
                    supportEntered.Set();
                    token.WaitHandle.WaitOne();
                    supportCancelled.Set();
                    token.ThrowIfCancellationRequested();
                };

                recordsTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                supportTask = manager.GetManagedFolderJournalSupportSnapshotAsync();
            });
            AddUntilStep("wait for both reads to enter", () => recordsEntered.IsSet && supportEntered.IsSet);
            AddStep("shutdown joins both reads", () => shutdownTask = Task.Run(manager.ShutdownManagedFolderMutations));
            AddUntilStep("wait for joined shutdown", () => shutdownTask?.IsCompleted == true);
            AddStep("assert cancellation observation and closed reentry", () =>
            {
                Task<IReadOnlyList<FolderSkinWorkspaceRecord>> afterShutdown = manager.GetFolderSkinWorkspaceRecordsAsync();
                Task<FolderSkinJournalSupportSnapshot> supportAfterShutdown = manager.GetManagedFolderJournalSupportSnapshotAsync();

                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(shutdownTask!.IsCompletedSuccessfully, Is.True);
                        Assert.That(recordsCancelled.IsSet, Is.True);
                        Assert.That(supportCancelled.IsSet, Is.True);
                        Assert.That(recordsTask!.IsCanceled, Is.True);
                        Assert.That(supportTask!.IsCanceled, Is.True);
                        Assert.That(afterShutdown.IsCanceled, Is.True);
                        Assert.That(supportAfterShutdown.IsCanceled, Is.True);
                    });
                }
                finally
                {
                    manager.FolderWorkspaceRecordsReadStarted = _ => { };
                    manager.FolderWorkspaceJournalSupportReadStarted = _ => { };
                    recordsEntered.Dispose();
                    recordsCancelled.Dispose();
                    supportEntered.Dispose();
                    supportCancelled.Dispose();
                }
            });
        }

        private string createExternalPackage(Action<string> populate)
        {
            string packageRoot = Path.Combine(Path.GetTempPath(), $"oms-bms-external-{Guid.NewGuid():N}");
            Directory.CreateDirectory(packageRoot);
            externalPackageRoots.Add(packageRoot);
            populate(packageRoot);
            return packageRoot;
        }

        private void deleteExternalPackageRoots()
        {
            foreach (string packageRoot in externalPackageRoots.ToArray())
            {
                if (Directory.Exists(packageRoot))
                    Directory.Delete(packageRoot, recursive: true);
            }

            externalPackageRoots.Clear();
        }

        private (string PackageRoot, Live<SkinInfo> Candidate) createCandidate(Action<string> populate, string instantiationInfo)
        {
            string folderName = $"folder-{Guid.NewGuid():N}";
            string relativePath = $"chartskin/{folderName}";
            string packageRoot = LocalStorage.GetFullPath(relativePath);
            Directory.CreateDirectory(packageRoot);
            populate(packageRoot);

            var info = new SkinInfo("managed folder", "OMS tests", instantiationInfo)
            {
                FilesystemStoragePath = relativePath,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
            };

            Realm.Write(realm => realm.Add(info));
            Live<SkinInfo> candidate = manager.Query(skin => skin.ID == info.ID);
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.PerformRead(skin => skin.Files.Count), Is.Zero);
            return (packageRoot, candidate);
        }

        private Live<SkinInfo> createRealmPackageCandidate()
        {
            var info = new SkinInfo("Realm package", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            Realm.Write(realm => realm.Add(info));
            Live<SkinInfo> candidate = manager.Query(skin => skin.ID == info.ID);
            Assert.That(candidate, Is.Not.Null);
            return candidate;
        }

        private static void createCompletePackage(string packageRoot)
        {
            string notes = Path.Combine(packageRoot, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: managed folder product test\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteImage1: notes/note\n" +
                "NoteImage1H: notes/head\n" +
                "NoteImage1L: notes/body\n" +
                "NoteImage1T: notes/tail\n" +
                "LongNoteBodyWidth: 0.4\n");

            File.WriteAllBytes(Path.Combine(notes, "note.png"), createPng(new Rgba32(240, 40, 80, 255)));
            File.WriteAllBytes(Path.Combine(notes, "head.png"), createPng(new Rgba32(40, 180, 240, 255)));
            File.WriteAllBytes(Path.Combine(notes, "body.png"), createPng(new Rgba32(250, 210, 30, 255)));
            File.WriteAllBytes(Path.Combine(notes, "tail.png"), createPng(new Rgba32(120, 230, 70, 255)));
        }

        private static void createRendererJourneyPackage(string packageRoot)
        {
            string bmsNotes = Path.Combine(packageRoot, "notes");
            string mania = Path.Combine(packageRoot, "mania");
            Directory.CreateDirectory(bmsNotes);
            Directory.CreateDirectory(mania);

            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: external renderer journey\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteImage1: notes/note\n" +
                "NoteImage1H: notes/head\n" +
                "NoteImage1L: notes/body\n" +
                "NoteImage1T: notes/tail\n" +
                "LongNoteBodyWidth: 0.4\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 4\n" +
                "KeyImage0: mania/key\n" +
                "KeyImage0D: mania/key-down\n" +
                "NoteImage0: mania/note\n" +
                "NoteImage0H: mania/head\n" +
                "NoteImage0L: mania/body\n" +
                "NoteImage0T: mania/tail\n");

            File.WriteAllBytes(Path.Combine(bmsNotes, "note.png"), createPng(new Rgba32(240, 40, 80, 255)));
            File.WriteAllBytes(Path.Combine(bmsNotes, "head.png"), createPng(new Rgba32(40, 180, 240, 255)));
            File.WriteAllBytes(Path.Combine(bmsNotes, "body.png"), createPng(new Rgba32(250, 210, 30, 255)));
            File.WriteAllBytes(Path.Combine(bmsNotes, "tail.png"), createPng(new Rgba32(120, 230, 70, 255)));
            File.WriteAllBytes(Path.Combine(mania, "key.png"), createPng(new Rgba32(70, 80, 100, 255)));
            File.WriteAllBytes(Path.Combine(mania, "key-down.png"), createPng(new Rgba32(120, 140, 180, 255)));
            File.WriteAllBytes(Path.Combine(mania, "note.png"), createPng(new Rgba32(220, 70, 200, 255)));
            File.WriteAllBytes(Path.Combine(mania, "head.png"), createPng(new Rgba32(70, 210, 220, 255)));
            File.WriteAllBytes(Path.Combine(mania, "body.png"), createPng(new Rgba32(240, 170, 50, 255)));
            File.WriteAllBytes(Path.Combine(mania, "tail.png"), createPng(new Rgba32(90, 220, 110, 255)));
        }

        private static FolderInventorySnapshot captureFolderInventory(string packageRoot)
        {
            string[] directories = Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories)
                                            .Select(path => normaliseRelativePath(packageRoot, path))
                                            .OrderBy(path => path, StringComparer.Ordinal)
                                            .ToArray();
            (string RelativePath, byte[] Bytes)[] files = Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories)
                                                                  .Select(path => (
                                                                      RelativePath: normaliseRelativePath(packageRoot, path),
                                                                      Bytes: File.ReadAllBytes(path)))
                                                                  .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                                                                  .ToArray();

            using var inventoryPayload = new MemoryStream();
            using var inventoryWriter = new BinaryWriter(inventoryPayload, Encoding.UTF8, leaveOpen: true);
            inventoryWriter.Write("folder-inventory-v1");

            foreach (string directory in directories)
            {
                inventoryWriter.Write('D');
                inventoryWriter.Write(directory);
            }

            foreach ((string relativePath, byte[] bytes) in files)
            {
                inventoryWriter.Write('F');
                inventoryWriter.Write(relativePath);
                inventoryWriter.Write(bytes.LongLength);
            }

            inventoryWriter.Flush();

            using var bytesPayload = new MemoryStream();
            using var bytesWriter = new BinaryWriter(bytesPayload, Encoding.UTF8, leaveOpen: true);
            bytesWriter.Write("folder-bytes-v1");

            foreach ((string relativePath, byte[] bytes) in files)
            {
                bytesWriter.Write(relativePath);
                bytesWriter.Write(bytes.LongLength);
                bytesWriter.Write(bytes);
            }

            bytesWriter.Flush();

            return new FolderInventorySnapshot(
                directories.Length,
                files.Length,
                files.Sum(file => (long)file.Bytes.Length),
                Convert.ToHexString(SHA256.HashData(inventoryPayload.ToArray())),
                Convert.ToHexString(SHA256.HashData(bytesPayload.ToArray())));
        }

        private string captureExternalRootPhysicalDigest(string packageRoot)
        {
            var probe = new SkinInfo(instantiationInfo: SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                FilesystemStoragePath = packageRoot,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
            };
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(probe, LocalStorage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ExternalFolder));
                Assert.That(resolution.ExternalCaptureRequest, Is.Not.Null);
            });

            SkinExternalFolderAuthorityCaptureResult capture =
                new SkinExternalFolderCaptureService().OpenAuthority(resolution.ExternalCaptureRequest);
            Assert.That(capture.IsSuccess, Is.True, capture.ToString());

            using ISkinExternalFolderAuthoritySession authority = capture.Session!;
            string digest = authority.PhysicalProof.Digest;
            authority.Validate();
            return digest;
        }

        private static string normaliseRelativePath(string root, string path)
            => Path.GetRelativePath(root, path).Replace('\\', '/');

        private static byte[] createPng(Rgba32 colour)
        {
            using var image = new Image<Rgba32>(3, 5, colour);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }

        private static Drawable? resolve(BmsSkinTransformer transformer, BmsNoteSkinElements element)
            => transformer.GetDrawableComponent(new BmsNoteSkinLookup(element, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K));

        private static void assertStaticSourceBound(Drawable? drawable, Type expectedType)
        {
            Assert.That(drawable, Is.TypeOf(expectedType));
            Assert.That(drawable!.ChildrenOfType<Sprite>().Single().Texture, Is.Not.Null);
        }

        private static void assertJourneyBmsRendererArtifacts(JourneyRendererHost renderer)
        {
            Assert.Multiple(() =>
            {
                Assert.That(renderer.ChildrenOfType<RulesetSkinProvidingContainer>().Single(), Is.SameAs(renderer.BmsProvider));
                Assert.That(renderer.BmsOrdinary, Is.TypeOf<DrawableBmsHitObject>());
                Assert.That(renderer.BmsHold, Is.TypeOf<DrawableBmsHoldNote>());
                Assert.That(renderer.BmsOrdinaryArtifact, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(renderer.BmsHeadArtifact, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(renderer.BmsBodyArtifact, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                Assert.That(renderer.BmsTailArtifact, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(renderer.BmsBodyArtifact.Width, Is.EqualTo(0.4f).Within(0.0001f));
            });

            foreach (Drawable artifact in new[]
                     {
                         renderer.BmsOrdinaryArtifact,
                         renderer.BmsHeadArtifact,
                         renderer.BmsBodyArtifact,
                         renderer.BmsTailArtifact,
                     })
            {
                assertReadyTexturedArtifact(artifact);
            }
        }

        private static void assertJourneyManiaRendererArtifacts(JourneyRendererHost renderer)
        {
            Assert.Multiple(() =>
            {
                Assert.That(renderer.ChildrenOfType<RulesetSkinProvidingContainer>().Single(), Is.SameAs(renderer.ManiaProvider));
                Assert.That(renderer.ManiaDrawable.Playfield.LayoutSnapshot, Is.SameAs(renderer.ManiaDrawable.LayoutSnapshot));
                Assert.That(renderer.ManiaColumn.LayoutSnapshot, Is.SameAs(renderer.ManiaDrawable.LayoutSnapshot));
                Assert.That(renderer.ManiaNote, Is.TypeOf<ManiaDrawableNote>());
                Assert.That(renderer.ManiaHold, Is.TypeOf<ManiaDrawableHoldNote>());
                Assert.That(renderer.ManiaNote.LayoutSnapshot, Is.SameAs(renderer.ManiaDrawable.LayoutSnapshot));
                Assert.That(renderer.ManiaHold.LayoutSnapshot, Is.SameAs(renderer.ManiaDrawable.LayoutSnapshot));
                Assert.That(renderer.ManiaNoteArtifact, Is.TypeOf<LegacyNotePiece>());
                Assert.That(renderer.ManiaHeadArtifact, Is.TypeOf<LegacyHoldNoteHeadPiece>());
                Assert.That(renderer.ManiaBodyArtifact, Is.TypeOf<LegacyBodyPiece>());
                Assert.That(renderer.ManiaTailArtifact, Is.TypeOf<LegacyHoldNoteTailPiece>());
            });

            foreach (Drawable artifact in new[]
                     {
                         renderer.ManiaNoteArtifact,
                         renderer.ManiaHeadArtifact,
                         renderer.ManiaBodyArtifact,
                         renderer.ManiaTailArtifact,
                     })
            {
                assertLoadedTexturedArtifact(artifact);
            }
        }

        private void addBoundedJourneyManiaArtifactWait(string label, Func<JourneyRendererHost> renderer)
        {
            Stopwatch? wait = null;

            AddStep($"start {label} wait", () => wait = Stopwatch.StartNew());
            AddUntilStep($"wait for {label} first bounded slice", () =>
                renderer().ManiaArtifactsLoaded || wait!.Elapsed >= TimeSpan.FromSeconds(8));
            AddUntilStep($"wait for {label} second bounded slice", () =>
                renderer().ManiaArtifactsLoaded || wait!.Elapsed >= TimeSpan.FromSeconds(16));
            AddUntilStep($"wait for {label} third bounded slice", () =>
                renderer().ManiaArtifactsLoaded || wait!.Elapsed >= TimeSpan.FromSeconds(24));
            AddStep($"assert {label} loaded within budget", () =>
            {
                wait!.Stop();
                Assert.That(
                    renderer().ManiaArtifactsLoaded,
                    Is.True,
                    $"{label} exceeded its explicit three-slice load budget.");
            });
        }

        private static void assertLoadedTexturedArtifact(Drawable artifact)
        {
            Assert.Multiple(() =>
            {
                Assert.That(artifact.IsLoaded, Is.True, $"{artifact.GetType().Name} did not load through the production provider tree.");
                Assert.That(
                    artifact.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null),
                    Is.True,
                    $"{artifact.GetType().Name} did not publish a textured renderer artifact.");
            });
        }

        private static void assertReadyTexturedArtifact(Drawable artifact)
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    artifact.LoadState,
                    Is.GreaterThanOrEqualTo(LoadState.Ready),
                    $"{artifact.GetType().Name} did not prepare through the production provider tree.");
                Assert.That(
                    artifact.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null),
                    Is.True,
                    $"{artifact.GetType().Name} did not publish a textured renderer artifact.");
            });
        }

        private readonly record struct FolderInventorySnapshot(
            int DirectoryCount,
            int FileCount,
            long TotalBytes,
            string InventoryDigest,
            string BytesDigest);

        private sealed partial class JourneyRendererHost : SkinProvidingContainer
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(IScrollingInfo))]
            private readonly IScrollingInfo scrollingInfo = new JourneyScrollingInfo();

            [Cached]
            private readonly ScoreProcessor scoreProcessor = new ScoreProcessor(new ManiaRuleset());

            [Cached]
            private readonly BmsRulesetConfigManager bmsRulesetConfig;

            public DrawableBmsHitObject BmsOrdinary { get; }
            public DrawableBmsHoldNote BmsHold { get; }
            public ManiaDrawableNote ManiaNote { get; }
            public ManiaDrawableHoldNote ManiaHold { get; }
            public DrawableManiaRuleset ManiaDrawable { get; }
            public RulesetSkinProvidingContainer BmsProvider { get; }
            public RulesetSkinProvidingContainer ManiaProvider { get; }
            public Column ManiaColumn => ManiaDrawable.Playfield.Stages.Single().Columns.Single(column => column.Index == 0);

            private readonly Container providerHost;

            public Drawable BmsOrdinaryArtifact => BmsOrdinary.ChildrenOfType<BmsAsyncNoteDrawable>().Single().Drawable!;

            public Drawable BmsHeadArtifact => BmsHold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>()
                                                       .Single()
                                                       .ChildrenOfType<BmsAsyncNoteDrawable>()
                                                       .Single()
                                                       .Drawable!;

            public Drawable BmsBodyArtifact => BmsHold.ChildrenOfType<BmsAsyncNoteDrawable>()
                                                       .Single(host => host.Drawable is BmsSourceBoundLongNoteBodyDrawable)
                                                       .Drawable!;

            public Drawable BmsTailArtifact => BmsHold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>()
                                                       .Single()
                                                       .ChildrenOfType<BmsAsyncNoteDrawable>()
                                                       .Single()
                                                       .Drawable!;

            public Drawable ManiaNoteArtifact => ManiaNote.ChildrenOfType<LegacyNotePiece>()
                                                         .Single(piece => piece.GetType() == typeof(LegacyNotePiece));

            public Drawable ManiaHeadArtifact => ManiaHold.Head.ChildrenOfType<LegacyHoldNoteHeadPiece>().Single();
            public Drawable ManiaBodyArtifact => ManiaHold.ChildrenOfType<LegacyBodyPiece>().Single();
            public Drawable ManiaTailArtifact => ManiaHold.Tail.ChildrenOfType<LegacyHoldNoteTailPiece>().Single();

            public bool BmsArtifactsLoaded =>
                BmsOrdinary.ChildrenOfType<BmsAsyncNoteDrawable>()
                           .Any(host => host.Drawable is BmsSourceBoundNoteDrawable artifact
                                        && artifact.LoadState >= LoadState.Ready)
                && BmsHold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>()
                          .Any(head => head.ChildrenOfType<BmsAsyncNoteDrawable>()
                                           .Any(host => host.Drawable is BmsSourceBoundNoteDrawable artifact
                                                        && artifact.LoadState >= LoadState.Ready))
                && BmsHold.ChildrenOfType<BmsAsyncNoteDrawable>()
                          .Any(host => host.Drawable is BmsSourceBoundLongNoteBodyDrawable artifact
                                       && artifact.LoadState >= LoadState.Ready)
                && BmsHold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>()
                          .Any(tail => tail.ChildrenOfType<BmsAsyncNoteDrawable>()
                                           .Any(host => host.Drawable is BmsSourceBoundNoteDrawable artifact
                                                        && artifact.LoadState >= LoadState.Ready));

            public bool ManiaArtifactsLoaded =>
                ManiaDrawable.IsLoaded
                && ManiaNote.ChildrenOfType<LegacyNotePiece>()
                            .Any(piece => piece.GetType() == typeof(LegacyNotePiece) && piece.IsLoaded)
                && ManiaHold.IsLoaded
                && ManiaHold.Head.ChildrenOfType<LegacyHoldNoteHeadPiece>().Any(piece => piece.IsLoaded)
                && ManiaHold.ChildrenOfType<LegacyBodyPiece>().Any(piece => piece.IsLoaded)
                && ManiaHold.Tail.ChildrenOfType<LegacyHoldNoteTailPiece>().Any(piece => piece.IsLoaded);

            public JourneyRendererHost(SkinManager skinManager, double bmsStartTime, double maniaStartTime)
                : base(skinManager.CurrentSkin.Value)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                var controlPoints = new ControlPointInfo();
                var difficulty = new BeatmapDifficulty();

                var bmsRuleset = new BmsRuleset();
                bmsRulesetConfig = new BmsRulesetConfigManager(null, bmsRuleset.RulesetInfo);
                var bmsBeatmap = new BmsBeatmap
                {
                    BeatmapInfo = { Ruleset = bmsRuleset.RulesetInfo },
                };
                var bmsOrdinary = new BmsHitObject
                {
                    StartTime = bmsStartTime,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };
                bmsOrdinary.ApplyDefaults(controlPoints, difficulty);
                BmsOrdinary = new DrawableBmsHitObject(bmsOrdinary);
                BmsOrdinary.Apply(bmsOrdinary);

                var bmsHold = new BmsHoldNote
                {
                    StartTime = bmsStartTime,
                    EndTime = bmsStartTime + 2_000,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };
                bmsHold.ApplyDefaults(controlPoints, difficulty);
                BmsHold = new DrawableBmsHoldNote(bmsHold);
                BmsHold.Apply(bmsHold);

                var maniaRuleset = new ManiaRuleset();
                var stageDefinition = new StageDefinition(4);
                var maniaBeatmap = new ManiaBeatmap(stageDefinition)
                {
                    BeatmapInfo = { Ruleset = maniaRuleset.RulesetInfo },
                    ControlPointInfo = controlPoints,
                };
                var maniaNote = new ManiaNote
                {
                    Column = 0,
                    StartTime = maniaStartTime,
                };
                maniaBeatmap.HitObjects.Add(maniaNote);
                maniaNote.ApplyDefaults(controlPoints, difficulty);

                var maniaHold = new ManiaHoldNote
                {
                    Column = 0,
                    StartTime = maniaStartTime,
                    Duration = 2_000,
                };
                maniaBeatmap.HitObjects.Add(maniaHold);
                maniaHold.ApplyDefaults(controlPoints, difficulty);
                ManiaDrawable = (DrawableManiaRuleset)maniaRuleset.CreateDrawableRulesetWith(maniaBeatmap);
                ManiaNote = new ManiaDrawableNote(maniaNote);
                ManiaHold = new ManiaDrawableHoldNote(maniaHold);

                // These manually-mounted drawables prove provider/material publication, not gameplay lifetime timing.
                // Keep their synthetic entries alive while retaining future hit times; nested hold entries are created during load.
                ManiaNote.Entry!.KeepAlive = true;
                ManiaHold.Entry!.KeepAlive = true;
                ManiaHold.OnNestedDrawableCreated += nested => nested.Entry!.KeepAlive = true;

                ManiaDrawable.Playfield.Add(ManiaNote);
                ManiaDrawable.Playfield.Add(ManiaHold);

                // This path intentionally exercises isolated BMS note resources, not a complete gameplay layout root.
                BmsProvider = new RulesetSkinProvidingContainer(bmsRuleset, bmsBeatmap, null, prepareGameplaySkinLayout: false)
                {
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            BmsOrdinary,
                            BmsHold,
                        },
                    },
                };

                ManiaProvider = new RulesetSkinProvidingContainer(
                    maniaRuleset,
                    maniaBeatmap,
                    null,
                    prepareGameplaySkinLayout: true)
                {
                    Child = ManiaDrawable,
                };

                InternalChild = providerHost = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                };
            }

            public void ShowBms() => providerHost.Child = BmsProvider;

            public void ShowMania() => providerHost.Child = ManiaProvider;
        }

        private sealed class JourneyScrollingInfo : IScrollingInfo
        {
            public IBindable<ScrollingDirection> Direction { get; } = new Bindable<ScrollingDirection>();
            public IBindable<double> TimeRange { get; } = new Bindable<double>(5_000);
            public IBindable<IScrollAlgorithm> Algorithm { get; } = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());
        }

        private sealed class FixedManagedFolderDiscoverySource : ISkinManagedFolderDiscoverySource
        {
            private readonly SkinManagedFolderDiscoverySnapshot snapshot;

            public FixedManagedFolderDiscoverySource(SkinManagedFolderDiscoverySnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return snapshot;
            }
        }

        private partial class ManagedDeleteSettingsCallerHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(IDialogOverlay))]
            public DialogOverlay DialogOverlay { get; } = new DialogOverlay();

            public SkinSection.DeleteSkinButton DeleteButton { get; } = new SkinSection.DeleteSkinButton();

            public ManagedDeleteSettingsCallerHost(SkinManager skinManager)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;
                InternalChildren = new Drawable[]
                {
                    DeleteButton,
                    DialogOverlay,
                };
            }
        }

        private partial class FullSkinSettingsCallerHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached]
            private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

            [Cached(typeof(IDialogOverlay))]
            public DialogOverlay DialogOverlay { get; } = new DialogOverlay();

            [Cached(typeof(INotificationOverlay))]
            private readonly RecordingNotificationOverlay notificationOverlay = new RecordingNotificationOverlay();

            public IReadOnlyList<Notification> PostedNotifications => notificationOverlay.Notifications;

            public SkinSection Section { get; } = new SkinSection();

            public FolderSkinWorkspace Workspace => Section.ChildrenOfType<FolderSkinWorkspace>().Single();

            public SkinSection.DeleteSkinButton CurrentDeleteButton => Section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single();

            public SkinSection.RenameSkinButton RenameButton => Section.ChildrenOfType<SkinSection.RenameSkinButton>().Single();

            public SkinSection.ExportSkinButton ExportButton => Section.ChildrenOfType<SkinSection.ExportSkinButton>().Single();

            public SettingsButtonV2 LayoutEditorButton => Section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString() == SkinSettingsStrings.SkinLayoutEditor.ToString());

            public FullSkinSettingsCallerHost(SkinManager skinManager)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;
                InternalChildren = new Drawable[]
                {
                    Section,
                    DialogOverlay,
                };
            }
        }

        private sealed class RecordingNotificationOverlay : INotificationOverlay
        {
            public List<Notification> Notifications { get; } = new List<Notification>();

            public void Post(Notification notification) => Notifications.Add(notification);

            public void Hide()
            {
            }

            public IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Notifications;
        }

        public enum StartupRetryInvalidation
        {
            Owner,
            Freeze,
            FactoryAllowlist,
            RecordDeleted,
        }

        public enum StartupScannerCompletionTiming
        {
            BeforeCaptureCompletion,
            DuringFactory,
        }
    }
}
