// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>Opt-in desktop evidence from an isolated ordinary import, never the player's save directory.</summary>
    public partial class TestSceneSkinSettingsCapture : OsuGameTestScene
    {
        public override bool AutomaticallyRunFirstStep => false;
        protected override double TimePerAction => 200;

        [Resolved] private GameHost host { get; set; } = null!;
        [Resolved] private FrameworkConfigManager frameworkConfig { get; set; } = null!;
        private SkinSection section => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().Single();

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Settings capture requires the disposable exact-test runner.");
        }

        [Test]
        public void CaptureScopedOmniSettings()
        {
            string? package = Environment.GetEnvironmentVariable("OMS_SETTINGS_CAPTURE_PACKAGE");
            string? output = Environment.GetEnvironmentVariable("OMS_SETTINGS_CAPTURE_DIRECTORY");
            if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(output))
                Assert.Ignore("Opt-in isolated settings capture requires an author package and evidence directory.");
            Task<Live<SkinInfo>?> import = null!;
            Task screenshot = null!;
            SkinManager skins = null!;
            AddStep("configure an isolated desktop framebuffer", () =>
            {
                frameworkConfig.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
                frameworkConfig.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                Directory.CreateDirectory(output!);
                skins = Game.Dependencies.Get<SkinManager>();
                // This opt-in evidence fixture imports a complete large author library before exposing settings.
                // UntilStepButton has a fixed 10-second deadline; keep the bounded desktop runner's watchdog as
                // the import limit instead of abandoning an in-flight writer and disposing its Realm underneath it.
                string disposableInput = Path.Combine(output!, "disposable-import-input.osk");
                File.Copy(package!, disposableInput, overwrite: false);
                import = Task.FromResult<Live<SkinInfo>?>(skins.Import(new ImportTask(disposableInput)).GetAwaiter().GetResult());
            });
            AddUntilStep("ordinary author import completed", () => import.IsCompleted);
            AddStep("assign private Omni only in isolated test preferences", () =>
            {
                Live<SkinInfo> target = import.GetAwaiter().GetResult()!;
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms"), target.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope("mania"), target.ID);
                Game.Settings.Show();
            });
            AddUntilStep("settings can inspect the imported package", () => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().SingleOrDefault()?.IsLoaded == true);
            AddStep("show BMS seven-key scope", () =>
            {
                section.ScopeSelector.Current.Value = new SkinSelectionScope("bms", 7);
                Game.Settings.SectionsContainer.ScrollTo(section);
            });
            AddUntilStep("BMS scope and thumbnail controls prepared", () => section.IsTargetReady && section.ChildrenOfType<SkinOptionChoiceSettings>().Any());
            AddStep("show management for the explicitly inspected skin", () => section.ChildrenOfType<SettingsButtonV2>()
                .Single(button => button.Text.ToString() == "皮肤管理与编辑").Action());
            AddWaitStep("let visible thumbnails upload", 15);
            AddStep("capture scope overview", () => screenshot = capture("settings-bms-7k.png"));
            AddUntilStep("scope overview saved", () => screenshot.IsCompletedSuccessfully);
            AddStep("show the selected style thumbnails", () => Game.Settings.SectionsContainer.ScrollTo(section.ChildrenOfType<SkinOptionChoiceSettings>().First()));
            AddWaitStep("selected style images upload", 10);
            AddStep("capture selected artwork", () => screenshot = capture("settings-selected-artwork.png"));
            AddUntilStep("selected artwork saved", () => screenshot.IsCompletedSuccessfully);
            AddStep("browse explosion artwork", () =>
            {
                SkinOptionChoiceSettings choices = section.ChildrenOfType<SkinOptionChoiceSettings>()
                    .Single(control => control.FilterTerms.Any(term => term.ToString().StartsWith("Explosion", StringComparison.Ordinal)));
                choices.BrowseButton.Action();
                Game.Settings.SectionsContainer.ScrollTo(choices);
            });
            AddWaitStep("visible gallery artwork loads", 15);
            AddStep("capture artwork browser", () => screenshot = capture("settings-artwork-browser.png"));
            AddUntilStep("artwork browser saved", () => screenshot.IsCompletedSuccessfully);
            AddStep("show mania sixteen-key scope", () =>
            {
                section.ScopeSelector.Current.Value = new SkinSelectionScope("mania", 16);
                Game.Settings.SectionsContainer.ScrollTo(section);
            });
            AddUntilStep("mania scope prepared", () => section.IsTargetReady && section.EditingScope == new SkinSelectionScope("mania", 16));
            AddStep("expand the compact mania key picker", () => section.ScopeSelector.ChildrenOfType<SkinScopeSelector.ScopeButton>()
                .Single(button => button.Text.ToString().StartsWith("16K", StringComparison.Ordinal)).Action());
            AddWaitStep("mania view settles", 10);
            AddStep("capture mania key selector", () => screenshot = capture("settings-mania-16k.png"));
            AddUntilStep("mania view saved", () => screenshot.IsCompletedSuccessfully);

            async Task capture(string name)
            {
                using Image<Rgba32>? image = await host.TakeScreenshotAsync().ConfigureAwait(false);
                if (image == null)
                    throw new InvalidOperationException("Settings capture did not return a framebuffer.");
                await image.SaveAsPngAsync(Path.Combine(output!, name)).ConfigureAwait(false);
            }
        }
    }
}
