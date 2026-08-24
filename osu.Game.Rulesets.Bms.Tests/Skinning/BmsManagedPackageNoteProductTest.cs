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
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK.Graphics;
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
    /// Beatmap-local skins used here are injected <see cref="ISkin"/> fixtures which prove source ordering only; they do
    /// not imply that OMS currently exposes a beatmap-local authoring format or package producer.
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
            AddStep("detach live production hosts", () =>
                Clear(disposeChildren: true));
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
        public void TestManagedOskStaticNativeBmsLongNoteHeadImageProvidesSourceBoundSprite()
        {
            ImportedSkin imported = importAndSelect(
                "static native BMS long-note head",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1H", Resource: "notes/head-static") },
                    ("notes/head-static.png", createPng(3, 5, new Rgba32(240, 40, 80, 255)))));

            Drawable? resolved = null;

            AddStep("resolve long-note head", () => resolved = resolveNoteComponent(BmsNoteSkinElements.LongNoteHead));
            AddStep("assert source-bound static long-note head", () =>
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
        public void TestManagedOskStaticNativeBmsLongNoteTailImageProvidesSourceBoundSprite()
        {
            ImportedSkin imported = importAndSelect(
                "static native BMS long-note tail",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1T", Resource: "notes/tail-static") },
                    ("notes/tail-static.png", createPng(3, 5, new Rgba32(240, 40, 80, 255)))));

            Drawable? resolved = null;

            AddStep("resolve long-note tail", () => resolved = resolveNoteComponent(BmsNoteSkinElements.LongNoteTail));
            AddStep("assert source-bound static long-note tail", () =>
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
        public void TestManagedOskStaticNativeBmsLongNoteBodyImageProvidesSourceBoundSprite()
        {
            ImportedSkin imported = importAndSelect(
                "static native BMS long-note body",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body-static"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-static.png", createPng(3, 5, new Rgba32(240, 40, 80, 255)))));

            Drawable? resolved = null;

            AddStep("resolve long-note body", () => resolved = resolveNoteComponent(BmsNoteSkinElements.LongNoteBody));
            AddStep("assert source-bound static long-note body", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(imported.Skin, Is.TypeOf<BmsLegacySkin>());
                    Assert.That(resolved, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(resolved!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(resolved.ChildrenOfType<TextureAnimation>(), Is.Empty);
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
        public void TestManagedOskNumberedLongNoteBodyFramesProvideSourceBoundTextureAnimation()
        {
            importAndSelect(
                "animated native BMS long-note body",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/body-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            Drawable? resolved = null;

            AddStep("resolve animated long-note body", () => resolved = resolveNoteComponent(BmsNoteSkinElements.LongNoteBody));
            AddStep("assert two-frame source-bound body animation", () =>
            {
                Assert.That(resolved, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                Assert.That(resolved!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                var animation = resolved.ChildrenOfType<LegacySkinExtensions.SkinnableTextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
                Assert.That(animation.DefaultFrameLength, Is.EqualTo(LegacySkinExtensions.SIXTY_FRAME_TIME));
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

        [TestCase(BmsKeymode.Key7K, 0, "7K", "NoteImageSH")]
        [TestCase(BmsKeymode.Key14K, 15, "14K", "NoteImageS2H")]
        public void TestManagedOskLongNoteHeadAnimationRoutesToScratchSlot(
            BmsKeymode keymode,
            int laneIndex,
            string keymodeDeclaration,
            string resourceDeclaration)
        {
            importAndSelect(
                $"{keymodeDeclaration} scratch animated native BMS long-note head",
                () => createOskWithDeclarations(
                    keymodeDeclaration,
                    new[] { (Key: resourceDeclaration, Resource: "notes/scratch-head") },
                    ("notes/scratch-head-0.png", createPng(5, 5, new Rgba32(220, 80, 50, 255))),
                    ("notes/scratch-head-1.png", createPng(6, 4, new Rgba32(50, 120, 230, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount scratch long-note head", () => host = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex, isScratch: true, keymode: keymode)).Single());
            AddUntilStep("scratch long-note head loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert scratch long-note head animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(host.Drawable!.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
            });
        }

        [TestCase(BmsKeymode.Key7K, 1, false, "7K", "NoteImage1T")]
        [TestCase(BmsKeymode.Key7K, 0, true, "7K", "NoteImageST")]
        [TestCase(BmsKeymode.Key14K, 15, true, "14K", "NoteImageS2T")]
        public void TestManagedOskLongNoteTailAnimationRoutesToNormalAndScratchSlots(
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch,
            string keymodeDeclaration,
            string resourceDeclaration)
        {
            importAndSelect(
                $"{keymodeDeclaration} {(isScratch ? "scratch" : "normal")} animated native BMS long-note tail",
                () => createOskWithDeclarations(
                    keymodeDeclaration,
                    new[] { (Key: resourceDeclaration, Resource: "notes/tail") },
                    ("notes/tail-0.png", createPng(5, 5, new Rgba32(220, 80, 50, 255))),
                    ("notes/tail-1.png", createPng(6, 4, new Rgba32(50, 120, 230, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount routed long-note tail", () => host = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, laneIndex, isScratch, keymode)).Single());
            AddUntilStep("routed long-note tail loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert routed long-note tail animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                Assert.That(host.Drawable!.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
            });
        }

        [TestCase(BmsKeymode.Key7K, 1, false, "7K", "NoteImage1L")]
        [TestCase(BmsKeymode.Key7K, 0, true, "7K", "NoteImageSL")]
        [TestCase(BmsKeymode.Key14K, 15, true, "14K", "NoteImageS2L")]
        public void TestManagedOskLongNoteBodyAnimationRoutesToNormalAndScratchSlots(
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch,
            string keymodeDeclaration,
            string resourceDeclaration)
        {
            importAndSelect(
                $"{keymodeDeclaration} {(isScratch ? "scratch" : "normal")} animated native BMS long-note body",
                () => createOskWithDeclarations(
                    keymodeDeclaration,
                    new[]
                    {
                        (Key: resourceDeclaration, Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-0.png", createPng(5, 5, new Rgba32(220, 80, 50, 255))),
                    ("notes/body-1.png", createPng(6, 4, new Rgba32(50, 120, 230, 255)))));

            BmsAsyncNoteDrawable host = null!;

            AddStep("mount routed long-note body", () => host = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, laneIndex, isScratch, keymode)).Single());
            AddUntilStep("routed long-note body loaded", () => host.Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true });
            AddStep("assert routed long-note body animation and width", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                Assert.That(host.Drawable!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
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
                    // Keep gameplay expiry outside the asynchronous package observation window.
                    StartTime = Clock.CurrentTime + 60_000,
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
        public void TestDrawableBmsHoldNoteBodyDisplaysAndLoopsImportedPackageAnimation()
        {
            importAndSelect(
                "gameplay hold-note body animated native BMS note",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/body-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            DrawableBmsHoldNote drawable = null!;
            BmsAsyncNoteDrawable host = null!;
            TextureAnimation animation = null!;

            AddStep("mount real gameplay hold note for body", () =>
            {
                var hold = new BmsHoldNote
                {
                    // Keep the real hold's judgement/expiry outside this animation observation window.
                    StartTime = Clock.CurrentTime + 60_000,
                    EndTime = Clock.CurrentTime + 62_000,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };

                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawable = mountGameplayHoldNote(hold);
            });
            AddUntilStep("gameplay hold-note body loaded", () =>
                drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Any(candidate =>
                    candidate.Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true }));
            AddStep("capture gameplay hold-note body animation", () =>
            {
                host = drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single(candidate => candidate.Drawable is BmsSourceBoundLongNoteBodyDrawable);
                Assert.That(host.Drawable!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                animation = host.Drawable.ChildrenOfType<TextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
            });
            AddUntilStep("hold-note body advances to second frame", () => animation.CurrentFrameIndex == 1);
            AddUntilStep("hold-note body loops to first frame", () => animation.CurrentFrameIndex == 0);
        }

        [Test]
        public void TestManagedLongNoteBodyUsesRealHcnStateAuthorityForBreakAndRegrabVisuals()
        {
            importAndSelect(
                "managed body driven by real HCN state",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body.png", createPng(4, 4, new Rgba32(80, 180, 240, 255)))));

            DrawableBmsHoldNote drawable = null!;
            BmsSourceBoundLongNoteBodyDrawable body = null!;
            Sprite bodySprite = null!;
            ManualClock manualClock = null!;
            FramedClock testClock = null!;

            AddStep("mount real HCN hold with managed body", () =>
            {
                var hold = new BmsHoldNote
                {
                    StartTime = 1000,
                    EndTime = 1500,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };

                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
                {
                    OverallDifficulty = 2,
                });

                manualClock = new ManualClock
                {
                    CurrentTime = hold.StartTime,
                    IsRunning = true,
                };
                testClock = new FramedClock(manualClock);
                testClock.ProcessFrame();
                drawable = mountGameplayHoldNote(hold, BmsLongNoteMode.HCN, testClock);
            });
            AddUntilStep("managed HCN body loaded", () =>
                drawable.ChildrenOfType<BmsSourceBoundLongNoteBodyDrawable>().SingleOrDefault() is { IsLoaded: true });
            AddStep("capture managed body and assert idle visual", () =>
            {
                body = drawable.ChildrenOfType<BmsSourceBoundLongNoteBodyDrawable>().Single();
                bodySprite = body.ChildrenOfType<Sprite>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(drawable.ComputeBodyStateForTesting(), Is.EqualTo(BmsLongNoteBodyState.Idle));
                    Assert.That(body.Width, Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(body.Alpha, Is.EqualTo(0.8f).Within(0.0001f));
                    Assert.That(bodySprite.Colour.TopLeft.SRGB, Is.EqualTo(Color4.White));
                });
            });
            AddStep("press real HCN head", () => Assert.That(drawable.OnPressed(createPressEvent()), Is.True));
            AddUntilStep("managed body reaches holding state", () =>
                drawable.ComputeBodyStateForTesting() == BmsLongNoteBodyState.Holding
                && Math.Abs(body.Alpha - 0.8f) < 0.0001f);
            AddStep("assert holding body remains active", () =>
            {
                Assert.That(body.Alpha, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(bodySprite.Colour.TopLeft.SRGB, Is.EqualTo(Color4.White));
            });
            AddStep("release real HCN hold", () =>
            {
                manualClock.CurrentTime = 1010;
                testClock.ProcessFrame();
                drawable.OnReleased(createReleaseEvent());
            });
            AddUntilStep("real HCN authority reports broken", () => drawable.ComputeBodyStateForTesting() == BmsLongNoteBodyState.Broken);
            AddStep("advance 60 ms into broken transition", () =>
            {
                manualClock.CurrentTime = 1070;
                testClock.ProcessFrame();
            });
            AddUntilStep("broken transition is neither instantaneous nor complete", () =>
            {
                Color4 colour = bodySprite.Colour.TopLeft.SRGB;
                Color4 broken = BmsDefaultPlayfieldPalette.GreyOutBroken(Color4.White);
                return body.Alpha > 0.32f
                       && body.Alpha < 0.8f
                       && colour != Color4.White
                       && colour != broken;
            });
            AddStep("advance to 79 ms of broken transition", () =>
            {
                manualClock.CurrentTime = 1089;
                testClock.ProcessFrame();
            });
            AddStep("broken transition has not completed at 79 ms", () =>
            {
                // OutQuint may round the visible float to its endpoint just before completion. The live transforms
                // are the stable duration authority at 79 ms; the 60 ms step above separately proves interpolation.
                Assert.That(body.Transforms.Any(), Is.True);
                Assert.That(bodySprite.Transforms.Any(), Is.True);
            });
            AddStep("advance beyond 80 ms broken boundary", () =>
            {
                manualClock.CurrentTime = 1091;
                testClock.ProcessFrame();
            });
            AddUntilStep("managed body completes broken fade and grey after 80 ms", () =>
                Math.Abs(body.Alpha - 0.32f) < 0.0001f
                && bodySprite.Colour.TopLeft.SRGB == BmsDefaultPlayfieldPalette.GreyOutBroken(Color4.White));
            AddStep("re-grab real HCN hold", () =>
            {
                manualClock.CurrentTime = 1120;
                testClock.ProcessFrame();
                Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
            });
            AddUntilStep("real HCN authority reports holding after re-grab", () =>
                drawable.ComputeBodyStateForTesting() == BmsLongNoteBodyState.Holding);
            AddStep("advance 60 ms into recovered transition", () =>
            {
                manualClock.CurrentTime = 1180;
                testClock.ProcessFrame();
            });
            AddUntilStep("recovered transition is neither instantaneous nor complete", () =>
            {
                Color4 colour = bodySprite.Colour.TopLeft.SRGB;
                Color4 broken = BmsDefaultPlayfieldPalette.GreyOutBroken(Color4.White);
                return body.Alpha > 0.32f
                       && body.Alpha < 0.8f
                       && colour != broken
                       && colour != Color4.White;
            });
            AddStep("advance to 79 ms of recovered transition", () =>
            {
                manualClock.CurrentTime = 1199;
                testClock.ProcessFrame();
            });
            AddStep("recovered transition has not completed at 79 ms", () =>
            {
                Assert.That(body.Transforms.Any(), Is.True);
                Assert.That(bodySprite.Transforms.Any(), Is.True);
            });
            AddStep("advance beyond 80 ms recovered boundary", () =>
            {
                manualClock.CurrentTime = 1201;
                testClock.ProcessFrame();
            });
            AddUntilStep("managed body restores active HCN visual after 80 ms", () =>
                Math.Abs(body.Alpha - 0.8f) < 0.0001f
                && bodySprite.Colour.TopLeft.SRGB == Color4.White);
        }

        [Test]
        public void TestAsyncSourceBoundBodyHydratesAlreadyBrokenRealHoldBeforeFirstPublish()
        {
            var blockingSkin = new BlockingLongNoteBodySkin();
            DrawableBmsHoldNote drawable = null!;
            BmsAsyncNoteDrawable bodyHost = null!;
            DefaultBmsLongNoteBodyDisplay protectedBody = null!;
            Box protectedBox = null!;
            Color4 protectedActiveColour = default;
            ManualClock manualClock = null!;
            FramedClock testClock = null!;

            AddStep("mount blocked async body under real HCN hold", () =>
            {
                var hold = new BmsHoldNote
                {
                    StartTime = 1000,
                    EndTime = 1500,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };

                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
                {
                    OverallDifficulty = 2,
                });

                manualClock = new ManualClock
                {
                    CurrentTime = hold.StartTime,
                    IsRunning = true,
                };
                testClock = new FramedClock(manualClock);
                testClock.ProcessFrame();
                drawable = mountGameplayHoldNote(hold, BmsLongNoteMode.HCN, testClock, blockingSkin);
            });
            AddUntilStep("async body candidate is blocked off update thread", () => blockingSkin.LookupEntered.IsSet);
            AddStep("capture loaded protected body while candidate is blocked", () =>
            {
                bodyHost = drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single(host => host.Drawable is DefaultBmsLongNoteBodyDisplay);
                protectedBody = (DefaultBmsLongNoteBodyDisplay)bodyHost.Drawable!;
                protectedBox = protectedBody.ChildrenOfType<Box>().Single();
                protectedActiveColour = protectedBox.Colour.TopLeft.SRGB;

                Assert.Multiple(() =>
                {
                    Assert.That(blockingSkin.LookupWasOnUpdateThread, Is.False);
                    Assert.That(protectedBody.IsLoaded, Is.True);
                    Assert.That(protectedBody.Alpha, Is.EqualTo(0.8f).Within(0.0001f));
                });
            });
            AddStep("press then release real HCN before candidate publishes", () =>
            {
                Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
                manualClock.CurrentTime = 1010;
                testClock.ProcessFrame();
                drawable.OnReleased(createReleaseEvent());
            });
            AddUntilStep("real hold becomes broken while candidate remains blocked", () =>
                drawable.ComputeBodyStateForTesting() == BmsLongNoteBodyState.Broken
                && !blockingSkin.ReleaseLookup.IsSet);
            AddStep("advance protected body beyond broken transition", () =>
            {
                manualClock.CurrentTime = 1110;
                testClock.ProcessFrame();
            });
            AddUntilStep("protected body shares broken-state projection", () =>
                Math.Abs(protectedBody.Alpha - 0.32f) < 0.0001f
                && protectedBox.Colour.TopLeft.SRGB == BmsDefaultPlayfieldPalette.GreyOutBroken(protectedActiveColour));
            AddStep("release already-broken async candidate", () => blockingSkin.ReleaseLookup.Set());
            AddUntilStep("source-bound body first publishes already broken", () =>
            {
                if (bodyHost.Drawable is not BmsSourceBoundLongNoteBodyDrawable published)
                    return false;

                Box publishedBox = published.ChildrenOfType<Box>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(published.IsLoaded, Is.True);
                    Assert.That(drawable.ComputeBodyStateForTesting(), Is.EqualTo(BmsLongNoteBodyState.Broken));
                    Assert.That(published.Alpha, Is.EqualTo(0.32f).Within(0.0001f));
                    Assert.That(publishedBox.Colour.TopLeft.SRGB, Is.EqualTo(BmsDefaultPlayfieldPalette.GreyOutBroken(Color4.White)));
                });

                return true;
            });
        }

        [Test]
        public void TestDrawableBmsHoldNoteHeadDisplaysAndLoopsImportedPackageAnimation()
        {
            importAndSelect(
                "gameplay hold-note head animated native BMS note",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1H", Resource: "notes/head") },
                    ("notes/head-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/head-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            DrawableBmsHoldNote drawable = null!;
            BmsAsyncNoteDrawable host = null!;
            TextureAnimation animation = null!;

            AddStep("mount real gameplay hold note", () =>
            {
                var hold = new BmsHoldNote
                {
                    // Keep gameplay judgement/expiry well outside this animation observation window.
                    StartTime = Clock.CurrentTime + 60_000,
                    EndTime = Clock.CurrentTime + 62_000,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };

                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawable = mountGameplayHoldNote(hold);

                DrawableBmsHoldNoteHead head = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
                host = head.ChildrenOfType<BmsAsyncNoteDrawable>().Single();
            });
            AddUntilStep("gameplay hold-note head loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert gameplay hold-note head animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                animation = host.Drawable!.ChildrenOfType<TextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
            });
            AddUntilStep("hold-note head advances to second frame", () => animation.CurrentFrameIndex == 1);
            AddUntilStep("hold-note head loops to first frame", () => animation.CurrentFrameIndex == 0);
        }

        [Test]
        public void TestDrawableBmsHoldNoteTailDisplaysAndLoopsImportedPackageAnimation()
        {
            importAndSelect(
                "gameplay hold-note tail animated native BMS note",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1T", Resource: "notes/tail") },
                    ("notes/tail-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/tail-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            DrawableBmsHoldNote drawable = null!;
            BmsAsyncNoteDrawable host = null!;
            TextureAnimation animation = null!;

            AddStep("mount real gameplay hold note for tail", () =>
            {
                var hold = new BmsHoldNote
                {
                    // Keep the real nested tail's judgement/expiry well outside this 60 FPS observation window.
                    StartTime = Clock.CurrentTime + 60_000,
                    EndTime = Clock.CurrentTime + 62_000,
                    LaneIndex = 1,
                    IsScratch = false,
                    Keymode = BmsKeymode.Key7K,
                };

                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawable = mountGameplayHoldNote(hold);

                DrawableBmsHoldNoteTail tail = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();
                host = tail.ChildrenOfType<BmsAsyncNoteDrawable>().Single();
            });
            AddUntilStep("gameplay hold-note tail loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert gameplay hold-note tail animation", () =>
            {
                Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                animation = host.Drawable!.ChildrenOfType<TextureAnimation>().Single();
                Assert.That(animation.FrameCount, Is.EqualTo(2));
            });
            AddUntilStep("hold-note tail advances to second frame", () => animation.CurrentFrameIndex == 1);
            AddUntilStep("hold-note tail loops to first frame", () => animation.CurrentFrameIndex == 0);
        }

        [TestCase("0.4", 0.4f)]
        [TestCase("1", 1f)]
        [TestCase(null, 0.5775f)]
        [TestCase("NaN", 0.5775f)]
        [TestCase("Infinity", 0.5775f)]
        [TestCase("0", 0.5775f)]
        [TestCase("-0.25", 0.5775f)]
        [TestCase("1.01", 0.5775f)]
        public void TestManagedLongNoteBodyWidthUsesValidatedSourceBoundValueOrFieldDefault(string? declaredWidth, float expectedWidth)
        {
            var declarations = new List<(string Key, string Resource)>
            {
                ("NoteImage1L", "notes/body"),
            };

            if (declaredWidth != null)
                declarations.Add(("LongNoteBodyWidth", declaredWidth));

            importAndSelect(
                $"body width {declaredWidth ?? "absent"}",
                () => createOskWithDeclarations(
                    "7K",
                    declarations,
                    ("notes/body.png", createPng(4, 4, new Rgba32(90, 180, 230, 255)))));

            Drawable? resolved = null;

            AddStep("resolve body with validated width", () => resolved = resolveNoteComponent(BmsNoteSkinElements.LongNoteBody));
            AddStep("assert valid body remains provided with resolved width", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(resolved, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(resolved!.Width, Is.EqualTo(expectedWidth).Within(0.0001f));
                    Assert.That(float.IsFinite(resolved.Width), Is.True);
                    Assert.That(resolved.Width, Is.GreaterThan(0));
                    Assert.That(resolved.Width, Is.LessThanOrEqualTo(1));
                });
            });
        }

        [Test]
        public void TestLongNoteBodySelectionRejectsLiveHostThenPublishesAfterDetach()
        {
            ImportedSkin first = importAndSelect(
                "first selected animated body package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/body-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable host = null!;
            Drawable firstVisual = null!;
            Live<SkinInfo> firstSelection = null!;
            Skin firstOwner = null!;
            SkinCurrentRevision firstRevision = null!;

            AddStep("mount host under first selected body package", () => host = mountProductionHosts(createLookup(BmsNoteSkinElements.LongNoteBody)).Single());
            AddUntilStep("first selected body revision loaded", () =>
                host.Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true }
                && host.Drawable.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 2);
            AddStep("capture first body revision", () =>
            {
                firstVisual = host.Drawable!;
                firstSelection = skinManager.CurrentSkinInfo.Value;
                firstOwner = skinManager.CurrentSkin.Value;
                firstRevision = skinManager.CurrentRevision;
                Assert.That(firstVisual.Width, Is.EqualTo(0.4f).Within(0.0001f));
            });

            ImportedSkin second = importSkin(
                "second selected animated body package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.8"),
                    },
                    ("notes/body-0.png", createPng(2, 6, new Rgba32(220, 60, 90, 255))),
                    ("notes/body-1.png", createPng(6, 2, new Rgba32(60, 220, 120, 255))),
                    ("notes/body-2.png", createPng(4, 4, new Rgba32(90, 80, 240, 255)))));

            AddStep("reject second body selection while live host is attached", () =>
            {
                skinManager.CurrentSkinInfo.Value = second.Info;

                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.LiveGameplayActive));
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(firstSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(firstOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(firstRevision));
                    Assert.That(host.Drawable, Is.SameAs(firstVisual), "The live body must remain on the exact old revision after rejection.");
                    Assert.That(host.Drawable!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(2));
                });
            });

            AddStep("detach first live body host", () => Assert.That(Remove(Child, disposeImmediately: true), Is.True));
            selectImported("second selected animated body package", second);
            AddStep("mount host under second selected body package", () => host = mountProductionHosts(createLookup(BmsNoteSkinElements.LongNoteBody)).Single());

            AddUntilStep("second body revision loads after safe detach", () =>
                host.Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true }
                && host.Drawable.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 3
                && Math.Abs(host.Drawable.Width - 0.8f) < 0.0001f);
            AddStep("assert body frames and width came from second revision", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(first.Info.ID, Is.Not.EqualTo(second.Info.ID));
                    Assert.That(host.Drawable, Is.Not.SameAs(firstVisual));
                    Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(host.Drawable!.Width, Is.EqualTo(0.8f).Within(0.0001f));
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>().Single().FrameCount, Is.EqualTo(3));
                });
            });
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestSelectionRejectsLiveHostThenProvidesPackageAnimationAfterDetach(BmsNoteSkinElements element)
        {
            ImportedSkin first = importAndSelect(
                $"first selected animated {element} package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/component") },
                    ("notes/component-0.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/component-1.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable host = null!;
            Drawable firstVisual = null!;
            Live<SkinInfo> firstSelection = null!;
            Skin firstOwner = null!;
            SkinCurrentRevision firstRevision = null!;

            AddStep("mount host under first selected package", () => host = mountProductionHosts(createLookup(element)).Single());
            AddUntilStep("first selected animation loaded", () => host.Drawable?.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 2);
            AddStep("capture first selected visual", () =>
            {
                firstVisual = host.Drawable!;
                firstSelection = skinManager.CurrentSkinInfo.Value;
                firstOwner = skinManager.CurrentSkin.Value;
                firstRevision = skinManager.CurrentRevision;
            });

            ImportedSkin second = importSkin(
                $"second selected animated {element} package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/component") },
                    ("notes/component-0.png", createPng(2, 6, new Rgba32(220, 60, 90, 255))),
                    ("notes/component-1.png", createPng(6, 2, new Rgba32(60, 220, 120, 255))),
                    ("notes/component-2.png", createPng(4, 4, new Rgba32(90, 80, 240, 255)))));

            AddStep("reject second selection while live host is attached", () =>
            {
                skinManager.CurrentSkinInfo.Value = second.Info;

                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.LiveGameplayActive));
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(firstSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(firstOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(firstRevision));
                    Assert.That(host.Drawable, Is.SameAs(firstVisual));
                });
            });

            AddStep("detach first live host", () => Assert.That(Remove(Child, disposeImmediately: true), Is.True));
            selectImported($"second selected animated {element} package", second);
            AddStep("mount host under second selected package", () => host = mountProductionHosts(createLookup(element)).Single());

            AddUntilStep("second selected animation loads after safe detach", () =>
                host.Drawable?.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount == 3);
            AddStep("assert safely selected package reached remounted host", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(first.Info.ID, Is.Not.EqualTo(second.Info.ID));
                    Assert.That(skinManager.CurrentSkinInfo.Value.ID, Is.EqualTo(second.Info.ID));
                    Assert.That(host.Drawable, Is.Not.SameAs(firstVisual));
                    Assert.That(host.Drawable, Is.TypeOf(getSourceBoundDrawableType(element)));
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
        public void TestBeatmapLocalLongNoteHeadProviderOrderKeepsDirectDrawableAndFallsThroughBrokenTexture()
        {
            importAndSelect(
                "selected long-note heads below beatmap provider",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1H", Resource: "notes/head-one"),
                        (Key: "NoteImage2H", Resource: "notes/head-two"),
                    },
                    ("notes/head-one.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/head-two.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount beatmap provider above selected long-note heads", () => hosts = mountProductionHosts(
                new BeatmapLongNoteHeadProviderOrderSkin(),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex: 2, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("provider-order long-note heads loaded", () =>
                hosts[0].Drawable is BeatmapNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert direct beatmap drawable wins and broken beatmap texture falls through", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BeatmapNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
        }

        [Test]
        public void TestBeatmapLocalLongNoteTailProviderOrderKeepsDirectDrawableAndFallsThroughBrokenTexture()
        {
            importAndSelect(
                "selected long-note tails below injected beatmap provider",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1T", Resource: "notes/tail-one"),
                        (Key: "NoteImage2T", Resource: "notes/tail-two"),
                    },
                    ("notes/tail-one.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/tail-two.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable[] hosts = null!;

            // The injected ISkin proves runtime provider order. It is not evidence of a public beatmap-local file format.
            AddStep("mount injected beatmap provider above selected long-note tails", () => hosts = mountProductionHosts(
                new BeatmapLongNoteTailProviderOrderSkin(),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, laneIndex: 2, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("provider-order long-note tails loaded", () =>
                hosts[0].Drawable is BeatmapNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert direct beatmap tail wins and broken beatmap texture falls through", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BeatmapNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
        }

        [Test]
        public void TestBeatmapLocalLongNoteBodyProviderOrderKeepsDirectDrawableAndFallsThroughBrokenTexture()
        {
            importAndSelect(
                "selected long-note bodies below injected beatmap provider",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "notes/body-one"),
                        (Key: "NoteImage2L", Resource: "notes/body-two"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/body-one.png", createPng(3, 5, new Rgba32(30, 180, 240, 255))),
                    ("notes/body-two.png", createPng(5, 3, new Rgba32(250, 210, 30, 255)))));

            BmsAsyncNoteDrawable[] hosts = null!;

            // This injected ISkin proves runtime ordering only; it is not a public beatmap-local authoring producer.
            AddStep("mount injected beatmap provider above selected long-note bodies", () => hosts = mountProductionHosts(
                new BeatmapLongNoteBodyProviderOrderSkin(),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, laneIndex: 2, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("provider-order long-note bodies loaded", () =>
                hosts[0].Drawable is BeatmapNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true });
            AddStep("assert direct beatmap body wins and broken beatmap texture falls through", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BeatmapNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(hosts[1].Drawable!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                });
            });
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestLiveSourceChangeLoadsOffUpdateThreadAndKeepsOldVisualUntilReady(BmsNoteSkinElements element)
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            Drawable oldVisual = null!;
            var blockingSkin = new BlockingNoteSkin(element: element);

            AddStep("mount initial OMS supported note-component host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(element)),
                };
            });
            AddUntilStep("initial supported note component loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
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

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestCancelledBlockedLookupDisposesUnpublishedVisual(BmsNoteSkinElements element)
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            var abandonedSkin = new BlockingNoteSkin("abandoned", element);
            var currentSkin = new BlockingNoteSkin("current", element);
            currentSkin.ReleaseLookup.Set();

            AddStep("mount initial OMS supported note-component host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(element)),
                };
            });
            AddUntilStep("initial supported note component loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
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

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestQueuedOldResultCannotPublishAfterNewSourceEventArrives(BmsNoteSkinElements element)
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            Drawable oldVisual = null!;
            var oldSkin = new BlockingNoteSkin("old", element);
            var currentSkin = new BlockingNoteSkin("current", element);

            AddStep("mount initial OMS supported note-component host", () =>
            {
                Child = source = new MutableSkinSourceContainer(new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(element)),
                };
            });
            AddUntilStep("initial supported note component loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
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

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestDynamicallyAddedInitialHostLoadsOffUpdateThreadAndKeepsProtectedFallback(BmsNoteSkinElements element)
        {
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable host = null!;
            var blockingSkin = new BlockingNoteSkin(element: element);

            AddStep("mount and load source container", () => Child = source = new MutableSkinSourceContainer(
                new BmsSkinTransformer(blockingSkin),
                new BmsSkinTransformer(skinManager.DefaultOmsSkin)));
            AddUntilStep("source container loaded", () => source.IsLoaded);
            AddStep("dynamically add initial note host", () => source.Add(host = new BmsAsyncNoteDrawable(createLookup(element))));
            AddUntilStep("initial lookup entered background", () => blockingSkin.LookupEntered.IsSet);
            AddAssert("initial lookup is not update thread", () => blockingSkin.LookupWasOnUpdateThread, () => Is.False);
            AddStep("protected fallback remains installed", () => assertProtectedFallback(host.Drawable, element));
            AddStep("release initial lookup", () => blockingSkin.ReleaseLookup.Set());
            AddUntilStep("initial source visual published", () => host.Drawable is ReloadedNoteDrawable && host.Drawable.IsLoaded);
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestCancelledPackagePreparationDoesNotCaptureNextRequestGeneration(BmsNoteSkinElements element)
        {
            ImportedSkin imported = importAndSelect(
                $"cancellable managed {element} package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/component") },
                    ("notes/component.png", createPng(4, 3, new Rgba32(80, 180, 240, 255))),
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
            AddStep("start first package request", () => firstRequest = resolveWithCancellation(isolatedSkin, element, firstCancellation.Token));
            AddUntilStep("first generation enters gate", () => gatedFiles.FirstEntered.IsSet);
            AddStep("cancel first and start second package request", () =>
            {
                firstCancellation.Cancel();
                secondRequest = resolveWithCancellation(isolatedSkin, element, secondCancellation.Token);
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
                    Assert.That(secondRequest.GetAwaiter().GetResult(), Is.TypeOf(getSourceBoundDrawableType(element)));
                });
            });
            AddStep("dispose request tokens", () =>
            {
                firstCancellation.Dispose();
                secondCancellation.Dispose();
            });
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestAnimationBeyondComponentFrameBudgetFallsBack(BmsNoteSkinElements element)
        {
            (string Name, byte[] Content)[] frames = Enumerable.Range(0, BmsManagedPackageNoteMaterializer.MAX_ANIMATION_FRAMES + 1)
                                                                  .Select(index => (
                                                                      $"notes/component-{index}.png",
                                                                      createPng(1, 1, new Rgba32((byte)(index % 255), 80, 160, 255))))
                                                                  .ToArray();

            importAndSelect(
                $"over-budget native BMS {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/component") },
                    frames));
            assertNoteComponentFailureReturnsNull($"over-budget {element}", element);
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        public void TestMissingDeclaredComponentReturnsNullWithoutThrowing(BmsNoteSkinElements element)
        {
            importAndSelect(
                $"missing native BMS {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/missing") }));
            assertNoteComponentFailureReturnsNull($"missing {element}", element);
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        public void TestCorruptDeclaredComponentReturnsNullWithoutThrowing(BmsNoteSkinElements element)
        {
            importAndSelect(
                $"corrupt native BMS {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "notes/corrupt") },
                    ("notes/corrupt.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00, 0xff })));

            assertNoteComponentFailureReturnsNull($"corrupt {element}", element);
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        public void TestLateDecodeFailureFallsBackOnlyItsOwnLane(BmsNoteSkinElements element)
        {
            importAndSelect(
                $"one late-decode failure beside one valid {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: getDeclarationKey(element, 1), Resource: "notes/corrupt"),
                        (Key: getDeclarationKey(element, 2), Resource: "notes/valid"),
                    },
                    ("notes/corrupt.png", createLateDecodeFailurePng()),
                    ("notes/valid.png", createPng(4, 3, new Rgba32(40, 200, 90, 255)))));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount corrupt and valid lanes through real containers", () => hosts = mountProductionHosts(
                new BmsNoteSkinLookup(element, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(element, laneIndex: 2, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("both lane visuals loaded", () =>
                hosts[0].Drawable is { IsLoaded: true } fallback
                && fallback.GetType() == getProtectedFallbackDrawableType(element)
                && hosts[1].Drawable is { IsLoaded: true } provided
                && provided.GetType() == getSourceBoundDrawableType(element));
            AddStep("assert decode failure stays lane-local and playable", () =>
            {
                Assert.Multiple(() =>
                {
                    assertProtectedFallback(hosts[0].Drawable, element);
                    Assert.That(hosts[0].Drawable!.ChildrenOfType<Box>(), Is.Not.Empty);
                    Assert.That(hosts[1].Drawable, Is.TypeOf(getSourceBoundDrawableType(element)));
                });
            });
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        public void TestExplicitEmptyDeclaredComponentReturnsNullWithoutThrowing(BmsNoteSkinElements element)
        {
            importAndSelect(
                $"empty native BMS {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: string.Empty) }));
            assertNoteComponentFailureReturnsNull($"empty {element}", element);
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        public void TestParentRelativeDeclaredComponentReturnsNullWithoutThrowing(BmsNoteSkinElements element)
        {
            importAndSelect(
                $"uncontained native BMS {element}",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(element), Resource: "../component") },
                    ("component.png", createPng(3, 3, new Rgba32(200, 30, 30, 255)))));

            assertNoteComponentFailureReturnsNull($"uncontained {element}", element);
        }

        [Test]
        public void TestInvalidLongNoteHeadDeclarationsFallBackWithoutRejectingValidOrdinaryNote()
        {
            importAndSelect(
                "valid ordinary note beside invalid long-note heads",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1", Resource: "notes/valid-note"),
                        (Key: "NoteImage1H", Resource: "notes/missing-head"),
                        (Key: "NoteImage2H", Resource: "notes/corrupt-head"),
                        (Key: "NoteImage3H", Resource: string.Empty),
                        (Key: "NoteImage4H", Resource: "../outside-head"),
                        (Key: "NoteImage5H", Resource: "notes/bad-sequence"),
                    },
                    ("notes/valid-note.png", createPng(4, 4, new Rgba32(40, 200, 90, 255))),
                    ("notes/corrupt-head.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00, 0xff }),
                    ("outside-head.png", createPng(3, 3, new Rgba32(200, 30, 30, 255))),
                    ("notes/bad-sequence-0.png", createPng(3, 4, new Rgba32(80, 160, 240, 255))),
                    ("notes/bad-sequence-1.png", new byte[] { 0x4f, 0x4d, 0x53, 0x01, 0xfe })));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount valid ordinary note and missing long-note head", () => hosts = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("valid ordinary note and head fallback loaded", () =>
                hosts[0].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is DefaultBmsLongNoteHeadDisplay { IsLoaded: true });
            AddStep("assert invalid heads are isolated from valid ordinary note", () =>
            {
                var transformer = new BmsSkinTransformer(skinManager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<DefaultBmsLongNoteHeadDisplay>());
                    Assert.That(hosts[1].Drawable!.ChildrenOfType<Box>(), Is.Not.Empty);

                    for (int lane = 1; lane <= 5; lane++)
                    {
                        int capturedLane = lane;
                        Drawable? resolved = null;

                        Assert.DoesNotThrow(() => resolved = transformer.GetDrawableComponent(
                            new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, capturedLane, isScratch: false, keymode: BmsKeymode.Key7K)));
                        Assert.That(resolved, Is.Null, $"Long-note head lane {capturedLane} should inherit after its invalid declaration.");
                    }
                });
            });
        }

        [Test]
        public void TestInvalidLongNoteTailDeclarationsUseTransparentFallbackWithoutRejectingValidNoteAndHead()
        {
            importAndSelect(
                "valid note and head beside invalid long-note tails",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1", Resource: "notes/valid-note"),
                        (Key: "NoteImage1H", Resource: "notes/valid-head"),
                        (Key: "NoteImage1T", Resource: "notes/missing-tail"),
                        (Key: "NoteImage2T", Resource: "notes/corrupt-tail"),
                        (Key: "NoteImage3T", Resource: string.Empty),
                        (Key: "NoteImage4T", Resource: "../outside-tail"),
                        (Key: "NoteImage5T", Resource: "notes/bad-sequence"),
                    },
                    ("notes/valid-note.png", createPng(4, 4, new Rgba32(40, 200, 90, 255))),
                    ("notes/valid-head.png", createPng(4, 3, new Rgba32(80, 160, 240, 255))),
                    ("notes/corrupt-tail.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00, 0xff }),
                    ("outside-tail.png", createPng(3, 3, new Rgba32(200, 30, 30, 255))),
                    ("notes/bad-sequence-0.png", createPng(3, 4, new Rgba32(80, 160, 240, 255))),
                    ("notes/bad-sequence-1.png", new byte[] { 0x4f, 0x4d, 0x53, 0x01, 0xfe })));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount valid note and head with missing selected tail", () => hosts = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("valid components and protected tail fallback loaded", () =>
                hosts[0].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[2].Drawable is DefaultBmsLongNoteTailDisplay { IsLoaded: true });
            AddStep("assert invalid tails are isolated and fallback stays transparent", () =>
            {
                var transformer = new BmsSkinTransformer(skinManager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    assertProtectedFallback(hosts[2].Drawable, BmsNoteSkinElements.LongNoteTail);
                    Assert.That(hosts[2].Drawable!.ChildrenOfType<TextureAnimation>(), Is.Empty);
                    Assert.That(hosts[2].Drawable.ChildrenOfType<Sprite>().Where(sprite => sprite is not Box), Is.Empty);

                    for (int lane = 1; lane <= 5; lane++)
                    {
                        int capturedLane = lane;
                        Drawable? resolved = null;

                        Assert.DoesNotThrow(() => resolved = transformer.GetDrawableComponent(
                            new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, capturedLane, isScratch: false, keymode: BmsKeymode.Key7K)));
                        Assert.That(resolved, Is.Null, $"Long-note tail lane {capturedLane} should inherit after its invalid declaration.");
                    }
                });
            });
        }

        [Test]
        public void TestInvalidLongNoteBodyDeclarationsUseVisibleFallbackWithoutRejectingValidNoteHeadAndTail()
        {
            importAndSelect(
                "valid note, head and tail beside invalid long-note bodies",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1", Resource: "notes/valid-note"),
                        (Key: "NoteImage1H", Resource: "notes/valid-head"),
                        (Key: "NoteImage1T", Resource: "notes/valid-tail"),
                        (Key: "NoteImage1L", Resource: "notes/missing-body"),
                        (Key: "NoteImage2L", Resource: "notes/corrupt-body"),
                        (Key: "NoteImage3L", Resource: string.Empty),
                        (Key: "NoteImage4L", Resource: "../outside-body"),
                        (Key: "NoteImage5L", Resource: "notes/bad-sequence"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/valid-note.png", createPng(4, 4, new Rgba32(40, 200, 90, 255))),
                    ("notes/valid-head.png", createPng(4, 3, new Rgba32(80, 160, 240, 255))),
                    ("notes/valid-tail.png", createPng(3, 4, new Rgba32(240, 160, 80, 255))),
                    ("notes/corrupt-body.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00, 0xff }),
                    ("outside-body.png", createPng(3, 3, new Rgba32(200, 30, 30, 255))),
                    ("notes/bad-sequence-0.png", createPng(3, 4, new Rgba32(80, 160, 240, 255))),
                    ("notes/bad-sequence-1.png", new byte[] { 0x4f, 0x4d, 0x53, 0x01, 0xfe })));

            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("mount valid caps and note with missing selected body", () => hosts = mountProductionHosts(
                new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K),
                new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K)));
            AddUntilStep("valid components and protected body fallback loaded", () =>
                hosts[0].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[2].Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && hosts[3].Drawable is DefaultBmsLongNoteBodyDisplay { IsLoaded: true });
            AddStep("assert invalid bodies are isolated and fallback remains visible", () =>
            {
                var transformer = new BmsSkinTransformer(skinManager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[2].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    assertProtectedFallback(hosts[3].Drawable, BmsNoteSkinElements.LongNoteBody);

                    for (int lane = 1; lane <= 5; lane++)
                    {
                        int capturedLane = lane;
                        Drawable? resolved = null;

                        Assert.DoesNotThrow(() => resolved = transformer.GetDrawableComponent(
                            new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, capturedLane, isScratch: false, keymode: BmsKeymode.Key7K)));
                        Assert.That(resolved, Is.Null, $"Long-note body lane {capturedLane} should inherit after its invalid declaration.");
                    }
                });
            });
        }

        [TestCase("filesystem-path")]
        [TestCase("external")]
        [TestCase("delete-pending")]
        public void TestBackingRealmAuthorityConflictCannotMutatePublishedRevision(string conflict)
        {
            ImportedSkin imported = importAndSelect(
                $"{conflict} authority package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1", Resource: "notes/ordinary"),
                        (Key: "NoteImage1H", Resource: "notes/head"),
                        (Key: "NoteImage1L", Resource: "notes/body"),
                        (Key: "NoteImage1T", Resource: "notes/tail"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/ordinary.png", createPng(3, 3, new Rgba32(40, 180, 220, 255))),
                    ("notes/head.png", createPng(3, 4, new Rgba32(220, 100, 40, 255))),
                    ("notes/body.png", createPng(4, 4, new Rgba32(80, 140, 230, 255))),
                    ("notes/tail.png", createPng(4, 3, new Rgba32(100, 220, 40, 255)))));

            Live<SkinInfo> publishedSelection = null!;
            Skin publishedOwner = null!;
            SkinCurrentRevision publishedRevision = null!;
            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("capture immutable published revision", () =>
            {
                publishedSelection = skinManager.CurrentSkinInfo.Value;
                publishedOwner = skinManager.CurrentSkin.Value;
                publishedRevision = skinManager.CurrentRevision;
            });

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

            AddStep("mount real renderer after backing projection drift", () => hosts = mountProductionHosts(
                createLookup(BmsNoteSkinElements.Note),
                createLookup(BmsNoteSkinElements.LongNoteHead),
                createLookup(BmsNoteSkinElements.LongNoteBody),
                createLookup(BmsNoteSkinElements.LongNoteTail)));
            AddUntilStep("wait for exact published revision visuals", () =>
                hosts[0].Drawable is BmsSourceBoundNoteDrawable
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable
                && hosts[2].Drawable is BmsSourceBoundLongNoteBodyDrawable
                && hosts[3].Drawable is BmsSourceBoundNoteDrawable);
            AddStep("assert backing projection cannot mutate active revision", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(publishedSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(publishedOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(publishedRevision));
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[2].Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(hosts[3].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
        }

        [Test]
        public void TestCaseConflictingBackingRealmFilenameCannotMutatePublishedRevision()
        {
            ImportedSkin imported = importAndSelect(
                "case-conflicting filename package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1", Resource: "notes/shared"),
                        (Key: "NoteImage1H", Resource: "notes/shared"),
                        (Key: "NoteImage1L", Resource: "notes/shared"),
                        (Key: "NoteImage1T", Resource: "notes/shared"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("notes/shared.png", createPng(3, 3, new Rgba32(40, 180, 220, 255)))));

            Live<SkinInfo> publishedSelection = null!;
            Skin publishedOwner = null!;
            SkinCurrentRevision publishedRevision = null!;
            BmsAsyncNoteDrawable[] hosts = null!;

            AddStep("capture immutable filename revision", () =>
            {
                publishedSelection = skinManager.CurrentSkinInfo.Value;
                publishedOwner = skinManager.CurrentSkin.Value;
                publishedRevision = skinManager.CurrentRevision;
            });

            AddStep("add case-conflicting package filename metadata", () => imported.Info.PerformWrite(info =>
            {
                RealmNamedFileUsage existing = info.Files.Single(file => file.Filename == "notes/shared.png");
                info.Files.Add(new RealmNamedFileUsage(existing.File, "NOTES/SHARED.PNG"));
            }));

            AddStep("mount real renderer after filename projection drift", () => hosts = mountProductionHosts(
                createLookup(BmsNoteSkinElements.Note),
                createLookup(BmsNoteSkinElements.LongNoteHead),
                createLookup(BmsNoteSkinElements.LongNoteBody),
                createLookup(BmsNoteSkinElements.LongNoteTail)));
            AddUntilStep("wait for immutable filename revision visuals", () =>
                hosts[0].Drawable is BmsSourceBoundNoteDrawable
                && hosts[1].Drawable is BmsSourceBoundNoteDrawable
                && hosts[2].Drawable is BmsSourceBoundLongNoteBodyDrawable
                && hosts[3].Drawable is BmsSourceBoundNoteDrawable);
            AddStep("assert filename projection cannot mutate active revision", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(publishedSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(publishedOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(publishedRevision));
                    Assert.That(hosts[0].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[1].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(hosts[2].Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(hosts[3].Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                });
            });
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

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestLowerSourceSameNamedTextureCannotSatisfyMissingSelectedDeclaration(BmsNoteSkinElements element)
        {
            IReadOnlyList<(string Key, string Resource)> lowerDeclarations = element == BmsNoteSkinElements.LongNoteBody
                ? new[] { (Key: "LongNoteBodyWidth", Resource: "0.4") }
                : Array.Empty<(string Key, string Resource)>();
            IReadOnlyList<(string Key, string Resource)> selectedDeclarations = element == BmsNoteSkinElements.LongNoteBody
                ? new[]
                {
                    (Key: "NoteImage1L", Resource: "shared/component"),
                    (Key: "LongNoteBodyWidth", Resource: "0.9"),
                }
                : new[] { (Key: getDeclarationKey(element), Resource: "shared/component") };

            ImportedSkin lowerTexturePackage = importAndSelect(
                "lower texture-only package",
                () => createOskWithDeclarations(
                    "7K",
                    lowerDeclarations,
                    ("shared/component.png", createPng(7, 3, new Rgba32(70, 220, 120, 255)))));
            ImportedSkin selectedMissingPackage = importAndSelect(
                "selected same-name missing package",
                () => createOskWithDeclarations(
                    "7K",
                    selectedDeclarations));

            BmsAsyncNoteDrawable host = null!;
            Skin lowerSource = null!;

            AddStep("mount selected, lower and Oms source chain", () =>
            {
                lowerSource = createOwnedExactRealmSource(lowerTexturePackage);
                Child = new TestSkinSourceContainer(
                    new BmsSkinTransformer(selectedMissingPackage.Skin),
                    new BmsSkinTransformer(lowerSource),
                    new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(element)),
                };
            });

            AddUntilStep("fallback note loaded", () => host.IsLoaded && host.Drawable?.IsLoaded == true);
            AddStep("assert lower same-name texture did not bleed", () =>
            {
                Assert.Multiple(() =>
                {
                    assertProtectedFallback(host.Drawable, element);
                    Assert.That(host.Drawable.ChildrenOfType<Box>(), Is.Not.Empty);
                    Assert.That(host.Drawable.ChildrenOfType<Sprite>().Where(sprite => sprite is not Box), Is.Empty);
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>(), Is.Empty);
                });
            });
        }

        [Test]
        public void TestLowerSourceCompleteLongNoteBodyDeclarationUsesItsOwnWidth()
        {
            ImportedSkin lowerBodyPackage = importAndSelect(
                "lower complete body package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "shared/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.4"),
                    },
                    ("shared/body.png", createPng(7, 3, new Rgba32(70, 220, 120, 255)))));
            ImportedSkin selectedMissingPackage = importAndSelect(
                "selected missing same-name body package",
                () => createOskWithDeclarations(
                    "7K",
                    new[]
                    {
                        (Key: "NoteImage1L", Resource: "shared/body"),
                        (Key: "LongNoteBodyWidth", Resource: "0.9"),
                    }));

            BmsAsyncNoteDrawable host = null!;
            Skin lowerSource = null!;

            AddStep("mount selected, lower complete body and OMS source chain", () =>
            {
                lowerSource = createOwnedExactRealmSource(lowerBodyPackage);
                Child = new TestSkinSourceContainer(
                    new BmsSkinTransformer(selectedMissingPackage.Skin),
                    new BmsSkinTransformer(lowerSource),
                    new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.LongNoteBody)),
                };
            });

            AddUntilStep("lower complete body component loaded", () => host.Drawable is BmsSourceBoundLongNoteBodyDrawable { IsLoaded: true });
            AddStep("assert lower source supplied body texture and its own width", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundLongNoteBodyDrawable>());
                    Assert.That(host.Drawable!.Width, Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(host.Drawable.ChildrenOfType<Sprite>().Single().Texture, Is.Not.Null);
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>(), Is.Empty);
                });
            });
        }

        [Test]
        public void TestLowerSourceCompleteLongNoteTailDeclarationMayTakeOverMissingSelectedTail()
        {
            ImportedSkin lowerTailPackage = importAndSelect(
                "lower complete tail package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1T", Resource: "shared/tail") },
                    ("shared/tail.png", createPng(7, 3, new Rgba32(70, 220, 120, 255)))));
            ImportedSkin selectedMissingPackage = importAndSelect(
                "selected missing same-name tail package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: "NoteImage1T", Resource: "shared/tail") }));

            BmsAsyncNoteDrawable host = null!;
            Skin lowerSource = null!;

            AddStep("mount selected, lower complete tail and OMS source chain", () =>
            {
                lowerSource = createOwnedExactRealmSource(lowerTailPackage);
                Child = new TestSkinSourceContainer(
                    new BmsSkinTransformer(selectedMissingPackage.Skin),
                    new BmsSkinTransformer(lowerSource),
                    new BmsSkinTransformer(skinManager.DefaultOmsSkin))
                {
                    Child = host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.LongNoteTail)),
                };
            });

            AddUntilStep("lower complete tail component loaded", () => host.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true });
            AddStep("assert lower source took over as a complete component", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(host.Drawable!.ChildrenOfType<Sprite>().Single().Texture, Is.Not.Null);
                    Assert.That(host.Drawable.ChildrenOfType<TextureAnimation>(), Is.Empty);
                });
            });
        }

        [TestCase(BmsNoteSkinElements.Note)]
        [TestCase(BmsNoteSkinElements.LongNoteHead)]
        [TestCase(BmsNoteSkinElements.LongNoteBody)]
        [TestCase(BmsNoteSkinElements.LongNoteTail)]
        public void TestManagedNotePackageDoesNotInterceptUnrelatedNoteElements(BmsNoteSkinElements declaredElement)
        {
            importAndSelect(
                $"{declaredElement}-only package",
                () => createOskWithDeclarations(
                    "7K",
                    new[] { (Key: getDeclarationKey(declaredElement), Resource: "notes/component") },
                    ("notes/component.png", createPng(4, 4, new Rgba32(200, 90, 230, 255)))));

            AddStep("assert only declared managed note component is intercepted", () =>
            {
                var transformer = new BmsSkinTransformer(skinManager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    foreach (BmsNoteSkinElements element in Enum.GetValues<BmsNoteSkinElements>())
                    {
                        Drawable? result = transformer.GetDrawableComponent(createLookup(element));
                        Assert.That(result, element == declaredElement ? Is.TypeOf(getSourceBoundDrawableType(element)) : Is.Null, $"Unexpected resolution for {element}.");
                    }
                });
            });
        }

        private ImportedSkin importAndSelect(string label, Func<MemoryStream> createArchive, Action? afterSelect = null)
        {
            ImportedSkin imported = importSkin(label, createArchive);
            selectImported(label, imported, afterSelect);
            return imported;
        }

        private Skin createOwnedExactRealmSource(ImportedSkin imported)
        {
            Skin source = skinManager.GetSkin(imported.Info.Value);
            ownedSkins.Add(source);
            return source;
        }

        private ImportedSkin importSkin(string label, Func<MemoryStream> createArchive)
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
            AddStep($"resolve {label} import", () => imported.Info = imported.ImportTask.GetAwaiter().GetResult());

            return imported;
        }

        private void selectImported(string label, ImportedSkin imported, Action? afterSelect = null)
        {
            AddStep($"select {label}", () =>
            {
                skinManager.CurrentSkinInfo.Value = imported.Info;
                imported.Skin = skinManager.CurrentSkin.Value;
                afterSelect?.Invoke();
            });

            AddUntilStep($"wait for {label} selection", () =>
                imported.Info != null
                && skinManager.CurrentSkinInfo.Value.ID == imported.Info.ID
                && skinManager.CurrentSkin.Value.SkinInfo.ID == imported.Info.ID
                && skinManager.CurrentSkin.Value is BmsLegacySkin);
        }

        private void assertNoteComponentFailureReturnsNull(string label, BmsNoteSkinElements element)
        {
            Drawable? resolved = null;

            AddStep($"resolve {label} without throw", () =>
            {
                Assert.DoesNotThrow(() => resolved = resolveNoteComponent(element));
            });
            AddAssert($"{label} inherits", () => resolved, () => Is.Null);
        }

        private Drawable? resolveOrdinaryNote()
            => resolveNoteComponent(BmsNoteSkinElements.Note);

        private Drawable? resolveNoteComponent(BmsNoteSkinElements element, int laneIndex = 1, bool isScratch = false, BmsKeymode keymode = BmsKeymode.Key7K)
            => new BmsSkinTransformer(skinManager.CurrentSkin.Value).GetDrawableComponent(new BmsNoteSkinLookup(element, laneIndex, isScratch, keymode));

        private static Task<Drawable?> resolveWithCancellation(BmsLegacySkin skin, BmsNoteSkinElements element, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                using (BmsManagedPackageNoteLoadContext.Enter(cancellationToken))
                {
                    return new BmsSkinTransformer(skin).GetDrawableComponent(createLookup(element));
                }
            });

        private static void assertProtectedFallback(Drawable? drawable, BmsNoteSkinElements element)
        {
            Assert.That(drawable, Is.TypeOf(getProtectedFallbackDrawableType(element)));

            if (element == BmsNoteSkinElements.LongNoteTail)
                Assert.That(drawable!.Alpha, Is.Zero, "The protected optional-tail fallback must remain transparent.");

            if (element == BmsNoteSkinElements.LongNoteBody)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(drawable!.Width, Is.EqualTo(0.5775f).Within(0.0001f));
                    Assert.That(drawable.Alpha, Is.EqualTo(0.8f).Within(0.0001f), "The protected critical-body fallback must remain visible.");
                });
            }
        }

        private static Type getProtectedFallbackDrawableType(BmsNoteSkinElements element)
            => element switch
            {
                BmsNoteSkinElements.Note => typeof(DefaultBmsNoteDisplay),
                BmsNoteSkinElements.LongNoteHead => typeof(DefaultBmsLongNoteHeadDisplay),
                BmsNoteSkinElements.LongNoteBody => typeof(DefaultBmsLongNoteBodyDisplay),
                BmsNoteSkinElements.LongNoteTail => typeof(DefaultBmsLongNoteTailDisplay),
                _ => throw new ArgumentOutOfRangeException(nameof(element), element, "No protected fallback exists for this note element."),
            };

        private static Type getSourceBoundDrawableType(BmsNoteSkinElements element)
            => element == BmsNoteSkinElements.LongNoteBody
                ? typeof(BmsSourceBoundLongNoteBodyDrawable)
                : typeof(BmsSourceBoundNoteDrawable);

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
            => mountProductionHosts(null, lookups);

        private BmsAsyncNoteDrawable[] mountProductionHosts(ISkin? beatmapSkin, params BmsNoteSkinLookup[] lookups)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
            BmsAsyncNoteDrawable[] hosts = lookups.Select(lookup => new BmsAsyncNoteDrawable(lookup)).ToArray();

            Child = new RulesetSkinProvidingContainer(ruleset, beatmap, beatmapSkin)
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

        private DrawableBmsHoldNote mountGameplayHoldNote(
            BmsHoldNote hold,
            BmsLongNoteMode? longNoteMode = null,
            FramedClock? clock = null,
            ISkin? beatmapSkin = null)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
            var drawable = new DrawableBmsHoldNote(hold);

            if (longNoteMode.HasValue)
                drawable.LongNoteModeOverrideForTesting = longNoteMode.Value;

            if (clock != null)
                drawable.Clock = clock;

            drawable.Apply(hold);

            if (clock != null)
            {
                foreach (var nested in drawable.NestedHitObjects)
                    nested.Clock = clock;
            }

            Child = new RulesetSkinProvidingContainer(ruleset, beatmap, beatmapSkin)
            {
                Child = drawable,
            };

            return drawable;
        }

        private static KeyBindingPressEvent<BmsAction> createPressEvent()
            => new KeyBindingPressEvent<BmsAction>(new Framework.Input.States.InputState(), BmsAction.Key1);

        private static KeyBindingReleaseEvent<BmsAction> createReleaseEvent()
            => new KeyBindingReleaseEvent<BmsAction>(new Framework.Input.States.InputState(), BmsAction.Key1);

        private static BmsNoteSkinLookup createLookup(BmsNoteSkinElements element)
            => new BmsNoteSkinLookup(element, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K);

        private static string getDeclarationKey(BmsNoteSkinElements element, int laneIndex = 1)
            => element switch
            {
                BmsNoteSkinElements.Note => $"NoteImage{laneIndex}",
                BmsNoteSkinElements.LongNoteHead => $"NoteImage{laneIndex}H",
                BmsNoteSkinElements.LongNoteBody => $"NoteImage{laneIndex}L",
                BmsNoteSkinElements.LongNoteTail => $"NoteImage{laneIndex}T",
                _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Only supported managed-package note elements have declarations in this fixture."),
            };

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

        private sealed class BeatmapLongNoteHeadProviderOrderSkin : Skin
        {
            public BeatmapLongNoteHeadProviderOrderSkin()
                : base(new SkinInfo(name: nameof(BeatmapLongNoteHeadProviderOrderSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsNoteSkinLookup
                {
                    Element: BmsNoteSkinElements.LongNoteHead,
                    LaneIndex: 1,
                }
                    ? new BeatmapNoteDrawable()
                    : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            {
                if (lookup is BmsSkinConfigurationLookup
                    {
                        Lookup: BmsSkinConfigurationLookups.HoldNoteHeadImage,
                        LaneIndex: 2,
                    }
                    && typeof(TValue) == typeof(string))
                {
                    return SkinUtils.As<TValue>(new Bindable<string>("missing/beatmap-head"));
                }

                return null;
            }
        }

        /// <summary>
        /// Injected runtime-only source used to prove body-provider precedence. It is not a public beatmap-local package fixture.
        /// </summary>
        private sealed class BeatmapLongNoteBodyProviderOrderSkin : Skin
        {
            public BeatmapLongNoteBodyProviderOrderSkin()
                : base(new SkinInfo(name: nameof(BeatmapLongNoteBodyProviderOrderSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsNoteSkinLookup
                {
                    Element: BmsNoteSkinElements.LongNoteBody,
                    LaneIndex: 1,
                }
                    ? new BeatmapNoteDrawable()
                    : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            {
                if (lookup is BmsSkinConfigurationLookup
                    {
                        Lookup: BmsSkinConfigurationLookups.HoldNoteBodyImage,
                        LaneIndex: 2,
                    }
                    && typeof(TValue) == typeof(string))
                {
                    return SkinUtils.As<TValue>(new Bindable<string>("missing/beatmap-body"));
                }

                return null;
            }
        }

        /// <summary>
        /// Injected runtime-only source used to prove beatmap-provider precedence. This is intentionally not a fixture
        /// for a user-authored beatmap-local package format, because no such production producer exists yet.
        /// </summary>
        private sealed class BeatmapLongNoteTailProviderOrderSkin : Skin
        {
            public BeatmapLongNoteTailProviderOrderSkin()
                : base(new SkinInfo(name: nameof(BeatmapLongNoteTailProviderOrderSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsNoteSkinLookup
                {
                    Element: BmsNoteSkinElements.LongNoteTail,
                    LaneIndex: 1,
                }
                    ? new BeatmapNoteDrawable()
                    : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            {
                if (lookup is BmsSkinConfigurationLookup
                    {
                        Lookup: BmsSkinConfigurationLookups.HoldNoteTailImage,
                        LaneIndex: 2,
                    }
                    && typeof(TValue) == typeof(string))
                {
                    return SkinUtils.As<TValue>(new Bindable<string>("missing/beatmap-tail"));
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

        private sealed class BlockingLongNoteBodySkin : Skin
        {
            public readonly ManualResetEventSlim LookupEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseLookup = new ManualResetEventSlim();

            public bool LookupWasOnUpdateThread { get; private set; }

            public BlockingLongNoteBodySkin()
                : base(new SkinInfo(name: nameof(BlockingLongNoteBodySkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is not BmsNoteSkinLookup { Element: BmsNoteSkinElements.LongNoteBody })
                    return null;

                LookupWasOnUpdateThread = ThreadSafety.IsUpdateThread;
                LookupEntered.Set();
                ReleaseLookup.Wait(TimeSpan.FromSeconds(10));
                return new BmsSourceBoundLongNoteBodyDrawable(
                    new Box { RelativeSizeAxes = Axes.Both },
                    width: 0.4f);
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class BlockingNoteSkin : Skin
        {
            public readonly ManualResetEventSlim LookupEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseLookup = new ManualResetEventSlim();

            private readonly string tag;
            private readonly BmsNoteSkinElements element;

            public bool LookupWasOnUpdateThread { get; private set; }
            public Action? BeforeReturn { get; set; }
            public ReloadedNoteDrawable? LastDrawable { get; private set; }

            public BlockingNoteSkin(string tag = "reloaded", BmsNoteSkinElements element = BmsNoteSkinElements.Note)
                : base(new SkinInfo(name: nameof(BlockingNoteSkin)), null)
            {
                this.tag = tag;
                this.element = element;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is not BmsNoteSkinLookup noteLookup || noteLookup.Element != element)
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
