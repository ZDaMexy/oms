// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsBgaPanelLayout : OsuTestScene
    {
        private DefaultBmsBgaPanelDisplay panel = null!;

        [Test]
        public void Test14KMirrorsBgaIntoFourCorners()
        {
            loadPanel(BmsKeymode.Key14K);
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("BGA mirrored into four corners", () => panel.ChildrenOfType<BmsBgaPlayer>().Count(), () => Is.EqualTo(4));
        }

        [Test]
        public void TestSinglePlayUsesOneCorner()
        {
            loadPanel(BmsKeymode.Key7K);
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("single BGA corner", () => panel.ChildrenOfType<BmsBgaPlayer>().Count(), () => Is.EqualTo(1));
        }

        private void loadPanel(BmsKeymode keymode) => AddStep($"load {keymode} BGA panel", () =>
        {
            var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } };
            beatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, LaneIndex = 1 });

            var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;

            panel = new DefaultBmsBgaPanelDisplay();

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(GameplayState), new GameplayState(beatmap, new BmsRuleset())),
                    (typeof(BmsRulesetConfigManager), config),
                },
                Child = panel,
            };

            // A non-empty timeline makes the panel mount a BmsBgaPlayer per corner (one for single play, four for 14K).
            panel.SetBgaSource(new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "x.png", false) }, BmsPoorBgaMode.Default);
            panel.SetLayout(BmsBgaPlacement.TopRight);
        });
    }
}
