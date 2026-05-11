// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsFilterControl : BmsSongSelectTestScene
    {
        private FilterControl filter => SongSelectScreen.ChildrenOfType<FilterControl>().Single();

        [Test]
        public void TestBmsRulesetSwapsOutSharedStarSlider()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            AddStep("set hidden star filter", () => Config.SetValue(OsuSetting.DisplayStarsMinimum, 10.0));

            AddAssert("BMS composition sliders visible", () => filter.ChildrenOfType<FilterControl.BmsCompositionRangeSlider>().Count(slider => slider.IsPresent), () => Is.EqualTo(3));
            AddAssert("BMS key buttons visible", () => filter.ChildrenOfType<FilterControl.BmsKeyCountToggleButton>().Count(button => button.IsPresent), () => Is.EqualTo(4));

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());
            AddAssert("uses BMS criteria", () => criteria.RulesetCriteria, () => Is.TypeOf<BmsFilterCriteria>());
            AddAssert("hidden star filter ignored", () => criteria.UserStarDifficulty.HasFilter, () => Is.False);
        }

        [Test]
        public void TestBmsVisualFiltersWriteIntoRulesetCriteria()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            AddStep("limit to 5K", () =>
            {
                foreach (var button in filter.ChildrenOfType<FilterControl.BmsKeyCountToggleButton>())
                    button.Active.Value = button.KeyCount == 5;
            });

            AddStep("set RC minimum", () => filter.ChildrenOfType<FilterControl.BmsCompositionRangeSlider>().Single(slider => slider.CompositionKey == "rc").LowerBound.Value = 50);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("5K RC60 matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
            AddAssert("9K RC60 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(9, 6, 2, 2), criteria));
            AddAssert("5K RC20 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 3, 5), criteria));
        }

        private static BeatmapInfo createBeatmap(int keyCount, int regular, int longNote, int scratch)
        {
            var metadata = new BeatmapMetadata();

            metadata.SetChartFilterStats(new BmsChartFilterStats
            {
                TotalPlayableObjectCount = regular + longNote + scratch,
                RegularNoteCount = regular,
                LongNoteCount = longNote,
                ScratchNoteCount = scratch,
            });

            return new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty { CircleSize = keyCount }, metadata);
        }
    }
}
