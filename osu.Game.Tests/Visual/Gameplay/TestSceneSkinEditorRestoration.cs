// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneSkinEditorRestoration : PlayerTestScene
    {
        [Cached]
        public readonly EditorClipboard Clipboard = new EditorClipboard();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private SkinEditor editor = null!;

        protected override bool Autoplay => true;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [Test]
        public void TestDragAndPropertyEditsSurviveSavingWithoutMutatingPlayingSkin()
        {
            Skin original = null!;
            SkinnableContainer target = null!;
            BarHitErrorMeter meter = null!;
            Vector2 savedPosition = default;

            AddUntilStep("HUD loaded", () => Player.ChildrenOfType<SkinnableContainer>().Any()
                && Player.ChildrenOfType<SkinnableContainer>().All(t => t.ComponentsLoaded));
            AddStep("open original editor", () =>
            {
                original = skins.CurrentSkin.Value;
                Player.ScaleTo(0.4f);
                LoadComponentAsync(editor = new SkinEditor(Player), Add);
            });
            AddUntilStep("editor ready", () => editor.IsLoaded && editor.DraftSkin != null);
            AddStep("show editor", () => editor.Show());
            AddUntilStep("component controls available", () => editor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                .Any(b => b.ChildrenOfType<BarHitErrorMeter>().Any()));
            AddStep("add hit error meter through toolbox", () => editor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                .First(b => b.ChildrenOfType<BarHitErrorMeter>().Any()).TriggerClick());
            AddUntilStep("new component selected", () =>
            {
                meter = editor.SelectedComponents.OfType<BarHitErrorMeter>().SingleOrDefault()!;
                if (meter?.IsLoaded != true)
                    return false;
                target = Player.ChildrenOfType<SkinnableContainer>().Single(t => t.Components.Contains(meter));
                return true;
            });
            AddStep("start dragging selected component", () =>
            {
                savedPosition = meter.Position;
                InputManager.MoveMouseTo(meter.ScreenSpaceDrawQuad.Centre);
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("move component", () => InputManager.MoveMouseTo(meter.ScreenSpaceDrawQuad.Centre + new Vector2(45, 25)));
            AddStep("finish drag", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("drag changes position", () => meter.Position != savedPosition);
            AddStep("drop an image into the editor", () =>
            {
                using (Stream input = TestResources.OpenResource("Textures/test-image.png"))
                using (Stream output = LocalStorage.GetStream("editor-image.png", FileAccess.Write, FileMode.Create))
                    input.CopyTo(output);
                _ = editor.Import(LocalStorage.GetFullPath("editor-image.png"));
            });
            AddUntilStep("imported image renders from editable copy", () =>
                editor.SelectedComponents.OfType<SkinnableSprite>().Any(s => s.Drawable is Sprite { Texture: not null }));
            AddStep("change editable property and save", () =>
            {
                meter.JudgementLineThickness.Value = 6;
                savedPosition = meter.Position;
                editor.Save();
            });
            AddAssert("playing skin is still the original", () => ReferenceEquals(original, skins.CurrentSkin.Value));
            AddStep("reopen saved skin from its stored files", () =>
            {
                using Skin reopened = editor.DraftSkin!.SkinInfo.PerformRead(skins.GetSkin);
                using var layout = (Container)reopened.GetDrawableComponent(new UserSkinComponentLookup(target.Lookup))!;
                BarHitErrorMeter saved = layout.Children.OfType<BarHitErrorMeter>().Last();
                Assert.Multiple(() =>
                {
                    Assert.That(saved.Position, Is.EqualTo(savedPosition));
                    Assert.That(saved.JudgementLineThickness.Value, Is.EqualTo(6));
                    Assert.That(reopened.SkinInfo.ID, Is.Not.EqualTo(original.SkinInfo.ID));
                    Assert.That(reopened.TryCaptureGameplaySkinResource("editor-image.png", 1024 * 1024, out byte[] imageBytes), Is.True);
                    Assert.That(imageBytes, Is.EqualTo(File.ReadAllBytes(LocalStorage.GetFullPath("editor-image.png"))));
                });
            });
            AddStep("close local editing preview", () =>
            {
                editor.EndPreview();
                editor.Expire();
            });
        }
    }
}
