// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReadingSettings
    {
        private const string positive_scroll_chart = """
            #TITLE Actual positive SCROLL reading intervals
            #BPM 120
            #SCROLL01 0.5
            #SCROLL02 2
            #SCROLL03 1
            #001SC:01
            #00211:01
            #003SC:02
            #00411:01
            #005SC:03
            #00611:01
            """;

        [TestCaseSource(nameof(positiveScrollCases))]
        public void TestPositiveScrollUsesTheSelectedNativePathForGreenNumber(BmsHiSpeedMode mode, BmsGimmickScrollMode gate, bool geometry)
        {
            var initial = settings(mode, sudden: geometry, hidden: geometry) with { LiftEnabled = geometry, LiftUnits = 500 };
            prepare(initial);
            setGimmickGate(gate);
            loadGame(chartText: positive_scroll_chart);
            AddStep("the actual path follows the existing Auto decision", () =>
            {
                Assert.That(beatmap.ScrollProfile!.IsStopMotionGimmick, Is.False);
                assertSelectedPath(gate == BmsGimmickScrollMode.On);
            });

            advanceReadingClock(3000);
            AddStep("SCROLL 0.5 doubles the real local lane time", () =>
            {
                double observed = assertNativeReadingInterval(3000, geometry ? 0.625 : 1);
                Assert.That(observed, Is.EqualTo(renderer.ScrollingInfo.TimeRange.Value * 2).Within(0.03));
            });
            advanceReadingClock(7000);
            AddStep("SCROLL 2 halves lane time without requiring a BPM change", () =>
            {
                double observed = assertNativeReadingInterval(7000, geometry ? 0.625 : 1);
                Assert.That(observed, Is.EqualTo(renderer.ScrollingInfo.TimeRange.Value / 2).Within(0.03));
            });
        }

        private static IEnumerable<TestCaseData> positiveScrollCases()
        {
            foreach (BmsHiSpeedMode mode in Enum.GetValues<BmsHiSpeedMode>())
                foreach (BmsGimmickScrollMode gate in Enum.GetValues<BmsGimmickScrollMode>())
                    yield return new TestCaseData(mode, gate, false);

            yield return new TestCaseData(BmsHiSpeedMode.Floating, BmsGimmickScrollMode.On, true);
        }

        [TestCase(BmsGimmickScrollMode.Off)]
        [TestCase(BmsGimmickScrollMode.Auto)]
        [TestCase(BmsGimmickScrollMode.On)]
        public void TestFloatingFirstScratchAfterScrollChangeRestoresAndRecordsTheActualTarget(BmsGimmickScrollMode gate)
        {
            var initial = settings(BmsHiSpeedMode.Floating, hidden: false) with { LiftEnabled = false };
            double target = 0;
            Score score = null!;
            prepare(initial);
            setGimmickGate(gate);
            loadGame(chartText: positive_scroll_chart);
            AddStep("retain the opening actual interval and start the real recorder", () =>
            {
                target = observeNativeFullLaneTime(1000) * 0.75;
                renderer.SetRecordTarget(score = new Score());
            });
            advanceReadingClock(7000);
            AddStep("the new scroll multiplier changes actual reading time", () =>
            {
                Assert.That(observeNativeFullLaneTime(7000) * 0.75, Is.EqualTo(target / 2).Within(0.03));
                assertNativeReadingInterval(7000, 0.75);
            });
            AddStep("the first scratch refloats to the opening physical interval", () =>
            {
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                double observedVisibleTime = observeNativeFullLaneTime(7000) * 0.73;
                double tolerance = target * BmsRulesetConfigManager.FLOATING_HI_SPEED_PRECISION / (2 * renderer.SelectedHiSpeed.Value) + 0.03;
                var expected = initial with { FloatingHiSpeed = renderer.SelectedHiSpeed.Value, SuddenUnits = 270 };
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(270));
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.LessThan(initial.FloatingHiSpeed * 0.7));
                    Assert.That(observedVisibleTime, Is.EqualTo(target).Within(tolerance));
                    Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(observedVisibleTime).Within(0.03));
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected), "Cover and compensating speed persist together.");
                });

                renderer.SetRecordTarget(null!);
                var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(data.ReadingSettings, Is.EqualTo(initial));
                Assert.That(data.ReadingSettingsTimeline!.Changes, Has.Count.EqualTo(1));
                Assert.That(data.ReadingSettingsTimeline.Changes.Single().Time, Is.EqualTo(7000));
                Assert.That(data.ReadingSettingsTimeline.Changes.Single().Settings, Is.EqualTo(expected), "Replay metadata has only the complete linked operation.");
            });
        }

        [TestCaseSource(nameof(dominantStopCases))]
        public void TestDominantStopUsesTheActiveAlgorithmReferenceWithoutChangingTheOffFallback(BmsHiSpeedMode mode, BmsGimmickScrollMode gate)
        {
            prepare(settings(mode, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(gate);
            loadGame(chartText: "#TITLE Dominant STOP reference\n#BPM 120\n#STOP01 3072\n#00109:01\n#00211:01\n");
            AddStep("the converter exposes the real distinction between the two references", () =>
            {
                Assert.That(beatmap.GetMostCommonBeatLength(), Is.EqualTo(6));
                Assert.That(beatmap.ScrollProfile!.BaseBeatLength, Is.EqualTo(500));
                Assert.That(beatmap.ScrollProfile.IsStopMotionGimmick, Is.True);
                assertSelectedPath(gate != BmsGimmickScrollMode.Off);
                double observed = assertNativeReadingInterval(1000, 1);
                if (gate != BmsGimmickScrollMode.Off)
                {
                    // The profile is slope1 outside STOP: these are independent calibrated local lane times.
                    double expected = mode switch
                    {
                        BmsHiSpeedMode.Normal => 961.5384615384615,
                        BmsHiSpeedMode.Floating => 3076.923076923077,
                        BmsHiSpeedMode.Classic => 1098.901098901099,
                        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                    };
                    Assert.That(observed, Is.EqualTo(expected).Within(0.05), "A STOP marker cannot shrink Floating/Classic in the actual profile algorithm.");
                }
            });
            advanceReadingClock(35000);
            AddStep("after STOP the real interval still agrees with the selected path's green number", () => assertNativeReadingInterval(35000, 1));
        }

        private static IEnumerable<TestCaseData> dominantStopCases()
        {
            foreach (BmsHiSpeedMode mode in Enum.GetValues<BmsHiSpeedMode>())
                foreach (BmsGimmickScrollMode gate in Enum.GetValues<BmsGimmickScrollMode>())
                    yield return new TestCaseData(mode, gate);
        }

        [TestCase(BmsGimmickScrollMode.Off)]
        [TestCase(BmsGimmickScrollMode.Auto)]
        [TestCase(BmsGimmickScrollMode.On)]
        public void TestShortStopKeepsTheHardFallbackAndReportsItsActualNativeMultiplier(BmsGimmickScrollMode gate)
        {
            double beforeStop = 0;
            prepare(settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(gate);
            loadGame(chartText: "#TITLE Short STOP fallback\n#BPM 120\n#STOP01 48\n#00109:01\n#00111:0001\n#01011:01\n");
            AddStep("Auto correctly keeps the existing normal path for this short STOP", () =>
            {
                Assert.That(beatmap.ScrollProfile!.IsStopMotionGimmick, Is.False);
                assertSelectedPath(gate == BmsGimmickScrollMode.On);
                beforeStop = observeNativeFullLaneTime(1000);
            });
            advanceReadingClock(2100);
            AddStep("musical STOP is distinct from the actual selected visual behaviour", () =>
            {
                Assert.That(beatmap.TimingProfile!.Sample(2100).IsStopped, Is.True);
                var lane = renderer.Playfield.Lanes[0];
                float first = lane.HitObjectContainer.PositionAtTime(3500, 2100);
                float next = lane.HitObjectContainer.PositionAtTime(3500, 2200);
                if (gate == BmsGimmickScrollMode.On)
                {
                    Assert.That(next, Is.EqualTo(first));
                    Assert.That(renderer.SpeedMetrics.Value.RuntimeTimeRange, Is.EqualTo(beforeStop).Within(0.03), "Profile STOP retains the approved musical reading reference.");
                }
                else
                {
                    Assert.That(Math.Abs(next - first), Is.GreaterThan(lane.HitObjectContainer.DrawHeight));
                    double observed = assertNativeReadingInterval(2100, 1);
                    Assert.That(observed, Is.LessThan(beforeStop / 50), "The readout must describe the retained base fallback, even when that path does not freeze.");
                    Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.SameAs(renderer.ScrollingInfo.Algorithm.Value));
                }
            });
        }

        [TestCase(BmsGimmickScrollMode.Off)]
        [TestCase(BmsGimmickScrollMode.Auto)]
        [TestCase(BmsGimmickScrollMode.On)]
        public void TestPositiveScrollOutsideSharedClampUsesTheActualSelectedPath(BmsGimmickScrollMode gate)
        {
            prepare(settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(gate);
            loadGame(chartText: "#TITLE Native SCROLL clamp versus raw profile\n#BPM 120\n#SCROLL01 20\n#001SC:01\n#00411:01\n");
            advanceReadingClock(3000);
            AddStep("readouts follow the actual clamp only when the base path is active", () =>
            {
                Assert.That(beatmap.ScrollProfile!.IsStopMotionGimmick, Is.False);
                assertSelectedPath(gate == BmsGimmickScrollMode.On);
                double observed = assertNativeReadingInterval(3000, 1);
                double selectedMultiplier = gate == BmsGimmickScrollMode.On ? 20 : 10;
                Assert.That(observed, Is.EqualTo(renderer.ScrollingInfo.TimeRange.Value / selectedMultiplier).Within(0.03));
            });
        }

        [Test]
        public void TestProfileStopAtANewBpmRetainsItsRawScrollReadingReference()
        {
            double duringStop = 0;
            prepare(settings(BmsHiSpeedMode.Floating, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(BmsGimmickScrollMode.On);
            loadGame(chartText: "#TITLE BPM changes exactly at STOP start\n#BPM 120\n#BPM01 240\n#STOP01 96\n#SCROLL01 0.5\n#001SC:01\n#00208:01\n#00209:01\n#00811:01\n");
            advanceReadingClock(4250);
            AddStep("the frozen interval retains the newly active BPM and existing SCROLL", () =>
            {
                var timing = beatmap.TimingProfile!.Sample(4250);
                Assert.That(timing.Bpm, Is.EqualTo(240));
                Assert.That(timing.IsStopped, Is.True);
                Assert.That(timing.ScrollSpeed, Is.EqualTo(0.5));
                duringStop = renderer.SpeedMetrics.Value.RuntimeTimeRange;
            });
            advanceReadingClock(4750);
            AddStep("the frozen reference equals actual native velocity immediately after STOP", () =>
            {
                Assert.That(beatmap.TimingProfile!.Sample(4750).IsStopped, Is.False);
                double observed = assertNativeReadingInterval(4750, 1);
                Assert.That(duringStop, Is.EqualTo(observed).Within(0.03));
            });
        }

        [Test]
        public void TestZeroScrollRetainsItsExistingFreezeWithoutPositiveRefloat()
        {
            prepare(settings(BmsHiSpeedMode.Floating, hidden: false) with { LiftEnabled = false });
            setGimmickGate(BmsGimmickScrollMode.On);
            loadGame(chartText: "#TITLE Zero SCROLL keeps musical reference\n#BPM 120\n#SCROLL01 0\n#SCROLL02 1\n#001SC:01\n#003SC:02\n#00811:01\n");
            advanceReadingClock(3000);
            AddStep("zero SCROLL keeps its render freeze and does not compensate Hi-Speed", () =>
            {
                var lane = renderer.Playfield.Lanes[0];
                Assert.That(lane.HitObjectContainer.PositionAtTime(7000, 3000), Is.EqualTo(lane.HitObjectContainer.PositionAtTime(7000, 3100)));
                double speed = renderer.SelectedHiSpeed.Value;
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(speed));
                Assert.That(double.IsFinite(renderer.SpeedMetrics.Value.VisibleLaneTime), Is.True);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.GreaterThan(0), "This is a musical reference during the freeze, not an invented inverse of zero velocity.");
            });
        }

        [Test]
        public void TestLivePathChangeRecalibratesTheActualFloatingAlgorithmWithoutChangingReadingSettings()
        {
            var initial = settings(BmsHiSpeedMode.Floating, sudden: false, hidden: false) with { LiftEnabled = false };
            double onRange = 0;
            double observedTime = 0;
            prepare(initial);
            setGimmickGate(BmsGimmickScrollMode.On);
            loadGame(chartText: "#TITLE Live path reference change\n#BPM 120\n#STOP01 3072\n#00109:01\n#00211:01\n");
            AddStep("retain the actual profile lane interval", () =>
            {
                assertSelectedPath(true);
                onRange = renderer.ScrollingInfo.TimeRange.Value;
                observedTime = assertNativeReadingInterval(1000, 1);
            });
            AddStep("return to the existing Off algorithm at the same gameplay time", () => config.SetValue(BmsRulesetSetting.GimmickScrollMode, BmsGimmickScrollMode.Off));
            AddStep("the actual base reference reaches native scrolling and the readout together", () =>
            {
                assertSelectedPath(false);
                Assert.That(renderer.ScrollingInfo.TimeRange.Value, Is.LessThan(onRange / 50));
                Assert.That(assertNativeReadingInterval(1000, 1), Is.EqualTo(observedTime).Within(0.05));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
            });
            AddStep("restore the actual profile path", () => config.SetValue(BmsRulesetSetting.GimmickScrollMode, BmsGimmickScrollMode.On));
            AddStep("the profile reference returns without accumulating a second compensation", () =>
            {
                assertSelectedPath(true);
                Assert.That(renderer.ScrollingInfo.TimeRange.Value, Is.EqualTo(onRange));
                Assert.That(assertNativeReadingInterval(1000, 1), Is.EqualTo(observedTime).Within(0.05));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
            });
        }

        private void setGimmickGate(BmsGimmickScrollMode gate)
            => AddStep($"select the existing {gate} gimmick gate", () => config.SetValue(BmsRulesetSetting.GimmickScrollMode, gate));

        private void assertSelectedPath(bool profile)
        {
            if (profile)
                Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.TypeOf<BmsStopMotionScrollAlgorithm>());
            else
            {
                Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.TypeOf<SequentialScrollAlgorithm>());
                Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.SameAs(renderer.ScrollingInfo.Algorithm.Value));
            }
        }

        private double observeNativeFullLaneTime(double time)
        {
            var container = renderer.Playfield.Lanes[0].HitObjectContainer;
            // One fixed target observed over 100ms measures actual native motion, not a second metrics formula.
            float first = container.PositionAtTime(time + 200, time);
            float next = container.PositionAtTime(time + 200, time + 100);
            double distance = Math.Abs(next - first);
            Assert.That(distance, Is.GreaterThan(0));
            return 100 * container.DrawHeight / distance;
        }

        private double assertNativeReadingInterval(double time, double visibleFraction)
        {
            double observed = observeNativeFullLaneTime(time);
            double visible = observed * visibleFraction;
            Assert.Multiple(() =>
            {
                Assert.That(renderer.SpeedMetrics.Value.RuntimeTimeRange, Is.EqualTo(observed).Within(0.03));
                Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(visible).Within(0.03));
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo((int)Math.Round(visible * 0.6, MidpointRounding.AwayFromZero)));
            });
            return observed;
        }
    }
}
