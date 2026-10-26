// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestJudgementTriggeredAnimationUsesRealChargeHeadTailTimesAndIndependentNativeExplosions()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            DrawableBmsHoldNote hold = null!;
            BmsLane lane = null!;
            BmsPoolableHitExplosion first = null!;
            BmsPoolableHitExplosion second = null!;
            GameplaySkinSceneRuntimeNode judgement = null!;

            AddStep("select judgement-triggered author package", () =>
            {
                (_, candidate) = createCandidate(writeJudgementTriggeredFiveKeyPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact triggered revision selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                 && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real charge-note renderer and fixed gameplay clock", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true, useChargeLongNotes: true));
                renderer.ShowBms();
            });
            AddUntilStep("native long-note head and tail loaded", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                    return false;

                hold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().SingleOrDefault()!;
                return hold != null
                       && hold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Count() == 1
                       && hold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Count() == 1;
            });
            AddStep("real head judgement starts frame zero", () =>
            {
                sceneHost = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                lane = renderer.BmsDrawable.Playfield.Lanes.Single(candidateLane => candidateLane.LaneIndex == ((BmsHoldNote)hold.HitObject).LaneIndex);
                Assert.That(sceneHost.TryGetRuntimeNode("node.triggered-judgement", out GameplaySkinSceneRuntimeNode? node), Is.True);
                judgement = node!;
                hold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single().ApplyHeadResult(HitResult.Perfect);
                sceneHost.ProcessFrame();
                first = lane.ActiveHitExplosions.Single();
                assertTriggeredNativeFrame(first.SpecialisedSceneVisual!.RuntimeNodes.Single(), 0, 11);
                assertTriggeredNativeFrame(judgement, 0, 11);
            });
            AddStep("wide author effect remains visible across lane and field edges", () =>
            {
                GameplaySkinSceneRuntimeNode node = first.SpecialisedSceneVisual!.RuntimeNodes.Single();
                Assert.That(node.TransformDrawable.Size, Is.EqualTo(new osuTK.Vector2(3, 4)));
                Drawable image = node.ContentDrawable;
                var expected = image.ScreenSpaceDrawQuad.AABBFloat;
                var laneBounds = lane.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(expected.Left, Is.LessThan(laneBounds.Left));
                Assert.That(expected.Right, Is.GreaterThan(laneBounds.Right));
                Assert.That(expected.Bottom, Is.GreaterThan(laneBounds.Bottom));
                for (Drawable? ancestor = image.Parent; ancestor != null; ancestor = ancestor.Parent)
                {
                    if (ancestor is not CompositeDrawable { Masking: true })
                        continue;
                    var clip = ancestor.ScreenSpaceDrawQuad.AABBFloat;
                    Assert.That(clip.Left, Is.LessThanOrEqualTo(expected.Left + 0.1f), ancestor.GetType().Name);
                    Assert.That(clip.Right, Is.GreaterThanOrEqualTo(expected.Right - 0.1f), ancestor.GetType().Name);
                    Assert.That(clip.Bottom, Is.GreaterThanOrEqualTo(expected.Bottom - 0.1f), ancestor.GetType().Name);
                }
            });
            AddStep("head animation advances on the production gameplay clock", () =>
            {
                renderer.AdvanceBmsTo(1_550);
                sceneHost.ProcessFrame();
                assertTriggeredNativeFrame(first.SpecialisedSceneVisual!.RuntimeNodes.Single(), 50, 19);
                assertTriggeredNativeFrame(judgement, 50, 19);
            });
            AddStep("real tail judgement starts a separate native effect", () =>
            {
                hold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single().ApplyTailResult(HitResult.Perfect, userReleased: false);
                sceneHost.ProcessFrame();
                second = lane.ActiveHitExplosions.Single(explosion => !ReferenceEquals(explosion, first));
                Assert.That(second.BoundObjectId, Is.EqualTo(first.BoundObjectId), "CN head and tail share the exact parent identity.");
                assertTriggeredNativeFrame(first.SpecialisedSceneVisual!.RuntimeNodes.Single(), 50, 19);
                assertTriggeredNativeFrame(second.SpecialisedSceneVisual!.RuntimeNodes.Single(), 0, 11);
                assertTriggeredNativeFrame(judgement, 0, 11);
            });
            AddStep("overlapping real effects retain separate phases after object retirement", () =>
            {
                renderer.AdvanceBmsTo(1_580);
                sceneHost.ProcessFrame();
                assertTriggeredNativeFrame(first.SpecialisedSceneVisual!.RuntimeNodes.Single(), 80, 19);
                assertTriggeredNativeFrame(second.SpecialisedSceneVisual!.RuntimeNodes.Single(), 30, 11);
                assertTriggeredNativeFrame(judgement, 30, 11);
                Assert.That(sceneHost.RuntimeFaults, Is.Empty);
            });
        }

        private static void assertTriggeredNativeFrame(GameplaySkinSceneRuntimeNode node, float rotation, int textureWidth)
        {
            Assert.Multiple(() =>
            {
                Assert.That(node.TransformDrawable.Rotation, Is.EqualTo(rotation).Within(0.001f));
                Assert.That(((Sprite)node.ContentDrawable).Texture!.Width, Is.EqualTo(textureWidth));
            });
        }

        private static void writeJudgementTriggeredFiveKeyPackage(string root)
        {
            writeHitExplosionOnlyFiveKeyPackage(root);
            using (var image = new Image<Rgba32>(19, 17, new Rgba32(210, 120, 35, 255)))
            using (Stream output = File.Create(Path.Combine(root, "notes", "triggered-second.png")))
                image.SaveAsPng(output);

            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            ((JArray)manifest["resources"]!).Add(new JObject
            {
                ["id"] = "texture.triggered-second",
                ["type"] = "texture",
                ["path"] = "notes/triggered-second.png",
            });
            File.WriteAllText(manifestPath, manifest.ToString());
            string path = Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME);
            JObject scene = JObject.Parse(File.ReadAllText(path));
            JObject explosion = ((JArray)scene["root"]!["children"]!).OfType<JObject>()
                .Single(child => (string?)child["id"] == "node.hit-explosion");
            explosion["properties"] = new JObject { ["x"] = -1, ["y"] = 0, ["width"] = 3, ["height"] = 4 };
            ((JArray)scene["root"]!["children"]!).Add(JObject.Parse("""
                {"id":"node.triggered-judgement","type":"sprite","target":{"kind":"stage","id":"bms.group.deck-1","index":0},
                 "slot":"hud.judgement","resource":"texture.public-note","blend":"alpha","properties":{},"effects":[],"children":[]}
                """));
            var tracks = (JArray)scene["tracks"]!;

            foreach (string target in new[] { "node.hit-explosion", "node.triggered-judgement" })
            {
                tracks.Add(new JObject
                {
                    ["id"] = $"{target}.frames",
                    ["type"] = "frame",
                    ["target"] = target,
                    ["property"] = "resource",
                    ["easing"] = "step",
                    ["loop"] = false,
                    ["trigger"] = "judgement.hit",
                    ["keyframes"] = new JArray(
                        new JObject { ["id"] = $"{target}.frame-0", ["time"] = 0, ["value"] = "texture.public-note" },
                        new JObject { ["id"] = $"{target}.frame-1", ["time"] = 40, ["value"] = "texture.triggered-second" }),
                });
                tracks.Add(new JObject
                {
                    ["id"] = $"{target}.rotation",
                    ["type"] = "tween",
                    ["target"] = target,
                    ["property"] = "rotation",
                    ["easing"] = "linear",
                    ["loop"] = false,
                    ["trigger"] = "judgement.hit",
                    ["keyframes"] = new JArray(
                        new JObject { ["id"] = $"{target}.rotation-0", ["time"] = 0, ["value"] = 0 },
                        new JObject { ["id"] = $"{target}.rotation-1", ["time"] = 100, ["value"] = 100 }),
                });
            }

            File.WriteAllText(path, scene.ToString());
        }

        [Test]
        public void TestHitExplosionUsesExactOverlappingBoundedProductionPool()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            BmsLane lane = null!;
            BmsPoolableHitExplosion[] firstLease = Array.Empty<BmsPoolableHitExplosion>();
            BmsHitObject firstNote = null!;
            long firstObjectId = -1;
            long secondObjectId = -1;
            int runtimeInstancesAfterPreload = -1;

            AddStep("create and select BMS hit-explosion package", () =>
            {
                (_, candidate) = createCandidate(
                    writeHitExplosionOnlyFiveKeyPackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact BMS hit-explosion revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount exact BMS production root", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact BMS production lanes", () => renderer.BmsReady);
            AddStep("capture exact hit-explosion lane and prebuilt pool", () =>
            {
                lane = renderer.BmsDrawable.Playfield.Lanes.Single(candidateLane =>
                    candidateLane.LayoutSnapshotLane?.LaneId.Value == "bms.lane.key-1");
                sceneHost = renderer.BmsDrawable.GameplaySkinSceneRuntime!;

                while (!sceneHost.IsSceneReady)
                    sceneHost.ProcessFrame();

                Assert.Multiple(() =>
                {
                    Assert.That(lane.HitExplosionPoolCapacity,
                        Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                    Assert.That(lane.HitExplosionPoolSize, Is.EqualTo(lane.HitExplosionPoolCapacity));
                });
                runtimeInstancesAfterPreload = sceneHost.RuntimeInstanceCount;
            });
            AddStep("publish two overlapping same-lane BMS results", () =>
            {
                firstNote = createHitExplosionNote(lane, 2_000);
                BmsHitObject secondNote = createHitExplosionNote(lane, 2_001);
                renderer.BmsDrawable.Beatmap.HitObjects.Add(firstNote);
                renderer.BmsDrawable.Beatmap.HitObjects.Add(secondNote);
                firstObjectId = renderer.BmsDrawable.GetGameplaySkinObjectId(firstNote);
                secondObjectId = renderer.BmsDrawable.GetGameplaySkinObjectId(secondNote);
                lane.ShowHitExplosion(createHitExplosionResult(firstNote), firstObjectId);
                lane.ShowHitExplosion(createHitExplosionResult(secondNote), secondObjectId);
            });
            AddUntilStep("both BMS author explosions remain active", () =>
                lane.ActiveHitExplosions.Count() == 2
                && lane.ActiveHitExplosions.All(explosion => explosion.BoundObjectId != null));
            AddStep("assert exact BMS overlapping bindings and geometry", () =>
            {
                firstLease = lane.ActiveHitExplosions.ToArray();
                GameplaySkinLaneId expectedLaneId = lane.LayoutSnapshotLane!.LaneId;

                Assert.Multiple(() =>
                {
                    Assert.That(firstLease.Select(explosion => explosion.BoundObjectId),
                        Is.EquivalentTo(new long?[] { firstObjectId, secondObjectId }));
                    Assert.That(firstLease.Select(explosion => explosion.BoundObjectId), Is.Unique);
                    Assert.That(firstLease.All(explosion => explosion.SpecialisedSceneVisual?.BoundObjectId == explosion.BoundObjectId), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.AppliedSceneNodeIds.SequenceEqual(new[] { "node.hit-explosion" })), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.SceneVisualGate.Route == GameplaySkinSceneHostRoute.Specialised), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.ResolvedMaterialKey.Target.LaneId == expectedLaneId), Is.True);
                    Assert.That(firstLease.All(explosion => explosion.ProgrammaticVisual.Alpha == 0), Is.True);
                    Assert.That(firstLease.All(explosion => isDescendantOf(explosion, lane.HitTarget)), Is.True,
                        "The pooled author visual must remain inside the engine-owned BMS hit-target geometry.");
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload));
                });
            });
            AddStep("return both BMS leases", () =>
            {
                foreach (BmsPoolableHitExplosion explosion in firstLease)
                    lane.HitTarget.HitExplosions.Remove(explosion, false);
            });
            AddUntilStep("BMS leases fully unbound", () =>
                lane.HitExplosionsInUse == 0
                && !lane.ActiveHitExplosions.Any()
                && firstLease.All(explosion => explosion.BoundObjectId == null && explosion.Alpha == 0));
            AddStep("reuse one BMS prebuilt lease", () =>
                lane.ShowHitExplosion(createHitExplosionResult(firstNote), firstObjectId));
            AddUntilStep("reused BMS lease active", () => lane.ActiveHitExplosions.Count() == 1);
            AddStep("assert BMS reuse without reconstruction", () => Assert.Multiple(() =>
            {
                BmsPoolableHitExplosion reused = lane.ActiveHitExplosions.Single();
                Assert.That(firstLease, Does.Contain(reused));
                Assert.That(reused.BoundObjectId, Is.EqualTo(firstObjectId));
                Assert.That(lane.HitExplosionPoolSize, Is.EqualTo(lane.HitExplosionPoolCapacity));
                Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload));
                lane.HitTarget.HitExplosions.Remove(reused, false);
            }));
            AddUntilStep("reused BMS lease returned", () => lane.HitExplosionsInUse == 0);
            AddStep("saturate BMS exact hard ceiling", () =>
            {
                int attempts = lane.HitExplosionPoolCapacity + 2;

                for (int i = 0; i < attempts; i++)
                {
                    BmsHitObject note = createHitExplosionNote(lane, 3_000 + i);
                    long objectId = renderer.BmsDrawable.GetGameplaySkinObjectId(note);
                    lane.ShowHitExplosion(createHitExplosionResult(note), objectId);
                }
            });
            AddUntilStep("BMS ceiling leases are active", () => lane.ActiveHitExplosions.Count() == lane.HitExplosionPoolCapacity);
            AddStep("assert BMS bounded ceiling is observable", () => Assert.Multiple(() =>
            {
                Assert.That(lane.HitExplosionPoolSize, Is.EqualTo(lane.HitExplosionPoolCapacity));
                Assert.That(lane.HitExplosionsInUse, Is.EqualTo(lane.HitExplosionPoolCapacity));
                Assert.That(lane.HitExplosionCapacityDropCount, Is.EqualTo(2));
                Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(runtimeInstancesAfterPreload));
            }));
        }

        [Test]
        public void TestChargeLongNoteHeadAndTailShareParentSceneAndJudgementIdentity()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            GameplaySkinEventSubscription subscription = null!;
            var observed = new List<GameplaySkinEventEnvelope>();
            BmsHoldNote hold = null!;
            BmsHitObject adjacentNote = null!;
            DrawableBmsHoldNote drawableHold = null!;
            DrawableBmsHoldNoteHead drawableHead = null!;
            DrawableBmsHoldNoteTail drawableTail = null!;
            BmsLane lane = null!;
            long parentObjectId = -1;
            long adjacentObjectId = -1;
            int projectionPassesAfterHead = -1;

            AddStep("create and select BMS charge-long-note identity package", () =>
            {
                (_, candidate) = createCandidate(
                    writeLongNoteIdentityFiveKeyPackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact BMS charge-long-note revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real charge-long-note production root", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true, useChargeLongNotes: true));
                renderer.ShowBms();
            });
            AddUntilStep("real charge long note and author surfaces ready", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                    return false;

                hold = renderer.BmsBeatmap.HitObjects.OfType<BmsHoldNote>().Single();
                adjacentNote = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>()
                                       .Single(note => note.GetType() == typeof(BmsHitObject) && note.LaneIndex == hold.LaneIndex);
                drawableHold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>()
                                       .SingleOrDefault(candidateDrawable => ReferenceEquals(candidateDrawable.HitObject, hold))!;

                if (drawableHold == null
                    || drawableHold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Count() != 1
                    || drawableHold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Count() != 1)
                    return false;

                BmsAsyncNoteDrawable[] surfaces = drawableHold.ChildrenOfType<BmsAsyncNoteDrawable>()
                                                                .Where(surface => surface.Lookup.Element is BmsNoteSkinElements.LongNoteHead
                                                                    or BmsNoteSkinElements.LongNoteBody
                                                                    or BmsNoteSkinElements.LongNoteTail)
                                                                .ToArray();
                return surfaces.Length == 3
                       && surfaces.All(surface => surface.SpecialisedSceneVisual?.RuntimeNodes.Count > 0);
            });
            AddStep("attach exact gameplay event consumer", () =>
            {
                sceneHost = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                subscription = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                drawableHead = drawableHold.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
                drawableTail = drawableHold.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();
                lane = renderer.BmsDrawable.Playfield.Lanes.Single(candidateLane => candidateLane.LaneIndex == hold.LaneIndex);
                parentObjectId = renderer.BmsDrawable.GetGameplaySkinObjectId(hold);
                adjacentObjectId = renderer.BmsDrawable.GetGameplaySkinObjectId(adjacentNote);
            });
            AddStep("assert nested tree and three specialised surfaces use one parent identity", () =>
            {
                BmsHoldNoteHead head = hold.NestedHitObjects.OfType<BmsHoldNoteHead>().Single();
                BmsHoldNoteTailEvent tail = hold.NestedHitObjects.OfType<BmsHoldNoteTailEvent>().Single();
                BmsAsyncNoteDrawable[] longNoteSurfaces = drawableHold.ChildrenOfType<BmsAsyncNoteDrawable>()
                                                                        .Where(surface => surface.Lookup.Element is BmsNoteSkinElements.LongNoteHead
                                                                            or BmsNoteSkinElements.LongNoteBody
                                                                            or BmsNoteSkinElements.LongNoteTail)
                                                                        .ToArray();
                DrawableBmsHitObject adjacentDrawable = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                                                  .Single(candidateDrawable => ReferenceEquals(candidateDrawable.HitObject, adjacentNote));
                BmsAsyncNoteDrawable adjacentSurface = adjacentDrawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single();
                GameplaySkinEventEnvelope snapshot = observed.Single();
                GameplaySkinObjectStateSnapshot[] activeObjects = ((GameplaySkinStateEventPayload)snapshot.Payload).State.ActiveObjects.ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(renderer.BmsDrawable.GetGameplaySkinObjectId(head), Is.EqualTo(parentObjectId));
                    Assert.That(renderer.BmsDrawable.GetGameplaySkinObjectId(tail), Is.EqualTo(parentObjectId));
                    Assert.That(adjacentObjectId, Is.Not.EqualTo(parentObjectId));
                    Assert.That(activeObjects.Count(state => state.ObjectId == parentObjectId), Is.EqualTo(1));
                    Assert.That(longNoteSurfaces.Select(surface => surface.SpecialisedSceneVisual!.BoundObjectId),
                        Is.All.EqualTo(parentObjectId));
                    Assert.That(longNoteSurfaces.Select(surface => surface.Lookup.Element),
                        Is.EquivalentTo(new[]
                        {
                            BmsNoteSkinElements.LongNoteHead,
                            BmsNoteSkinElements.LongNoteBody,
                            BmsNoteSkinElements.LongNoteTail,
                        }));
                    Assert.That(adjacentSurface.SpecialisedSceneVisual!.BoundObjectId, Is.EqualTo(adjacentObjectId));
                });
            });
            AddStep("apply real charge-long-note head judgement", () =>
            {
                Assert.That(drawableHead.Judged, Is.False);
                drawableHead.ApplyHeadResult(HitResult.Perfect);
            });
            AddUntilStep("head judgement reaches exact parent scene state", () =>
            {
                subscription.DrainFrame(observed.Add);
                return observed.Count(isParentJudgement) == 1
                       && sceneHost.StateMachineStates.Values.Count(state => state == "state.long-note-judged") == 1;
            });
            AddStep("capture head judgement projection", () =>
                projectionPassesAfterHead = sceneHost.StateMachineProjectionPassCount);
            AddStep("apply real charge-long-note tail judgement", () => drawableTail.ApplyTailResult(HitResult.Perfect, userReleased: false));
            AddUntilStep("tail judgement retriggers same parent scene and overlaps head effect", () =>
            {
                subscription.DrainFrame(observed.Add);
                return observed.Count(isParentJudgement) == 2
                       && sceneHost.StateMachineStates.Values.Count(state => state == "state.long-note-judged") == 1
                       && sceneHost.StateMachineProjectionPassCount > projectionPassesAfterHead
                       && lane.ActiveHitExplosions.Count() == 2;
            });
            AddStep("assert both nested judgements stay on parent and never cross adjacent note", () =>
            {
                GameplaySkinJudgementStateSnapshot[] judgements = observed.Where(isParentJudgement)
                                                                          .Select(envelope => ((GameplaySkinJudgementEventPayload)envelope.Payload).State)
                                                                          .ToArray();
                BmsPoolableHitExplosion[] explosions = lane.ActiveHitExplosions.ToArray();
                GameplaySkinLaneGroupId groupId = lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id;
                GameplaySkinLaneId laneId = lane.LayoutSnapshotLane.LaneId;

                Assert.Multiple(() =>
                {
                    Assert.That(judgements, Has.Length.EqualTo(2));
                    Assert.That(judgements.Select(judgement => judgement.ObjectId), Is.All.EqualTo(parentObjectId));
                    Assert.That(judgements.Select(judgement => judgement.GroupId), Is.All.EqualTo(groupId));
                    Assert.That(judgements.Select(judgement => judgement.LaneId), Is.All.EqualTo(laneId));
                    Assert.That(judgements.Any(judgement => judgement.ObjectId == adjacentObjectId), Is.False);
                    Assert.That(explosions, Has.Length.EqualTo(2));
                    Assert.That(explosions.Select(explosion => explosion.BoundObjectId), Is.All.EqualTo(parentObjectId));
                    Assert.That(explosions.All(explosion => explosion.SpecialisedSceneVisual?.BoundObjectId == parentObjectId), Is.True);
                });
            });
            AddStep("detach charge-long-note consumer", () => subscription.Dispose());

            bool isParentJudgement(GameplaySkinEventEnvelope envelope)
                => envelope.EventKind == GameplaySkinEventKind.JudgementApplied
                   && envelope.Payload is GameplaySkinJudgementEventPayload judgement
                   && judgement.State.ObjectId == parentObjectId;
        }

        private static BmsHitObject createHitExplosionNote(BmsLane lane, double startTime)
            => new BmsHitObject
            {
                Keymode = lane.LayoutSnapshot!.Keymode,
                LaneIndex = lane.LaneIndex,
                IsScratch = lane.IsScratch,
                StartTime = startTime,
            };

        private static JudgementResult createHitExplosionResult(BmsHitObject note)
            => new JudgementResult(note, note.CreateJudgement()) { Type = HitResult.Perfect };

        private static void writeHitExplosionOnlyFiveKeyPackage(string root)
        {
            writePublicCommonFiveKeyNotePackage(root);
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
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
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "effect.hit-explosion",
                        "resource": "texture.public-note",
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

        private static void writeLongNoteIdentityFiveKeyPackage(string root)
        {
            writePublicCommonFiveKeyNotePackage(root);
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
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
                        "id": "node.note",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.note",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.5, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-head",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.head",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.6, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-body",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.body",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.4, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-tail",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.tail",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.6, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.hit-explosion",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "effect.hit-explosion",
                        "resource": "texture.public-note",
                        "blend": "additive",
                        "properties": { "opacity": 0.9, "visible": true },
                        "effects": [],
                        "children": []
                      }
                    ]
                  },
                  "tracks": [],
                  "stateMachines": [
                    {
                      "id": "machine.long-note-judgement",
                      "initial": "state.long-note-idle",
                      "states": [
                        {
                          "id": "state.long-note-idle",
                          "set": [
                            { "id": "assignment.long-note-idle", "target": "node.long-note-body", "property": "opacity", "value": 0.4 }
                          ]
                        },
                        {
                          "id": "state.long-note-judged",
                          "set": [
                            { "id": "assignment.long-note-judged", "target": "node.long-note-body", "property": "opacity", "value": 1.0 }
                          ]
                        }
                      ],
                      "transitions": [
                        { "id": "transition.long-note-judged", "from": "state.long-note-idle", "to": "state.long-note-judged", "event": "judgement.hit" }
                      ]
                    }
                  ],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }
    }
}
