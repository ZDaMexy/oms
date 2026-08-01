// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
                });

                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                manager.SourceChanged += () => sourceChangedCount++;
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose selected folder skin", () =>
            {
                manager?.ShutdownManagedFolderMutations();

                if (manager?.CurrentSkin.Value is { } current
                    && !ReferenceEquals(current, manager.DefaultOmsSkin)
                    && !ReferenceEquals(current, manager.DefaultClassicSkin))
                {
                    current.Dispose();
                }
            });
        }

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
        public void TestDeleteFoundationConfirmsProtectedOmsPairWithoutDeleting()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Skin? selected = null;

            AddStep("create eligible managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
            });

            AddStep("select managed folder", () => manager.CurrentSkinInfo.Value = candidate);
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("confirm fallback pair under delete authority", () =>
            {
                selected = manager.CurrentSkin.Value;
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
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.Committed));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(candidate.PerformRead(info => info.DeletePending), Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                });

                Assert.That(session.TryAbortPreparedJournal(receipt), Is.True, "foundation test must leave no unresolved delete intent");
            });

            AddStep("dispose superseded managed skin", () => selected!.Dispose());
        }

        [Test]
        public void TestDeleteFoundationRejectsWhenFallbackCannotCommit()
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
            AddStep("disable selection and reject fallback commit", () =>
            {
                SkinManagedFolderMutationAuthorityResult authority = manager.ManagedFolderMutationAuthority.OpenDelete(
                    Guid.NewGuid(),
                    candidate.ID);

                Assert.That(authority.IsSuccess, Is.True);

                using SkinManagedFolderMutationAuthoritySession session = authority.Session!;
                SkinManagedFolderDurableMutationReceipt receipt = session.PersistPreparedJournal();
                manager.CurrentSkinInfo.Disabled = true;
                SkinManagedFolderProtectedFallbackCommitResult result =
                    manager.CommitProtectedFallbackPairForDelete(session, receipt);
                manager.CurrentSkinInfo.Disabled = false;

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.SelectionDisabled));
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
        public void TestDeleteFoundationNeverTreatsSplitSelectionPairAsNotRequired()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Skin? selected = null;

            AddStep("create and select eligible managed folder", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "registered-revision");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for managed selection", () => manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("construct reachable split selection pair", () =>
            {
                selected = manager.CurrentSkin.Value;
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;

                Assert.Throws<InvalidOperationException>(() => manager.CurrentSkin.Value = selected);
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(candidate.ID));
                });
            });
            AddStep("reject delete when protected pair cannot be reconfirmed", () =>
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
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(candidate.ID));
                    Assert.That(Directory.Exists(packageRoot), Is.True);
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
            Skin? selected = null;
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
                selected = manager.CurrentSkin.Value;

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
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Realm.Run(r => r.Find<SkinInfo>(candidate.ID) == null), Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("dispose detached deleted skin", () => selected!.Dispose());
        }

        [Test]
        public void TestManagedDeleteFallbackReentrantSelectionNeverSplitsPairAndLatestWins()
        {
            string deletedRoot = string.Empty;
            Live<SkinInfo> deleted = null!;
            Live<SkinInfo> selectable = null!;
            Skin? deletedSkin = null;
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
                deletedSkin = manager.CurrentSkin.Value;
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
            AddUntilStep("wait for reentrant delete convergence", () =>
                deleteTask?.IsCompleted == true);
            AddUntilStep("wait for reentrant selection linearisation", () =>
                reentrantAttempted
                && (reentrantRejection == SkinSelectionRejectionReason.ManagedFolderOperationInProgress
                    || Volatile.Read(ref captureCalls) > 0));
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
            AddStep("dispose detached deleted skin", () =>
            {
                Assert.That(captureCalls, Is.EqualTo(1));
                deletedSkin!.Dispose();
            });
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
                deletedSkin = manager.CurrentSkin.Value;
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
            AddStep("assert source callback did not form a join cycle", () =>
            {
                bool deleted = deleteTask!.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(shutdownEntered, Is.True);
                    Assert.That(shutdownCompletedInsideCallback, Is.True);
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<OmsSkin>());
                    Assert.That(Directory.Exists(packageRoot), Is.EqualTo(!deleted));
                    Assert.That(
                        Realm.Run(r => r.Find<SkinInfo>(candidate.ID) != null),
                        Is.EqualTo(!deleted));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });

                deletedSkin!.Dispose();
            });
        }

        [Test]
        public void TestManagedDeleteRealSettingsButtonAndDialogReturnBeforeConvergence()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            DialogOverlay dialogOverlay = null!;
            SkinSection.DeleteSkinButton deleteButton = null!;
            Skin? deletedSkin = null;

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
                deletedSkin = manager.CurrentSkin.Value;
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
            AddStep("dispose detached deleted skin", () => deletedSkin!.Dispose());
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
            Live<SkinInfo> realmPackage = null!;
            Task<bool>? deleteTask = null;

            AddStep("create and select ordinary Realm package", () =>
            {
                realmPackage = createRealmPackageCandidate();
                manager.CurrentSkinInfo.Value = realmPackage;
            });
            AddStep("assert ordinary package remains independently deletable", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(realmPackage.ID));
                    Assert.That(manager.CanModify(realmPackage), Is.True);
                    Assert.That(manager.CanDelete(realmPackage), Is.True);
                });
            });
            AddStep("confirm ordinary settings delete", () => deleteTask = manager.DeleteSkinAsync(realmPackage.ID));
            AddStep("assert legacy soft delete and protected default", () =>
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
            Live<SkinInfo> managedCandidate = null!;
            Live<SkinInfo> realmPackage = null!;
            SkinPackageRevisionCapsule? firstCapsule = null;
            Task? startupTask = null;
            Task? realmRequestTask = null;
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
            AddStep("queue newer Realm selection", () =>
                realmRequestTask = Task.Run(() => manager.CurrentSkinInfo.Value = realmPackage));
            AddUntilStep("wait for Realm request boundary", () => realmRequestReachedBoundary.IsSet);
            AddStep("release managed capture", releaseCapture.Set);
            AddUntilStep("wait for startup final contention", () => finalBoundaryContended.IsSet);
            AddUntilStep("wait for registered startup retry", () => retryWaiting.IsSet);
            AddStep("release startup after retry registration", releaseStartup.Set);
            AddUntilStep("wait for newer Realm selection", () =>
                realmRequestTask?.IsCompleted == true
                && manager.CurrentSkinInfo.Value.ID == realmPackage.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == realmPackage.ID);
            AddStep("assert latest accepted selection wins", () =>
            {
                Assert.That(startupTask!.Wait(TimeSpan.FromSeconds(30)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(realmRequestTask!.IsCompletedSuccessfully, Is.True);
                    Assert.That(startupTask.IsCompletedSuccessfully, Is.True);
                    Assert.That(captureCalls, Is.EqualTo(1));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
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
        public void TestConcurrentRealmSelectionSupersedesManagedCommitAtomically()
        {
            Live<SkinInfo> managed = null!;
            Live<SkinInfo> realmPackage = null!;
            Skin? supersededManagedSkin = null;
            Task? realmRequest = null;
            var realmRequestReachedLock = new ManualResetEventSlim();
            var allowRealmRequest = new ManualResetEventSlim();

            AddStep("create managed and Realm candidates", () =>
            {
                (_, managed) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                realmPackage = createRealmPackageCandidate();

                manager.SourceChanged += () =>
                {
                    if (manager.CurrentSkinInfo.Value.ID == managed.ID)
                        supersededManagedSkin = manager.CurrentSkin.Value;
                };

                manager.SelectionRequestBeforeCommitLock = target =>
                {
                    if (target.ID != realmPackage.ID)
                        return;

                    realmRequestReachedLock.Set();
                    Assert.That(allowRealmRequest.Wait(TimeSpan.FromSeconds(5)), Is.True);
                };

                manager.ManagedFolderBeforeCommit = () =>
                {
                    realmRequest = Task.Run(() => manager.CurrentSkinInfo.Value = realmPackage);
                    Assert.That(realmRequestReachedLock.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    allowRealmRequest.Set();
                };
            });

            AddStep("request managed candidate", () => manager.CurrentSkinInfo.Value = managed);
            AddUntilStep("wait for concurrent Realm request", () => realmRequest?.IsCompleted == true);
            AddUntilStep("wait for Realm package to remain current", () =>
                manager.CurrentSkinInfo.Value.ID == realmPackage.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == realmPackage.ID);
            AddStep("assert latest request wins", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(realmRequest!.IsCompletedSuccessfully, Is.True);
                    Assert.That(supersededManagedSkin, Is.Not.Null);
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(sourceChangedCount, Is.EqualTo(2));
                });

                supersededManagedSkin!.Dispose();
                realmRequestReachedLock.Dispose();
                allowRealmRequest.Dispose();
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
            AddStep("dispose superseded capsule and request renamed candidate", () =>
            {
                activeManagedSkin!.Dispose();
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
            Task<Live<SkinInfo>>? spoofUpdate = null;

            AddStep("create managed folder record", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
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

            AddStep("start spoofed package update", () => spoofUpdate = manager.ImportAsUpdate(
                new ProgressNotification(),
                new ImportTask(new MemoryStream(new byte[] { 5 }), "spoof-update.osk"),
                spoof));
            AddUntilStep("wait for spoofed update rejection", () => spoofUpdate?.IsCompleted == true);
            AddStep("assert authoritative folder record is unchanged", () =>
            {
                Assert.That(spoofUpdate!.IsFaulted, Is.True);
                Assert.That(spoofUpdate.Exception!.GetBaseException(), Is.TypeOf<InvalidOperationException>());
                candidate.PerformRead(info =>
                {
                    Assert.That(info.Files, Is.Empty);
                    Assert.That(info.DeletePending, Is.False);
                    Assert.That(info.FilesystemStoragePath, Does.StartWith("chartskin/"));
                });
            });
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
