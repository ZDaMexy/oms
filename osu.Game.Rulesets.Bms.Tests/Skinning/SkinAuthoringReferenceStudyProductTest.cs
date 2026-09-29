// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestManualReferenceStudyInBothRulesets(bool grantScript)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Task? authorizationTask = null;

            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", "oms-simple.osk"), root);
                string source = Path.Combine(AppContext.BaseDirectory, "SkinManualExamples", "reference-study");
                foreach (string filename in new[] { "gameplay-skin.json", "gameplay-skin.scene.json", "gameplay-skin.script" })
                    File.Copy(Path.Combine(source, filename), Path.Combine(root, filename), overwrite: true);
            });
            AddStep("choose the reference study's actual script permissions", () =>
            {
                GameplaySkinScriptAuthorization authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                authorizationTask = grantScript ? grantC6Script(authorization) : denyReferenceStudyScript(authorization);
            });
            AddUntilStep("reference study permission choices complete", () => authorizationTask?.IsCompleted == true);
            AddStep("mount the copied template in both real playfields", () =>
            {
                authorizationTask!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: new[] { 4 });
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both reference scenes are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return new[] { bms, mania }.All(scene => scene.IsSceneReady && scene.TryGetRuntimeNode("ref.score", out _));
            });
            AddStep("bindings and the template state work before player input", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(referenceStudyText(scene, "ref.score"), Is.EqualTo("0"));
                    Assert.That(referenceStudyText(scene, "ref.status"), Is.EqualTo("RUNNING"));
                    Assert.That(referenceStudyNode(scene, "ref.status").InstanceId, Is.Not.EqualTo("ref.status"),
                        "The status must come from the example's instantiated template.");
                    Assert.That(referenceStudyNode(scene, "ref.progress").TransformDrawable.Width, Is.Zero);
                    assertReferenceStudyTexture(scene, "ref.cool");
                    if (!grantScript)
                        Assert.That(referenceStudyNode(scene, "ref.icon").TransformDrawable.Scale.X, Is.EqualTo(1));
                }

                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
            });
            AddStep("play the first real notes", () =>
            {
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
                c6Input(renderer, false);
            });
            AddUntilStep("both examples display successful real play", () =>
                referenceStudyText(bms, "ref.score") != "0" && referenceStudyText(mania, "ref.score") != "0");
            AddStep("the real perfect judgement selects the warm resource", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(authoredInformationSnapshot(scene).Score.Score, Is.GreaterThan(0));
                    assertReferenceStudyTexture(scene, "ref.warm");
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                }
            });
            AddStep("advance the bound progress and animation", () =>
            {
                for (int time = 2_040; time <= 2_500; time += 20)
                    clock.Sample(time);
            });
            AddStep("declarative visuals remain usable with either script choice", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    GameplaySkinSceneRuntimeNode icon = referenceStudyNode(scene, "ref.icon");
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(referenceStudyText(scene, "ref.status"), Is.EqualTo("RUNNING"));
                    Assert.That(referenceStudyText(scene, "ref.score"), Is.Not.EqualTo("0"));
                    Assert.That(referenceStudyNode(scene, "ref.progress").TransformDrawable.Width, Is.GreaterThan(0));
                    Assert.That(icon.TransformDrawable.Rotation, Is.EqualTo(90).Within(2));
                    Assert.That(icon.TransformDrawable.Scale.Y, Is.EqualTo(icon.TransformDrawable.Scale.X));
                    if (grantScript)
                    {
                        float gaugeOnlyScale = 1 + (float)authoredInformationSnapshot(scene).Score.Gauge * 0.15f;
                        Assert.That(icon.TransformDrawable.Scale.X, Is.GreaterThan(gaugeOnlyScale + 0.0001f),
                            "Real judgement history adds a decaying contribution beyond the current gauge.");
                        Assert.That(icon.TransformDrawable.Scale.X, Is.LessThanOrEqualTo(1.55f));
                    }
                    else
                        Assert.That(icon.TransformDrawable.Scale.X, Is.EqualTo(1));
                }
            });
            AddStep("leave both reference study playfields", () => Remove(renderer, disposeImmediately: true));
        }

        private static async Task denyReferenceStudyScript(GameplaySkinScriptAuthorization authorization)
        {
            foreach (string id in new[] { "gameplay.snapshot.read", "gameplay.events.read", "scene.numeric.write" })
                Assert.That(await authorization.SetAsync(id, GameplaySkinScriptAuthorizationChoice.Denied).ConfigureAwait(false), Is.True);
        }

        private static void assertReferenceStudyTexture(GameplaySkinSceneRuntimeHost scene, string resourceId)
            => Assert.That(((Sprite)referenceStudyNode(scene, "ref.icon").ContentDrawable).Texture,
                Is.SameAs(scene.PreparedScene.Resources.Single(resource => resource.Id == resourceId).Texture));

        private static string referenceStudyText(GameplaySkinSceneRuntimeHost scene, string sourceId)
            => ((SpriteText)referenceStudyNode(scene, sourceId).ContentDrawable).Text.ToString();

        private static GameplaySkinSceneRuntimeNode referenceStudyNode(GameplaySkinSceneRuntimeHost scene, string sourceId)
        {
            GameplaySkinPreparedSceneNode prepared = referenceStudyNodes(scene.PreparedScene.Roots).Single(node => node.Source.Id == sourceId);
            return c6CandidateNode(scene, prepared.InstanceId);
        }

        private static IEnumerable<GameplaySkinPreparedSceneNode> referenceStudyNodes(IEnumerable<GameplaySkinPreparedSceneNode> roots)
        {
            foreach (GameplaySkinPreparedSceneNode node in roots)
            {
                yield return node;
                foreach (GameplaySkinPreparedSceneNode child in referenceStudyNodes(node.Children))
                    yield return child;
            }
        }
    }
}
