// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsAutoplayResultParity : RateAdjustedBeatmapTestScene
    {
        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new BmsRuleset();

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(BmsLongNoteMode.LN, 3)]
        [TestCase(BmsLongNoteMode.CN, 4)]
        [TestCase(BmsLongNoteMode.HCN, 4)]
        public void TestActualAutoplayPreservesLiveGaugeAndPerfectLampWhenPreparingResults(BmsLongNoteMode mode, int expectedScoredNotes)
        {
            AutoplayPlayer player = null!;
            DrawableBmsRuleset renderer = null!;
            GameplayClockContainer gameplayClock = null!;
            BmsGaugeProcessor gauge = null!;
            ScoreInfo snapshot = null!;
            double liveGauge = double.NaN;
            double rebuiltGauge = double.NaN;
            double endTime = double.NaN;

            AddStep("load the real autoplay Player and generated replay", () =>
            {
                var ruleset = new BmsRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                    #TITLE Autoplay runtime and results parity
                    #BPM 120
                    #RANK 2
                    #TOTAL 100
                    #LNTYPE 1
                    #WAV01 note.wav
                    #00111:01
                    #00212:01
                    #00351:01
                    #00451:01
                    """, "autoplay-result-parity.bme"))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };
                Beatmap.Value = CreateWorkingBeatmap(source);
                SelectedMods.Value = mode switch
                {
                    BmsLongNoteMode.LN => new Mod[] { new BmsModAutoplay() },
                    BmsLongNoteMode.CN => new Mod[] { new BmsModAutoplay(), new BmsModChargeNote() },
                    BmsLongNoteMode.HCN => new Mod[] { new BmsModAutoplay(), new BmsModHellChargeNote() },
                    _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                };
                LoadScreen(player = new AutoplayPlayer());
            });
            AddUntilStep("autoplay Player is the current screen", () => player?.IsCurrentScreen() == true);
            AddStep("retain actual runtime owners", () =>
            {
                renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                gameplayClock = player.ChildrenOfType<GameplayClockContainer>().Single();
                gauge = (BmsGaugeProcessor)player.GameplayState.HealthProcessor;
                endTime = player.GameplayState.Beatmap.HitObjects.Max(hitObject => hitObject.GetEndTime());

                Assert.Multiple(() =>
                {
                    Assert.That(renderer.Mods.OfType<BmsModAutoplay>().Count(), Is.EqualTo(1));
                    Assert.That(renderer.Beatmap.HitObjects.OfType<BmsHitObject>().All(note => note.AutoPlay), Is.True,
                        "Exercise the production full-autoplay state, which retains scorable judgements.");
                    Assert.That(gauge.TotalHittableObjects, Is.EqualTo(expectedScoredNotes), "runtime recovery denominator");
                    Assert.That(gauge.ChartTotal, Is.EqualTo(100), "authored TOTAL");
                    Assert.That(player.GameplayState.Score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.Version, Is.EqualTo(7), "new autoplay identity");
                });
            });
            AddStep("seek through the actual autoplay chart", () => gameplayClock.Seek(endTime + 100));
            AddUntilStep("frame-stable gameplay reaches chart end", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(endTime + 100).Within(0.01));
            AddUntilStep("all actual note judgements completed", () => player.GameplayState.ScoreProcessor.HasCompleted.Value);
            AddStep("capture completed runtime score and prepare the same playable chart for results", () =>
            {
                liveGauge = gauge.Health.Value;
                snapshot = player.GameplayState.Score.ScoreInfo.DeepClone();
                rebuiltGauge = BmsClearLampProcessor.CalculateFinalGauge(snapshot, player.GameplayState.Beatmap);
                new BmsRuleset().PrepareScoreInfoForResults(snapshot, player.GameplayState.Beatmap);
            });
            AddStep("completed autoplay retains the independent perfect outcome through results preparation", () =>
            {
                var finalData = snapshot.GetRulesetData<BmsScoreInfoData>();
                Assert.Multiple(() =>
                {
                    // TOTAL=100 and Legacy Normal provide 0.8 total recovery. Together with the initial 0.2,
                    // every PG reaches 1; HCN's extra successful body ticks can only meet the same upper bound.
                    Assert.That(liveGauge, Is.EqualTo(1).Within(1e-9), "runtime independent gauge oracle");
                    Assert.That(rebuiltGauge, Is.EqualTo(1).Within(1e-9), "result reconstruction independent gauge oracle");
                    Assert.That(rebuiltGauge, Is.EqualTo(liveGauge).Within(1e-9), "runtime and rebuilt gauge parity");
                    Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(expectedScoredNotes), "actual PG count");
                    Assert.That(snapshot.Statistics.GetValueOrDefault(HitResult.Miss), Is.Zero, "actual misses");
                    Assert.That(snapshot.MaximumStatistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(expectedScoredNotes), "maximum PG count");
                    Assert.That(snapshot.TotalScore, Is.EqualTo(expectedScoredNotes * 2), "actual EX");
                    Assert.That(snapshot.Accuracy, Is.EqualTo(1), "actual accuracy");
                    Assert.That(snapshot.MaxCombo, Is.EqualTo(expectedScoredNotes), "actual maximum combo");
                    Assert.That(finalData, Is.Not.Null, "prepared result metadata");
                    Assert.That(finalData?.Version, Is.EqualTo(7), "TOTAL version is preserved");
                    Assert.That(finalData?.LongNoteMode, Is.EqualTo(mode), "selected long-note mode is preserved");
                    Assert.That(finalData?.FinalGauge, Is.EqualTo(1).Within(1e-9), "persisted final gauge");
                    Assert.That(finalData?.ClearLamp, Is.EqualTo(BmsClearLamp.Perfect), "persisted lamp for a completed all-PG clear");
                });
            });
        }

        private sealed partial class AutoplayPlayer : ReplayPlayer
        {
            protected override bool PauseOnFocusLost => false;

            public AutoplayPlayer()
                : base(new BmsModAutoplay().CreateScoreFromReplayData, new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
