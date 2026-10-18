// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [HeadlessTest]
    public partial class GameplaySkinScoreMaximumTest : OsuTestScene
    {
        [Test]
        public void TestMaximumBoundsKeepUnavailableAndExactLongValues()
        {
            AddStep("validate the immutable score denominator", () =>
            {
                Assert.That(new GameplaySkinScoreStateSnapshot(11, 2, 3, 1, 0.5).MaximumScore, Is.Zero);
                Assert.That(new GameplaySkinScoreStateSnapshot(11, 2, 3, 1, 0.5, maximumScore: 1).Score, Is.EqualTo(11),
                    "Projection preserves the real score even if it exceeds the supplied maximum.");
                Assert.That(new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0, maximumScore: long.MaxValue).MaximumScore, Is.EqualTo(long.MaxValue));
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0, maximumScore: -1));
                Assert.That(typeof(GameplaySkinScoreStateSnapshot).GetProperty(nameof(GameplaySkinScoreStateSnapshot.MaximumScore))!.CanWrite, Is.False);
            });
        }

        [TestCase(GameplaySkinEventKind.ScoreChanged)]
        [TestCase(GameplaySkinEventKind.ComboChanged)]
        [TestCase(GameplaySkinEventKind.GaugeChanged)]
        public void TestCompactScorePreservesMaximumAndGaugeClearIndependently(GameplaySkinEventKind kind)
        {
            AddStep("round trip every score family without reducing a long to double", () =>
            {
                foreach (long maximum in new[] { 0L, 1L, 9_007_199_254_740_993L, long.MaxValue })
                {
                    foreach (bool clear in new[] { false, true })
                    {
                        var gauge = new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.Hard, 1, 0, clear);
                        var statistics = new GameplaySkinJudgementStatistics(10, 9, 8, 7, 6, 5, 4);
                        var score = new GameplaySkinScoreStateSnapshot(29, 2, 19, 0.75, 0.4, statistics, gauge, maximum);
                        GameplaySkinEventValue compact = GameplaySkinEventValue.Score(kind, score);
                        GameplaySkinScoreStateSnapshot decoded = compact.GetScore();
                        GameplaySkinScoreStateSnapshot materialized = ((GameplaySkinScoreEventPayload)compact.Materialize(null, null)).State;
                        Assert.Multiple(() =>
                        {
                            Assert.That(decoded.MaximumScore, Is.EqualTo(maximum));
                            Assert.That(materialized.MaximumScore, Is.EqualTo(maximum));
                            Assert.That(decoded.GaugeState, Is.EqualTo(gauge));
                            Assert.That(materialized.GaugeState, Is.EqualTo(gauge));
                            Assert.That(decoded.Statistics, Is.EqualTo(statistics));
                            Assert.That(decoded.Score, Is.EqualTo(29));
                            Assert.That(decoded.MaxCombo, Is.EqualTo(19));
                        });
                    }
                }
            });
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Rewind)]
        [TestCase(GameplaySkinEventResetReason.Retry)]
        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt)]
        public void TestMaximumSurvivesLiveLateAndResetSnapshots(GameplaySkinEventResetReason reason)
        {
            AddStep("deliver the complete score through an epoch replacement", () =>
            {
                using var stream = new GameplaySkinEventStream(GameplaySkinEventRevision.Create(1, 1, 1, 1), 0, state(0));
                GameplaySkinEventProducer producer = stream.CreateProducer();
                using GameplaySkinEventSubscription attached = stream.Subscribe();
                Assert.That(((GameplaySkinStateEventPayload)dequeue(attached).Payload).State.Score.MaximumScore, Is.Zero);
                const long maximum = 9_007_199_254_740_993;
                stream.Publish(producer, 10, GameplaySkinEventValue.Score(GameplaySkinEventKind.ScoreChanged,
                    new GameplaySkinScoreStateSnapshot(13, 1, 1, 1, 0.5, maximumScore: maximum)), null, null);
                Assert.That(((GameplaySkinScoreEventPayload)dequeue(attached).Payload).State.MaximumScore, Is.EqualTo(maximum));
                using (GameplaySkinEventSubscription late = stream.Subscribe())
                    Assert.That(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Score.MaximumScore, Is.EqualTo(maximum));

                producer = producer.Reset(0, state(17), reason);
                GameplaySkinEventEnvelope reset = dequeue(attached);
                Assert.That(reset.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
                Assert.That(((GameplaySkinStateEventPayload)reset.Payload).State.Score.MaximumScore, Is.EqualTo(17));
                using (GameplaySkinEventSubscription rebuilt = stream.Subscribe())
                    Assert.That(((GameplaySkinStateEventPayload)dequeue(rebuilt).Payload).State.Score.MaximumScore, Is.EqualTo(17));
                producer.Dispose();
            });
        }

        [Test]
        public void TestProductionScoreMaximumIsRetainedThroughJudgementSeekRetryAndRebuild()
        {
            NativeScoreHost host = null!;
            GameplaySkinEventSubscription observer = null!;
            long maximum = 0;
            long epoch = 0;
            var resets = new List<GameplaySkinEventEnvelope>();
            AddStep("mount production score processor and authoritative clock", () => Child = host = new NativeScoreHost());
            AddUntilStep("production event host is loaded", () => host.Events?.IsLoaded == true);
            AddStep("read native maximum before the first result", () =>
            {
                maximum = host.Score.MaximumTotalScore;
                Assert.That(maximum, Is.GreaterThan(1));
                Assert.That(host.Score.HighestCombo.Value, Is.Zero);
                observer = host.Events.EventStream.Subscribe();
                GameplaySkinEventEnvelope initial = dequeue(observer);
                epoch = initial.Epoch;
                Assert.That(((GameplaySkinStateEventPayload)initial.Payload).State.Score.MaximumScore, Is.EqualTo(maximum));
                Assert.That(((GameplaySkinStateEventPayload)initial.Payload).State.Timing.LiftState, Is.EqualTo(default(GameplaySkinLiftState)),
                    "A production host without a native lift source retains an unavailable adjustment.");
            });
            AddStep("apply an actual mania result", () =>
            {
                host.ClockHost.Sample(1500);
                host.Score.ApplyResult(new JudgementResult(host.Beatmap.HitObjects[0], host.Beatmap.HitObjects[0].Judgement) { Type = HitResult.Great });
            });
            AddStep("score edges and late attach share the processor denominator", () =>
            {
                bool scoreSeen = false;
                observer.DrainFrame(envelope =>
                {
                    if (envelope.Payload is GameplaySkinScoreEventPayload payload)
                    {
                        scoreSeen = true;
                        Assert.That(payload.State.MaximumScore, Is.EqualTo(maximum));
                    }
                });
                Assert.That(scoreSeen, Is.True);
                Assert.That(host.Score.TotalScore.Value, Is.GreaterThan(0));
                Assert.That(host.Score.HighestCombo.Value, Is.EqualTo(1));
                using GameplaySkinEventSubscription late = host.Events.EventStream.Subscribe();
                Assert.That(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Score.MaximumScore, Is.EqualTo(maximum));
            });
            AddStep("seek the real clock", () => host.ClockHost.Seek(2500));
            AddUntilStep("seek publishes the complete denominator", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("inspect the seek snapshot", () =>
            {
                assertReset(observer, GameplaySkinEventResetReason.Seek, maximum, resets);
                epoch = host.Events.EventStream.CurrentEpoch;
            });
            AddStep("retry the real beatmap and clock", () =>
            {
                host.Score.ApplyBeatmap(host.Beatmap);
                host.ClockHost.Reset(500);
            });
            AddUntilStep("retry publishes the replacement score", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("inspect retry without deriving maximum from highest combo", () =>
            {
                assertReset(observer, GameplaySkinEventResetReason.Retry, maximum, resets);
                Assert.That(host.Score.TotalScore.Value, Is.Zero);
                Assert.That(host.Score.HighestCombo.Value, Is.Zero);
                epoch = host.Events.EventStream.CurrentEpoch;
            });
            AddStep("rebuild the read-only consumer epoch", () => host.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt));
            AddUntilStep("rebuild commits its complete state", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("late rebuild still receives the native maximum", () =>
            {
                assertReset(observer, GameplaySkinEventResetReason.ConsumerRebuilt, maximum, resets);
                using GameplaySkinEventSubscription late = host.Events.EventStream.Subscribe();
                Assert.That(((GameplaySkinStateEventPayload)dequeue(late).Payload).State.Score.MaximumScore, Is.EqualTo(maximum));
                observer.Dispose();
            });
        }

        private static void assertReset(GameplaySkinEventSubscription subscription, GameplaySkinEventResetReason reason, long maximum,
                                        List<GameplaySkinEventEnvelope> resets)
        {
            resets.Clear();
            subscription.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload { ResetReason: var actual } && actual == reason)
                    resets.Add(envelope);
            });
            Assert.That(resets, Has.Count.EqualTo(1));
            Assert.That(((GameplaySkinStateEventPayload)resets[0].Payload).State.Score.MaximumScore, Is.EqualTo(maximum));
        }

        private static GameplaySkinEventEnvelope dequeue(GameplaySkinEventSubscription subscription)
        {
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return envelope!;
        }

        private static GameplaySkinEventStateSnapshot state(long maximum)
            => new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Loaded, Array.Empty<GameplaySkinInputStateSnapshot>(),
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0, maximumScore: maximum),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1), Array.Empty<GameplaySkinBgaStateSnapshot>());

        private partial class NativeScoreHost : Container
        {
            [Cached(typeof(ScoreProcessor))]
            public readonly ManiaScoreProcessor Score = new ManiaScoreProcessor();

            public readonly Beatmap<Note> Beatmap = new Beatmap<Note>
            {
                HitObjects = { new Note { StartTime = 1000 }, new Note { StartTime = 3000 } },
            };

            public readonly NativeClock ClockHost = new NativeClock();
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;

            public NativeScoreHost()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                foreach (Note note in Beatmap.HitObjects)
                    note.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
                Score.ApplyBeatmap(Beatmap);
                GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, false);
                Events = new GameplaySkinEventRuntimeHost(publication, Beatmap);
                ClockHost.Child = Events;
                Child = ClockHost;
            }
        }

        private partial class NativeClock : GameplayClockContainer
        {
            private readonly AdjustableClock source;

            public NativeClock()
                : this(new AdjustableClock { CurrentTime = 500 })
            {
            }

            private NativeClock(AdjustableClock source)
                : base(source, false, false)
            {
                this.source = source;
                SoftUnpause();
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                UpdateSubTree();
            }
        }

        private class AdjustableClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }
            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }
            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
