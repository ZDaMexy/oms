// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsScoreProcessorTest
    {
        [Test]
        public void TestRulesetCreatesBmsScoreProcessor()
            => Assert.That(new BmsRuleset().CreateScoreProcessor(), Is.TypeOf<BmsScoreProcessor>());

        [Test]
        public void TestApplyBeatmapComputesMaximumExScoreAndCombo()
        {
            var processor = new BmsScoreProcessor();

            processor.ApplyBeatmap(createBeatmap(3));

            Assert.Multiple(() =>
            {
                Assert.That(processor.MaximumExScore, Is.EqualTo(6));
                Assert.That(processor.MaximumCombo, Is.EqualTo(3));
                Assert.That(processor.MaximumStatistics[HitResult.Perfect], Is.EqualTo(3));
            });
        }

        [Test]
        public void TestExScoreAndComboRulesFollowBmsBehaviour()
        {
            var beatmap = createBeatmap(4);
            var processor = new BmsScoreProcessor();

            processor.ApplyBeatmap(beatmap);

            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Perfect));
            processor.ApplyResult(createResult(beatmap.HitObjects[1], HitResult.Great));
            processor.ApplyResult(createResult(beatmap.HitObjects[2], HitResult.Good));

            Assert.Multiple(() =>
            {
                Assert.That(processor.CurrentExScore, Is.EqualTo(3));
                Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(3));
                Assert.That(processor.Combo.Value, Is.EqualTo(3));
                Assert.That(processor.HighestCombo.Value, Is.EqualTo(3));
            });

            processor.ApplyResult(createResult(beatmap.HitObjects[3], HitResult.Meh));

            Assert.Multiple(() =>
            {
                Assert.That(processor.CurrentExScore, Is.EqualTo(3));
                Assert.That(processor.Combo.Value, Is.EqualTo(0));
                Assert.That(processor.HighestCombo.Value, Is.EqualTo(3));
                Assert.That(processor.Accuracy.Value, Is.EqualTo(3d / 8).Within(0.000001));
            });
        }

        [Test]
        public void TestEmptyPoorBreaksComboWithoutAffectingExScoreOrAccuracy()
        {
            var beatmap = createBeatmap(1);
            var processor = new BmsScoreProcessor();
            var emptyPoor = new BmsEmptyPoorHitObject
            {
                StartTime = 1000,
            };

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Perfect));
            processor.ApplyResult(createResult(emptyPoor, HitResult.ComboBreak));

            Assert.Multiple(() =>
            {
                Assert.That(processor.CurrentExScore, Is.EqualTo(2));
                Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(2));
                Assert.That(processor.Combo.Value, Is.EqualTo(0));
                Assert.That(processor.HighestCombo.Value, Is.EqualTo(1));
                Assert.That(processor.Accuracy.Value, Is.EqualTo(1).Within(0.000001));
                Assert.That(processor.JudgedHits, Is.EqualTo(1));
                Assert.That(BmsScoreProcessor.GetEmptyPoorCount(processor.Statistics), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestReplayResetDoesNotCountEmptyPoorTowardsCompletion()
        {
            var processor = new BmsScoreProcessor();

            processor.ResetFromReplayFrame(new ReplayFrame
            {
                Header = new FrameHeader(0, 1, 0, 1, new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 1,
                    [HitResult.ComboBreak] = 3,
                }, new ScoreProcessorStatistics
                {
                    MaximumBaseScore = 2,
                    BaseScore = 2,
                    AccuracyJudgementCount = 1,
                    ComboPortion = 0,
                    BonusPortion = 0,
                }, DateTimeOffset.Now)
            });

            Assert.That(processor.JudgedHits, Is.EqualTo(1));
        }

        [Test]
        public void TestConvertedSevenKeyBeatmapCanReachCompletionState()
        {
            const string text = @"
#TITLE Completion Regression
#BPM 120
#00111:AA00
#00116:BB00
#00117:CC00
#00119:DD00
";

            var decodedChart = new BmsBeatmapDecoder().DecodeText(text, "completion.bme");
            var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var processor = new TestableBmsScoreProcessor();
            var manualClock = new ManualClock
            {
                CurrentTime = 10000,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);

            var playableObjects = beatmap.HitObjects.OfType<BmsHitObject>().OrderBy(hitObject => hitObject.StartTime).ToArray();

            Assert.That(playableObjects.Select(hitObject => hitObject.LaneIndex), Is.EqualTo(new[] { 1, 0, 7 }));

            foreach (var hitObject in playableObjects)
                processor.ApplyResult(createResult(hitObject, HitResult.Perfect));

            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(playableObjects, Has.Length.EqualTo(3));
                Assert.That(processor.JudgedHits, Is.EqualTo(3));
                Assert.That(processor.HasCompleted.Value, Is.True);
            });
        }

        [Test]
        public void TestHoldNoteCompletionReachesTrueInLnMode()
        {
            var beatmap = createHoldBeatmap();
            var processor = new TestableBmsScoreProcessor();
            var manualClock = new ManualClock
            {
                CurrentTime = 10000,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);

            var holdNote = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

            // Apply results for all nested and parent objects, similar to gameplay
            processor.ApplyResult(createResult(holdNote.Head!, HitResult.Perfect));

            foreach (var bodyTick in holdNote.BodyTicks)
                processor.ApplyResult(createResult(bodyTick, HitResult.IgnoreHit));

            processor.ApplyResult(createResult(holdNote.Tail!, HitResult.IgnoreHit));
            processor.ApplyResult(createResult(holdNote, HitResult.IgnoreHit));

            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.HasCompleted.Value, Is.True, $"JudgedHits={processor.JudgedHits}");
            });
        }

        [Test]
        public void TestMixedBeatmapCompletionReachesTrue()
        {
            var beatmap = createHoldBeatmap();

            // Add a single note alongside the hold note
            beatmap.HitObjects.Add(new BmsHitObject
            {
                StartTime = 500,
                LaneIndex = 2,
            });

            var processor = new TestableBmsScoreProcessor();
            var manualClock = new ManualClock
            {
                CurrentTime = 10000,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);

            var holdNote = beatmap.HitObjects.OfType<BmsHoldNote>().Single();
            var singleNote = beatmap.HitObjects.OfType<BmsHitObject>().First(h => h is not BmsHoldNote);

            // Apply single note result
            processor.ApplyResult(createResult(singleNote, HitResult.Perfect));

            // Apply hold note results
            processor.ApplyResult(createResult(holdNote.Head!, HitResult.Perfect));

            foreach (var bodyTick in holdNote.BodyTicks)
                processor.ApplyResult(createResult(bodyTick, HitResult.IgnoreHit));

            processor.ApplyResult(createResult(holdNote.Tail!, HitResult.IgnoreHit));
            processor.ApplyResult(createResult(holdNote, HitResult.IgnoreHit));

            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.HasCompleted.Value, Is.True, $"JudgedHits={processor.JudgedHits}");
            });
        }

        [Test]
        public void TestCompletionStateReachesTrueAtFinalJudgementTime()
        {
            var beatmap = createBeatmap(1);
            var processor = new TestableBmsScoreProcessor();
            var manualClock = new ManualClock
            {
                CurrentTime = beatmap.HitObjects[0].StartTime,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Perfect));

            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.JudgedHits, Is.EqualTo(1));
                Assert.That(processor.HasCompleted.Value, Is.True);
            });
        }

        [Test]
        public void TestCompletionStateRemainsTrueAfterClockDriftsBackwards()
        {
            var beatmap = createBeatmap(1);
            var processor = new TestableBmsScoreProcessor();
            var result = createResult(beatmap.HitObjects[0], HitResult.Perfect);
            var manualClock = new ManualClock
            {
                CurrentTime = beatmap.HitObjects[0].StartTime,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(result);
            processor.UpdateCompletionState();

            manualClock.CurrentTime = beatmap.HitObjects[0].StartTime - 1;
            testClock.ProcessFrame();
            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.JudgedHits, Is.EqualTo(1));
                Assert.That(processor.HasCompleted.Value, Is.True);
            });
        }

        [Test]
        public void TestCompletionStateClearsWhenJudgementIsReverted()
        {
            var beatmap = createBeatmap(1);
            var processor = new TestableBmsScoreProcessor();
            var result = createResult(beatmap.HitObjects[0], HitResult.Perfect);
            var manualClock = new ManualClock
            {
                CurrentTime = beatmap.HitObjects[0].StartTime,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(result);
            processor.UpdateCompletionState();

            processor.RevertResult(result);
            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.JudgedHits, Is.EqualTo(0));
                Assert.That(processor.HasCompleted.Value, Is.False);
            });
        }

        [Test]
        public void TestCompletionStateIgnoresTrailingBgmEvents()
        {
            var beatmap = createBeatmap(1);
            var processor = new TestableBmsScoreProcessor();
            var manualClock = new ManualClock
            {
                CurrentTime = beatmap.HitObjects[0].StartTime,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);

            beatmap.HitObjects.Add(new BmsBgmEvent
            {
                StartTime = beatmap.HitObjects[0].StartTime + 1000,
            });

            testClock.ProcessFrame();
            processor.Clock = testClock;
            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Perfect));

            processor.UpdateCompletionState();

            Assert.Multiple(() =>
            {
                Assert.That(processor.JudgedHits, Is.EqualTo(1));
                Assert.That(processor.HasCompleted.Value, Is.True);
            });
        }

        [TestCase(8, 9, BmsDjLevel.AAA)]
        [TestCase(7, 9, BmsDjLevel.AA)]
        [TestCase(6, 9, BmsDjLevel.A)]
        [TestCase(5, 9, BmsDjLevel.B)]
        [TestCase(4, 9, BmsDjLevel.C)]
        [TestCase(3, 9, BmsDjLevel.D)]
        [TestCase(2, 9, BmsDjLevel.E)]
        [TestCase(1, 9, BmsDjLevel.F)]
        public void TestDjLevelBoundaries(long exScore, long maxExScore, BmsDjLevel expected)
            => Assert.That(BmsDjLevelCalculator.Calculate(exScore, maxExScore), Is.EqualTo(expected));

        [TestCase(1.0, ScoreRank.X)]
        [TestCase(8d / 9, ScoreRank.S)]
        [TestCase(7d / 9, ScoreRank.A)]
        [TestCase(6d / 9, ScoreRank.B)]
        [TestCase(5d / 9, ScoreRank.C)]
        [TestCase(4d / 9, ScoreRank.D)]
        public void TestScoreRankUsesBmsExThresholds(double exRatio, ScoreRank expected)
            => Assert.That(new BmsScoreProcessor().RankFromScore(exRatio, new Dictionary<HitResult, int>()), Is.EqualTo(expected));

        [Test]
        public void TestExScoreHelpersUseStatistics()
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 3,
                [HitResult.Great] = 2,
                [HitResult.Good] = 1,
            };

            Assert.That(BmsScoreProcessor.CalculateExScore(statistics), Is.EqualTo(8));
            Assert.That(BmsScoreProcessor.CalculateMaxExScore(statistics), Is.EqualTo(8));
        }

        [Test]
        public void TestLongNoteModeDefaultsToLnWithoutPersistedScoreData()
            => Assert.That(BmsScoreProcessor.GetLongNoteMode(new ScoreInfo()), Is.EqualTo(BmsLongNoteMode.LN));

        [Test]
        public void TestJudgeModeDefaultsToOdWithoutPersistedScoreData()
            => Assert.That(BmsJudgeModeExtensions.GetJudgeMode(new ScoreInfo()), Is.EqualTo(BmsJudgeMode.OD));

        [Test]
        public void TestLongNoteModeUsesPersistedScoreData()
        {
            var score = new ScoreInfo();

            score.SetRulesetData(new BmsScoreInfoData
            {
                LongNoteMode = BmsLongNoteMode.HCN,
            });

            Assert.That(BmsScoreProcessor.GetLongNoteMode(score), Is.EqualTo(BmsLongNoteMode.HCN));
        }

        [Test]
        public void TestJudgeModeUsesPersistedScoreData()
        {
            var score = new ScoreInfo();

            score.SetRulesetData(new BmsScoreInfoData
            {
                JudgeMode = BmsJudgeMode.LR2,
            });

            Assert.That(BmsJudgeModeExtensions.GetJudgeMode(score), Is.EqualTo(BmsJudgeMode.LR2));
        }

        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestLongNoteModeUsesScoreModsWhenPersistedScoreDataAbsent(BmsLongNoteMode expectedMode)
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { createLongNoteModeMod(expectedMode) },
            };

            Assert.That(BmsScoreProcessor.GetLongNoteMode(score), Is.EqualTo(expectedMode));
        }

        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        public void TestJudgeModeUsesScoreModsWhenPersistedScoreDataAbsent(BmsJudgeMode expectedMode)
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { createJudgeModeMod(expectedMode) },
            };

            Assert.That(BmsJudgeModeExtensions.GetJudgeMode(score), Is.EqualTo(expectedMode));
        }

        [Test]
        public void TestPersistedLongNoteModeOverridesScoreMods()
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { new BmsModChargeNote() },
            };

            score.SetRulesetData(new BmsScoreInfoData
            {
                LongNoteMode = BmsLongNoteMode.HCN,
            });

            Assert.That(BmsScoreProcessor.GetLongNoteMode(score), Is.EqualTo(BmsLongNoteMode.HCN));
        }

        [Test]
        public void TestPersistedJudgeModeOverridesScoreMods()
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { new BmsModJudgeBeatoraja() },
            };

            score.SetRulesetData(new BmsScoreInfoData
            {
                JudgeMode = BmsJudgeMode.LR2,
            });

            Assert.That(BmsJudgeModeExtensions.GetJudgeMode(score), Is.EqualTo(BmsJudgeMode.LR2));
        }

        [Test]
        public void TestRulesetScoreDisplayBucketUsesPersistedScoreData()
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { new BmsModJudgeBeatoraja(), new BmsModChargeNote() },
            };

            score.SetRulesetData(new BmsScoreInfoData
            {
                JudgeMode = BmsJudgeMode.LR2,
                LongNoteMode = BmsLongNoteMode.HCN,
            });

            Assert.That(new BmsRuleset().GetScoreDisplayBucket(score), Is.EqualTo("judge-mode:LR2|long-note-mode:HCN"));
        }

        [TestCase(BmsJudgeMode.OD, BmsLongNoteMode.LN, "judge-mode:OD|long-note-mode:LN")]
        [TestCase(BmsJudgeMode.Beatoraja, BmsLongNoteMode.CN, "judge-mode:BEATORAJA|long-note-mode:CN")]
        [TestCase(BmsJudgeMode.LR2, BmsLongNoteMode.HCN, "judge-mode:LR2|long-note-mode:HCN")]
        public void TestRulesetScoreDisplayBucketUsesSelectedJudgeAndLongNoteModes(BmsJudgeMode judgeMode, BmsLongNoteMode longNoteMode, string expectedBucket)
        {
            var mods = new List<Mod>();

            if (judgeMode != BmsJudgeMode.OD)
                mods.Add(createJudgeModeMod(judgeMode));

            if (longNoteMode != BmsLongNoteMode.LN)
                mods.Add(createLongNoteModeMod(longNoteMode));

            Assert.That(new BmsRuleset().GetScoreDisplayBucket(mods), Is.EqualTo(expectedBucket));
        }

        [Test]
        public void TestScoreDisplayBucketFilteringUsesJudgeAndLongNoteMode()
        {
            var ruleset = new BmsRuleset();
            var filteredScores = new[]
            {
                createScoreForModes(BmsJudgeMode.OD, BmsLongNoteMode.HCN),
                createScoreForModes(BmsJudgeMode.Beatoraja, BmsLongNoteMode.HCN),
                createScoreForModes(BmsJudgeMode.LR2, BmsLongNoteMode.CN),
            }.FilterToScoreDisplayBucket(ruleset, ruleset.GetScoreDisplayBucket(new Mod[] { new BmsModJudgeBeatoraja(), new BmsModHellChargeNote() })).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(filteredScores, Has.Length.EqualTo(1));
                Assert.That(BmsJudgeModeExtensions.GetJudgeMode(filteredScores[0]), Is.EqualTo(BmsJudgeMode.Beatoraja));
                Assert.That(BmsScoreProcessor.GetLongNoteMode(filteredScores[0]), Is.EqualTo(BmsLongNoteMode.HCN));
            });
        }

        [TestCase(HitResult.Meh, "BAD")]
        [TestCase(HitResult.Miss, "POOR")]
        [TestCase(HitResult.ComboBreak, "EMPTY POOR")]
        public void TestRulesetDisplaysBmsHitResultNames(HitResult result, string expectedDisplayName)
            => Assert.That(new BmsRuleset().GetDisplayNameForHitResult(result).ToString(), Is.EqualTo(expectedDisplayName));

        [TestCase(BmsLongNoteMode.LN, 2, 1)]
        [TestCase(BmsLongNoteMode.CN, 4, 2)]
        [TestCase(BmsLongNoteMode.HCN, 4, 2)]
        public void TestApplyBeatmapUsesActiveLongNoteModeForHoldTail(BmsLongNoteMode longNoteMode, long expectedMaxExScore, int expectedCombo)
        {
            var beatmap = createHoldBeatmap();
            var processor = new BmsScoreProcessor();

            if (longNoteMode != BmsLongNoteMode.LN)
                processor.Mods.Value = new Mod[] { createLongNoteModeMod(longNoteMode) };

            processor.ApplyBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(processor.MaximumExScore, Is.EqualTo(expectedMaxExScore));
                Assert.That(processor.MaximumCombo, Is.EqualTo(expectedCombo));
                Assert.That(processor.MaximumStatistics[HitResult.Perfect], Is.EqualTo(expectedCombo));
                Assert.That(beatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail?.Judgement, Is.TypeOf<BmsHoldNoteTailJudgement>());
                Assert.That(((BmsHoldNoteTailJudgement)beatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail!.Judgement).CountsForScore, Is.EqualTo(longNoteMode.RequiresTailJudgement()));
                Assert.That(beatmap.HitObjects.OfType<BmsHoldNote>().Single().BodyTicks.All(tick => tick.CountsForGauge == longNoteMode.RequiresBodyGaugeTicks()), Is.True);
            });
        }

        private static BmsBeatmap createBeatmap(int noteCount)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo()
            };

            for (int i = 0; i < noteCount; i++)
            {
                beatmap.HitObjects.Add(new BmsHitObject
                {
                    StartTime = i,
                    LaneIndex = 1,
                });
            }

            return beatmap;
        }

        private static BmsBeatmap createHoldBeatmap()
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo()
            };

            var holdNote = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            beatmap.HitObjects.Add(holdNote);
            return beatmap;
        }

        private static JudgementResult createResult(osu.Game.Rulesets.Objects.HitObject hitObject, HitResult hitResult)
            => new JudgementResult(hitObject, hitObject.CreateJudgement())
            {
                Type = hitResult,
            };

        private static ScoreInfo createScoreForModes(BmsJudgeMode judgeMode, BmsLongNoteMode longNoteMode)
        {
            var score = new ScoreInfo();

            var mods = new List<Mod>();

            if (judgeMode != BmsJudgeMode.OD)
                mods.Add(createJudgeModeMod(judgeMode));

            if (longNoteMode != BmsLongNoteMode.LN)
                mods.Add(createLongNoteModeMod(longNoteMode));

            if (mods.Count > 0)
                score.Mods = mods.ToArray();

            score.SetRulesetData(new BmsScoreInfoData
            {
                JudgeMode = judgeMode,
                LongNoteMode = longNoteMode,
            });

            return score;
        }

        private static Mod createLongNoteModeMod(BmsLongNoteMode longNoteMode)
            => longNoteMode switch
            {
                BmsLongNoteMode.CN => new BmsModChargeNote(),
                BmsLongNoteMode.HCN => new BmsModHellChargeNote(),
                _ => throw new AssertionException($"Unsupported long note mode test input: {longNoteMode}"),
            };

        private static Mod createJudgeModeMod(BmsJudgeMode judgeMode)
            => judgeMode switch
            {
                BmsJudgeMode.Beatoraja => new BmsModJudgeBeatoraja(),
                BmsJudgeMode.LR2 => new BmsModJudgeLr2(),
                _ => throw new AssertionException($"Unsupported judge mode test input: {judgeMode}"),
            };

        private sealed partial class TestableBmsScoreProcessor : BmsScoreProcessor
        {
            public void UpdateCompletionState() => Update();
        }
    }
}
