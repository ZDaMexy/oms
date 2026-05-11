// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBeatmapStatisticsTest
    {
        [Test]
        public void TestStatisticsDisplayExclusiveNormalLongNoteAndScratchCounts()
        {
            var beatmap = new BmsBeatmap();

            beatmap.HitObjects.AddRange(new HitObject[]
            {
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 100, LaneIndex = 2 },
                new BmsHoldNote { StartTime = 200, EndTime = 600, LaneIndex = 3 },
                new BmsHitObject { StartTime = 700, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 800, EndTime = 1200, LaneIndex = 0, IsScratch = true },
            });

            var statistics = beatmap.GetStatistics().ToArray();
            var filterStats = BmsChartFilterStats.FromBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(statistics.Select(statistic => statistic.Name.ToString()), Is.EqualTo(new[] { "Notes", "Hold Notes", "Spinners" }));
                Assert.That(statistics.Select(statistic => statistic.Content), Is.EqualTo(new[] { "2 (40.0%)", "1 (20.0%)", "2 (40.0%)" }));
                Assert.That(statistics.Select(statistic => statistic.BarDisplayLength), Is.EqualTo(new float?[] { 0.4f, 0.2f, 0.4f }));
                Assert.That(filterStats.TotalPlayableObjectCount, Is.EqualTo(5));
                Assert.That(filterStats.RegularNoteCount, Is.EqualTo(2));
                Assert.That(filterStats.LongNoteCount, Is.EqualTo(1));
                Assert.That(filterStats.ScratchNoteCount, Is.EqualTo(2));
                Assert.That(filterStats.RegularNotePercentage, Is.EqualTo(40).Within(0.001));
                Assert.That(filterStats.LongNotePercentage, Is.EqualTo(20).Within(0.001));
                Assert.That(filterStats.ScratchNotePercentage, Is.EqualTo(40).Within(0.001));
            });
        }
    }
}
