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
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsTapJudgementPipeline : RateAdjustedBeatmapTestScene
    {
        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new BmsRuleset();

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(BmsJudgeMode.OD)]
        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        [TestCase(BmsJudgeMode.IIDX)]
        public void TestEachJudgeFamilyDeliversAllTapGradesAndEmptyPoorToTheCompletedScore(BmsJudgeMode family)
        {
            ManualReplayPlayer player = null!;
            DrawableBmsRuleset renderer = null!;
            GameplayClockContainer clock = null!;
            var actualNotes = new List<(double Start, HitResult Type, double Offset)>();
            var emptyPoors = new List<(double Start, int Combo, long Ex, double Accuracy)>();

            // Fixed NORMAL rank windows are already pinned by BmsJudgementSystemParityTest and
            // BmsDrawableRulesetTest: OD 17.5/43.5/76.5/130.5/167.5, Beatoraja 15/45/112 with
            // BAD early210/late165, LR2 18/40/100/200, IIDX 16.67/33.33/116.67/250.
            // 0,+30,-60,+125 produce PG/GR/GD/BAD in every family. The fifth note is unplayed.
            // At 7600 the -400ms press is outside every note window but inside each documented
            // empty-poor window (IIDX's 500/150 range remains an OMS heuristic).
            double[] starts = { 2000, 4000, 6000, 8000, 10000 };
            double[] hitOffsets = { 0, 30, -60, 125 };
            HitResult[] expected = { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Meh, HitResult.Miss };

            AddStep("load a real BMS ReplayPlayer with authored timings", () =>
            {
                var ruleset = new BmsRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                    #TITLE Fixed mixed tap judgement pipeline
                    #BPM 120
                    #RANK 2
                    #TOTAL 100
                    #WAV01 note.wav
                    #00111:01
                    #00211:01
                    #00311:01
                    #00411:01
                    #00511:01
                    """, "mixed-tap-judgements.bme"))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };
                Beatmap.Value = CreateWorkingBeatmap(source);
                Mod[] mods = family switch
                {
                    BmsJudgeMode.Beatoraja => new Mod[] { new BmsModJudgeBeatoraja() },
                    BmsJudgeMode.LR2 => new Mod[] { new BmsModJudgeLr2() },
                    BmsJudgeMode.IIDX => new Mod[] { new BmsModJudgeIidx() },
                    _ => System.Array.Empty<Mod>(),
                };
                SelectedMods.Value = mods;
                var replay = new Replay
                {
                    Frames =
                    {
                        new BmsReplayFrame(0),
                        new BmsReplayFrame(2000, BmsAction.Key1), new BmsReplayFrame(2020),
                        new BmsReplayFrame(4030, BmsAction.Key1), new BmsReplayFrame(4050),
                        new BmsReplayFrame(5940, BmsAction.Key1), new BmsReplayFrame(5960),
                        new BmsReplayFrame(7600, BmsAction.Key1), new BmsReplayFrame(7620),
                        new BmsReplayFrame(8125, BmsAction.Key1), new BmsReplayFrame(8145),
                        new BmsReplayFrame(11000),
                    },
                };
                var score = new Score { Replay = replay, ScoreInfo = { Ruleset = ruleset.RulesetInfo, Mods = mods } };
                BmsScoreInfoData.InitialiseNewPlay(score.ScoreInfo);
                var next = new ManualReplayPlayer(score);
                next.OnLoadComplete += _ => next.GameplayState.ScoreProcessor.NewJudgement += result =>
                {
                    ScoreProcessor processor = next.GameplayState.ScoreProcessor;
                    if (result.HitObject is BmsHitObject)
                        actualNotes.Add((result.HitObject.StartTime, result.Type, result.TimeOffset));
                    else if (result.Type == HitResult.Ok)
                        emptyPoors.Add((result.HitObject.StartTime, processor.Combo.Value, processor.TotalScore.Value, processor.Accuracy.Value));
                };
                LoadScreen(player = next);
            });
            AddUntilStep("manual ReplayPlayer is current", () => player?.IsCurrentScreen() == true);
            AddStep("retain exact production ruleset and clock", () =>
            {
                renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                Assert.That(renderer.JudgeMode, Is.EqualTo(family));
                Assert.That(renderer.Beatmap.HitObjects.OfType<BmsHitObject>().All(note => !note.AutoPlay), Is.True);
                Assert.That(player.GameplayState.Score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.Version, Is.EqualTo(7));
            });
            AddStep("advance through the actual replay and the unplayed fifth note", () => clock.Seek(11000));
            AddUntilStep("all five authored notes complete", () => player.GameplayState.ScoreProcessor.HasCompleted.Value);
            AddStep("actual input outcomes and completed score match the independent EX oracle", () =>
            {
                var runtime = (BmsScoreProcessor)player.GameplayState.ScoreProcessor;
                ScoreInfo savedSnapshot = player.GameplayState.Score.ScoreInfo;
                Assert.Multiple(() =>
                {
                    Assert.That(actualNotes.Select(result => result.Start), Is.EqualTo(starts), "five notes judged exactly once");
                    Assert.That(actualNotes.Select(result => result.Type), Is.EqualTo(expected), "all five actual note grades");
                    for (int i = 0; i < System.Math.Min(actualNotes.Count, hitOffsets.Length); i++)
                        Assert.That(actualNotes[i].Offset, Is.EqualTo(hitOffsets[i]).Within(0.01), $"input error for note {i}");

                    Assert.That(emptyPoors.Count, Is.EqualTo(1), "one genuine lane fallback from the manual replay");
                    if (emptyPoors.Count == 1)
                    {
                        Assert.That(emptyPoors[0].Start, Is.EqualTo(7600).Within(0.01));
                        Assert.That(emptyPoors[0].Combo, Is.EqualTo(3), "empty poor preserves the existing combo");
                        Assert.That(emptyPoors[0].Ex, Is.EqualTo(3), "empty poor adds no EX");
                        Assert.That(emptyPoors[0].Accuracy, Is.EqualTo(0.5), "empty poor adds no accuracy denominator");
                    }

                    foreach (HitResult grade in expected.Append(HitResult.Ok))
                    {
                        Assert.That(runtime.Statistics.GetValueOrDefault(grade), Is.EqualTo(1), $"runtime {grade} count");
                        Assert.That(savedSnapshot.Statistics.GetValueOrDefault(grade), Is.EqualTo(1), $"completed score {grade} count");
                    }

                    Assert.That(runtime.Statistics.GetValueOrDefault(HitResult.ComboBreak), Is.EqualTo(1), "only BAD breaks a nonzero combo");
                    Assert.That(runtime.JudgedHits, Is.EqualTo(5), "empty poor cannot advance completion");
                    Assert.That(runtime.MaximumExScore, Is.EqualTo(10), "five authored notes, 2 EX each");
                    Assert.That(runtime.CurrentExScore, Is.EqualTo(3), "one PG*2 plus one GR");
                    Assert.That(runtime.TotalScore.Value, Is.EqualTo(3));
                    Assert.That(runtime.Accuracy.Value, Is.EqualTo(0.3), "3 EX out of 10 maximum EX");
                    Assert.That(runtime.Combo.Value, Is.Zero);
                    Assert.That(runtime.HighestCombo.Value, Is.EqualTo(3));
                    Assert.That(savedSnapshot.TotalScore, Is.EqualTo(3));
                    Assert.That(savedSnapshot.Accuracy, Is.EqualTo(0.3));
                    Assert.That(savedSnapshot.MaxCombo, Is.EqualTo(3));
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
