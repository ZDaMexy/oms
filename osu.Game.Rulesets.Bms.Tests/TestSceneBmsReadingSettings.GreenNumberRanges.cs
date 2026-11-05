// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReadingSettings
    {
        [TestCase(ScrollVisualisationMethod.Sequential)]
        [TestCase(ScrollVisualisationMethod.Overlapping)]
        public void TestGreenNumberRangeIncludesBpmChangesWithGimmicksOff(ScrollVisualisationMethod method)
        {
            prepare(settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(BmsGimmickScrollMode.Off);
            loadGame(chartText: "#TITLE Native base BPM range\n#BPM 120\n#BPM01 240\n#00111:01\n#00208:01\n#00311:01\n#00611:01\n");
            AddStep("select the actual base algorithm", () => renderer.VisualisationMethod = method);
            advanceReadingClock(1500);
            AddStep("the opening readout includes the later faster interval", () =>
            {
                Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.SameAs(renderer.ScrollingInfo.Algorithm.Value));
                assertNativeReadingInterval(1500, 1);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(500));
                Assert.That(readGreenNumberBounds(), Is.EqualTo((250, 500)));
            });
            advanceReadingClock(4500);
            AddStep("the actual faster motion reaches the lower endpoint", () =>
            {
                assertNativeReadingInterval(4500, 1);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(250));
                Assert.That(readGreenNumberBounds(), Is.EqualTo((250, 500)));
            });
        }

        [TestCase(BmsGimmickScrollMode.Off, ScrollVisualisationMethod.Sequential)]
        [TestCase(BmsGimmickScrollMode.Off, ScrollVisualisationMethod.Overlapping)]
        [TestCase(BmsGimmickScrollMode.On, ScrollVisualisationMethod.Sequential)]
        public void TestGreenNumberRangeIncludesBothScrollEndpointsWithActiveCoversAndLift(BmsGimmickScrollMode gate, ScrollVisualisationMethod method)
        {
            (int Minimum, int Maximum) range = default;
            var initial = settings(BmsHiSpeedMode.Floating);
            prepare(initial, remember: false);
            setGimmickGate(gate);
            loadGame(chartText: positive_scroll_chart);
            AddStep("select the base method before sampling its native range", () => renderer.VisualisationMethod = method);
            AddStep("a constant BPM still exposes a varying SCROLL range", () =>
            {
                range = readGreenNumberBounds();
                Assert.That(renderer.GameplaySkinAdjustmentState.GreenNumberRangeAvailable, Is.True);
                Assert.That(range.Minimum, Is.LessThan(range.Maximum));
            });
            advanceReadingClock(3000);
            AddStep("slow SCROLL reaches the covered and lifted maximum", () =>
            {
                assertNativeReadingInterval(3000, .625);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(range.Maximum));
                Assert.That(readGreenNumberBounds(), Is.EqualTo(range));
                Assert.That(renderer.LiftUnits, Is.EqualTo(initial.LiftUnits));
            });
            advanceReadingClock(7000);
            AddStep("fast SCROLL reaches the covered and lifted minimum", () =>
            {
                assertNativeReadingInterval(7000, .625);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(range.Minimum));
                Assert.That(readGreenNumberBounds(), Is.EqualTo(range));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
            });
        }

        [Test]
        public void TestGreenNumberRangeUsesTheActualClampAndFollowsLiveAlgorithmSelection()
        {
            var initial = settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false };
            prepare(initial, remember: false);
            setGimmickGate(BmsGimmickScrollMode.Off);
            loadGame(chartText: "#TITLE Actual range clamp\n#BPM 120\n#SCROLL01 20\n#001SC:01\n#00411:01\n");
            advanceReadingClock(3000);
            AddStep("the shared path reports its real SCROLL clamp", () =>
            {
                assertNativeReadingInterval(3000, 1);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(50));
                Assert.That(readGreenNumberBounds(), Is.EqualTo((50, 500)));
            });
            AddStep("select the native unclamped profile", () => config.SetValue(BmsRulesetSetting.GimmickScrollMode, BmsGimmickScrollMode.On));
            AddStep("the profile has its own cached endpoints", () =>
            {
                assertNativeReadingInterval(3000, 1);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(25));
                Assert.That(readGreenNumberBounds(), Is.EqualTo((25, 500)));
            });
            AddStep("select the real constant algorithm", () =>
            {
                config.SetValue(BmsRulesetSetting.GimmickScrollMode, BmsGimmickScrollMode.Off);
                renderer.VisualisationMethod = ScrollVisualisationMethod.Constant;
            });
            AddStep("constant motion has one endpoint regardless of chart BPM or SCROLL", () =>
            {
                Assert.That(renderer.Playfield.ActiveScrollAlgorithm, Is.TypeOf<ConstantScrollAlgorithm>());
                assertNativeReadingInterval(3000, 1);
                int actual = renderer.SpeedMetrics.Value.GreenNumber;
                Assert.That(readGreenNumberBounds(), Is.EqualTo((actual, actual)));
                Assert.That(renderer.GameplaySkinAdjustmentState.GreenNumberRangeAvailable, Is.False);
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
            });
        }

        [TestCase(BmsGimmickScrollMode.Off)]
        [TestCase(BmsGimmickScrollMode.On)]
        public void TestGreenNumberRangeDistinguishesStopMarkersFromTheProfileMusicalReference(BmsGimmickScrollMode gate)
        {
            (int Minimum, int Maximum) range = default;
            prepare(settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false });
            setGimmickGate(gate);
            loadGame(chartText: "#TITLE STOP readout range\n#BPM 120\n#STOP01 48\n#00109:01\n#00111:0001\n#01011:01\n");
            AddStep("the initial range belongs to the selected visual algorithm", () =>
            {
                range = readGreenNumberBounds();
                Assert.That(range.Maximum, Is.EqualTo(renderer.SpeedMetrics.Value.GreenNumber));
                Assert.That(renderer.GameplaySkinAdjustmentState.GreenNumberRangeAvailable, Is.EqualTo(gate == BmsGimmickScrollMode.Off));
            });
            advanceReadingClock(2100);
            AddStep("the actual STOP interval has the expected reading reference", () =>
            {
                Assert.That(beatmap.TimingProfile!.Sample(2100).IsStopped, Is.True);
                if (gate == BmsGimmickScrollMode.Off)
                    assertNativeReadingInterval(2100, 1);
                Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(range.Minimum));
                Assert.That(readGreenNumberBounds(), Is.EqualTo(range));
            });
        }

        [Test]
        public void TestGreenNumberRangeRemainsUnavailableForLegacyReadingReplay()
        {
            prepare(settings(BmsHiSpeedMode.Normal));
            loadGame(chartText: positive_scroll_chart);
            AddStep("adopt a genuine legacy calculation payload", () => renderer.SetReplayScore(legacyFractionalReadingReplay(0)));
            AddStep("legacy playback does not claim the new native range", () =>
            {
                GameplaySkinAdjustmentState state = renderer.GameplaySkinAdjustmentState;
                Assert.That(state.GreenNumberMinimum, Is.Null);
                Assert.That(state.GreenNumberMaximum, Is.Null);
                Assert.That(state.GreenNumberRangeAvailable, Is.False);
                Assert.That(state.GreenNumber, Is.EqualTo(renderer.SpeedMetrics.Value.GreenNumber));
            });
        }

        private (int Minimum, int Maximum) readGreenNumberBounds()
        {
            GameplaySkinAdjustmentState state = renderer.GameplaySkinAdjustmentState;
            Assert.That(state.GreenNumberMinimum.HasValue && state.GreenNumberMaximum.HasValue, Is.True);
            Assert.That(state.GreenNumber, Is.EqualTo(renderer.SpeedMetrics.Value.GreenNumber));
            return (state.GreenNumberMinimum!.Value, state.GreenNumberMaximum!.Value);
        }
    }
}
