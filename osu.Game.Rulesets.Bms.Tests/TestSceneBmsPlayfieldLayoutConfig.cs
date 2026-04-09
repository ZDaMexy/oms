// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPlayfieldLayoutConfig : OsuTestScene
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        private TestableDrawableBmsRuleset drawableRuleset = null!;

        [Test]
        public void TestConfiguredLaneSpacingAppliesToPlayfieldLayout()
        {
            setupScene(laneSpacing: 0.08);

            AddAssert("profile lane spacing matches config", () => drawableRuleset.Playfield.LayoutProfile.NormalLaneRelativeSpacing, () => Is.EqualTo(0.08f).Within(0.0001f));
            AddAssert("normal-lane spacing matches config", () => drawableRuleset.Playfield.LaneLayout.Lanes[2].RelativeSpacingBefore, () => Is.EqualTo(0.08f).Within(0.0001f));
            AddAssert("normal lane gap exists", () => drawableRuleset.Playfield.Lanes[2].X > drawableRuleset.Playfield.Lanes[1].X + drawableRuleset.Playfield.Lanes[1].Width);
        }

        [Test]
        public void TestConfiguredLaneWidthAppliesToPlayfieldLayout()
        {
            setupScene(laneWidth: 1.5);

            AddAssert("profile lane width matches config", () => drawableRuleset.Playfield.LayoutProfile.NormalLaneRelativeWidth, () => Is.EqualTo(1.5f).Within(0.0001f));
            AddAssert("normal lane width matches layout", () => drawableRuleset.Playfield.LaneLayout.Lanes[1].RelativeWidth, () => Is.EqualTo(1.5f).Within(0.0001f));
            AddAssert("regular lane becomes wider than scratch lane", () => drawableRuleset.Playfield.Lanes[1].Width > drawableRuleset.Playfield.Lanes[0].Width);
            AddAssert("drawable widths follow configured ratio", () => drawableRuleset.Playfield.Lanes[1].Width / drawableRuleset.Playfield.Lanes[0].Width, () => Is.EqualTo(1.5f / drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeWidth).Within(0.0001f));
        }

        [Test]
        public void TestConfiguredPlayfieldWidthAppliesToPlayfieldLayout()
        {
            setupScene(playfieldWidth: 0.64);

            AddAssert("profile playfield width matches config", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldWidth, () => Is.EqualTo(0.64f).Within(0.0001f));
            AddAssert("lane span width matches config", () => getLaneSpanWidth(drawableRuleset.Playfield) / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Width, () => Is.EqualTo(0.64f).Within(0.01f));
        }

        [Test]
        public void TestConfiguredPlayfieldHeightAppliesToPlayfieldLayout()
        {
            setupScene(playfieldHeight: 0.72);

            AddAssert("profile playfield height matches config", () => drawableRuleset.Playfield.LayoutProfile.PlayfieldHeight, () => Is.EqualTo(0.72f).Within(0.0001f));
            AddAssert("lane heights match config", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.ScreenSpaceDrawQuad.Height / drawableRuleset.Playfield.ScreenSpaceDrawQuad.Height - 0.72f) <= 0.01f));
        }

        [Test]
        public void TestConfiguredScratchSpacingAppliesToPlayfieldLayout()
        {
            setupScene(scratchLaneSpacing: 0.24);

            AddAssert("profile spacing matches config", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeSpacing, () => Is.EqualTo(0.24f).Within(0.0001f));
            AddAssert("lane spacing matches config", () => drawableRuleset.Playfield.LaneLayout.Lanes[1].RelativeSpacingBefore, () => Is.EqualTo(0.24f).Within(0.0001f));
            AddAssert("scratch gap exists", () => drawableRuleset.Playfield.Lanes[1].X > drawableRuleset.Playfield.Lanes[0].Width);
        }

        [Test]
        public void TestZeroConfiguredScratchSpacingRemovesGap()
        {
            setupScene(scratchLaneSpacing: 0.0);

            AddAssert("profile spacing is zero", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeSpacing, () => Is.EqualTo(0f).Within(0.0001f));
            AddAssert("lane spacing is zero", () => drawableRuleset.Playfield.LaneLayout.Lanes[1].RelativeSpacingBefore, () => Is.EqualTo(0f).Within(0.0001f));
            AddAssert("scratch gap removed", () => Math.Abs(drawableRuleset.Playfield.Lanes[1].X - drawableRuleset.Playfield.Lanes[0].Width) <= 0.0001f);
        }

        [Test]
        public void TestConfiguredScratchWidthRatioAppliesToPlayfieldLayout()
        {
            setupScene(scratchLaneWidthRatio: 1.6);

            AddAssert("profile width ratio matches config", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeWidth, () => Is.EqualTo(1.6f).Within(0.0001f));
            AddAssert("scratch lane wider than regular lane", () => drawableRuleset.Playfield.Lanes[0].Width > drawableRuleset.Playfield.Lanes[1].Width);
            AddAssert("scratch width ratio applied to layout", () => drawableRuleset.Playfield.Lanes[0].Width / drawableRuleset.Playfield.Lanes[1].Width, () => Is.EqualTo(1.6f).Within(0.0001f));
        }

        [Test]
        public void TestConfiguredScratchWidthRatioCanCollapseToRegularWidth()
        {
            setupScene(scratchLaneWidthRatio: 1.0);

            AddAssert("profile width ratio is one", () => drawableRuleset.Playfield.LayoutProfile.ScratchLaneRelativeWidth, () => Is.EqualTo(1.0f).Within(0.0001f));
            AddAssert("scratch lane width matches regular lane", () => Math.Abs(drawableRuleset.Playfield.Lanes[0].Width - drawableRuleset.Playfield.Lanes[1].Width) <= 0.0001f);
        }

        [Test]
        public void TestConfiguredHitTargetHeightAppliesToPlayfieldLayout()
        {
            setupScene(hitTargetHeight: 24.0);

            AddAssert("profile hit target height matches config", () => drawableRuleset.Playfield.LayoutProfile.HitTargetHeight, () => Is.EqualTo(24f).Within(0.0001f));
            AddAssert("lane hit target heights match config", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.HitTarget.Height - 24f) <= 0.0001f));
        }

        [Test]
        public void TestConfiguredHitTargetBarHeightAppliesToPlayfieldLayout()
        {
            setupScene(hitTargetBarHeight: 9.5);

            AddAssert("profile hit target bar height matches config", () => drawableRuleset.Playfield.LayoutProfile.HitTargetBarHeight, () => Is.EqualTo(9.5f).Within(0.0001f));
            AddAssert("lane hit target bar heights match config", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(getDefaultHitTargetDisplay(lane).BarHeight - 9.5f) <= 0.0001f));
        }

        [Test]
        public void TestConfiguredHitTargetLineHeightAppliesToPlayfieldLayout()
        {
            setupScene(hitTargetLineHeight: 4.5);

            AddAssert("profile hit target line height matches config", () => drawableRuleset.Playfield.LayoutProfile.HitTargetLineHeight, () => Is.EqualTo(4.5f).Within(0.0001f));
            AddAssert("lane hit target line heights match config", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(getDefaultHitTargetDisplay(lane).LineHeight - 4.5f) <= 0.0001f));
            AddAssert("focus edge heights track line height", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(getDefaultHitTargetDisplay(lane).FocusEdgeHeight - 4.5f) <= 0.0001f));
        }

        [Test]
        public void TestConfiguredHitTargetGlowRadiusAppliesToPlayfieldLayout()
        {
            setupScene(hitTargetGlowRadius: 8.0);

            AddAssert("profile hit target glow radius matches config", () => drawableRuleset.Playfield.LayoutProfile.HitTargetGlowRadius, () => Is.EqualTo(8f).Within(0.0001f));
            AddAssert("lane hit target glow radii match config", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(getDefaultHitTargetDisplay(lane).GlowRadius - 8f) <= 0.0001f));
        }

        [Test]
        public void TestConfiguredHitTargetVerticalOffsetMovesHitLine()
        {
            setupScene(hitTargetVerticalOffset: 48.0);

            AddAssert("profile hit target offset matches config", () => drawableRuleset.Playfield.LayoutProfile.HitTargetVerticalOffset, () => Is.EqualTo(48f).Within(0.0001f));
            AddAssert("lane hit target bottoms move off playfield edge", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.ScreenSpaceDrawQuad.BottomLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y - 48f) <= 1f));
            AddAssert("scrolling container edge matches receptor", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.HitObjectContainer.ScreenSpaceDrawQuad.BottomLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y) <= 1f));
        }

        [Test]
        public void TestConfiguredHitTargetVerticalOffsetRespectsReverseDirection()
        {
            setupScene(scrollDirection: ScrollingDirection.Up, hitTargetVerticalOffset: 36.0);

            AddAssert("lane hit target tops move off playfield edge", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.HitTarget.ScreenSpaceDrawQuad.TopLeft.Y - lane.ScreenSpaceDrawQuad.TopLeft.Y - 36f) <= 1f));
            AddAssert("reverse scrolling container edge matches receptor", () => drawableRuleset.Playfield.Lanes.All(lane => Math.Abs(lane.HitObjectContainer.ScreenSpaceDrawQuad.TopLeft.Y - lane.HitTarget.ScreenSpaceDrawQuad.TopLeft.Y) <= 1f));
        }

        [Test]
        public void TestConfiguredBarLineHeightAppliesToPlayfieldLayout()
        {
            setupScene(barLineHeight: 4.5);

            AddAssert("profile bar line height matches config", () => drawableRuleset.Playfield.LayoutProfile.BarLineHeight, () => Is.EqualTo(4.5f).Within(0.0001f));
            AddAssert("bar lines exist", () => drawableRuleset.Playfield.Lanes.SelectMany(lane => lane.AllHitObjects.OfType<DrawableBmsBarLine>()).Any());
            AddAssert("bar line heights match config", () => drawableRuleset.Playfield.Lanes.SelectMany(lane => lane.AllHitObjects.OfType<DrawableBmsBarLine>()).All(barLine => Math.Abs(barLine.Height - 4.5f) <= 0.0001f));
        }

        private void setupScene(ScrollingDirection? scrollDirection = null, double? playfieldWidth = null, double? playfieldHeight = null, double? laneSpacing = null, double? laneWidth = null, double? scratchLaneSpacing = null, double? scratchLaneWidthRatio = null, double? hitTargetHeight = null, double? hitTargetBarHeight = null, double? hitTargetLineHeight = null, double? hitTargetGlowRadius = null, double? hitTargetVerticalOffset = null, double? barLineHeight = null)
        {
            AddStep($"configure layout bridge", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;

                config.SetValue(BmsRulesetSetting.ScrollDirection, scrollDirection ?? ScrollingDirection.Down);
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

                Child = drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), createPlayableBeatmap())
                {
                    RelativeSizeAxes = Axes.Both,
                };
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

        private static DefaultBmsHitTargetDisplay getDefaultHitTargetDisplay(BmsLane lane)
            => lane.HitTarget.ChildrenOfType<DefaultBmsHitTargetDisplay>().Single();

        private static float getLaneSpanWidth(BmsPlayfield playfield)
            => playfield.Lanes.Last().ScreenSpaceDrawQuad.TopRight.X - playfield.Lanes.First().ScreenSpaceDrawQuad.TopLeft.X;

        private sealed partial class TestableDrawableBmsRuleset : DrawableBmsRuleset
        {
            public TestableDrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }
        }
    }
}
