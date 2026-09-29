// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    public partial class TestSceneManiaHitObjectSamples : HitObjectSampleTest
    {
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();
        protected override IResourceStore<byte[]> RulesetResources => new DllResourceStore(Assembly.GetAssembly(typeof(TestSceneManiaHitObjectSamples)));

        /// <summary>
        /// Tests that when a normal sample bank is used, the normal hitsound will be looked up.
        /// </summary>
        [Test]
        public void TestManiaHitObjectNormalSampleBank()
        {
            const string expected_sample = "normal-hitnormal2";

            SetupSkins(expected_sample, expected_sample);

            CreateTestWithBeatmap("mania-hitobject-beatmap-normal-sample-bank.osu");

            AssertBeatmapLookup(expected_sample);
        }

        /// <summary>
        /// Tests that when a custom sample bank is used, layered hitsounds are not played
        /// (only the sample from the custom bank is looked up).
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TestManiaHitObjectCustomSampleBank(bool automaticBmsKeysound)
        {
            const string expected_sample = "normal-hitwhistle2";
            const string unwanted_sample = "normal-hitnormal2";

            AddStep("set BMS-only automatic keysound preference", () =>
                ((ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!)
                .SetValue(ManiaRulesetSetting.AutoKeysoundForBms, automaticBmsKeysound));

            SetupSkins(expected_sample, unwanted_sample);

            CreateTestWithBeatmap("mania-hitobject-beatmap-custom-sample-bank.osu");

            AssertBeatmapLookup(expected_sample);
            AssertNoLookup(unwanted_sample);
            AddAssert("native mania does not host a BMS keysound store",
                () => !Player.ChildrenOfType<Drawable>().OfType<IManiaKeysoundStore>().Any());
            AddAssert("native mania note still scores", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.EqualTo(1));
        }

        [Test]
        public void TestConvertHitObjectCustomSampleBank()
        {
            const string beatmap_sample = "normal-hitwhistle2";
            const string user_skin_sample = "normal-hitnormal";

            SetupSkins(beatmap_sample, user_skin_sample);

            CreateTestWithBeatmap("convert-beatmap-custom-sample-bank.osu");

            AssertBeatmapLookup(beatmap_sample);
            AssertUserLookup(user_skin_sample);
        }
    }
}
