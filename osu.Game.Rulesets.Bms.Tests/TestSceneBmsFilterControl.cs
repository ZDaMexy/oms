// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;
using osu.Game.Graphics.UserInterface;
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
            AddStep("set RC maximum", () => { compositionControl.LongNoteRow.UpperBound.Value = 0; compositionControl.RegularRow.UpperBound.Value = 50; });

            FilterCriteria criteria = null!;
            AddStep("create criteria", () => criteria = filter.CreateCriteria());

            AddAssert("5K RC20 matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 3, 5), criteria));
            AddAssert("9K RC20 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(9, 2, 3, 5), criteria));
            AddAssert("5K RC60 filtered", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 6, 2, 2), criteria));
        }

        [Test]
        public void TestTextRangeRemainsAvailable()
        {
            SelectBmsRuleset();
            LoadSongSelect();
            AddStep("search RC range", () => filter.Search("rc>=30 rc<=70"));
            AddAssert("below range excluded", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 2, 5, 3), filter.CreateCriteria()));
            AddAssert("in range matches", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 5, 3, 2), filter.CreateCriteria()));
            AddAssert("above range excluded", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 8, 1, 1), filter.CreateCriteria()));
        }

        [Test]
        public void TestSharedBudgetAndZeroWidthRecovery()
        {
            SelectBmsRuleset();
            LoadSongSelect();
            FilterControl.BmsCompositionFilterControl control = null!;
            AddStep("get control", () => control = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("consume tail", () => control.SetUpperBound(control.RegularRow, 100));
            AddAssert("other segments preserved", () => control.RegularRow.UpperBound.Value == 40 && control.LongNoteRow.UpperBound.Value == 30 && control.ScratchRow.UpperBound.Value == 30);
            AddStep("collapse RC", () => control.SetUpperBound(control.RegularRow, -10));
            AddAssert("RC clamped to zero", () => control.RegularRow.UpperBound.Value == 0);
            AddStep("restore RC", () => control.SetUpperBound(control.RegularRow, 20));
            AddAssert("restored without changing neighbours", () => control.RegularRow.UpperBound.Value == 20 && control.LongNoteRow.UpperBound.Value == 30);
            AddStep("disable RC", () => control.RegularRow.Enabled.Value = false);
            AddAssert("disabled cap does not filter", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 8, 1, 1), filter.CreateCriteria()));
            AddStep("enable RC again", () => control.RegularRow.Enabled.Value = true);
            AddAssert("same cap restored", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 8, 1, 1), filter.CreateCriteria()));
        }

        [Test]
        public void TestRulesetRoundTripKeepsFiltersSeparate()
        {
            SelectBmsRuleset();
            LoadSongSelect();
            AddStep("set BMS cap and hidden stars", () =>
            {
                var control = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single();
                control.SetUpperBound(control.RegularRow, 20);
                Config.SetValue(OsuSetting.DisplayStarsMinimum, 10.0);
            });
            AddAssert("BMS ignores stars", () => !filter.CreateCriteria().UserStarDifficulty.HasFilter);
            AddStep("switch to mania", () => Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.ShortName == "mania"));
            AddAssert("mania restores stars", () => filter.CreateCriteria().UserStarDifficulty.Min == 10);
            AddUntilStep("BMS controls hidden", () => !compositionSurfacePresent());
            SelectBmsRuleset();
            AddUntilStep("BMS controls restored", compositionSurfacePresent);
            AddAssert("BMS cap preserved", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(createBeatmap(5, 8, 1, 1), filter.CreateCriteria()));
            AddAssert("stars still ignored", () => !filter.CreateCriteria().UserStarDifficulty.HasFilter);
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
                InputManager.MoveMouseTo(compositionControl.GetTrackScreenSpacePosition(0.75f) + new Vector2(0, 1));
            });
            AddStep("release mouse", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("SCR upper bound decreased", () => compositionControl.ScratchRow.UpperBound.Value < initialUpperBound - 1);
            AddAssert("SCR row enabled by drag", () => compositionControl.ScratchRow.Enabled.Value);
        }

        [Test]
        public void TestClickSegmentEditsMaximum()
        {
            SelectBmsRuleset();
            LoadSongSelect();
            FilterControl.BmsCompositionFilterControl control = null!;
            AddStep("get control", () => control = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single());
            AddStep("collapse RC segment", () => control.SetUpperBound(control.RegularRow, 0));
            AddStep("click zero-width RC entry", () =>
            {
                InputManager.MoveMouseTo(control.GetSegmentButton(control.RegularRow));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("editor visible", () => this.ChildrenOfType<OsuTextBox>().Any(t => t.IsPresent && t.Text == "0"));
            AddStep("enter cap", () =>
            {
                var input = this.ChildrenOfType<OsuTextBox>().Single(t => t.IsPresent && t.Text == "0");
                InputManager.MoveMouseTo(input);
                InputManager.Click(MouseButton.Left);
                input.Text = "15";
                InputManager.Key(Key.Enter);
            });
            AddAssert("numeric cap applied", () => control.RegularRow.UpperBound.Value == 15 && control.RegularRow.Enabled.Value);
        }

        [Test]
        public void TestLateCacheSubscriberStillReceivesRefresh()
        {
            SelectBmsRuleset();
            // Loading song select wires the FilterControl, which starts the stats backfill (and registers its own
            // cache-updated callback). This claims the one-time bootstrap.
            LoadSongSelect();

            bool lateCallbackInvoked = false;

            // Simulates the beatmap-details graph (or any later caller) re-entering Initialise. The old static guard
            // returned early and silently dropped this callback; the backfill must now still register and immediately
            // refresh the late subscriber so its composition filter picks up the already-populated cache.
            AddStep("register late cache subscriber", () =>
                BmsChartFilterStatsBackfill.Initialise(Beatmaps, Realm, storage: null, notifications: null, onCacheUpdated: () => lateCallbackInvoked = true));

            AddAssert("late subscriber refreshed immediately", () => lateCallbackInvoked);
        }

        private bool compositionSurfacePresent()
        {
            // IsPresent describes each drawable itself, not visibility inherited from its ruleset container.
            // Stop at FilterControl: its overall expanded/collapsed state is unrelated to ruleset branching.
            for (Drawable drawable = filter.ChildrenOfType<FilterControl.BmsCompositionFilterControl>().Single(); drawable != filter; drawable = drawable.Parent!)
            {
                if (!drawable.IsPresent)
                    return false;
            }

            return true;
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
