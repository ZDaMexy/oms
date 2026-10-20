// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidAxisInputHandlerTest
    {
        [Test]
        public void TestMatchingDirectionPressesUntilPollingEnds()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginPolling();
            Assert.That(handler.ApplyAxisDelta(" TURNTABLE ", 0, 4), Is.True);
            Assert.That(handler.FinishPolling(), Is.True);

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestSameDirectionMovementRetriggersOnNextPoll()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginPolling();
            handler.ApplyAxisDelta("turntable", 0, 4);
            handler.FinishPolling();

            handler.BeginPolling();
            handler.ApplyAxisDelta("turntable", 0, 3);
            handler.FinishPolling();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestDirectionFlipAcrossPollsEmitsDistinctPulses()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginPolling();
            handler.ApplyAxisDelta("turntable", 0, 4);
            handler.FinishPolling();

            handler.BeginPolling();
            handler.ApplyAxisDelta("turntable", 0, -4);
            handler.FinishPolling();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestDirectionFlipWithinPollRetriggersPulse()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginPolling();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyAxisDelta("turntable", 0, 4), Is.True);
                Assert.That(handler.ApplyAxisDelta("turntable", 0, -4), Is.True);
                Assert.That(handler.FinishPolling(), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestAxisInversionFlipsDirection()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisInverted: true))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginPolling();
            handler.ApplyAxisDelta("turntable", 0, -3);
            handler.FinishPolling();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestCircularPositionUsesRangeAndDoesNotStrikeOnFirstSample()
        {
            var events = new List<string>();
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition)),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative, axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition))),
            }, _ => events.Add("press"), _ => events.Add("release"), motions.Add);

            Assert.That(handler.ApplyAxisChange(position(254, 500)), Is.False);
            Assert.That(motions, Is.Empty);
            Assert.That(handler.ApplyAxisChange(position(1, -253)), Is.True);
            handler.FinishPolling();
            Assert.That(handler.ApplyAxisChange(position(254, 253)), Is.True);
            handler.FinishPolling();

            Assert.Multiple(() =>
            {
                Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 3.0 / 256, -3.0 / 256 }));
                Assert.That(motions[0].SourceId, Is.EqualTo(motions[1].SourceId));
                Assert.That(motions[0].DeviceIdentifier, Is.EqualTo("turntable"));
                Assert.That(events, Is.EqualTo(new[] { "press", "release", "press", "release" }));
            });

            static OmsHidAxisChange position(int value, int delta)
                => new OmsHidAxisChange("turntable", 0, delta, value, 0, 255, false);
        }

        [Test]
        public void TestCircularRangeChangeAndReconnectRebaseWithoutInventingMovement()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition))),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 0, 100, 0, 255, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 1, 101, 0, 255, false));
            Assert.That(motions, Has.Count.EqualTo(1));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 1, 500, 0, 1023, false));
            Assert.That(motions, Has.Count.EqualTo(1));
            handler.ReleaseDevice(" TURNTABLE ");
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 0, 900, 0, 1023, false));
            Assert.That(motions, Has.Count.EqualTo(1));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 1, 901, 0, 1023, false));
            Assert.That(motions[1].Delta, Is.EqualTo(1.0 / 1024));
            Assert.That(motions[1].SourceId, Is.EqualTo(motions[0].SourceId));
        }

        [TestCase(1, -253, 3)]
        [TestCase(254, 253, -3)]
        public void TestCircularResetRecoversFirstFreshWrappedMovement(int value, int delta, int wrappedDelta)
        {
            var motions = new List<OmsAxisMotion>();
            var events = new List<bool>();
            var settings = new OmsAxisSettings(OmsAxisMode.CircularPosition);
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: settings),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative, axisSettings: settings)),
            }, _ => events.Add(true), _ => events.Add(false), motions.Add);

            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 0, 128, 0, 255, false));
            handler.Reset();
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, delta, value, 0, 255, false));
            handler.FinishPolling();
            Assert.Multiple(() =>
            {
                Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { wrappedDelta / 256.0 }));
                Assert.That(events, Is.EqualTo(new[] { true, false }));
            });
        }

        [Test]
        public void TestCircularRecoveryRejectsMissingRangeAndOutOfRangePreviousPosition()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition))),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 1, 1, null, null, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 1, 1, 0, 0, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, int.MinValue, 1, 0, 255, false));
            Assert.That(motions, Is.Empty, "An unavailable range or impossible previous position cannot be guessed.");
            handler.Reset();
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 0, 254, 0, 255, false));
            Assert.That(motions, Is.Empty, "A new zero-delta position only calibrates the device.");
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, -253, 1, 0, 255, false));
            Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 3.0 / 256 }));
        }

        [Test]
        public void TestPositionModeDoesNotFallBackToReportedDeltaWithoutAbsoluteMetadata()
        {
            int presses = 0;
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition))),
            }, _ => presses++, _ => { });

            handler.ApplyAxisDelta("turntable", 0, 100);
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 100, 100, null, null, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 100, 100, 0, 255, true));
            Assert.That(presses, Is.Zero);
        }

        [Test]
        public void TestCentredPositionStaysHeldAcrossPollsWithHysteresisAndNoReturnStroke()
        {
            var events = new List<string>();
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition))),
                new OmsBinding(OmsAction.Key2P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Negative, axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition))),
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), motions.Add);

            Assert.That(handler.ApplyAxisChange(position(60)), Is.True);
            Assert.That(handler.FinishPolling(), Is.False);
            Assert.That(handler.ApplyAxisChange(position(45)), Is.False);
            Assert.That(handler.ApplyAxisChange(position(45)), Is.False);
            Assert.That(handler.ApplyAxisChange(position(35)), Is.True);
            Assert.That(handler.ApplyAxisChange(position(0)), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
                Assert.That(motions, Is.Empty);
            });

            static OmsHidAxisChange position(int value)
                => new OmsHidAxisChange("stick", 0, -60, value, -100, 100, false);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void TestCentredEqualThresholdRemainsHeldForRepeatedBoundaryReports(int direction)
        {
            var events = new List<bool>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, (OmsAxisDirection)direction,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition, activationThreshold: 0.5, releaseThreshold: 0.5))),
            }, _ => events.Add(true), _ => events.Add(false));

            for (int index = 0; index < 5; index++)
            {
                handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, direction * 50, -100, 100, false));
                handler.FinishPolling();
            }
            Assert.That(events, Is.EqualTo(new[] { true }));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, direction * 49, -100, 100, false));
            Assert.That(events, Is.EqualTo(new[] { true, false }));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void TestCentredZeroReleaseThresholdStillReleasesAtCentre(int direction)
        {
            var events = new List<bool>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, (OmsAxisDirection)direction,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition, releaseThreshold: 0))),
            }, _ => events.Add(true), _ => events.Add(false));

            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, direction * 50, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, direction, -100, 100, false));
            Assert.That(events, Is.EqualTo(new[] { true }));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 0, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 0, -100, 100, false));
            Assert.That(events, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TestCentredEqualThresholdReleasesOldDirectionBeforeOppositePress()
        {
            var events = new List<string>();
            var settings = new OmsAxisSettings(OmsAxisMode.CentredPosition, activationThreshold: 0.5, releaseThreshold: 0.5);
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive, axisSettings: settings)),
                new OmsBinding(OmsAction.Key2P_Scratch, OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Negative, axisSettings: settings)),
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 50, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, -50, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, -50, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 0, -100, 100, false));
            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key2P_Scratch", "-Key2P_Scratch" }));
        }

        [Test]
        public void TestExplicitCalibrationAndInversionApplyToPosition()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive, axisInverted: true,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition, minimum: 10, maximum: 90, centre: 30))),
            }, _ => events.Add("press"), _ => events.Add("release"));

            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 20, 0, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 0, 0, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 30, 0, 100, false));
            Assert.That(events, Is.EqualTo(new[] { "press", "release" }));
        }

        [Test]
        public void TestReleasingOneDeviceDoesNotReleaseOtherHeldPositionSource()
        {
            int presses = 0;
            int releases = 0;
            var settings = new OmsAxisSettings(OmsAxisMode.CentredPosition);
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("first", 0, OmsAxisDirection.Positive, axisSettings: settings),
                    OmsBindingTrigger.HidAxis("second", 0, OmsAxisDirection.Positive, axisSettings: settings)),
            }, _ => presses++, _ => releases++);

            handler.ApplyAxisChange(new OmsHidAxisChange("first", 0, 0, 100, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("second", 0, 0, 100, -100, 100, false));
            Assert.That(handler.ReleaseDevice("first"), Is.False);
            Assert.That(releases, Is.Zero);
            Assert.That(handler.ReleaseDevice("second"), Is.True);
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void TestAcceptedMotionPrecedesPulseAndOnlyMatchesConfiguredDirection()
        {
            var events = new List<string>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(stopTimeoutMilliseconds: 75))),
            }, _ => events.Add("press"), _ => events.Add("release"), motion =>
            {
                events.Add("motion");
                Assert.That(motion.StopTimeoutMilliseconds, Is.EqualTo(75));
                Assert.That(motion.SourceId, Is.Not.Empty);
            });

            handler.ApplyAxisDelta("turntable", 0, -10);
            Assert.That(events, Is.Empty);
            handler.ApplyAxisDelta("turntable", 0, 10);
            handler.FinishPolling();
            Assert.That(events, Is.EqualTo(new[] { "motion", "press", "release" }));
        }

        [Test]
        public void TestDuplicateDirectionsAndEquivalentSettingsDoNotDuplicateAcceptedMotion()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings()),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings()),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative, axisSettings: new OmsAxisSettings())),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisDelta("turntable", 0, 10);
            handler.ApplyAxisDelta("turntable", 0, -10);
            Assert.That(motions, Has.Count.EqualTo(2));
            Assert.That(motions[0].SourceId, Is.SameAs(motions[1].SourceId));
        }

        [Test]
        public void TestThresholdAccumulatesSlowMovementAndOppositeMovementCancelsIt()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(movementThreshold: 2))),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisDelta("turntable", 0, 1);
            handler.FinishPolling();
            handler.ApplyAxisDelta("turntable", 0, -1);
            handler.ApplyAxisDelta("turntable", 0, 2);
            Assert.That(motions, Is.Empty);
            handler.ApplyAxisDelta("turntable", 0, 1);
            Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 3.0 }));
        }

        [Test]
        public void TestInvertingMinimumIntegerDoesNotOverflow()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive, axisInverted: true)),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisDelta("turntable", 0, int.MinValue);
            Assert.That(motions[0].Delta, Is.EqualTo(2147483648.0));
        }

        [Test]
        public void TestStickHeldPositionProducesTimedMotionAndRecenteringDoesNotReverse()
        {
            var events = new List<string>();
            var motions = new List<OmsAxisMotion>();
            var settings = new OmsAxisSettings(OmsAxisMode.StickToMotion);
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive, axisSettings: settings),
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Negative, axisSettings: settings)),
            }, _ => events.Add("press"), _ => events.Add("release"), motion =>
            {
                events.Add("motion");
                motions.Add(motion);
            });

            handler.Update(0);
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 100, 100, -100, 100, false));
            handler.FinishPolling();
            Assert.That(events, Is.Empty);
            handler.Update(250);
            handler.FinishPolling();
            handler.Update(500);
            handler.FinishPolling();
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, -100, 0, -100, 100, false));
            handler.Update(750);
            Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 0.25, 0.25 }));
            Assert.That(events, Is.EqualTo(new[] { "motion", "press", "release", "motion", "press", "release" }));
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(120)]
        public void TestStickDisplacementUsesElapsedTimeRatherThanPollCount(int updates)
        {
            double movement = 0;
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.StickToMotion, sensitivity: 0.5, revolutionsPerSecond: 2))),
            }, _ => { }, _ => { }, motion => movement += motion.Delta);

            handler.Update(0);
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 100, 100, -100, 100, false));
            for (int i = 1; i <= updates; i++)
            {
                handler.Update(1000.0 * i / updates);
                handler.FinishPolling();
            }

            Assert.That(movement, Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void TestResetDiscardsPendingMovementAndStickElapsedTime()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.StickToMotion))),
            }, _ => { }, _ => { }, motions.Add);

            handler.Update(0);
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 100, -100, 100, false));
            handler.Update(100);
            handler.Reset();
            handler.Update(5000);
            Assert.That(motions, Has.Count.EqualTo(1));
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 0, 100, -100, 100, false));
            handler.Update(5010);
            Assert.That(motions[1].Delta, Is.EqualTo(0.01));
        }
    }
}
