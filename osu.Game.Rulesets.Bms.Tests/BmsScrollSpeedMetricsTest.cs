// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsScrollSpeedMetricsTest
    {
        [Test]
        public void TestRuntimeMetricsApplyScrollLengthAndCoverUnits()
        {
            var metrics = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150);

            Assert.Multiple(() =>
            {
                Assert.That(metrics.HiSpeedMode, Is.EqualTo(BmsHiSpeedMode.Normal));
                Assert.That(metrics.BaseTimeRange, Is.EqualTo(961.5384615385).Within(0.0001));
                Assert.That(metrics.RuntimeTimeRange, Is.EqualTo(769.2307692308).Within(0.0001));
                Assert.That(metrics.SuddenUnits, Is.EqualTo(350));
                Assert.That(metrics.HiddenUnits, Is.EqualTo(200));
                Assert.That(metrics.LiftUnits, Is.EqualTo(150));
                Assert.That(metrics.WhiteNumber, Is.EqualTo(350));
                Assert.That(metrics.VisibleLaneUnits, Is.EqualTo(450));
                Assert.That(metrics.VisibleLaneTime, Is.EqualTo(346.1538461538).Within(0.0001));
                Assert.That(metrics.GreenNumber, Is.EqualTo(208));
            });
        }

        [Test]
        public void TestOfficialNormalHiSpeedSampleMatchesReference()
        {
            var metrics = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 10.0, 1.0, suddenUnits: 350);

            Assert.Multiple(() =>
            {
                Assert.That(metrics.HiSpeedMode, Is.EqualTo(BmsHiSpeedMode.Normal));
                Assert.That(metrics.BaseTimeRange, Is.EqualTo(769.2307692308).Within(0.0001));
                Assert.That(metrics.RuntimeTimeRange, Is.EqualTo(769.2307692308).Within(0.0001));
                Assert.That(metrics.VisibleLaneUnits, Is.EqualTo(650));
                Assert.That(metrics.VisibleLaneTime, Is.EqualTo(500).Within(0.0001));
                Assert.That(metrics.WhiteNumber, Is.EqualTo(350));
                Assert.That(metrics.GreenNumber, Is.EqualTo(300));
            });
        }

        [Test]
        public void TestRuntimeMetricsClampCoverUnits()
        {
            var metrics = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 1.0, suddenUnits: -200, hiddenUnits: 1500);

            Assert.Multiple(() =>
            {
                Assert.That(metrics.SuddenUnits, Is.EqualTo(0));
                Assert.That(metrics.HiddenUnits, Is.EqualTo(1000));
                Assert.That(metrics.LiftUnits, Is.EqualTo(0));
                Assert.That(metrics.VisibleLaneUnits, Is.EqualTo(0));
                Assert.That(metrics.VisibleLaneTime, Is.EqualTo(0).Within(0.0001));
                Assert.That(metrics.GreenNumber, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestFloatingHiSpeedUsesInitialBeatLengthScale()
        {
            double baseTimeRange = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(BmsHiSpeedMode.Floating, 2.5, 500, 250, 1);

            Assert.That(baseTimeRange, Is.EqualTo(DrawableBmsRuleset.ComputeScrollTime(2.5) * 2).Within(0.0001));
        }

        [Test]
        public void TestClassicHiSpeedUsesAbsoluteBeatScaling()
        {
            double baseTimeRange = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(BmsHiSpeedMode.Classic, 2.5, 500, 250, 1);

            Assert.That(baseTimeRange, Is.EqualTo(DrawableBmsRuleset.ComputeScrollTime(2.5) * 0.5).Within(0.0001));
        }

        [Test]
        public void TestRuntimeMetricsEqualityUsesValueSemantics()
        {
            var left = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150);
            var right = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150);
            var different = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 351, hiddenUnits: 200, liftUnits: 150);
            var differentMode = BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Classic, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150);

            Assert.Multiple(() =>
            {
                Assert.That(left, Is.EqualTo(right));
                Assert.That(left == right, Is.True);
                Assert.That(left != right, Is.False);
                Assert.That(left.Equals(different), Is.False);
                Assert.That(left == different, Is.False);
                Assert.That(left.Equals(differentMode), Is.False);
            });
        }
    }
}
