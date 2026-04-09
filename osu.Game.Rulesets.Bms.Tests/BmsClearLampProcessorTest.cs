// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsClearLampProcessorTest
    {
        [TestCase(BmsGaugeType.AssistEasy, BmsClearLamp.AssistEasyClear)]
        [TestCase(BmsGaugeType.Easy, BmsClearLamp.EasyClear)]
        [TestCase(BmsGaugeType.Normal, BmsClearLamp.NormalClear)]
        [TestCase(BmsGaugeType.Hard, BmsClearLamp.HardClear)]
        [TestCase(BmsGaugeType.ExHard, BmsClearLamp.ExHardClear)]
        [TestCase(BmsGaugeType.Hazard, BmsClearLamp.HazardClear)]
        public void TestGaugeClearLampReflectsSelectedGaugeType(BmsGaugeType gaugeType, BmsClearLamp expectedLamp)
        {
            var beatmap = createBeatmap(100, total: 800);
            var score = createScore(beatmap, Enumerable.Repeat(HitResult.Perfect, 99).Concat(new[] { HitResult.Good }).ToArray());

            score.Mods = createGaugeMods(gaugeType);

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(expectedLamp));
                Assert.That(finalGauge, Is.GreaterThan(0));
            });
        }

        [Test]
        public void TestPerfectLampWhenExScoreMatchesMaximum()
        {
            var beatmap = createBeatmap(2);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Perfect);

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.Perfect));
                Assert.That(finalGauge, Is.EqualTo(1).Within(0.000001));
            });
        }

        [Test]
        public void TestFullComboLampAllowsGreatButNotGood()
        {
            var beatmap = createBeatmap(2);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great);

            Assert.That(BmsClearLampProcessor.Calculate(score, beatmap), Is.EqualTo(BmsClearLamp.FullCombo));
        }

        [Test]
        public void TestEmptyPoorPreventsPerfectAndFullComboLamp()
        {
            var beatmap = createBeatmap(1000);
            var score = createScore(beatmap, Enumerable.Repeat(HitResult.Perfect, 1000).ToArray());

            score.Statistics[HitResult.ComboBreak] = 1;
            score.HitEvents.Add(new HitEvent(0, 1.0, HitResult.ComboBreak, new BmsEmptyPoorHitObject
            {
                StartTime = 1000,
            }, beatmap.HitObjects[^1], null));

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.NormalClear));
                Assert.That(finalGauge, Is.GreaterThanOrEqualTo(BmsGaugeProcessor.CLEAR_THRESHOLD));
            });
        }

        [Test]
        public void TestHazardGaugeFailsOnBadJudgement()
        {
            var beatmap = createBeatmap(100, total: 800);
            var score = createScore(beatmap, Enumerable.Repeat(HitResult.Perfect, 99).Prepend(HitResult.Meh).ToArray());

            score.Mods = new Mod[] { new BmsModGaugeHazard() };

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.Failed));
                Assert.That(finalGauge, Is.EqualTo(0).Within(0.000001));
            });
        }

        [Test]
        public void TestGaugeAutoShiftDowngradesAndAwardsFinalActiveGaugeLamp()
        {
            var beatmap = createBeatmap(100, total: 800);
            var score = createScore(beatmap,
                Enumerable.Repeat(HitResult.Meh, 10)
                          .Concat(Enumerable.Repeat(HitResult.Meh, 20))
                          .Concat(Enumerable.Repeat(HitResult.Perfect, 70))
                          .ToArray());
            var gasMod = new BmsModGaugeAutoShift();

            gasMod.StartingGauge.Value = BmsGaugeType.ExHard;
            gasMod.FloorGauge.Value = BmsGaugeType.Easy;
            score.Mods = new Mod[] { gasMod };

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.NormalClear));
                Assert.That(finalGauge, Is.GreaterThanOrEqualTo(BmsGaugeProcessor.CLEAR_THRESHOLD));
            });
        }

        [Test]
        public void TestGaugeHistoryTracksGaugeAutoShiftTransitions()
        {
            var beatmap = createBeatmap(100, total: 800);
            var score = createScore(beatmap,
                Enumerable.Repeat(HitResult.Meh, 10)
                          .Concat(Enumerable.Repeat(HitResult.Meh, 20))
                          .Concat(Enumerable.Repeat(HitResult.Perfect, 70))
                          .ToArray());
            var gasMod = new BmsModGaugeAutoShift();

            gasMod.StartingGauge.Value = BmsGaugeType.ExHard;
            gasMod.FloorGauge.Value = BmsGaugeType.Easy;
            score.Mods = new Mod[] { gasMod };

            var history = BmsClearLampProcessor.CreateGaugeHistory(score, beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(history.Timelines.Select(timeline => timeline.GaugeType), Is.EqualTo(new[] { BmsGaugeType.ExHard, BmsGaugeType.Hard, BmsGaugeType.Normal }));
                Assert.That(history.Timelines[0].Samples.First().Value, Is.EqualTo(1).Within(0.000001));
                Assert.That(history.Timelines[0].Samples.Last().Value, Is.EqualTo(0).Within(0.000001));
                Assert.That(history.Timelines[1].Samples.First().Value, Is.EqualTo(1).Within(0.000001));
                Assert.That(history.Timelines[1].Samples.Last().Value, Is.EqualTo(0).Within(0.000001));
                Assert.That(history.Timelines[2].Samples.First().Value, Is.EqualTo(BmsGaugeProcessor.STARTING_GAUGE).Within(0.000001));
                Assert.That(history.Timelines[2].Samples.Last().Value, Is.GreaterThanOrEqualTo(BmsGaugeProcessor.CLEAR_THRESHOLD));
                Assert.That(history.EndTime, Is.GreaterThan(history.StartTime));
            });
        }

        [Test]
        public void TestGaugeAutoShiftNoPlayUsesConfiguredStartingGauge()
        {
            var beatmap = createBeatmap(1);
            var score = new ScoreInfo
            {
                Mods = new Mod[]
                {
                    new BmsModGaugeAutoShift
                    {
                        StartingGauge = { Value = BmsGaugeType.Hazard },
                        FloorGauge = { Value = BmsGaugeType.Easy },
                    }
                }
            };

            var available = BmsClearLampProcessor.TryCalculate(score, beatmap, out BmsClearLamp clearLamp, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(available, Is.True);
                Assert.That(clearLamp, Is.EqualTo(BmsClearLamp.NoPlay));
                Assert.That(finalGauge, Is.EqualTo(1).Within(0.000001));
            });
        }

        [Test]
        public void TestNormalClearLampWhenGaugeClearsWithoutFullCombo()
        {
            var beatmap = createBeatmap(5);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Great, HitResult.Great);

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.NormalClear));
                Assert.That(finalGauge, Is.GreaterThanOrEqualTo(BmsGaugeProcessor.CLEAR_THRESHOLD));
            });
        }

        [Test]
        public void TestFailedLampWhenGaugeDoesNotClear()
        {
            var beatmap = createBeatmap(5);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Miss, HitResult.Miss, HitResult.Miss, HitResult.Miss);

            var lamp = BmsClearLampProcessor.Calculate(score, beatmap, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(lamp, Is.EqualTo(BmsClearLamp.Failed));
                Assert.That(finalGauge, Is.LessThan(BmsGaugeProcessor.CLEAR_THRESHOLD));
            });
        }

        [Test]
        public void TestFinalGaugeUsesHitEventOrder()
        {
            var beatmap = createBeatmap(3);
            var frontloadedScore = createScore(beatmap, HitResult.Perfect, HitResult.Perfect, HitResult.Miss);
            var backloadedScore = createScore(beatmap, HitResult.Miss, HitResult.Perfect, HitResult.Perfect);

            var frontloadedLamp = BmsClearLampProcessor.Calculate(frontloadedScore, beatmap, out double frontloadedGauge);
            var backloadedLamp = BmsClearLampProcessor.Calculate(backloadedScore, beatmap, out double backloadedGauge);

            Assert.Multiple(() =>
            {
                Assert.That(frontloadedGauge, Is.EqualTo(BmsGaugeProcessor.SURVIVAL_FLOOR).Within(0.000001));
                Assert.That(backloadedGauge, Is.EqualTo(1).Within(0.000001));
                Assert.That(frontloadedLamp, Is.EqualTo(BmsClearLamp.Failed));
                Assert.That(backloadedLamp, Is.EqualTo(BmsClearLamp.NormalClear));
            });
        }

        [Test]
        public void TestStoredResultDataUsedWhenHitEventsUnavailable()
        {
            var beatmap = createBeatmap(5);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Great, HitResult.Great);

            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);
            score.HitEvents.Clear();

            var available = BmsClearLampProcessor.TryCalculate(score, beatmap, out BmsClearLamp clearLamp, out double finalGauge);

            Assert.Multiple(() =>
            {
                Assert.That(available, Is.True);
                Assert.That(clearLamp, Is.EqualTo(BmsClearLamp.NormalClear));
                Assert.That(finalGauge, Is.GreaterThanOrEqualTo(BmsGaugeProcessor.CLEAR_THRESHOLD));
            });
        }

        [Test]
        public void TestResultDataUnavailableWithoutHitEventsOrStoredState()
        {
            var beatmap = createBeatmap(5);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Great, HitResult.Great);

            score.HitEvents.Clear();

            Assert.That(BmsClearLampProcessor.TryCalculate(score, beatmap, out _, out _), Is.False);
        }

        [Test]
        public void TestPrepareScoreInfoPersistsDefaultLongNoteMode()
        {
            var beatmap = createBeatmap(1);
            var score = createScore(beatmap, HitResult.Perfect);

            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);

            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            Assert.Multiple(() =>
            {
                Assert.That(scoreData, Is.Not.Null);
                Assert.That(scoreData!.GaugeType, Is.EqualTo(BmsGaugeType.Normal));
                Assert.That(scoreData!.LongNoteMode, Is.EqualTo(BmsLongNoteMode.LN));
            });
        }

        [TestCase(BmsGaugeType.AssistEasy)]
        [TestCase(BmsGaugeType.Easy)]
        [TestCase(BmsGaugeType.Hard)]
        [TestCase(BmsGaugeType.ExHard)]
        [TestCase(BmsGaugeType.Hazard)]
        public void TestPrepareScoreInfoPersistsSelectedGaugeTypeFromMods(BmsGaugeType gaugeType)
        {
            var beatmap = createBeatmap(1);
            var score = createScore(beatmap, HitResult.Perfect);

            score.Mods = createGaugeMods(gaugeType);

            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);

            Assert.That(score.GetRulesetData<BmsScoreInfoData>()?.GaugeType, Is.EqualTo(gaugeType));
        }

        [Test]
        public void TestPrepareScoreInfoPersistsGaugeAutoShiftConfiguration()
        {
            var beatmap = createBeatmap(100, total: 800);
            var score = createScore(beatmap,
                Enumerable.Repeat(HitResult.Meh, 10)
                          .Concat(Enumerable.Repeat(HitResult.Meh, 20))
                          .Concat(Enumerable.Repeat(HitResult.Perfect, 70))
                          .ToArray());
            var gasMod = new BmsModGaugeAutoShift();

            gasMod.StartingGauge.Value = BmsGaugeType.ExHard;
            gasMod.FloorGauge.Value = BmsGaugeType.Easy;
            score.Mods = new Mod[] { gasMod };

            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);

            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            Assert.Multiple(() =>
            {
                Assert.That(scoreData, Is.Not.Null);
                Assert.That(scoreData!.UsesGaugeAutoShift, Is.True);
                Assert.That(scoreData.StartingGaugeType, Is.EqualTo(BmsGaugeType.ExHard));
                Assert.That(scoreData.FloorGaugeType, Is.EqualTo(BmsGaugeType.Easy));
                Assert.That(scoreData.GaugeType, Is.EqualTo(BmsGaugeType.Normal));
                Assert.That(scoreData.ClearLamp, Is.EqualTo(BmsClearLamp.NormalClear));
            });
        }

        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestPrepareScoreInfoPersistsSelectedLongNoteModeFromMods(BmsLongNoteMode expectedMode)
        {
            var beatmap = createBeatmap(1);
            var score = createScore(beatmap, HitResult.Perfect);

            score.Mods = new Mod[] { createLongNoteModeMod(expectedMode) };

            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);

            Assert.That(score.GetRulesetData<BmsScoreInfoData>()?.LongNoteMode, Is.EqualTo(expectedMode));
        }

        private static BmsBeatmap createBeatmap(int noteCount, double total = 200)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo
                {
                    Total = total,
                },
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

        private static ScoreInfo createScore(BmsBeatmap beatmap, params HitResult[] results)
        {
            var statistics = new Dictionary<HitResult, int>();
            var hitEvents = new List<HitEvent>();

            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                var hitObject = beatmap.HitObjects[i];

                statistics[result] = statistics.TryGetValue(result, out int count) ? count + 1 : 1;
                hitEvents.Add(new HitEvent(0, 1.0, result, hitObject, i > 0 ? beatmap.HitObjects[i - 1] : null, null));
            }

            return new ScoreInfo
            {
                Statistics = statistics,
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = results.Length,
                },
                HitEvents = hitEvents,
            };
        }

        private static Mod createLongNoteModeMod(BmsLongNoteMode longNoteMode)
            => longNoteMode switch
            {
                BmsLongNoteMode.CN => new BmsModChargeNote(),
                BmsLongNoteMode.HCN => new BmsModHellChargeNote(),
                _ => throw new AssertionException($"Unsupported long note mode test input: {longNoteMode}"),
            };

        private static Mod[] createGaugeMods(BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => new Mod[] { new BmsModGaugeAssistEasy() },
                BmsGaugeType.Easy => new Mod[] { new BmsModGaugeEasy() },
                BmsGaugeType.Normal => Array.Empty<Mod>(),
                BmsGaugeType.Hard => new Mod[] { new BmsModGaugeHard() },
                BmsGaugeType.ExHard => new Mod[] { new BmsModGaugeExHard() },
                BmsGaugeType.Hazard => new Mod[] { new BmsModGaugeHazard() },
                _ => throw new AssertionException($"Unsupported gauge type test input: {gaugeType}"),
            };
    }
}
