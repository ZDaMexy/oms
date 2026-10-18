// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLiftProjectionTest
    {
        [Test]
        public void TestUnavailableIsDistinctFromAnAppliedZeroAndRejectsInvalidState()
        {
            Assert.That(new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1).LiftState.IsAvailable, Is.False);
            Assert.That(new GameplaySkinLiftState(true, 0, 0, false).IsAvailable, Is.True);
            Assert.That(typeof(IGameplaySkinLiftStateSource).IsAssignableFrom(typeof(ManiaPlayfield)), Is.False,
                "Ordinary mania does not acquire a BMS playfield adjustment through the neutral producer.");
            foreach (double units in new[] { double.NaN, double.PositiveInfinity, -1, 1000.1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinLiftState(true, units, 0, false));
            foreach (double offset in new[] { double.NaN, double.PositiveInfinity, -0.1, 1.1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinLiftState(true, 0, offset, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinLiftState(false, 1, 0, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinLiftState(false, 0, 0.1, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinLiftState(false, 0, 0, true));
        }

        [TestCase(GameplaySkinEventKind.TimingBeat, false)]
        [TestCase(GameplaySkinEventKind.TimingBar, false)]
        [TestCase(GameplaySkinEventKind.TimingBpmChanged, false)]
        [TestCase(GameplaySkinEventKind.TimingScrollChanged, false)]
        [TestCase(GameplaySkinEventKind.TimingStopStarted, true)]
        [TestCase(GameplaySkinEventKind.TimingStopEnded, false)]
        public void TestEveryTimingRecordRetainsNativeDoublesAndIndependentStopFlag(GameplaySkinEventKind kind, bool stopped)
        {
            foreach (GameplaySkinLiftState lift in new[]
                     {
                         default,
                         new GameplaySkinLiftState(true, 0, 0, false),
                         new GameplaySkinLiftState(true, 1000, 1, true),
                         new GameplaySkinLiftState(true, double.Epsilon, double.Epsilon, true),
                         new GameplaySkinLiftState(true, 100.12345678901234, 0.12345678901234567, false),
                     })
            {
                var timing = new GameplaySkinTimingStateSnapshot(3.5, 2, 150, stopped, -0.5, 0.625, 2.25, lift);
                GameplaySkinEventValue compact = GameplaySkinEventValue.Timing(kind, timing);
                GameplaySkinTimingStateSnapshot decoded = compact.GetTiming();
                GameplaySkinTimingStateSnapshot materialized = ((GameplaySkinTimingEventPayload)compact.Materialize(null, null)).State;
                assertLift(decoded.LiftState, lift);
                assertLift(materialized.LiftState, lift);
                Assert.Multiple(() =>
                {
                    Assert.That(decoded.IsStopped, Is.EqualTo(stopped));
                    Assert.That(decoded.Beat, Is.EqualTo(3.5));
                    Assert.That(decoded.BarIndex, Is.EqualTo(2));
                    Assert.That(decoded.Bpm, Is.EqualTo(150));
                    Assert.That(decoded.ScrollMultiplier, Is.EqualTo(-0.5));
                    Assert.That(decoded.Progress, Is.EqualTo(0.625));
                    Assert.That(decoded.ScrollSpeed, Is.EqualTo(2.25));
                });
            }
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Rewind)]
        [TestCase(GameplaySkinEventResetReason.Retry)]
        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt)]
        public void TestLateConsumerAndEpochReplacementKeepAppliedLift(GameplaySkinEventResetReason reason)
        {
            var initialLift = new GameplaySkinLiftState(true, 150, 0.2, false);
            var nextLift = new GameplaySkinLiftState(true, 500, 0.75, true);
            using var stream = new GameplaySkinEventStream(GameplaySkinEventRevision.Create(1, 1, 1, 1), 0, state(initialLift));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription observer = stream.Subscribe();
            assertLift(((GameplaySkinStateEventPayload)dequeue(observer).Payload).State.Timing.LiftState, initialLift);
            stream.Publish(producer, 10, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingScrollChanged, timing(nextLift)), null, null);
            assertLift(((GameplaySkinTimingEventPayload)dequeue(observer).Payload).State.LiftState, nextLift);
            using (GameplaySkinEventSubscription late = stream.Subscribe())
                assertLift(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Timing.LiftState, nextLift);

            producer = producer.Reset(0, state(default), reason);
            GameplaySkinEventEnvelope reset = dequeue(observer);
            Assert.That(reset.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
            assertLift(((GameplaySkinStateEventPayload)reset.Payload).State.Timing.LiftState, default);
            using (GameplaySkinEventSubscription rebuilt = stream.Subscribe())
                assertLift(((GameplaySkinStateEventPayload)dequeue(rebuilt).Payload).State.Timing.LiftState, default);
            producer.Dispose();
        }

        [Test]
        public void TestQueuedTimingCannotObserveNewerLiftBeforeItsHighWater()
        {
            var applied = new GameplaySkinLiftState(true, 200, 0.2, false);
            var newer = new GameplaySkinLiftState(true, 600, 0.6, true);
            using var stream = new GameplaySkinEventStream(GameplaySkinEventRevision.Create(1, 1, 1, 1), 0, state(default));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription observer = stream.Subscribe();
            Assert.That(stream.TryDequeue(observer, out GameplaySkinEventRecord initial), Is.True);
            stream.Publish(producer, 10, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingScrollChanged, timing(applied)), null, null);
            producer.SynchroniseTiming(20, timing(newer));
            stream.ReadConsumerTimingHighWater(observer, initial.GameplayTime, initial.AuthoritativeTiming, out double beforeTime, out GameplaySkinTimingStateSnapshot before);
            Assert.That(beforeTime, Is.Zero);
            assertLift(before.LiftState, default);
            Assert.That(stream.TryDequeue(observer, out GameplaySkinEventRecord edge), Is.True);
            assertLift(edge.AuthoritativeTiming.LiftState, applied);
            assertLift(edge.Payload.GetTiming().LiftState, applied);
            stream.ReadConsumerTimingHighWater(observer, edge.GameplayTime, edge.AuthoritativeTiming, out double afterTime, out GameplaySkinTimingStateSnapshot after);
            Assert.That(afterTime, Is.EqualTo(20));
            assertLift(after.LiftState, newer);
        }

        private static void assertLift(GameplaySkinLiftState actual, GameplaySkinLiftState expected)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.IsAvailable, Is.EqualTo(expected.IsAvailable));
                Assert.That(actual.Reversed, Is.EqualTo(expected.Reversed));
                Assert.That(BitConverter.DoubleToInt64Bits(actual.Units), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Units)));
                Assert.That(BitConverter.DoubleToInt64Bits(actual.OffsetRatio), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.OffsetRatio)));
            });
        }

        private static GameplaySkinTimingStateSnapshot timing(GameplaySkinLiftState lift)
            => new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, liftState: lift);

        private static GameplaySkinEventStateSnapshot state(GameplaySkinLiftState lift)
            => new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Loaded, Array.Empty<GameplaySkinInputStateSnapshot>(),
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0), timing(lift), Array.Empty<GameplaySkinBgaStateSnapshot>());

        private static GameplaySkinEventEnvelope dequeue(GameplaySkinEventSubscription subscription)
        {
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return envelope!;
        }
    }
}
