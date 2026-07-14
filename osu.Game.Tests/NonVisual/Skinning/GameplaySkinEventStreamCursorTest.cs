// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;
using static osu.Game.Tests.NonVisual.Skinning.GameplaySkinEventEnvelopeTest;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinEventStreamCursorTest
    {
        [Test]
        public void TestAttachAcceptsSnapshotHeaderAtMidSessionHighWater()
        {
            var cursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);
            GameplaySkinEventEnvelope snapshot = envelope(GameplaySkinEventDeliveryKind.Snapshot, 5, 42, -100, 7);

            cursor.ValidateAndAdvance(snapshot);

            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(snapshot));
        }

        [TestCase(GameplaySkinEventDeliveryKind.Edge)]
        [TestCase(GameplaySkinEventDeliveryKind.Reset)]
        public void TestAttachRejectsNonSnapshotWithoutAdvancing(GameplaySkinEventDeliveryKind deliveryKind)
        {
            var cursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);

            Assert.That(() => cursor.ValidateAndAdvance(envelope(deliveryKind, 3, 0, 10, 2)), Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.Null);

            GameplaySkinEventEnvelope snapshot = envelope(GameplaySkinEventDeliveryKind.Snapshot, 3, 9, 10, 2);
            cursor.ValidateAndAdvance(snapshot);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(snapshot));
        }

        [Test]
        public void TestSameTimeEdgesUseContiguousSequenceOrder()
        {
            var cursor = attach(epoch: 2, sequence: 8, gameplayTime: 100, layoutRevision: 4);
            GameplaySkinEventEnvelope first = envelope(GameplaySkinEventDeliveryKind.Edge, 2, 9, 100, 4);
            GameplaySkinEventEnvelope second = envelope(GameplaySkinEventDeliveryKind.Edge, 2, 10, 100, 4);

            cursor.ValidateAndAdvance(first);
            cursor.ValidateAndAdvance(second);

            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(second));
        }

        [TestCase(8)]
        [TestCase(10)]
        [TestCase(2)]
        public void TestSameEpochSequenceMustBeContiguousAndRejectionIsAtomic(long invalidSequence)
        {
            var cursor = attach(epoch: 1, sequence: 8, gameplayTime: 50, layoutRevision: 3);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 1, invalidSequence, 50, 3)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, 1, 9, 50, 3);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestSameEpochGameplayTimeCannotMoveBackwardsAndRejectionIsAtomic()
        {
            var cursor = attach(epoch: 1, sequence: 0, gameplayTime: 25, layoutRevision: 3);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 1, 1, 24.999, 3)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, 1, 1, 25, 3);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestSameEpochSnapshotCanKeepOrAdvanceLayoutRevision()
        {
            var cursor = attach(epoch: 4, sequence: 20, gameplayTime: 75, layoutRevision: 3);
            GameplaySkinEventEnvelope sameLayoutReload = envelope(GameplaySkinEventDeliveryKind.Snapshot, 4, 21, 75, 3);
            GameplaySkinEventEnvelope changedLayoutReload = envelope(GameplaySkinEventDeliveryKind.Snapshot, 4, 22, 75, 8);
            GameplaySkinEventEnvelope nextEdge = envelope(GameplaySkinEventDeliveryKind.Edge, 4, 23, 75, 8);

            cursor.ValidateAndAdvance(sameLayoutReload);
            cursor.ValidateAndAdvance(changedLayoutReload);
            cursor.ValidateAndAdvance(nextEdge);

            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(nextEdge));
        }

        [Test]
        public void TestEdgeCannotIntroduceOrUseStaleLayoutRevision()
        {
            var cursor = attach(epoch: 4, sequence: 0, gameplayTime: 75, layoutRevision: 3);

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 4, 1, 75, 4)),
                Throws.InvalidOperationException);

            GameplaySkinEventEnvelope changedLayout = envelope(GameplaySkinEventDeliveryKind.Snapshot, 4, 1, 75, 4);
            cursor.ValidateAndAdvance(changedLayout);

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 4, 2, 75, 3)),
                Throws.InvalidOperationException);

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, 4, 2, 75, 4);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestSnapshotCannotRegressLayoutRevisionWithinEpoch()
        {
            var cursor = attach(epoch: 4, sequence: 0, gameplayTime: 75, layoutRevision: 3);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Snapshot, 4, 1, 75, 2)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, 4, 1, 75, 3);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestNewEpochResetHeaderReanchorsSequenceAndGameplayTime()
        {
            var cursor = attach(epoch: 4, sequence: 10, gameplayTime: 100, layoutRevision: 5);
            GameplaySkinEventEnvelope backwardReset = envelope(GameplaySkinEventDeliveryKind.Reset, 5, 0, -20, 5);
            GameplaySkinEventEnvelope edgeAfterBackwardReset = envelope(GameplaySkinEventDeliveryKind.Edge, 5, 1, -20, 5);
            GameplaySkinEventEnvelope forwardReset = envelope(GameplaySkinEventDeliveryKind.Reset, 6, 0, 250, 6);
            GameplaySkinEventEnvelope edgeAfterForwardReset = envelope(GameplaySkinEventDeliveryKind.Edge, 6, 1, 250, 6);

            cursor.ValidateAndAdvance(backwardReset);
            cursor.ValidateAndAdvance(edgeAfterBackwardReset);
            cursor.ValidateAndAdvance(forwardReset);
            cursor.ValidateAndAdvance(edgeAfterForwardReset);

            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(edgeAfterForwardReset));
        }

        [Test]
        public void TestNewEpochRequiresResetAtSequenceZeroAndRejectionIsAtomic()
        {
            var cursor = attach(epoch: 3, sequence: 2, gameplayTime: 10, layoutRevision: 1);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.Multiple(() =>
            {
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 3, 3, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 4, 0, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Snapshot, 4, 0, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 4, 1, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 2, 0, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 5, 0, 10, 1)), Throws.InvalidOperationException);
                Assert.That(() => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 4, 0, 10, 0)), Throws.InvalidOperationException);
            });
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Reset, 4, 0, 10, 1);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestApiVersionCannotChangeAndRejectionIsAtomic()
        {
            var cursor = attach(epoch: 0, sequence: 0, gameplayTime: 0, layoutRevision: 0);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Edge, 0, 1, 0, 0, apiVersion: 2)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, 0, 1, 0, 0);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestUnsupportedApiVersionCannotAttachAndRejectionIsAtomic()
        {
            var cursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Snapshot, 0, 0, 0, 0, apiVersion: 2)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.Null);

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Snapshot, 0, 0, 0, 0);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestSequenceExhaustionRequiresNewEpochReset()
        {
            var cursor = attach(epoch: 5, sequence: long.MaxValue, gameplayTime: 10, layoutRevision: long.MaxValue);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Snapshot, 5, long.MaxValue, 10, long.MaxValue)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope reset = envelope(GameplaySkinEventDeliveryKind.Reset, 6, 0, -10, long.MaxValue);
            cursor.ValidateAndAdvance(reset);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(reset));
        }

        [Test]
        public void TestEpochCannotWrapFromMaximumValue()
        {
            var cursor = attach(epoch: long.MaxValue, sequence: 0, gameplayTime: 10, layoutRevision: 3);
            GameplaySkinEventEnvelope previous = cursor.LastAcceptedEnvelope!;

            Assert.That(
                () => cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Reset, 0, 0, -10, 3)),
                Throws.InvalidOperationException);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(previous));

            GameplaySkinEventEnvelope valid = envelope(GameplaySkinEventDeliveryKind.Edge, long.MaxValue, 1, 10, 3);
            cursor.ValidateAndAdvance(valid);
            Assert.That(cursor.LastAcceptedEnvelope, Is.SameAs(valid));
        }

        [Test]
        public void TestRejectsInvalidCursorInput()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => new GameplaySkinEventStreamCursor(0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new GameplaySkinEventStreamCursor(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new GameplaySkinEventStreamCursor(2), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new GameplaySkinEventStreamCursor(1).ValidateAndAdvance(null!), Throws.ArgumentNullException);
            });
        }

        private static GameplaySkinEventStreamCursor attach(
            long epoch,
            long sequence,
            double gameplayTime,
            long layoutRevision)
        {
            var cursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);
            cursor.ValidateAndAdvance(envelope(GameplaySkinEventDeliveryKind.Snapshot, epoch, sequence, gameplayTime, layoutRevision));
            return cursor;
        }

        private static GameplaySkinEventEnvelope envelope(
            GameplaySkinEventDeliveryKind deliveryKind,
            long epoch,
            long sequence,
            double gameplayTime,
            long layoutRevision,
            int apiVersion = GameplaySkinEventApiVersions.V1)
            => GameplaySkinEventEnvelope.Create(
                apiVersion,
                epoch,
                sequence,
                gameplayTime,
                layoutRevision,
                new TestPayload(deliveryKind));
    }
}
