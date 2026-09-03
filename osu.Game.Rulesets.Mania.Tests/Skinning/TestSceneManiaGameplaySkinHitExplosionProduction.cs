// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Database;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneManiaGameplaySkinLayoutProduction
    {
        [Test]
        public void TestHitExplosionUsesExactOverlappingBoundedProductionPool()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            PoolableHitExplosion[] firstLease = Array.Empty<PoolableHitExplosion>();
            long firstObjectId = -1;
            long secondObjectId = -1;
            int runtimeInstancesAfterPreload = -1;
            Note firstNote = null!;
            Note secondNote = null!;

            AddStep("create hit-explosion public package", () =>
            {
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-hit-explosion-{Guid.NewGuid():N}");
                writeHitExplosionOnlyManiaPackage(packageRoot);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for hit-explosion exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount hit-explosion production root", () =>
                Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!)));
            AddUntilStep("wait for hit-explosion production column", () =>
                renderer.Drawable.LoadState >= LoadState.Ready
                && renderer.Drawable.Playfield.Stages.Single().LoadState >= LoadState.Ready
                && renderer.FirstColumn.LoadState >= LoadState.Ready);
            AddStep("create two engine-owned same-lane objects", () =>
            {
                firstNote = new Note { Column = 0, StartTime = 1_100 };
                secondNote = new Note { Column = 0, StartTime = 1_101 };
                renderer.Drawable.Beatmap.HitObjects.Add(firstNote);
                renderer.Drawable.Beatmap.HitObjects.Add(secondNote);
            });
            AddStep("capture fully prebuilt explosion pool", () =>
            {
                sceneHost = renderer.Drawable.GameplaySkinSceneRuntime!;

                while (!sceneHost.IsSceneReady)
                    sceneHost.ProcessFrame();

                Assert.That(sceneHost.IsSceneReady, Is.True);
                Assert.That(renderer.FirstColumn.HitExplosionPoolSize,
                    Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                Assert.That(renderer.FirstColumn.HitExplosionPoolCapacity,
                    Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                runtimeInstancesAfterPreload = sceneHost.RuntimeInstanceCount;
            });
            AddStep("publish two overlapping real results", () =>
            {
                var firstResult = new JudgementResult(firstNote, new ManiaJudgement()) { Type = HitResult.Perfect };
                var secondResult = new JudgementResult(secondNote, new ManiaJudgement()) { Type = HitResult.Great };
                GameplaySkinLaneGroupId groupId = renderer.FirstColumn.ResolvedMaterialKey.Target.GroupId!;
                var identity = (IManiaGameplaySkinObjectIdentityProvider)renderer.Drawable;
                firstObjectId = identity.GetObjectId(firstResult.HitObject, groupId);
                secondObjectId = identity.GetObjectId(secondResult.HitObject, groupId);
                renderer.FirstColumn.ShowHitExplosion(firstResult);
                renderer.FirstColumn.ShowHitExplosion(secondResult);
                firstLease = renderer.FirstColumn.ActiveHitExplosions.ToArray();
                GameplaySkinLaneId laneId = renderer.FirstColumn.LayoutLaneId;

                Assert.Multiple(() =>
                {
                    Assert.That(firstLease, Has.Length.EqualTo(2));
                    Assert.That(firstLease.Select(explosion => explosion.BoundObjectId),
                        Is.EquivalentTo(new long?[] { firstObjectId, secondObjectId }));
                    Assert.That(firstLease.Select(explosion => explosion.BoundObjectId), Is.Unique);
                    Assert.That(firstLease.All(explosion => explosion.SpecialisedSceneVisual?.BoundObjectId == explosion.BoundObjectId), Is.True);
                    Assert.That(firstLease, Has.All.Property(nameof(PoolableHitExplosion.AppliedSceneNodeIds))
                                                       .EqualTo(new[] { "node.hit-explosion" }));
                    Assert.That(firstLease.All(explosion => explosion.SceneVisualGate.Route == GameplaySkinSceneHostRoute.Specialised), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.ResolvedMaterialKey.Target.LaneId == laneId), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.ProgrammaticVisual.Alpha == 0), Is.True,
                        "Each author replacement must gate only its own programmatic effect instance.");
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload),
                        "Judgement handling must not build another scene graph or resource.");
                });
            });
            AddStep("return both overlapping leases", () =>
            {
                foreach (PoolableHitExplosion explosion in firstLease)
                    renderer.FirstColumn.HitObjectArea.Explosions.Remove(explosion, false);
            });
            AddUntilStep("overlapping leases fully unbound", () =>
                renderer.FirstColumn.HitExplosionsInUse == 0
                && !renderer.FirstColumn.ActiveHitExplosions.Any()
                && firstLease.All(explosion => explosion.BoundObjectId == null && explosion.Alpha == 0));
            AddStep("reuse one prebuilt lease", () =>
            {
                var result = new JudgementResult(firstNote, new ManiaJudgement()) { Type = HitResult.Perfect };
                renderer.FirstColumn.ShowHitExplosion(result);
            });
            AddUntilStep("reused lease active", () => renderer.FirstColumn.ActiveHitExplosions.Count() == 1);
            AddStep("assert reuse without reconstruction", () => Assert.Multiple(() =>
            {
                PoolableHitExplosion reused = renderer.FirstColumn.ActiveHitExplosions.Single();
                Assert.That(firstLease, Does.Contain(reused));
                Assert.That(reused.BoundObjectId, Is.EqualTo(firstObjectId));
                Assert.That(renderer.FirstColumn.HitExplosionPoolSize,
                    Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload));
                renderer.FirstColumn.HitObjectArea.Explosions.Remove(reused, false);
            }));
            AddUntilStep("reused lease returned", () => renderer.FirstColumn.HitExplosionsInUse == 0);
            AddStep("saturate exact hard ceiling", () =>
            {
                int attempts = renderer.FirstColumn.HitExplosionPoolCapacity + 2;

                for (int i = 0; i < attempts; i++)
                {
                    var note = new Note { Column = 0, StartTime = 2_000 + i };
                    renderer.FirstColumn.ShowHitExplosion(
                        new JudgementResult(note, new ManiaJudgement()) { Type = HitResult.Perfect });
                }
            });
            AddUntilStep("ceiling leases are active", () =>
                renderer.FirstColumn.ActiveHitExplosions.Count() == renderer.FirstColumn.HitExplosionPoolCapacity);
            AddStep("assert bounded ceiling is observable", () => Assert.Multiple(() =>
            {
                Assert.That(renderer.FirstColumn.HitExplosionPoolSize, Is.EqualTo(renderer.FirstColumn.HitExplosionPoolCapacity));
                Assert.That(renderer.FirstColumn.HitExplosionsInUse, Is.EqualTo(renderer.FirstColumn.HitExplosionPoolCapacity));
                Assert.That(renderer.FirstColumn.HitExplosionCapacityDropCount, Is.EqualTo(2));
                Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload));
            }));
        }

        private static void writeHitExplosionOnlyManiaPackage(string packageRoot)
        {
            writePublicManiaMaterialPackage(packageRoot);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania hit-explosion production\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 4\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "effect.hit-explosion: resource Provide \"public/note\"\n");
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.root",
                    "type": "container",
                    "target": { "kind": "global" },
                    "blend": "inherit",
                    "properties": {},
                    "effects": [],
                    "children": [
                      {
                        "id": "node.hit-explosion",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "effect.hit-explosion",
                        "resource": "texture.note",
                        "blend": "additive",
                        "properties": { "opacity": 0.9, "visible": true },
                        "effects": [],
                        "children": []
                      }
                    ]
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }
    }
}
