// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPlayerAudioSemantics : PlayerTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };
        private readonly FramedClock referenceClock;
        private readonly BmsDecodedBeatmap sourceBeatmap;
        private readonly BmsBeatmap playableBeatmap;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public TestSceneBmsPlayerAudioSemantics()
        {
            referenceClock = new FramedClock(manualClock);

            (sourceBeatmap, playableBeatmap) = createBeatmapPairFromText(@"
#TITLE Player Audio Semantics Stub
#BPM 60
#RANK 2
#WAVAA bgm.wav
#WAVBB key1.wav
#00201:AA00
#00411:BB00
", "player-audio-semantics-stub.bme");
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            manualClock.CurrentTime = 0;
            referenceClock.ProcessFrame();
        });

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => sourceBeatmap;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new OsuTestScene.ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [Test]
        public void TestGameplayClockResumesFromPausedPosition()
        {
            double pausedClockTime = 0;
            bool pauseAccepted = false;

            AddUntilStep("player ready", isPlayerReady);
            AddUntilStep("track starts running", () => Beatmap.Value.Track.IsRunning);

            advanceReferenceClockBy(5000);
            AddUntilStep("gameplay clock progresses before pause", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(4500));

            AddStep("record paused gameplay time", () => pausedClockTime = Player.GameplayClockContainer.CurrentTime);
            AddStep("pause player", () => pauseAccepted = Player.Pause());
            AddAssert("pause accepted", () => pauseAccepted);
            AddUntilStep("gameplay clock pauses", () => Player.GameplayClockContainer.IsPaused.Value);

            advanceReferenceClockBy(1000);
            AddAssert("gameplay clock held while paused", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(pausedClockTime).Within(100));

            AddStep("resume player", () => Player.Resume());
            AddUntilStep("gameplay clock resumes running", () => Player.GameplayClockContainer.IsRunning);

            advanceReferenceClockBy(600);
            AddUntilStep("gameplay clock continues from paused position", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(pausedClockTime + 400));
        }

        [Test]
        public void TestBgmEventReplaysAfterSeekBackwards()
        {
            double bgmEventTime = getBgmEventTime();

            AddUntilStep("player ready", isPlayerReady);
            AddUntilStep("track starts running", () => Beatmap.Value.Track.IsRunning);

            advanceReferenceClockBy(bgmEventTime + 200);
            AddUntilStep("bgm requested first time", () => isSampleRequested("bgm.wav"));

            AddStep("seek before bgm event", () => Player.GameplayClockContainer.Seek(bgmEventTime - 300));
            AddUntilStep("rewind applied", () => Precision.AlmostEquals(Player.DrawableRuleset.FrameStableClock.CurrentTime, bgmEventTime - 300, 100));
            AddUntilStep("seek clears requested bgm", () => !isSampleRequested("bgm.wav"));

            advanceReferenceClockBy(500);
            AddUntilStep("bgm requested after replay", () => Player.DrawableRuleset.FrameStableClock.CurrentTime > bgmEventTime && isSampleRequested("bgm.wav"));
        }

        private void advanceReferenceClockBy(double delta)
            => AddStep($"advance reference clock by {delta:N0} ms", () =>
            {
                manualClock.CurrentTime += delta;
                referenceClock.ProcessFrame();
            });

        private bool isPlayerReady()
        {
            if (Player?.IsLoaded != true || Player.DrawableRuleset is not DrawableBmsRuleset drawableRuleset)
                return false;

            return drawableRuleset.Playfield.KeysoundStore.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready);
        }

        private bool isSampleRequested(string filename)
            => ((DrawableBmsRuleset)Player.DrawableRuleset).Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying
                                                                                                                 && channel.Samples.OfType<BmsKeysoundSampleInfo>().Any(sample => sample.Filename == filename));

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

        private double getBgmEventTime()
            => playableBeatmap.HitObjects.OfType<BmsBgmEvent>().Single().StartTime;
    }
}
