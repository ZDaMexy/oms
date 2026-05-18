// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsKeysoundPlaybackLifecycle : OsuTestScene
    {
        private TestManualClock manualClock = null!;
        private GameplayClockContainer gameplayClockContainer = null!;
        private BmsKeysoundStore keysoundStore = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            manualClock = new TestManualClock();

            Child = gameplayClockContainer = new GameplayClockContainer(manualClock, applyOffsets: false, requireDecoupling: false)
            {
                RelativeSizeAxes = Axes.Both,
                Child = keysoundStore = new BmsKeysoundStore(2)
            };
        });

        [Test]
        public void TestPauseStopsRequestedKeysoundChannels()
        {
            AddUntilStep("store loaded", () => areChannelsReady());
            AddStep("start gameplay clock", () => gameplayClockContainer.Start());
            AddUntilStep("gameplay unpaused", () => !gameplayClockContainer.IsPaused.Value);

            playTestSample();

            AddStep("pause gameplay clock", () => gameplayClockContainer.Stop());
            AddUntilStep("keysound requests cleared", () => noRequestedChannels());
        }

        [Test]
        public void TestSeekStopsRequestedKeysoundChannels()
        {
            AddUntilStep("store loaded", () => areChannelsReady());
            AddStep("start gameplay clock", () => gameplayClockContainer.Start());
            AddUntilStep("gameplay unpaused", () => !gameplayClockContainer.IsPaused.Value);

            playTestSample();

            AddStep("seek gameplay clock", () => gameplayClockContainer.Seek(1000));
            AddUntilStep("keysound requests cleared", () => noRequestedChannels());
        }

        private void playTestSample()
        {
            AddStep("play test keysound", () => keysoundStore.Play(new SampleInfo("bgm.wav"), 0));
            AddUntilStep("keysound requested", () => keysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying));
        }

        private bool areChannelsReady()
            => keysoundStore?.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready) == true;

        private bool noRequestedChannels()
            => keysoundStore.ChannelPool.All(channel => !channel.RequestedPlaying);

        private class TestManualClock : ManualClock, IAdjustableClock
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

            public void ResetSpeedAdjustments()
            {
            }
        }
    }
}
