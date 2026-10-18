// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
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

        private void arrangeSkins()
        {
            Task<Live<SkinInfo>?> importing = null!;
            AddStep("import first ordinary authored skin", () => importing = skins.Import(new ImportTask(archive("Scope Alpha"), "alpha.osk")));
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
            AddStep("expand management for the inspected skin", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString().StartsWith("皮肤管理与编辑", StringComparison.Ordinal)).Action());
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
            AddUntilStep("seven inherits the mode option", () => notes().Current.Value == "alternate");
            AddStep("choose author default only for seven", () => notes().Current.Value = "normal");
            AddUntilStep("author default saved as a real key override", () => options.GetLocalOverrides(first.ID, "bms", 7).GetValueOrDefault("notes") == "normal");
            AddAssert("other keys and mode preference remain alternate", () => options.GetOverrides(first.ID, "bms", 5)["notes"] == "alternate"
                && options.GetOverrides(first.ID, "bms")["notes"] == "alternate");
            AddStep("reset only seven options", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString().StartsWith("清除 7K 单独配置", StringComparison.Ordinal)).Action());
            AddUntilStep("seven follows mode again", () => options.GetLocalOverrides(first.ID, "bms", 7).Count == 0 && notes().Current.Value == "alternate");

            FormDropdown<string> notes() => section.ChildrenOfType<FormDropdown<string>>().Single(control => control.Caption.ToString() == "音符");
        }

        [Test]
        public void TestGlobalSearchHidesTheCompleteThumbnailControlAndCollapsedActions()
        {
            arrangeSkins();
            AddStep("inspect a dual-stage mania scope", () => section.ScopeSelector.Current.Value = new SkinSelectionScope("mania", 14));
            AddUntilStep("target options prepared", () => section.IsTargetReady && section.ChildrenOfType<SkinOptionChoiceSettings>().Any());
            AddStep("search for another option", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "双侧专用");
            AddUntilStep("whole notes thumbnail row is hidden", () => section.ChildrenOfType<SkinOptionChoiceSettings>().All(control => !control.IsPresent));
            AddStep("search for a collapsed management action", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "删除皮肤文件");
            AddUntilStep("collapsed action does not create an invisible search hit", () => !section.MatchingFilter);
            AddStep("clear search", () => Game.Settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = string.Empty);
            AddUntilStep("option including its thumbnail returns", () => section.ChildrenOfType<SkinOptionChoiceSettings>().Any(control => control.IsPresent));
        }

        [Test]
        public void TestArtworkGallerySelectionUsesTheKeyScope()
        {
            arrangeSkins();
            AddStep("open the artwork browser", () => section.ChildrenOfType<SkinOptionChoiceSettings>().Single().BrowseButton.Action());
            AddUntilStep("two actual choice cards are visible", () => section.ChildrenOfType<SkinOptionChoiceSettings>().Single()
                .ChildrenOfType<ClickableContainer>().Count(control => control.IsPresent && control.ChildrenOfType<SkinOptionPreviewImage>().Any()) >= 2);
            AddStep("select the second artwork", () => section.ChildrenOfType<SkinOptionChoiceSettings>().Single().ChildrenOfType<ClickableContainer>()
                .Last(control => control.IsPresent && control.ChildrenOfType<SkinOptionPreviewImage>().Any()).Action());
            AddUntilStep("only seven-key preference changes", () => options.GetLocalOverrides(first.ID, "bms", 7).GetValueOrDefault("notes") == "alternate"
                && !options.GetOverrides(first.ID, "bms").ContainsKey("notes"));
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
                        BeatmapInfo = { Ruleset = ruleset.RulesetInfo }, Difficulty = { CircleSize = 4 },
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

        private static MemoryStream archive(string name)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                write("skin.ini", Encoding.UTF8.GetBytes($"[General]\nName: {name}\nAuthor: OMS test fixture\nVersion: 2.7\n"));
                var manifest = new JObject
                {
                    ["contract"] = "oms-gameplay-skin-manifest.v1", ["scene"] = "gameplay-skin.scene.json",
                    ["sceneContract"] = "oms-gameplay-skin-scene.v1", ["eventContract"] = "oms-gameplay-skin-event.v1", ["resources"] = new JArray(),
                    ["options"] = new JArray(
                        new JObject { ["id"] = "notes", ["label"] = "音符", ["type"] = "choice", ["default"] = "normal", ["choices"] = choices() },
                        new JObject { ["id"] = "dual", ["label"] = "双侧专用", ["type"] = "choice", ["default"] = "normal", ["rulesets"] = new JArray("mania"), ["keymodes"] = new JArray("7k-7k"), ["choices"] = choices(false) }),
                };
                write(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, Encoding.UTF8.GetBytes(manifest.ToString()));
                write("gameplay-skin.scene.json", Encoding.UTF8.GetBytes("""
                    {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"root","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]},"tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                    """));
                using var image = new Image<Rgba32>(8, 8, new Rgba32(220, 100, 80));
                using var pixels = new MemoryStream();
                image.SaveAsPng(pixels);
                write("previews/sample.png", pixels.ToArray());
                void write(string path, byte[] bytes) { using Stream stream = zip.CreateEntry(path).Open(); stream.Write(bytes); }
                static JArray choices(bool previews = true)
                {
                    var result = new JArray(new JObject { ["value"] = "normal", ["label"] = "原版" },
                        new JObject { ["value"] = "alternate", ["label"] = "另一套" });
                    if (previews)
                        foreach (JObject choice in result)
                            choice["preview"] = "previews/sample.png";
                    return result;
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
