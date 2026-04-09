// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
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
    }
}
