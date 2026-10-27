// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Pages;
using osu.Game.Tests.NonVisual.Skinning;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneSkinSongSelectPage : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private Live<SkinInfo> keyed = null!;
        private Live<SkinInfo> unrelated = null!;
        private SkinManagedFolderOperationCoordinator.Lease? heldMutation;

        [TearDownSteps]
        public override void TearDownSteps()
        {
            AddStep("release held read boundary", () =>
            {
                heldMutation?.Dispose();
                heldMutation = null;
            });
            base.TearDownSteps();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestActualNativeSelectionAndOperationsSurviveChromeSwitchAndPoolReuse(bool bms)
        {
            Ruleset ruleset = bms ? new BmsRuleset() : new ManiaRuleset();
            SoloSongSelect screen = null!;
            BeatmapCarousel carousel = null!;
            FilterControl filter = null!;
            Skin globalOwner = null!;
            WorkingBeatmap selected = null!;
            arrange(ruleset);
            PushAndConfirm(() => screen = new SoloSongSelect());
            AddUntilStep("actual seven-key selection resolves the keyed page", () => screen.SkinPageAvailable
                && screen.SongSelectPageSkin?.SkinInfo.ID == keyed.ID
                && screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope == new SkinSelectionScope(ruleset.ShortName, 7));
            AddStep("retain actual native instances and selection", () =>
            {
                carousel = screen.ChildrenOfType<BeatmapCarousel>().Single();
                filter = screen.ChildrenOfType<FilterControl>().Single();
                globalOwner = skins.CurrentSkin.Value;
                selected = Game.Beatmap.Value;
                Assert.That(screen.ShowingSkinPage, Is.False);
                Assert.That(screen.ChildrenOfType<SkinSongSelectChrome>().All(chrome => !chrome.HandlePositionalInput), Is.True);
                Assert.That(screen.ChildrenOfType<SkinSongSelectChrome>().SelectMany(chrome => chrome.ChildrenOfType<Sprite>()).All(sprite => sprite.Texture == null), Is.True);
            });
            AddStep("open using the actual footer control", () =>
            {
                var button = Game.ChildrenOfType<ScreenFooterButton>().Single(button => button.Text.ToString() == "皮肤界面");
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("native row chrome contains the captured pixels", () => screen.ShowingSkinPage
                && screen.ChildrenOfType<Panel>().Where(panel => panel.Item?.IsVisible == true)
                    .SelectMany(panel => panel.ChildrenOfType<SkinSongSelectChrome>())
                    .Any(chrome => chrome.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture?.Width == 7 && sprite.Texture.Height == 5)));
            AddAssert("real selected and idle row state choose their distinct pixels", () => screen.ChildrenOfType<Panel>()
                .Where(panel => panel.Item?.IsVisible == true && panel.ChildrenOfType<SkinSongSelectChrome>().Any())
                .All(panel => panel.ChildrenOfType<SkinSongSelectChrome>().Single().ChildrenOfType<Sprite>().Single().Texture?.Width
                    == (panel.Selected.Value || panel.Expanded.Value ? 7 : 4)));
            AddAssert("original controls selection and global owner are retained", () =>
                ReferenceEquals(screen.ChildrenOfType<BeatmapCarousel>().Single(), carousel)
                && ReferenceEquals(screen.ChildrenOfType<FilterControl>().Single(), filter)
                && ReferenceEquals(Game.Beatmap.Value, selected)
                && ReferenceEquals(skins.CurrentSkin.Value, globalOwner)
                && screen.ChildrenOfType<PanelSetBackground>().Any());
            AddStep("mounted chrome rejects unstaged current refresh", () =>
            {
                Task<SkinCurrentRevisionReloadResult> reload = skins.ReloadCurrentRevisionAsync();
                Assert.That(reload.IsCompleted, Is.True);
                Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.ParticipantRejected));
            });
            AddStep("search uses the original native filtering", () => filter.Search("no-matching-native-song-738294"));
            AddUntilStep("the native no-results state is reached", () => !screen.IsFiltering && !screen.ChildrenOfType<Panel>().Any(panel => panel.Item?.IsVisible == true));
            AddStep("clear native search to recycle rows", () => filter.Search(string.Empty));
            AddUntilStep("pooled rows reacquire correct chrome", () => !screen.IsFiltering
                && screen.ChildrenOfType<Panel>().Where(panel => panel.Item?.IsVisible == true)
                    .SelectMany(panel => panel.ChildrenOfType<SkinSongSelectChrome>()).Any(chrome => chrome.Alpha > 0));
            AddStep("return with the actual original-page footer action", () =>
            {
                var button = Game.ChildrenOfType<ScreenFooterButton>().Single(button => button.Text.ToString() == "原界面");
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("all native chrome consumers release their sprite references", () => !screen.ShowingSkinPage
                && screen.ChildrenOfType<SkinSongSelectChrome>().SelectMany(chrome => chrome.ChildrenOfType<Sprite>()).All(sprite => sprite.Texture == null)
                && ReferenceEquals(skins.CurrentSkin.Value, globalOwner));
            AddStep("changing only the real key assignment refreshes the page owner", () =>
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope(ruleset.ShortName, 7), unrelated.ID));
            AddUntilStep("the mode default owner is now used", () => screen.SkinPageAvailable && screen.SongSelectPageSkin?.SkinInfo.ID == unrelated.ID);
            AddAssert("no global current was applied while browsing", () => ReferenceEquals(skins.CurrentSkin.Value, globalOwner));
        }

        [Test]
        public void TestIncompatiblePlayingManiaSourceUsesBmsModeDefaultAtRoot()
        {
            SoloSongSelect screen = null!;
            WorkingBeatmap playing = null!;
            Skin globalOwner = null!;
            Guid[] bmsSourceIds = null!;
            arrange(new ManiaRuleset());
            AddStep("retain the native seven-key mania source and distinguish BMS skin assignments", () =>
            {
                playing = Game.Beatmap.Value;
                globalOwner = skins.CurrentSkin.Value;
                Assert.That(playing.BeatmapInfo.Ruleset.ShortName, Is.EqualTo("mania"));
                Assert.That(playing.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(7));
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms", 7), keyed.ID);
                Assert.That(skins.GetConfiguredSkin(new SkinSelectionScope("bms")).ID, Is.EqualTo(unrelated.ID));
                Assert.That(skins.GetConfiguredSkin(new SkinSelectionScope("bms", 7)).ID, Is.EqualTo(keyed.ID));
                Assert.That(keyed.ID, Is.Not.EqualTo(unrelated.ID));
            });
            AddStep("import two real native BMS sets so the root has matching charts", () =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var set = TestResources.CreateTestBeatmapSetInfo(1, new[] { new BmsRuleset().RulesetInfo });
                    set.Beatmaps.Single().Difficulty.CircleSize = 7;
                    set.Beatmaps.Single().Metadata.Title = $"Root scope native BMS {i}";
                    Game.BeatmapManager.Import(set);
                }
            });
            AddUntilStep("both native BMS sources are imported", () => Game.BeatmapManager.GetAllUsableBeatmapSets()
                .SelectMany(set => set.Beatmaps).Count(beatmap => beatmap.Ruleset.ShortName == "bms"), () => Is.EqualTo(2));
            AddStep("retain the actual imported BMS identities", () => bmsSourceIds = Game.BeatmapManager.GetAllUsableBeatmapSets()
                .SelectMany(set => set.Beatmaps).Where(beatmap => beatmap.Ruleset.ShortName == "bms").Select(beatmap => beatmap.ID).ToArray());
            PushAndConfirm(() => screen = new SoloSongSelect());
            AddUntilStep("initial native carousel filtering has settled", () => screen.CarouselItemsPresented && !screen.IsFiltering);
            AddStep("present the exact mania source through the native selection entry", () =>
                ((IHandlePresentBeatmap)screen).PresentBeatmap(playing, new ManiaRuleset().RulesetInfo));
            AddUntilStep("the specified native mania source owns the initial seven-key page", () =>
                (screen.Ruleset.Value.ShortName, screen.Beatmap.Value.BeatmapInfo.ID,
                    screen.ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap?.ID,
                    screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope, screen.SongSelectPageSkin?.SkinInfo.ID, screen.IsFiltering),
                () => Is.EqualTo(("mania", playing.BeatmapInfo.ID, (Guid?)playing.BeatmapInfo.ID,
                    (SkinSelectionScope?)new SkinSelectionScope("mania", 7), (Guid?)keyed.ID, false)));
            AddStep("switch the actual selection mode to BMS", () => Game.Ruleset.Value = new BmsRuleset().RulesetInfo);
            AddUntilStep("the real BMS root resolves its mode default instead of the incompatible seven-key override", () =>
            {
                var carousel = screen.ChildrenOfType<BeatmapCarousel>().Single();
                var host = screen.ChildrenOfType<SkinSongSelectPageHost>().Single();
                return (screen.Ruleset.Value.ShortName, Game.Dependencies.Get<OsuConfigManager>().Get<GroupMode>(OsuSetting.SongSelectGroupMode),
                    screen.ChildrenOfType<FilterControl>().Single().CreateCriteria().Group, carousel.Criteria?.Group,
                    screen.IsFiltering, carousel.MatchedBeatmapsCount, host.Scope, host.Available, host.PageSkin?.SkinInfo.ID);
            }, () => Is.EqualTo(("bms", GroupMode.DifficultyTable, GroupMode.DifficultyTable, (GroupMode?)GroupMode.DifficultyTable,
                false, 2, (SkinSelectionScope?)new SkinSelectionScope("bms"), true, (Guid?)unrelated.ID)));
            AddAssert("both actual BMS sources remain matched", () => screen.ChildrenOfType<BeatmapCarousel>().Single().GetCarouselItems()!
                    .Select(item => item.Model).OfType<GroupedBeatmap>()
                    .Select(grouped => (grouped.Beatmap.ID, grouped.Beatmap.Ruleset.ShortName)).Distinct().ToArray(),
                () => Is.EquivalentTo(bmsSourceIds.Select(id => (id, "bms"))));
            AddAssert("the BMS root has no automatic chart selection or expanded group", () =>
                screen.ChildrenOfType<BeatmapCarousel>().Single().CurrentGroupedBeatmap == null
                && screen.ChildrenOfType<BeatmapCarousel>().Single().CurrentExpandedGroup.Value == null);
            AddAssert("the nonempty visible native models are only root groups", () =>
            {
                var visible = screen.ChildrenOfType<BeatmapCarousel>().Single().GetCarouselItems()!.Where(item => item.IsVisible).ToArray();
                return visible.Length > 0 && visible.All(item => item.Model is GroupDefinition { Depth: 0 });
            });
            AddAssert("the exact global and screen playing mania identities are retained", () =>
                (Game.Beatmap.Value.BeatmapInfo.ID, Game.Beatmap.Value.BeatmapInfo.Ruleset.ShortName,
                    screen.Beatmap.Value.BeatmapInfo.ID, screen.Beatmap.Value.BeatmapInfo.Ruleset.ShortName, Game.Beatmap.IsDefault),
                () => Is.EqualTo((playing.BeatmapInfo.ID, "mania", playing.BeatmapInfo.ID, "mania", false)));
            AddAssert("the page did not apply a global skin or replace the playing working beatmap", () =>
                ReferenceEquals(Game.Beatmap.Value, playing) && ReferenceEquals(skins.CurrentSkin.Value, globalOwner));
        }

        [Test]
        public void TestSupersededReadAndMalformedPageCannotRepaintTheNativeSelection()
        {
            SoloSongSelect screen = null!;
            arrange(new ManiaRuleset());
            PushAndConfirm(() => screen = new SoloSongSelect());
            AddUntilStep("initial actual page prepared", () => screen.SkinPageAvailable && screen.SongSelectPageSkin?.SkinInfo.ID == keyed.ID);
            AddStep("hold the real independent read boundary", () =>
            {
                heldMutation = skins.ManagedFolderOperationCoordinator.EnterMutation();
                var host = screen.ChildrenOfType<SkinSongSelectPageHost>().Single();
                host.Suspend();
                host.ResumePreparation();
            });
            AddUntilStep("non-current keyed capture waits while native remains available", () => !screen.SkinPageAvailable
                && screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope?.RulesetId == "mania");
            AddStep("change actual mode while the old read is pending", () => Game.Ruleset.Value = new BmsRuleset().RulesetInfo);
            AddStep("release the superseded preparation", () =>
            {
                heldMutation!.Dispose();
                heldMutation = null;
            });
            AddUntilStep("the latest actual mode wins", () => screen.SkinPageAvailable
                && screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope?.RulesetId == "bms"
                && screen.SongSelectPageSkin?.SkinInfo.ID == unrelated.ID);
            Task<Live<SkinInfo>?> importing = null!;
            AddStep("import an invalid optional page without touching gameplay", () => importing = skins.Import(new ImportTask(archive("Invalid selection page", true), "invalid-page.osk")));
            AddUntilStep("invalid author import is retained", () => importing.IsCompleted);
            AddStep("assign the invalid page to actual current key scope", () =>
            {
                var host = screen.ChildrenOfType<SkinSongSelectPageHost>().Single();
                skins.SelectionPreferences!.SetSkin(host.Scope!.Value, importing.GetAwaiter().GetResult()!.ID);
            });
            AddUntilStep("preparation failure is visible and native remains usable", () =>
                screen.ChildrenOfType<SkinSongSelectPageHost>().Single().PreparationError != null && !screen.ShowingSkinPage && !screen.SkinPageAvailable);
            AddAssert("all chrome remains detached and native filter survives", () => screen.ChildrenOfType<FilterControl>().Single().IsLoaded
                && screen.ChildrenOfType<SkinSongSelectChrome>().SelectMany(chrome => chrome.ChildrenOfType<Sprite>()).All(sprite => sprite.Texture == null));
        }

        [Test]
        public void TestActualConvertedKeyModAndSettingsKeepTheExactScopeAndOwner()
        {
            SoloSongSelect screen = null!;
            ManiaModRandom random = new ManiaModRandom();
            Skin selectedOwner = null!;
            BeatmapInfo convertedSource = null!;
            arrange(new ManiaRuleset());
            AddStep("import a real BMS source for mania conversion", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ConvertedBeatmapsDisplay, ConvertedBeatmapsDisplay.Shown);
                var set = TestResources.CreateTestBeatmapSetInfo(1, new[] { new BmsRuleset().RulesetInfo });
                set.Beatmaps.Single().Difficulty.CircleSize = 7;
                Game.BeatmapManager.Import(set);
            });
            AddUntilStep("converted source imported", () => Game.BeatmapManager.GetAllUsableBeatmapSets()
                .SelectMany(set => set.Beatmaps).Any(beatmap => beatmap.Ruleset.ShortName == "bms"));
            AddStep("retain the exact imported BMS source", () => convertedSource = Game.BeatmapManager.GetAllUsableBeatmapSets()
                .SelectMany(set => set.Beatmaps).Single(beatmap => beatmap.Ruleset.ShortName == "bms"));
            PushAndConfirm(() => screen = new SoloSongSelect());
            AddUntilStep("initial native filtering has presented its real items", () => screen.CarouselItemsPresented && !screen.IsFiltering);
            AddStep("present the specified BMS source through the actual selection entry", () =>
            {
                Assert.That(screen.Ruleset.Value.ShortName, Is.EqualTo("mania"));
                ((IHandlePresentBeatmap)screen).PresentBeatmap(Game.BeatmapManager.GetWorkingBeatmap(convertedSource), new ManiaRuleset().RulesetInfo);
            });
            AddUntilStep("unmodified conversion resolves the specified BMS source and seven-key scope", () =>
                (screen.Ruleset.Value.ShortName, screen.Beatmap.Value.BeatmapInfo.ID, screen.Beatmap.Value.BeatmapInfo.Ruleset.ShortName,
                    Game.Beatmap.Value.BeatmapInfo.ID, screen.ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap?.ID,
                    screen.SkinPageAvailable, screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope, screen.IsFiltering),
                () => Is.EqualTo(("mania", convertedSource.ID, "bms", convertedSource.ID, (Guid?)convertedSource.ID,
                    true, (SkinSelectionScope?)new SkinSelectionScope("mania", 7), false)));
            AddStep("apply the actual four-key conversion mod", () => screen.Mods.Value = new Mod[] { new ManiaModKey4(), random });
            AddUntilStep("real converted key count selects the four-key scope", () =>
            {
                var host = screen.ChildrenOfType<SkinSongSelectPageHost>().Single();
                var carouselSource = screen.ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap;
                return (
                    Native: (screen.Ruleset.Value.ShortName, screen.Beatmap.Value.BeatmapInfo.ID, screen.Beatmap.Value.BeatmapInfo.Ruleset.ShortName),
                    Global: (Game.Ruleset.Value.ShortName, Game.Beatmap.Value.BeatmapInfo.ID, Game.Beatmap.Value.BeatmapInfo.Ruleset.ShortName),
                    Mods: (modTypes(screen.Mods.Value), modTypes(Game.SelectedMods.Value)),
                    Page: (host.Scope, host.Available, host.PageSkin?.SkinInfo.ID, host.PreparationError),
                    ConfiguredOwner: skins.GetConfiguredSkin(new SkinSelectionScope("mania", 4)).ID,
                    Carousel: (carouselSource?.ID, carouselSource?.Ruleset.ShortName),
                    Filtering: screen.IsFiltering);
            }, () => Is.EqualTo((
                Native: ("mania", convertedSource.ID, "bms"),
                Global: ("mania", convertedSource.ID, "bms"),
                Mods: ("ManiaModKey4,ManiaModRandom", "ManiaModKey4,ManiaModRandom"),
                Page: ((SkinSelectionScope?)new SkinSelectionScope("mania", 4), true, (Guid?)unrelated.ID, (string?)null),
                ConfiguredOwner: unrelated.ID,
                Carousel: ((Guid?)convertedSource.ID, (string?)"bms"),
                Filtering: false)));
            AddStep("retain the exact owner and modify an existing mod setting", () =>
            {
                selectedOwner = screen.SongSelectPageSkin!;
                // The native overlay canonicalises external selections into its own Mod instances.
                random = screen.Mods.Value.OfType<ManiaModRandom>().Single();
                random.Seed.Value = 12345;
            });
            AddAssert("same-list mod setting did not invent a different key scope or read", () =>
                ReferenceEquals(screen.SongSelectPageSkin, selectedOwner)
                && screen.ChildrenOfType<SkinSongSelectPageHost>().Single().Scope == new SkinSelectionScope("mania", 4)
                && ReferenceEquals(Game.SelectedMods.Value.OfType<ManiaModRandom>().Single(), random));

            static string modTypes(IReadOnlyList<Mod> mods) => string.Join(",", mods.Select(mod => mod.GetType().Name).OrderBy(name => name, StringComparer.Ordinal));
        }

        private void arrange(Ruleset ruleset)
        {
            Task<Live<SkinInfo>?> importing = null!;
            AddStep("import a real page-only key author", () => importing = skins.Import(new ImportTask(archive("Select keyed"), "select-keyed.osk")));
            AddUntilStep("key author imported", () => importing.IsCompleted);
            AddStep("import a distinct global author", () =>
            {
                keyed = importing.GetAwaiter().GetResult()!;
                importing = skins.Import(new ImportTask(archive("Select unrelated"), "select-unrelated.osk"));
            });
            AddUntilStep("global author imported", () => importing.IsCompleted);
            AddStep("assign actual mode and key sources", () =>
            {
                unrelated = importing.GetAwaiter().GetResult()!;
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms"), unrelated.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope("mania"), unrelated.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope(ruleset.ShortName, 7), keyed.ID);
                skins.CurrentSkinInfo.Value = unrelated;
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.SelectedMods.SetDefault();
                var config = Game.Dependencies.Get<OsuConfigManager>();
                config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None);
                var set = TestResources.CreateTestBeatmapSetInfo(2, new[] { ruleset.RulesetInfo });
                foreach (var beatmap in set.Beatmaps)
                {
                    beatmap.Difficulty.CircleSize = 7;
                    beatmap.Metadata.Title = "Actual native selection";
                }
                Game.BeatmapManager.Import(set);
            });
            AddUntilStep("selection source is imported", () => Game.BeatmapManager.GetAllUsableBeatmapSets().Any());
            AddStep("select the actual imported working beatmap", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(
                Game.BeatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First()));
            AddUntilStep("global owner selection settled", () => skins.CurrentSkin.Value.SkinInfo.ID == unrelated.ID && !skins.CurrentSkin.Disabled);
        }

        private static MemoryStream archive(string name, bool malformed = false)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                write("skin.ini", Encoding.UTF8.GetBytes($"[General]\nName: {name}\nAuthor: OMS isolated tests\nVersion: latest\n"));
                foreach (var (path, bytes) in SkinSongSelectPageCodecTest.SongFiles())
                    write(path, malformed && path == SkinPageContracts.FILE_NAME ? Encoding.UTF8.GetBytes("{") : bytes);
                void write(string path, byte[] bytes)
                {
                    using Stream destination = zip.CreateEntry(path).Open();
                    destination.Write(bytes);
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
