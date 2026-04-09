// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsGaugeProcessorTest
    {
        [Test]
        public void TestRulesetCreatesBmsGaugeProcessor()
            => Assert.That(new BmsRuleset().CreateHealthProcessor(0), Is.TypeOf<BmsGaugeProcessor>());

        [TestCase(BmsGaugeType.AssistEasy, 0.2)]
        [TestCase(BmsGaugeType.Easy, 0.2)]
        [TestCase(BmsGaugeType.Normal, 0.2)]
        [TestCase(BmsGaugeType.Hard, 1.0)]
        [TestCase(BmsGaugeType.ExHard, 1.0)]
        [TestCase(BmsGaugeType.Hazard, 1.0)]
        public void TestGaugeTypesUseExpectedStartingValue(BmsGaugeType gaugeType, double expectedHealth)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, gaugeType);

            processor.ApplyBeatmap(beatmap);

            Assert.That(processor.Health.Value, Is.EqualTo(expectedHealth).Within(0.000001));
        }

        [Test]
        public void TestDerivesBaseRateFromTotalAndHittableObjects()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0);

            processor.ApplyBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(processor.ChartTotal, Is.EqualTo(200).Within(0.001));
                Assert.That(processor.TotalHittableObjects, Is.EqualTo(1000));
                Assert.That(processor.BaseRate, Is.EqualTo(0.002).Within(0.000001));
                Assert.That(processor.Health.Value, Is.EqualTo(BmsGaugeProcessor.STARTING_GAUGE).Within(0.000001));
            });
        }

        [Test]
        public void TestCountsOnlyPlayableBmsObjectsForGaugeScaling()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 3, includeBgm: true, includeAutoPlayNote: true);
            var processor = new BmsGaugeProcessor(0);

            processor.ApplyBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(processor.TotalHittableObjects, Is.EqualTo(3));
                Assert.That(processor.BaseRate, Is.EqualTo(200d / 300).Within(0.000001));
            });
        }

        [TestCase(BmsLongNoteMode.LN, 1000)]
        [TestCase(BmsLongNoteMode.CN, 1001)]
        [TestCase(BmsLongNoteMode.HCN, 1001)]
        public void TestGaugeScalingRespectsAppliedLongNoteMode(BmsLongNoteMode longNoteMode, int expectedHittableObjects)
        {
            var beatmap = createHoldBeatmap(total: 200, baselineNoteCount: 999);
            var processor = new BmsGaugeProcessor(0);

            longNoteMode.ApplyToBeatmap(beatmap);

            processor.ApplyBeatmap(beatmap);

            var holdNote = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(processor.TotalHittableObjects, Is.EqualTo(expectedHittableObjects));
                Assert.That(processor.BaseRate, Is.EqualTo(200d / (expectedHittableObjects * 100)).Within(0.000001));
                Assert.That(((BmsHoldNoteTailJudgement)holdNote.Tail!.Judgement).CountsForScore, Is.EqualTo(longNoteMode.RequiresTailJudgement()));
                Assert.That(holdNote.BodyTicks.All(tick => tick.CountsForGauge == longNoteMode.RequiresBodyGaugeTicks()), Is.True);
            });
        }

        [TestCase(HitResult.Perfect, 0.2016)]
        [TestCase(HitResult.Great, 0.2016)]
        [TestCase(HitResult.Good, 0.2008)]
        [TestCase(HitResult.Meh, 0.1840)]
        [TestCase(HitResult.Miss, 0.1760)]
        public void TestNormalGaugeAppliesExpectedDeltaPerJudgement(HitResult hitResult, double expectedHealth)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0);

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(beatmap.HitObjects[0], hitResult));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(expectedHealth).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
            });
        }

        [TestCase(BmsGaugeType.AssistEasy, HitResult.Perfect, 0.2032, false)]
        [TestCase(BmsGaugeType.AssistEasy, HitResult.Meh, 0.1920, false)]
        [TestCase(BmsGaugeType.Easy, HitResult.Perfect, 0.2024, false)]
        [TestCase(BmsGaugeType.Easy, HitResult.Meh, 0.1880, false)]
        [TestCase(BmsGaugeType.Hard, HitResult.Meh, 0.9500, false)]
        [TestCase(BmsGaugeType.ExHard, HitResult.Meh, 0.9000, false)]
        [TestCase(BmsGaugeType.Hazard, HitResult.Meh, 0.0000, true)]
        public void TestGaugeTypesApplyExpectedDeltaPerJudgement(BmsGaugeType gaugeType, HitResult hitResult, double expectedHealth, bool expectedFailure)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, gaugeType);

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(beatmap.HitObjects[0], hitResult));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(expectedHealth).Within(0.000001));
                Assert.That(processor.HasFailed, Is.EqualTo(expectedFailure));
            });
        }

        [Test]
        public void TestHazardGoodDoesNotDamageOrFail()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, BmsGaugeType.Hazard);

            processor.ApplyBeatmap(beatmap);
            processor.Health.Value = 0.5;
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Good));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(0.5).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
            });
        }

        [Test]
        public void TestNormalGaugeUsesTwoPercentSurvivalFloor()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0);

            processor.ApplyBeatmap(beatmap);
            processor.Health.Value = 0.03;
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Miss));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(BmsGaugeProcessor.SURVIVAL_FLOOR).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
                Assert.That(processor.IsClear, Is.False);
            });
        }

        [TestCase(BmsGaugeType.AssistEasy)]
        [TestCase(BmsGaugeType.Easy)]
        [TestCase(BmsGaugeType.Normal)]
        public void TestNonSurvivalGaugesUseTwoPercentSurvivalFloor(BmsGaugeType gaugeType)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, gaugeType);

            processor.ApplyBeatmap(beatmap);
            processor.Health.Value = 0.03;
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Miss));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(BmsGaugeProcessor.SURVIVAL_FLOOR).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
            });
        }

        [TestCase(BmsGaugeType.Hard, HitResult.Meh, 20)]
        [TestCase(BmsGaugeType.ExHard, HitResult.Meh, 10)]
        [TestCase(BmsGaugeType.Hazard, HitResult.Meh, 1)]
        public void TestSurvivalGaugesFailWhenDepleted(BmsGaugeType gaugeType, HitResult hitResult, int requiredHits)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, gaugeType);

            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < requiredHits; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], hitResult));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(0).Within(0.000001));
                Assert.That(processor.HasFailed, Is.True);
                Assert.That(processor.IsClear, Is.False);
            });
        }

        [TestCase(BmsLongNoteMode.LN, HitResult.IgnoreHit, BmsGaugeProcessor.STARTING_GAUGE)]
        [TestCase(BmsLongNoteMode.CN, HitResult.IgnoreMiss, BmsGaugeProcessor.STARTING_GAUGE)]
        [TestCase(BmsLongNoteMode.HCN, HitResult.IgnoreHit, 0.20015984015984015)]
        [TestCase(BmsLongNoteMode.HCN, HitResult.IgnoreMiss, 0.1984015984015984)]
        public void TestBodyTicksOnlyAffectGaugeInHcnMode(BmsLongNoteMode longNoteMode, HitResult resultType, double expectedHealth)
        {
            var beatmap = createHoldBeatmap(total: 200, baselineNoteCount: 999);
            longNoteMode.ApplyToBeatmap(beatmap);

            var processor = new BmsGaugeProcessor(0);

            processor.ApplyBeatmap(beatmap);

            var bodyTick = beatmap.HitObjects.OfType<BmsHoldNote>().Single().BodyTicks.First();
            processor.ApplyResult(createResult(bodyTick, resultType));

            int expectedHittableObjects = longNoteMode.RequiresTailJudgement() ? 1001 : 1000;

            Assert.Multiple(() =>
            {
                Assert.That(processor.TotalHittableObjects, Is.EqualTo(expectedHittableObjects));
                Assert.That(processor.BaseRate, Is.EqualTo(200d / (expectedHittableObjects * 100)).Within(0.000001));
                Assert.That(processor.Health.Value, Is.EqualTo(expectedHealth).Within(0.000001));
            });
        }

        [Test]
        public void TestEmptyPoorUsesBadGaugeDamage()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0);
            var emptyPoor = new BmsEmptyPoorHitObject
            {
                StartTime = 1000,
            };

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(emptyPoor, HitResult.ComboBreak));

            Assert.Multiple(() =>
            {
                Assert.That(processor.BaseRate, Is.EqualTo(0.002).Within(0.000001));
                Assert.That(processor.Health.Value, Is.EqualTo(0.1840).Within(0.000001));
            });
        }

        [TestCase(BmsGaugeType.AssistEasy, 0.1920, false)]
        [TestCase(BmsGaugeType.Easy, 0.1880, false)]
        [TestCase(BmsGaugeType.Normal, 0.1840, false)]
        [TestCase(BmsGaugeType.Hard, 0.9500, false)]
        [TestCase(BmsGaugeType.ExHard, 0.9000, false)]
        [TestCase(BmsGaugeType.Hazard, 0.0000, true)]
        public void TestEmptyPoorUsesGaugeSpecificBadDamage(BmsGaugeType gaugeType, double expectedHealth, bool expectedFailure)
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGaugeProcessor(0, gaugeType);
            var emptyPoor = new BmsEmptyPoorHitObject
            {
                StartTime = 1000,
            };

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(emptyPoor, HitResult.ComboBreak));

            Assert.Multiple(() =>
            {
                Assert.That(processor.Health.Value, Is.EqualTo(expectedHealth).Within(0.000001));
                Assert.That(processor.HasFailed, Is.EqualTo(expectedFailure));
            });
        }

        private static BmsBeatmap createBeatmap(double total, int noteCount, bool includeBgm = false, bool includeAutoPlayNote = false)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo
                {
                    Total = total,
                }
            };

            for (int i = 0; i < noteCount; i++)
            {
                beatmap.HitObjects.Add(new BmsHitObject
                {
                    StartTime = i,
                    LaneIndex = 1,
                });
            }

            if (includeBgm)
            {
                beatmap.HitObjects.Add(new BmsBgmEvent
                {
                    StartTime = noteCount + 1,
                });
            }

            if (includeAutoPlayNote)
            {
                beatmap.HitObjects.Add(new BmsHitObject
                {
                    StartTime = noteCount + 2,
                    LaneIndex = 0,
                    AutoPlay = true,
                });
            }

            return beatmap;
        }

        private static BmsBeatmap createHoldBeatmap(double total, int baselineNoteCount)
        {
            var beatmap = createBeatmap(total, baselineNoteCount);

            var holdNote = new BmsHoldNote
            {
                StartTime = baselineNoteCount + 1000,
                EndTime = baselineNoteCount + 1500,
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
    }
}
