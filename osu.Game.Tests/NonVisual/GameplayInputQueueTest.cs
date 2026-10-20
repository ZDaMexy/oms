// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using osu.Game.Input;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameplayInputQueueTest
    {
        private const long clock_origin = 10_000_000_000;

        [Test]
        public void TestCatchUpPreservesCompleteTapsAndLeavesFutureReportsForTheNextFrame()
        {
            double visitedTime = 1000;
            var received = new List<(int Value, double Time)>();
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(value => received.Add((value, visitedTime)), () => { });
            queue.PrepareFrame(clock, visitedTime, true);
            queue.Enqueue(1, stamp(1040));
            queue.Enqueue(2, stamp(1040));
            queue.Enqueue(3, stamp(1080));
            queue.Enqueue(4, stamp(1080));
            queue.Enqueue(5, stamp(1120));
            clock.InputTime = sample(1100);
            queue.PrepareFrame(clock, visitedTime, true);
            Assert.That(queue.NextInputTime, Is.EqualTo(1040));
            visit(1040);
            visit(1080);
            visit(1100);
            Assert.That(received, Is.EqualTo(new[] { (1, 1040d), (2, 1040d), (3, 1080d), (4, 1080d) }));
            Assert.That(queue.NextInputTime, Is.Null);

            queue.Enqueue(6, stamp(1150));
            clock.InputTime = sample(1130);
            queue.PrepareFrame(clock, visitedTime, true);
            Assert.That(queue.NextInputTime, Is.EqualTo(1120));
            visit(1120);
            clock.InputTime = sample(1160);
            queue.PrepareFrame(clock, visitedTime, true);
            visit(1150);
            Assert.That(received, Is.EqualTo(new[] { (1, 1040d), (2, 1040d), (3, 1080d), (4, 1080d), (5, 1120d), (6, 1150d) }));

            void visit(double time)
            {
                visitedTime = time;
                queue.DispatchDue(time);
            }
        }

        [Test]
        public void TestAnObservationWithoutATimestampedClockDoesNotEnterHistoricalCatchUp()
        {
            int received = 0;
            using var queue = new GameplayInputQueue<int>(value => received += value, () => { });
            queue.Enqueue(1, stamp(1100));
            queue.PrepareFrame(null, 1000, true, observationTime: 1100);
            queue.DispatchDue(1050);
            Assert.That(received, Is.Zero);
            Assert.That(queue.NextInputTime, Is.EqualTo(1100));
            queue.DispatchDue(1100);
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void TestAFutureMouseReceiptCannotBlockAnAlreadyCapturedDeviceObservation()
        {
            var received = new List<int>();
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(received.Add, () => { });
            queue.PrepareFrame(clock, 1000, true);
            queue.Enqueue(1, stamp(1101));
            queue.EnqueueObserved(2, 1100, captureClockEpoch: 0);
            clock.InputTime = sample(1100);
            queue.PrepareFrame(clock, 1000, true);
            Assert.That(queue.NextInputTime, Is.EqualTo(1100));
            queue.DispatchDue(1100);
            Assert.That(received, Is.EqualTo(new[] { 2 }));

            clock.InputTime = sample(1102);
            queue.PrepareFrame(clock, 1100, true);
            queue.DispatchDue(1102);
            Assert.That(received, Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void TestPolledObservationsCarryTheirCaptureEpochAcrossAClockChange()
        {
            var received = new List<int>();
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(received.Add, () => { });
            queue.PrepareFrame(clock, 1000, true);
            queue.EnqueueObserved(1, 1150, captureClockEpoch: 0);
            queue.EnqueueObserved(2, 1160, captureClockEpoch: 1);
            clock.InputTime = new GameplayInputTime(1200, stamp(1200), 1, true, 1, stamp(1100));
            queue.PrepareFrame(clock, 1100, true);
            queue.DispatchDue(1200);
            Assert.That(received, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void TestLateInputCannotRewindPreviouslyCommittedGameplay()
        {
            int received = 0;
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(value => received += value, () => { });
            queue.PrepareFrame(clock, 1000, true);
            queue.Enqueue(1, stamp(1040));
            clock.InputTime = sample(1200);
            queue.PrepareFrame(clock, 1100, true);
            Assert.That(queue.NextInputTime, Is.EqualTo(1100));
            queue.DispatchDue(1099);
            Assert.That(received, Is.Zero);
            queue.DispatchDue(1100);
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void TestClockEpochDropsOldReportsAndReleasesOnlyItsConsumer()
        {
            var received = new List<int>();
            int releases = 0;
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(received.Add, () => releases++);
            queue.PrepareFrame(clock, 1000, true);
            queue.Enqueue(1, stamp(1040));
            clock.InputTime = sample(1100);
            queue.PrepareFrame(clock, 1000, true);

            queue.Enqueue(2, stamp(1080));
            queue.Enqueue(3, stamp(1120));
            clock.InputTime = new GameplayInputTime(1200, stamp(1200), 1, true, 1, stamp(1100));
            queue.PrepareFrame(clock, 1100, true);
            queue.DispatchDue(1200);
            Assert.That(received, Is.EqualTo(new[] { 3 }));
            Assert.That(releases, Is.EqualTo(1));
            queue.DispatchDue(1200);
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void TestExplicitResetInvalidatesAnAlreadyFrozenBatchAndAcceptsFreshInput()
        {
            var received = new List<int>();
            int releases = 0;
            var clock = new InputClock { InputTime = sample(1000) };
            using var queue = new GameplayInputQueue<int>(received.Add, () => releases++);
            queue.PrepareFrame(clock, 1000, true);
            queue.Enqueue(1, stamp(1040));
            clock.InputTime = sample(1100);
            queue.PrepareFrame(clock, 1000, true);
            queue.Reset();
            queue.DispatchDue(1100);
            Assert.That(received, Is.Empty);
            Assert.That(releases, Is.EqualTo(1));
            queue.Enqueue(2, stamp(1120));
            clock.InputTime = sample(1130);
            queue.PrepareFrame(clock, 1100, true);
            queue.DispatchDue(1130);
            Assert.That(received, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void TestStoppedClockKeepsInputOnItsCurrentTime()
        {
            int received = 0;
            var clock = new InputClock { InputTime = new GameplayInputTime(1000, stamp(1100), 1, false, 0, clock_origin) };
            using var queue = new GameplayInputQueue<int>(value => received += value, () => { });
            queue.Enqueue(1, stamp(1050));
            queue.PrepareFrame(clock, 1000, true);
            Assert.That(queue.NextInputTime, Is.EqualTo(1000));
            queue.DispatchDue(1000);
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void TestClosedInputCannotReappearInANewOwner()
        {
            var queue = new GameplayInputQueue<int>(_ => Assert.Fail("Closed producer was applied."), () => { });
            queue.Dispose();
            Assert.That(queue.Enqueue(1, stamp(1000)), Is.False);
            queue.DispatchDue(1000);
        }

        [Test]
        public void TestResetInAnIsolatedPreviewDoesNotReleaseTheNewPressOnEveryUpdate()
        {
            bool held = false;
            using var queue = new GameplayInputQueue<int>(value => held = value == 1, () => held = false);
            queue.Enqueue(1, stamp(1000));
            queue.DispatchDue(1000);
            Assert.That(held, Is.True);
            queue.Reset();
            queue.Enqueue(1, stamp(1010));
            queue.DispatchDue(1010);
            Assert.That(held, Is.True);
            queue.DispatchDue(1020);
            Assert.That(held, Is.True);
        }

        [Test]
        public void TestSteadyReceiptTimeDeliveryDoesNotAllocateAfterWarmup()
        {
            int received = 0;
            var clock = new InputClock();
            using var queue = new GameplayInputQueue<int>(value => received += value, () => { });
            for (int frame = 1; frame <= 256; frame++)
                deliver(frame);
            received = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 257; frame < 257 + 16384; frame++)
                deliver(frame);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"timestamped-input: reports=32768 allocated={allocated}");
            Assert.That(received, Is.EqualTo(32768));
            Assert.That(allocated, Is.Zero);

            void deliver(int time)
            {
                queue.Enqueue(1, stamp(time - 0.5));
                queue.Enqueue(1, stamp(time - 0.25));
                clock.InputTime = sample(time);
                queue.PrepareFrame(clock, time - 1, true);
                queue.DispatchDue(time);
            }
        }

        private static long stamp(double milliseconds) => clock_origin + (long)(milliseconds * Stopwatch.Frequency / 1000);

        private static GameplayInputTime sample(double time) => new GameplayInputTime(time, stamp(time), 1, true, 0, clock_origin);

        private class InputClock : IGameplayInputTimeSource
        {
            public GameplayInputTime? InputTime { get; set; }
        }
    }
}
