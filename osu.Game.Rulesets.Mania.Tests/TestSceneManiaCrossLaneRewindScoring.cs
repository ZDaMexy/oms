// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneManiaCrossLaneRewindScoring : PlayerTestScene
    {
        private const double first_note_time = 2000;
        private const double meh_offset = 130;

        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });
        private bool simultaneous;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override bool AllowFail => true;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new Note { StartTime = first_note_time, Column = 0 },
                    new Note { StartTime = first_note_time + (simultaneous ? 0 : 1000), Column = 1 },
                    new Note { StartTime = 16000, Column = 0 },
                },
                Difficulty = { CircleSize = 4, OverallDifficulty = 5, DrainRate = 5 },
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(true)]
        [TestCase(false)]
        public void TestManualReplayRewindRestoresCrossLaneScoreAndHealth(bool sameTime)
        {
            DrawableManiaRuleset renderer = null!;
            Snapshot initial = default;
            Snapshot first = default;
            Snapshot rewound = default;
            Snapshot second = default;
            double secondNoteTime = first_note_time + (sameTime ? 0 : 1000);

            AddStep("load real native mania player", () =>
            {
                simultaneous = sameTime;
                LoadPlayer();
            });
            AddUntilStep("player loaded", () => Player?.IsLoaded == true && Player.DrawableRuleset is DrawableManiaRuleset);
            AddStep("attach actual two-column manual replay", () =>
            {
                renderer = (DrawableManiaRuleset)Player.DrawableRuleset;
                var replay = new Replay { Frames = { new ManiaReplayFrame(0) } };

                // A late but valid MEH has a fixed HP cost independent of the legacy recovery simulation,
                // and reduces health below the initial maximum rather than disappearing into its clamp.
                if (sameTime)
                {
                    replay.Frames.Add(new ManiaReplayFrame(first_note_time + meh_offset, ManiaAction.Key1, ManiaAction.Key2));
                    replay.Frames.Add(new ManiaReplayFrame(first_note_time + meh_offset + 20));
                }
                else
                {
                    replay.Frames.Add(new ManiaReplayFrame(first_note_time + meh_offset, ManiaAction.Key1));
                    replay.Frames.Add(new ManiaReplayFrame(first_note_time + meh_offset + 20));
                    replay.Frames.Add(new ManiaReplayFrame(secondNoteTime + meh_offset, ManiaAction.Key2));
                    replay.Frames.Add(new ManiaReplayFrame(secondNoteTime + meh_offset + 20));
                }

                replay.Frames.Add(new ManiaReplayFrame(16100));
                renderer.SetRecordTarget(null!);
                renderer.SetReplayScore(new Score { Replay = replay, ScoreInfo = { Mods = renderer.Mods.ToArray() } });
            });

            seekTo(first_note_time - 200, "before either note");
            AddStep("capture untouched prefix", () => initial = capture());
            seekTo(secondNoteTime + meh_offset + 50, "first two-note pass");
            AddStep("capture first pass", () => first = capture());
            seekTo(first_note_time - 200, "rewound prefix");
            AddStep("capture reverted prefix", () => rewound = capture());
            seekTo(secondNoteTime + meh_offset + 50, "second two-note pass");
            AddStep("capture second pass", () => second = capture());
            AddStep("both passes and the reverted prefix match independent scoring values", () =>
            {
                const double expected_health = 1 - 2 * (5 + 1) * 0.0016;
                const double expected_accuracy = 50.0 / 305;
                double maximumComboPortion = 300 * (0.5 + 0.5 + Math.Log(3, 4));
                long expectedScore = (long)Math.Round(150000 * (50 / maximumComboPortion)
                                                     + 850000 * Math.Pow(expected_accuracy, 2 + 2 * expected_accuracy) * (2.0 / 3));

                Assert.Multiple(() =>
                {
                    assertPrefix(initial, "initial");
                    assertPrefix(rewound, "rewound");
                    assertTwoNotes(first, "first");
                    assertTwoNotes(second, "second");
                    Assert.That(first.Health, Is.EqualTo(expected_health).Within(1e-9), "first pass health oracle");
                    Assert.That(second.Health, Is.EqualTo(expected_health).Within(1e-9), "replayed health oracle");
                    Assert.That(first.Score, Is.EqualTo(expectedScore), "first pass standardised score oracle");
                    Assert.That(second.Score, Is.EqualTo(expectedScore), "replayed standardised score oracle");
                    Assert.That(first.Accuracy, Is.EqualTo(expected_accuracy).Within(1e-12), "first pass accuracy oracle");
                    Assert.That(second.Accuracy, Is.EqualTo(expected_accuracy).Within(1e-12), "replayed accuracy oracle");
                    Assert.That(second.Statistics, Is.EquivalentTo(first.Statistics), "replay cannot add another judgement");
                    Assert.That(second.Health, Is.EqualTo(first.Health).Within(1e-9), "rewind/forward round trip");
                });
            });

            void seekTo(double destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination));
                AddUntilStep($"replay reaches {description}", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(destination).Within(0.01));
            }

            static void assertPrefix(Snapshot snapshot, string phase)
            {
                Assert.That(snapshot.Combo, Is.Zero, $"{phase} combo");
                Assert.That(snapshot.HighestCombo, Is.Zero, $"{phase} highest combo");
                Assert.That(snapshot.Score, Is.Zero, $"{phase} score");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Meh), Is.Zero, $"{phase} MEH count");
                Assert.That(snapshot.Health, Is.EqualTo(1).Within(1e-9), $"{phase} health before either judged note");
            }

            static void assertTwoNotes(Snapshot snapshot, string phase)
            {
                Assert.That(snapshot.Combo, Is.EqualTo(2), $"{phase} combo");
                Assert.That(snapshot.HighestCombo, Is.EqualTo(2), $"{phase} highest combo");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Meh), Is.EqualTo(2), $"{phase} MEH count");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Miss), Is.Zero, $"{phase} misses");
            }
        }

        private Snapshot capture()
            => new Snapshot(Player.HealthProcessor.Health.Value, Player.ScoreProcessor.Combo.Value, Player.ScoreProcessor.HighestCombo.Value,
                Player.ScoreProcessor.TotalScore.Value, Player.ScoreProcessor.Accuracy.Value,
                Player.ScoreProcessor.Statistics.ToDictionary(pair => pair.Key, pair => pair.Value));

        private readonly record struct Snapshot(double Health, int Combo, int HighestCombo, long Score, double Accuracy,
                                                IReadOnlyDictionary<HitResult, int> Statistics);
    }
}
