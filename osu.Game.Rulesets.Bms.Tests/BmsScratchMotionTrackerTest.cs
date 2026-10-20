// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using oms.Input;
using osu.Game.Rulesets.Bms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsScratchMotionTrackerTest
    {
        [Test]
        public void TestContinuousMotionAndIndependentSourcesKeepTheFinalRelease()
        {
            var edges = new List<(BmsAction Action, bool Pressed)>();
            var tracker = new BmsScratchMotionTracker(action => edges.Add((action, true)), action => edges.Add((action, false)));
            tracker.Observe(motion("mouse", null), 0);
            tracker.Observe(motion("deck", "device-a"), 20);
            tracker.Observe(motion("mouse", null), 40);
            tracker.Update(70);
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true) }));
            Assert.That(tracker.NextExpiry, Is.EqualTo(90));
            tracker.Update(90);
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true), (BmsAction.Scratch1Motion, false) }));
            Assert.That(tracker.NextExpiry, Is.Null);
        }

        [Test]
        public void TestUnplugOnlyReleasesThatDevicesMovement()
        {
            var edges = new List<(BmsAction Action, bool Pressed)>();
            var tracker = new BmsScratchMotionTracker(action => edges.Add((action, true)), action => edges.Add((action, false)));
            tracker.Observe(motion("left", "left-device"), 0);
            tracker.Observe(motion("right", "right-device"), 0);
            tracker.ReleaseDevice("left-device");
            Assert.That(edges, Has.Count.EqualTo(1));
            tracker.ReleaseDevice("right-device");
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true), (BmsAction.Scratch1Motion, false) }));
        }

        [Test]
        public void TestEpochResetReleasesBothSidesWithoutKeepingOldTimeouts()
        {
            var released = new List<BmsAction>();
            var tracker = new BmsScratchMotionTracker(_ => { }, released.Add);
            tracker.Observe(motion("left", "left-device"), 1000);
            tracker.Observe(new OmsAxisMotion(OmsAction.Key2P_Scratch, OmsAxisMode.RelativeMotion, -1, "right", "right-device", 50), 1000);
            tracker.Clear();
            tracker.Update(0);
            Assert.That(released, Is.EquivalentTo(new[] { BmsAction.Scratch1Motion, BmsAction.Scratch2Motion }));
            Assert.That(tracker.NextExpiry, Is.Null);
        }

        [Test]
        public void TestMovementOnlyProducesContinuityActions()
        {
            var pressed = new List<BmsAction>();
            var tracker = new BmsScratchMotionTracker(pressed.Add, _ => { });
            tracker.Observe(motion("mouse", null), 0);
            tracker.Observe(motion("mouse", null), 20);
            tracker.Observe(new OmsAxisMotion(OmsAction.Key1P_1, OmsAxisMode.RelativeMotion, 1, "other", null, 50), 20);
            Assert.That(pressed, Is.EqualTo(new[] { BmsAction.Scratch1Motion }));
            Assert.That(pressed.Exists(action => action.IsLaneAction()), Is.False);
        }

        [Test]
        public void TestReachedExpiryKeepsOtherMotionAcrossProjectionRounding()
        {
            var edges = new List<(BmsAction Action, bool Pressed)>();
            var tracker = new BmsScratchMotionTracker(action => edges.Add((action, true)), action => edges.Add((action, false)));
            tracker.Observe(motion("mouse", null), 100);
            tracker.Observe(motion("deck", "device-a"), 125);
            tracker.Update(125);
            tracker.Update(Math.BitDecrement(125));
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true) }));

            double deviceTime = Math.BitDecrement(150);
            tracker.Update(deviceTime, reachedExpiry: 150);
            tracker.Update(deviceTime);
            Assert.That(tracker.NextExpiry, Is.EqualTo(175));
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true) }));

            tracker.Update(175);
            Assert.That(edges, Is.EqualTo(new[] { (BmsAction.Scratch1Motion, true), (BmsAction.Scratch1Motion, false) }));
        }

        private static OmsAxisMotion motion(string source, string? device)
            => new OmsAxisMotion(OmsAction.Key1P_Scratch, OmsAxisMode.RelativeMotion, 1, source, device, 50);
    }
}
