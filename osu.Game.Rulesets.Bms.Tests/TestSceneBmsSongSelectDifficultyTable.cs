// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Carousel;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSongSelectDifficultyTable : BmsSongSelectTestScene
    {
        private static int nextSetId;

        [Test]
        public void TestInitialLoadWithPreselectedBeatmapStaysAtRootLevel()
        {
            importDifficultyTableBeatmaps();
            SelectBmsRuleset();
            PreselectBeatmap("Satellite Song");
            SetDifficultyTableGrouping();

            LoadSongSelect();

            assertDifficultyTableRootLevelVisible();
        }

        [Test]
        public void TestSwitchingToDifficultyTableClearsExpandedBeatmapSelection()
        {
            importDifficultyTableBeatmaps();
            SelectBmsRuleset();
            PreselectBeatmap("Satellite Song");

            LoadSongSelect();
            AddUntilStep("wait for preselected beatmap selection", () => Carousel.CurrentGroupedBeatmap != null);

            SetDifficultyTableGrouping();

            assertDifficultyTableRootLevelVisible();
        }

        private void importDifficultyTableBeatmaps()
        {
            AddStep("import difficulty table beatmaps", () =>
            {
                Beatmaps.Import(createBeatmapSet(
                    "Satellite Song",
                    1.4,
                    new BmsDifficultyTableEntry("Satellite", "★", 1, "★1", "satellite-1", 0)));

                Beatmaps.Import(createBeatmapSet(
                    "Stella Song",
                    2.1,
                    new BmsDifficultyTableEntry("Stella", "☆", 1, "☆1", "stella-1", 1)));
            });

            AddUntilStep("wait for beatmaps imported", () => Beatmaps.GetAllUsableBeatmapSets().Count, () => Is.EqualTo(2));
        }

        private void assertDifficultyTableRootLevelVisible()
        {
            WaitForFiltering();
            AddUntilStep("wait for visible carousel items", () => GetVisibleCarouselModels().Length, () => Is.GreaterThan(0));
            AddAssert("no beatmap is auto-selected", () => Carousel.CurrentGroupedBeatmap, () => Is.Null);
            AddAssert("only root groups are visible", () => GetVisibleCarouselModels().All(model => model is GroupDefinition group && group.Depth == 0));
            AddAssert("visible groups are root table names", () => GetVisibleCarouselModels().OfType<GroupDefinition>().Select(group => group.Title.ToString()).ToArray(), () => Is.EquivalentTo(new[] { "Satellite", "Stella" }));
        }

        private static BeatmapSetInfo createBeatmapSet(string title, double starRating, BmsDifficultyTableEntry tableEntry)
        {
            int setId = Interlocked.Increment(ref nextSetId);

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata
            {
                Title = title,
                Artist = title,
            })
            {
                OnlineID = setId * 1000,
                DifficultyName = $"{title} [{tableEntry.LevelLabel}]",
                StarRating = starRating,
                Length = 120000,
                BPM = 150,
                Hash = Guid.NewGuid().ToString("N"),
                MD5Hash = $"{title}-{tableEntry.LevelLabel}-{Guid.NewGuid():N}".ToLowerInvariant(),
            };

            beatmap.Metadata.SetDifficultyTableEntries(new[] { tableEntry });

            var beatmapSet = new BeatmapSetInfo
            {
                OnlineID = setId,
                Hash = Guid.NewGuid().ToString("N"),
            };

            beatmapSet.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = beatmapSet;

            return beatmapSet;
        }
    }

    public abstract partial class BmsSongSelectTestScene : ScreenTestScene
    {
        protected BeatmapManager Beatmaps { get; private set; } = null!;
        protected RealmRulesetStore Rulesets { get; private set; } = null!;
        protected OsuConfigManager Config { get; private set; } = null!;
        protected ScoreManager ScoreManager { get; private set; } = null!;

        protected osu.Game.Screens.Select.SongSelect SongSelectScreen { get; private set; } = null!;
        protected BeatmapCarousel Carousel => SongSelectScreen.ChildrenOfType<BeatmapCarousel>().Single();

        private RealmDetachedBeatmapStore beatmapStore = null!;

        [Cached]
        private readonly OsuLogo logo;

        [Cached]
        private readonly VolumeOverlay volume;

        [Cached(typeof(INotificationOverlay))]
        private readonly INotificationOverlay notificationOverlay = new NotificationOverlay();

        [Cached]
        protected readonly LeaderboardManager LeaderboardManager = new LeaderboardManager();

        protected override bool UseFreshStoragePerRun => true;

        protected BmsSongSelectTestScene()
        {
            Children = new Drawable[]
            {
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        LeaderboardManager,
                        new Toolbar
                        {
                            State = { Value = Visibility.Visible },
                        },
                        logo = new OsuLogo
                        {
                            Alpha = 0f,
                        },
                        volume = new VolumeOverlay(),
                    },
                },
            };

            Stack.Padding = new MarginPadding { Top = Toolbar.HEIGHT };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.Cache(Rulesets = new RealmRulesetStore(Realm));
            dependencies.Cache(Realm);
            dependencies.Cache(Beatmaps = new BeatmapManager(LocalStorage, Realm, null, Dependencies.Get<AudioManager>(), Resources, Dependencies.Get<GameHost>(), Beatmap.Default));
            dependencies.Cache(Config = new OsuConfigManager(LocalStorage));
            dependencies.Cache(ScoreManager = new ScoreManager(Rulesets, () => Beatmaps, LocalStorage, Realm, API, Config));
            dependencies.CacheAs<BeatmapStore>(beatmapStore = new RealmDetachedBeatmapStore());

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(beatmapStore);
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("reset defaults", () =>
            {
                Ruleset.Value = Rulesets.AvailableRulesets.First();

                Beatmap.SetDefault();
                SelectedMods.SetDefault();

                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Title);
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None);

                SongSelectScreen = null!;
            });

            AddStep("delete all beatmaps", () => Beatmaps.Delete());
        }

        protected void LoadSongSelect()
        {
            AddStep("load screen", () => Stack.Push(SongSelectScreen = new SoloSongSelect()));
            AddUntilStep("wait for load", () => Stack.CurrentScreen == SongSelectScreen && SongSelectScreen.IsLoaded);
            WaitForFiltering();
        }

        protected void WaitForFiltering() => AddUntilStep("wait for filtering", () => !SongSelectScreen.IsFiltering);

        protected void SelectBmsRuleset()
            => AddStep("select BMS ruleset", () => Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.ShortName == BmsRuleset.SHORT_NAME));

        protected void PreselectBeatmap(string title)
        {
            AddStep($"preselect {title}", () =>
            {
                var beatmap = Beatmaps.GetAllUsableBeatmapSets()
                                     .SelectMany(set => set.Beatmaps)
                                     .Single(b => b.Metadata.Title == title);

                Beatmap.Value = Beatmaps.GetWorkingBeatmap(beatmap, true);
            });
        }

        protected void SetDifficultyTableGrouping()
        {
            AddStep("enable difficulty table grouping", () =>
            {
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Difficulty);
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.DifficultyTable);
            });
        }

        protected object[] GetVisibleCarouselModels()
        {
            return SongSelectScreen.ChildrenOfType<ICarouselPanel>()
                             .Where(panel => panel.Item?.IsVisible == true)
                             .OrderBy(panel => panel.DrawYPosition)
                             .Select(panel => panel.Item!.Model)
                             .ToArray();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (Rulesets != null)
                Rulesets.Dispose();
        }
    }
}
