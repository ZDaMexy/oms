// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Colour;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsRulesetStatisticsTest
    {
        [Test]
        public void TestCreateStatisticsIncludesSkinnableResultsSummary()
        {
            var beatmap = createBeatmap(3);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good);

            var statistics = new BmsRuleset().CreateStatisticsForScore(score, beatmap);
            var summaryStatistic = statistics.SingleOrDefault(statistic => !statistic.RequiresHitEvents);

            Assert.Multiple(() =>
            {
                Assert.That(summaryStatistic, Is.Not.Null);
                Assert.That(summaryStatistic!.Name.ToString(), Is.Empty);
                Assert.That(summaryStatistic!.RequiresHitEvents, Is.False);
                Assert.That(summaryStatistic.CreateContent(), Is.TypeOf<SkinnableBmsResultsSummaryPanelDisplay>());
            });
        }

        [Test]
        public void TestCreateStatisticsIncludesGaugeHistoryGraph()
        {
            var beatmap = createBeatmap(3);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good);

            var statistics = new BmsRuleset().CreateStatisticsForScore(score, beatmap);
            var gaugeHistoryStatistic = statistics.SingleOrDefault(statistic => statistic.RequiresHitEvents);

            Assert.Multiple(() =>
            {
                Assert.That(gaugeHistoryStatistic, Is.Not.Null);
                Assert.That(gaugeHistoryStatistic!.Name.ToString(), Is.Empty);
                Assert.That(gaugeHistoryStatistic!.RequiresHitEvents, Is.True);
                Assert.That(gaugeHistoryStatistic.CreateContent(), Is.TypeOf<SkinnableBmsGaugeHistoryPanelDisplay>());
            });
        }

        [Test]
        public void TestBeatmapAttributesDisplayUsesPersistedJudgeRank()
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty
            {
                CircleSize = 8,
            }, new BeatmapMetadata
            {
                Title = "Judge Rank Test",
                Artist = "Artist",
            });

            beatmap.Metadata.SetChartMetadata(new BmsChartMetadata { JudgeRank = 1 });

            var displayedAttributes = ruleset.GetBeatmapAttributesForDisplay(beatmap, new List<Mod>()).ToArray();
            var accuracyAttribute = displayedAttributes.Last();

            Assert.Multiple(() =>
            {
                Assert.That(accuracyAttribute.DisplayValue, Is.EqualTo("HARD"));
                Assert.That(accuracyAttribute.OriginalValue, Is.EqualTo(3));
                Assert.That(accuracyAttribute.AdditionalMetrics[0].Name.ToString(), Is.EqualTo("Judge system"));
                Assert.That(accuracyAttribute.AdditionalMetrics[0].Value.ToString(), Is.EqualTo("OD"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Name.ToString(), Is.EqualTo("Judge difficulty"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Value.ToString(), Is.EqualTo("HARD (#RANK 1)"));
                Assert.That(accuracyAttribute.AdditionalMetrics.Any(metric => metric.Name.ToString() == "PGREAT hit window"), Is.True);
            });
        }

        [Test]
        public void TestBeatmapAttributesDisplayUsesJudgeModeAndRankMods()
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty
            {
                CircleSize = 8,
            }, new BeatmapMetadata
            {
                Title = "Judge Override Test",
                Artist = "Artist",
            });

            beatmap.Metadata.SetChartMetadata(new BmsChartMetadata { JudgeRank = 1 });

            var judgeRankMod = new BmsModJudgeRank();
            judgeRankMod.JudgeRank.Value = BmsJudgeRank.VeryHard;

            var accuracyAttribute = ruleset.GetBeatmapAttributesForDisplay(beatmap, new Mod[]
            {
                new BmsModJudgeBeatoraja(),
                judgeRankMod,
            }).Last();

            Assert.Multiple(() =>
            {
                Assert.That(accuracyAttribute.DisplayValue, Is.EqualTo("VERY HARD"));
                Assert.That(accuracyAttribute.AdditionalMetrics[0].Value.ToString(), Is.EqualTo("BEATORAJA"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Name.ToString(), Is.EqualTo("Chart #RANK"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Value.ToString(), Is.EqualTo("1 (HARD)"));
                Assert.That(accuracyAttribute.AdditionalMetrics[2].Name.ToString(), Is.EqualTo("Applied difficulty"));
                Assert.That(accuracyAttribute.AdditionalMetrics[2].Value.ToString(), Is.EqualTo("VERY HARD"));
                Assert.That(accuracyAttribute.AdditionalMetrics.Any(metric => metric.Name.ToString() == "BAD hit window" && metric.Value.ToString() == "-55 / +70 ms"), Is.True);
                Assert.That(accuracyAttribute.AdditionalMetrics.Any(metric => metric.Name.ToString() == "EPOOR window" && metric.Value.ToString() == "-500 / +150 ms"), Is.True);
            });
        }

        [Test]
        public void TestBeatmapAttributesDisplayUsesFixedIidxJudgeProfile()
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty
            {
                CircleSize = 8,
            }, new BeatmapMetadata
            {
                Title = "IIDX Judge Test",
                Artist = "Artist",
            });

            beatmap.Metadata.SetChartMetadata(new BmsChartMetadata { JudgeRank = 1 });

            var judgeRankMod = new BmsModJudgeRank();
            judgeRankMod.JudgeRank.Value = BmsJudgeRank.VeryHard;

            var accuracyAttribute = ruleset.GetBeatmapAttributesForDisplay(beatmap, new Mod[]
            {
                new BmsModJudgeIidx(),
                judgeRankMod,
            }).Last();

            Assert.Multiple(() =>
            {
                Assert.That(accuracyAttribute.DisplayValue, Is.EqualTo("FIXED"));
                Assert.That(accuracyAttribute.Description.ToString(), Is.EqualTo("Uses the current OMS fixed IIDX timing preset. Chart #RANK and Judge Difficulty overrides do not apply."));
                Assert.That(accuracyAttribute.AdditionalMetrics[0].Value.ToString(), Is.EqualTo("IIDX"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Name.ToString(), Is.EqualTo("Chart #RANK"));
                Assert.That(accuracyAttribute.AdditionalMetrics[1].Value.ToString(), Is.EqualTo("1 (HARD)"));
                Assert.That(accuracyAttribute.AdditionalMetrics[2].Name.ToString(), Is.EqualTo("Applied difficulty"));
                Assert.That(accuracyAttribute.AdditionalMetrics[2].Value.ToString(), Is.EqualTo("FIXED (IIDX)"));
                Assert.That(accuracyAttribute.AdditionalMetrics.Any(metric => metric.Name.ToString() == "BAD hit window" && metric.Value.ToString() == "±250 ms"), Is.True);
                Assert.That(accuracyAttribute.AdditionalMetrics.Any(metric => metric.Name.ToString() == "EPOOR window" && metric.Value.ToString() == "-500 / +150 ms"), Is.True);
            });
        }

        [Test]
        public void TestRulesetDisplaysIidxStyleHitResultOrder()
        {
            var displayedResults = new BmsRuleset().GetHitResultsForDisplay().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(displayedResults.Select(result => result.result), Is.EqualTo(new[]
                {
                    HitResult.Perfect,
                    HitResult.Great,
                    HitResult.Good,
                    HitResult.Meh,
                    HitResult.Miss,
                    HitResult.Ok,
                    HitResult.ComboBreak,
                }));
                Assert.That(displayedResults.Select(result => result.displayName.ToString()), Is.EqualTo(new[]
                {
                    "PGREAT",
                    "GREAT",
                    "GOOD",
                    "BAD",
                    "POOR",
                    "EPOOR",
                    "COMBO BREAK",
                }));
            });
        }

        [Test]
        public void TestScoreStatisticsForDisplayUsesSeparatedEmptyPoorAndComboBreakRows()
        {
            var score = new ScoreInfo
            {
                Ruleset = new BmsRuleset().RulesetInfo,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 232,
                    [HitResult.Great] = 598,
                    [HitResult.Good] = 198,
                    [HitResult.Meh] = 14,
                    [HitResult.Miss] = 37,
                    [HitResult.Ok] = 12,
                    [HitResult.ComboBreak] = 5,
                },
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 1097,
                },
            };

            var displayedStatistics = score.GetStatisticsForDisplay().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(displayedStatistics.Select(stat => stat.Result), Is.EqualTo(new[]
                {
                    HitResult.Perfect,
                    HitResult.Great,
                    HitResult.Good,
                    HitResult.Meh,
                    HitResult.Miss,
                    HitResult.Ok,
                    HitResult.ComboBreak,
                }));
                Assert.That(displayedStatistics.Select(stat => stat.DisplayName.ToString()), Is.EqualTo(new[]
                {
                    "PGREAT",
                    "GREAT",
                    "GOOD",
                    "BAD",
                    "POOR",
                    "EPOOR",
                    "COMBO BREAK",
                }));
                Assert.That(displayedStatistics[^2].Count, Is.EqualTo(12));
                Assert.That(displayedStatistics[^1].Count, Is.EqualTo(5));
            });
        }

        [Test]
        public void TestLegacyScoreStatisticsMapHistoricalEmptyPoorIntoEpPoorRow()
        {
            var score = new ScoreInfo
            {
                Ruleset = new BmsRuleset().RulesetInfo,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 232,
                    [HitResult.Great] = 598,
                    [HitResult.Good] = 198,
                    [HitResult.Meh] = 14,
                    [HitResult.Miss] = 37,
                    [HitResult.ComboBreak] = 12,
                },
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 1097,
                },
            };

            score.SetRulesetData(new BmsScoreInfoData
            {
                Version = 5,
            });

            var displayedStatistics = score.GetStatisticsForDisplay().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(displayedStatistics.Single(stat => stat.Result == HitResult.Ok).Count, Is.EqualTo(12));
                Assert.That(displayedStatistics.Single(stat => stat.Result == HitResult.ComboBreak).Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestSongSelectPanelAccentUsesPersistedClearLamp()
        {
            var ruleset = new BmsRuleset();
            var score = new ScoreInfo();

            score.SetRulesetData(new BmsScoreInfoData
            {
                ClearLamp = BmsClearLamp.EasyClear,
                FinalGauge = 0.84,
            });

            var accent = ruleset.GetSongSelectPanelAccent(score);

            Assert.Multiple(() =>
            {
                Assert.That(accent, Is.Not.Null);
                Assert.That(accent!.Value.AccentColour, Is.EqualTo(ColourInfo.GradientVertical(new Color4(72, 204, 108, 255), new Color4(72, 204, 108, 255))));
                Assert.That(accent.Value.ForegroundColour, Is.EqualTo(new Color4(249, 252, 255, 255)));
            });
        }

        [Test]
        public void TestSongSelectPanelAccentUsesStaticRainbowForFullCombo()
        {
            var ruleset = new BmsRuleset();
            var score = new ScoreInfo();

            score.SetRulesetData(new BmsScoreInfoData
            {
                ClearLamp = BmsClearLamp.FullCombo,
                FinalGauge = 1,
            });

            var accent = ruleset.GetSongSelectPanelAccent(score);

            Assert.Multiple(() =>
            {
                Assert.That(accent, Is.Not.Null);
                Assert.That(accent!.Value.AccentColour, Is.EqualTo(ColourInfo.GradientVertical(new Color4(255, 118, 214, 255), new Color4(102, 226, 255, 255))));
                Assert.That(accent.Value.ForegroundColour, Is.EqualTo(new Color4(249, 252, 255, 255)));
            });
        }

        [Test]
        public void TestSongSelectPanelAccentReturnsNullWithoutPersistedResult()
        {
            var accent = new BmsRuleset().GetSongSelectPanelAccent(new ScoreInfo());

            Assert.That(accent, Is.Null);
        }

        [Test]
        public void TestResultsDisplaysUseDjLevelComponents()
        {
            var beatmap = createBeatmap(4);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Good);
            var ruleset = new BmsRuleset();

            Assert.Multiple(() =>
            {
                Assert.That(ruleset.CreateResultsAccuracyDisplay(score), Is.TypeOf<BmsResultsAccuracyDisplay>());
                Assert.That(ruleset.CreateResultsRankBadge(score), Is.TypeOf<BmsDrawableDjLevel>());
            });
        }

        [Test]
        public void TestResultsScoreLabelUsesExScore()
        {
            var beatmap = createBeatmap(3);
            var score = createScore(beatmap, HitResult.Perfect, HitResult.Great, HitResult.Good);

            Assert.That(new BmsRuleset().GetResultsScoreLabel(score)?.ToString(), Is.EqualTo("EX-SCORE"));
        }

        [TestCase(9, 0, 0, BmsDjLevel.AAA, 18)]
        [TestCase(7, 2, 0, BmsDjLevel.AAA, 16)]
        [TestCase(5, 4, 0, BmsDjLevel.AA, 14)]
        [TestCase(2, 0, 7, BmsDjLevel.E, 4)]
        public void TestDjLevelDisplayInfoUsesExScoreThresholds(int perfectCount, int greatCount, int missCount, BmsDjLevel expectedLevel, long expectedExScore)
        {
            var beatmap = createBeatmap(perfectCount + greatCount + missCount);
            var score = createScore(
                beatmap,
                Enumerable.Repeat(HitResult.Perfect, perfectCount)
                          .Concat(Enumerable.Repeat(HitResult.Great, greatCount))
                          .Concat(Enumerable.Repeat(HitResult.Miss, missCount))
                          .ToArray());

            var displayInfo = BmsDjLevelDisplayInfo.FromScore(score);

            Assert.Multiple(() =>
            {
                Assert.That(displayInfo.Level, Is.EqualTo(expectedLevel));
                Assert.That(displayInfo.ExScore, Is.EqualTo(expectedExScore));
                Assert.That(displayInfo.MaxExScore, Is.EqualTo((perfectCount + greatCount + missCount) * 2L));
            });
        }

        private static BmsBeatmap createBeatmap(int noteCount)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo(),
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
    }
}
