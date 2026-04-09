// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

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
