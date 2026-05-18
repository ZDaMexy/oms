// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsAutoplayReplayPlayback : PlayerTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        protected override bool HasCustomSteps => true;

        protected override bool Autoplay => true;

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => createBeatmap();

        [Test]
        public void TestAutoplayReplayCompletesWithPerfectResults()
        {
            CreateTest();

            AddUntilStep("replay handler loaded", () => Player.DrawableRuleset != null && Player.DrawableRuleset.HasReplayLoaded.Value);
            AddUntilStep("autoplay completes", () => Player.ScoreProcessor.HasCompleted.Value);
            AddAssert("non-ignored judgements are perfect", () => Player.Results.Where(result => result.Type != HitResult.IgnoreHit).All(result => result.Type == HitResult.Perfect));
        }

        [Test]
        public void TestAutoplayReplayStillFeedsInputCounter()
        {
            CreateTest();

            AddUntilStep("replay handler loaded", () => Player.DrawableRuleset != null && Player.DrawableRuleset.HasReplayLoaded.Value);
            AddUntilStep("key counter receives replay input", () => Player.HUDOverlay != null && Player.HUDOverlay.InputCountController.Triggers.Any(trigger => trigger.ActivationCount.Value > 0));
        }

        private IBeatmap createBeatmap()
        {
            var beatmap = new BmsDecodedBeatmap(decoder.DecodeText(@"
#TITLE Autoplay Replay Stub
#BPM 120
#RANK 2
#LNTYPE 1
#WAVAA hold-head.wav
#WAVBB hold-tail.wav
#WAVCC scratch.wav
#00151:AA00BB00
#00216:CC00
", "autoplay-replay-stub.bme"))
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };

            return beatmap;
        }
    }
}
