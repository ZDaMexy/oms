// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPlayfieldLayoutConfig : OsuTestScene
    {
        private const float expected_side_anchored_screen_inset_ratio = 0.05f;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        private TestableDrawableBmsRuleset drawableRuleset = null!;

        [Test]
        public void TestStrictProfileIgnoresConfiguredLaneAndPlayfieldGeometryOverrides()
        {
            setupScene(playfieldWidth: 0.64, playfieldHeight: 0.72, laneSpacing: 0.08, laneWidth: 1.5, scratchLaneSpacing: 0.24, scratchLaneWidthRatio: 1.6);

            AddAssert("profile playfield width stays default", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldWidth, () => Is.EqualTo(0.396f).Within(0.0001f));
            AddAssert("profile playfield height stays default", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldHeight, () => Is.EqualTo(0.92f).Within(0.0001f));
            AddAssert("profile lane width stays default", () => drawableRuleset.Playfield.LayoutProfile.NormalLaneRelativeWidth, () => Is.EqualTo(1f).Within(0.0001f));
            AddAssert("profile lane spacing stays default", () => drawableRuleset.Playfield.LayoutProfile.NormalLaneRelativeSpacing, () => Is.EqualTo(0f).Within(0.0001f));
            AddAssert("profile scratch width stays default", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeWidth, () => Is.EqualTo(1.5f).Within(0.0001f));
            AddAssert("profile scratch spacing stays default", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeSpacing, () => Is.EqualTo(0.12f).Within(0.0001f));
            AddAssert("scratch lane stays 1.5x the key lane width", () => drawableRuleset.Playfield.Lanes[0].Width / drawableRuleset.Playfield.Lanes[1].Width, () => Is.EqualTo(1.5f).Within(0.05f));
            AddAssert("lane heights stay default", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.ScreenSpaceDrawQuad.Height / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height - 0.92f) <= 0.01f));
        }

        [Test]
        public void TestStrictProfileIgnoresConfiguredHitTargetGeometryOverrides()
        {
            setupScene(hitTargetHeight: 24.0, hitTargetBarHeight: 9.5, hitTargetLineHeight: 4.5, hitTargetGlowRadius: 8.0, hitTargetVerticalOffset: 48.0, barLineHeight: 4.5);

            AddAssert("profile hit target height stays default", () => drawableRuleset.Playfield.LayoutProfile.HitTargetHeight, () => Is.EqualTo(16f).Within(0.0001f));
            AddAssert("profile hit target bar height stays default", () => drawableRuleset.Playfield.LayoutProfile.HitTargetBarHeight, () => Is.EqualTo(12f).Within(0.0001f));
            AddAssert("profile hit target line height stays default", () => drawableRuleset.Playfield.LayoutProfile.HitTargetLineHeight, () => Is.EqualTo(3f).Within(0.0001f));
            AddAssert("profile hit target glow stays default", () => drawableRuleset.Playfield.LayoutProfile.HitTargetGlowRadius, () => Is.EqualTo(6f).Within(0.0001f));
            AddAssert("profile hit target offset stays default", () => drawableRuleset.Playfield.LayoutProfile.HitTargetVerticalOffset, () => Is.EqualTo(0f).Within(0.0001f));
            AddAssert("profile bar line height stays default", () => drawableRuleset.Playfield.LayoutProfile.BarLineHeight, () => Is.EqualTo(2f).Within(0.0001f));
            AddAssert("lane hit target surfaces stay at exact default", () => drawableRuleset.Playfield.Lanes.All(lane =>
                Math.Abs(lane.HitTarget.ScreenSpaceDrawQuad.Height / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height
                         - drawableRuleset.LayoutSnapshot.HitTargetRect.Height) <= 0.001f));
            AddAssert("lane hit target bottoms stay at playfield edge", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.ScreenSpaceDrawQuad.BottomLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y) <= 1f));
            AddAssert("scrolling container edge matches receptor", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.HitObjectContainer.ScreenSpaceDrawQuad.BottomLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y) <= 1f));
            AddAssert("bar line surfaces stay at exact default", () => drawableRuleset.Playfield.BarLinePlayfields.SelectMany(owner => owner.AllHitObjects.OfType<DrawableBmsBarLine>()).All(barLine =>
                Math.Abs(barLine.Height - drawableRuleset.LayoutSnapshot.ProjectVerticalProfileMetric(2f)) <= 0.0001f));
        }

        [Test]
        public void TestSkinGeometryUsesExactFieldsAndFallsBackInvalidInterfieldValue()
        {
            // Unlike the ruleset-config sliders (ignored above), valid skin fields drive the snapshot. The bar height is
            // deliberately larger than the receptor height and must independently fall back instead of contaminating
            // the other valid fields.
            var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nPlayfieldWidth: 0.5\nPlayfieldHeight: 0.8\nScratchLaneWidth: 2.0\nHitTargetBarHeight: 20\nBarLineHeight: 5\n");

            setupScene(skin: skin);

            AddAssert("profile playfield width from skin", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldWidth, () => Is.EqualTo(0.5f).Within(0.0001f));
            AddAssert("profile playfield height from skin", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldHeight, () => Is.EqualTo(0.8f).Within(0.0001f));
            AddAssert("profile scratch width from skin", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeWidth, () => Is.EqualTo(2.0f).Within(0.0001f));
            AddAssert("invalid hit target bar height falls back", () => drawableRuleset.Playfield.LayoutProfile.HitTargetBarHeight, () => Is.EqualTo(12f).Within(0.0001f));
            AddAssert("invalid field emits stable diagnostic", () => drawableRuleset.LayoutSnapshot.Neutral.Diagnostics.Any(diagnostic => diagnostic.Code == "bms.layout.invalid-hit-target-bar-height"));
            AddAssert("profile bar line height from skin", () => drawableRuleset.Playfield.LayoutProfile.BarLineHeight, () => Is.EqualTo(5f).Within(0.0001f));
            // Unset keys keep their defaults (lane width 1, glow 6, vertical offset locked at 0 for timing).
            AddAssert("unset lane width stays default", () => drawableRuleset.Playfield.LayoutProfile.NormalLaneRelativeWidth, () => Is.EqualTo(1f).Within(0.0001f));
            AddAssert("hit target vertical offset stays locked at 0", () => drawableRuleset.Playfield.LayoutProfile.HitTargetVerticalOffset, () => Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TestLiftModRaisesHitLineByLaneFraction()
        {
            var liftMod = new BmsModLift();
            liftMod.LiftUnits.Value = 250;

            setupScene(mods: new Mod[] { liftMod });

            AddAssert("playfield lift units match mod", () => drawableRuleset.Playfield.LiftUnits.Value, () => Is.EqualTo(250).Within(0.0001f));
            AddAssert("lane hit target bottoms move by quarter lane height", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.ScreenSpaceDrawQuad.BottomLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y - lane.ScreenSpaceDrawQuad.Height * 0.25f) <= 2f));
            AddAssert("scroll ratio reflects lift", () => drawableRuleset.Playfield.ScrollLengthRatio.Value, () => Is.EqualTo(0.75).Within(0.03));
        }

        [Test]
        public void TestP1StyleAnchorsSinglePlayfieldLeft()
        {
            setupScene(playfieldStyle: BmsPlayfieldStyle.P1);

            AddAssert("scratch lane is visual leftmost", () => drawableRuleset.Playfield.Lanes[0].ScreenSpaceDrawQuad.TopLeft.X, () => Is.EqualTo(drawableRuleset.Playfield.Lanes.Min(lane => lane.ScreenSpaceDrawQuad.TopLeft.X)).Within(1f));
            AddAssert("single playfield leaves left screen inset", () => drawableRuleset.Playfield.Lanes.Min(lane => lane.ScreenSpaceDrawQuad.TopLeft.X) - drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft.X, () => Is.EqualTo(drawableRuleset.Playfield.ScreenSpaceDrawQuad.Width * expected_side_anchored_screen_inset_ratio).Within(2f));
        }

        [Test]
        public void TestP2StyleAnchorsSinglePlayfieldRight()
        {
            setupScene(playfieldStyle: BmsPlayfieldStyle.P2);

            AddAssert("scratch lane is visual rightmost", () => drawableRuleset.Playfield.Lanes[0].ScreenSpaceDrawQuad.TopRight.X, () => Is.EqualTo(drawableRuleset.Playfield.Lanes.Max(lane => lane.ScreenSpaceDrawQuad.TopRight.X)).Within(1f));
            AddAssert("single playfield leaves right screen inset", () => drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopRight.X - drawableRuleset.Playfield.Lanes.Max(lane => lane.ScreenSpaceDrawQuad.TopRight.X), () => Is.EqualTo(drawableRuleset.Playfield.ScreenSpaceDrawQuad.Width * expected_side_anchored_screen_inset_ratio).Within(2f));
        }

        [Test]
        public void TestCenterStyleBalancesSinglePlayfieldMargins()
        {
            setupScene(playfieldStyle: BmsPlayfieldStyle.Center);

            AddAssert("scratch lane is visual leftmost", () => drawableRuleset.Playfield.Lanes[0].ScreenSpaceDrawQuad.TopLeft.X, () => Is.EqualTo(drawableRuleset.Playfield.Lanes.Min(lane => lane.ScreenSpaceDrawQuad.TopLeft.X)).Within(1f));
            AddAssert("single playfield stays centered", () =>
            {
                float leftMargin = drawableRuleset.Playfield.Lanes.Min(lane => lane.ScreenSpaceDrawQuad.TopLeft.X) - drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft.X;
                float rightMargin = drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopRight.X - drawableRuleset.Playfield.Lanes.Max(lane => lane.ScreenSpaceDrawQuad.TopRight.X);
                return Math.Abs(leftMargin - rightMargin);
            }, () => Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void TestCenterRightScratchStyleBalancesSinglePlayfieldMargins()
        {
            setupScene(playfieldStyle: BmsPlayfieldStyle.CenterRightScratch);

            AddAssert("scratch lane is visual rightmost", () => drawableRuleset.Playfield.Lanes[0].ScreenSpaceDrawQuad.TopRight.X, () => Is.EqualTo(drawableRuleset.Playfield.Lanes.Max(lane => lane.ScreenSpaceDrawQuad.TopRight.X)).Within(1f));
            AddAssert("single playfield stays centered", () =>
            {
                float leftMargin = drawableRuleset.Playfield.Lanes.Min(lane => lane.ScreenSpaceDrawQuad.TopLeft.X) - drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft.X;
                float rightMargin = drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopRight.X - drawableRuleset.Playfield.Lanes.Max(lane => lane.ScreenSpaceDrawQuad.TopRight.X);
                return Math.Abs(leftMargin - rightMargin);
            }, () => Is.LessThanOrEqualTo(2f));
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void TestHitTargetAndJudgementLineDrawQuadsMatchExactDpiSurface(float dpiScale)
        {
            var environment = new BmsGameplayLayoutEnvironment(
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                16f / 9f,
                dpiScale);

            setupScene(environment: environment);

            AddUntilStep("pre-start exact renderer loaded", () => drawableRuleset.PreStartSpeedPreviewLayoutSnapshot != null);

            AddAssert($"DPI {dpiScale} target draw quads use exact surface", () => drawableRuleset.Playfield.Lanes.All(lane =>
            {
                float rootHeight = drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height;
                float targetHeight = lane.HitTarget.ScreenSpaceDrawQuad.Height / rootHeight;
                float targetTop = (lane.HitTarget.ScreenSpaceDrawQuad.TopLeft.Y - drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft.Y) / rootHeight;
                return Math.Abs(targetHeight - drawableRuleset.LayoutSnapshot.HitTargetRect.Height) <= 0.001f
                       && Math.Abs(targetTop - drawableRuleset.LayoutSnapshot.HitTargetRect.Top) <= 0.001f;
            }));
            AddAssert($"DPI {dpiScale} judgement-line draw quads use exact surface", () => drawableRuleset.Playfield.Lanes.All(lane =>
            {
                DefaultBmsHitTargetDisplay display = lane.HitTarget.ChildrenOfType<DefaultBmsHitTargetDisplay>().Single();
                float rootHeight = drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height;
                float lineHeight = display.LineScreenSpaceHeight / rootHeight;
                float lineTop = (display.LineScreenSpaceTop - drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft.Y) / rootHeight;
                return Math.Abs(lineHeight - drawableRuleset.LayoutSnapshot.JudgementLineRect.Height) <= 0.001f
                       && Math.Abs(lineTop - drawableRuleset.LayoutSnapshot.JudgementLineRect.Top) <= 0.001f;
            }));
            AddAssert($"DPI {dpiScale} bar-line draw quads use snapshot projection", () => drawableRuleset.Playfield.BarLinePlayfields
                .SelectMany(owner => owner.AllHitObjects.OfType<DrawableBmsBarLine>())
                .All(barLine => Math.Abs(barLine.ScreenSpaceDrawQuad.Height / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height
                                         - drawableRuleset.LayoutSnapshot.ProjectVerticalProfileMetric(drawableRuleset.LayoutSnapshot.Profile.BarLineHeight)
                                           * drawableRuleset.LayoutSnapshot.PlayfieldRect.Height) <= 0.001f));
            AddAssert($"DPI {dpiScale} pre-start note draw quad matches target surface", () =>
                Math.Abs(drawableRuleset.PreStartSpeedPreviewNoteScreenSpaceHeight / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height
                         - drawableRuleset.LayoutSnapshot.HitTargetRect.Height) <= 0.001f);
        }

        private void setupScene(BmsPlayfieldStyle? playfieldStyle = null, double? playfieldWidth = null, double? playfieldHeight = null, double? laneSpacing = null, double? laneWidth = null, double? scratchLaneSpacing = null, double? scratchLaneWidthRatio = null, double? hitTargetHeight = null, double? hitTargetBarHeight = null, double? hitTargetLineHeight = null, double? hitTargetGlowRadius = null, double? hitTargetVerticalOffset = null, double? barLineHeight = null, IReadOnlyList<Mod>? mods = null, ISkin? skin = null, BmsGameplayLayoutEnvironment? environment = null)
        {
            AddStep($"configure layout bridge", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;

                config.SetValue(BmsRulesetSetting.PlayfieldStyle, playfieldStyle ?? BmsPlayfieldStyle.Center);
                config.SetValue(BmsRulesetSetting.PlayfieldWidth, playfieldWidth ?? 0.0);
                config.SetValue(BmsRulesetSetting.PlayfieldHeight, playfieldHeight ?? 0.0);
                config.SetValue(BmsRulesetSetting.LaneSpacing, laneSpacing ?? 0.0);
                config.SetValue(BmsRulesetSetting.LaneWidth, laneWidth ?? 1.0);
                config.SetValue(BmsRulesetSetting.ScratchLaneSpacing, scratchLaneSpacing ?? 0.12);
                config.SetValue(BmsRulesetSetting.ScratchLaneWidthRatio, scratchLaneWidthRatio ?? 1.25);
                config.SetValue(BmsRulesetSetting.HitTargetHeight, hitTargetHeight ?? 16.0);
                config.SetValue(BmsRulesetSetting.HitTargetBarHeight, hitTargetBarHeight ?? 12.0);
                config.SetValue(BmsRulesetSetting.HitTargetLineHeight, hitTargetLineHeight ?? 3.0);
                config.SetValue(BmsRulesetSetting.HitTargetGlowRadius, hitTargetGlowRadius ?? 6.0);
                config.SetValue(BmsRulesetSetting.HitTargetVerticalOffset, hitTargetVerticalOffset ?? 0.0);
                config.SetValue(BmsRulesetSetting.BarLineHeight, barLineHeight ?? 2.0);

                drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), createPlayableBeatmap(), mods)
                {
                    RelativeSizeAxes = Axes.Both,
                };
                drawableRuleset.InitialiseCompatibilityLayoutForTesting(playfieldStyle ?? BmsPlayfieldStyle.Center, skin, environment);

                // Wrap in the supplied skin so the playfield resolves its per-keymode geometry overrides; with no skin the
                // ruleset is mounted directly (the default-skin path the rest of the fixture exercises).
                Child = skin == null
                    ? drawableRuleset
                    : new SkinProvidingContainer(skin) { RelativeSizeAxes = Axes.Both, Child = drawableRuleset };
            });

            AddUntilStep("drawable ruleset loaded", () => drawableRuleset?.IsLoaded == true);
        }

        private BmsBeatmap createPlayableBeatmap()
        {
            const string text = @"
#TITLE Layout Config Stub
#BPM 120
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#WAVDD scratch.wav
#00111:BB00
#00112:BB00
#00116:DD00
";

            var decodedChart = decoder.DecodeText(text, "layout-config-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private sealed partial class TestableDrawableBmsRuleset : DrawableBmsRuleset
        {
            public TestableDrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
                : base(ruleset, beatmap, mods)
            {
            }
        }

        private class TestBmsLegacySkin : BmsLegacySkin
        {
            public TestBmsLegacySkin(string ini)
                : base(new SkinInfo { Name = @"test" }, new TestResourceProvider(), new IniStore(ini))
            {
            }
        }

        private class TestResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files { get; } = new ResourceStore<byte[]>();
            public IResourceStore<byte[]> Resources => Files;
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private class IniStore : IResourceStore<byte[]>
        {
            private readonly byte[] data;

            public IniStore(string ini) => data = Encoding.UTF8.GetBytes(ini);

            public byte[] Get(string name) => name == @"skin.ini" ? data : null!;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => name == @"skin.ini" ? new MemoryStream(data) : null!;
            public IEnumerable<string> GetAvailableResources() => new[] { @"skin.ini" };
            public void Dispose() { }
        }
    }
}
