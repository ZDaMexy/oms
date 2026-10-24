// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual.Spectator;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneHUDOverlayRulesetLayouts
    {
        [TestCase("bms", true)]
        [TestCase("mania", true)]
        [TestCase("mania", false)]
        public void TestAuthorModsPlacementRetainsTheRealReplayDisplayAcrossCanvasAndModChanges(string rulesetName, bool declared)
        {
            Drawable[] previous = [];
            Task<Live<SkinInfo>> import = null!;
            MemoryStream archive = null!;
            SkinManager manager = null!;
            HUDOverlay overlay = null!;
            DrawableRuleset drawableRuleset = null!;
            Container viewport = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            Vector2 originalTopRightPosition = default;
            Mod[] originalMods = [new ManiaModAutoplay(), new ManiaModMirror()];
            Mod[] additionalMods = [.. originalMods, new ManiaModNoFail(), new ManiaModHidden()];
            var modsRect = GameplaySkinLayoutRect.Create(0.1f, 0.14f, 0.18f, 0.08f);

            AddStep("detach the previous real gameplay tree", () =>
            {
                previous = Children.ToArray();
                Clear(disposeChildren: true);
            });
            AddUntilStep("previous gameplay releases its selection barrier", () => previous.All(isModsDrawableDisposed));
            AddStep("import a layout-only author package", () =>
            {
                manager = Dependencies.Get<SkinManager>();
                configManager.SetValue(OsuSetting.HUDVisibilityMode, HUDVisibilityMode.Always);
                int lanes = rulesetName == "bms" ? 8 : 7;
                var author = new GameplaySkinAuthorLayout("mods", rulesetName, "7k", "any",
                    new[] { new GameplaySkinAuthorLayoutStage(GameplaySkinLayoutRect.Create(0.1f, 0, 0.3f, 0.65f),
                        Enumerable.Range(0, lanes).Select(index => new GameplaySkinAuthorLaneSpan(index / (double)lanes, 1d / lanes)).ToArray()) },
                    keyAreaHeight: 0.1, gaugeHeight: 0.1, modsRect: declared ? modsRect : null);
                var manifest = new GameplaySkinSceneManifest([], authorLayouts: new[] { author });
                archive = new MemoryStream();
                using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
                {
                    write("skin.ini", Encoding.UTF8.GetBytes($"[General]\nName: Native mod layout {Guid.NewGuid():N}\nAuthor: OMS tests\nVersion: 2.7\n"));
                    write(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, GameplaySkinSceneCodec.EncodeManifestUtf8(manifest));
                    write(GameplaySkinSceneContracts.SCENE_FILE_NAME, Encoding.UTF8.GetBytes("""
                        {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"root","type":"container",
                          "target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]},
                         "tracks":[],"stateMachines":[],"bindings":[],"templates":[],"instances":[]}
                        """));

                    void write(string name, byte[] bytes)
                    {
                        using Stream entry = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
                        entry.Write(bytes);
                    }
                }
                archive.Position = 0;
                import = manager.Import(new ModsLayoutImportTask(archive));
            });
            AddUntilStep("author import completes", () => import.IsCompleted);
            AddStep("select the imported package normally", () => manager.CurrentSkinInfo.Value = import.GetAwaiter().GetResult());
            AddUntilStep("the package is the coherent selected owner", () =>
            {
                Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                return manager.CurrentSkin.Value.SkinInfo.ID == import.GetAwaiter().GetResult().ID;
            });
            AddStep("mount a real ruleset and its replay HUD in different canvases", () =>
            {
                var rulesetInfo = rulesets.GetRuleset(rulesetName)!;
                var ruleset = rulesetInfo.CreateInstance();
                IBeatmap beatmap = rulesetName == "bms"
                    ? new BmsBeatmapConverter(new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("#TITLE Native HUD\n#BPM 120\n#01611:01\n", "hud.bme")), ruleset).Convert()
                    : new ManiaBeatmap(new StageDefinition(7)) { HitObjects = { new Note { StartTime = 30_000 } } };
                beatmap.BeatmapInfo.Ruleset = rulesetInfo;
                foreach (var hitObject in beatmap.HitObjects)
                    hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                drawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap);
                drawableRuleset.HasReplayLoaded.Value = true;
                var gameplayState = new GameplayState(beatmap, ruleset);
                gameplayState.ScoreProcessor.ApplyBeatmap(beatmap);
                gameplayState.HealthProcessor.ApplyBeatmap(beatmap);
                ((Bindable<LocalUserPlayingState>)gameplayState.PlayingState).Value = LocalUserPlayingState.Playing;
                var spectator = new TestSpectatorClient();
                var clock = new GameplayClockContainer(new TrackVirtual(60_000), false, false);
                var dependencies = new List<(Type, object)>
                {
                    (typeof(GameplayState), gameplayState),
                    (typeof(ScoreProcessor), gameplayState.ScoreProcessor),
                    (typeof(HealthProcessor), gameplayState.HealthProcessor),
                    (typeof(IGameplayClock), clock),
                    (typeof(SpectatorClient), spectator),
                    (typeof(IGameplayLeaderboardProvider), new TestGameplayLeaderboardProvider()),
                };
                if (drawableRuleset is IDrawableScrollingRuleset scrolling)
                    dependencies.Add((typeof(IScrollingInfo), scrolling.ScrollingInfo));
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = dependencies.ToArray(),
                    Children = new Drawable[]
                    {
                        spectator,
                        new RulesetSkinProvidingContainer(ruleset, beatmap, manager.CurrentSkin.Value, prepareGameplaySkinLayout: true)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                viewport = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Size = new Vector2(0.78f, 0.73f),
                                    Position = new Vector2(60, 40),
                                    Child = drawableRuleset,
                                },
                                overlay = new HUDOverlay(drawableRuleset, originalMods, new PlayerConfiguration()) { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                };
                clock.Start();
            });
            AddUntilStep("the actual replay HUD has loaded and faded in", () => overlay.IsLoaded && overlay.ModDisplay.Alpha >= 0.99f
                && drawableRuleset is IGameplaySkinSceneRuntimeSource { GameplaySkinSceneRuntime: { IsSceneReady: true } });
            AddStep("capture the frozen canvas and original native mod objects", () =>
            {
                scene = ((IGameplaySkinSceneRuntimeSource)drawableRuleset).GameplaySkinSceneRuntime!;
                Assert.That(scene.Publication.Snapshot.Surfaces.Any(surface => surface.Id == GameplaySkinAuthorLayout.MODS_SURFACE), Is.EqualTo(declared));
                Assert.That(overlay.ModDisplay.Current.Value, Is.SameAs(originalMods));
                Assert.That(overlay.ModDisplay.ChildrenOfType<ModIcon>().Select(icon => icon.Mod), Is.EquivalentTo(originalMods));
                originalTopRightPosition = overlay.TopRightElements.Position;
            });
            AddUntilStep("native icons occupy the declared region or original top-right", placementIsCorrect);
            AddUntilStep("normal hover expansion policy is restored", () => overlay.ModDisplay.ExpansionMode == ExpansionMode.ExpandOnHover);
            AddStep("expand the same native display with more genuine mod objects", () =>
            {
                overlay.ModDisplay.Current.Value = additionalMods;
                overlay.ModDisplay.ExpansionMode = ExpansionMode.AlwaysExpanded;
                viewport.Size = new Vector2(0.35f, 0.52f);
                viewport.Position = new Vector2(120, 90);
            });
            AddUntilStep("all new icons remain present and fitted after resizing", () =>
                overlay.ModDisplay.ChildrenOfType<ModIcon>().Count() == additionalMods.Length && placementIsCorrect());
            AddStep("the native mod data and expansion behaviour remain intact", () =>
            {
                Assert.That(overlay.ModDisplay.Current.Value, Is.SameAs(additionalMods));
                Assert.That(overlay.ModDisplay.ChildrenOfType<ModIcon>().Select(icon => icon.Mod), Is.EquivalentTo(additionalMods));
                Assert.That(overlay.ModDisplay.ExpansionMode, Is.EqualTo(ExpansionMode.AlwaysExpanded));
                Assert.That(overlay.ModDisplay.Alpha, Is.GreaterThanOrEqualTo(0.99f));
                Assert.That(overlay.ShowHud.Value, Is.True);
                if (declared)
                    Assert.That(overlay.TopRightElements.Scale.X, Is.LessThan(1));
            });
            AddStep("contract to one mod using the same native control", () =>
            {
                overlay.ModDisplay.Current.Value = new Mod[] { originalMods[0] };
                overlay.ModDisplay.ExpansionMode = ExpansionMode.ExpandOnHover;
            });
            AddUntilStep("one native icon remains in the same declared region", () =>
                overlay.ModDisplay.ChildrenOfType<ModIcon>().Count() == 1 && placementIsCorrect());
            AddStep("detach the finished native HUD", () => Clear(disposeChildren: true));
            AddUntilStep("the native HUD releases its gameplay barrier", () => isModsDrawableDisposed(drawableRuleset));
            AddStep("restore the default skin", () =>
            {
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;
                archive.Dispose();
            });

            bool placementIsCorrect()
            {
                if (!declared)
                    return overlay.TopRightElements.Anchor == Anchor.TopRight && overlay.TopRightElements.Position == originalTopRightPosition
                        && overlay.TopRightElements.Scale == Vector2.One && overlay.TopRightElements.Margin.Top == 10;
                GameplaySkinLayoutRect rect = scene.Publication.Snapshot.GetSurface(GameplaySkinAuthorLayout.MODS_SURFACE).Rect;
                Drawable canvas = scene.Layers.HudForeground;
                Vector2 expectedTopRight = canvas.ToScreenSpace(new Vector2(rect.Right, rect.Top) * canvas.DrawSize);
                Vector2 expectedBottomLeft = canvas.ToScreenSpace(new Vector2(rect.Left, rect.Bottom) * canvas.DrawSize);
                RectangleF actual = overlay.ModDisplay.ScreenSpaceDrawQuad.AABBFloat;
                return actual.Width > 0 && actual.Height > 0
                    && Math.Abs(actual.Right - expectedTopRight.X) < 1
                    && Math.Abs(actual.Top - expectedTopRight.Y) < 1
                    && actual.Left >= expectedBottomLeft.X - 1 && actual.Bottom <= expectedBottomLeft.Y + 1;
            }
        }

        private static bool isModsDrawableDisposed(Drawable drawable)
            => (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;

        private sealed class ModsLayoutImportTask : ImportTask
        {
            public ModsLayoutImportTask(Stream stream) : base(stream, "native-mods.osk") { }
            public override void DeleteFile() { }
        }
    }
}
