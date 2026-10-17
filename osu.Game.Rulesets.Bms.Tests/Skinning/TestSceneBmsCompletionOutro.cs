// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsPlayerSkinEntry
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestRealPlayerFullComboOutroUsesTheValidatedPackageAndGameplayClock(bool authoredOutro)
        {
            Task<Live<SkinInfo>>? import = null;
            Player player = null!;
            DrawableBmsRuleset drawable = null!;
            GameplayClockContainer clock = null!;
            ScoreProcessor score = null!;
            double origin = double.NaN;
            double resultsTime = double.NaN;
            double frozenTime = double.NaN;

            AddStep("import a real minimal completion author package", () =>
            {
                var ruleset = new BmsRuleset();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                var decoded = new BmsBeatmapDecoder().DecodeText(
                    "#TITLE Completion outro\n#BPM 120\n#WAV01 note.wav\n#00111:01\n#00211:01\n", "completion-outro.bme");
                Game.Beatmap.Value = CreateWorkingBeatmap(new BmsDecodedBeatmap(decoded)
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                });
                Game.SelectedMods.Value = new Mod[] { ruleset.GetAutoplayMod()! };
                import = Game.Dependencies.Get<SkinManager>().Import(
                    new ImportTask(completionPackage(authoredOutro), $"completion-outro-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("completion package imported", () => import?.IsCompleted == true);
            AddStep("select the exact imported scene", () =>
                Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value = import!.GetAwaiter().GetResult());
            AddUntilStep("wait for the real skin revision", () =>
                Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.SkinInfo.ID == import!.GetAwaiter().GetResult().ID);
            AddStep("enter the actual autoplay Player", () =>
            {
                var ruleset = new BmsRuleset();
                Game.ScreenStack.Push(new PlayerLoader(() => player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)));
            });
            addUntilPlayerReady(() => player);
            AddStep("observe the actual result transition and score processor", () =>
            {
                drawable = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                score = drawable.Dependencies.Get<ScoreProcessor>();
                player.OnShowingResults += () => resultsTime = clock.CurrentTime;
                Assert.That(drawable.GameplaySkinSceneRuntime!.PreparedScene.Program.CompletionOutro, Is.EqualTo(authoredOutro ? 2500 : 0));
                Assert.That(drawable.GameplaySkinSceneRuntime.GetCompletionOutroDuration(false), Is.Zero);
            });
            AddUntilStep("real engine completes the playable chart", () => score.HasCompleted.Value);
            AddStep("capture the retained engine completion origin", () =>
            {
                GameplaySkinEventStateSnapshot state = captureCompletionState();
                Assert.That(score.IsFullCombo, Is.True);
                Assert.That(score.Combo.Value, Is.EqualTo(score.MaximumCombo));
                Assert.That(state.Completion?.IsFullCombo, Is.True);
                origin = state.Completion!.Value.AppliedTime;
                Assert.That(drawable.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
            });

            if (authoredOutro)
            {
                AddUntilStep("continue past the original one-second result delay", () => clock.CurrentTime >= origin + 1400);
                AddAssert("Player still presents the authored full-combo animation", () => ReferenceEquals(Game.ScreenStack.CurrentScreen, player));
                AddStep("freeze the real gameplay clock inside the outro", () =>
                {
                    clock.Stop();
                    frozenTime = clock.CurrentTime;
                });
                AddWaitStep("wall time does not exhaust a paused gameplay outro", 6);
                AddAssert("results remain held on the same gameplay frame", () =>
                    ReferenceEquals(Game.ScreenStack.CurrentScreen, player) && Math.Abs(clock.CurrentTime - frozenTime) < 0.001);
                AddStep("seek forward inside the same completed outro", () => clock.Seek(origin + 1900));
                AddUntilStep("seek retains the original completed application", () =>
                {
                    GameplaySkinEventStateSnapshot state = captureCompletionState();
                    return state.LifecycleState == GameplaySkinLifecycleState.Completed
                           && state.Completion is { } completion && Math.Abs(completion.AppliedTime - origin) < 0.001;
                });
                AddStep("resume the remaining original outro", () => clock.Start());
            }

            AddUntilStep("Player reaches the real result transition", () => double.IsFinite(resultsTime));
            AddStep("result transition honoured only the selected bounded duration", () =>
            {
                Assert.That(resultsTime, Is.GreaterThanOrEqualTo(origin + (authoredOutro ? 2500 : Player.RESULTS_DISPLAY_DELAY) - 1));
                Assert.That(score.IsFullCombo, Is.True);
                Assert.That(drawable.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
            });

            GameplaySkinEventStateSnapshot captureCompletionState()
            {
                using GameplaySkinEventSubscription subscription = drawable.GameplaySkinEventStream.Subscribe();
                GameplaySkinEventStateSnapshot state = null!;
                subscription.DrainFrame(envelope => state = ((GameplaySkinStateEventPayload)envelope.Payload).State);
                return state;
            }
        }

        private static MemoryStream completionPackage(bool authoredOutro)
        {
            var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                write("skin.ini", """
                    [General]
                    Name: Full combo completion fixture
                    Author: OMS tests
                    Version: 2.7
                    [Bms]
                    Keymode: 7K
                    [GameplaySkin.Common:1]
                    Target: Global ruleset=bms keymode=7k stage-mode=single
                    hud.text: resource Provide "completion/anchor"
                    """);
                using (var image = new Image<Rgba32>(1, 1, new Rgba32(255, 255, 255, 255)))
                using (Stream entry = archive.CreateEntry("completion/anchor.png").Open())
                    image.SaveAsPng(entry);
                write(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, """
                    {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
                     "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1","resources":[]}
                    """);
                write(GameplaySkinSceneContracts.SCENE_FILE_NAME, """
                    {"contract":"oms-gameplay-skin-scene.v1",
                     "root":{"id":"completion.root","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                       {"id":"completion.text","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"FULL COMBO","font-size":24},"effects":[],"children":[]}]},
                     "tracks":[{"id":"completion.fade","type":"tween","target":"completion.text","property":"opacity","easing":"linear","loop":false,"trigger":"gameplay.complete",
                       "keyframes":[{"id":"completion.start","time":0,"value":1},{"id":"completion.end","time":2500,"value":0}]}],
                     "bindings":[{"id":"completion.visible","target":"completion.root","property":"visible","source":"gameplay.full-combo"}],
                     "stateMachines":[],"templates":[],"instances":[],"completionOutro":OUTRO}
                    """.Replace("OUTRO", authoredOutro ? "2500" : "0", StringComparison.Ordinal));

                void write(string path, string content)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                    writer.Write(content);
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
