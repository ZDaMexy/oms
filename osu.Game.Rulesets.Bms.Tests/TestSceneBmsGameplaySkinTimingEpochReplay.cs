// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGameplaySkinTimingEpochReplay : OsuGameTestScene
    {
        // The source keeps progressing while frame stability is catching up. Holding a manual clock at
        // each seek target would hide a stale discontinuity override after a long gameplay update.
        private readonly FramedClock referenceClock = new FramedClock(new StopwatchClock(true));

        protected override double TimePerAction => 25;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, Audio);

        protected override void Update()
        {
            referenceClock.ProcessFrame();
            base.Update();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAdvancingAutoplaySupportsLongCatchUpAndRepeatedForwardAndBackwardSeeks(bool mania)
        {
            Ruleset ruleset = mania ? new ManiaRuleset() : new BmsRuleset();
            ReplayPlayer player = null!;
            DrawableRuleset renderer = null!;
            GameplayClockContainer clock = null!;
            GameplaySkinEventRuntimeHost events = null!;
            CatchUpObserver observer = null!;
            double stalledSeekTarget = 0;
            double resumedAt = 0;

            AddStep("select a dense synthetic chart with notes and holds", () =>
            {
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createChart(ruleset, mania));
                Game.SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddStep("enter the actual autoplay ReplayPlayer", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = new AdvancingReplayPlayer(ruleset))));
            AddUntilStep("the actual player and event publication are ready", () =>
            {
                DismissAnyNotifications();
                if (player == null || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player) || !player.LoadedBeatmapSuccessfully)
                    return false;

                renderer = player.ChildrenOfType<DrawableRuleset>().Single();
                events = renderer.ChildrenOfType<GameplaySkinEventRuntimeHost>().Single();
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                return events.IsLoaded && clock.IsRunning && clock.CurrentTime > 250;
            });
            AddStep("observe the real stream and a long catch-up update", () =>
                renderer.FrameStableComponents.Add(observer = new CatchUpObserver(renderer.FrameStableClock, events.EventStream)));
            AddUntilStep("actual autoplay naturally judges several rows before any seek", () =>
                player.GameplayState.ScoreProcessor.HitEvents.Count(hit => hit.Result == HitResult.Perfect
                    && hit.HitObject.Judgement.MaxResult == HitResult.Perfect) >= 32);
            AddStep("autoplay timing counters are neutral despite real update-sized offsets", () =>
            {
                if (!mania)
                {
                    double[] offsets = player.GameplayState.ScoreProcessor.HitEvents
                        .Where(hit => hit.Result == HitResult.Perfect && hit.HitObject.Judgement.MaxResult == HitResult.Perfect)
                        .Select(hit => hit.TimeOffset).ToArray();
                    TestContext.Progress.WriteLine($"Natural BMS autoplay offsets before any seek: count={offsets.Length}, min={offsets.Min():R}, max={offsets.Max():R} ms.");
                    Assert.That(offsets.Any(offset => offset >= 0.000001), Is.True,
                        "These offsets come from naturally applied autoplay results, not synthetic HitEvents or a seek.");
                }
                using GameplaySkinEventSubscription late = events.EventStream.Subscribe();
                GameplaySkinEventStateSnapshot state = null!;
                late.DrainFrame(envelope => state = ((GameplaySkinStateEventPayload)envelope.Payload).State);
                Assert.That(state.Score.Statistics.Fast, Is.Zero);
                Assert.That(state.Score.Statistics.Slow, Is.Zero);
            });
            AddStep("seek with one representative 350 ms gameplay stall", () =>
            {
                observer.DelayNextCatchUp = true;
                player.SeekInDirection(5);
                stalledSeekTarget = clock.LastSeekTarget!.Value;
            });
            AddUntilStep("the real frame-stable catch-up encountered the stall", () => observer.Delayed);
            AddUntilStep("the advancing clock has passed the old proximity window and caught up", () =>
                clock.CurrentTime > stalledSeekTarget + 400
                && Math.Abs(renderer.FrameStableClock.CurrentTime - clock.CurrentTime) < 0.1);
            AddStep("the event authority has handed back to the moving source", () =>
            {
                Assert.That(events.AuthoritativeGameplayTime, Is.EqualTo(clock.CurrentTime).Within(0.1));
                Assert.That(observer.Resets, Is.GreaterThan(0));
            });

            AddRepeatStep("rapidly seek five seconds forward through actual replay input", () => player.SeekInDirection(5), 5);
            AddRepeatStep("rapidly seek five seconds backward through actual replay input", () => player.SeekInDirection(-5), 3);
            AddUntilStep("the final backwards seek restores one complete baseline", () =>
                observer.SawRewind && Math.Abs(events.AuthoritativeGameplayTime - clock.CurrentTime) < 0.1
                && Math.Abs(renderer.FrameStableClock.CurrentTime - clock.CurrentTime) < 0.1);
            AddStep("retain the running destination", () => resumedAt = clock.CurrentTime);
            AddUntilStep("normal autoplay progresses after the final seek", () => clock.CurrentTime > resumedAt + 500);
            AddStep("verify retained state comes from the real score and health owners", () =>
            {
                using GameplaySkinEventSubscription late = events.EventStream.Subscribe();
                GameplaySkinEventStateSnapshot state = null!;
                late.DrainFrame(envelope => state = ((GameplaySkinStateEventPayload)envelope.Payload).State);
                Assert.Multiple(() =>
                {
                    Assert.That(state.Score.Score, Is.EqualTo(player.GameplayState.ScoreProcessor.TotalScore.Value));
                    Assert.That(state.Score.Gauge, Is.EqualTo(player.GameplayState.HealthProcessor.Health.Value));
                    Assert.That(state.Score.Statistics.Perfect, Is.EqualTo(player.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Perfect)));
                    Assert.That(state.Score.Statistics.Miss, Is.EqualTo(player.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss)));
                    Assert.That(state.Score.Statistics.Fast, Is.Zero);
                    Assert.That(state.Score.Statistics.Slow, Is.Zero);
                    Assert.That(observer.TimesAreMonotonic, Is.True, "Every edge, timing sample and reset must retain its epoch order.");
                });
            });
            AddStep("seek near the end through the same player", () => player.Seek(player.GameplayState.Beatmap.GetLastObjectTime() - 1000));
            AddUntilStep("autoplay completes the remaining real judgements", () => player.GameplayState.ScoreProcessor.HasCompleted.Value);
            AddStep("completion retains actual successful results and a live event stream", () =>
            {
                Assert.That(player.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss), Is.Zero);
                Assert.That(player.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Perfect), Is.Positive);
                Assert.That(observer.TimesAreMonotonic, Is.True);
                Assert.That(events.AuthoritativeGameplayTime, Is.EqualTo(clock.CurrentTime).Within(0.1));
            });
            AddUntilStep("the completed replay reaches the normal results screen", () => Game.ScreenStack.CurrentScreen is ResultsScreen);
        }

        private static IBeatmap createChart(Ruleset ruleset, bool mania)
        {
            if (mania)
            {
                var result = new ManiaBeatmap(new StageDefinition(7))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    Difficulty = { CircleSize = 7 },
                };
                for (int i = 0; i < 1280; i++)
                {
                    double time = 2000 + i * 50;
                    result.HitObjects.Add(i % 32 == 0
                        ? new HoldNote { Column = i % 7, StartTime = time, Duration = 300 }
                        : new Note { Column = i % 7, StartTime = time });
                }
                return result;
            }

            // Non-integral note intervals prevent the chart from coinciding with the fixture's update grid.
            var text = new StringBuilder("#TITLE Moving autoplay seek epochs\n#BPM 137.123\n#LNTYPE 1\n#RANK 2\n#WAV01 note.wav\n");
            for (int measure = 1; measure <= 32; measure++)
            {
                foreach (int channel in new[] { 11, 12, 13, 14, 15, 19 })
                    text.AppendLine($"#{measure:000}{channel}:01010101010101010101010101010101");
                text.AppendLine($"#{measure:000}58:01000100");
            }
            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text.ToString(), "moving-autoplay-seek.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
        }

        private sealed partial class AdvancingReplayPlayer : ReplayPlayer
        {
            protected override bool PauseOnFocusLost => false;

            public AdvancingReplayPlayer(Ruleset ruleset)
                : base(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)
            {
            }
        }

        private sealed partial class CatchUpObserver : Component
        {
            private readonly IFrameStableClock clock;
            private readonly GameplaySkinEventStream stream;
            private readonly GameplaySkinEventSubscription subscription;
            private long epoch = -1;
            private double lastTime = double.NegativeInfinity;
            private GameplaySkinTimingStateSnapshot timing;

            public bool DelayNextCatchUp;
            public bool Delayed { get; private set; }
            public bool SawRewind { get; private set; }
            public int Resets { get; private set; }
            public bool TimesAreMonotonic { get; private set; } = true;

            public CatchUpObserver(IFrameStableClock clock, GameplaySkinEventStream stream)
            {
                this.clock = clock;
                this.stream = stream;
                subscription = stream.Subscribe();
            }

            protected override void Update()
            {
                base.Update();
                subscription.DrainFrame(envelope =>
                {
                    if (envelope.Epoch == epoch)
                        TimesAreMonotonic &= envelope.GameplayTime >= lastTime;
                    else
                    {
                        TimesAreMonotonic &= epoch < 0 || envelope.Epoch == epoch + 1
                            && envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset && envelope.Sequence == 0;
                        epoch = envelope.Epoch;
                    }
                    lastTime = envelope.GameplayTime;
                    timing = envelope.Payload switch
                    {
                        GameplaySkinStateEventPayload state => state.State.Timing,
                        GameplaySkinTimingEventPayload time => time.State,
                        _ => timing,
                    };
                    if (envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset)
                    {
                        Resets++;
                        SawRewind |= ((GameplaySkinStateEventPayload)envelope.Payload).ResetReason == GameplaySkinEventResetReason.Rewind;
                    }
                });
                stream.ReadConsumerTimingHighWater(subscription, lastTime, timing, out double highWater, out GameplaySkinTimingStateSnapshot currentTiming);
                TimesAreMonotonic &= highWater >= lastTime;
                lastTime = highWater;
                timing = currentTiming;
                if (DelayNextCatchUp && clock.IsCatchingUp.Value)
                {
                    DelayNextCatchUp = false;
                    Thread.Sleep(350);
                    Delayed = true;
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                subscription.Dispose();
                base.Dispose(isDisposing);
            }
        }
    }
}
