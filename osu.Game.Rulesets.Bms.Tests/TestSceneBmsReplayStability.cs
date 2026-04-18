// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReplayStability : ReplayStabilityTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestRegularLaneReplayRoundTripsThroughLegacyEncoding()
        {
            var (sourceBeatmap, playableBeatmap) = createBeatmapPairFromText(@"
#TITLE Replay Stability Key Stub
#BPM 60
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#00211:BB00
", "replay-stability-key.bme");

            RunTest(
                sourceBeatmap,
                createReplay(getReplayNoteTime(playableBeatmap), BmsAction.Key1),
                getExpectedResults(playableBeatmap, HitResult.Perfect));
        }

        [Test]
        public void TestScratchReplayRoundTripsThroughLegacyEncoding()
        {
            var (sourceBeatmap, playableBeatmap) = createBeatmapPairFromText(@"
#TITLE Replay Stability Scratch Stub
#BPM 60
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVDD scratch.wav
#00216:DD00
", "replay-stability-scratch.bme");

            RunTest(
                sourceBeatmap,
                createReplay(getReplayNoteTime(playableBeatmap), BmsAction.Scratch1),
                getExpectedResults(playableBeatmap, HitResult.Perfect));
        }

        private static Replay createReplay(double noteTime, BmsAction action)
            => new Replay
            {
                Frames =
                {
                    new BmsReplayFrame(0),
                    new BmsReplayFrame(noteTime, action),
                    new BmsReplayFrame(noteTime + 20),
                }
            };

        private (BmsDecodedBeatmap SourceBeatmap, BmsBeatmap PlayableBeatmap) createBeatmapPairFromText(string text, string path)
        {
            var decodedChart = decoder.DecodeText(text, path);
            var sourceBeatmap = new BmsDecodedBeatmap(decodedChart)
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };

            var playableBeatmap = (BmsBeatmap)new BmsBeatmapConverter(sourceBeatmap, new BmsRuleset()).Convert();

            return (sourceBeatmap, playableBeatmap);
        }

        private static double getReplayNoteTime(BmsBeatmap playableBeatmap)
            => playableBeatmap.HitObjects.OfType<BmsHitObject>().Single().StartTime;

        private static HitResult[] getExpectedResults(BmsBeatmap playableBeatmap, HitResult expectedNoteResult)
        {
            int laneCount = BmsLaneLayout.CreateFor(playableBeatmap).Lanes.Count;
            int barLineJudgements = playableBeatmap.MeasureStartTimes.Count * laneCount;
            int backgroundJudgements = playableBeatmap.HitObjects.Count(hitObject => hitObject is not BmsHitObject);

            return Enumerable.Repeat(HitResult.IgnoreHit, barLineJudgements + backgroundJudgements)
                             .Append(expectedNoteResult)
                             .ToArray();
        }
    }
}
