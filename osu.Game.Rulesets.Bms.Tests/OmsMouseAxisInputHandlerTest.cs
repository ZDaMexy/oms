// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsMouseAxisInputHandlerTest
    {
        [Test]
        public void TestMatchingDirectionPressesUntilFrameEnds()
        {
            var events = new List<string>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginFrame();
            Assert.That(handler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
            Assert.That(handler.FinishFrame(), Is.True);

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestSameDirectionMovementRetriggersOnNextFrame()
        {
            var events = new List<string>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, 4);
            handler.FinishFrame();

            handler.BeginFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, 3);
            handler.FinishFrame();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestDirectionFlipAcrossFramesEmitsDistinctPulses()
        {
            var events = new List<string>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, 4);
            handler.FinishFrame();

            handler.BeginFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, -4);
            handler.FinishFrame();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestDirectionFlipWithinFrameRetriggersPulse()
        {
            var events = new List<string>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginFrame();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(handler.ApplyAxisDelta(OmsMouseAxis.X, -4), Is.True);
                Assert.That(handler.FinishFrame(), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestAxisInversionFlipsDirection()
        {
            var events = new List<string>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive, axisInverted: true))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            handler.BeginFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, -3);
            handler.FinishFrame();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestMotionPrecedesPulseAndBothDirectionsShareStableSource()
        {
            var events = new List<string>();
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative)),
            }, _ => events.Add("press"), _ => events.Add("release"), motion =>
            {
                events.Add("motion");
                motions.Add(motion);
            });

            handler.ApplyAxisDelta(OmsMouseAxis.X, 4);
            handler.ApplyAxisDelta(OmsMouseAxis.X, -4);
            handler.FinishFrame();

            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(new[] { "motion", "press", "release", "motion", "press", "release" }));
                Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 4.0, -4.0 }));
                Assert.That(motions[0].SourceId, Is.SameAs(motions[1].SourceId));
                Assert.That(motions[0].DeviceIdentifier, Is.Null);
                Assert.That(motions[0].StopTimeoutMilliseconds, Is.EqualTo(50));
            });
        }

        [Test]
        public void TestOnlyBoundDirectionProducesAcceptedMotion()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisDelta(OmsMouseAxis.X, -4);
            Assert.That(motions, Is.Empty);
            handler.ApplyAxisDelta(OmsMouseAxis.X, 4);
            Assert.That(motions, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestThresholdAccumulatesSlowMovementAcrossFramesAndResetClearsIt()
        {
            var motions = new List<OmsAxisMotion>();
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(sensitivity: 2, movementThreshold: 2))),
            }, _ => { }, _ => { }, motions.Add);

            handler.ApplyAxisDelta(OmsMouseAxis.X, 0.5f);
            handler.FinishFrame();
            handler.ApplyAxisDelta(OmsMouseAxis.X, 0.5f);
            Assert.That(motions, Is.Empty);
            handler.ApplyAxisDelta(OmsMouseAxis.X, 0.5f);
            Assert.That(motions[0].Delta, Is.EqualTo(3));

            handler.ApplyAxisDelta(OmsMouseAxis.X, 1);
            handler.Reset();
            handler.ApplyAxisDelta(OmsMouseAxis.X, 0.5f);
            Assert.That(motions, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestResetReleasesAllAxisSourcesOnlyOnce()
        {
            int presses = 0;
            int releases = 0;
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.Y, OmsAxisDirection.Positive)),
            }, _ => presses++, _ => releases++);

            handler.ApplyAxisDelta(OmsMouseAxis.X, 1);
            handler.ApplyAxisDelta(OmsMouseAxis.Y, 1);
            Assert.That(handler.ReleaseAll(), Is.True);
            Assert.That(handler.ReleaseAll(), Is.False);
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(releases, Is.EqualTo(1));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TestNonFiniteMouseReportsDoNotProduceMotion(float delta)
        {
            int presses = 0;
            int motions = 0;
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)),
            }, _ => presses++, _ => { }, _ => motions++);

            Assert.That(handler.ApplyAxisDelta(OmsMouseAxis.X, delta), Is.False);
            handler.ApplyAxisDelta(OmsMouseAxis.X, 1);
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(motions, Is.EqualTo(1));
        }

        [Test]
        public void TestCapturePrefersDominantAxis()
        {
            Assert.That(OmsMouseAxisCapture.TryResolve(9, -3, 8, out var axis, out var direction), Is.True);
            Assert.That(axis, Is.EqualTo(OmsMouseAxis.X));
            Assert.That(direction, Is.EqualTo(OmsAxisDirection.Positive));
        }

        [Test]
        public void TestCaptureResolvesNegativeVerticalMovement()
        {
            Assert.That(OmsMouseAxisCapture.TryResolve(4, -11, 8, out var axis, out var direction), Is.True);
            Assert.That(axis, Is.EqualTo(OmsMouseAxis.Y));
            Assert.That(direction, Is.EqualTo(OmsAxisDirection.Negative));
        }

        [Test]
        public void TestCaptureRequiresThreshold()
        {
            Assert.That(OmsMouseAxisCapture.TryResolve(5, -6, 8, out _, out _), Is.False);
        }

        [Test]
        public void TestCaptureTieFallsBackToHorizontalAxis()
        {
            Assert.That(OmsMouseAxisCapture.TryResolve(-8, 8, 8, out var axis, out var direction), Is.True);
            Assert.That(axis, Is.EqualTo(OmsMouseAxis.X));
            Assert.That(direction, Is.EqualTo(OmsAxisDirection.Negative));
        }
    }
}
