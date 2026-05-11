// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsFilterCriteriaTest
    {
        [Test]
        public void TestQueryParserConsumesBmsKeywordsAndMatchesBeatmaps()
        {
            var beatmaps = new[]
            {
                createBeatmap(5, createStats(totalPlayable: 10, regular: 6, longNote: 2, scratch: 2)),
                createBeatmap(7, createStats(totalPlayable: 10, regular: 3, longNote: 6, scratch: 1)),
                createBeatmap(9, createStats(totalPlayable: 10, regular: 5, longNote: 0, scratch: 5)),
            };

            var criteria = new FilterCriteria { RulesetCriteria = new BmsRuleset().CreateRulesetFilterCriteria() };

            FilterQueryParser.ApplyQueries(criteria, "keys<9 rc>=40 scr<30");

            int[] matchingBeatmaps = beatmaps.Select((beatmap, index) => (beatmap, index))
                                           .Where(tuple => BeatmapCarouselFilterMatching.CheckCriteriaMatch(tuple.beatmap, criteria))
                                           .Select(tuple => tuple.index)
                                           .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(criteria.SearchText.Trim(), Is.Empty);
                Assert.That(matchingBeatmaps, Is.EqualTo(new[] { 0 }));
            });
        }

        [Test]
        public void TestMissingFilterStatsDoNotSilentlyFilterBeatmaps()
        {
            var beatmaps = new[]
            {
                createBeatmap(5, createStats(totalPlayable: 10, regular: 6, longNote: 2, scratch: 2)),
                createBeatmap(7, createStats(totalPlayable: 10, regular: 2, longNote: 5, scratch: 3)),
                createBeatmap(9, null),
            };

            var criteria = new FilterCriteria { RulesetCriteria = new BmsRuleset().CreateRulesetFilterCriteria() };

            FilterQueryParser.ApplyQueries(criteria, "regular>=50 scratch<30");

            int[] matchingBeatmaps = beatmaps.Select((beatmap, index) => (beatmap, index))
                                           .Where(tuple => BeatmapCarouselFilterMatching.CheckCriteriaMatch(tuple.beatmap, criteria))
                                           .Select(tuple => tuple.index)
                                           .ToArray();

            Assert.That(matchingBeatmaps, Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void TestKeyOperatorsMatchSupportedBmsVariants()
        {
            var beatmaps = new[]
            {
                createBeatmap(5, createStats(totalPlayable: 10, regular: 5, longNote: 3, scratch: 2)),
                createBeatmap(7, createStats(totalPlayable: 10, regular: 5, longNote: 3, scratch: 2)),
                createBeatmap(9, createStats(totalPlayable: 10, regular: 5, longNote: 3, scratch: 2)),
                createBeatmap(14, createStats(totalPlayable: 10, regular: 5, longNote: 3, scratch: 2)),
            };

            var criteria = new FilterCriteria { RulesetCriteria = new BmsRuleset().CreateRulesetFilterCriteria() };

            FilterQueryParser.ApplyQueries(criteria, "keys!=7 key<=9");

            int[] matchingBeatmaps = beatmaps.Select((beatmap, index) => (beatmap, index))
                                           .Where(tuple => BeatmapCarouselFilterMatching.CheckCriteriaMatch(tuple.beatmap, criteria))
                                           .Select(tuple => tuple.index)
                                           .ToArray();

            Assert.That(matchingBeatmaps, Is.EqualTo(new[] { 0, 2 }));
        }

        private static BeatmapInfo createBeatmap(int keyCount, BmsChartFilterStats? filterStats)
        {
            var metadata = new BeatmapMetadata();

            metadata.SetChartFilterStats(filterStats);

            return new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty { CircleSize = keyCount }, metadata);
        }

        private static BmsChartFilterStats createStats(int totalPlayable, int regular, int longNote, int scratch)
            => new BmsChartFilterStats
            {
                TotalPlayableObjectCount = totalPlayable,
                RegularNoteCount = regular,
                LongNoteCount = longNote,
                ScratchNoteCount = scratch,
            };
    }
}
