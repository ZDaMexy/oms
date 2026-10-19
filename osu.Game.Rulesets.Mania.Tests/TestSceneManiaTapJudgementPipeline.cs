// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneManiaTapJudgementPipeline : RateAdjustedBeatmapTestScene
    {
        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [Test]
        public void TestAllSixTapGradesReachTheActualCompletedScore()
        {
            ManualReplayPlayer player = null!;
            DrawableManiaRuleset renderer = null!;
            GameplayClockContainer clock = null!;
            var actual = new List<(double Start, HitResult Type, double Offset)>();

            // Fixed OD=5 numbers are already pinned by TestSceneReplayStability: native windows
            // 19.5 / 49.5 / 82.5 / 112.5 / 136.5 / 173.5 ms. These offsets are inside each
            // distinct tier; expected results never call WindowFor, ResultFor, or ApplyResult.
            double[] starts = { 2000, 4000, 6000, 8000, 10000, 12000 };
            double[] offsets = { 0, -30, -60, -100, -130, -150 };
            HitResult[] expected = { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };

            AddStep("load a real Player with a fixed manual replay", () =>
            {
                var ruleset = new ManiaRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                var beatmap = new ManiaBeatmap(new StageDefinition(1))
                {
                    HitObjects = starts.Select(start => (ManiaHitObject)new Note { StartTime = start, Column = 0 }).ToList(),
                    Difficulty = { CircleSize = 1, OverallDifficulty = 5, DrainRate = 5 },
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };
                Beatmap.Value = CreateWorkingBeatmap(beatmap);
                SelectedMods.Value = System.Array.Empty<Mod>();

                var replay = new Replay { Frames = { new ManiaReplayFrame(0) } };
                for (int i = 0; i < starts.Length; i++)
                {
                    replay.Frames.Add(new ManiaReplayFrame(starts[i] + offsets[i], ManiaAction.Key1));
                    replay.Frames.Add(new ManiaReplayFrame(starts[i] + offsets[i] + 20));
                }

                replay.Frames.Add(new ManiaReplayFrame(13000));
                var score = new Score { Replay = replay, ScoreInfo = { Ruleset = ruleset.RulesetInfo } };
                var next = new ManualReplayPlayer(score);
                next.OnLoadComplete += _ => next.GameplayState.ScoreProcessor.NewJudgement += result =>
                {
                    if (result.HitObject is Note)
                        actual.Add((result.HitObject.StartTime, result.Type, result.TimeOffset));
                };
                LoadScreen(player = next);
            });
            AddUntilStep("manual ReplayPlayer is current", () => player?.IsCurrentScreen() == true);
            AddStep("retain production runtime owners", () =>
            {
                renderer = player.ChildrenOfType<DrawableManiaRuleset>().Single();
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                Assert.That(player.GameplayState.Mods, Is.Empty, "exercise current native mania, without Classic or AT");
            });
            AddStep("advance through the real manual input sequence", () => clock.Seek(13000));
            AddUntilStep("all actual notes complete", () => player.GameplayState.ScoreProcessor.HasCompleted.Value);
            AddStep("actual judgements and final statistics match the independent oracle", () =>
            {
                ScoreProcessor runtime = player.GameplayState.ScoreProcessor;
                ScoreInfo savedSnapshot = player.GameplayState.Score.ScoreInfo;
                Assert.Multiple(() =>
                {
                    Assert.That(actual.Select(result => result.Start), Is.EqualTo(starts), "six authored notes judged once, in order");
                    Assert.That(actual.Select(result => result.Type), Is.EqualTo(expected), "each grade came from real replay input");
                    for (int i = 0; i < actual.Count; i++)
                        Assert.That(actual[i].Offset, Is.EqualTo(offsets[i]).Within(0.01), $"input error for note {i}");

                    foreach (HitResult grade in expected)
                    {
                        Assert.That(runtime.Statistics.GetValueOrDefault(grade), Is.EqualTo(1), $"runtime {grade} count");
                        Assert.That(savedSnapshot.Statistics.GetValueOrDefault(grade), Is.EqualTo(1), $"completed score {grade} count");
                    }

                    // Independent specification arithmetic: accuracy weights 305+300+200+100+50+0,
                    // maximum 6*305. Combo weights are 150+150+200*log4(3)+100+50*log4(5),
                    // maximum 300*(.5+.5+log4(3)+1+log4(5)+log4(6)). Applying the documented
                    // 150000 combo / 850000 accuracy formula and rounding gives the fixed 176180.
                    Assert.That(runtime.Accuracy.Value, Is.EqualTo(955.0 / 1830).Within(1e-12));
                    Assert.That(runtime.TotalScore.Value, Is.EqualTo(176180));
                    Assert.That(runtime.Combo.Value, Is.Zero);
                    Assert.That(runtime.HighestCombo.Value, Is.EqualTo(5));
                    Assert.That(runtime.MaximumCombo, Is.EqualTo(6));
                    Assert.That(runtime.MaximumStatistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(6));
                    Assert.That(savedSnapshot.TotalScore, Is.EqualTo(176180));
                    Assert.That(savedSnapshot.Accuracy, Is.EqualTo(955.0 / 1830).Within(1e-12));
                    Assert.That(savedSnapshot.MaxCombo, Is.EqualTo(5));
                });
            });
        }

        private sealed partial class ManualReplayPlayer : ReplayPlayer
        {
            protected override bool PauseOnFocusLost => false;

            public ManualReplayPlayer(Score score)
                : base(score, new PlayerConfiguration { AllowPause = false, ShowResults = false })
            {
            }
        }
    }
}
