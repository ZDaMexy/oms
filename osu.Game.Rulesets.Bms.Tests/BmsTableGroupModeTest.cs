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
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsTableGroupModeTest
    {
        private readonly RulesetInfo rulesetInfo = new BmsRuleset().RulesetInfo;

        [Test]
        public async Task TestDifficultyTableGroupingBuildsTableLevelHierarchyAndLeavesUnratedLast()
        {
            var satelliteLevelOne = createBeatmap("Satellite Level 1", 1.4,
                new BmsDifficultyTableEntry("Satellite", "★", 1, "★1", "a", 0));
            var satelliteLevelTwo = createBeatmap("Satellite Level 2", 2.7,
                new BmsDifficultyTableEntry("Satellite", "★", 2, "★2", "b", 0));
            var stellaLevelOne = createBeatmap("Stella Level 1", 1.9,
                new BmsDifficultyTableEntry("Stella", "☆", 1, "☆1", "c", 1));
            var unrated = createBeatmap("Unrated", 0.8);

            var results = await runGrouping(unrated, stellaLevelOne, satelliteLevelTwo, satelliteLevelOne).ConfigureAwait(false);

            var groups = results.Select(item => item.Model).OfType<GroupDefinition>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(groups.Select(group => group.Title.ToString()), Is.EqualTo(new[] { "Satellite", "★1", "★2", "Stella", "☆1", "Unrated" }));
                Assert.That(groups.Select(group => group.Depth), Is.EqualTo(new[] { 0, 1, 1, 0, 1, 0 }));
                Assert.That(groups[1].Parent, Is.EqualTo(groups[0]));
                Assert.That(groups[2].Parent, Is.EqualTo(groups[0]));
                Assert.That(groups[4].Parent, Is.EqualTo(groups[3]));
                Assert.That(groups[5].Parent, Is.Null);
                Assert.That(results.Select(item => item.Model).OfType<GroupedBeatmapSet>().Count(), Is.EqualTo(4));
            });

            var groupedBeatmaps = results.Select(item => item.Model).OfType<GroupedBeatmap>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == satelliteLevelOne).Group!.Title.ToString(), Is.EqualTo("★1"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == satelliteLevelOne).Group!.Parent!.Title.ToString(), Is.EqualTo("Satellite"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == satelliteLevelTwo).Group!.Title.ToString(), Is.EqualTo("★2"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == stellaLevelOne).Group!.Parent!.Title.ToString(), Is.EqualTo("Stella"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == unrated).Group!.Title.ToString(), Is.EqualTo("Unrated"));
            });
        }

        [Test]
        public async Task TestDifficultyTableGroupingDuplicatesChartsAcrossTables()
        {
            var sharedBeatmap = createBeatmap("Shared", 3.5,
                new BmsDifficultyTableEntry("Satellite", "★", 1, "★1", "shared", 0),
                new BmsDifficultyTableEntry("Stella", "☆", 3, "☆3", "shared", 1));

            var results = await runGrouping(sharedBeatmap).ConfigureAwait(false);
            var groupedCopies = results.Select(item => item.Model).OfType<GroupedBeatmap>().Where(grouped => grouped.Beatmap == sharedBeatmap).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(groupedCopies.Length, Is.EqualTo(2));
                Assert.That(groupedCopies.Select(copy => $"{copy.Group!.Parent!.Title}/{copy.Group.Title}"), Is.EquivalentTo(new[] { "Satellite/★1", "Stella/☆3" }));
                Assert.That(results.Select(item => item.Model).OfType<GroupedBeatmapSet>().Count(), Is.EqualTo(2));
            });
        }

        private async Task<List<CarouselItem>> runGrouping(params BeatmapInfo[] beatmaps)
        {
            var criteria = new FilterCriteria
            {
                Group = GroupMode.DifficultyTable,
                Sort = SortMode.Difficulty,
                Ruleset = rulesetInfo,
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

        private BeatmapInfo createBeatmap(string title, double starRating, params BmsDifficultyTableEntry[] tableEntries)
        {
            var beatmap = new BeatmapInfo(rulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata
            {
                Title = title,
                Artist = title,
            })
            {
                DifficultyName = title,
                StarRating = starRating,
                MD5Hash = $"{title}-{Guid.NewGuid():N}".ToLowerInvariant(),
            };

            beatmap.Metadata.SetDifficultyTableEntries(tableEntries);

            var beatmapSet = new BeatmapSetInfo();
            beatmapSet.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = beatmapSet;

            return beatmap;
        }
    }
}
