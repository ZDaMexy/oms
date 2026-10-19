// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsCrossLaneRewindScoring : PlayerTestScene
    {
        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });
        private bool simultaneous;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override bool AllowFail => true;

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText($"""
                #TITLE Cross-lane scoring rewind
                #BPM 120
                #RANK 2
                #TOTAL 1
                #WAV01 note.wav
                #00111:01
                #00112:{(simultaneous ? "01" : "0001")}
                #00811:01
                """, "cross-lane-rewind.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(true)]
        [TestCase(false)]
        public void TestManualReplayRewindRestoresCrossLaneScoreAndGauge(bool sameTime)
        {
            DrawableBmsRuleset renderer = null!;
            BmsHitObject[] notes = null!;
            Snapshot initial = default;
            Snapshot first = default;
            Snapshot rewound = default;
            Snapshot second = default;

            AddStep("load real BMS player", () =>
            {
                simultaneous = sameTime;
                LoadPlayer();
            });
            AddUntilStep("player loaded", () => Player?.IsLoaded == true && Player.DrawableRuleset is DrawableBmsRuleset);
            AddStep("attach actual two-lane manual replay", () =>
            {
                renderer = (DrawableBmsRuleset)Player.DrawableRuleset;
                notes = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().OrderBy(note => note.StartTime).ThenBy(note => note.LaneIndex).ToArray();
                Assert.That(notes.Length, Is.EqualTo(3));
                var replay = new Replay { Frames = { new BmsReplayFrame(0) } };

                if (sameTime)
                {
                    replay.Frames.Add(new BmsReplayFrame(notes[0].StartTime, BmsAction.Key1, BmsAction.Key2));
                    replay.Frames.Add(new BmsReplayFrame(notes[0].StartTime + 20));
                }
                else
                {
                    replay.Frames.Add(new BmsReplayFrame(notes[0].StartTime, BmsAction.Key1));
                    replay.Frames.Add(new BmsReplayFrame(notes[0].StartTime + 20));
                    replay.Frames.Add(new BmsReplayFrame(notes[1].StartTime, BmsAction.Key2));
                    replay.Frames.Add(new BmsReplayFrame(notes[1].StartTime + 20));
                }

                replay.Frames.Add(new BmsReplayFrame(notes[2].StartTime + 100));
                var score = new Score { Replay = replay, ScoreInfo = { Mods = renderer.Mods.ToArray() } };
                BmsScoreInfoData.InitialiseNewPlay(score.ScoreInfo);
                renderer.SetRecordTarget(null!);
                renderer.SetReplayScore(score);
            });

            seekTo(() => notes[0].StartTime - 200, "before either note");
            AddStep("capture untouched prefix", () => initial = capture());
            seekTo(() => notes[1].StartTime + 50, "first two-note pass");
            AddStep("capture first pass", () => first = capture());
            seekTo(() => notes[0].StartTime - 200, "rewound prefix");
            AddStep("capture reverted prefix", () => rewound = capture());
            seekTo(() => notes[1].StartTime + 50, "second two-note pass");
            AddStep("capture second pass", () => second = capture());
            AddStep("both passes and the reverted prefix match independent scoring values", () =>
            {
                // Three authored notes, TOTAL=1, Legacy Normal: each PG changes the gauge by
                // (TOTAL / N) * 0.8 / 100. No result reaches either clamp or a failure threshold.
                const double expected_after_two = 0.2 + 2 * (1.0 / 3 * 0.8 / 100);

                Assert.Multiple(() =>
                {
                    assertPrefix(initial, "initial");
                    assertPrefix(rewound, "rewound");
                    assertTwoNotes(first, "first");
                    assertTwoNotes(second, "second");
                    Assert.That(first.Health, Is.EqualTo(expected_after_two).Within(1e-9), "first pass gauge oracle");
                    Assert.That(second.Health, Is.EqualTo(expected_after_two).Within(1e-9), "replayed gauge oracle");
                    Assert.That(second.Statistics, Is.EquivalentTo(first.Statistics), "replay cannot add another judgement");
                    Assert.That(second.Health, Is.EqualTo(first.Health).Within(1e-9), "rewind/forward round trip");
                });
            });

            void seekTo(System.Func<double> destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination()));
                AddUntilStep($"replay reaches {description}", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(destination()).Within(0.01));
            }

            static void assertPrefix(Snapshot snapshot, string phase)
            {
                Assert.That(snapshot.JudgedHits, Is.Zero, $"{phase} judged notes");
                Assert.That(snapshot.Combo, Is.Zero, $"{phase} combo");
                Assert.That(snapshot.HighestCombo, Is.Zero, $"{phase} highest combo");
                Assert.That(snapshot.Score, Is.Zero, $"{phase} EX");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Perfect), Is.Zero, $"{phase} PG count");
                Assert.That(snapshot.Health, Is.EqualTo(0.2).Within(1e-9), $"{phase} gauge before either judged note");
            }

            static void assertTwoNotes(Snapshot snapshot, string phase)
            {
                Assert.That(snapshot.JudgedHits, Is.EqualTo(2), $"{phase} judged notes");
                Assert.That(snapshot.Combo, Is.EqualTo(2), $"{phase} combo");
                Assert.That(snapshot.HighestCombo, Is.EqualTo(2), $"{phase} highest combo");
                Assert.That(snapshot.Score, Is.EqualTo(4), $"{phase} EX");
                Assert.That(snapshot.Accuracy, Is.EqualTo(1), $"{phase} accuracy");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(2), $"{phase} PG count");
                Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Miss), Is.Zero, $"{phase} misses");
            }
        }

        private Snapshot capture()
            => new Snapshot(Player.HealthProcessor.Health.Value, Player.ScoreProcessor.JudgedHits, Player.ScoreProcessor.Combo.Value,
                Player.ScoreProcessor.HighestCombo.Value, Player.ScoreProcessor.TotalScore.Value, Player.ScoreProcessor.Accuracy.Value,
                Player.ScoreProcessor.Statistics.ToDictionary(pair => pair.Key, pair => pair.Value));

        private readonly record struct Snapshot(double Health, int JudgedHits, int Combo, int HighestCombo, long Score, double Accuracy,
                                                IReadOnlyDictionary<HitResult, int> Statistics);
    }
}
