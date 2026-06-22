// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Graphics.Carousel;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaDifficultyTableGroupingTest
    {
        private const string satellite_entry_json =
            "{\"difficulty_table_entries\":[{\"TableName\":\"Satellite\",\"Symbol\":\"sl\",\"Level\":4,\"LevelLabel\":\"sl4\",\"Md5\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"TableSortOrder\":0}]}";

        [Test]
        public void TestManiaExposesDifficultyTableGrouping()
        {
            Assert.That(new ManiaRuleset().GetAvailableSongSelectGroupModes(), Contains.Item(GroupMode.DifficultyTable));
            Assert.That(new ManiaRuleset().IsSongSelectGroupingHierarchical(GroupMode.DifficultyTable), Is.True);
        }

        [Test]
        public async Task TestDifficultyTableGroupingShowsOnlyBmsConvertsAndDropsNativeMania()
        {
            var convert = bmsConvert("Convert", satellite_entry_json);
            var native = nativeMania("Native");

            var results = await runGrouping(native, convert).ConfigureAwait(false);

            var groupedBeatmaps = results.Select(item => item.Model).OfType<GroupedBeatmap>().ToArray();

            Assert.Multiple(() =>
            {
                // Native mania is excluded entirely from the BMS difficulty-table grouping.
                Assert.That(groupedBeatmaps.Any(grouped => grouped.Beatmap == native), Is.False);

                // The BMS convert is present, grouped under its table → level.
                var convertGrouped = groupedBeatmaps.Single(grouped => grouped.Beatmap == convert);
                Assert.That(convertGrouped.Group!.Title.ToString(), Is.EqualTo("sl4"));
                Assert.That(convertGrouped.Group!.Parent!.Title.ToString(), Is.EqualTo("Satellite"));
            });
        }

        [Test]
        public async Task TestUnindexedBmsConvertFallsIntoUnrated()
        {
            var convert = bmsConvert("Unindexed", "{\"chart_metadata\":{\"play_level\":\"5\"}}");

            var results = await runGrouping(convert).ConfigureAwait(false);

            var convertGrouped = results.Select(item => item.Model).OfType<GroupedBeatmap>().Single(grouped => grouped.Beatmap == convert);
            Assert.That(convertGrouped.Group!.Title.ToString(), Is.EqualTo("Unrated"));
        }

        private static async Task<List<CarouselItem>> runGrouping(params BeatmapInfo[] beatmaps)
        {
            var criteria = new FilterCriteria
            {
                Group = GroupMode.DifficultyTable,
                Sort = SortMode.Difficulty,
                Ruleset = new ManiaRuleset().RulesetInfo,
            };

            var sortingFilter = new BeatmapCarouselFilterSorting(() => criteria);
            var sortedItems = await sortingFilter.Run(beatmaps.Select(beatmap => new CarouselItem(beatmap)).ToList(), CancellationToken.None).ConfigureAwait(false);

            var groupingFilter = new BeatmapCarouselFilterGrouping
            {
                GetCriteria = () => criteria,
                GetCollections = () => new List<BeatmapCollection>(),
                GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                GetFavouriteBeatmapSets = () => new HashSet<int>(),
            };

            return await groupingFilter.Run(sortedItems, CancellationToken.None).ConfigureAwait(false);
        }

        private static BeatmapInfo bmsConvert(string title, string rulesetDataJson)
            => createBeatmap(new RulesetInfo { ShortName = "bms", OnlineID = -1 }, title, rulesetDataJson);

        private static BeatmapInfo nativeMania(string title)
            => createBeatmap(new ManiaRuleset().RulesetInfo, title, string.Empty);

        private static BeatmapInfo createBeatmap(RulesetInfo ruleset, string title, string rulesetDataJson)
        {
            var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata
            {
                Title = title,
                Artist = title,
                RulesetDataJson = rulesetDataJson,
            })
            {
                DifficultyName = title,
                StarRating = 5.0,
                MD5Hash = $"{title}-{Guid.NewGuid():N}".ToLowerInvariant(),
            };

            var beatmapSet = new BeatmapSetInfo();
            beatmapSet.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = beatmapSet;

            return beatmap;
        }
    }
}
