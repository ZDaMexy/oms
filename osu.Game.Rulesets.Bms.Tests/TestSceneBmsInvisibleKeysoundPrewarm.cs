// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsInvisibleKeysoundPrewarm : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            const string chart = "#TITLE Invisible-only keysound prewarm\n#BPM 120\n#WAV01 visible.wav\n#WAV02 invisible-only.wav\n#00131:02\n#00811:01\n";
            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(chart, "invisible-prewarm.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };
        }

        [Test]
        public void TestLaneOnlyKeysoundIsPreparedBeforeAnyPlayerInput()
        {
            AddUntilStep("the real player has loaded its BMS playfield", () => Player?.IsLoaded == true
                && Player.DrawableRuleset is DrawableBmsRuleset { IsLoaded: true });
            AddStep("invisible sound remains an armed assignment and is already warmed", () =>
            {
                var ruleset = (DrawableBmsRuleset)Player.DrawableRuleset;
                var beatmap = (BmsBeatmap)ruleset.Beatmap;
                Assert.That(beatmap.LaneKeysoundTimelines.Values.SelectMany(entries => entries)
                    .Any(entry => entry.Sample.Filename == "invisible-only.wav"), Is.True);
                Assert.That(beatmap.HitObjects.OfType<BmsHitObject>()
                    .Any(hitObject => hitObject.KeysoundSample?.Filename == "invisible-only.wav"), Is.False,
                    "Warming the lane resource must not create a visible or automatically sounded note.");

                // Observe the actual production Playfield sample preparation, without invoking GetPooledSample:
                // that API would create the missing pool itself and conceal first-key cold preparation.
                var pools = (Dictionary<ISampleInfo, DrawablePool<PoolableSkinnableSample>>)typeof(Playfield)
                    .GetField("samplePools", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ruleset.Playfield)!;
                string[] warmedFiles = pools.Keys.OfType<BmsKeysoundSampleInfo>().Select(sample => sample.Filename).ToArray();
                Assert.That(warmedFiles, Does.Contain("visible.wav"), "The ordinary visible-note prewarm is the control.");
                Assert.That(warmedFiles, Does.Contain("invisible-only.wav"),
                    "The first empty key press must find the lane-only sample pool ready, rather than prepare it on the gameplay update thread.");
            });
        }
    }
}
