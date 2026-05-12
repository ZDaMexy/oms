// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Input;

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
            AddAssert("composition rows disabled by default", () => filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single().Rows.All(row => !row.Enabled.Value));

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

            AddStep("enable RC filter", () => compositionControl.RegularRow.Enabled.Value = true);
            AddStep("set RC maximum", () => compositionControl.RegularRow.UpperBound.Value = 50);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("5K RC20 matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 3, 5), criteria));
            AddAssert("9K RC20 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(9, 2, 3, 5), criteria));
            AddAssert("5K RC60 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestRangeFilterAppliesBothBounds()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());

            AddStep("enable RC filter", () => compositionControl.RegularRow.Enabled.Value = true);
            AddStep("set RC lower bound", () => compositionControl.RegularRow.LowerBound.Value = 30);
            AddStep("set RC upper bound", () => compositionControl.RegularRow.UpperBound.Value = 70);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("RC20 filtered (below lower)", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 5, 3), criteria));
            AddAssert("RC50 matches (within range)", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 5, 3, 2), criteria));
            AddAssert("RC80 filtered (above upper)", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 8, 1, 1), criteria));
        }

        [Test]
        public void TestDisabledRowDoesNotEmitConstraint()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("enable RC filter", () => compositionControl.RegularRow.Enabled.Value = true);
            AddStep("set RC maximum", () => compositionControl.RegularRow.UpperBound.Value = 40);
            AddStep("disable RC constraint", () => compositionControl.RegularRow.Enabled.Value = false);

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("RC60 still matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestMaxHandleDragUpdatesUpperBound()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;
            double initialUpperBound = 0;

            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("record initial SCR upper bound", () => initialUpperBound = compositionControl.ScratchRow.UpperBound.Value);
            AddStep("move mouse to SCR max handle", () => InputManager.MoveMouseTo(compositionControl.GetMaxHandleDrawable(compositionControl.ScratchRow)));
            AddStep("drag max handle left", () =>
            {
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(compositionControl.GetTrackScreenSpacePosition(compositionControl.ScratchRow, 0.6f) + new Vector2(0, 1));
            });
            AddStep("release mouse", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("SCR upper bound decreased", () => compositionControl.ScratchRow.UpperBound.Value < initialUpperBound - 1);
            AddAssert("SCR row enabled by drag", () => compositionControl.ScratchRow.Enabled.Value);
        }

        [Test]
        public void TestMinHandleDragUpdatesLowerBound()
        {
            SelectBmsRuleset();
            LoadSongSelect();

            FilterControl.BmsCompositionFilterControl compositionControl = null!;

            AddStep("get composition control", () => compositionControl = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("move mouse to RC min handle", () => InputManager.MoveMouseTo(compositionControl.GetMinHandleDrawable(compositionControl.RegularRow)));
            AddStep("drag min handle right", () =>
            {
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(compositionControl.GetTrackScreenSpacePosition(compositionControl.RegularRow, 0.3f) + new Vector2(0, 1));
            });
            AddStep("release mouse", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("RC lower bound increased", () => compositionControl.RegularRow.LowerBound.Value > 1);
            AddAssert("RC row enabled by drag", () => compositionControl.RegularRow.Enabled.Value);
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
