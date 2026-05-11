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

            AddAssert("BMS composition control visible", () => filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Count(control => control.IsPresent), () => Is.EqualTo(1));
            AddAssert("BMS key buttons visible", () => filter.ChildrenOfType<FilterControl.BmsKeyCountToggleButton>().Count(button => button.IsPresent), () => Is.EqualTo(4));

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());
            AddAssert("uses BMS criteria", () => criteria.RulesetCriteria, () => Is.TypeOf<BmsFilterCriteria>());
            AddAssert("hidden star filter ignored", () => criteria.UserStarDifficulty.HasFilter, () => Is.False);
            AddAssert("default composition does not filter", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestBmsVisualFiltersWriteIntoRulesetCriteria()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());

            AddStep("limit to 5K", () =>
            {
                foreach (var button in filter.ChildrenOfType<FilterControl.BmsKeyCountToggleButton>())
                    button.Active.Value = button.KeyCount == 5;
            });

            AddStep("set RC maximum", () => compositionControl.RegularSegment.UpperBound.Value = 50);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("5K RC20 matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 3, 5), criteria));
            AddAssert("9K RC20 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(9, 2, 3, 5), criteria));
            AddAssert("5K RC60 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestDisabledCompositionSegmentDoesNotEmitConstraint()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("set RC maximum", () => compositionControl.RegularSegment.UpperBound.Value = 40);
            AddStep("disable RC constraint", () => compositionControl.RegularSegment.Enabled.Value = false);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("RC60 still matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestCompositionUpperBoundsClampToOneHundred()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("set RC maximum", () => compositionControl.RegularSegment.UpperBound.Value = 60);
            AddStep("set LN past remaining space", () => compositionControl.LongNoteSegment.UpperBound.Value = 60);
            AddAssert("LN clamped to remainder", () => compositionControl.LongNoteSegment.UpperBound.Value, () => Is.EqualTo(25).Within(0.01));

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("LN25 beatmap matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 5, 2, 3), criteria));
            AddAssert("LN30 beatmap filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 5, 3, 2), criteria));
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
