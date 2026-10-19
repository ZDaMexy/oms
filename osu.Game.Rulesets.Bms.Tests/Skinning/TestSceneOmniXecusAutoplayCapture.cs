// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Opt-in native regression for an ordinary Omni import and the copied real Xecus SHD chart.
    /// The external chart must be a disposable copy under the development temporary directory.
    /// Settings interaction is covered separately; this fixture owns only the actual autoplay path.
    /// </summary>
    public partial class TestSceneOmniXecusAutoplayCapture : OsuGameTestScene
    {
        private const string chart_filename = "xecus SHD.bms";
        private static readonly JsonSerializerOptions receipt_options = new JsonSerializerOptions { WriteIndented = true };

        [Resolved]
        private GameHost host { get; set; } = null!;

        private ExactVisualTestIsolation? isolation;
        private string packagePath = null!;
        private string packageHash = null!;
        private string chartDirectory = null!;
        private string outputDirectory = null!;
        private Live<SkinInfo> importedSkin = null!;
        private SkinManager skins = null!;
        private Player player = null!;
        private DrawableBmsRuleset renderer = null!;
        private BmsBgaPlaybackSession session = null!;
        private BmsBgaPlayer bgaPlayer = null!;
        private ScoreProcessor score = null!;
        private Task? screenshot;
        private bool playFullChart;
        private double playSeconds;
        private double targetTime;
        private long firstRedrawCount;

        public override bool AutomaticallyRunFirstStep => false;

        protected override double TimePerAction => 200;

        protected override TestOsuGame CreateTestGame() => new FilesystemBmsTestGame(LocalStorage, API);

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? exactIsolation)
        {
            isolation = exactIsolation;
        }

        [Test]
        public void CaptureDefaultOmniThroughRealXecusAutoplay()
        {
            string? directory = Environment.GetEnvironmentVariable("OMS_XECUS_DIRECTORY");
            string? package = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PACKAGE");
            string? output = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY");
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(package) || string.IsNullOrEmpty(output))
                Assert.Ignore("Set OMS_XECUS_DIRECTORY, OMS_AUTHOR_SKIN_PACKAGE and OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY for this opt-in native capture.");

            AddStep("require an isolated native host and a disposable Xecus copy", () =>
            {
                if (host is HeadlessGameHost || isolation == null)
                    Assert.Ignore("Xecus drawing evidence requires the disposable native ExactVisualTestGame host.");

                chartDirectory = Path.GetFullPath(directory!);
                packagePath = Path.GetFullPath(package!);
                outputDirectory = Path.GetFullPath(output!);
                string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
                Assert.That(Path.GetPathRoot(temporaryRoot), Is.Not.EqualTo("C:\\").IgnoreCase,
                    "UseDevelopmentStorage must put disposable inputs on the development drive.");
                Assert.That(chartDirectory.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True,
                    "The chart input must be a disposable copy under the development temporary directory.");
                Assert.That(Path.GetPathRoot(outputDirectory), Is.EqualTo(Path.GetPathRoot(temporaryRoot)).IgnoreCase);
                Assert.That(File.Exists(Path.Combine(chartDirectory, chart_filename)), Is.True,
                    "Use the unchanged real xecus SHD.bms file.");
                Assert.That(File.Exists(packagePath), Is.True);
                Directory.CreateDirectory(outputDirectory);
                string? duration = Environment.GetEnvironmentVariable("OMS_XECUS_PLAY_SECONDS");
                playFullChart = string.Equals(duration, "full", StringComparison.OrdinalIgnoreCase);
                playSeconds = playFullChart ? 0 : double.Parse(duration ?? "20", CultureInfo.InvariantCulture);
                Assert.That(playFullChart || double.IsFinite(playSeconds) && playSeconds >= 5, Is.True);
                var framework = Game.Dependencies.Get<FrameworkConfigManager>();
                framework.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                framework.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
                packageHash = hashPackage();
            });
            AddStep("import the complete ordinary package while retaining the deliverable", () =>
            {
                skins = Game.Dependencies.Get<SkinManager>();
                // The complete library may take more than UntilStepButton's fixed ten-second deadline.
                // Let the isolated native host own the overall watchdog rather than abandoning its Realm writer.
                importedSkin = skins.Import(new PreservingImportTask(File.OpenRead(packagePath), Path.GetFileName(packagePath)))
                                    .GetAwaiter().GetResult() ?? throw new InvalidOperationException("The author package importer returned no skin.");
                Assert.That(hashPackage(), Is.EqualTo(packageHash));
            });
            AddStep("register the copied song and resolve the production filesystem WorkingBeatmap", () =>
            {
                var importer = new BmsFolderImporter(Game.Storage, Game.Realm);
                BmsFolderImporter.FolderImportResult result = importer.RegisterExternalDirectory(chartDirectory).GetAwaiter().GetResult();
                Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
                BeatmapInfo chart = result.ImportedBeatmapSet!.PerformRead(set => set.Beatmaps.Single(beatmap =>
                    string.Equals(Path.GetFileName(beatmap.Path), chart_filename, StringComparison.OrdinalIgnoreCase)).Detach());
                var ruleset = new BmsRuleset();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(chart);
                Game.SelectedMods.Value = [ruleset.GetAutoplayMod()!];
                Assert.That(Game.Beatmap.Value.Beatmap, Is.TypeOf<BmsDecodedBeatmap>(),
                    "The native probe must register the same BMS loader as OsuGameDesktop.");
                Assert.That(Game.Beatmap.Value.GetPlayableBeatmap(ruleset.RulesetInfo, Game.SelectedMods.Value).HitObjects, Is.Not.Empty);
                DismissAnyNotifications();
            });
            AddStep("select Omni as the isolated BMS mode default", () =>
            {
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms"), importedSkin.ID);
                skins.SelectionPreferences.Reset(new SkinSelectionScope("bms", 7));
                skins.CurrentSkinInfo.Value = importedSkin;
            });
            AddUntilStep("the ordinary imported package is selected", () =>
            {
                Assert.That(skins.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                return skins.CurrentSkin.Value.SkinInfo.ID == importedSkin.ID;
            });
            AddStep("enter the real unmodified autoplay ReplayPlayer", () =>
            {
                var ruleset = new BmsRuleset();
                Game.ScreenStack.Push(new PlayerLoader(() => player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)));
            });
            AddUntilStep("the real Xecus Player and BMS renderer have loaded", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                                      && player.LoadedBeatmapSuccessfully
                                      && player.ChildrenOfType<DrawableBmsRuleset>().SingleOrDefault()?.IsLoaded == true;
            });
            AddStep("verify the exact author publication and gameplay-owned BGA session", () =>
            {
                renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                score = renderer.Dependencies.Get<ScoreProcessor>();
                session = renderer.BgaPlaybackSession!;
                Assert.That(session, Is.Not.Null);
                bgaPlayer = session.Player!;
                Assert.That(bgaPlayer, Is.Not.Null);
                var beatmap = (BmsBeatmap)renderer.Beatmap;
                Assert.That(beatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(beatmap.BgaTimeline, Is.Not.Empty, "The real chart must exercise its original BGA timeline.");
                Assert.That(session.HasContent, Is.True);
                Assert.That(session.ChildrenOfType<BmsBgaPlayer>().Single(), Is.SameAs(bgaPlayer));
                BmsBgaPanel panel = renderer.ChildrenOfType<BmsBgaPanel>().Single();
                Assert.That(panel.ChildrenOfType<BmsBgaPlayer>(), Is.Empty, "The skin display owns views, not another playback session.");
                Assert.That(panel.ChildrenOfType<BufferedContainerView<Drawable>>(), Is.Not.Empty);
                GameplaySkinLayoutRevisionOwner owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
                Assert.That(skins.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(importedSkin.ID));
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.False);
                Assert.That(renderer.GameplaySkinSceneRuntime, Is.Not.Null);
                Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                Assert.That(renderer.GameplaySkinSceneRuntime.MaterialSet.Diagnostics, Is.Empty);
                targetTime = playFullChart ? beatmap.HitObjects.Max(hitObject => hitObject.GetEndTime()) + 1000 : playSeconds * 1000;
                firstRedrawCount = session.SurfaceRedrawCount;
                writeReceipt("player-entered");
                hideTestChrome();
            });
            AddUntilStep("a real BGA bitmap has loaded into the shared source", () => session.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null)
                && session.SurfaceRedrawCount > firstRedrawCount);
            AddStep("capture the first rendered real-song frame", () => screenshot = saveCapture("xecus-first.png"));
            AddUntilStep("the first framebuffer is saved", () => screenshot?.IsCompleted == true);
            AddStep("verify the framebuffer and append the requested natural playback", () =>
            {
                screenshot!.GetAwaiter().GetResult();
                appendPlaybackSteps();
            });
        }

        private void appendPlaybackSteps()
        {
            // Keep every wait shorter than the framework's ten-second UntilStep deadline. Full-song runs
            // additionally need an isolated host whose outer watchdog allows the complete song and import.
            for (double next = 5000; next < targetTime + 5000; next += 5000)
            {
                double checkpoint = Math.Min(next, targetTime);
                AddUntilStep($"play the real chart naturally to {checkpoint / 1000:0.0}s", () =>
                    score.HasCompleted.Value || renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime >= checkpoint);
                AddStep("retain the same media and exact skin owners", () =>
                {
                    Assert.That(session.Player, Is.SameAs(bgaPlayer));
                    Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                    Assert.That(skins.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(importedSkin.ID));
                    writeReceipt("playing");
                });
                if (checkpoint <= 20000 || checkpoint % 30000 == 0 || checkpoint == targetTime)
                {
                    AddStep("save a native gameplay framebuffer", () => screenshot = saveCapture($"xecus-{checkpoint:000000}.png"));
                    AddUntilStep("the gameplay framebuffer is saved", () => screenshot?.IsCompleted == true);
                    AddStep("propagate framebuffer failures", () => screenshot!.GetAwaiter().GetResult());
                }
                if (checkpoint == targetTime)
                    break;
            }
            AddStep("record successful actual autoplay and preserve the input package", () =>
            {
                Assert.That(score.TotalScore.Value, Is.GreaterThan(0));
                Assert.That(score.HighestCombo.Value, Is.GreaterThan(0));
                Assert.That(session.SurfaceRedrawCount, Is.GreaterThan(firstRedrawCount));
                if (playFullChart)
                    Assert.That(score.HasCompleted.Value, Is.True);
                Assert.That(hashPackage(), Is.EqualTo(packageHash));
                writeReceipt("completed");
            });
        }

        private void writeReceipt(string stage)
        {
            GameplaySkinLayoutRevisionOwner owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
            File.WriteAllText(Path.Combine(outputDirectory, "xecus-autoplay.json"), JsonSerializer.Serialize(new
            {
                stage,
                chart = chart_filename,
                packageSha256 = packageHash,
                settingsInteractionCovered = false,
                requestedSeconds = playFullChart ? "full" : playSeconds.ToString(CultureInfo.InvariantCulture),
                gameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                targetTime,
                actualSkin = skins.CurrentSkin.Value.SkinInfo.PerformRead(info => info.Name),
                exactPackageRetained = owner.PackageRevision.RetainsExact(skins.CurrentRevision),
                selectedMods = Game.SelectedMods.Value.Select(mod => mod.Acronym).ToArray(),
                options = owner.CurrentPublication!.Snapshot.Context.OptionValues.Values.ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue),
                bgaTimelineEntries = ((BmsBeatmap)renderer.Beatmap).BgaTimeline.Count,
                bgaState = session.ContentState.ToString(),
                bgaRevision = session.ContentRevision,
                bgaRedraws = session.SurfaceRedrawCount,
                oneOriginalBgaPlayer = ReferenceEquals(session.Player, bgaPlayer) && session.ChildrenOfType<BmsBgaPlayer>().Count() == 1,
                bgaViews = renderer.ChildrenOfType<BufferedContainerView<Drawable>>().Count(),
                runtimeFaults = renderer.GameplaySkinSceneRuntime!.RuntimeFaults.Count,
                hasCompleted = score.HasCompleted.Value,
                totalScore = score.TotalScore.Value,
                highestCombo = score.HighestCombo.Value,
            }, receipt_options));
        }

        private void hideTestChrome()
        {
            OsuGameBase? outer = null;
            for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
                if (ancestor is OsuGameBase candidate)
                {
                    outer = candidate;
                    break;
                }
            Assert.That(outer, Is.Not.Null);
            var content = (Container<Drawable>)typeof(OsuGameBase)
                .GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(outer)!;
            AlwaysPresent = true;
            Remove(Game, false);
            foreach (Drawable chrome in content.Children)
            {
                chrome.Alpha = 0;
                chrome.AlwaysPresent = true;
            }
            content.Add(Game);
            Game.Notifications.Hide();
            InputManager.MoveMouseTo(player.ScreenSpaceDrawQuad.Centre);
        }

        private string hashPackage()
        {
            using FileStream stream = File.OpenRead(packagePath);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private async Task saveCapture(string filename)
        {
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            if (pixels == null || pixels.Width < 640 || pixels.Height < 360)
                throw new InvalidOperationException("The native Xecus capture did not return a usable framebuffer.");
            await pixels.SaveAsPngAsync(Path.Combine(outputDirectory, filename)).ConfigureAwait(false);
        }

        private partial class FilesystemBmsTestGame : TestOsuGame
        {
            public FilesystemBmsTestGame(Storage storage, IAPIProvider api)
                : base(storage, api)
            {
            }

            protected override IEnumerable<ICustomBeatmapLoader> CreateCustomBeatmapLoaders() => [new BmsBeatmapLoader()];
        }

        private sealed class PreservingImportTask : ImportTask
        {
            public PreservingImportTask(Stream stream, string filename)
                : base(stream, filename)
            {
            }

            public override void DeleteFile()
            {
                // Ordinary import uses disposable game storage; the independently delivered author package stays intact.
            }
        }
    }
}
