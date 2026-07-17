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
    }
}
