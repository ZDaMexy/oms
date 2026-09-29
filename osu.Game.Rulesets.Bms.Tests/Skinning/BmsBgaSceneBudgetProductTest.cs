// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
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
        [TestCase("global-effects", true)]
        [TestCase("explicit-effects", false)]
        [TestCase("global-bindings", true)]
        [TestCase("explicit-bindings", false)]
        public void TestBgaBudgetCountsActualWindowProjection(string scenario, bool rejected)
        {
            var selection = new C6ScriptSelection();
            Task<GameplaySkinSceneDiagnosticCode?>? preparation = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
                writeBgaBudgetPackage(root, scenario));
            AddStep("prepare the actual BGA projection and template expansion", () =>
            {
                var revision = manager.CurrentRevision;
                preparation = Task.Run<GameplaySkinSceneDiagnosticCode?>(() =>
                {
                    try
                    {
                        using GameplaySkinLayoutPublication publication = prepareExactBmsPublication(revision,
                            new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } }, CancellationToken.None);
                        Assert.That(publication.Snapshot.BgaViewports, Has.Count.EqualTo(16));
                        return null;
                    }
                    catch (GameplaySkinScenePreparationException exception)
                    {
                        return exception.Code;
                    }
                });
            });
            AddUntilStep("projection budget preparation completes", () => preparation?.IsCompleted == true);
            AddStep("only the actual over-budget arrangement is rejected", () =>
                Assert.That(preparation!.GetAwaiter().GetResult(),
                    Is.EqualTo(rejected ? GameplaySkinSceneDiagnosticCode.BudgetExceeded : (GameplaySkinSceneDiagnosticCode?)null)));
        }

        private static void writeBgaBudgetPackage(string root, string scenario)
        {
            copyManualFirstScene(root);
            string windows = ".70,.10,.001,.001,fit;" + string.Join(';', Enumerable.Repeat(".70,.10,.30,.80,fit", 15));
            File.AppendAllText(Path.Combine(root, "skin.ini"),
                $"\n[Bms]\nKeymode: 7K\nPlayfieldWidth: .25\nBgaViewports: {windows}\n" +
                "\n[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k stage-mode=single\nbga.frame: resource Provide \"scene/white\"\n");
            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            manifest.Remove("script");
            File.WriteAllText(manifestPath, manifest.ToString());

            JObject sceneRoot = budgetNode("budget.root", "container");
            sceneRoot["blend"] = "inherit";
            var document = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = sceneRoot,
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
            };
            bool explicitWindows = scenario.StartsWith("explicit-", System.StringComparison.Ordinal);

            if (scenario.EndsWith("effects", System.StringComparison.Ordinal))
            {
                // A tiny first window must not budget the other fifteen large windows. Conversely, an explicit
                // effect belongs to only one window and must not be multiplied by the complete window count.
                for (int index = 0; index < (explicitWindows ? 16 : 1); index++)
                {
                    JObject node = budgetNode($"budget.effect-node-{index}", "sprite", "bga.frame");
                    if (explicitWindows)
                        node["target"] = bgaTarget(index);
                    for (int effect = 0; effect < (explicitWindows ? 1 : 8); effect++)
                    {
                        ((JArray)node["effects"]!).Add(new JObject
                        {
                            ["id"] = $"budget.effect-{index}-{effect}",
                            ["type"] = "blur",
                            ["properties"] = new JObject { ["radius"] = 1 },
                        });
                    }
                    ((JArray)sceneRoot["children"]!).Add(node);
                }
            }
            else
            {
                JObject templateRoot = budgetNode("budget.template-root", "container", "bga.frame");
                for (int index = 0; index < 128; index++)
                {
                    string id = $"budget.bound-{index}";
                    ((JArray)templateRoot["children"]!).Add(budgetNode(id, "sprite"));
                    foreach (string property in new[] { "x", "y", "rotation", "opacity" })
                    {
                        ((JArray)document["bindings"]!).Add(new JObject
                        {
                            ["id"] = $"budget.binding-{index}-{property}",
                            ["target"] = id,
                            ["property"] = property,
                            ["source"] = "gauge.value",
                        });
                    }
                }
                ((JArray)document["templates"]!).Add(new JObject { ["id"] = "budget.template", ["root"] = templateRoot });
                for (int index = 0; index < 3; index++)
                {
                    var instance = new JObject { ["id"] = $"budget.instance-{index}", ["template"] = "budget.template" };
                    if (explicitWindows)
                        instance["target"] = bgaTarget(index);
                    else
                        instance["material"] = new JObject { ["slot"] = "bga.frame", ["resource"] = "scene/white" };
                    ((JArray)document["instances"]!).Add(instance);
                }
            }
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), document.ToString());
        }

        private static JObject budgetNode(string id, string type, string? slot = null)
        {
            var node = new JObject
            {
                ["id"] = id,
                ["type"] = type,
                ["target"] = new JObject { ["kind"] = "global" },
                ["blend"] = "alpha",
                ["properties"] = new JObject(),
                ["effects"] = new JArray(),
                ["children"] = new JArray(),
            };
            if (slot != null)
                node["slot"] = slot;
            if (type == "sprite")
                node["resource"] = "lesson.white";
            return node;
        }

        private static JObject bgaTarget(int index) => new JObject { ["kind"] = "bga", ["index"] = index };
    }
}
