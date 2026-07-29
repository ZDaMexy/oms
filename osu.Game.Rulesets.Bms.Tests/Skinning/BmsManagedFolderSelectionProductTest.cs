// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
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
                    new SkinManagedFolderPhysicalIdentity(101, 102, 103));
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
            Live<SkinInfo> valid = null!;
            Live<SkinInfo> invalid = null!;

            AddStep("create valid and invalid candidates", () =>
            {
                (_, valid) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                (_, invalid) = createCandidate(createCompletePackage, "missing.Type, missing.Assembly");
                manager.SourceChanged += () =>
                {
                    if (manager.CurrentSkinInfo.Value.ID == valid.ID)
                        manager.CurrentSkinInfo.Value = invalid;
                };
            });

            AddStep("request valid candidate", () => manager.CurrentSkinInfo.Value = valid);
            AddUntilStep("wait for valid publication", () => manager.CurrentSkin.Value.SkinInfo.ID == valid.ID);
            AddStep("assert reentrant rejection remains observable", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(valid.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(valid.ID));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.InstantiationInfoNotAllowed));
                    Assert.That(sourceChangedCount, Is.EqualTo(1));
                });
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
    }
}
