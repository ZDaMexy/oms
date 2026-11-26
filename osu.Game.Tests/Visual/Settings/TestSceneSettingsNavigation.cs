// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osuTK.Input;
using SixLabors.ImageSharp;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneSettingsNavigation : OsuGameTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private ExactVisualTestIsolation? isolation;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(ExactVisualTestIsolation? testIsolation) => isolation = testIsolation;

        [Test]
        public void TestModeSettingsAndKeyBindingsAreReachableFromTheMainMenu()
        {
            AddStep("open settings with Ctrl+O", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.O);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("main settings shows BMS and mania in the sidebar", () => Game.Settings.State.Value == Visibility.Visible
                && Game.Settings.ChildrenOfType<SidebarIconButton>()
                       .Where(button => button.Section is RulesetSection)
                       .Select(button => button.Section.Header.ToString().ToLowerInvariant())
                       .ToHashSet().IsSupersetOf(new[] { "bms", "osu!mania" }));
            AddStep("jump to BMS settings", () => Game.Settings.ChildrenOfType<SidebarIconButton>()
                .Single(button => button.Section is RulesetSection && button.Section.Header.ToString().Equals("BMS", StringComparison.OrdinalIgnoreCase))
                .TriggerClick());
            captureIfRequested("settings-bms.png");

            AddStep("jump to the input section", () => Game.Settings.ChildrenOfType<SidebarIconButton>()
                .Single(button => button.Section is InputSection).TriggerClick());
            AddStep("open Configure", () => Game.Settings.ChildrenOfType<BindingSettings>().Single().ChildrenOfType<OsuButton>().Single().TriggerClick());
            AddUntilStep("key bindings shows both modes", () =>
            {
                var panel = Game.Settings.ChildrenOfType<KeyBindingPanel>().SingleOrDefault();
                return panel?.State.Value == Visibility.Visible
                       && panel.ChildrenOfType<VariantBindingsSubsection>().Any(section => section.Ruleset?.ShortName == "bms")
                       && panel.ChildrenOfType<VariantBindingsSubsection>().Any(section => section.Ruleset?.ShortName == "mania");
            });
            captureIfRequested("key-bindings.png");
        }

        private void captureIfRequested(string filename)
        {
            string? directory = Environment.GetEnvironmentVariable("OMS_UPSTREAM_UI_CAPTURE_DIRECTORY");
            if (string.IsNullOrEmpty(directory) || host is HeadlessGameHost || isolation == null)
                return;

            Task? capture = null;
            AddWaitStep("wait for the settings transition", 8);
            AddStep("capture " + filename, () => capture = saveScreenshot(Path.Combine(directory, filename)));
            AddUntilStep("screenshot saved", () => capture?.IsCompleted == true);
            AddStep("check screenshot", () => capture!.GetAwaiter().GetResult());
        }

        private async Task saveScreenshot(string path)
        {
            using var image = await host.TakeScreenshotAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("No desktop framebuffer.");
            await image.SaveAsPngAsync(path).ConfigureAwait(false);
        }
    }
}
