// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinPreparationArrivalStateTest
    {
        private static readonly GameplaySkinLaneGroupId first_group = GameplaySkinLaneGroupId.Create("cue.first-deck");
        private static readonly GameplaySkinLaneGroupId second_group = GameplaySkinLaneGroupId.Create("cue.second-deck");
        private static readonly GameplaySkinLaneId first_lane = GameplaySkinLaneId.Create("cue.first-lane");
        private static readonly GameplaySkinLaneId chord_lane = GameplaySkinLaneId.Create("cue.chord-lane");
        private static readonly GameplaySkinLaneId later_lane = GameplaySkinLaneId.Create("cue.later-lane");

        [Test]
        public void TestFirstRowIsSharedAcrossDecksWhileOldLaneArrivalOriginsRemainIndependent()
        {
            GameplaySkinChartPresentation chart = createChart();
            Assert.Multiple(() =>
            {
                Assert.That(chart.IsFirstRow(first_lane, first_group), Is.True);
                Assert.That(chart.IsFirstRow(chord_lane, second_group), Is.True);
                Assert.That(chart.IsFirstRow(later_lane, second_group), Is.False);
                Assert.That(chart.IsFirstRow(null, first_group), Is.True);
                Assert.That(chart.IsFirstRow(null, second_group), Is.True);
                Assert.That(chart.IsFirstRow(null, null), Is.True);
                Assert.That(chart.ArrivalFor(later_lane, second_group), Is.EqualTo(2000));
                Assert.That(chart.ArrivalFor(null, second_group), Is.EqualTo(1000.0000005));
                Assert.That(chart.ArrivalCueAvailable, Is.True);
            });
        }

        [Test]
        public void TestNoSourceOrNoNotesCannotClaimACue()
        {
            var empty = new GameplaySkinChartPresentation(Array.Empty<GameplaySkinLaneArrival>(), arrivalCueAvailable: true);
            var noSource = new GameplaySkinChartPresentation(createChart().Arrivals);
            var visibility = createVisibility();
            Assert.Multiple(() =>
            {
                Assert.That(empty.ArrivalCueAvailable, Is.False);
                Assert.That(empty.IsFirstRow(null, null), Is.False);
                Assert.That(noSource.ArrivalCueAvailable, Is.False);
                Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(noSource, visibility, 1000, null), Is.Null);
                Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(createChart(), null, 1000, -100), Is.Null);
                Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(empty, visibility, 1000, null), Is.Null);
                Assert.That(visibility.Calls, Is.Zero);
            });
        }

        [Test]
        public void TestFirstRowCuePredictsOnlyItsActualLanesAndLatchesOneFade()
        {
            var chart = createChart();
            var visibility = createVisibility();
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 0, null), Is.Null);
            double? start = GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 200, null);
            Assert.That(start, Is.EqualTo(200));
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 250, start), Is.EqualTo(start));
            Assert.That(visibility.QueriedLaterLane, Is.False, "A later lane's first note never participates in the opening cue.");

            visibility.Calls = 0;
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 400, start), Is.EqualTo(start));
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 1000, start), Is.EqualTo(start));
            Assert.That(visibility.Calls, Is.Zero, "A completed cue stays retired without walking the chart every frame.");
        }

        [Test]
        public void TestAnAlreadyVisibleChordLaneImmediatelyClearsTheWholeCue()
        {
            var chart = createChart();
            var visibility = createVisibility();
            visibility.EntryTimes[chord_lane] = 100;
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 100, null), Is.EqualTo(-100),
                "The first lane's prediction must not hide a different chord lane which has already entered.");
            Assert.That(GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 100, 50), Is.EqualTo(-100),
                "A live scroll change cannot leave a partially faded cue over an actual note.");
        }

        [Test]
        public void TestCueSamplingDoesNotAllocatePerFrame()
        {
            var chart = createChart();
            var visibility = createVisibility();
            GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 0, null);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                GameplaySkinEventRuntimeHost.SampleArrivalCueFadeStart(chart, visibility, 0, null);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);
        }

        [Test]
        public void TestPreparationAndCueSurviveCompactTimingAndPublicPayloadExactly()
        {
            var preparation = new GameplaySkinPreparationStateSnapshot(true, true, 3900.25, 1099.75, true, 400.25);
            var timing = new GameplaySkinTimingStateSnapshot(-1.5, -1, 150, false, 1, startTime: -2000,
                preparation: preparation, arrivalCueFadeStartTime: -900.125);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBeat, timing);
            GameplaySkinTimingStateSnapshot restored = compact.GetTiming();
            GameplaySkinTimingStateSnapshot materialized = ((GameplaySkinTimingEventPayload)compact.Materialize(null, null)).State;
            Assert.Multiple(() =>
            {
                Assert.That(restored.Preparation, Is.EqualTo(preparation));
                Assert.That(restored.ArrivalCueFadeStartTime, Is.EqualTo(-900.125));
                Assert.That(restored.StartTime, Is.EqualTo(-2000));
                Assert.That(materialized.Preparation, Is.EqualTo(preparation));
                Assert.That(materialized.ArrivalCueFadeStartTime, Is.EqualTo(-900.125));
            });
        }

        [Test]
        public void TestFrozenGameplayTimeStillPublishesCurrentPreparationViaHighWaterWithoutAnEdge()
        {
            var first = new GameplaySkinPreparationStateSnapshot(true, true, 100, 4900, false, 0);
            var second = new GameplaySkinPreparationStateSnapshot(true, true, 200, 4800, false, 0);
            GameplaySkinChartPresentation chart = createChart();
            GameplaySkinEventStateSnapshot initial = state(first, null, chart);
            using var stream = new GameplaySkinEventStream(GameplaySkinEventRevision.Create(1, 1, 1, 1), -2000, initial);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using GameplaySkinEventSubscription consumer = stream.Subscribe();
                Assert.That(consumer.TryDequeue(out _), Is.True);
                GameplaySkinTimingStateSnapshot sampled = state(second, -2100, chart).Timing;
                producer.SynchroniseTiming(-2000, sampled);
                Assert.That(consumer.PendingCount, Is.Zero, "Preparation updates do not create per-frame C5/script edges.");
                stream.ReadConsumerTimingHighWater(consumer, -2000, initial.Timing, out double currentTime, out GameplaySkinTimingStateSnapshot highWater);
                Assert.That(currentTime, Is.EqualTo(-2000), "Music and judgement time do not advance for a preparation animation.");
                Assert.That(highWater.Preparation, Is.EqualTo(second));
                Assert.That(highWater.ArrivalCueFadeStartTime, Is.EqualTo(-2100));

                using GameplaySkinEventSubscription late = stream.Subscribe();
                Assert.That(late.TryDequeue(out GameplaySkinEventEnvelope? attached), Is.True);
                var retained = ((GameplaySkinStateEventPayload)attached!.Payload).State;
                Assert.That(retained.Timing.Preparation, Is.EqualTo(second));
                Assert.That(retained.Timing.ArrivalCueFadeStartTime, Is.EqualTo(-2100));
                Assert.That(retained.ChartPresentation, Is.SameAs(chart));
                Assert.That(retained.ChartPresentation!.ArrivalCueAvailable, Is.True);

                GameplaySkinEventStateSnapshot retry = state(first, null, chart);
                producer = producer.Reset(-2000, retry, GameplaySkinEventResetReason.Retry);
                Assert.That(consumer.TryDequeue(out GameplaySkinEventEnvelope? reset), Is.True);
                Assert.That(((GameplaySkinStateEventPayload)reset!.Payload).State.Timing.ArrivalCueFadeStartTime, Is.Null);
                Assert.That(((GameplaySkinStateEventPayload)reset.Payload).State.Timing.Preparation, Is.EqualTo(first));
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestDefaultAndCompletedPreparationHaveExplicitZeroState()
        {
            Assert.That(default(GameplaySkinPreparationStateSnapshot).Available, Is.False);
            var completed = new GameplaySkinPreparationStateSnapshot(true, false, 5000, 0, false, 0);
            Assert.That(completed.Available, Is.True);
            Assert.That(completed.Active, Is.False);
            Assert.That(completed.Elapsed, Is.EqualTo(5000));
            Assert.That(completed.Ready, Is.False);
            Assert.That(new GameplaySkinPreparationStateSnapshot(true, true, 0, 1000, true, 500).ReadyElapsed, Is.EqualTo(500),
                "A short preparation phase enters the common 1.5s GET READY clip at its corresponding later sample.");
            Assert.Throws<ArgumentException>(() => new GameplaySkinPreparationStateSnapshot(false, true, 0, 1, false, 0));
            Assert.Throws<ArgumentException>(() => new GameplaySkinPreparationStateSnapshot(false, false, 1, 0, false, 0));
            Assert.Throws<ArgumentException>(() => new GameplaySkinPreparationStateSnapshot(true, false, 5000, 1, false, 0));
            Assert.Throws<ArgumentException>(() => new GameplaySkinPreparationStateSnapshot(true, false, 5000, 0, true, 1));
            Assert.Throws<ArgumentException>(() => new GameplaySkinPreparationStateSnapshot(true, true, 100, 4900, false, 1));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1)]
        public void TestPreparationRejectsInvalidTimeComponents(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinPreparationStateSnapshot(true, true, value, 0, false, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinPreparationStateSnapshot(true, true, 100, value, false, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinPreparationStateSnapshot(true, true, 100, 100, true, value));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.MinValue)]
        public void TestCueRejectsUnknownOrNonFiniteClockOrigins(double value)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, arrivalCueFadeStartTime: value));

        private static GameplaySkinChartPresentation createChart()
            => new(new[]
            {
                new GameplaySkinLaneArrival(first_group, first_lane, 1000),
                new GameplaySkinLaneArrival(second_group, chord_lane, 1000.0000005),
                new GameplaySkinLaneArrival(second_group, later_lane, 2000),
            }, arrivalCueAvailable: true);

        private static VisibilityProjection createVisibility()
            => new(new Dictionary<GameplaySkinLaneId, double>
            {
                [first_lane] = 400,
                [chord_lane] = 500,
                [later_lane] = 0,
            });

        private static GameplaySkinEventStateSnapshot state(GameplaySkinPreparationStateSnapshot preparation, double? fade, GameplaySkinChartPresentation chart)
            => new(GameplaySkinLifecycleState.Loaded, Array.Empty<GameplaySkinInputStateSnapshot>(), Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(), new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1),
                new GameplaySkinTimingStateSnapshot(-4, -1, 120, false, 1, startTime: -2000, preparation: preparation, arrivalCueFadeStartTime: fade),
                Array.Empty<GameplaySkinBgaStateSnapshot>(), chartPresentation: chart);

        private sealed class VisibilityProjection : IGameplaySkinArrivalVisibilityProjection
        {
            internal Dictionary<GameplaySkinLaneId, double> EntryTimes { get; }
            internal int Calls { get; set; }
            internal bool QueriedLaterLane { get; private set; }

            internal VisibilityProjection(Dictionary<GameplaySkinLaneId, double> entryTimes) => EntryTimes = entryTimes;

            public bool IsArrivalVisible(GameplaySkinLaneId laneId, double hitTime, double gameplayTime)
            {
                Calls++;
                QueriedLaterLane |= laneId == later_lane;
                return gameplayTime >= EntryTimes[laneId];
            }
        }
    }
}
