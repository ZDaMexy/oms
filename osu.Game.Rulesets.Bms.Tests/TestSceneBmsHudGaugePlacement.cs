// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
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
        private BmsGameplayLayoutProvider layoutProvider = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K },
            };
            beatmap.Difficulty.CircleSize = BmsRuleset.GetKeyCount(BmsKeymode.Key7K);
            beatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, LaneIndex = 1, Keymode = BmsKeymode.Key7K });
            layoutProvider = new BmsGameplayLayoutProvider(beatmap);
            layoutProvider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());

            config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
            config.SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.Center);

            gauge = new Container
            {
                Child = new Box { RelativeSizeAxes = Axes.X, Height = 28 },
            };

            var gaugeProcessor = new BmsGaugeProcessor(0);
            gaugeProcessor.ApplyBeatmap(beatmap);

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(GameplayState), new GameplayState(beatmap, new BmsRuleset())),
                    (typeof(HealthProcessor), gaugeProcessor),
                    (typeof(BmsGameplayLayoutProvider), layoutProvider),
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
            AddUntilStep("gauge positioned", () => gauge.RelativeSizeAxes == Axes.Both);

            AddAssert("gauge width matches playfield strip",
                () => gauge.Width,
                () => Is.EqualTo(layoutProvider.Current.GaugeRect.Width).Within(0.0001f));
            AddAssert("gauge sits below the judgement line",
                () => gauge.Y >= layoutProvider.Current.PlayfieldRect.Bottom);
            AddAssert("gauge uses relative positioning", () => gauge.RelativePositionAxes == Axes.Both);
            AddAssert("gauge uses exact snapshot origin", () => gauge.Anchor == Anchor.TopLeft && gauge.Origin == Anchor.TopLeft);
            AddAssert("gauge x uses exact snapshot", () => gauge.X, () => Is.EqualTo(layoutProvider.Current.GaugeRect.X).Within(0.0001f));
        }

        [Test]
        public void TestCommittedSnapshotDoesNotReactToLiveStyleMutation()
        {
            float committedX = 0;

            AddUntilStep("gauge positioned", () => gauge.RelativeSizeAxes == Axes.Both);
            AddStep("capture committed x", () => committedX = gauge.X);
            AddStep("attempt live 1P style mutation", () => config.SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.P1));
            AddAssert("committed exact snapshot remains frozen", () => gauge.X, () => Is.EqualTo(committedX).Within(0.0001f));
        }

        [Test]
        public void TestCustomHudCarrierReceivesAndAppliesExactSnapshot()
        {
            TestCarrierHudLayout customLayout = null!;
            BmsHudLayoutSnapshotCarrier carrier = null!;

            AddStep("mount custom HUD carrier", () =>
            {
                customLayout = new TestCarrierHudLayout();
                carrier = new BmsHudLayoutSnapshotCarrier(customLayout, customLayout);
                carrier.SetComponents(null, new Container(), new TestComboCounter());

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(BmsGameplayLayoutProvider), layoutProvider),
                    },
                    Child = carrier,
                };
            });

            AddUntilStep("custom HUD carrier loaded", () => customLayout?.IsLoaded == true && customLayout.LayoutSnapshot != null);
            AddAssert("carrier and custom HUD retain exact typed snapshot", () =>
                ReferenceEquals(carrier.LayoutSnapshot, layoutProvider.Current)
                && ReferenceEquals(customLayout.LayoutSnapshot, layoutProvider.Current));
            AddAssert("custom HUD receives snapshot exactly once", () => customLayout.LayoutInitialisationCount, () => Is.EqualTo(1));
            AddAssert("custom HUD component callback observes snapshot first", () => customLayout.ComponentsObservedSnapshot);
            AddAssert("custom gauge uses exact solved rect", () => customLayout.GaugeRect, () => Is.EqualTo(layoutProvider.Current.GaugeRect));
            AddAssert("custom combo uses exact solved rect", () => customLayout.ComboRect, () => Is.EqualTo(layoutProvider.Current.ComboRect));
        }

        [Test]
        public void TestComboCentredOnPlayfield()
        {
            AddUntilStep("combo positioned", () => combo.Origin == Anchor.Centre);
            AddAssert("combo uses relative positioning", () => combo.RelativePositionAxes == Axes.Both);
            AddAssert("combo at exact snapshot horizontal centre", () => Math.Abs(combo.X - (layoutProvider.Current.ComboRect.X + layoutProvider.Current.ComboRect.Width / 2)) <= 0.0001f);
            AddAssert("combo at exact snapshot vertical centre", () => Math.Abs(combo.Y - (layoutProvider.Current.ComboRect.Y + layoutProvider.Current.ComboRect.Height / 2)) <= 0.0001f);
        }

        private partial class TestComboCounter : ComboCounter
        {
        }

        private sealed partial class TestCarrierHudLayout : Container, IBmsHudLayoutDisplay
        {
            private Drawable gaugeBar = null!;
            private ComboCounter comboCounter = null!;

            public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

            public int LayoutInitialisationCount { get; private set; }

            public bool ComponentsObservedSnapshot { get; private set; }

            public GameplaySkinLayoutRect GaugeRect { get; private set; }

            public GameplaySkinLayoutRect ComboRect { get; private set; }

            public void SetComponents(Drawable? wrappedHud, Drawable newGaugeBar, ComboCounter newComboCounter)
            {
                ComponentsObservedSnapshot = LayoutSnapshot != null;
                gaugeBar = newGaugeBar;
                comboCounter = newComboCounter;
                AddRange(new Drawable[] { gaugeBar, comboCounter });

                gaugeBar.RelativePositionAxes = gaugeBar.RelativeSizeAxes = Axes.Both;
                gaugeBar.Position = new osuTK.Vector2(GaugeRect.X, GaugeRect.Y);
                gaugeBar.Size = new osuTK.Vector2(GaugeRect.Width, GaugeRect.Height);
                comboCounter.RelativePositionAxes = Axes.Both;
                comboCounter.Position = new osuTK.Vector2(ComboRect.X + ComboRect.Width / 2, ComboRect.Y + ComboRect.Height / 2);
            }

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
            {
                LayoutInitialisationCount++;
                LayoutSnapshot = snapshot;
                GaugeRect = snapshot.GaugeRect;
                ComboRect = snapshot.ComboRect;
            }
        }
    }
}
