// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public class BmsConvertedDifficultyTableGroupingTest
    {
        private const string single_entry_json =
            "{\"difficulty_table_entries\":[{\"TableName\":\"Satellite\",\"Symbol\":\"sl\",\"Level\":4,\"LevelLabel\":\"sl4\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":0}]}";

        [Test]
        public void TestBmsChartBuildsTableLevelTree()
        {
            var groups = BmsConvertedDifficultyTableGrouping.ComputeGroupDefinitions(bmsBeatmap(single_entry_json));

            Assert.That(groups, Has.Length.EqualTo(1));
            Assert.That(groups[0].Title.ToString(), Is.EqualTo("sl4"));
            Assert.That(groups[0].Parent, Is.Not.Null);
            Assert.That(groups[0].Parent!.Title.ToString(), Is.EqualTo("Satellite"));
        }

        [Test]
        public void TestBmsChartWithoutEntriesProducesUnrated()
        {
            var groups = BmsConvertedDifficultyTableGrouping.ComputeGroupDefinitions(bmsBeatmap("{\"chart_metadata\":{\"play_level\":\"5\"}}"));

            Assert.That(groups, Has.Length.EqualTo(1));
            Assert.That(groups[0].Title.ToString(), Is.EqualTo("Unrated"));
            Assert.That(groups[0].Parent, Is.Null);
        }

        [Test]
        public void TestNonBmsChartIsExcluded()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "mania" }) { Metadata = new BeatmapMetadata() };
            Assert.That(BmsConvertedDifficultyTableGrouping.ComputeGroupDefinitions(beatmap), Is.Empty);
        }

        [Test]
        public void TestNonBmsChartWithEntriesIsStillExcluded()
        {
            // Defensive: difficulty-table entries on a non-BMS chart must not pull it into the grouping
            // (the grouping is meant to show only BMS converts).
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "mania" })
            {
                Metadata = new BeatmapMetadata { RulesetDataJson = single_entry_json }
            };
            Assert.That(BmsConvertedDifficultyTableGrouping.ComputeGroupDefinitions(beatmap), Is.Empty);
        }

        private static BeatmapInfo bmsBeatmap(string rulesetDataJson) => new BeatmapInfo(new RulesetInfo { ShortName = "bms" })
        {
            Metadata = new BeatmapMetadata { RulesetDataJson = rulesetDataJson }
        };
    }
}
