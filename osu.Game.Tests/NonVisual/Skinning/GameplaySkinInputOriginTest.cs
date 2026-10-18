// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinInputOriginTest
    {
        private static readonly GameplaySkinLaneGroupId group = GameplaySkinLaneGroupId.Create("stage.primary");
        private static readonly GameplaySkinLaneId first_lane = GameplaySkinLaneId.Create("stage.primary.key1");
        private static readonly GameplaySkinLaneId second_lane = GameplaySkinLaneId.Create("stage.primary.key2");

        [Test]
        public void TestLegacyAndUnknownPressStateDoNotInventAnOrigin()
        {
            var held = new GameplaySkinInputStateSnapshot(group, first_lane, true, 1);
            Assert.That(held.PressedAt, Is.Null);
            Assert.That(held.ReleasedAt, Is.Null);

            GameplaySkinInputStateSnapshot unchanged = GameplaySkinEventRuntimeHost.SnapshotInputAt(held, true, 0.5f, 900);
            Assert.That(unchanged.PressedAt, Is.Null, "A held-state sample is not a new input edge.");
            Assert.That(unchanged.Strength, Is.EqualTo(0.5f));

            GameplaySkinInputStateSnapshot released = GameplaySkinEventRuntimeHost.SnapshotInputAt(unchanged, false, 0, 1000.25);
            Assert.That(released.IsPressed, Is.False);
            Assert.That(released.PressedAt, Is.Null);
            Assert.That(released.ReleasedAt, Is.EqualTo(1000.25), "An observed release remains usable even when the press predates the epoch.");
            GameplaySkinInputStateSnapshot repeated = GameplaySkinEventRuntimeHost.SnapshotInputAt(released, false, 0, 1100);
            Assert.That(repeated.ReleasedAt, Is.EqualTo(1000.25));
        }

        [Test]
        public void TestInputEdgesKeepIndependentLaneOriginsAndClearOnlyTheirOwnRelease()
        {
            GameplaySkinInputStateSnapshot first = GameplaySkinEventRuntimeHost.SnapshotInputAt(input(first_lane, false), true, 1, -20.5);
            GameplaySkinInputStateSnapshot second = GameplaySkinEventRuntimeHost.SnapshotInputAt(input(second_lane, false), true, 0.75f, 0);
            first = GameplaySkinEventRuntimeHost.SnapshotInputAt(first, true, 0.5f, 10);
            Assert.That(first.PressedAt, Is.EqualTo(-20.5));
            first = GameplaySkinEventRuntimeHost.SnapshotInputAt(first, false, 0, 30.25);
            Assert.That(first.PressedAt, Is.EqualTo(-20.5));
            Assert.That(first.ReleasedAt, Is.EqualTo(30.25));
            Assert.That(second.PressedAt, Is.Zero, "Time zero is a real edge, not an unavailable sentinel.");
            Assert.That(second.IsPressed, Is.True);
            Assert.That(second.ReleasedAt, Is.Null);
            first = GameplaySkinEventRuntimeHost.SnapshotInputAt(first, true, 1, 40);
            Assert.That(first.PressedAt, Is.EqualTo(40));
            Assert.That(first.ReleasedAt, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestCompactInputAndTypedPayloadRetainOriginBits(bool pressed)
        {
            double press = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(-12.25) + 1);
            double release = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(19.5) + 1);
            var original = new GameplaySkinInputStateSnapshot(group, first_lane, pressed, pressed ? 0.75f : 0, press, pressed ? null : release);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Input(pressed ? GameplaySkinEventKind.InputPressed : GameplaySkinEventKind.InputReleased, original);
            assertInput(compact.GetInput(group, first_lane), original);
            assertInput(((GameplaySkinInputEventPayload)compact.Materialize(group, first_lane)).State, original);
            Assert.That(typeof(GameplaySkinInputStateSnapshot).GetProperties().All(property => property.SetMethod == null), Is.True);
            Assert.That(typeof(GameplaySkinInputStateSnapshot).GetConstructors(), Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestEpochBarrierDiscardsFutureHistoryWithoutChangingTheObservedInput(bool pressed)
        {
            var input = new GameplaySkinInputStateSnapshot(group, first_lane, pressed, pressed ? 0.75f : 0,
                900, pressed ? null : 1200);
            GameplaySkinInputStateSnapshot atEarlierClock = GameplaySkinEventRuntimeHost.SnapshotInputForEpoch(input, 500);
            Assert.Multiple(() =>
            {
                Assert.That(atEarlierClock.GroupId, Is.EqualTo(input.GroupId));
                Assert.That(atEarlierClock.LaneId, Is.EqualTo(input.LaneId));
                Assert.That(atEarlierClock.IsPressed, Is.EqualTo(input.IsPressed));
                Assert.That(atEarlierClock.Strength, Is.EqualTo(input.Strength));
                Assert.That(atEarlierClock.PressedAt, Is.Null);
                Assert.That(atEarlierClock.ReleasedAt, Is.Null);
            });
            assertInput(GameplaySkinEventRuntimeHost.SnapshotInputForEpoch(input, 1500), input);
        }

        [Test]
        public void TestFutureReleaseInvalidatesItsOldPressButDoesNotEraseOtherLanes()
        {
            var released = new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, -20, 1000);
            GameplaySkinInputStateSnapshot rewound = GameplaySkinEventRuntimeHost.SnapshotInputForEpoch(released, 0);
            Assert.That(rewound.IsPressed, Is.False);
            Assert.That(rewound.PressedAt, Is.Null, "A future release belongs to a discarded input history even if that history's press was earlier.");
            Assert.That(rewound.ReleasedAt, Is.Null);
            var otherLane = new GameplaySkinInputStateSnapshot(group, second_lane, true, 0.5f, -10);
            assertInput(GameplaySkinEventRuntimeHost.SnapshotInputForEpoch(otherLane, 0), otherLane);
        }

        [Test]
        public void TestRealReleaseDuringPendingClockRollbackCannotPrecedeAnOldFuturePress()
        {
            var oldPress = new GameplaySkinInputStateSnapshot(group, first_lane, true, 1, 2170);
            GameplaySkinInputStateSnapshot held = GameplaySkinEventRuntimeHost.SnapshotInputAt(oldPress, true, 0.5f, 0);
            Assert.That(held.IsPressed, Is.True);
            Assert.That(held.PressedAt, Is.Null, "An unchanged held sample must not invent a zero-time press.");
            GameplaySkinInputStateSnapshot release = GameplaySkinEventRuntimeHost.SnapshotInputAt(oldPress, false, 0, 0);
            Assert.That(release.PressedAt, Is.Null);
            Assert.That(release.ReleasedAt, Is.Zero, "The actual new release can be retained without an old-epoch press.");
            var oldRelease = new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, 2000, 2170);
            GameplaySkinInputStateSnapshot press = GameplaySkinEventRuntimeHost.SnapshotInputAt(oldRelease, true, 1, 0);
            Assert.That(press.PressedAt, Is.Zero);
            Assert.That(press.ReleasedAt, Is.Null);
        }

        [Test]
        public void TestCompactInputDistinguishesZeroAndMissingOrigins()
        {
            var pressedAtZero = new GameplaySkinInputStateSnapshot(group, first_lane, true, 1, -0.0);
            assertInput(GameplaySkinEventValue.Input(GameplaySkinEventKind.InputPressed, pressedAtZero).GetInput(group, first_lane), pressedAtZero);
            var releaseOnly = new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, releasedAt: 0);
            assertInput(GameplaySkinEventValue.Input(GameplaySkinEventKind.InputReleased, releaseOnly).GetInput(group, first_lane), releaseOnly);
            GameplaySkinInputStateSnapshot unknown = input(first_lane, true);
            assertInput(GameplaySkinEventValue.Input(GameplaySkinEventKind.InputPressed, unknown).GetInput(group, first_lane), unknown);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestInputOriginsMustBeFinite(double invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinInputStateSnapshot(group, first_lane, true, 1, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, releasedAt: invalid));
        }

        [Test]
        public void TestInputOriginsRejectContradictoryState()
        {
            Assert.Throws<ArgumentException>(() => new GameplaySkinInputStateSnapshot(group, first_lane, true, 1, 10, 20));
            Assert.Throws<ArgumentException>(() => new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, 20, 10));
        }

        [Test]
        public void TestLateAttachAndFractionalTimingHighWaterPreserveTheActualEdge()
        {
            using var stream = new GameplaySkinEventStream(revision, -100, state(input(first_lane, false), input(second_lane, false)));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinInputStateSnapshot held = GameplaySkinEventRuntimeHost.SnapshotInputAt(input(first_lane, false), true, 1, -25.125);
            publish(stream, producer, -25.125, held);
            producer.SynchroniseTiming(110.375, timing);
            using (GameplaySkinEventSubscription late = stream.Subscribe())
            {
                GameplaySkinEventEnvelope envelope = dequeue(late);
                Assert.That(envelope.GameplayTime, Is.EqualTo(110.375));
                assertInput(((GameplaySkinStateEventPayload)envelope.Payload).State.Inputs.Single(value => value.LaneId == first_lane), held);
            }

            GameplaySkinInputStateSnapshot released = GameplaySkinEventRuntimeHost.SnapshotInputAt(held, false, 0, 120.625);
            publish(stream, producer, 120.625, released);
            producer.SynchroniseTiming(150.875, timing);
            using GameplaySkinEventSubscription reattached = stream.Subscribe();
            GameplaySkinEventStateSnapshot snapshot = ((GameplaySkinStateEventPayload)dequeue(reattached).Payload).State;
            assertInput(snapshot.Inputs.Single(value => value.LaneId == first_lane), released);
            assertInput(snapshot.Inputs.Single(value => value.LaneId == second_lane), input(second_lane, false));
        }

        [Test]
        public void TestBackpressureIsAtomicAndConsumerRebuildRetainsSuppressedRealRelease()
        {
            using var stream = new GameplaySkinEventStream(revision, 0, state(input(first_lane, false)), pendingEventBudget: 1);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription observer = stream.Subscribe();
            dequeue(observer);
            GameplaySkinInputStateSnapshot held = GameplaySkinEventRuntimeHost.SnapshotInputAt(input(first_lane, false), true, 1, 100);
            publish(stream, producer, 100, held);
            GameplaySkinInputStateSnapshot released = GameplaySkinEventRuntimeHost.SnapshotInputAt(held, false, 0, 150);
            Assert.Throws<GameplaySkinEventBackpressureException>(() => publish(stream, producer, 150, released));
            using (GameplaySkinEventSubscription late = stream.Subscribe())
            {
                GameplaySkinEventEnvelope unchanged = dequeue(late);
                Assert.That(unchanged.Sequence, Is.EqualTo(1));
                Assert.That(unchanged.GameplayTime, Is.EqualTo(100));
                assertInput(((GameplaySkinStateEventPayload)unchanged.Payload).State.Inputs.Single(), held);
            }

            producer = producer.Reset(175, state(released), GameplaySkinEventResetReason.ConsumerRebuilt);
            GameplaySkinEventEnvelope reset = dequeue(observer);
            Assert.That(reset.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
            Assert.That(reset.Epoch, Is.EqualTo(1));
            assertInput(((GameplaySkinStateEventPayload)reset.Payload).State.Inputs.Single(), released);
            using (GameplaySkinEventSubscription late = stream.Subscribe())
                assertInput(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Inputs.Single(), released);
            producer.Dispose();
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Rewind)]
        [TestCase(GameplaySkinEventResetReason.Retry)]
        public void TestReplacementEpochAcceptsOnlyTheRebuiltInputOrigin(GameplaySkinEventResetReason reason)
        {
            var original = new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, 100, 200);
            using var stream = new GameplaySkinEventStream(revision, 300, state(original));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription observer = stream.Subscribe();
            dequeue(observer);
            producer = producer.Reset(-50, state(input(first_lane, true)), reason);
            GameplaySkinInputStateSnapshot replacement = ((GameplaySkinStateEventPayload)dequeue(observer).Payload).State.Inputs.Single();
            Assert.That(replacement.IsPressed, Is.True);
            Assert.That(replacement.PressedAt, Is.Null);
            Assert.That(replacement.ReleasedAt, Is.Null);
            GameplaySkinInputStateSnapshot releaseOnly = GameplaySkinEventRuntimeHost.SnapshotInputAt(replacement, false, 0, -20);
            publish(stream, producer, -20, releaseOnly);
            assertInput(((GameplaySkinInputEventPayload)dequeue(observer).Payload).State, releaseOnly);
            GameplaySkinInputStateSnapshot nextPress = GameplaySkinEventRuntimeHost.SnapshotInputAt(releaseOnly, true, 1, 0);
            publish(stream, producer, 0, nextPress);
            assertInput(((GameplaySkinInputEventPayload)dequeue(observer).Payload).State, nextPress);
            using (GameplaySkinEventSubscription late = stream.Subscribe())
                assertInput(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Inputs.Single(), nextPress);
            producer.Dispose();
        }

        [Test]
        public void TestFutureInputOriginsAreRejectedBeforeStreamOrEpochMutation()
        {
            var futurePress = new GameplaySkinInputStateSnapshot(group, first_lane, true, 1, 101);
            Assert.Throws<ArgumentException>(() => new GameplaySkinEventStream(revision, 100, state(futurePress)));
            using var stream = new GameplaySkinEventStream(revision, 0, state(input(first_lane, false)));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            Assert.Throws<ArgumentException>(() => publish(stream, producer, 100, futurePress));
            var futureRelease = new GameplaySkinInputStateSnapshot(group, first_lane, false, 0, releasedAt: 101);
            Assert.Throws<ArgumentException>(() => publish(stream, producer, 100, futureRelease));
            Assert.Throws<ArgumentException>(() => producer.Reset(100, state(futureRelease), GameplaySkinEventResetReason.Seek));
            using GameplaySkinEventSubscription late = stream.Subscribe();
            GameplaySkinEventEnvelope snapshot = dequeue(late);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Epoch, Is.Zero);
            assertInput(((GameplaySkinStateEventPayload)snapshot.Payload).State.Inputs.Single(), input(first_lane, false));
        }

        private static void assertInput(GameplaySkinInputStateSnapshot actual, GameplaySkinInputStateSnapshot expected)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.GroupId, Is.EqualTo(expected.GroupId));
                Assert.That(actual.LaneId, Is.EqualTo(expected.LaneId));
                Assert.That(actual.IsPressed, Is.EqualTo(expected.IsPressed));
                Assert.That(actual.Strength, Is.EqualTo(expected.Strength));
                Assert.That(actual.PressedAt.HasValue, Is.EqualTo(expected.PressedAt.HasValue));
                Assert.That(actual.ReleasedAt.HasValue, Is.EqualTo(expected.ReleasedAt.HasValue));
                Assert.That(BitConverter.DoubleToInt64Bits(actual.PressedAt ?? 0), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.PressedAt ?? 0)));
                Assert.That(BitConverter.DoubleToInt64Bits(actual.ReleasedAt ?? 0), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.ReleasedAt ?? 0)));
            });
        }

        private static readonly GameplaySkinEventRevision revision = GameplaySkinEventRevision.Create(1, 1, 1, 1);
        private static readonly GameplaySkinTimingStateSnapshot timing = new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1);

        private static GameplaySkinInputStateSnapshot input(GameplaySkinLaneId lane, bool pressed)
            => new GameplaySkinInputStateSnapshot(group, lane, pressed, pressed ? 1 : 0);

        private static GameplaySkinEventStateSnapshot state(params GameplaySkinInputStateSnapshot[] inputs)
            => new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Running, inputs,
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0), timing, Array.Empty<GameplaySkinBgaStateSnapshot>());

        private static void publish(GameplaySkinEventStream stream, GameplaySkinEventProducer producer, double time, GameplaySkinInputStateSnapshot input)
            => stream.Publish(producer, time,
                GameplaySkinEventValue.Input(input.IsPressed ? GameplaySkinEventKind.InputPressed : GameplaySkinEventKind.InputReleased, input),
                input.GroupId, input.LaneId);

        private static GameplaySkinEventEnvelope dequeue(GameplaySkinEventSubscription subscription)
        {
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return envelope!;
        }
    }
}
