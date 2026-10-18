// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Navigation
{
    [HeadlessTest]
    public partial class TestSceneSkinEditorApply : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private SkinEditorOverlay overlay => Game.Dependencies.Get<SkinEditorOverlay>();
        private SkinEditor editor => overlay.ChildrenOfType<SkinEditor>().Single();

        [TestCase(false)]
        [TestCase(true)]
        public void TestSavedGameplayLayoutAppliesAfterClosingPreviewToOnlyEditedMode(bool perKey)
        {
            Task<Live<BeatmapSetInfo>?> import = null!;
            Guid sourceId = default;
            Guid draftId = default;
            string otherModePreference = string.Empty;
            string modePreference = string.Empty;
            SkinSelectionScope scope = default;
            Task<SkinEditorOpenResult>? opening = null;

            AddStep("import existing mania chart fixture", () => import = Game.BeatmapManager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())));
            AddUntilStep("chart import finishes", () => import.IsCompleted);
            AddStep("select mania chart and mode", () =>
            {
                Guid setId = import.GetAwaiter().GetResult()!.ID;
                BeatmapInfo chart = Game.BeatmapManager.GetAllUsableBeatmapSets().Single(s => s.ID == setId).Beatmaps.First(b => b.Ruleset.ShortName == "mania");
                Game.Ruleset.Value = chart.Ruleset;
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(chart);
            });
            AddUntilStep("mania selection settles", () => Game.Ruleset.Value.ShortName == "mania"
                && !skins.CurrentSkin.Disabled && overlay.IsLoaded);
            AddStep("open original editor", () =>
            {
                sourceId = skins.CurrentSkinInfo.Value.ID;
                otherModePreference = Game.LocalConfig.Get<string>(OsuSetting.SkinBms);
                modePreference = Game.LocalConfig.Get<string>(OsuSetting.SkinMania);
                scope = new SkinSelectionScope("mania", perKey
                    ? Game.Ruleset.Value.CreateInstance().GetGameplaySkinKeyCount(Game.Beatmap.Value.BeatmapInfo, Game.SelectedMods.Value) : null);
                if (perKey)
                    opening = overlay.OpenForScopeAsync(scope);
                else
                    overlay.Show();
            });
            AddUntilStep("explicit scope request finishes", () => !perKey || opening?.IsCompletedSuccessfully == true);
            AddAssert("explicit scope opens the original editor", () => !perKey || opening!.GetAwaiter().GetResult() == SkinEditorOpenResult.Opened);
            AddUntilStep("gameplay preview and editable targets ready", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player player && player.IsLoaded
                    && overlay.ChildrenOfType<SkinEditor>().Any(e => e.DraftSkin != null && e.CanFinishEditing)
                    && player.ChildrenOfType<SkinnableContainer>().Any()
                    && player.ChildrenOfType<SkinnableContainer>().All(c => c.ComponentsLoaded)
                    && editor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().Any(b => b.IsLoaded);
            });
            AddStep("add component through editor toolbox", () =>
            {
                draftId = editor.DraftSkin!.SkinInfo.ID;
                editor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First(b => b.IsLoaded).TriggerClick();
            });
            AddUntilStep("new component selected and loaded", () => editor.SelectedComponents.Count == 1
                && Game.ScreenStack.CurrentScreen is Player player
                && player.ChildrenOfType<SkinnableContainer>().All(c => c.ComponentsLoaded));
            AddStep("save while preview retains original skin", () =>
            {
                editor.Save();
                Assert.Multiple(() =>
                {
                    Assert.That(editor.HasSavedChanges, Is.True);
                    Assert.That(editor.DraftSkin!.LayoutInfos, Is.Not.Empty);
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(sourceId));
                });
            });
            AddStep("close editor", () => overlay.Hide());
            AddUntilStep("preview closes and saved skin applies", () => overlay.State.Value == Visibility.Hidden
                && Game.ScreenStack.CurrentScreen is not Player
                && skins.CurrentSkinInfo.Value.ID == draftId);
            AddUntilStep("edited scope preference persists", () => skins.SelectionPreferences!.GetConfiguredSkinId(scope) == draftId);
            AddAssert("key edit leaves mode default intact", () => !perKey || Game.LocalConfig.Get<string>(OsuSetting.SkinMania) == modePreference);
            AddAssert("other mode preference stays unchanged", () => Game.LocalConfig.Get<string>(OsuSetting.SkinBms) == otherModePreference);
            AddAssert("applied skin contains saved layout", () => skins.CurrentSkin.Value.LayoutInfos.Count > 0);
        }
    }
}
