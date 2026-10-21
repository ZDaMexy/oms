// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinLivePresentationTest
    {
        [TestCase(HitResult.Perfect, -10, -1)]
        [TestCase(HitResult.Great, 10, 1)]
        [TestCase(HitResult.Good, -10, -1)]
        [TestCase(HitResult.Meh, 0, 0)]
        [TestCase(HitResult.Perfect, 9.094947017729282E-13, 0)]
        [TestCase(HitResult.Perfect, -1.3642420526593924E-12, 0)]
        [TestCase(HitResult.Perfect, 0.000002, 1)]
        [TestCase(HitResult.Perfect, -0.000002, -1)]
        [TestCase(HitResult.Miss, 100, 0)]
        [TestCase(HitResult.LargeTickHit, -10, 0)]
        [TestCase(HitResult.IgnoreHit, -10, 0)]
        public void TestTimingCountsOnlyRealSuccessfulBasicTiming(HitResult result, double offset, int expected)
            => Assert.That(GameplaySkinEventRuntimeHost.TimingDirection(new HitEvent(offset, 1, result, new HitObject(), null, null)), Is.EqualTo(expected));

        [Test]
        public void TestEmptyPoorAndHoldBookkeepingDoNotInventFastOrSlow()
        {
            Assert.That(GameplaySkinEventRuntimeHost.TimingDirection(new HitEvent(-10, 1, HitResult.Ok, new NonTimingObject(HitResult.Ok), null, null)), Is.Zero);
            Assert.That(GameplaySkinEventRuntimeHost.TimingDirection(new HitEvent(10, 1, HitResult.Perfect, new NonTimingObject(HitResult.IgnoreHit), null, null)), Is.Zero);
        }

        [Test]
        public void TestCompactTransportRetainsOffsetTimingOriginAndIndependentMotionWithoutAnInputAttack()
        {
            var offset = new GameplaySkinOffsetStateSnapshot(true, -12.75, true);
            var statistics = new GameplaySkinJudgementStatistics(1, 2, 3, 4, 5, 6, 7, 8, 9);
            var score = new GameplaySkinScoreStateSnapshot(4, 0, 5, 0.5, 0.6, statistics, maximumScore: 100, offsetState: offset);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Score(GameplaySkinEventKind.ScoreChanged, score);
            Assert.That(compact.GetScore().OffsetState, Is.EqualTo(offset));
            Assert.That(((GameplaySkinScoreEventPayload)compact.Materialize(null, null)).State.Statistics, Is.EqualTo(statistics));
            var timing = new GameplaySkinTimingStateSnapshot(-1.5, -1, 150, false, 1, startTime: -2345.5);
            Assert.That(GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBeat, timing).GetTiming().StartTime, Is.EqualTo(-2345.5));
            GameplaySkinLaneGroupId group = GameplaySkinLaneGroupId.Create("test.deck");
            GameplaySkinLaneId lane = GameplaySkinLaneId.Create("test.scratch");
            var motion = new GameplaySkinInputStateSnapshot(group, lane, false, 0, motionCounts: -100, motionRevolutions: 0.5, motionAvailable: true);
            GameplaySkinEventValue input = GameplaySkinEventValue.Input(GameplaySkinEventKind.InputMotionChanged, motion);
            GameplaySkinInputStateSnapshot restored = ((GameplaySkinInputEventPayload)input.Materialize(group, lane)).State;
            Assert.That(restored.IsPressed, Is.False);
            Assert.That(restored.PressedAt, Is.Null);
            Assert.That(restored.MotionCounts, Is.EqualTo(-100));
            Assert.That(restored.MotionRevolutions, Is.EqualTo(0.5));
        }

        [Test]
        public void TestArrivalMetadataKeepsDistinctLaneAndStageOriginsAndNoEntryForEmptySlots()
        {
            var group = GameplaySkinLaneGroupId.Create("test.group");
            var first = GameplaySkinLaneId.Create("test.first");
            var second = GameplaySkinLaneId.Create("test.second");
            var empty = GameplaySkinLaneId.Create("test.empty");
            var values = new[] { new GameplaySkinLaneArrival(group, first, 3000), new GameplaySkinLaneArrival(group, second, 1000) };
            var chart = new GameplaySkinChartPresentation(values);
            values[0] = new GameplaySkinLaneArrival(group, first, 10);
            Assert.That(chart.ArrivalFor(first, group), Is.EqualTo(3000));
            Assert.That(chart.ArrivalFor(null, group), Is.EqualTo(1000));
            Assert.That(chart.ArrivalFor(null, null), Is.EqualTo(1000));
            Assert.That(chart.ArrivalFor(empty, group), Is.Null);
            Assert.That(chart.PersonalBestScore, Is.Null);
            Assert.That(chart.UsesExScore, Is.False);
        }

        [TestCase(100, 8, 89)]
        [TestCase(100, 7, 78)]
        [TestCase(100, 6, 67)]
        [TestCase(2, 8, 2)]
        [TestCase(18, 8, 16)]
        public void TestRankTargetsUseIntegerExScoreThresholds(long maximum, int ninths, long expected)
            => Assert.That(GameplaySkinChartPresentation.RankThreshold(maximum, ninths), Is.EqualTo(expected));

        [Test]
        public void TestUnknownClockOriginsAndInventedUnavailableDisplacementAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: double.MinValue));
            Assert.Throws<ArgumentException>(() => new GameplaySkinOffsetStateSnapshot(false, 0, true));
            Assert.Throws<ArgumentException>(() => new GameplaySkinInputStateSnapshot(GameplaySkinLaneGroupId.Create("test.group"), GameplaySkinLaneId.Create("test.lane"),
                false, 0, motionCounts: 1));
        }

        private sealed class NonTimingObject : HitObject
        {
            private readonly HitResult maximum;

            public NonTimingObject(HitResult maximum) => this.maximum = maximum;

            public override Judgement CreateJudgement() => new NonTimingJudgement(maximum);
        }

        private sealed class NonTimingJudgement : Judgement
        {
            private readonly HitResult maximum;

            public NonTimingJudgement(HitResult maximum) => this.maximum = maximum;

            public override HitResult MaxResult => maximum;
        }
    }
}
