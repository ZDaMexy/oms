// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Diagnostics;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneGameplayInputTimeSource : OsuTestScene
    {
        private OsuConfigManager localConfig = null!;
        private InputClock clock = null!;

        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));

        [TestCase(1, 75, 75)]
        [TestCase(1.5, 75, 112.5)]
        [TestCase(1, -75, -75)]
        [TestCase(1.5, -75, -112.5)]
        public void TestSnapshotUsesTheActualClockWithOffsetsAndRate(double rate, double userOffset, double expectedUserOffset)
        {
            AddStep("load the real offset-adjusted gameplay clock", () =>
            {
                localConfig.SetValue(OsuSetting.AudioOffset, userOffset);
                Child = clock = new InputClock(applyOffsets: true, rate: rate);
            });
            AddUntilStep("the input clock sample is available", () => clock.InputTime.HasValue);
            AddStep("the timestamp pair contains the existing authority time", () =>
            {
                GameplayInputTime sample = clock.InputTime!.Value;
                double platformOffset = RuntimeInfo.OS == RuntimeInfo.Platform.Windows ? 15 * rate : 0;
                Assert.Multiple(() =>
                {
                    Assert.That(clock.CurrentTime, Is.EqualTo(1000 + expectedUserOffset + platformOffset).Within(0.0001));
                    Assert.That(sample.Time, Is.EqualTo(clock.CurrentTime));
                    Assert.That(sample.Rate, Is.EqualTo(rate));
                    Assert.That(sample.IsRunning, Is.False);
                    Assert.That(sample.EpochStartTimestamp, Is.LessThanOrEqualTo(sample.Timestamp));
                    Assert.That(sample.Timestamp, Is.LessThanOrEqualTo(Stopwatch.GetTimestamp()));
                });
            });
        }

        [Test]
        public void TestSeekPauseResumeResetAndSourceChangeStartNewEpochs()
        {
            GameplayInputTime previous = default;
            AddStep("load a real gameplay clock", () => Child = clock = new InputClock(applyOffsets: false));
            AddUntilStep("the initial clock sample is available", () => clock.InputTime.HasValue);
            AddStep("seek marks the exact update-thread timeline boundary", () =>
            {
                previous = clock.InputTime!.Value;
                long before = Stopwatch.GetTimestamp();
                clock.Seek(4000);
                GameplayInputTime sample = clock.InputTime!.Value;
                Assert.That(sample.Epoch, Is.GreaterThan(previous.Epoch));
                Assert.That(sample.EpochStartTimestamp, Is.GreaterThanOrEqualTo(before));
                Assert.That(sample.Time, Is.EqualTo(4000).Within(0.0001));
                previous = sample;
            });
            AddStep("pause ends the current input epoch", () =>
            {
                clock.Stop();
                Assert.That(clock.InputTime!.Value.Epoch, Is.GreaterThan(previous.Epoch));
                Assert.That(clock.InputTime!.Value.IsRunning, Is.False);
                previous = clock.InputTime!.Value;
            });
            AddStep("resume the existing clock", () => clock.Start());
            AddUntilStep("the scheduled start publishes a fresh epoch", () => clock.InputTime is GameplayInputTime sample && sample.IsRunning && sample.Epoch > previous.Epoch);
            AddStep("reset starts another timeline", () =>
            {
                previous = clock.InputTime!.Value;
                clock.Reset(3000);
                Assert.That(clock.InputTime!.Value.Epoch, Is.GreaterThan(previous.Epoch));
                Assert.That(clock.InputTime!.Value.Time, Is.EqualTo(3000).Within(0.0001));
                previous = clock.InputTime!.Value;
            });
            AddStep("changing the source invalidates the old sample immediately", () =>
            {
                clock.ReplaceSource(7000);
                Assert.That(clock.InputTime, Is.Null);
            });
            AddUntilStep("the replacement source publishes a new sample", () => clock.InputTime is GameplayInputTime sample && sample.Epoch > previous.Epoch);
            AddAssert("the sample now follows the replacement source", () => clock.InputTime!.Value.Time == 7000);
        }

        [Test]
        public void TestChangingAudioOffsetInvalidatesThePreviousInputEpoch()
        {
            GameplayInputTime previous = default;
            AddStep("load the real clock without a user offset", () =>
            {
                localConfig.SetValue(OsuSetting.AudioOffset, 0.0);
                Child = clock = new InputClock(applyOffsets: true, rate: 1.5);
            });
            AddUntilStep("the initial sample is ready", () => clock.InputTime.HasValue);
            AddStep("change the audio offset", () =>
            {
                previous = clock.InputTime!.Value;
                localConfig.SetValue(OsuSetting.AudioOffset, 75.0);
            });
            AddUntilStep("offset change is a new timestamp epoch", () => clock.InputTime is GameplayInputTime sample && sample.Epoch > previous.Epoch);
            AddStep("the new epoch retains the real adjusted clock", () =>
            {
                double platformOffset = RuntimeInfo.OS == RuntimeInfo.Platform.Windows ? 22.5 : 0;
                Assert.That(clock.InputTime!.Value.Time, Is.EqualTo(1112.5 + platformOffset).Within(0.0001));
                Assert.That(clock.InputTime!.Value.Time, Is.EqualTo(clock.CurrentTime));
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                localConfig?.Dispose();
            base.Dispose(isDisposing);
        }

        private sealed partial class InputClock : GameplayClockContainer
        {
            public InputClock(bool applyOffsets, double rate = 1)
                : base(new AdjustableManualClock { CurrentTime = 1000, Rate = rate, IsRunning = false }, applyOffsets, requireDecoupling: false)
            {
                SoftUnpause();
            }

            public void ReplaceSource(double time) => ChangeSource(new AdjustableManualClock { CurrentTime = time, IsRunning = false });
        }

        private sealed class AdjustableManualClock : ManualClock, IAdjustableClock
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
