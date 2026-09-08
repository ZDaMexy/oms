// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestScriptTargetsEveryActualNativeNotePoolCloneAndRevocationRestoresThem()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;
            GameplaySkinSceneRuntimeNode[] clones = Array.Empty<GameplaySkinSceneRuntimeNode>();
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writePublicCommonFiveKeyNotePackage(root);
                string path = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
                JObject manifest = JObject.Parse(File.ReadAllText(path));
                manifest["script"] = GameplaySkinSceneContracts.SCRIPT_FILE_NAME;
                File.WriteAllText(path, manifest.ToString());
                File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), """
                    oms-script 1
                    required gameplay.snapshot.read
                    required gameplay.events.read
                    required scene.numeric.write
                    target node.note rotation
                    set node.note rotation 25
                    halt
                    """);
            });
            AddStep("mount the real five-key native note pool", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                string chart = createC5MatrixChart(BmsKeymode.Key5K)
                            .Replace("#00111:0100", "#00111:0101000000000000000000000000000000000000", StringComparison.Ordinal);
                var fixture = new ExactBmsProductionFixture(chart, "c6-note-clones.bms", BmsPlayfieldStyle.P1,
                    initialGameplayTime: 1_500, keymodeOverride: BmsKeymode.Key5K);
                renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture);
                clock = renderer.AttachC6GameplayClock(1_500, includeMania: false);
                Add(renderer);
            });
            AddUntilStep("multiple actual pooled note visuals share the prepared author source", () =>
            {
                if (!renderer.BmsReady)
                    return false;
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                clones = renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                 .Where(native => native.SpecialisedSceneVisual?.Key.Slot == GameplaySkinSlotCatalog.Note)
                                 .SelectMany(native => native.SpecialisedSceneVisual!.RuntimeNodes)
                                 .Where(node => node.PreparedNode.Source.Id == "node.note")
                                 .ToArray();
                return scene.IsSceneReady && clones.Length >= 2;
            });
            AddStep("all native clones start from their authored base", () => Assert.That(clones.All(node => node.TransformDrawable.Rotation == 0), Is.True));
            AddStep("grant the exact package script", () => permission = grantC6Script(authorization));
            AddUntilStep("the grant completes", () => permission?.IsCompleted == true);
            AddStep("advance the real BMS clock for an admitted script tick", () =>
            {
                permission!.GetAwaiter().GetResult();
                clock.Sample(1_550);
            });
            AddUntilStep("one author target reaches every actual native clone", () => clones.All(node => node.TransformDrawable.Rotation == 25));
            AddStep("revoke the script with the native pool still attached", () =>
                permission = authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Denied));
            AddUntilStep("all native clone transforms are restored", () => clones.All(node => node.TransformDrawable.Rotation == 0));
            AddStep("native visual ownership survives script revocation", () =>
            {
                Assert.That(clones.All(node => node.RootDrawable.Parent != null), Is.True);
                Assert.That(scene.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("native pool and script consumers detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestActualAuthorTemplateCloneApplicationsHaveAnIndependentRuntimeBudget()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            Task? permission = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                string path = Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME);
                JObject document = JObject.Parse(File.ReadAllText(path));
                var children = new JArray();
                foreach (string id in new[] { "clone.a", "clone.b", "clone.c" })
                {
                    children.Add(new JObject
                    {
                        ["id"] = id,
                        ["type"] = "sprite",
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["resource"] = "texture.glow",
                        ["blend"] = "alpha",
                        ["properties"] = new JObject(),
                        ["effects"] = new JArray(),
                        ["children"] = new JArray(),
                    });
                }
                document["templates"] = new JArray(new JObject
                {
                    ["id"] = "template.clone",
                    ["root"] = new JObject
                    {
                        ["id"] = "clone.root",
                        ["type"] = "container",
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["slot"] = "decoration",
                        ["blend"] = "inherit",
                        ["properties"] = new JObject(),
                        ["effects"] = new JArray(),
                        ["children"] = children,
                    },
                });
                document["instances"] = new JArray(Enumerable.Range(0, 1024).Select(index => new JObject
                {
                    ["id"] = $"instance.clone-{index}",
                    ["template"] = "template.clone",
                    ["target"] = new JObject { ["kind"] = "global" },
                }));
                File.WriteAllText(path, document.ToString());
                var declarations = new StringBuilder("oms-script 1\nrequired gameplay.snapshot.read\nrequired gameplay.events.read\nrequired scene.numeric.write\n");
                var code = new StringBuilder();
                foreach (string id in new[] { "clone.a", "clone.b", "clone.c" })
                    foreach (string property in new[] { "alpha", "x", "y", "rotation", "scale-x", "scale-y" })
                    {
                        declarations.Append("target ").Append(id).Append(' ').Append(property).AppendLine();
                        code.Append("set ").Append(id).Append(' ').Append(property).AppendLine(" 0.5");
                    }
                File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), declarations.Append(code).AppendLine("halt").ToString());
            });
            AddStep("grant the bounded source with a large legal template expansion", () =>
                permission = grantC6Script(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!));
            AddUntilStep("the template package grant completes", () => permission?.IsCompleted == true);
            AddStep("mount the real template graph on the BMS production host", () =>
            {
                permission!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("the actual clone application budget isolates only the script", () =>
            {
                if (!renderer.BmsReady)
                    return false;
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return scene.IsSceneReady && scene.ScriptInstance?.Fault?.Code == GameplaySkinScriptFaultCode.NodeLimit;
            });
            AddStep("prove the failure is actual clone fanout rather than VM write quota", () =>
            {
                Assert.That(scene.RuntimeNodeCount, Is.GreaterThan(3_000));
                Assert.That(scene.ScriptInstance!.Fault!.Line, Is.Zero, "Post-VM clone admission produces an engine gate diagnostic, not an opcode source line.");
                Assert.That(scene.ScriptInstance.Profiler.MaximumCallbackInstructions, Is.LessThan(GameplaySkinScriptInstance.MAX_CALLBACK_INSTRUCTIONS));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                Assert.That(scene.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
                Assert.That(scene.TryGetRuntimeNode("instance.clone-0/clone.a", out GameplaySkinSceneRuntimeNode? clone), Is.True);
                Assert.That(clone!.TransformDrawable.Rotation, Is.Zero);
                Assert.That(clone.TransformDrawable.Alpha, Is.EqualTo(1));
            });
            AddStep("detach the bounded template publication", () => renderer.Expire());
            AddUntilStep("the large template scene releases the exact owner", () => renderer.Parent == null);
        }
    }
}
