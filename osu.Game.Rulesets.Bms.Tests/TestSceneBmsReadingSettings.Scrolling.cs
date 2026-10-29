// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReadingSettings
    {
        [TestCaseSource(nameof(nativeSpeedExtremes))]
        public void TestHiSpeedExtremesAndLiftReachTheNativeScrollingAlgorithm(BmsHiSpeedMode mode, double speed, float lift)
        {
            var initial = settings(mode, sudden: false, hidden: false) with
            {
                NormalHiSpeed = mode == BmsHiSpeedMode.Normal ? speed : 8,
                FloatingHiSpeed = mode == BmsHiSpeedMode.Floating ? speed : 2.5,
                ClassicHiSpeed = mode == BmsHiSpeedMode.Classic ? speed : 3.5,
                LiftUnits = lift,
            };
            prepare(initial);
            loadGame();
            AddUntilStep("the actual lane applies Lift geometry", () =>
                Math.Abs(renderer.Playfield.ScrollLengthRatio.Value - (1 - lift / 1000d)) < 0.001);
            AddStep("compare native time and position rather than a second metrics calculation", () =>
            {
                var lane = renderer.Playfield.Lanes[0];
                double ratio = renderer.Playfield.ScrollLengthRatio.Value;
                double nativeTimeRange = renderer.ScrollingInfo.TimeRange.Value;
                // The native reference units give these intervals for this one120 BPM segment.
                double expectedFullRange = mode == BmsHiSpeedMode.Normal
                    ? speed == 1 ? 2000 : 433.333282470703125
                    : speed == 0.5 ? 4854.3798828125 : 242.718963623046875;
                double expectedRange = expectedFullRange * ratio;
                float height = lane.HitObjectContainer.DrawHeight;
                double distance = Math.Abs(lane.HitObjectContainer.PositionAtTime(1100, 1000));
                Assert.That(double.IsFinite(nativeTimeRange) && nativeTimeRange > 0, Is.True);
                Assert.That(double.IsFinite(distance), Is.True);

                if (lift == 1000)
                {
                    Assert.That(height, Is.Zero);
                    Assert.That(distance, Is.Zero);
                    Assert.That(renderer.SpeedMetrics.Value.RuntimeTimeRange, Is.Zero);
                    Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.Zero);
                    return;
                }

                Assert.That(nativeTimeRange, Is.EqualTo(expectedRange).Within(0.001), "Valid settings must reach the native algorithm without the legacy 290ms/7692ms truncation.");
                Assert.That(height, Is.GreaterThan(0));
                Assert.That(distance, Is.EqualTo(100 / expectedRange * height).Within(0.001));
                double observedFullLaneTime = 100 * height / distance;
                Assert.That(renderer.SpeedMetrics.Value.RuntimeTimeRange, Is.EqualTo(observedFullLaneTime).Within(0.01));
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.InRange(
                    (int)Math.Ceiling((float)((observedFullLaneTime - 0.01) * 0.6)),
                    (int)Math.Ceiling((float)((observedFullLaneTime + 0.01) * 0.6))));
            });
        }

        private static IEnumerable<TestCaseData> nativeSpeedExtremes()
        {
            foreach (BmsHiSpeedMode mode in Enum.GetValues<BmsHiSpeedMode>())
            {
                foreach (double speed in mode == BmsHiSpeedMode.Normal ? new[] { 1d, 20d } : new[] { 0.5, 10d })
                {
                    foreach (float lift in new[] { 0f, 500f, 1000f })
                        yield return new TestCaseData(mode, speed, lift);
                }
            }
        }

        [TestCase(BmsHiSpeedMode.Normal)]
        [TestCase(BmsHiSpeedMode.Floating)]
        [TestCase(BmsHiSpeedMode.Classic)]
        public void TestStopKeepsMusicalBpmForGreenNumberAndScratchWhileNativeNotesFreeze(BmsHiSpeedMode mode)
        {
            double beforeStop = 0;
            double afterAdjustment = 0;
            double previousSpeed = 0;
            float frozenPosition = 0;
            prepare(settings(mode, hidden: false), remember: false);
            loadGame(chartText: "#TITLE Reading during STOP\n#BPM 120\n#STOP01 192\n#00209:01\n#00811:01\n");
            advanceReadingClock(3500);
            AddStep("capture the real reading interval before STOP", () =>
            {
                beforeStop = renderer.SpeedMetrics.Value.VisibleLaneTime;
                previousSpeed = renderer.SelectedHiSpeed.Value;
            });
            advanceReadingClock(4500);
            AddStep("STOP freezes positions without becoming a 10000 BPM reading change", () =>
            {
                Assert.That(beatmap.ControlPointInfo.TimingPointAt(4500), Is.TypeOf<BmsStopFreezeTimingControlPoint>());
                Assert.That(beatmap.TimingProfile!.Sample(4500).IsStopped, Is.True);
                Assert.That(beatmap.TimingProfile.Sample(4500).Bpm, Is.EqualTo(120));
                var lane = renderer.Playfield.Lanes[0];
                frozenPosition = lane.HitObjectContainer.PositionAtTime(7000, 4500);
                Assert.That(lane.HitObjectContainer.PositionAtTime(7000, 4900), Is.EqualTo(frozenPosition).Within(0.001));
                Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(beforeStop).Within(0.001));
            });
            AddStep("make an actual scratch adjustment during STOP", () =>
            {
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                afterAdjustment = renderer.SpeedMetrics.Value.VisibleLaneTime;
                if (mode == BmsHiSpeedMode.Floating)
                {
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.GreaterThan(previousSpeed * 0.9));
                    Assert.That(afterAdjustment, Is.EqualTo(beforeStop).Within(3));
                }
                else
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(previousSpeed));
            });
            advanceReadingClock(6500);
            AddStep("the same interval survives STOP end and notes move again", () =>
            {
                Assert.That(beatmap.TimingProfile!.Sample(6500).IsStopped, Is.False);
                Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(afterAdjustment).Within(0.001));
                Assert.That(renderer.Playfield.Lanes[0].HitObjectContainer.PositionAtTime(7000, 6500), Is.Not.EqualTo(frozenPosition));
            });
        }

        private void advanceReadingClock(double time)
        {
            AddStep($"advance the gameplay clock to {time}ms", () => clock.CurrentTime = time);
            AddUntilStep("the native frame-stable clock reaches that time", () => Math.Abs(renderer.FrameStableClock.CurrentTime - time) < 0.001);
        }
    }
}
