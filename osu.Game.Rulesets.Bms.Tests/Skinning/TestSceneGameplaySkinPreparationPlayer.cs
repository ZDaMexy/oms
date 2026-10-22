// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Real SkinManager -> PlayerLoader -> Player preparation, using a tiny ordinary author package.
    /// No private artwork, recorded skin events, production player data, or synthetic gameplay clock is used.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneGameplaySkinPreparationPlayer : OsuGameTestScene
    {
        private Task<Live<SkinInfo>>? import;
        private MemoryStream? archive;
        private Player player = null!;
        private GameplayClockContainer clock = null!;
        private DrawableRuleset renderer = null!;
        private GameplaySkinSceneRuntimeHost runtime = null!;
        private GameplaySkinPreparationController preparation = null!;

        protected override double TimePerAction => 25;

        [TestCase(false)]
        [TestCase(true)]
        public void TestBmsAndManiaLoadingAndEntryFadeDoNotConsumePreparation(bool mania)
        {
            loadPlayer(mania, autoplay: false, declaredPreparation: true, blockEntryPresentation: true);
            AddStep("simulate a slow load before the player can be presented", () => Thread.Sleep(1200));
            AddWaitStep("allow the long first parent frame to reach the hidden player", 2);
            AddStep("loading has consumed none of the opening or its ready countdown", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(player.Alpha, Is.Zero);
                    Assert.That(preparation.State.Active, Is.True);
                    Assert.That(preparation.State.Elapsed, Is.Zero);
                    Assert.That(preparation.State.Remaining, Is.EqualTo(5000));
                    Assert.That(clock.IsRunning, Is.False);
                });
                player.ScaleTo(1);
                player.FadeIn(500);
            });
            AddUntilStep("an entry fade still samples the beginning of the artwork", () =>
            {
                if (player.Alpha <= 0)
                    return false;

                Assert.That(player.Alpha, Is.LessThan(1));
                Assert.That(preparation.State.Elapsed, Is.Zero);
                // The next parent frame completes the fade after a slow first presentation. Its whole
                // elapsed interval must not become preparation time just because alpha has reached one.
                Thread.Sleep(1200);
                return true;
            });
            AddUntilStep("the player and its actual author scene become fully visible", () => player.Alpha == 1 && runtime.IsSceneReady);
            AddStep("the first visible sample excludes the earlier loading interval", () =>
            {
                Assert.That(preparation.State.Elapsed, Is.LessThan(250));
                Assert.That(preparation.State.Remaining, Is.GreaterThan(4750));
                Assert.That(clock.IsRunning, Is.False);
            });
            AddUntilStep("ordinary visible updates now advance the full opening", () => preparation.State.Elapsed >= 500);
            AddAssert("ready has not started early", () => !preparation.State.Ready && !clock.IsRunning);
            exitPlayer();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBmsAndManiaNormalRetryStartsAFreshPreparation(bool mania)
        {
            loadPlayer(mania, autoplay: false, declaredPreparation: true);
            Player firstPlayer = null!;
            GameplaySkinPreparationController firstPreparation = null!;
            AddUntilStep("the first player's opening advances normally", () => preparation.State.Elapsed >= 1000);
            AddStep("retry through the real parent loader", () =>
            {
                firstPlayer = player;
                firstPreparation = preparation;
                Assert.That(player.Restart(false), Is.True);
            });
            AddUntilStep("the replacement player and its scene finish loading", () =>
            {
                if (ReferenceEquals(player, firstPlayer) || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                    || !player.LoadedBeatmapSuccessfully)
                    return false;

                renderer = player.ChildrenOfType<DrawableRuleset>().Single();
                if (renderer is not IGameplaySkinSceneRuntimeSource { GameplaySkinSceneRuntime: { IsSceneReady: true } scene })
                    return false;

                runtime = scene;
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                preparation = player.Dependencies.Get<GameplaySkinPreparationController>();
                return true;
            });
            AddStep("retry withdraws the old phase and waits for a new visible opening", () =>
            {
                Assert.That(firstPreparation.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
                Assert.That(preparation, Is.Not.SameAs(firstPreparation));
                Assert.That(preparation.State.Active, Is.True);
                Assert.That(preparation.State.Elapsed, Is.LessThan(1000));
                Assert.That(clock.IsRunning, Is.False);
            });
            AddUntilStep("the replacement opening advances independently", () => preparation.State.Elapsed >= 1000);
            exitPlayer();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBmsAndManiaAutoplayWaitOnceAndShowReadyOnlyInTheActualFinalWindow(bool mania)
        {
            loadPlayer(mania, autoplay: true, declaredPreparation: true);
            double origin = 0;
            double observedElapsed = 0;
            AddStep("record the stopped real clock at entry", () =>
            {
                origin = clock.CurrentTime;
                Assert.That(preparation.State.Active, Is.True);
                Assert.That(preparation.State.Ready, Is.False);
                Assert.That(clock.IsRunning, Is.False);
            });
            AddUntilStep("parent updates advance preparation while gameplay stays at its origin", () =>
                preparation.State.Elapsed >= 1000 && preparation.State.Remaining > 2000);
            AddStep("verify actual scene progress before GET READY", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(clock.IsRunning, Is.False);
                    Assert.That(clock.CurrentTime, Is.EqualTo(origin).Within(0.1));
                    Assert.That(preparation.State.Ready, Is.False);
                    Assert.That(findNode("probe.ready").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(findNode("probe.opening-content").TransformDrawable.Alpha, Is.GreaterThan(0).And.LessThan(1));
                });
                observedElapsed = preparation.State.Elapsed;
            });
            AddUntilStep("the actual last 1500 ms becomes GET READY", () => preparation.State.Ready);
            AddStep("ready comes from the retained engine state and the actual scene binding", () =>
            {
                GameplaySkinPreparationStateSnapshot state = preparation.State;
                Assert.Multiple(() =>
                {
                    Assert.That(state.Elapsed, Is.GreaterThan(observedElapsed));
                    Assert.That(state.Remaining, Is.InRange(0d, 1500d));
                    Assert.That(state.ReadyElapsed, Is.EqualTo(1500 - state.Remaining).Within(0.001));
                    Assert.That(clock.IsRunning, Is.False);
                    Assert.That(clock.CurrentTime, Is.EqualTo(origin).Within(0.1));
                    Assert.That(findNode("probe.ready").TransformDrawable.Alpha, Is.EqualTo(1));
                    Assert.That(findNode("probe.opening-content").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(retainedPreparation(), Is.EqualTo(state));
                });
            });
            AddUntilStep("one preparation naturally releases the real audio clock", () => clock.IsRunning);
            AddStep("completion retains its finished phase instead of starting another delay", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(preparation.State.Available, Is.True);
                    Assert.That(preparation.State.Active, Is.False);
                    Assert.That(preparation.State.Elapsed, Is.GreaterThanOrEqualTo(5000));
                    Assert.That(preparation.State.Elapsed, Is.LessThan(6000));
                    Assert.That(preparation.State.Ready, Is.False);
                    Assert.That(findNode("probe.ready").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(findNode("probe.opening-content").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(runtime.RuntimeFaults, Is.Empty);
                });
            });
            exitPlayer();
        }

        [Test]
        public void TestRealBmsHoldExpiryAndPauseExtendTheCountdownWithoutReplayingCompletedArt()
        {
            loadPlayer(mania: false, autoplay: false, declaredPreparation: true);
            PreparationBmsPlayer bms = null!;
            double heldAfterEntry = 0;
            GameplaySkinPreparationStateSnapshot paused = default;
            AddUntilStep("entry art has started on the parent clock", () => preparation.State.Elapsed >= 1000);
            AddStep("hold the real BMS pre-start action", () =>
            {
                bms = (PreparationBmsPlayer)player;
                heldAfterEntry = preparation.State.Elapsed;
                Assert.That(bms.LegacyDelaySchedules, Is.Zero, "An opt-in BMS player must not stack the old five-second scheduler.");
                Assert.That(bms.BmsInput.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True);
            });
            AddUntilStep("hold really reaches the existing input manager", () => bms.BmsInput.PreStartHoldPressed.Value);
            AddUntilStep("the complete countdown expires while hold remains pressed", () => preparation.State.Remaining == 0);
            AddStep("hold blocks actual audio and the GET READY gate", () =>
            {
                Assert.That(clock.IsRunning, Is.False);
                Assert.That(preparation.State.Ready, Is.False);
                Assert.That(preparation.State.Elapsed, Is.GreaterThanOrEqualTo(heldAfterEntry + 5000));
                Assert.That(findNode("probe.opening-content").TransformDrawable.Alpha, Is.Zero);
                Assert.That(bms.BmsInput.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True);
            });
            AddStep("release creates a fresh delay while finished visuals remain finished", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(preparation.State.Remaining, Is.GreaterThan(4000));
                    Assert.That(preparation.State.Elapsed, Is.GreaterThan(5000));
                    Assert.That(preparation.State.Ready, Is.False);
                    Assert.That(findNode("probe.opening-content").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(clock.IsRunning, Is.False);
                    Assert.That(bms.LegacyDelaySchedules, Is.Zero);
                });
                Assert.That(player.Pause(), Is.True);
                paused = preparation.State;
            });
            AddWaitStep("let ordinary parent updates run under the pause overlay", 10);
            AddStep("pause freezes both preparation counters", () =>
            {
                Assert.That(preparation.State, Is.EqualTo(paused));
                Assert.That(clock.IsRunning, Is.False);
                player.Resume();
            });
            AddUntilStep("resume advances preparation without requiring a gameplay resume countdown", () =>
                preparation.State.Elapsed > paused.Elapsed + 100);
            AddAssert("resume keeps the actual source stopped before the refreshed delay expires", () => !clock.IsRunning);
            exitPlayer();
        }

        [Test]
        public void TestWaitingOnFramesAndExternalStartCannotBypassPreparationAndSkipClearsItsHalfFinishedArt()
        {
            loadPlayer(mania: false, autoplay: false, declaredPreparation: true);
            AddStep("exercise the actual waiting-on-frames callback pair", () =>
            {
                // This is the real FrameStabilityContainer bindable observed by Player, not a skin event.
                // The producer's frame readiness can change synchronously before another parent update.
                var waiting = (Bindable<bool>)renderer.FrameStableClock.WaitingOnFrames;
                waiting.Value = true;
                waiting.Value = false;
                Assert.That(clock.IsRunning, Is.False);
                clock.Stop();
                clock.Start();
            });
            AddWaitStep("allow both source-clock and parent after-children schedulers to run", 4);
            AddStep("the queued premature source start is suppressed", () =>
            {
                Assert.That(preparation.State.Active, Is.True);
                Assert.That(clock.IsRunning, Is.False);
                Assert.That(player.Pause(), Is.True);
                var waiting = (Bindable<bool>)renderer.FrameStableClock.WaitingOnFrames;
                waiting.Value = true;
                waiting.Value = false;
                Assert.That(clock.IsPaused.Value, Is.True, "Frame arrival must not resume under the pause dialog.");
                player.Resume();
            });
            AddUntilStep("the actual opening is visible again before a user skip", () =>
                preparation.State.Active && preparation.State.Elapsed > 500 && preparation.State.Elapsed < 2500
                && findNode("probe.opening-content").TransformDrawable.Alpha > 0);
            AddStep("use the actual intro skip callback", () => ((PreparationBmsPlayer)player).SkipIntro());
            AddUntilStep("the skipped preparation is withdrawn and audio starts at the seek destination", () =>
                !preparation.State.Available && clock.IsRunning);
            AddStep("the opening is hidden instead of frozen at its interrupted phase", () =>
            {
                Assert.That(preparation.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
                Assert.That(findNode("probe.opening").TransformDrawable.Alpha, Is.Zero);
                Assert.That(findNode("probe.ready").TransformDrawable.Alpha, Is.Zero);
                Assert.That(clock.CurrentTime, Is.GreaterThan(1000));
                Assert.That(runtime.RuntimeFaults, Is.Empty);
            });
            exitPlayer();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOrdinarySkinPreservesLegacyBmsDelayAndImmediateManiaStart(bool mania)
        {
            loadPlayer(mania, autoplay: false, declaredPreparation: false);
            AddStep("the ordinary skin has no additional preparation authority", () =>
            {
                Assert.That(preparation.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
                Assert.That(runtime.PreparedScene.Program.PreparationDuration, Is.Zero);
                if (mania)
                    return;

                Assert.That(((PreparationBmsPlayer)player).LegacyDelaySchedules, Is.EqualTo(1));
                Assert.That(clock.IsRunning, Is.False);
            });
            if (mania)
                AddUntilStep("ordinary mania starts its actual source without an added five-second phase", () => clock.IsRunning);
            exitPlayer();
        }

        private void loadPlayer(bool mania, bool autoplay, bool declaredPreparation, bool blockEntryPresentation = false)
        {
            Ruleset ruleset = mania ? new ManiaRuleset() : new BmsRuleset();
            AddStep("create a test-owned ordinary package and chart", () =>
            {
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createChart(ruleset, mania));
                Game.SelectedMods.Value = autoplay ? new[] { ruleset.GetAutoplayMod()! } : Array.Empty<Mod>();
                archive = createPackage(declaredPreparation);
                import = Game.Dependencies.Get<SkinManager>().Import(new FixtureImportTask(archive, $"prepare-{Guid.NewGuid():N}.osk"));
                DismissAnyNotifications();
            });
            AddUntilStep("import the small real package", () => import?.IsCompleted == true);
            AddStep("select the exact mode skin for PlayerLoader", () =>
            {
                Live<SkinInfo> info = import!.GetAwaiter().GetResult();
                var skins = Game.Dependencies.Get<SkinManager>();
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope(mania ? "mania" : "bms"), info.ID);
                skins.CurrentSkinInfo.Value = info;
            });
            AddUntilStep("wait for the coherent selected skin", () =>
                Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.SkinInfo.ID == import!.GetAwaiter().GetResult().ID);
            AddStep("enter the normal player loading screen", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = autoplay ? new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)
                    : mania ? new PreparationManiaPlayer { BlockEntryPresentation = blockEntryPresentation }
                        : new PreparationBmsPlayer { BlockEntryPresentation = blockEntryPresentation })));
            AddUntilStep("the real player and its published author scene finish loading", () =>
            {
                DismissAnyNotifications();
                if (player == null || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player) || !player.LoadedBeatmapSuccessfully)
                    return false;

                renderer = player.ChildrenOfType<DrawableRuleset>().Single();
                // An intentionally fully hidden screen does not update its child scene. Observe its real owner
                // here, then release the test's entry barrier before requiring all scene nodes to be ready.
                if (renderer is not IGameplaySkinSceneRuntimeSource { GameplaySkinSceneRuntime: { } scene }
                    || (!blockEntryPresentation && !scene.IsSceneReady))
                    return false;

                runtime = scene;
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                preparation = player.Dependencies.Get<GameplaySkinPreparationController>();
                return preparation != null;
            });
            AddStep("assert the real selected program controls preparation", () =>
            {
                Assert.That(runtime.PreparedScene.Program.PreparationDuration, Is.EqualTo(declaredPreparation ? 5000 : 0));
                Assert.That(runtime.Publication.Snapshot.Context.PackageRevision.RetainsExact(Game.Dependencies.Get<SkinManager>().CurrentRevision), Is.True);
                Assert.That(runtime.RuntimeFaults, Is.Empty);
            });
        }

        private void exitPlayer()
        {
            AddStep("exit through the actual gameplay action", () => player.ChildrenOfType<HotkeyExitOverlay>().Single().Action());
            AddUntilStep("the player and loader return to the menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
            AddStep("exit withdraws preparation and releases the test archive", () =>
            {
                Assert.That(preparation.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
                archive!.Dispose();
            });
        }

        private GameplaySkinSceneRuntimeNode findNode(string sourceId)
        {
            GameplaySkinPreparedSceneNode prepared = runtime.PreparedScene.Roots.SelectMany(tree).Single(node => node.Source.Id == sourceId);
            Assert.That(runtime.TryGetRuntimeNode(prepared.InstanceId, out GameplaySkinSceneRuntimeNode? node), Is.True);
            return node!;
        }

        private GameplaySkinPreparationStateSnapshot retainedPreparation()
        {
            GameplaySkinEventStateSnapshot? retained = null;
            GameplaySkinEventStream stream = renderer.Dependencies.Get<GameplaySkinEventStream>();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            subscription.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                    retained = snapshot.State;
            });
            Assert.That(retained, Is.Not.Null);
            return retained!.Timing.Preparation;
        }

        private static System.Collections.Generic.IEnumerable<GameplaySkinPreparedSceneNode> tree(GameplaySkinPreparedSceneNode node)
        {
            yield return node;
            foreach (GameplaySkinPreparedSceneNode child in node.Children.SelectMany(tree))
                yield return child;
        }

        private static IBeatmap createChart(Ruleset ruleset, bool mania)
        {
            if (mania)
                return new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    Difficulty = { CircleSize = 4 },
                    HitObjects = { new Note { Column = 0, StartTime = 60000 } },
                };

            var decoded = new BmsBeatmapDecoder().DecodeText(
                "#TITLE Preparation player boundary\n#BPM 120\n#WAV01 note.wav\n#03011:01\n#03016:01\n", "preparation-player.bme");
            return new BmsDecodedBeatmap(decoded) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
        }

        private static MemoryStream createPackage(bool declaredPreparation)
        {
            string ini = $"[General]\nName: Preparation player fixture {Guid.NewGuid():N}\nAuthor: OMS tests\nVersion: 2.7\n"
                         + "[Bms]\nKeymode: 7K\n[GameplaySkin.Common:1]\nTarget: Global ruleset=any keymode=any stage-mode=any\n"
                         + "decoration: resource Provide \"pixel\"\n";
            string scene = """
                {"contract":"oms-gameplay-skin-scene.v1",PREPARATION
                 "root":{"id":"probe.root","type":"container","target":{"kind":"global"},"blend":"inherit",
                   "properties":{},"effects":[],"children":[
                     {"id":"probe.visuals","type":"container","target":{"kind":"global"},"slot":"decoration","blend":"inherit",
                      "properties":{},"effects":[],"children":[
                       {"id":"probe.opening","type":"container","target":{"kind":"global"},"blend":"inherit",
                        "properties":{"visible":false},"effects":[],"children":[
                          {"id":"probe.opening-content","type":"sprite","target":{"kind":"global"},"resource":"probe.pixel","blend":"alpha",
                           "properties":{"x":0.1,"y":0.1,"width":0.1,"height":0.1,"opacity":0},"effects":[],"children":[]}]},
                       {"id":"probe.ready","type":"sprite","target":{"kind":"global"},"resource":"probe.pixel","blend":"alpha",
                        "properties":{"x":0.2,"y":0.1,"width":0.1,"height":0.1,"visible":false},"effects":[],"children":[]}
                     ]}]},
                 "tracks":[{"id":"probe.opening-clock","type":"tween","target":"probe.opening-content","property":"opacity",
                   "easing":"linear","loop":false,"clock":"preparation.elapsed","keyframes":[
                    {"id":"probe.opening-first","time":0,"value":1},{"id":"probe.opening-last","time":3000,"value":0}]}],
                 "bindings":[{"id":"probe.opening-visible","target":"probe.opening","property":"visible","source":"preparation.available"},
                   {"id":"probe.ready-visible","target":"probe.ready","property":"visible","source":"preparation.ready"}],
                 "variants":[],"stateMachines":[],"templates":[],"instances":[]}
                """.Replace("PREPARATION", declaredPreparation ? "\"preparationDuration\":5000," : "", StringComparison.Ordinal);
            const string manifest = """
                {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
                 "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1",
                 "resources":[{"id":"probe.pixel","type":"texture","path":"pixel.png"}]}
                """;
            var result = new MemoryStream();
            using (var zip = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
            {
                write(zip, "skin.ini", Encoding.UTF8.GetBytes(ini));
                write(zip, "gameplay-skin.json", Encoding.UTF8.GetBytes(manifest));
                write(zip, "gameplay-skin.scene.json", Encoding.UTF8.GetBytes(scene));
                using var image = new Image<Rgba32>(2, 2, new Rgba32(32, 128, 240, 255));
                using var png = new MemoryStream();
                image.SaveAsPng(png);
                write(zip, "pixel.png", png.ToArray());
            }
            result.Position = 0;
            return result;

            static void write(ZipArchive zip, string name, byte[] bytes)
            {
                using Stream stream = zip.CreateEntry(name).Open();
                stream.Write(bytes);
            }
        }

        private partial class PreparationBmsPlayer : BmsSoloPlayer
        {
            public bool BlockEntryPresentation { get; init; }

            public int LegacyDelaySchedules { get; private set; }

            public BmsInputManager BmsInput => (BmsInputManager)((DrawableBmsRuleset)DrawableRuleset).GameplayInputManager!;

            public void SkipIntro() => SkipIntroOverlay.RequestSkip();

            public override void OnEntering(ScreenTransitionEvent e)
            {
                base.OnEntering(e);
                if (!BlockEntryPresentation)
                    return;

                this.ClearTransforms();
                Alpha = 0;
            }

            protected override ScheduledDelegate SchedulePreStartDelay(Action onElapsed)
            {
                LegacyDelaySchedules++;
                return base.SchedulePreStartDelay(onElapsed);
            }

            protected override Task ImportScore(osu.Game.Scoring.Score score) => Task.CompletedTask;
        }

        private partial class PreparationManiaPlayer : SoloPlayer
        {
            public bool BlockEntryPresentation { get; init; }

            public override void OnEntering(ScreenTransitionEvent e)
            {
                base.OnEntering(e);
                if (!BlockEntryPresentation)
                    return;

                this.ClearTransforms();
                Alpha = 0;
            }

            protected override Task ImportScore(osu.Game.Scoring.Score score) => Task.CompletedTask;
        }

        private sealed class FixtureImportTask : ImportTask
        {
            public FixtureImportTask(Stream stream, string filename) : base(stream, filename)
            {
            }

            public override void DeleteFile()
            {
                // All bytes belong to this fixture's memory stream; no external file is owned.
            }
        }
    }
}
