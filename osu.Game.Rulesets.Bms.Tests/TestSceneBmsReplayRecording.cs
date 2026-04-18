// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsReplayRecording : PlayerTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsDecodedBeatmap sourceBeatmap;
        private readonly BmsBeatmap playableBeatmap;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public TestSceneBmsReplayRecording()
        {
            (sourceBeatmap, playableBeatmap) = createBeatmapPairFromText(@"
#TITLE Replay Recording Stub
#BPM 60
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#WAVCC scratch.wav
#WAVDD key2.wav
#00211:BB00
#00316:CC00
#00512:DD00
", "replay-recording-stub.bme");
        }

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => sourceBeatmap;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new OsuTestScene.ClockBackedTestWorkingBeatmap(beatmap, storyboard, new FramedClock(new ManualClock { Rate = 1 }), audioManager);

        [Test]
        public void TestRecordingStoresLaneActionsOnly()
        {
            seekTo(() => getNoteTime(0));
            AddStep("press key1 action", () => Assert.That(getInputManager().TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            seekTo(() => getNoteTime(0) + 20);
            AddStep("release key1 action", () => Assert.That(getInputManager().TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));

            seekTo(() => getMidpointTime(0, 1));
            AddStep("press lane cover focus", () => Assert.That(getInputManager().TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            seekTo(() => getMidpointTime(0, 1) + 20);
            AddStep("release lane cover focus", () => Assert.That(getInputManager().TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));

            seekTo(() => getNoteTime(1));
            AddStep("press scratch action", () => Assert.That(getInputManager().TriggerOmsActionPressed(OmsAction.Key1P_Scratch), Is.True));
            seekTo(() => getNoteTime(1) + 20);
            AddStep("release scratch action", () => Assert.That(getInputManager().TriggerOmsActionReleased(OmsAction.Key1P_Scratch), Is.True));

            AddUntilStep("key action recorded", () => Player.Score.Replay.Frames.OfType<BmsReplayFrame>().Any(frame => frame.Actions.SequenceEqual(new[] { BmsAction.Key1 })));
            AddUntilStep("scratch action recorded", () => Player.Score.Replay.Frames.OfType<BmsReplayFrame>().Any(frame => frame.Actions.SequenceEqual(new[] { BmsAction.Scratch1 })));
            AddAssert("ui action not recorded", () => Player.Score.Replay.Frames.OfType<BmsReplayFrame>().All(frame => frame.Actions.All(action => action.IsLaneAction())));
        }

        private (BmsDecodedBeatmap SourceBeatmap, BmsBeatmap PlayableBeatmap) createBeatmapPairFromText(string text, string path)
        {
            var decodedChart = decoder.DecodeText(text, path);
            var beatmap = new BmsDecodedBeatmap(decodedChart)
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };

            var playable = (BmsBeatmap)new BmsBeatmapConverter(beatmap, new BmsRuleset()).Convert();

            return (beatmap, playable);
        }

        private double getNoteTime(int index)
            => playableBeatmap.HitObjects.Where(h => h is BmsHitObject).OrderBy(h => h.StartTime).ElementAt(index).StartTime;

        private double getMidpointTime(int firstNoteIndex, int secondNoteIndex)
            => (getNoteTime(firstNoteIndex) + getNoteTime(secondNoteIndex)) / 2;

        private BmsInputManager getInputManager() => Player.DrawableRuleset.ChildrenOfType<BmsInputManager>().Single();

        private void seekTo(System.Func<double> time)
        {
            AddStep("seek to target time", () => Player.GameplayClockContainer.Seek(time()));
            AddUntilStep("wait for seek to finish", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(time()).Within(500));
        }
    }
}
