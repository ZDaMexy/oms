// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsAutomaticKeysoundLifecycle : OsuTestScene
    {
        private TestClock source = null!;
        private GameplayClockContainer gameplay = null!;
        private BmsKeysoundStore store = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            source = new TestClock();
            store = new BmsKeysoundStore(2);
            var beatmap = new BmsBeatmap();
            foreach (double time in new[] { 1000.0, 2000.0, 3000.0 })
                beatmap.HitObjects.Add(new BmsHitObject { StartTime = time, KeysoundId = 1, KeysoundSample = new BmsKeysoundSampleInfo("key.wav") });
            store.EnableAutomaticPlayback(beatmap);
            store.EnablePlaybackLogForTesting();
            Child = gameplay = new GameplayClockContainer(source, applyOffsets: false, requireDecoupling: false)
            {
                RelativeSizeAxes = Axes.Both,
                Child = store,
            };
        });

        [Test]
        public void TestPauseResumeAndForwardSeekDoNotBurstPastEvents()
        {
            start();
            advance(1100);
            AddAssert("first event", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(1));
            AddStep("pause", () => gameplay.Stop());
            AddUntilStep("paused", () => gameplay.IsPaused.Value);
            AddAssert("pause stops channels", () => store.ChannelPool.All(c => !c.RequestedPlaying));
            AddStep("seek while paused", () => gameplay.Seek(2500));
            AddWaitStep("remain paused", 3);
            AddAssert("seek does not burst skipped note", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(1));
            AddStep("resume", () => gameplay.Start());
            AddUntilStep("running", () => gameplay.IsRunning);
            advance(3100);
            AddAssert("only future event plays", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestResetRearmsAudioAndRateDoesNotChangeChartTimes()
        {
            start();
            AddStep("double source rate", () => source.Rate = 2);
            advance(3100);
            AddAssert("crossed events each played once", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(3));
            AddWaitStep("same clock repeated frames", 3);
            AddAssert("same clock cannot repeat music", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(3));
            AddStep("stop before retry", () => gameplay.Stop());
            AddUntilStep("stopped", () => gameplay.IsPaused.Value);
            AddStep("reset gameplay", () => gameplay.Reset(0, startClock: true));
            AddUntilStep("retry running", () => gameplay.IsRunning);
            AddUntilStep("reset clock", () => gameplay.CurrentTime, () => Is.EqualTo(0).Within(100));
            AddAssert("reset clears channels", () => store.ChannelPool.All(c => !c.RequestedPlaying));
            advance(1100);
            AddAssert("first event replayed after retry", () => store.PlaybackLogForTesting.Count, () => Is.EqualTo(4));
        }

        private void start()
        {
            AddUntilStep("store ready", () => store.ChannelPool.All(c => c.LoadState >= LoadState.Ready));
            AddStep("start gameplay", () => gameplay.Start());
            AddUntilStep("unpaused", () => !gameplay.IsPaused.Value);
        }

        private void advance(double time)
        {
            AddStep($"source to {time}", () => source.CurrentTime = time);
            AddUntilStep("clock settled", () => gameplay.CurrentTime, () => Is.EqualTo(time).Within(100));
        }

        private class TestClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;

            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }

            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }

            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
