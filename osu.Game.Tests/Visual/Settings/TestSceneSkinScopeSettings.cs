// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestSceneSkinScopeSettings : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private SkinOptionPreferences options => Game.Dependencies.Get<SkinOptionPreferences>();
        private SkinSection section => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().Single();
        private Live<SkinInfo> first = null!;
        private Live<SkinInfo> second = null!;

        private void arrangeSkins(int artworkChoiceCount = 2)
        {
            Task<Live<SkinInfo>?> importing = null!;
            AddStep("import first ordinary authored skin", () => importing = skins.Import(new ImportTask(archive("Scope Alpha", artworkChoiceCount), "alpha.osk")));
            AddUntilStep("first skin imported", () => importing.IsCompleted);
            AddStep("import second ordinary authored skin", () =>
            {
                first = importing.GetAwaiter().GetResult()!;
                importing = skins.Import(new ImportTask(archive("Scope Beta"), "beta.osk"));
            });
            AddUntilStep("second skin imported", () => importing.IsCompleted);
            AddStep("set mode defaults and show settings", () =>
            {
                second = importing.GetAwaiter().GetResult()!;
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms"), first.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope("mania"), second.ID);
                Game.Settings.Show();
            });
            AddUntilStep("settings section loaded", () => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().SingleOrDefault()?.IsLoaded == true);
            AddStep("inspect BMS seven", () =>
            {
                section.ScopeSelector.Current.Value = new SkinSelectionScope("bms", 7);
                Game.Settings.SectionsContainer.ScrollTo(section);
            });
            AddUntilStep("BMS inherited skin prepared", () => section.IsTargetReady && section.DisplayedSkinId == first.ID);
            AddAssert("mode and key buttons fit their rows", () => section.ScopeSelector.ChildrenOfType<SkinScopeSelector.ScopeButton>()
                .All(button => button.DrawHeight <= 40));
        }

        [Test]
        public void TestBrowsingAndAssigningAnInactiveScopeNeverChangesModeChartOrCurrentSkin()
        {
            Guid originalSkin = default;
            string originalMode = string.Empty;
            Guid originalChart = default;
            arrangeSkins();
            AddUntilStep("global skin settles", () => !skins.CurrentSkin.Disabled);
            AddStep("remember actual gameplay context", () =>
            {
                originalSkin = skins.CurrentSkinInfo.Value.ID;
                originalMode = Game.Ruleset.Value.ShortName;
                originalChart = Game.Beatmap.Value.BeatmapInfo.ID;
                section.ScopeSelector.Current.Value = new SkinSelectionScope("mania", 14);
            });
            AddUntilStep("other mode target is prepared", () => section.IsTargetReady && section.DisplayedSkinId == second.ID);
            AddAssert("dual-stage declaration appears for its total key count", () => section.ChildrenOfType<FormDropdown<string>>()
                .Any(control => control.Caption.ToString() == "双侧专用"));
            AddStep("assign first skin to mania fourteen", () => section.ChildrenOfType<FormDropdown<Live<SkinInfo>>>().Single().Current.Value = first);
            AddUntilStep("only target assignment saved", () => skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("mania", 14)) == first.ID
                && section.IsTargetReady && section.DisplayedSkinId == first.ID);
            AddAssert("global ruleset, chart and skin are untouched", () => Game.Ruleset.Value.ShortName == originalMode
                && Game.Beatmap.Value.BeatmapInfo.ID == originalChart && skins.CurrentSkinInfo.Value.ID == originalSkin);
            AddAssert("both mode defaults and other key inheritance remain", () => skins.SelectionPreferences!.GetEffectiveSkinId(new SkinSelectionScope("bms")) == first.ID
                && skins.SelectionPreferences.GetEffectiveSkinId(new SkinSelectionScope("mania")) == second.ID
                && skins.SelectionPreferences.GetConfiguredSkinId(new SkinSelectionScope("mania", 7)) == null);
            AddStep("request deletion of that explicit target", () => section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single().Action());
            AddUntilStep("confirmation names the inspected skin", () => Game.ChildrenOfType<SkinSection.SkinDeleteDialog>().SingleOrDefault()?.RecordId == first.ID);
            AddStep("dismiss without deleting files", () => Game.ChildrenOfType<SkinSection.SkinDeleteDialog>().Single().Hide());
            AddStep("restore inheritance with the actual scope button", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString() == "皮肤选择恢复跟随模式默认").Action());
            AddUntilStep("mania fourteen follows its own default again", () => section.IsTargetReady && section.DisplayedSkinId == second.ID
                && skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("mania", 14)) == null);
            AddStep("pin the already displayed inherited skin", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString() == "将当前皮肤设为 14K 专用").Action());
            AddUntilStep("same-value assignment is independently stored", () => skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("mania", 14)) == second.ID);
        }

        [TestCase("bms", 7)]
        [TestCase("mania", 14)]
        public void TestDeleteActionIsDirectlyVisibleForTheInspectedSkin(string mode, int keys)
        {
            Guid expectedTarget = default;
            arrangeSkins();
            AddStep("inspect the requested skin scope", () =>
            {
                expectedTarget = mode == "mania" ? second.ID : first.ID;
                section.ScopeSelector.Current.Value = new SkinSelectionScope(mode, keys);
            });
            AddUntilStep("the requested skin and its action are ready", () => section.IsTargetReady && section.DisplayedSkinId == expectedTarget
                && deleteButton().IsLoaded);
            AddAssert("delete is directly available in the card", () =>
            {
                for (Drawable? ancestor = deleteButton(); ancestor != null; ancestor = ancestor.Parent)
                {
                    if (!ancestor.IsPresent)
                        return false;
                }

                return deleteButton().Enabled.Value;
            });
            AddStep("scroll the visible delete action into view", () => Game.Settings.SectionsContainer.ScrollTo(deleteButton()));
            AddUntilStep("delete is reachable by the mouse", () => !isMaskedAway(deleteButton()));
            AddStep("click the normal card delete action", () =>
            {
                InputManager.MoveMouseTo(deleteButton());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("confirmation belongs to the inspected skin", () => Game.ChildrenOfType<SkinSection.SkinDeleteDialog>().SingleOrDefault()?.RecordId == expectedTarget);
            AddStep("dismiss the confirmation without deleting", () => Game.ChildrenOfType<SkinSection.SkinDeleteDialog>().Single().Hide());

            SkinSection.DeleteSkinButton deleteButton() => section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single();
        }

        [Test]
        public void TestOlderDeletionCompletionPreservesANewerSkinChoice()
        {
            Live<SkinInfo> replacement = null!;
            arrangeSkins();
            AddStep("choose another skin while an earlier deletion completes", () =>
            {
                Action<Guid> completed = section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single().DeleteCompleted;
                replacement = skins.CurrentSkinInfo.Value.ID == second.ID ? skins.DefaultOmsSkin.SkinInfo : second;
                using SkinManagedFolderOperationCoordinator.Lease reservation = skins.ManagedFolderOperationCoordinator.EnterMutation();
                section.ChildrenOfType<FormDropdown<Live<SkinInfo>>>().Single().Current.Value = replacement;
                completed(first.ID);
            });
            AddUntilStep("the current request is ready", () => section.IsTargetReady);
            AddAssert("the newer choice is displayed and saved", () => section.DisplayedSkinId == replacement.ID
                && skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("bms", 7)) == replacement.ID);
            AddAssert("the mode default is unchanged", () => skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("bms")) == first.ID);
        }

        [Test]
        public void TestBuiltInSkinKeepsDeletionUnavailable()
        {
            arrangeSkins();
            AddStep("inspect the built-in skin in this key scope", () => skins.SelectionPreferences!.SetSkin(
                new SkinSelectionScope("bms", 7), SkinInfo.OMS_SKIN));
            AddUntilStep("the built-in skin is prepared", () => section.IsTargetReady && section.DisplayedSkinId == SkinInfo.OMS_SKIN);
            AddAssert("the protected skin never gains a delete action", () =>
            {
                SkinSection.DeleteSkinButton button = section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single();
                return !button.CanBeShown.Value && !button.Enabled.Value && !skins.CanDelete(SkinInfo.OMS_SKIN);
            });
        }

        [Test]
        public void TestRestoringInheritanceCanCancelAPreparingKeyTarget()
        {
            arrangeSkins();
            AddStep("restore a key assignment while its package is preparing", () =>
            {
                var five = new SkinSelectionScope("bms", 5);
                skins.SelectionPreferences!.SetSkin(five, second.ID);
                section.ScopeSelector.Current.Value = five;
                Assert.That(section.IsTargetReady, Is.False);
                SettingsButtonV2 restore = section.ChildrenOfType<SettingsButtonV2>()
                    .Single(button => button.Text.ToString() == "皮肤选择恢复跟随模式默认");
                Assert.That(restore.Enabled.Value, Is.True);
                restore.Action();
            });
            AddUntilStep("inherited skin wins and the stale target is not saved", () => section.IsTargetReady && section.DisplayedSkinId == first.ID
                && skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("bms", 5)) == null);
            AddAssert("mode default remains unchanged", () => skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope("bms")) == first.ID);
        }

        [Test]
        public void TestKeyOptionsCanOverrideAnInheritedValueWithTheAuthorDefaultAndResetLocally()
        {
            arrangeSkins();
            AddStep("change BMS mode option", () => options.SetOverride(first.ID, "bms", "notes", "alternate"));
            AddUntilStep("seven inherits the mode option", () => notes().Value == "alternate");
            AddStep("choose author default only for seven", () => notes().Value = "normal");
            AddUntilStep("author default saved as a real key override", () => options.GetLocalOverrides(first.ID, "bms", 7).GetValueOrDefault("notes") == "normal");
            AddAssert("other keys and mode preference remain alternate", () => options.GetOverrides(first.ID, "bms", 5)["notes"] == "alternate"
                && options.GetOverrides(first.ID, "bms")["notes"] == "alternate");
            AddStep("reset only the artwork selection field", () =>
            {
                SkinOptionChoiceSettings notesControl = section.ChildrenOfType<SkinOptionChoiceSettings>().Single(control => control.OptionId == "notes");
                notesControl.ChildrenOfType<SettingsItemV2>().Single(item => ReferenceEquals(item.Control, notesControl.SelectionButton)).ApplyDefault();
            });
            AddUntilStep("artwork field reset follows the inherited choice", () => !options.GetLocalOverrides(first.ID, "bms", 7).ContainsKey("notes")
                && notes().Value == "alternate");
            AddStep("customise the same field again", () => notes().Value = "normal");
            AddUntilStep("customisation is stored again", () => options.GetLocalOverrides(first.ID, "bms", 7).GetValueOrDefault("notes") == "normal");
            AddStep("reset only seven options", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString().StartsWith("清除 7K 单独配置", StringComparison.Ordinal)).Action());
            AddUntilStep("seven follows mode again", () => options.GetLocalOverrides(first.ID, "bms", 7).Count == 0 && notes().Value == "alternate");

            Bindable<string> notes() => section.ChildrenOfType<SkinOptionChoiceSettings>().Single(control => control.OptionId == "notes").Current;
        }

        [Test]
        public void TestGlobalSearchHidesCompleteThumbnailControlsAndFindsVisibleActions()
        {
            arrangeSkins();
            AddStep("inspect a dual-stage mania scope", () => section.ScopeSelector.Current.Value = new SkinSelectionScope("mania", 14));
            AddUntilStep("target options prepared", () => section.IsTargetReady && section.ChildrenOfType<SkinOptionChoiceSettings>().Any());
            AddStep("search for another option", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "双侧专用");
            AddUntilStep("whole notes thumbnail row is hidden", () => section.ChildrenOfType<SkinOptionChoiceSettings>().All(control => !control.IsPresent));
            AddStep("search for the visible delete action", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "删除皮肤文件");
            AddUntilStep("delete is a visible search result", () => section.MatchingFilter
                && section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single().IsPresent);
            AddStep("search for a collapsed gallery pager", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "下一页");
            AddUntilStep("hidden gallery controls do not create invisible search hits", () => !section.MatchingFilter);
            AddStep("clear search", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = string.Empty);
            AddUntilStep("option including its thumbnail returns", () => section.ChildrenOfType<SkinOptionChoiceSettings>().Any(control => control.IsPresent));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestArtworkGallerySelectionUsesTheKeyScope(bool clickArrow)
        {
            arrangeSkins();
            AddStep("scroll the normal notes setting into view", () => Game.Settings.SectionsContainer.ScrollTo(notes()));
            AddUntilStep("normal notes setting accepts input", () => Game.Settings.SectionsContainer.SelectedSection.Value == section
                && !isMaskedAway(notes().SelectionButton));
            AddAssert("there is no competing text-only dropdown", () => !notes().ChildrenOfType<FormDropdown<string>>().Any());
            AddStep(clickArrow ? "click the ordinary notes arrow" : "click the ordinary notes setting", () =>
            {
                if (clickArrow)
                    InputManager.MoveMouseTo(notes().SelectionArrow);
                else
                    InputManager.MoveMouseTo(notes().SelectionButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("both named artwork cards are visible and loaded", () => notes().IsExpanded
                && notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>().Count(control => control.IsPresent
                    && control.ChildrenOfType<SkinOptionPreviewImage>().Any(image => image.IsLoaded)) == 2);
            AddStep("click the alternate artwork card", () =>
            {
                InputManager.MoveMouseTo(notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>().Single(control => control.Value == "alternate"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("only seven-key preference changes", () => options.GetLocalOverrides(first.ID, "bms", 7).GetValueOrDefault("notes") == "alternate"
                && !options.GetOverrides(first.ID, "bms").ContainsKey("notes"));
            AddUntilStep("normal setting shows the saved choice and closes the gallery", () => notes().Current.Value == "alternate" && !notes().IsExpanded);
            AddAssert("other key counts still inherit the original notes", () => !options.GetOverrides(first.ID, "bms", 5).ContainsKey("notes"));

            SkinOptionChoiceSettings notes() => section.ChildrenOfType<SkinOptionChoiceSettings>().Single(control => control.OptionId == "notes");
        }

        [Test]
        public void TestArtworkGallerySearchDoesNotSaveUntilAnArtworkIsClicked()
        {
            arrangeSkins();
            AddStep("scroll the normal notes setting into view", () => Game.Settings.SectionsContainer.ScrollTo(notes()));
            AddUntilStep("normal notes setting accepts input", () => Game.Settings.SectionsContainer.SelectedSection.Value == section
                && !isMaskedAway(notes().SelectionButton));
            AddStep("open styles from the normal setting", () =>
            {
                InputManager.MoveMouseTo(notes().SelectionButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("artwork gallery opens", () => notes().IsExpanded && notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>().Count() == 2);
            AddStep("filter the artwork cards", () => notes().ChildrenOfType<FormTextBox>().Single().Current.Value = "另一套");
            AddUntilStep("only the matching artwork remains", () => notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>().SingleOrDefault()?.Value == "alternate");
            AddAssert("browsing has not changed this key's appearance", () => notes().Current.Value == "normal" && options.GetLocalOverrides(first.ID, "bms", 7).Count == 0);
            AddStep("clear the gallery filter", () => notes().ChildrenOfType<FormTextBox>().Single().Current.Value = string.Empty);
            AddUntilStep("all artwork choices return", () => notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>().Count() == 2);
            AddStep("close with the same normal selection control", () =>
            {
                InputManager.MoveMouseTo(notes().SelectionButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("gallery closes without saving a choice", () => !notes().IsExpanded
                && options.GetLocalOverrides(first.ID, "bms", 7).Count == 0);

            SkinOptionChoiceSettings notes() => section.ChildrenOfType<SkinOptionChoiceSettings>().Single(control => control.OptionId == "notes");
        }

        [Test]
        public void TestLargeArtworkGalleryOnlyBuildsTheCurrentPageAndReleasesPreviousCards()
        {
            arrangeSkins(26);
            SkinOptionChoiceSettings.ChoiceCard[] firstPage = null!;
            SkinOptionChoiceSettings.ChoiceCard[] lastPage = null!;
            AddStep("scroll large artwork selector into view", () => Game.Settings.SectionsContainer.ScrollTo(notes()));
            AddUntilStep("artwork selector is visible", () => !isMaskedAway(notes().SelectionButton));
            AddAssert("collapsed gallery has no artwork cards", () => !cards().Any());
            AddStep("open large gallery", () => notes().SelectionButton.Action());
            AddUntilStep("only twelve cards are created", () => notes().IsExpanded && cards().Count() == 12);
            AddStep("advance one page", () =>
            {
                firstPage = cards().ToArray();
                next().Action();
            });
            AddUntilStep("old cards released and next twelve created", () => firstPage.All(isDisposed) && cards().Count() == 12);
            AddStep("advance to last page", () => next().Action());
            AddUntilStep("only last two cards remain", () => cards().Count() == 2);
            AddAssert("paging never saves a selection", () => notes().Current.Value == "normal" && options.GetLocalOverrides(first.ID, "bms", 7).Count == 0);
            AddStep("collapse gallery", () =>
            {
                lastPage = cards().ToArray();
                notes().SelectionButton.Action();
            });
            AddUntilStep("all page cards released", () => !notes().IsExpanded && !cards().Any() && lastPage.All(isDisposed));

            SkinOptionChoiceSettings notes() => section.ChildrenOfType<SkinOptionChoiceSettings>().Single(control => control.OptionId == "notes");
            IEnumerable<SkinOptionChoiceSettings.ChoiceCard> cards() => notes().ChildrenOfType<SkinOptionChoiceSettings.ChoiceCard>();
            RoundedButton next() => notes().ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == "下一页");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestActualPlayerLoadsTheSkinAndOptionsForItsFinalKeys(bool mania, bool converted)
        {
            Ruleset ruleset = mania ? new ManiaRuleset() : new BmsRuleset();
            int keys = mania && !converted ? 4 : 7;
            var scope = new SkinSelectionScope(ruleset.ShortName, keys);
            Player player = null!;
            string expectedNote = "alternate";
            Task<bool> liveSelection = null!;
            arrangeSkins();
            AddStep("assign a distinct key skin and configure its notes", () =>
            {
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope(ruleset.ShortName), second.ID);
                skins.SelectionPreferences.SetSkin(scope, first.ID);
                options.SetOverride(first.ID, ruleset.ShortName, "notes", "alternate", keyCount: keys);
                Game.Settings.Hide();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                IBeatmap beatmap = mania && !converted
                    ? new ManiaBeatmap(new StageDefinition(4))
                    {
                        BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                        Difficulty = { CircleSize = 4 },
                        HitObjects = { new Note { Column = 0, StartTime = 60_000 } },
                    }
                    : new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(
                        "#TITLE Scoped skin entry\n#BPM 120\n#WAV01 note.wav\n#03011:01\n#03016:01\n#03019:01\n", "scoped-entry.bme"))
                    { BeatmapInfo = { Ruleset = new BmsRuleset().RulesetInfo } };
                Game.Beatmap.Value = CreateWorkingBeatmap(beatmap);
                Game.SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddUntilStep("menu skin projection settles before gameplay", () => !skins.CurrentSkin.Disabled);
            AddStep("enter through the real loading screen", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData))));
            AddUntilStep("final scoped player is ready", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player) && player.IsLoaded
                    && player.LoadedBeatmapSuccessfully && player.ChildrenOfType<DrawableRuleset>().SingleOrDefault()?.IsLoaded == true;
            });
            AddAssert("player owns the exact key skin and frozen option", ownsKeySkin);
            AddStep("attempt a different skin during real gameplay", () => liveSelection = skins.SelectSkinForScopeAsync(scope, second));
            AddUntilStep("live selection is rejected without saving", () => liveSelection.IsCompletedSuccessfully && !liveSelection.GetAwaiter().GetResult()
                && skins.CurrentSkinInfo.Value.ID == first.ID && skins.SelectionPreferences!.GetConfiguredSkinId(scope) == first.ID);
            AddStep("change the saved option while this root is live", () => options.SetOverride(first.ID, ruleset.ShortName, "notes", "normal", keyCount: keys));
            AddAssert("the current root retains its frozen appearance", ownsKeySkin);
            Player initialPlayer = null!;
            AddStep("retry through the actual loader", () =>
            {
                initialPlayer = player;
                expectedNote = "normal";
                Assert.That(player.Restart(true), Is.True);
            });
            AddUntilStep("retried player is ready", () => player != initialPlayer && player.IsLoaded && player.LoadedBeatmapSuccessfully
                && ReferenceEquals(Game.ScreenStack.CurrentScreen, player));
            AddAssert("retry retains the key skin without rewriting its mode default", ownsKeySkin);
            AddStep("exit actual gameplay", () => player.ChildrenOfType<HotkeyExitOverlay>().Single().Action());
            AddUntilStep("returned to menu", () => Game.ScreenStack.CurrentScreen is MainMenu);

            bool ownsKeySkin()
            {
                GameplaySkinLayoutRevisionOwner owner = player.ChildrenOfType<DrawableRuleset>().Single().Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                return skins.CurrentSkinScope == scope && skins.CurrentSkinInfo.Value.ID == first.ID
                    && owner.PackageRevision.RetainsExact(skins.CurrentRevision)
                    && owner.CurrentPublication!.Snapshot.Context.OptionValues.TryGetValue("notes", out GameplaySkinOptionValue note)
                    && note.CanonicalValue == expectedNote
                    && skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope(ruleset.ShortName)) == second.ID;
            }
        }

        private static bool isMaskedAway(Drawable drawable)
            => (bool)typeof(Drawable).GetProperty("IsMaskedAway", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;

        private static bool isDisposed(Drawable drawable)
            => (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;

        private static MemoryStream archive(string name, int artworkChoiceCount = 2)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                write("skin.ini", Encoding.UTF8.GetBytes($"[General]\nName: {name}\nAuthor: OMS test fixture\nVersion: 2.7\n"));
                var manifest = new JObject
                {
                    ["contract"] = "oms-gameplay-skin-manifest.v1",
                    ["scene"] = "gameplay-skin.scene.json",
                    ["sceneContract"] = "oms-gameplay-skin-scene.v1",
                    ["eventContract"] = "oms-gameplay-skin-event.v1",
                    ["resources"] = new JArray(),
                    ["options"] = new JArray(
                        new JObject { ["id"] = "notes", ["label"] = "音符", ["type"] = "choice", ["default"] = "normal", ["choices"] = choices(count: artworkChoiceCount) },
                        new JObject { ["id"] = "dual", ["label"] = "双侧专用", ["type"] = "choice", ["default"] = "normal", ["rulesets"] = new JArray("mania"), ["keymodes"] = new JArray("7k-7k"), ["choices"] = choices(false) }),
                };
                write(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, Encoding.UTF8.GetBytes(manifest.ToString()));
                write("gameplay-skin.scene.json", Encoding.UTF8.GetBytes("""
                    {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"root","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]},"tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                    """));
                foreach ((string value, Rgba32 colour) in new[] { ("normal", new Rgba32(220, 100, 80)), ("alternate", new Rgba32(80, 120, 220)) })
                {
                    using var image = new Image<Rgba32>(8, 8, colour);
                    using var pixels = new MemoryStream();
                    image.SaveAsPng(pixels);
                    write("previews/" + value + ".png", pixels.ToArray());
                }
                void write(string path, byte[] bytes) { using Stream stream = zip.CreateEntry(path).Open(); stream.Write(bytes); }
                static JArray choices(bool previews = true, int count = 2)
                {
                    var result = new JArray(new JObject { ["value"] = "normal", ["label"] = "原版" },
                        new JObject { ["value"] = "alternate", ["label"] = "另一套" });
                    for (int index = 2; index < count; index++)
                        result.Add(new JObject { ["value"] = $"sample.n{index:D3}", ["label"] = $"样式 {index:D3}" });
                    if (previews)
                        foreach (JObject choice in result)
                            choice["preview"] = choice["value"]!.Value<string>() == "alternate" ? "previews/alternate.png" : "previews/normal.png";
                    return result;
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
