// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    /// <summary>Opt-in desktop evidence through ordinary import, exact publication, real autoplay and results.</summary>
    [TestFixture]
    public partial class TestSceneManiaAuthorSkinCapture : OsuGameTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private Task<Live<SkinInfo>>? import;
        private Task? capture;
        private string output = null!;
        private Player player = null!;
        private DrawableManiaRuleset renderer = null!;
        private ScoreProcessor score = null!;
        private int columns;
        private bool dual;
        private readonly Dictionary<string, (float Width, float Height)> effectGeometry = new Dictionary<string, (float, float)>();

        public override bool AutomaticallyRunFirstStep => false;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Author capture requires the disposable --exact-test runner.");
        }

        [Test]
        public void CaptureImportedManiaPackageThroughCompletePlayer()
        {
            AddStep("import an author package into disposable desktop storage", () =>
            {
                if (host is HeadlessGameHost)
                    Assert.Ignore("Opt-in desktop pixel evidence.");
                string? package = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PACKAGE");
                string? directory = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY");
                if (package == null || directory == null)
                    Assert.Ignore("Set OMS_AUTHOR_SKIN_PACKAGE and OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY.");
                output = Path.GetFullPath(directory!);
                Directory.CreateDirectory(output);
                int total = int.Parse(Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_KEYMODE")!.TrimEnd('K', 'k'));
                if (total is not (5 or 6 or 7 or 8 or 14 or 16))
                    throw new InvalidOperationException("Unsupported author capture key count.");
                dual = total > 8;
                columns = dual ? total / 2 : total;
                var framework = Game.Dependencies.Get<FrameworkConfigManager>();
                framework.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                framework.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
                import = Game.Dependencies.Get<SkinManager>().Import(new PreservingImportTask(File.OpenRead(package!), Path.GetFileName(package!)));
            });
            AddUntilStep("wait for normal import", () => import?.IsCompleted == true);
            AddStep("select native mania and a chart with exact stage definitions", () =>
            {
                var ruleset = new ManiaRuleset();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createChart(ruleset));
                Game.SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddStep("select the normally imported skin", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value = import!.GetAwaiter().GetResult());
            AddUntilStep("wait for coherent selection", () => Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.SkinInfo.ID == import!.GetAwaiter().GetResult().ID);
            if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_SETTINGS") == "omni")
            {
                SettingsOverlay settings = null!;
                FormSliderBar<double> originalScale = null!;
                AddStep("open the complete skin configuration", () =>
                {
                    settings = Game.ChildrenOfType<SettingsOverlay>().Single();
                    settings.ShowAtControl<SkinOptionsSettings>();
                });
                AddUntilStep("wait for all declared configuration controls", () =>
                {
                    var controls = settings.ChildrenOfType<SkinOptionsSettings>().SingleOrDefault();
                    return controls?.ChildrenOfType<FormDropdown<string>>().Count() == 19
                        && controls.ChildrenOfType<FormSliderBar<double>>().Count() == 2
                        && controls.ChildrenOfType<FormCheckBox>().Count() == 3;
                });
                AddStep("change a frame, a note style, an effect size and a toggle", () =>
                {
                    var controls = settings.ChildrenOfType<SkinOptionsSettings>().Single();
                    Assert.That(Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania"), Is.Empty);
                    controls.ChildrenOfType<FormDropdown<string>>().Single(dropdown => dropdown.Caption.ToString() == "Game Frame").Current.Value = "bpl-tradz";
                    controls.ChildrenOfType<FormDropdown<string>>().Single(dropdown => dropdown.Caption.ToString() == "Notes · 音符").Current.Value = "n03";
                    originalScale = controls.ChildrenOfType<FormSliderBar<double>>().Single(slider => slider.Caption.ToString() == "爆炸缩放");
                    Assert.That(originalScale.Current.Value, Is.EqualTo(1));
                    originalScale.Current.Value = 1.25;
                    controls.ChildrenOfType<FormCheckBox>().Single(toggle => toggle.Caption.ToString() == "Graph Area 显示统计").Current.Value = true;
                });
                AddUntilStep("save the independent typed values for this package and mode", () =>
                {
                    var saved = Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania");
                    return saved.Count == 4 && saved["frame"] == "bpl-tradz" && saved["notes"] == "n03"
                        && saved["explosion-scale"] == "1.25" && saved["graph-visible"] == "true";
                });
                AddUntilStep("rebuild controls with the saved size", () =>
                {
                    var rebuilt = settings.ChildrenOfType<SkinOptionsSettings>().Single().ChildrenOfType<FormSliderBar<double>>()
                        .Single(slider => slider.Caption.ToString() == "爆炸缩放");
                    return !ReferenceEquals(originalScale, rebuilt) && rebuilt.Current.Value == 1.25;
                });
                AddWaitStep("allow the configuration layout to settle", 3);
                AddStep("capture the complete configuration", () => capture = save(0));
                AddUntilStep("wait for configuration pixels", () => capture?.IsCompleted == true);
                AddStep("restore the package defaults", () => settings.ChildrenOfType<SkinOptionsSettings>().Single()
                    .ChildrenOfType<SettingsButtonV2>().Single().Action());
                AddUntilStep("reset clears only these overrides", () =>
                    Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania").Count == 0);
                AddStep("close the configuration and propagate capture failures", () => { capture!.GetAwaiter().GetResult(); settings.Hide(); });
            }
            if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_SETTINGS") == "1")
            {
                SettingsOverlay settings = null!;
                FormSliderBar<double> oldNumber = null!;
                bool offsetNumber = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_NUMBER_OFFSET") == "1";
                double expectedNumber = offsetNumber ? 0.13 : 1.25;
                AddStep("open the real skin settings", () =>
                {
                    settings = Game.ChildrenOfType<SettingsOverlay>().Single();
                    settings.ShowAtControl<SkinOptionsSettings>();
                });
                AddUntilStep("wait for author-generated choice, toggle and number controls", () =>
                    settings.ChildrenOfType<SkinOptionsSettings>().SingleOrDefault()?.ChildrenOfType<FormDropdown<string>>().Count() == 6);
                AddStep("change an actual generated dropdown and number control", () =>
                {
                    var options = settings.ChildrenOfType<SkinOptionsSettings>().Single();
                    Assert.That(Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania"), Is.Empty,
                        "Loading generated controls must not manufacture player overrides.");
                    oldNumber = options.ChildrenOfType<FormSliderBar<double>>().Single();
                    Assert.That(oldNumber.Current.Value, Is.EqualTo(offsetNumber ? 0.08 : 1).Within(0.000001));
                    options.ChildrenOfType<FormDropdown<string>>().Single(dropdown => dropdown.Caption.ToString() == "音符样式").Current.Value = "n03";
                    oldNumber.Current.Value = offsetNumber ? 0.129 : 1.25;
                    options.ChildrenOfType<FormCheckBox>().Single().Current.Value = false;
                });
                AddUntilStep("settings save both typed values for this skin and mode", () =>
                {
                    var values = Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania");
                    return values.TryGetValue("notes", out string? notes) && notes == "n03"
                        && values.TryGetValue("bomb-scale", out string? scale)
                        && Math.Abs(double.Parse(scale, System.Globalization.CultureInfo.InvariantCulture) - expectedNumber) < 0.000001
                        && values.TryGetValue("beam-visible", out string? beam) && beam == "false";
                });
                AddUntilStep("rebuild replaces old controls and preserves the saved number", () =>
                {
                    var rebuilt = settings.ChildrenOfType<SkinOptionsSettings>().Single().ChildrenOfType<FormSliderBar<double>>().Single();
                    return !ReferenceEquals(oldNumber, rebuilt) && Math.Abs(rebuilt.Current.Value - expectedNumber) < 0.000001;
                });
                AddWaitStep("allow settings layout to settle", 3);
                AddStep("capture the generated settings", () => capture = save(0));
                AddUntilStep("wait for settings PNG", () => capture?.IsCompleted == true);
                AddStep("restore defaults with the generated button", () =>
                {
                    settings.ChildrenOfType<SkinOptionsSettings>().Single().ChildrenOfType<SettingsButtonV2>().Single().Action();
                });
                AddUntilStep("reset clears only the selected skin and mode overrides", () =>
                    Game.Dependencies.Get<SkinOptionPreferences>().GetOverrides(import!.GetAwaiter().GetResult().ID, "mania").Count == 0);
                AddStep("close settings and propagate capture errors", () => { capture!.GetAwaiter().GetResult(); settings.Hide(); });
            }
            AddStep("enter the normal replay Player", () =>
            {
                var preferences = Game.Dependencies.Get<SkinOptionPreferences>();
                preferences.SetOverride(import!.GetAwaiter().GetResult().ID, "mania", "layout",
                    Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PRESENTATION") == "p2" ? "p2" : "p1");
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_OPTIONS") is string overrides)
                    foreach (var choice in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(overrides)!)
                        preferences.SetOverride(import.GetAwaiter().GetResult().ID, "mania", choice.Key, choice.Value);
                var ruleset = new ManiaRuleset();
                Game.ScreenStack.Push(new PlayerLoader(() => player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)));
            });
            AddUntilStep("wait for the real loaded Player", () => player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                && player.LoadedBeatmapSuccessfully && player.ChildrenOfType<DrawableManiaRuleset>().SingleOrDefault()?.IsLoaded == true);
            AddStep("verify exact materials and expose the game framebuffer", () =>
            {
                renderer = player.ChildrenOfType<DrawableManiaRuleset>().Single();
                score = renderer.Dependencies.Get<ScoreProcessor>();
                var owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                Assert.That(owner.CurrentPublication!.Snapshot.Context.Topology.LanesInLogicalOrder.Count,
                    Is.EqualTo(columns * (dual ? 2 : 1)));
                Assert.That(owner.CurrentPublication.MaterialSet.Diagnostics, Is.Empty);
                Assert.That(renderer.GameplaySkinSceneRuntime, Is.Not.Null);
                GameplaySkinOptionValues publishedOptions = owner.CurrentPublication.Snapshot.Context.OptionValues;
                File.WriteAllText(Path.Combine(output, "options.json"), System.Text.Json.JsonSerializer.Serialize(
                    publishedOptions.Values.ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue)));
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_OPTIONS") is string requestedOptions)
                    foreach (var requested in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestedOptions)!)
                    {
                        GameplaySkinOptionDefinition definition = Game.Dependencies.Get<SkinManager>().CurrentSkin.Value
                            .PreparedGameplaySkinPackage!.Manifest!.Options.Single(option => option.Id == requested.Key);
                        Assert.That(definition.TryValidateValue(requested.Value, out GameplaySkinOptionValue expected), Is.True, requested.Key);
                        Assert.That(publishedOptions.TryGetValue(requested.Key, out GameplaySkinOptionValue actual), Is.True, requested.Key);
                        Assert.That(actual.CanonicalValue, Is.EqualTo(expected.CanonicalValue), requested.Key);
                    }
                if (owner.CurrentPublication.Snapshot.Context.OptionValues.TryGetValue("frame", out _))
                {
                    GameplaySkinResolvedMaterialEntry[] information = owner.CurrentPublication.MaterialSet.Entries
                        .Where(entry => entry.State == GameplaySkinResolvedMaterialState.Provide
                                        && entry.Material is GameplaySkinPublicSlotMaterial { ResourceName: { } resource }
                                        && resource.StartsWith("markers/frame-", StringComparison.Ordinal)
                                        && resource.EndsWith("-information", StringComparison.Ordinal)).ToArray();
                    Assert.That(information, Has.Length.EqualTo(1));
                    Assert.That(information[0].Target.Kind, Is.EqualTo(GameplaySkinResolvedMaterialTargetKind.Global),
                        "Frame information must not be inherited and redrawn inside stages, groups or lanes.");
                }
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_EFFECT_GEOMETRY") == "1")
                {
                    GameplaySkinOptionValues options = owner.CurrentPublication.Snapshot.Context.OptionValues;
                    bool compact = options.Values["bomb"].StringValue == "compact";
                    string suffix = compact ? ".f.c" : ".f";
                    Scheduler.AddDelayed(() =>
                    {
                        foreach (PoolableHitExplosion explosion in renderer.Playfield.Stages.SelectMany(stage => stage.Columns)
                                     .SelectMany(column => column.ActiveHitExplosions))
                            foreach (GameplaySkinSceneRuntimeNode runtime in explosion.SpecialisedSceneVisual!.RuntimeNodes
                                         .Where(node => node.PreparedNode.Source.Id.StartsWith("x.m.", StringComparison.Ordinal)
                                             && node.PreparedNode.Source.Id.EndsWith(suffix, StringComparison.Ordinal)))
                            {
                                var quad = runtime.TransformDrawable.ScreenSpaceDrawQuad;
                                if (quad.Width > 0 && quad.Height > 0)
                                    effectGeometry[runtime.PreparedNode.Source.Id] = (quad.Width, quad.Height);
                            }
                    }, 10, true);
                }
                File.WriteAllText(Path.Combine(output, "layout.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    context = owner.CurrentPublication.Snapshot.Context.KeymodeId,
                    presentation = owner.CurrentPublication.Snapshot.Context.PresentationStyleId,
                    lanes = owner.CurrentPublication.Snapshot.LanesInLogicalOrder.Select(lane => new { id = lane.TopologyEntry.Identity.Id.Value, lane.Rect }),
                }));
                OsuGameBase? outer = null;
                for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
                    if (ancestor is OsuGameBase game) { outer = game; break; }
                Assert.That(outer, Is.Not.Null);
                var content = (Container<Drawable>)typeof(OsuGameBase).GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(outer)!;
                AlwaysPresent = true;
                Remove(Game, false);
                foreach (Drawable chrome in content.Children) { chrome.Alpha = 0; chrome.AlwaysPresent = true; }
                content.Add(Game);
                Game.Notifications.Hide();
            });
            foreach (int time in new[] { 2600, 3600, 5200, 6800, 10000 })
            {
                int sample = time;
                AddUntilStep("play naturally to " + sample, () => renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime >= sample);
                AddStep("verify scene and capture framebuffer", () =>
                {
                    Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                    capture = save(sample);
                });
                AddUntilStep("wait for PNG", () => capture?.IsCompleted == true);
                AddStep("propagate capture errors", () => capture!.GetAwaiter().GetResult());
            }
            AddUntilStep("complete the actual scored chart", () => score.HasCompleted.Value);
            AddAssert("autoplay produced real score and combo", () => score.TotalScore.Value > 0 && score.HighestCombo.Value > 0);
            AddUntilStep("normal results transition", () => !ReferenceEquals(Game.ScreenStack.CurrentScreen, player));
            AddAssert("no scene runtime faults", () => renderer.GameplaySkinSceneRuntime!.RuntimeFaults.Count == 0);
            AddStep("verify original effect geometry in the actual native host", () =>
            {
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_EFFECT_GEOMETRY") != "1")
                    return;
                GameplaySkinOptionValues values = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>().CurrentPublication!.Snapshot.Context.OptionValues;
                double scale = values.Values["bomb-scale"].NumberValue * (values.Values["bomb"].StringValue == "compact" ? 0.7 : 1);
                Assert.That(effectGeometry, Is.Not.Empty);
                foreach (var geometry in effectGeometry.Values)
                {
                    Assert.That(geometry.Width, Is.EqualTo(253.173828125 * scale).Within(1));
                    Assert.That(geometry.Height, Is.EqualTo(229.345703125 * scale).Within(1));
                }
                File.WriteAllText(Path.Combine(output, "effect-geometry.json"), System.Text.Json.JsonSerializer.Serialize(
                    effectGeometry.Select(pair => new { source = pair.Key, pair.Value.Width, pair.Value.Height, scale })));
            });
        }

        private async Task save(int time)
        {
            using Image<Rgba32> image = await host.TakeScreenshotAsync().ConfigureAwait(false);
            await image.SaveAsPngAsync(Path.Combine(output, $"mania-{time:00000}.png")).ConfigureAwait(false);
        }

        private ManiaBeatmap createChart(ManiaRuleset ruleset)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(columns)) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
            if (dual)
                beatmap.Stages.Add(new StageDefinition(columns));
            beatmap.Difficulty.CircleSize = columns * (dual ? 2 : 1);
            for (int row = 0; row < 8; row++)
                for (int lane = 0; lane < beatmap.TotalColumns; lane++)
                    beatmap.HitObjects.Add(row % 2 == 0
                        ? new Note { Column = lane, StartTime = 2000 + row * 1500 + lane * 30 }
                        : new HoldNote { Column = lane, StartTime = 2000 + row * 1500 + lane * 30, Duration = 700 });
            return beatmap;
        }

        private sealed class PreservingImportTask : ImportTask
        {
            public PreservingImportTask(Stream stream, string filename) : base(stream, filename) { }
            public override void DeleteFile() { }
        }
    }
}
