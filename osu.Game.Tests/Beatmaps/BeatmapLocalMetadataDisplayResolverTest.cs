// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class BeatmapLocalMetadataDisplayResolverTest
    {
        [Test]
        public void TestDisplayCreatorUsesPersistedBmsChartMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Example Artist",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Test Charter\"}}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("Test Charter"));
        }

        [Test]
        public void TestDisplayArtistStripsEmbeddedBmsCreator()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Test Artist /obj: Test Charter",
                    ArtistUnicode = "表示名 /obj: Test Charter"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmap), Is.EqualTo("Test Artist"));
            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtistUnicode(beatmap), Is.EqualTo("表示名"));
        }

        [Test]
        public void TestDifficultyTableClassificationSingleTable()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson =
                        "{\"difficulty_table_entries\":[{\"TableName\":\"Satellite\",\"Symbol\":\"sl\",\"Level\":4,\"LevelLabel\":\"sl4\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":0}]}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification(beatmap), Is.EqualTo("sl4"));
        }

        [Test]
        public void TestDifficultyTableClassificationMultipleTablesOrderedBySortOrder()
        {
            // The chart is indexed by two tables; the displayed order follows the song-select difficulty-table order
            // (TableSortOrder). 発狂 has the lower sort order here, so it leads: "★8/sl4".
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson =
                        "{\"difficulty_table_entries\":["
                        + "{\"TableName\":\"Satellite\",\"Symbol\":\"sl\",\"Level\":4,\"LevelLabel\":\"sl4\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":1},"
                        + "{\"TableName\":\"発狂BMS難易度表\",\"Symbol\":\"★\",\"Level\":8,\"LevelLabel\":\"★8\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":0}"
                        + "]}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification(beatmap), Is.EqualTo("★8/sl4"));
        }

        [Test]
        public void TestDifficultyTableClassificationEmptyWhenUnindexed()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson = "{\"chart_metadata\":{\"play_level\":\"★12\"}}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification(beatmap), Is.Empty);
        }

        [Test]
        public void TestDifficultyTableClassificationEmptyForNonBmsBeatmap()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "mania" })
            {
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson =
                        "{\"difficulty_table_entries\":[{\"TableName\":\"Satellite\",\"Symbol\":\"sl\",\"Level\":4,\"LevelLabel\":\"sl4\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":0}]}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification(beatmap), Is.Empty);
        }

        [Test]
        public void TestDisplayGenreAndMapperTagsUsePersistedBmsChartMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Tags = "Hardcore",
                    RulesetDataJson = "{\"chart_metadata\":{\"genre\":\"Hardcore\"}}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("Hardcore"));
            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
        }
    }
}
