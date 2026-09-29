// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(5)]
        [TestCase(16)]
        public void TestEveryAuthoredBgaWindowMountsItsFrameWithOnePlayer(int count)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            DefaultBmsBgaPanelDisplay display = null!;

            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                copyManualFirstScene(root);
                string windows = string.Join(';', Enumerable.Range(0, count).Select(index =>
                    FormattableString.Invariant($"{.72f + index % 4 * .06f},{.05f + index / 4 * .20f},.05,.15,fit")));
                File.AppendAllText(Path.Combine(root, "skin.ini"),
                    $"\n[Bms]\nKeymode: 14K\nPlayfieldWidth: .30\nBgaViewports: {windows}\n" +
                    "\n[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=14k stage-mode=dual\nbga.frame: resource Provide \"scene/white\"\n");
                string path = Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME);
                JObject document = JObject.Parse(File.ReadAllText(path));
                ((JArray)document["root"]!["children"]!).Add(JObject.Parse("""
                    { "id": "many.frame", "type": "sprite", "slot": "bga.frame",
                      "target": { "kind": "global" }, "resource": "lesson.white",
                      "blend": "alpha", "properties": {}, "effects": [], "children": [] }
                    """));
                File.WriteAllText(path, document.ToString());
            });
            AddStep("mount the complete authored window arrangement", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager, useFourteenKeyBeatmap: true, includeBgaTimeline: true);
                Add(renderer);
                renderer.ShowBms();
            });
            AddUntilStep("the real BMS host is ready", () => renderer.BmsReady);
            AddUntilStep("every author frame is ready", () =>
            {
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                display ??= (DefaultBmsBgaPanelDisplay)renderer.BmsDrawable.ChildrenOfType<BmsBgaPanel>().Single().Drawable!;
                return scene.IsSceneReady && scene.PendingCreationCount == 0
                                         && display.GameplaySkinFrameSceneVisualsByViewport.Count == count
                                         && display.GameplaySkinFrameSceneVisualsByViewport.All(visual => visual != null && visual.IsLoaded)
                                         && display.NativeFrameVisuals.All(frame => frame.Alpha == 0);
            });
            AddStep("all windows use author frames and one playback owner", () =>
            {
                Assert.That(renderer.BmsDrawable.LayoutSnapshot.BgaViewports, Has.Count.EqualTo(count));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                Assert.That(display.GameplaySkinFrameSceneVisuals, Has.Count.EqualTo(count));
                Assert.That(display.ChildrenOfType<BufferedContainerView<Drawable>>().Count(), Is.EqualTo(count));
                Assert.That(display.ChildrenOfType<BmsBgaPlayer>(), Is.Empty);
                Assert.That(renderer.BmsDrawable.ChildrenOfType<BmsBgaPlayer>().Count(), Is.EqualTo(1));
                Assert.That(display.NativeFrameVisuals.All(frame => frame.Alpha == 0), Is.True);
                Assert.That(display.GameplaySkinFrameSceneVisuals.All(visual =>
                    visual.RuntimeNodes.Single().PreparedNode.Source.Id == "many.frame"), Is.True);
                var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.BgaFrame, GameplaySkinResolvedMaterialTarget.Global);
                Assert.That(scene.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(gate.IsReplacementReady, Is.True);
            });
        }
    }
}
