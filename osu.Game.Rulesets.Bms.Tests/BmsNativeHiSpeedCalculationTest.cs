// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsNativeHiSpeedCalculationTest
    {
        [TestCase(1, 1200)]
        [TestCase(3, 800)]
        [TestCase(8, 500)]
        [TestCase(20, 260)]
        public void TestNormalPresetsResolveToTheNativeVisibleInterval(int level, int greenNumber)
        {
            double interval = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(BmsHiSpeedMode.Normal, level, 60000d / 130, 60000d / 130, 1, 1);
            Assert.That(interval, Is.EqualTo(greenNumber / 0.6).Within(0.001));
        }

        [Test]
        public void TestFloatingAndClassicShareAnAbsoluteMultiplier()
        {
            double floating = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(BmsHiSpeedMode.Floating, 2.5, 500, 250, 1, 1);
            double classic = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(BmsHiSpeedMode.Classic, 2.5, 500, 250, 1, 1);
            Assert.That(floating, Is.EqualTo(classic));
            Assert.That(floating, Is.EqualTo(970.8758544921875).Within(0.001));
        }

        [TestCase(1.75, 1.5)]
        [TestCase(1.7533334096272786, 2)]
        [TestCase(0.5, 1)]
        [TestCase(10, 4)]
        public void TestClassicUsesTheInternalValueAndLowerMidpoint(double floating, double expected)
            => Assert.That(BmsHiSpeedRuntimeCalculator.NearestClassic(floating), Is.EqualTo(expected));

        [Test]
        public void TestScratchKeepsInternalPrecisionPastTwoDisplayDecimals()
        {
            double adjusted = BmsHiSpeedRuntimeCalculator.AdjustFloatingFromScratch(1.75, 1);
            Assert.That(adjusted, Is.EqualTo(1.7533334096272786));
            Assert.That(BmsHiSpeedRuntimeCalculator.NearestClassic(adjusted), Is.EqualTo(2));
        }

        [TestCase(500, 300)]
        [TestCase(500.001, 301)]
        [TestCase(500.000001, 300)]
        public void TestNativeGreenNumberCeilsTheFloat32Value(double visibleTime, int expected)
        {
            var metrics = BmsScrollSpeedMetrics.FromTimeRange(BmsHiSpeedMode.Floating, 2.5, visibleTime, 1, 0, 0, 0, 1);
            Assert.That(metrics.GreenNumber, Is.EqualTo(expected));
            Assert.That(BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Floating, 2.5, 1,
                visibleTime / DrawableBmsRuleset.ComputeScrollTime(2.5)).GreenNumber, Is.EqualTo(300), "Legacy replay rounding stays unchanged.");
        }
    }
}
