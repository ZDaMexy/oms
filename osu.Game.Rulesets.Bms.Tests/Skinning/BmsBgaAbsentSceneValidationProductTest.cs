// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase("valid-absent-surface", null)]
        [TestCase("missing-provide", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("slotless-render", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("nested-slot", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("foreign-child-target", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("nonempty-invalid-index", GameplaySkinSceneDiagnosticCode.InvalidIndex)]
        public void TestAbsentBgaSurfaceStillValidatesSceneOwnership(string fault, GameplaySkinSceneDiagnosticCode? expected)
        {
            var selection = new C6ScriptSelection();
            Task<GameplaySkinSceneDiagnosticCode?>? preparation = null;

            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                copyManualFirstScene(root);
                string windows = fault == "nonempty-invalid-index" ? ".70,.10,.20,.20,fit" : "none";
                string declaration = fault == "missing-provide" ? string.Empty :
                    "\n[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k stage-mode=single\nbga.frame: resource Provide \"scene/white\"\n";
                File.AppendAllText(Path.Combine(root, "skin.ini"), $"\n[Bms]\nKeymode: 7K\nBgaViewports: {windows}\n" + declaration);

                JObject bga = JObject.Parse("""
                    { "id": "validation.bga", "type": "sprite", "slot": "bga.frame",
                      "target": { "kind": "bga", "index": 0 }, "resource": "lesson.white",
                      "blend": "alpha", "properties": {}, "effects": [], "children": [] }
                    """);
                if (fault == "slotless-render")
                    bga.Remove("slot");
                if (fault == "nonempty-invalid-index")
                    bga["target"]!["index"] = 1;
                if (fault is "nested-slot" or "foreign-child-target")
                {
                    JObject child = (JObject)bga.DeepClone();
                    child["id"] = "validation.child";
                    if (fault == "foreign-child-target")
                    {
                        child.Remove("slot");
                        child["target"] = JObject.Parse("""{ "kind": "stage", "id": "bms.group.deck-1", "index": 0 }""");
                    }
                    ((JArray)bga["children"]!).Add(child);
                }

                string scenePath = Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME);
                JObject scene = JObject.Parse(File.ReadAllText(scenePath));
                ((JArray)scene["root"]!["children"]!).Add(bga);
                File.WriteAllText(scenePath, scene.ToString());
            });
            AddStep("prepare the selected package against the actual BMS layout", () =>
            {
                var revision = manager.CurrentRevision;
                preparation = Task.Run<GameplaySkinSceneDiagnosticCode?>(() =>
                {
                    try
                    {
                        using GameplaySkinLayoutPublication ignored = prepareExactBmsPublication(revision,
                            new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } }, CancellationToken.None);
                        return null;
                    }
                    catch (GameplaySkinScenePreparationException exception)
                    {
                        return exception.Code;
                    }
                });
            });
            AddUntilStep("layout-dependent author validation completes", () => preparation?.IsCompleted == true);
            AddStep("missing BGA geometry never grants author ownership", () =>
                Assert.That(preparation!.GetAwaiter().GetResult(), Is.EqualTo(expected)));
        }
    }
}
