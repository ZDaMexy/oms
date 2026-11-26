// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
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
    /// Saves pixels from a desktop host running the actual PlayerLoader, canonical package and BMS renderer.
    /// The synthetic offline chart supplies repeatable notes; it is not a capture of a user's song or BGA.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBmsSimpleGameplayCapture : OsuGameTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private Task? capture;

        public override bool AutomaticallyRunFirstStep => false;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Gameplay capture requires the disposable --exact-test runner.");
        }

        [Test]
        public void CaptureCanonicalSimplePlayer()
        {
            Player player = null!;
            string mode = Environment.GetEnvironmentVariable("OMS_SIMPLE_CAPTURE_MODE") ?? "bms7";
            string presentation = Environment.GetEnvironmentVariable("OMS_SIMPLE_CAPTURE_SIDE") ?? "p1";
            bool isBms = mode.StartsWith("bms", StringComparison.Ordinal);
            var ruleset = isBms ? (Rulesets.Ruleset)new BmsRuleset() : new ManiaRuleset();
            AddStep("require a real desktop renderer", () =>
            {
                if (host is HeadlessGameHost)
                    Assert.Ignore("Pixel evidence requires the isolated desktop --exact-test runner.");
            });
            AddStep("select installed simple and a repeatable chart", () =>
            {
                var framework = Game.Dependencies.Get<FrameworkConfigManager>();
                framework.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                var resolution = Environment.GetEnvironmentVariable("OMS_SIMPLE_CAPTURE_SIZE") == "4:3"
                    ? new System.Drawing.Size(1280, 960)
                    : new System.Drawing.Size(1920, 1080);
                framework.SetValue(FrameworkSetting.WindowedSize, resolution);
                var skins = Game.Dependencies.Get<SkinManager>();
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.DefaultOmsSkin), Is.True);
                skins.CurrentSkinInfo.Value = skins.DefaultOmsSkin.SkinInfo;
                GameplaySkinOptionDefinition layout = skins.DefaultOmsSkin.GameplayOptions.Single(option => option.Effect == GameplaySkinOptionEffect.Presentation);
                if (isBms)
                    Game.Dependencies.Get<SkinOptionPreferences>().SetOverride(skins.DefaultOmsSkin.SkinInfo.ID, "bms", layout.Id, presentation);
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(isBms ? createBmsChart((BmsRuleset)ruleset, mode) : createManiaChart((ManiaRuleset)ruleset, mode));
                Game.SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddStep("enter real autoplay player", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData))));
            AddUntilStep("wait for actual player and committed skin", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                                      && player.IsLoaded && player.LoadedBeatmapSuccessfully
                                      && player.ChildrenOfType<DrawableRuleset>().SingleOrDefault() is IGameplaySkinSceneRuntimeSource { GameplaySkinSceneRuntime.IsSceneReady: true };
            });
            AddStep("verify renderer owns the installed package", () =>
            {
                var skins = Game.Dependencies.Get<SkinManager>();
                DrawableRuleset renderer = player.ChildrenOfType<DrawableRuleset>().Single();
                var owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                Assert.That(owner.CurrentPublication, Is.Not.Null);
                Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.True);
                if (isBms)
                {
                    bool receivedInformation = false;
                    using var observer = renderer.Dependencies.Get<GameplaySkinEventRuntimeHost>().EventStream.Subscribe();
                    observer.DrainFrame(envelope =>
                    {
                        if (envelope.Payload is GameplaySkinStateEventPayload state)
                        {
                            receivedInformation = true;
                            Assert.That(state.State.SongInformation!.TableClassification, Is.EqualTo("Satellite sl4 / 発狂BMS ★8"));
                            Assert.That(state.State.SongInformation.Level, Is.EqualTo("12"));
                        }
                    });
                    Assert.That(receivedInformation, Is.True);
                }

                // The test's automation remains alive while the actual game renders at the full viewport.
                OsuGameBase? outer = null;
                for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
                {
                    if (ancestor is not OsuGameBase game)
                        continue;
                    outer = game;
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
            });
            AddUntilStep("display chords and sustained notes", () => player.ChildrenOfType<GameplayClockContainer>().Single().CurrentTime >= 4050);
            AddStep("capture real desktop framebuffer", () => capture = saveCapture());
            AddUntilStep("wait for PNG write", () => capture?.IsCompleted == true);
            AddStep("propagate capture failures", () => capture!.GetAwaiter().GetResult());
        }

        private async Task saveCapture()
        {
            string path = Path.GetFullPath(Environment.GetEnvironmentVariable("OMS_SIMPLE_CAPTURE_PATH")
                                           ?? Path.Combine("artifacts", "simple-gameplay-capture.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            if (pixels == null || pixels.Width < 640 || pixels.Height < 360)
                throw new InvalidOperationException("Desktop capture did not return a usable framebuffer.");
            await pixels.SaveAsPngAsync(path).ConfigureAwait(false);
        }

        private static IBeatmap createBmsChart(BmsRuleset ruleset, string mode)
        {
            var text = new StringBuilder("#TITLE Glass Current / 静線\n#ARTIST OMS Offline Studio\n#DIFFICULTY 4\n#PLAYLEVEL 12\n#BPM 138\n#BPM01 172\n#00808:01\n#01603:78\n#WAV01 note.wav\n");
            string[] channels = mode switch
            {
                "bms5" => new[] { "11", "12", "13", "14", "15", "16" },
                "bms7" => new[] { "11", "12", "13", "14", "15", "18", "19", "16" },
                "bms9" => new[] { "11", "12", "13", "14", "15", "16", "17", "18", "19" },
                "bms14" => new[] { "11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26" },
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown BMS capture mode."),
            };
            for (int measure = 1; measure <= 32; measure++)
                for (int lane = 0; lane < channels.Length; lane++)
                {
                    if (measure % 2 == 0 && lane % 5 == 0)
                    {
                        string hold = (channels[lane][0] == '1' ? "5" : "6") + channels[lane][1];
                        text.Append('#').Append(measure.ToString("000")).Append(hold).Append(":0100000000000100\n");
                        continue;
                    }
                    text.Append('#').Append(measure.ToString("000")).Append(channels[lane]).Append(':')
                        .Append(lane % 2 == 0 ? "0100000001000000" : "0000010000000100").Append('\n');
                }
            var decoded = new BmsBeatmapDecoder().DecodeText(text.ToString(), mode == "bms9" ? "simple-gameplay-capture.pms" : "simple-gameplay-capture.bme");
            var beatmap = new BmsDecodedBeatmap(decoded) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
            // Synthetic offline memberships exercise the same persisted read path as imported enabled tables.
            beatmap.BeatmapInfo.Metadata.SetRulesetData(new BmsBeatmapMetadataData
            {
                DifficultyTableEntries =
                {
                    new BmsDifficultyTableEntry("発狂BMS", "★", 8, "★8", "", 1),
                    new BmsDifficultyTableEntry("Satellite", "sl", 4, "sl4", "", 0),
                },
            });
            return beatmap;
        }

        private static ManiaBeatmap createManiaChart(ManiaRuleset ruleset, string mode)
        {
            int keys = mode switch
            {
                "mania4" => 4,
                "mania7" => 7,
                "mania14" => 14,
                "mania20" => 20,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown mania capture mode."),
            };
            var beatmap = new ManiaBeatmap(new StageDefinition(keys > 10 ? keys / 2 : keys))
            {
                BeatmapInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    Metadata = new BeatmapMetadata { Title = "Glass Current / 静線", Artist = "OMS Offline Studio" },
                },
                Difficulty = { CircleSize = keys, OverallDifficulty = 5 },
            };
            if (keys > 10)
                beatmap.Stages.Add(new StageDefinition(keys / 2));

            for (int beat = 0; beat < 70; beat++)
            {
                double time = 1800 + beat * 420;
                for (int column = beat % 2; column < keys; column += 2)
                {
                    if (beat % 4 == 2 && column % 3 == 0)
                        beatmap.HitObjects.Add(new HoldNote { StartTime = time, Duration = 580, Column = column });
                    else
                        beatmap.HitObjects.Add(new Note { StartTime = time, Column = column });
                }
            }
            return beatmap;
        }
    }
}
