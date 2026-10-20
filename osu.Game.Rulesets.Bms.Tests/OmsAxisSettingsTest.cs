// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using oms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsAxisSettingsTest
    {
        [Test]
        public void TestExistingBindingsKeepRelativeMotionDefaults()
        {
            var hid = OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive);
            var mouse = OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive);
            Assert.Multiple(() =>
            {
                Assert.That(hid.AxisSettings, Is.SameAs(OmsAxisSettings.Default));
                Assert.That(mouse.AxisSettings, Is.SameAs(OmsAxisSettings.Default));
                Assert.That(hid.AxisSettings.Mode, Is.EqualTo(OmsAxisMode.RelativeMotion));
                Assert.That(hid.AxisSettings.Sensitivity, Is.EqualTo(1));
                Assert.That(hid.AxisSettings.MovementThreshold, Is.Zero);
                Assert.That(hid.AxisSettings.StopTimeoutMilliseconds, Is.EqualTo(50));
            });
        }

        [Test]
        public void TestMouseCannotDeclarePositionOrStickInterpretation()
        {
            Assert.Throws<ArgumentException>(() => OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive,
                axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition)));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(0)]
        [TestCase(101)]
        public void TestSensitivityRejectsInvalidValues(double sensitivity)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(sensitivity: sensitivity));

        [Test]
        public void TestInvalidCalibrationAndThresholdsAreRejectedAtSettingsBoundary()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(mode: (OmsAxisMode)100));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(movementThreshold: -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(deadzone: 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(activationThreshold: 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(activationThreshold: 0.3, releaseThreshold: 0.4));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(revolutionsPerSecond: 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(stopTimeoutMilliseconds: 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(stopTimeoutMilliseconds: 251));
                Assert.Throws<ArgumentException>(() => new OmsAxisSettings(minimum: 1));
                Assert.Throws<ArgumentException>(() => new OmsAxisSettings(minimum: 1, maximum: 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OmsAxisSettings(minimum: 0, maximum: 100, centre: 100));
            });
        }
    }
}
