// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsDisplayLevelGroupingTest
    {
        private readonly RulesetInfo bms = new BmsRuleset().RulesetInfo;

        private FilterCriteria criteria(GroupMode group, SortMode sort, DisplayLevel? displayLevel) => new FilterCriteria
        {
            Ruleset = bms,
            Group = group,
            Sort = sort,
            DisplayLevel = displayLevel,
        };

        [Test]
        public void TestExplicitDisplayLevelDrivesGroupingForFreeGroupings()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Artist, SortMode.Title, DisplayLevel.Songs)), Is.True);
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Artist, SortMode.Title, DisplayLevel.Difficulties)), Is.False);
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Title, SortMode.Title, DisplayLevel.Songs)), Is.True);
            });
        }

        [Test]
        public void TestForcedStandaloneGroupingsIgnoreDisplayLevel()
        {
            Assert.Multiple(() =>
            {
                // hierarchical (difficulty table) is always standalone
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.DifficultyTable, SortMode.Title, DisplayLevel.Songs)), Is.False);
                // sort by difficulty
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Artist, SortMode.Difficulty, DisplayLevel.Songs)), Is.False);
                // group by difficulty
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Difficulty, SortMode.Title, DisplayLevel.Songs)), Is.False);
                // rank achieved
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.RankAchieved, SortMode.Title, DisplayLevel.Songs)), Is.False);
            });
        }

        [Test]
        public void TestNullDisplayLevelPreservesDefaultHeuristic()
        {
            Assert.Multiple(() =>
            {
                // null (non-BMS path) keeps the default heuristic: free groupings group together
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.Artist, SortMode.Title, null)), Is.True);
                // null still forces standalone for incoherent groupings
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria(GroupMode.DifficultyTable, SortMode.Title, null)), Is.False);

                Assert.That(BeatmapCarouselFilterGrouping.GroupingForcesStandaloneDifficulties(criteria(GroupMode.Artist, SortMode.Title, null)), Is.False);
                Assert.That(BeatmapCarouselFilterGrouping.GroupingForcesStandaloneDifficulties(criteria(GroupMode.DifficultyTable, SortMode.Title, null)), Is.True);
            });
        }
    }
}
