// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using SharpCompress.Archives.Zip;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Product-boundary tests for the first Skin V1 file-backed vertical slice.
    /// </summary>
    /// <remarks>
    /// Managed-package cases import a real in-memory <c>.osk</c> into an isolated headless test store and select it
    /// through <see cref="SkinManager"/>. Product-path cases then mount the real BMS skin containers, asynchronous note
    /// host and gameplay hit object. The input archives and gated stores are test-owned and never address production data.
    /// </remarks>
    [HeadlessTest]
    [TestFixture]
    public partial class BmsManagedPackageNoteProductTest : OsuTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private readonly List<MemoryStream> ownedArchives = new List<MemoryStream>();
        private readonly List<Skin> ownedSkins = new List<Skin>();

        protected override bool UseFreshStoragePerRun => true;

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("restore OMS skin and dispose archives", () =>
            {
                skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo;

                foreach (Skin skin in ownedSkins)
                    skin.Dispose();

                ownedSkins.Clear();

                foreach (MemoryStream archive in ownedArchives)
                    archive.Dispose();

                ownedArchives.Clear();
            });
        }

        [Test]
        public void TestManagedOskStaticNativeBmsNoteImageProvidesSourceBoundSprite()
        {
            ImportedSkin imported = importAndSelect(
                "static native BMS note",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(3, 5, new Rgba32(240, 40, 80, 255)))));

            Drawable? resolved = null;

            AddStep("resolve ordinary note", () => resolved = resolveOrdinaryNote());
            AddStep("assert source-bound static sprite", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(imported.Skin, Is.TypeOf<BmsLegacySkin>());
                    Assert.That(resolved, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(resolved!.ChildrenOfType<TextureAnimation>(), Is.Empty);
                    Assert.That(resolved.ChildrenOfType<Sprite>().Single().Texture, Is.Not.Null);
                });
            });
        }

        [Test]
        public void TestManagedOskNumberedFramesProvideSourceBoundTextureAnimation()
        {
            importAndSelect(
                "animated native BMS note",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/ordinary-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            Drawable? resolved = null;

            AddStep("resolve animated ordinary note", () => resolved = resolveOrdinaryNote());
            AddStep("assert two-frame source-bound animation", () =>
            {
                Assert.That(resolved, Is.TypeOf<BmsSourceBoundNoteDrawable>());

                TextureAnimation animation = resolved!.ChildrenOfType<TextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestManualGateGeneratedGoodPackageUsesProductionAnimation()
        {
            importAndSelect(
                "manual-gate generated animated package",
                () => new MemoryStream(BmsNoteAnimationManualGateGenerator.CreateGoodPackage(), writable: false));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount generated manual-gate package", () => host = mountProductionHost(null));
            AddUntilStep("generated animation loaded", () =>
                host.Drawable?.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount
                == BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT);
            AddAssert("generated package uses source-bound note", () => host.Drawable, () => Is.TypeOf<BmsSourceBoundNoteDrawable>());
        }

        [Test]
        public void TestManualGateGeneratedBrokenPackageFallsBack()
        {
            importAndSelect(
                "manual-gate generated broken package",
                () => new MemoryStream(BmsNoteAnimationManualGateGenerator.CreateBrokenPackage(), writable: false));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount generated broken package", () => host = mountProductionHost(null));
            AddUntilStep("generated broken slot falls back", () => host.Drawable is DefaultBmsNoteDisplay { IsLoaded: true });
            AddAssert("generated broken package remains playable", () => host.Drawable, () => Is.TypeOf<DefaultBmsNoteDisplay>());
        }

        [Test]
        public void TestManagedOskAnimationRoutesToFourteenKeySecondScratch()
        {
            importAndSelect(
                "14K second-scratch animated native BMS note",
                () => createOskWithDeclarations(
                    "14K",
                    new[] { (Key: "NoteImageS2", Resource: "notes/scratch-two") },
                    ("notes/scratch-two-0.png", createPng(5, 5, new Rgba32(220, 80, 50, 255))),
                    ("notes/scratch-two-1.png", createPng(6, 4, new Rgba32(50, 120, 230, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount 14K second scratch", () => host = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 15, isScratch: true, keymode: BmsKeymode.Key14K)).Single());
            AddUntilStep("14K second scratch loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert 14K second-scratch animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(host.Drawable!.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestProductionHostLoadsSelectedAnimationThroughRealRulesetContainers()
        {
            importAndSelect(
                "production-host animated native BMS note",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/ordinary-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable host = null!;
            TextureAnimation animation = null!;

            AddStep("mount real BMS skin containers", () => host = mountProductionHost(null));
            AddUntilStep("production note loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert production host animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                animation = host.Drawable!.ChildrenOfType<TextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
            });
            AddUntilStep("animation advances to second frame", () => animation.CurrentFrameIndex == 1);
            AddUntilStep("animation loops to first frame", () => animation.CurrentFrameIndex == 0);
        }

        [Test]
        public void TestDrawableBmsHitObjectDisplaysImportedPackageAnimation()
        {
            importAndSelect(
                "gameplay hit-object animated native BMS note",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/ordinary-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            DrawableBmsHitObject drawable = null!;
            BmsAsyncNoteDrawable host = null!;

            AddStep("mount real gameplay hit object", () =>
            {
                drawable = mountGameplayHitObject(new BmsHitObject
                {
                    StartTime = Clock.CurrentTime + 1000,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                });
                host = drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single();
            });
            AddUntilStep("gameplay hit-object note loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert gameplay hit-object animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(host.Drawable!.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestSkinManagerSelectionChangeReplacesManagedPackageAnimation()
        {
            ImportedSkin first = importAndSelect(
                "first selected animated package",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/ordinary-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable host = null!;
            Drawable firstVisual = null!;

            AddStep("mount host under first selected package", () => host = mountProductionHost(null));
            AddUntilStep("first selected animation loaded", () => host.Drawable?.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 2);
            AddStep("capture first selected visual", () => firstVisual = host.Drawable!);

            ImportedSkin second = importAndSelect(
                "second selected animated package",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary-0.png", createPng(2, 6, new Rgba32(220, 60, 90, 255))),
                    ("notes/ordinary-1.png", createPng(6, 2, new Rgba32(60, 220, 120, 255))),
                    ("notes/ordinary-2.png", createPng(4, 4, new Rgba32(90, 80, 240, 255)))));

            AddUntilStep("second selected animation replaces first", () =>
                host.Drawable?.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 3);
            AddStep("assert real selection event reached mounted host", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(first.Info.ID, Is.Not.EqualTo(second.Info.ID));
                    Assert.That(skinManager.CurrentSkinInfo.Value.ID, Is.EqualTo(second.Info.ID));
                    Assert.That(host.Drawable, Is.Not.SameAs(firstVisual));
                    Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
        }

        [Test]
        public void TestBeatmapLocalDrawableKeepsPriorityOverSelectedManagedPackage()
        {
            importAndSelect(
                "selected package below beatmap provider",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(3, 5, new Rgba32(30, 180, 240, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount real containers with beatmap note", () => host = mountProductionHost(new BeatmapNoteSkin()));
            AddUntilStep("beatmap note loaded", () => host.Drawable is BeatmapNoteDrawable { IsLoaded: true });
            AddAssert("beatmap note wins", () => host.Drawable, () => Is.TypeOf<BeatmapNoteDrawable>());
        }

        [Test]
        public void TestBrokenBeatmapLocalComponentFallsThroughToSelectedManagedPackage()
        {
            importAndSelect(
                "selected package below broken beatmap provider",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(3, 5, new Rgba32(30, 180, 240, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount real containers with broken beatmap note", () => host = mountProductionHost(new BrokenBeatmapNoteSkin()));
            AddUntilStep("selected note loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddAssert("selected managed package takes over", () => host.Drawable, () => Is.TypeOf<BmsSourceBoundNoteDrawable>());
        }

        [Test]
        public void TestLiveSourceChangeLoadsOffUpdateThreadAndKeepsOldVisualUntilReady()
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            Drawable oldVisual = null!;
            var blockingSkin = new BlockingNoteSkin();

            AddStep("mount initial OMS note host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note)),
                };
            });
            AddUntilStep("initial note loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
            AddStep("capture old visual", () => oldVisual = host.Drawable!);
            AddStep("start blocked live replacement", () => source.Replace(
                new BmsSkinTransformer(blockingSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddUntilStep("replacement entered background lookup", () => blockingSkin.LookupEntered.IsSet);
            AddAssert("old visual remains during preparation", () => host.Drawable, () => Is.SameAs(oldVisual));
            AddAssert("replacement lookup is not update thread", () => blockingSkin.LookupWasOnUpdateThread, () => Is.False);
            AddStep("release replacement", () => blockingSkin.ReleaseLookup.Set());
            AddUntilStep("replacement published", () => host.Drawable is ReloadedNoteDrawable && host.Drawable.IsLoaded);
        }

        [Test]
        public void TestCancelledBlockedLookupDisposesUnpublishedVisual()
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            var abandonedSkin = new BlockingNoteSkin("abandoned");
            var currentSkin = new BlockingNoteSkin("current");
            currentSkin.ReleaseLookup.Set();

            AddStep("mount initial OMS note host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note)),
                };
            });
            AddUntilStep("initial note loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
            AddStep("start abandoned blocked lookup", () => source.Replace(
                new BmsSkinTransformer(abandonedSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddUntilStep("abandoned lookup entered", () => abandonedSkin.LookupEntered.IsSet);
            AddStep("replace blocked lookup with current source", () => source.Replace(
                new BmsSkinTransformer(currentSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddStep("release abandoned lookup after cancellation", () => abandonedSkin.ReleaseLookup.Set());
            AddUntilStep("current visual publishes", () => host.Drawable is ReloadedNoteDrawable { Tag: "current" } && host.Drawable.IsLoaded);
            AddUntilStep("unpublished abandoned visual is disposed", () => abandonedSkin.LastDrawable?.WasDisposed == true);
        }

        [Test]
        public void TestQueuedOldResultCannotPublishAfterNewSourceEventArrives()
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            Drawable oldVisual = null!;
            var oldSkin = new BlockingNoteSkin("old");
            var currentSkin = new BlockingNoteSkin("current");

            AddStep("mount initial OMS note host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note)),
                };
            });
            AddUntilStep("initial note loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
            AddStep("capture initial visual and queue next source from old loader", () =>
            {
                oldVisual = host.Drawable!;
                oldSkin.BeforeReturn = () => Scheduler.Add(() => source.Replace(
                    new BmsSkinTransformer(currentSkin),
                    new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            });
            AddStep("start old blocked replacement", () => source.Replace(
                new BmsSkinTransformer(oldSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddUntilStep("old replacement entered lookup", () => oldSkin.LookupEntered.IsSet);
            AddStep("release old result", () => oldSkin.ReleaseLookup.Set());
            AddUntilStep("current replacement entered lookup", () => currentSkin.LookupEntered.IsSet);
            AddAssert("queued old result never publishes", () => host.Drawable, () => Is.SameAs(oldVisual));
            AddStep("release current result", () => currentSkin.ReleaseLookup.Set());
            AddUntilStep("current result publishes", () => host.Drawable is ReloadedNoteDrawable { Tag: "current" } && host.Drawable.IsLoaded);
        }

        [Test]
        public void TestDynamicallyAddedInitialHostLoadsOffUpdateThreadAndKeepsCriticalFallback()
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            var blockingSkin = new BlockingNoteSkin();

            AddStep("mount and load source container", () => Child = source = new MutableSkinSourceContainer(
                new BmsSkinTransformer(blockingSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddUntilStep("source container loaded", () => source.IsLoaded);
            AddStep("dynamically add initial note host", () => source.Add(host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))));
            AddUntilStep("initial lookup entered background", () => blockingSkin.LookupEntered.IsSet);
            AddAssert("initial lookup is not update thread", () => blockingSkin.LookupWasOnUpdateThread, () => Is.False);
            AddAssert("critical fallback remains visible", () => host.Drawable, () => Is.TypeOf<DefaultBmsNoteDisplay>());
            AddStep("release initial lookup", () => blockingSkin.ReleaseLookup.Set());
            AddUntilStep("initial source visual published", () => host.Drawable is ReloadedNoteDrawable && host.Drawable.IsLoaded);
        }

        [Test]
        public void TestCancelledPackagePreparationDoesNotCaptureNextRequestGeneration()
        {
            ImportedSkin imported = importAndSelect(
                "cancellable managed package",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(4, 3, new Rgba32(80, 180, 240, 255))),
                    ("extra-a.bin", new byte[] { 1 }),
                    ("extra-b.bin", new byte[] { 2 })));

            var firstCancellation = new CancellationTokenSource();
            var secondCancellation = new CancellationTokenSource();
            SequencedGateResourceStore gatedFiles = null!;
            BmsLegacySkin isolatedSkin = null!;
            Task<Drawable?> firstRequest = null!;
            Task<Drawable?> secondRequest = null!;

            AddStep("construct gated package view", () =>
            {
                var resources = new DelegatingStorageResourceProvider((IStorageResourceProvider)skinManager);
                gatedFiles = new SequencedGateResourceStore(resources.Files);
                resources.FilesOverride = gatedFiles;
                isolatedSkin = new BmsLegacySkin(imported.Info.Value, resources);
                ownedSkins.Add(isolatedSkin);
                gatedFiles.Enabled = true;
            });
            AddStep("start first package request", () => firstRequest = resolveWithCancellation(isolatedSkin, firstCancellation.Token));
            AddUntilStep("first generation enters gate", () => gatedFiles.FirstEntered.IsSet);
            AddStep("cancel first and start second package request", () =>
            {
                firstCancellation.Cancel();
                secondRequest = resolveWithCancellation(isolatedSkin, secondCancellation.Token);
            });
            AddUntilStep("second generation starts before old gate releases", () => gatedFiles.SecondEntered.IsSet);
            AddStep("release both package generations", () =>
            {
                gatedFiles.ReleaseFirst.Set();
                gatedFiles.ReleaseSecond.Set();
            });
            AddUntilStep("both requests finish", () => firstRequest.IsCompleted && secondRequest.IsCompleted);
            AddStep("assert cancelled generation is isolated", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(firstRequest.IsCanceled || firstRequest.Exception?.GetBaseException() is OperationCanceledException, Is.True);
                    Assert.That(secondRequest.GetAwaiter().GetResult(), Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
            AddStep("dispose request tokens", () =>
            {
                firstCancellation.Dispose();
                secondCancellation.Dispose();
            });
        }

        [Test]
        public void TestAnimationBeyondComponentFrameBudgetFallsBack()
        {
            (string Name, byte[] Content)[] frames = Enumerable.Range(0, BmsManagedPackageNoteMaterializer.MAX_ANIMATION_FRAMES + 1)
                                                                  .Select(index => (
                                                                      $"notes/ordinary-{index}.png",
                                                                      createPng(1, 1, new Rgba32((byte)(index % 255), 80, 160, 255))))
                                                                  .ToArray();

            importAndSelect("over-budget native BMS note", () => createOsk("notes/ordinary", frames));
            assertOrdinaryNoteFailureReturnsNull("over-budget ordinary note");
        }

        [Test]
        public void TestMissingDeclaredNoteReturnsNullWithoutThrowing()
        {
            importAndSelect("missing native BMS note", () => createOsk("notes/missing"));
            assertOrdinaryNoteFailureReturnsNull("missing ordinary note");
        }

        [Test]
        public void TestCorruptDeclaredNoteReturnsNullWithoutThrowing()
        {
            importAndSelect(
                "corrupt native BMS note",
                () => createOsk("notes/corrupt", ("notes/corrupt.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00, 0xff })));

            assertOrdinaryNoteFailureReturnsNull("corrupt ordinary note");
        }

        [Test]
        public void TestLateDecodeFailureFallsBackOnlyItsOwnLane()
        {
            importAndSelect(
                "one late-decode failure beside one valid note",
                () => createOskWithLaneResources(
                    new[]
                    {
                        (Lane: 1, Resource: "notes/corrupt"),
                        (Lane: 2, Resource: "notes/valid"),
                    },
                    ("notes/corrupt.png", createLateDecodeFailurePng()),
                    ("notes/valid.png", createPng(4, 3, new Rgba32(40, 200, 90, 255)))));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount corrupt and valid lanes through real containers", () => hosts = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 2, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("both lane visuals loaded", () =>
                hosts[0].Drawable is DefaultBmsNoteDisplay { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert decode failure stays lane-local and playable", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(hosts[0].Drawable!.ChildrenOfType<Box>(), Is.Not.Empty);
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
        }

        [Test]
        public void TestExplicitEmptyDeclaredNoteReturnsNullWithoutThrowing()
        {
            importAndSelect("empty native BMS note", () => createOsk(string.Empty));
            assertOrdinaryNoteFailureReturnsNull("empty ordinary note");
        }

        [Test]
        public void TestParentRelativeDeclaredNoteReturnsNullWithoutThrowing()
        {
            importAndSelect(
                "uncontained native BMS note",
                () => createOsk("../ordinary", ("ordinary.png", createPng(3, 3, new Rgba32(200, 30, 30, 255)))));

            assertOrdinaryNoteFailureReturnsNull("uncontained ordinary note");
        }

        [TestCase("filesystem-path")]
        [TestCase("external")]
        [TestCase("delete-pending")]
        public void TestConflictingManagedAuthorityCannotProvide(string conflict)
        {
            ImportedSkin imported = importAndSelect(
                $"{conflict} authority package",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(3, 3, new Rgba32(40, 180, 220, 255)))));

            AddStep($"apply {conflict} metadata conflict", () => imported.Info.PerformWrite(info =>
            {
                switch (conflict)
                {
                    case "filesystem-path":
                        info.FilesystemStoragePath = "test-folder";
                        break;

                    case "external":
                        info.IsExternalFilesystemStorage = true;
                        break;

                    case "delete-pending":
                        info.DeletePending = true;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(conflict));
                }
            }));

            assertOrdinaryNoteFailureReturnsNull($"{conflict} authority ordinary note");
        }

        [Test]
        public void TestSameNamedResourceInAnotherManagedPackageDoesNotBleed()
        {
            ImportedSkin providingPackage = importAndSelect(
                "same-name providing package",
                () => createOsk(
                    "shared/ordinary",
                    ("shared/ordinary.png", createPng(7, 3, new Rgba32(70, 220, 120, 255)))));

            Drawable? providingResult = null;
            AddStep("resolve providing package", () => providingResult = resolveOrdinaryNote());
            AddAssert("providing package resolves", () => providingResult, () => Is.TypeOf<BmsSourceBoundNoteDrawable>());

            ImportedSkin missingPackage = importAndSelect(
                "same-name missing package",
                () => createOsk("shared/ordinary"));

            Drawable? missingResult = null;
            AddStep("resolve missing package without throw", () =>
            {
                Assert.DoesNotThrow(() => missingResult = resolveOrdinaryNote());
            });

            AddStep("assert no cross-package resource bleed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(providingPackage.Info.ID, Is.Not.EqualTo(missingPackage.Info.ID));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(missingPackage.Skin));
                    Assert.That(missingResult, Is.Null);
                });
            });
        }

        [Test]
        public void TestLowerSourceSameNamedTextureCannotSatisfyMissingSelectedDeclaration()
        {
            ImportedSkin lowerTexturePackage = importAndSelect(
                "lower texture-only package",
                () => createOsk(
                    null,
                    ("shared/ordinary.png", createPng(7, 3, new Rgba32(70, 220, 120, 255)))));
            ImportedSkin selectedMissingPackage = importAndSelect(
                "selected same-name missing package",
                () => createOsk("shared/ordinary"));

            SkinnableDrawable host = null!;

            AddStep("mount selected, lower and Oms source chain", () =>
            {
                Child = new TestSkinSourceContainer(
                    new BmsSkinTransformer(selectedMissingPackage.Skin),
                    new BmsSkinTransformer(lowerTexturePackage.Skin),
                    new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new SkinnableDrawable(createLookup(BmsNoteSkinElements.Note))
                    {
                        CentreComponent = false,
                    },
                };
            });

            AddUntilStep("fallback note loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
            AddStep("assert lower same-name texture did not bleed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(host.Drawable.ChildrenOfType<Box>(), Is.Not.Empty);
                    Assert.That(host.Drawable.ChildrenOfType<Sprite>().Where(sprite => sprite is not Box), Is.Empty);
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>(), Is.Empty);
                });
            });
        }

        [Test]
        public void TestOrdinaryNotePackageDoesNotInterceptLongNoteElements()
        {
            importAndSelect(
                "ordinary-note-only package",
                () => createOsk(
                    "notes/ordinary",
                    ("notes/ordinary.png", createPng(4, 4, new Rgba32(200, 90, 230, 255)))));

            AddStep("assert only ordinary note is intercepted", () =>
            {
                var transformer = new BmsSkinTransformer(skinManager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    Assert.That(transformer.GetDrawableComponent(createLookup(BmsNoteSkinElements.Note)), Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(transformer.GetDrawableComponent(createLookup(BmsNoteSkinElements.LongNoteHead)), Is.Null);
                    Assert.That(transformer.GetDrawableComponent(createLookup(BmsNoteSkinElements.LongNoteBody)), Is.Null);
                    Assert.That(transformer.GetDrawableComponent(createLookup(BmsNoteSkinElements.LongNoteTail)), Is.Null);
                });
            });
        }

        private ImportedSkin importAndSelect(string label, Func<MemoryStream> createArchive)
        {
            var imported = new ImportedSkin();

            AddStep($"create {label} osk", () =>
            {
                imported.Archive = createArchive();
                ownedArchives.Add(imported.Archive);
            });

            AddStep($"import {label} osk", () =>
            {
                string archiveName = $"bms-managed-note-{Guid.NewGuid():N}.osk";
                imported.ImportTask = skinManager.Import(new ImportTask(imported.Archive, archiveName));
            });

            AddUntilStep($"wait for {label} import", () => imported.ImportTask?.IsCompleted == true);
            AddStep($"select {label}", () =>
            {
                imported.Info = imported.ImportTask.GetAwaiter().GetResult();
                skinManager.CurrentSkinInfo.Value = imported.Info;
                imported.Skin = skinManager.CurrentSkin.Value;
            });

            AddUntilStep($"wait for {label} selection", () =>
                imported.Info != null
                && skinManager.CurrentSkinInfo.Value.ID == imported.Info.ID
                && skinManager.CurrentSkin.Value.SkinInfo.ID == imported.Info.ID
                && skinManager.CurrentSkin.Value is BmsLegacySkin);

            return imported;
        }

        private void assertOrdinaryNoteFailureReturnsNull(string label)
        {
            Drawable? resolved = null;

            AddStep($"resolve {label} without throw", () =>
            {
                Assert.DoesNotThrow(() => resolved = resolveOrdinaryNote());
            });
            AddAssert($"{label} inherits", () => resolved, () => Is.Null);
        }

        private Drawable? resolveOrdinaryNote()
            => new BmsSkinTransformer(skinManager.CurrentSkin.Value).GetDrawableComponent(createLookup(BmsNoteSkinElements.Note));

        private static Task<Drawable?> resolveWithCancellation(BmsLegacySkin skin, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                using (BmsManagedPackageNoteLoadContext.Enter(cancellationToken))
                {
                    return new BmsSkinTransformer(skin).GetDrawableComponent(createLookup(BmsNoteSkinElements.Note));
                }
            });

        private BmsAsyncNoteDrawable mountProductionHost(ISkin? beatmapSkin)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
            var host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note));

            Child = new RulesetSkinProvidingContainer(ruleset, beatmap, beatmapSkin)
            {
                Child = host,
            };

            return host;
        }

        private BmsAsyncNoteDrawable[] mountProductionHosts(params BmsNoteSkinLookup[] lookups)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
            BmsAsyncNoteDrawable[] hosts = lookups.Select(lookup => new BmsAsyncNoteDrawable(lookup)).ToArray();

            Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = hosts,
                },
            };

            return hosts;
        }

        private DrawableBmsHitObject mountGameplayHitObject(BmsHitObject note)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
            var drawable = new DrawableBmsHitObject(note);

            Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
            {
                Child = drawable,
            };

            return drawable;
        }

        private static BmsNoteSkinLookup createLookup(BmsNoteSkinElements element)
            => new BmsNoteSkinLookup(element, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K);

        private static MemoryStream createOsk(string? noteResource, params (string Name, byte[] Content)[] entries)
            => createOskWithLaneResources(
                noteResource == null
                    ? Array.Empty<(int Lane, string Resource)>()
                    : new[] { (Lane: 1, Resource: noteResource) },
                entries);

        private static MemoryStream createOskWithLaneResources(
            IReadOnlyList<(int Lane, string Resource)> noteResources,
            params (string Name, byte[] Content)[] entries)
            => createOskWithDeclarations(
                "7K",
                noteResources.Select(note => (Key: $"NoteImage{note.Lane}", note.Resource)).ToArray(),
                entries);

        private static MemoryStream createOskWithDeclarations(
            string keymode,
            IReadOnlyList<(string Key, string Resource)> declarations,
            params (string Name, byte[] Content)[] entries)
        {
            string skinName = $"BMS managed note product {Guid.NewGuid():N}";
            string skinIni =
                "[General]\n" +
                $"Name: {skinName}\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                $"Keymode: {keymode}\n" +
                string.Concat(declarations.Select(declaration => $"{declaration.Key}: {declaration.Resource}\n"));

            var output = new MemoryStream();
            var entryStreams = new List<MemoryStream>();

            try
            {
                using var archive = ZipArchive.Create();
                var iniStream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
                entryStreams.Add(iniStream);
                archive.AddEntry("skin.ini", iniStream);

                foreach ((string name, byte[] content) in entries)
                {
                    var entryStream = new MemoryStream(content, writable: false);
                    entryStreams.Add(entryStream);
                    archive.AddEntry(name, entryStream);
                }

                archive.SaveTo(output);
            }
            finally
            {
                foreach (MemoryStream entryStream in entryStreams)
                    entryStream.Dispose();
            }

            output.Position = 0;
            return output;
        }

        private static byte[] createPng(int width, int height, Rgba32 colour)
        {
            using var image = new Image<Rgba32>(width, height, colour);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }

        private static byte[] createLateDecodeFailurePng()
        {
            byte[] valid = createPng(4, 3, new Rgba32(230, 40, 80, 255));

            // PNG identification only needs the signature and IHDR. Truncating the following image data preserves
            // dimensions for the preflight while guaranteeing that full pixel decode cannot complete.
            return valid.Take(40).ToArray();
        }

        private sealed class ImportedSkin
        {
            public MemoryStream Archive { get; set; } = null!;
            public Task<Live<SkinInfo>> ImportTask { get; set; } = null!;
            public Live<SkinInfo> Info { get; set; } = null!;
            public Skin Skin { get; set; } = null!;
        }

        private sealed class DelegatingStorageResourceProvider : IStorageResourceProvider
        {
            private readonly IStorageResourceProvider inner;

            public IResourceStore<byte[]>? FilesOverride { private get; set; }

            public IRenderer Renderer => inner.Renderer;
            public AudioManager? AudioManager => inner.AudioManager;
            public IResourceStore<byte[]> Files => FilesOverride ?? inner.Files;
            public IResourceStore<byte[]> Resources => inner.Resources;
            public RealmAccess RealmAccess => inner.RealmAccess;

            public DelegatingStorageResourceProvider(IStorageResourceProvider inner)
            {
                this.inner = inner;
            }

            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
                => inner.CreateTextureLoaderStore(underlyingStore);
        }

        private sealed class SequencedGateResourceStore : IResourceStore<byte[]>
        {
            private readonly IResourceStore<byte[]> inner;
            private int gatedReads;

            public readonly ManualResetEventSlim FirstEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim SecondEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseFirst = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseSecond = new ManualResetEventSlim();

            public bool Enabled { get; set; }

            public SequencedGateResourceStore(IResourceStore<byte[]> inner)
            {
                this.inner = inner;
            }

            public byte[] Get(string name) => inner.Get(name);

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => inner.GetAsync(name, cancellationToken);

            public Stream? GetStream(string name)
            {
                if (Enabled)
                {
                    switch (Interlocked.Increment(ref gatedReads))
                    {
                        case 1:
                            FirstEntered.Set();
                            ReleaseFirst.Wait(TimeSpan.FromSeconds(10));
                            break;

                        case 2:
                            SecondEntered.Set();
                            ReleaseSecond.Wait(TimeSpan.FromSeconds(10));
                            break;
                    }
                }

                return inner.GetStream(name);
            }

            public IEnumerable<string> GetAvailableResources() => inner.GetAvailableResources();

            public void Dispose()
            {
                // The wrapped SkinManager store is process-owned; this gate never disposes it.
            }
        }

        private sealed partial class TestSkinSourceContainer : SkinProvidingContainer
        {
            public TestSkinSourceContainer(params ISkin[] sources)
            {
                SetSources(sources);
            }
        }

        private sealed partial class MutableSkinSourceContainer : SkinProvidingContainer
        {
            public MutableSkinSourceContainer(params ISkin[] sources)
            {
                SetSources(sources);
            }

            public void Replace(params ISkin[] sources)
            {
                SetSources(sources);
                TriggerSourceChanged();
            }
        }

        private sealed class BeatmapNoteSkin : Skin
        {
            public BeatmapNoteSkin()
                : base(new SkinInfo(name: nameof(BeatmapNoteSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsNoteSkinLookup { Element: BmsNoteSkinElements.Note } ? new BeatmapNoteDrawable() : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class BrokenBeatmapNoteSkin : Skin
        {
            public BrokenBeatmapNoteSkin()
                : base(new SkinInfo(name: nameof(BrokenBeatmapNoteSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            {
                if (lookup is BmsSkinConfigurationLookup { Lookup: BmsSkinConfigurationLookups.NoteImage }
                    && typeof(TValue) == typeof(string))
                {
                    return SkinUtils.As<TValue>(new Bindable<string>("missing/beatmap-note"));
                }

                return null;
            }
        }

        private sealed partial class BeatmapNoteDrawable : CompositeDrawable
        {
            public BeatmapNoteDrawable()
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }
        }

        private sealed class BlockingNoteSkin : Skin
        {
            public readonly ManualResetEventSlim LookupEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseLookup = new ManualResetEventSlim();

            private readonly string tag;

            public bool LookupWasOnUpdateThread { get; private set; }
            public Action? BeforeReturn { get; set; }
            public ReloadedNoteDrawable? LastDrawable { get; private set; }

            public BlockingNoteSkin(string tag = "reloaded")
                : base(new SkinInfo(name: nameof(BlockingNoteSkin)), null)
            {
                this.tag = tag;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is not BmsNoteSkinLookup { Element: BmsNoteSkinElements.Note })
                    return null;

                LookupWasOnUpdateThread = ThreadSafety.IsUpdateThread;
                LookupEntered.Set();
                ReleaseLookup.Wait(TimeSpan.FromSeconds(10));
                BeforeReturn?.Invoke();
                return LastDrawable = new ReloadedNoteDrawable(tag);
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed partial class ReloadedNoteDrawable : CompositeDrawable
        {
            public string Tag { get; }
            public bool WasDisposed { get; private set; }

            public ReloadedNoteDrawable(string tag)
            {
                Tag = tag;
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }

            protected override void Dispose(bool isDisposing)
            {
                WasDisposed = true;
                base.Dispose(isDisposing);
            }
        }
    }
}
