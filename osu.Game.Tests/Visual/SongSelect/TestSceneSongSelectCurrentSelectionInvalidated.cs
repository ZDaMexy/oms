// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms;
using osu.Game.Screens;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// The fallback behaviour guaranteed by SongSelect is that a random selection will happen in worst case scenario.
    /// Every case we're testing here is expected to have a *custom behaviour* – engaging and overriding this random selection fallback.
    ///
    /// The scenarios we care abouts are:
    /// - Ruleset change (select another difficulty from same set for the new ruleset, if possible).
    /// - Beatmap difficulty hidden (select closest valid difficulty from same set)
    /// - Beatmap set deleted (select closest valid beatmap post-deletion)
    ///
    /// We are working with 5 sets, each with 3 native mania difficulties.
    /// </summary>
    public partial class TestSceneSongSelectCurrentSelectionInvalidated : SongSelectTestScene
    {
        private BeatmapInfo? selectedBeatmap => Carousel.CurrentBeatmap;
        private BeatmapSetInfo? selectedBeatmapSet => selectedBeatmap?.BeatmapSet;
        private int loadedFilterCount;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            ChangeMode("mania");

            for (int i = 0; i < 5; i++)
                ImportBeatmapForModes("mania");

            LoadSongSelect();
            WaitForFiltering();
            AddStep("capture the fully loaded screen's actual filter baseline", () => loadedFilterCount = Carousel.FilterCount);
        }

        [Test]
        public void TestRulesetChange()
        {
            AddStep("disable converts", () => Config.SetValue(OsuSetting.ConvertedBeatmapsDisplay, ConvertedBeatmapsDisplay.Hidden));

            ImportBeatmapForModes("mania", "bms");
            ImportBeatmapForModes("mania", "bms");
            ImportBeatmapForModes("bms");
            WaitForFiltering();
            AddUntilStep("native carousel items have actually been presented", () => SongSelect.CarouselItemsPresented && !SongSelect.IsFiltering);

            BeatmapSetInfo mixed = null!;
            BeatmapSetInfo maniaOnly = null!;
            BeatmapSetInfo bmsOnly = null!;
            BeatmapInfo[] bmsSources = null!;
            AddStep("retain actual mixed and single-mode sets", () =>
            {
                var sets = Beatmaps.GetAllUsableBeatmapSets().OrderBy(set => set.OnlineID).ToArray();
                mixed = sets.First(set => set.Beatmaps.Any(beatmap => beatmap.Ruleset.ShortName == "mania")
                                         && set.Beatmaps.Any(beatmap => beatmap.Ruleset.ShortName == "bms"));
                maniaOnly = sets.First(set => set.Beatmaps.All(beatmap => beatmap.Ruleset.ShortName == "mania"));
                bmsOnly = sets.Single(set => set.Beatmaps.All(beatmap => beatmap.Ruleset.ShortName == "bms"));
                bmsSources = sets.SelectMany(set => set.Beatmaps).Where(beatmap => beatmap.Ruleset.ShortName == "bms").ToArray();
                Assert.That(bmsSources.All(beatmap => BmsRuleset.TryGetKeyCount(beatmap, out int keys) && keys == 7), Is.True,
                    "Every native BMS fixture must carry a supported persisted key count.");
            });
            presentNative(() => mixed, "mania");

            // A compatible difficulty in the same set does not override BMS's default root grouping.
            ChangeMode("bms");
            assertBmsRootWhileManiaPlays(() => mixed, () => bmsSources);

            presentNative(() => mixed, "bms");
            ChangeMode("mania");
            assertUngroupedNativeManiaSelection();
            AddAssert("BMS to mania uses the same mixed set", () => selectedBeatmapSet, () => Is.EqualTo(mixed));

            // BMS switches to its default root grouping rather than automatically choosing an unrelated chart.
            presentNative(() => maniaOnly, "mania");
            ChangeMode("bms");
            assertBmsRootWhileManiaPlays(() => maniaOnly, () => bmsSources);

            // A real user selection ends root focus. Mania supports None, so the incompatible single-mode set
            // now exercises the cross-set fallback without bypassing BMS's grouping contract.
            presentNative(() => bmsOnly, "bms");
            ChangeMode("mania");
            assertUngroupedNativeManiaSelection();
            AddAssert("BMS-only set falls back to a different mania set", () => selectedBeatmapSet, () => Is.Not.EqualTo(bmsOnly));
        }

        private void assertBmsRootWhileManiaPlays(Func<BeatmapSetInfo> playingSet, Func<BeatmapInfo[]> bmsSources)
        {
            WaitForFiltering();
            AddAssert("BMS default grouping agrees across mode, config, control and carousel", () =>
                (Ruleset.Value.ShortName, Config.Get<GroupMode>(OsuSetting.SongSelectGroupMode),
                    SongSelect.ChildrenOfType<FilterControl>().Single().CreateCriteria().Group, Carousel.Criteria?.Group),
                () => Is.EqualTo(("bms", GroupMode.DifficultyTable, GroupMode.DifficultyTable, (GroupMode?)GroupMode.DifficultyTable)));
            AddAssert("all imported native BMS sources are matched", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(bmsSources().Length));
            AddAssert("matched BMS source identities are preserved", () => Carousel.GetCarouselItems()!
                    .Select(item => item.Model).OfType<GroupedBeatmap>()
                    .Select(grouped => (grouped.Beatmap.ID, grouped.Beatmap.Ruleset.ShortName)).Distinct().ToArray(),
                () => Is.EquivalentTo(bmsSources().Select(beatmap => (beatmap.ID, "bms"))));
            AddAssert("no BMS beatmap is auto-selected", () => Carousel.CurrentGroupedBeatmap, () => Is.Null);
            AddAssert("no BMS group is auto-expanded", () => Carousel.CurrentExpandedGroup.Value, () => Is.Null);
            AddAssert("BMS root rows are visible", () => Carousel.GetCarouselItems()!.Count(item => item.IsVisible), () => Is.GreaterThan(0));
            AddAssert("only BMS root group models are visible", () => Carousel.GetCarouselItems()!
                .Where(item => item.IsVisible).All(item => item.Model is GroupDefinition { Depth: 0 }));
            AddAssert("root grouping retains the exact global mania source", () =>
                (Beatmap.Value.BeatmapInfo.ID, Beatmap.Value.BeatmapInfo.Ruleset.ShortName,
                    Beatmap.Value.BeatmapInfo.BeatmapSet?.ID, Beatmap.IsDefault),
                () => Is.EqualTo((playingSet().Beatmaps.First(beatmap => beatmap.Ruleset.ShortName == "mania").ID,
                    "mania", (Guid?)playingSet().ID, false)));
        }

        private void assertUngroupedNativeManiaSelection()
        {
            GroupBy(GroupMode.None);
            WaitForFiltering();
            AddAssert("mania uses its actual ungrouped entry", () =>
                (Ruleset.Value.ShortName, Config.Get<GroupMode>(OsuSetting.SongSelectGroupMode),
                    SongSelect.ChildrenOfType<FilterControl>().Single().CreateCriteria().Group, Carousel.Criteria?.Group),
                () => Is.EqualTo(("mania", GroupMode.None, GroupMode.None, (GroupMode?)GroupMode.None)));
            assertNativeSelection("mania");
        }

        private void presentNative(Func<BeatmapSetInfo> set, string mode)
        {
            AddStep($"present actual native {mode} source", () =>
            {
                var beatmap = set().Beatmaps.First(beatmap => beatmap.Ruleset.ShortName == mode);
                ((IHandlePresentBeatmap)SongSelect).PresentBeatmap(Beatmaps.GetWorkingBeatmap(beatmap), Ruleset.Value);
            });
            assertNativeSelection(mode);
            AddAssert("specified set is selected", () => selectedBeatmapSet, () => Is.EqualTo(set()));
            AddAssert("the exact presented native source is selected", () => selectedBeatmap?.ID,
                () => Is.EqualTo(set().Beatmaps.First(beatmap => beatmap.Ruleset.ShortName == mode).ID));
        }

        private void assertNativeSelection(string mode)
        {
            WaitForFiltering();
            AddUntilStep($"native {mode} selection agrees across carousel and global working beatmap", () =>
                (Ruleset.Value.ShortName, selectedBeatmap?.Ruleset.ShortName, Beatmap.Value.BeatmapInfo.Ruleset.ShortName,
                    selectedBeatmap?.ID, Beatmap.Value.BeatmapInfo.ID, Beatmap.IsDefault),
                () => Is.EqualTo((mode, (string?)mode, mode, (Guid?)Beatmap.Value.BeatmapInfo.ID, Beatmap.Value.BeatmapInfo.ID, false)));
        }

        /// <summary>
        /// Make sure that deleting all sets doesn't hit some weird edge case / crash.
        /// </summary>
        [TestCase(SortMode.Title)]
        [TestCase(SortMode.Artist)]
        [TestCase(SortMode.Difficulty)]
        public void TestDeleteAllSets(SortMode sortMode)
        {
            int filterCount = sortMode != SortMode.Title ? 1 : 0;

            SortBy(sortMode);
            waitForFiltering(filterCount);

            BeatmapSetInfo deletedSet = null!;

            for (int i = 0; i < 4; i++)
            {
                AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
                waitForFiltering(filterCount + 1 + i);
                selectionChangedFrom(() => deletedSet);
            }

            // The carousel still holds an invalid selection after the final deletion. Probably fine?
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            AddUntilStep("wait for no global selection", () => Beatmap.IsDefault, () => Is.True);
        }

        [Test]
        public void DifficultiesGrouped_DeleteSet_SelectsAdjacent()
        {
            SortAndGroupBy(SortMode.Difficulty, GroupMode.Difficulty);
            waitForFiltering(1);

            makePanelSelected<PanelGroupStarDifficulty>(2);
            makePanelSelected<PanelBeatmapStandalone>(3);

            // Deleting second-last, should select last
            BeatmapSetInfo deletedSet = null!;
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            waitForFiltering(2);

            selectionChangedFrom(() => deletedSet);
            assertPanelSelected<PanelBeatmapStandalone>(3);

            // Deleting last, should select previous
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            waitForFiltering(3);

            selectionChangedFrom(() => deletedSet);
            assertPanelSelected<PanelBeatmapStandalone>(2);
        }

        [TestCase(SortMode.Title)]
        [TestCase(SortMode.Artist)]
        public void SetsGrouped_DeleteSet_SelectsAdjacent(SortMode sortMode)
        {
            int filterCount = sortMode != SortMode.Title ? 1 : 0;

            SortBy(sortMode);
            waitForFiltering(filterCount);

            makePanelSelected<PanelBeatmapSet>(3);

            // Deleting second-last, should select last
            BeatmapSetInfo deletedSet = null!;
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            waitForFiltering(filterCount + 1);

            selectionChangedFrom(() => deletedSet);
            assertPanelSelected<PanelBeatmapSet>(3);
            assertPanelSelected<PanelBeatmap>(0);

            // Deleting last, should select previous
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            waitForFiltering(filterCount + 2);

            selectionChangedFrom(() => deletedSet);
            assertPanelSelected<PanelBeatmapSet>(2);
            assertPanelSelected<PanelBeatmap>(0);
        }

        // Same scenario as the test case above, but where selected difficulty before deletion is not first index in the expanded set.
        // Basically ensures that the reselection is running `RequestRecommendedSelection` and not just relying on indices.
        [TestCase(SortMode.Title)]
        [TestCase(SortMode.Artist)]
        public void SetsGrouped_DeleteSet_SelectsNextSetRecommendedDifficulty(SortMode sortMode)
        {
            int filterCount = sortMode != SortMode.Title ? 1 : 0;

            SortBy(sortMode);
            waitForFiltering(filterCount);

            makePanelSelected<PanelBeatmapSet>(2);
            makePanelSelected<PanelBeatmap>(2);

            AddUntilStep("wait for beatmap to be selected", () => selectedBeatmapSet != null);

            BeatmapSetInfo deletedSet = null!;
            AddStep("delete selected", () => Beatmaps.Delete(deletedSet = selectedBeatmapSet!));
            waitForFiltering(++filterCount);

            selectionChangedFrom(() => deletedSet);
            assertPanelSelected<PanelBeatmapSet>(2);
            assertPanelSelected<PanelBeatmap>(0);
        }

        [Test]
        public void TestHideBeatmap()
        {
            makePanelSelected<PanelBeatmapSet>(2);
            makePanelSelected<PanelBeatmap>(1);

            BeatmapInfo hiddenBeatmap = null!;

            AddStep("hide selected", () => Beatmaps.Hide(hiddenBeatmap = selectedBeatmap!));
            waitForFiltering(1);

            AddAssert("selected beatmap below", () => selectedBeatmap!.BeatmapSet, () => Is.EqualTo(hiddenBeatmap.BeatmapSet));

            AddStep("hide selected", () => Beatmaps.Hide(hiddenBeatmap = selectedBeatmap!));
            waitForFiltering(2);

            AddAssert("selected beatmap below", () => selectedBeatmap!.BeatmapSet, () => Is.EqualTo(hiddenBeatmap.BeatmapSet));
            assertPanelSelected<PanelBeatmap>(0);
        }

        [Test]
        [Explicit]
        public void TestDebounceNotBypassedOnUpdate()
        {
            BeatmapInfo? selectedBefore = null;
            BeatmapInfo? selectedBeatmapDuringDebounce = null;

            // we're testing the song select side debounce, so let's make filtering immediate
            AddStep("set filter debounce delay to zero", () => Carousel.DebounceDelay = 0);

            WaitForFiltering();

            AddUntilStep("wait for global beatmap selection", () => !Beatmap.IsDefault);

            AddStep("store selection", () => selectedBefore = Beatmap.Value.BeatmapInfo);

            AddStep("traverse to next panel and update simultaneously", () =>
            {
                InputManager.Key(Key.Right);

                Beatmaps.Delete(Beatmaps.GetAllUsableBeatmapSets().Last());

                // check selection during debounce
                Scheduler.AddDelayed(() => selectedBeatmapDuringDebounce = Beatmap.Value.BeatmapInfo, Screens.Select.SongSelect.SELECTION_DEBOUNCE / 2f);
            });

            WaitForFiltering();

            AddUntilStep("wait for pre-debounce selection", () => selectedBeatmapDuringDebounce, () => Is.Not.Null);

            AddAssert("selection during debounce didn't change", () => selectedBeatmapDuringDebounce, () => Is.EqualTo(selectedBefore));

            // Due to nunit runs having limited precision this tends to fail when headless, even though you'd expect the previous step to fail.
            // Interactively, things fail as expected.
            AddUntilStep("selection has changed after debounce", () => selectedBeatmapDuringDebounce, () => Is.Not.EqualTo(Beatmap.Value.BeatmapInfo));
        }

        private void waitForFiltering(int filterCount = 0)
        {
            AddUntilStep($"wait for exactly {filterCount} filter operations after screen load", () => Carousel.FilterCount,
                () => Is.EqualTo(loadedFilterCount + filterCount));
            AddUntilStep("filtering finished", () => Carousel.IsFiltering, () => Is.False);
        }

        private void makePanelSelected<T>(int index)
            where T : Panel
        {
            AddStep($"click panel at index {index} if not selected", () =>
            {
                var panel = allPanels<T>().ElementAt(index).ChildrenOfType<Panel>().Single();

                // May have already been selected randomly. Don't click a second time or gameplay will start.
                if (!panel.Selected.Value)
                    panel.TriggerClick();
            });

            assertPanelSelected<T>(index);
        }

        private void selectionChangedFrom(Func<BeatmapSetInfo> deletedSet) =>
            AddUntilStep("selection changed", () => selectedBeatmapSet, () => Is.Not.EqualTo(deletedSet()));

        private void assertPanelSelected<T>(int index)
            where T : Panel
            => AddUntilStep($"selected panel at index {index}", getActivePanelIndex<T>, () => Is.EqualTo(index));

        private int getActivePanelIndex<T>()
            where T : Panel
            => allPanels<T>().ToList().FindIndex(p =>
            {
                switch (p)
                {
                    case PanelBeatmapStandalone pb:
                        return pb.Selected.Value;

                    case PanelBeatmap pb:
                        return pb.Selected.Value;

                    case Panel pbs:
                        return pbs.Expanded.Value;

                    default:
                        throw new InvalidOperationException();
                }
            });

        private IEnumerable<T> allPanels<T>()
            where T : Panel
            => Carousel.ChildrenOfType<T>().Where(p => p.Item != null).OrderBy(p => p.Y);
    }
}
