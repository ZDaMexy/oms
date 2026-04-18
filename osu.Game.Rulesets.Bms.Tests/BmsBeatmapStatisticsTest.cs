// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
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

            Assert.Multiple(() =>
            {
                Assert.That(statistics.Select(statistic => statistic.Name.ToString()), Is.EqualTo(new[] { "Notes", "Hold Notes", "Spinners" }));
                Assert.That(statistics.Select(statistic => statistic.Content), Is.EqualTo(new[] { "2 (40.0%)", "1 (20.0%)", "2 (40.0%)" }));
                Assert.That(statistics.Select(statistic => statistic.BarDisplayLength), Is.EqualTo(new float?[] { 0.4f, 0.2f, 0.4f }));
            });
        }
    }
}
