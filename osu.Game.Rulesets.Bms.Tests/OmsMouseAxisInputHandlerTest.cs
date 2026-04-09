// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
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
