// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestManualFirstSceneInBothRulesets(bool grantScript)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Task? authorizationTask = null;

            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, copyManualFirstScene);
            if (grantScript)
            {
                AddStep("grant the tutorial's actual requests", () => authorizationTask = grantManualScript(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!));
                AddUntilStep("tutorial authorization completes", () => authorizationTask?.IsCompleted == true);
                AddStep("authorization succeeds", () => authorizationTask!.GetAwaiter().GetResult());
            }
            AddStep("mount tutorial in real BMS and mania gameplay", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: new[] { 4 });
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both tutorial HUDs are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return new[] { bms, mania }.All(scene => scene.IsSceneReady && scene.TryGetRuntimeNode("lesson.score", out _));
            });
            AddStep("the default tutorial has usable HUD and no progress before the first note", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(manualText(scene, "lesson.score"), Is.EqualTo("0"));
                    Assert.That(c6CandidateNode(scene, "lesson.progress").TransformDrawable.Width, Is.Zero);
                    if (!grantScript)
                        Assert.That(c6CandidateNode(scene, "lesson.accent").TransformDrawable.Scale.X, Is.EqualTo(1));
                }
            });
            AddStep("play a real note in both modes", () =>
            {
                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
                c6Input(renderer, false);
            });
            AddUntilStep("the tutorial displays the real scores", () =>
                manualText(bms, "lesson.score") != "0" && manualText(mania, "lesson.score") != "0");
            AddStep("sample the tutorial after the first judgement", () =>
            {
                for (int time = 2_040; time <= 2_500; time += 20)
                    clock.Sample(time);
            });
            AddStep("progress, rotation and optional gauge response match the real state", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(c6CandidateNode(scene, "lesson.progress").TransformDrawable.Width, Is.GreaterThan(0));
                    Assert.That(c6CandidateNode(scene, "lesson.accent").TransformDrawable.Rotation, Is.EqualTo(90).Within(2));
                    float expectedScale = grantScript ? 1 + (float)authoredInformationSnapshot(scene).Score.Gauge * 0.25f : 1;
                    Assert.That(c6CandidateNode(scene, "lesson.accent").TransformDrawable.Scale.X, Is.EqualTo(expectedScale).Within(0.001f));
                }
            });
            AddStep("pause the actual gameplay clock", () => clock.Stop());
            AddWaitStep("leave the gameplay scene frozen", 3);
            AddStep("pause preserves the displayed gameplay state", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(manualText(scene, "lesson.status"), Is.EqualTo("RUNNING"));
                    Assert.That(c6CandidateNode(scene, "lesson.accent").TransformDrawable.Rotation, Is.EqualTo(90).Within(2));
                }
            });
            AddStep("resume at the same chart time", () =>
            {
                clock.SoftUnpause();
                clock.Sample(2_500);
            });
            AddStep("resume rebuilds the current state without moving the animation", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(manualText(scene, "lesson.status"), Is.EqualTo("RUNNING"));
                    Assert.That(c6CandidateNode(scene, "lesson.accent").TransformDrawable.Rotation, Is.EqualTo(90).Within(2));
                }
            });
        }

        [TestCase("none")]
        [TestCase("0,0,1,1,fit")]
        public void TestNoBgaWindowKeepsTheAuthoredHud(string viewports)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                copyManualFirstScene(root);
                File.AppendAllText(Path.Combine(root, "skin.ini"),
                    $"\n[Bms]\nKeymode: 7K\nBgaViewports: {viewports}\n\n[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k stage-mode=single\nbga.frame: resource Provide \"scene/white\"\n");
                string path = Path.Combine(root, "gameplay-skin.scene.json");
                JObject document = JObject.Parse(File.ReadAllText(path));
                ((JArray)document["root"]!["children"]!).Add(JObject.Parse("""
                    { "id": "lesson.bga", "type": "sprite", "slot": "bga.frame",
                      "target": { "kind": "bga", "index": 0 }, "resource": "lesson.white",
                      "blend": "alpha", "properties": {}, "effects": [], "children": [] }
                    """));
                File.WriteAllText(path, document.ToString());
            });
            AddStep("mount BMS tutorial with no safe BGA window", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.AttachC6GameplayClock(1_000, includeMania: false);
                Add(renderer);
            });
            AddUntilStep("the BMS tutorial is playable", () => renderer.BmsReady);
            AddUntilStep("the ordinary authored HUD still mounts", () =>
            {
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return scene.IsSceneReady && scene.TryGetRuntimeNode("lesson.score", out _);
            });
            AddStep("only the absent BGA decoration is omitted", () =>
            {
                Assert.That(renderer.BmsDrawable.LayoutSnapshot.BgaViewports, Is.Empty);
                Assert.That(scene.TryGetRuntimeNode("lesson.bga", out _), Is.False);
                Assert.That(scene.RuntimeFaults, Is.Empty);
                Assert.That(manualText(scene, "lesson.score"), Is.EqualTo("0"));
                if (viewports != "none")
                    Assert.That(scene.MaterialSet.PersistenceSafeDiagnosticBatch, Does.Contain("bms.layout.bga-viewports-unavailable"));
            });
        }

        private static string manualText(GameplaySkinSceneRuntimeHost scene, string id)
            => ((SpriteText)c6CandidateNode(scene, id).ContentDrawable).Text.ToString();

        private static async Task grantManualScript(GameplaySkinScriptAuthorization authorization)
        {
            Assert.That(await authorization.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
            Assert.That(await authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
        }

        [Test]
        public void TestSimpleSkinInformationRemainsWhenBgaWindowsDisabled()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", "oms-simple.osk"), root);
                File.AppendAllText(Path.Combine(root, "skin.ini"), "\n[Bms]\nKeymode: 7K\nBgaViewports: none\n");
            });
            AddStep("mount a normal template with only BGA windows disabled", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.AttachC6GameplayClock(1_000, includeMania: false);
                Add(renderer);
            });
            AddUntilStep("the normal template is playable", () => renderer.BmsReady);
            AddUntilStep("its original information still appears", () =>
            {
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return scene.IsSceneReady && scene.TryGetRuntimeNode(authoredInformationId(scene, "still.hud.score"), out _);
            });
            AddStep("no BGA window and no missing song or statistics area", () =>
            {
                Assert.That(renderer.BmsDrawable.LayoutSnapshot.BgaViewports, Is.Empty);
                Assert.That(scene.RuntimeFaults, Is.Empty);
                foreach (string id in new[] { "information.song", "information.judgements", "information.tempo", "information.player" })
                    Assert.That(scene.PreparedScene.Snapshot.Surfaces.Any(surface => surface.Id == id), Is.True, id);
                Assert.That(authoredInformationValue(scene, "still.hud.score"), Is.EqualTo("0"));
            });
        }

        private static void copyManualFirstScene(string root)
        {
            string source = Path.Combine(AppContext.BaseDirectory, "SkinManualExamples", "first-scene");
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }
    }
}
