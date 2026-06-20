// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Skinning;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsHudGaugePlacement : OsuTestScene
    {
        private DefaultBmsHudLayoutDisplay layout = null!;
        private Container gauge = null!;
        private TestComboCounter combo = null!;
        private BmsBeatmap beatmap = null!;
        private BmsRulesetConfigManager config = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K },
            };
            beatmap.Difficulty.CircleSize = BmsRuleset.GetKeyCount(BmsKeymode.Key7K);
            beatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, LaneIndex = 1 });

            config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
            config.SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.Center);

            // Mirror BmsGaugeBar's axis shape (AutoSizeAxes.Y) so the test exercises the exact RelativeSizeAxes.X +
            // AutoSizeAxes.Y combination applyGaugePlacement applies to the real gauge.
            gauge = new Container
            {
                AutoSizeAxes = Axes.Y,
                Child = new Box { RelativeSizeAxes = Axes.X, Height = 28 },
            };

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(GameplayState), new GameplayState(beatmap, new BmsRuleset())),
                    (typeof(HealthProcessor), new BmsGaugeProcessor(0)),
                },
                Child = layout = new DefaultBmsHudLayoutDisplay { RelativeSizeAxes = Axes.Both },
            };

            layout.SetComponents(null, gauge, combo = new TestComboCounter());
        });

        [Test]
        public void TestRealGaugeLoadsAndIsVisible()
        {
            BmsGaugeBar realGauge = null!;

            AddStep("swap in real gauge", () =>
            {
                realGauge = new BmsGaugeBar();
                layout.SetComponents(null, realGauge, new TestComboCounter());
            });

            AddUntilStep("real gauge loaded", () => realGauge.IsLoaded);
            AddAssert("real gauge visible", () => realGauge.Alpha > 0 && realGauge.DrawWidth > 0 && realGauge.DrawHeight > 0);
        }

        [Test]
        public void TestRealGaugeVisibleAlongsideStrippedWrappedHud()
        {
            BmsGaugeBar realGauge = null!;

            AddStep("strip wrapped HUD + set components", () =>
            {
                // Production-shaped wrapped HUD (DefaultSkinComponentsContainer like LegacySkin) carrying the default
                // combo + leaderboard, then stripped exactly like BmsSkinTransformer.stripDefaultHudElements.
                var wrappedHud = new DefaultSkinComponentsContainer(container =>
                {
                    foreach (var d in container.OfType<ISerialisableDrawable>())
                        d.UsesFixedAnchor = true;
                })
                {
                    new LegacyDefaultComboCounter(),
                    new DrawableGameplayLeaderboard(),
                };

                foreach (var d in wrappedHud.Children.Where(c => c is ComboCounter or LegacyDefaultComboCounter or DrawableGameplayLeaderboard).ToArray())
                    wrappedHud.Remove(d, true);

                realGauge = new BmsGaugeBar();
                layout.SetComponents(wrappedHud, realGauge, new TestComboCounter());
            });

            AddUntilStep("real gauge loaded", () => realGauge.IsLoaded);
            AddAssert("real gauge visible", () => realGauge.Alpha > 0 && realGauge.DrawWidth > 0 && realGauge.DrawHeight > 0);
        }

        [Test]
        public void TestGaugeMatchesPlayfieldStripBelowJudgementLine()
        {
            AddUntilStep("gauge positioned", () => gauge.RelativeSizeAxes == Axes.X);

            AddAssert("gauge width matches playfield strip",
                () => gauge.Width,
                () => Is.EqualTo(BmsLaneLayout.CreateFor(beatmap).Profile.PlayfieldWidth).Within(0.0001f));
            AddAssert("gauge sits below the judgement line",
                () => gauge.Y >= BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT);
            AddAssert("gauge uses relative positioning", () => gauge.RelativePositionAxes == Axes.Both);
            AddAssert("centre style centres the gauge", () => gauge.Anchor == Anchor.TopCentre && gauge.Origin == Anchor.TopCentre);
        }

        [Test]
        public void TestGaugeMirrorsPlayfieldSideAnchoring()
        {
            var keymode = BmsLaneLayout.CreateFor(beatmap).Keymode;
            var expectedAnchor = BmsPlayfieldStyle.P1.GetAppliedStyle(keymode) switch
            {
                BmsPlayfieldStyle.P1 => Anchor.TopLeft,
                BmsPlayfieldStyle.P2 => Anchor.TopRight,
                _ => Anchor.TopCentre,
            };

            AddUntilStep("gauge positioned", () => gauge.RelativeSizeAxes == Axes.X);
            AddStep("set 1P style", () => config.SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.P1));
            AddUntilStep("gauge re-anchors to playfield side", () => gauge.Anchor == expectedAnchor && gauge.Origin == expectedAnchor);
        }

        [Test]
        public void TestComboCentredOnPlayfield()
        {
            AddUntilStep("combo positioned", () => combo.Origin == Anchor.Centre);
            AddAssert("combo uses relative positioning", () => combo.RelativePositionAxes == Axes.Both);
            AddAssert("combo at playfield horizontal centre (centre style)", () => Math.Abs(combo.X - 0.5f) <= 0.0001f);
            AddAssert("combo at playfield vertical centre", () => Math.Abs(combo.Y - BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT / 2f) <= 0.0001f);
        }

        private partial class TestComboCounter : ComboCounter
        {
        }
    }
}
