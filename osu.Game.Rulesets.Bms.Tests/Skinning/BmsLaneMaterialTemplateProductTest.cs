// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestLaneMaterialTemplatesKeepNativeKeysAndJudgementEffectsIndependent(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            GameplaySkinEventSubscription subscription = null!;
            var observed = new List<GameplaySkinEventEnvelope>();
            BmsLane firstLane = null!;
            BmsLane secondLane = null!;
            BmsHitObject firstNote = null!;
            BmsHitObject secondNote = null!;
            DrawableBmsHitObject firstDrawableNote = null!;
            DrawableBmsHitObject secondDrawableNote = null!;
            GameplaySkinSpecialisedSceneVisual firstKey = null!;
            GameplaySkinSpecialisedSceneVisual secondKey = null!;
            BmsPoolableHitExplosion firstEffect = null!;
            BmsPoolableHitExplosion secondEffect = null!;
            OmsAction firstAction = default;
            OmsAction secondAction = default;
            int prebuiltRuntimeInstances = 0;
            string? lastNativeState = null;

            void reportNativeState(string phase)
            {
                BmsInputManager input = renderer.BmsDrawable.GameplayInputManager!;
                string state = $"{phase}: time={renderer.BmsDrawable.GameplaySkinEventRuntime!.AuthoritativeGameplayTime:0.###}; "
                               + $"first={firstLane.LayoutSnapshotLane!.LaneId.Value}/{firstAction} "
                               + $"pressed={input.KeyBindingContainer.PressedActions.Contains(firstLane.LayoutLane.Action)} "
                               + $"note={firstDrawableNote.Result?.Type.ToString() ?? "returned-to-pool"} display={firstDrawableNote.DisplayResult} "
                               + $"key={laneMaterialSprite(firstKey, "key").TransformDrawable.Alpha} "
                               + $"effects={firstLane.ActiveHitExplosions.Count()} applied={firstEffect?.SpecialisedSceneVisual?.IsApplied}; "
                               + $"second={secondLane.LayoutSnapshotLane!.LaneId.Value}/{secondAction} "
                               + $"pressed={input.KeyBindingContainer.PressedActions.Contains(secondLane.LayoutLane.Action)} "
                               + $"note={secondDrawableNote.Result?.Type.ToString() ?? "returned-to-pool"} display={secondDrawableNote.DisplayResult} "
                               + $"key={laneMaterialSprite(secondKey, "key").TransformDrawable.Alpha} "
                               + $"effects={secondLane.ActiveHitExplosions.Count()} applied={secondEffect?.SpecialisedSceneVisual?.IsApplied}; "
                               + $"input-events={observed.Count(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed)} "
                               + $"judgement-events={observed.Count(envelope => envelope.EventKind == GameplaySkinEventKind.JudgementApplied)} "
                               + $"faults={string.Join(",", sceneHost.RuntimeFaults)}";

                if (state == lastNativeState)
                    return;

                lastNativeState = state;
                TestContext.Progress.WriteLine($"Lane material {keymode}/{style}: {state}");
            }

            AddStep("select one SP and DP lane-template package", () =>
            {
                (_, candidate) = createCandidate(writeLaneMaterialTemplatePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact lane-template package selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                       && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real fixed-clock production lanes", () =>
            {
                var fixture = new ExactBmsProductionFixture(createLaneMaterialTemplateChart(keymode), "lane-material.bme", style,
                    initialGameplayTime: 1_500, keymodeOverride: keymode);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("native key owners effects and notes are prepared", () =>
            {
                if (!renderer.BmsReady)
                    return false;
                sceneHost ??= renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                return sceneHost.IsSceneReady && sceneHost.PendingCreationCount == 0
                       && renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                  .Count(note => note.HitObject.GetType() == typeof(BmsHitObject))
                       == renderer.BmsDrawable.LayoutSnapshot.Neutral.Context.Topology.LanesInLogicalOrder.Count;
            });
            AddStep("capture two independently scoped native lanes", () =>
            {
                BmsGameplayLayoutSnapshot layout = renderer.BmsDrawable.LayoutSnapshot;
                BmsLane[] keys = renderer.BmsDrawable.Playfield.Lanes.Where(lane => !lane.LayoutSnapshotLane!.IsScratch)
                                         .OrderBy(lane => lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.GlobalLogicalIndex).ToArray();
                firstLane = keys[0];
                secondLane = keymode == BmsKeymode.Key14K
                    ? keys.First(lane => !lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(
                        firstLane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id))
                    : keys[1];
                firstNote = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>().Single(note => note.LaneIndex == firstLane.LaneIndex);
                secondNote = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>().Single(note => note.LaneIndex == secondLane.LaneIndex);
                firstDrawableNote = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                            .Single(note => ReferenceEquals(note.HitObject, firstNote));
                secondDrawableNote = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                             .Single(note => ReferenceEquals(note.HitObject, secondNote));
                firstKey = firstLane.HitTarget.KeyVisual!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single();
                secondKey = secondLane.HitTarget.KeyVisual!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single();
                Assert.That(OmsBmsActionMap.TryMapToOmsAction(renderer.BmsDrawable.Variant, firstLane.LayoutLane.Action, out firstAction), Is.True);
                Assert.That(OmsBmsActionMap.TryMapToOmsAction(renderer.BmsDrawable.Variant, secondLane.LayoutLane.Action, out secondAction), Is.True);
                Assert.That(firstAction, Is.Not.EqualTo(secondAction));
                Assert.That(secondNote.StartTime, Is.EqualTo(firstNote.StartTime));
                if (keymode == BmsKeymode.Key14K)
                {
                    Assert.That(secondLane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id,
                        Is.Not.EqualTo(firstLane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id),
                        "The DP input journey must exercise a separate native deck.");
                }
                subscription = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                prebuiltRuntimeInstances = sceneHost.RuntimeInstanceCount;

                assertLaneMaterialNativeOwner(firstKey, firstLane.HitTarget.KeyVisual!, firstLane, "key", layout);
                assertLaneMaterialNativeOwner(secondKey, secondLane.HitTarget.KeyVisual!, secondLane, "key", layout);
                GameplaySkinPreparedSceneNode[] prepared = sceneHost.PreparedScene.Roots.Where(node => node.Source.Id != "material.dispatch").ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(prepared, Has.Length.EqualTo(layout.Neutral.Context.Topology.LanesInLogicalOrder.Count * 2));
                    Assert.That(prepared.All(node => node.ResolvedTarget.Kind == GameplaySkinSceneTargetKind.Lane), Is.True);
                    Assert.That(prepared.All(node => node.Source.Target.Kind == GameplaySkinSceneTargetKind.Global), Is.True,
                        "Source placeholders are resolved only from exact winning Lane material entries.");
                    Assert.That(prepared.All(node => node.Children.Single().ResolvedTarget.StableId == node.ResolvedTarget.StableId
                                                     && node.Children.Single().ResolvedTarget.Index == node.ResolvedTarget.Index), Is.True);
                    Assert.That(laneMaterialSprite(firstKey, "key").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(laneMaterialSprite(secondKey, "key").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(firstLane.HitExplosionPoolSize, Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                    Assert.That(secondLane.HitExplosionPoolSize, Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                    Assert.That(prebuiltRuntimeInstances, Is.LessThanOrEqualTo(sceneHost.PreparedScene.ReservedRuntimeInstanceCount));
                    Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                });
            });
            // AdvanceBmsTo sets the fixed source, but one FrameStabilityContainer update has a 10ms catch-up
            // budget. Wait for the real consuming clock before injecting input or asserting an exact phase.
            AddUntilStep("settle the production clock at the first note before accepting input", () =>
            {
                renderer.AdvanceBmsTo(firstNote.StartTime);
                return Math.Abs(renderer.BmsDrawable.FrameStableClock.CurrentTime - firstNote.StartTime) < 0.001;
            });
            AddStep("hit the first real note at its source time", () =>
            {
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(firstAction), Is.True);
                // Settle native input polling and pool activation as well as the scene consumer, without advancing
                // the judgement's gameplay-time origin or relying on the outer test runner's update ordering.
                renderer.AdvanceBmsTo(firstNote.StartTime);
            });
            AddUntilStep("first native key and hit effect consume its exact lane event", () =>
            {
                sceneHost.ProcessFrame();
                subscription.DrainFrame(observed.Add);
                firstEffect = firstLane.ActiveHitExplosions.SingleOrDefault()!;
                reportNativeState("first input");
                return firstEffect?.SpecialisedSceneVisual?.IsApplied == true
                       && laneMaterialSprite(firstKey, "key").TransformDrawable.Alpha == 1
                       && laneMaterialSprite(secondKey, "key").TransformDrawable.Alpha == 0;
            });
            AddUntilStep("settle the production clock fifty milliseconds after the first hit", () =>
            {
                renderer.AdvanceBmsTo(secondNote.StartTime + 50);
                return Math.Abs(renderer.BmsDrawable.FrameStableClock.CurrentTime - (secondNote.StartTime + 50)) < 0.001;
            });
            AddStep("hit the second real note fifty milliseconds later", () =>
            {
                sceneHost.ProcessFrame();
                assertLaneMaterialPhase(firstEffect.SpecialisedSceneVisual!, 50);
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(secondAction), Is.True);
                renderer.AdvanceBmsTo(secondNote.StartTime + 50);
            });
            AddUntilStep("two native effects and both key owners are independently active", () =>
            {
                sceneHost.ProcessFrame();
                subscription.DrainFrame(observed.Add);
                secondEffect = secondLane.ActiveHitExplosions.SingleOrDefault()!;
                reportNativeState("second input");
                return secondEffect?.SpecialisedSceneVisual?.IsApplied == true
                       && laneMaterialSprite(firstKey, "key").TransformDrawable.Alpha == 1
                       && laneMaterialSprite(secondKey, "key").TransformDrawable.Alpha == 1;
            });
            AddStep("verify native geometry exact identities and separate animation origins", () =>
            {
                assertLaneMaterialNativeOwner(firstEffect.SpecialisedSceneVisual!, firstEffect, firstLane, "hit", renderer.BmsDrawable.LayoutSnapshot);
                assertLaneMaterialNativeOwner(secondEffect.SpecialisedSceneVisual!, secondEffect, secondLane, "hit", renderer.BmsDrawable.LayoutSnapshot);
                assertLaneMaterialPhase(firstEffect.SpecialisedSceneVisual!, 50);
                assertLaneMaterialPhase(secondEffect.SpecialisedSceneVisual!, 0);
                assertLaneMaterialEvents(observed, firstLane, renderer.BmsDrawable.GetGameplaySkinObjectId(firstNote));
                assertLaneMaterialEvents(observed, secondLane, renderer.BmsDrawable.GetGameplaySkinObjectId(secondNote));
                Assert.Multiple(() =>
                {
                    Assert.That(firstEffect.BoundObjectId, Is.EqualTo(renderer.BmsDrawable.GetGameplaySkinObjectId(firstNote)));
                    Assert.That(secondEffect.BoundObjectId, Is.EqualTo(renderer.BmsDrawable.GetGameplaySkinObjectId(secondNote)));
                    Assert.That(firstEffect.BoundObjectId, Is.Not.EqualTo(secondEffect.BoundObjectId));
                    Assert.That(firstEffect.ProgrammaticVisual.Alpha, Is.Zero);
                    Assert.That(secondEffect.ProgrammaticVisual.Alpha, Is.Zero);
                    Assert.That(isDescendantOf(firstEffect, firstLane.HitTarget), Is.True);
                    Assert.That(isDescendantOf(secondEffect, secondLane.HitTarget), Is.True);
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(prebuiltRuntimeInstances));
                    Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                });
            });
            AddStep("release one real key", () =>
            {
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(firstAction), Is.True);
            });
            AddUntilStep("settle the production clock eighty milliseconds after the first hit", () =>
            {
                renderer.AdvanceBmsTo(secondNote.StartTime + 80);
                return Math.Abs(renderer.BmsDrawable.FrameStableClock.CurrentTime - (secondNote.StartTime + 80)) < 0.001;
            });
            AddStep("retain both original effect clocks after the release", () =>
            {
                sceneHost.ProcessFrame();
                assertLaneMaterialPhase(firstEffect.SpecialisedSceneVisual!, 80);
                assertLaneMaterialPhase(secondEffect.SpecialisedSceneVisual!, 30);
            });
            AddUntilStep("released key hides independently from the second key and both hit effects", () =>
            {
                sceneHost.ProcessFrame();
                return laneMaterialSprite(firstKey, "key").TransformDrawable.Alpha == 0
                       && laneMaterialSprite(secondKey, "key").TransformDrawable.Alpha == 1;
            });
            AddStep("finish exact lane-template journey", () =>
            {
                Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(secondAction), Is.True);
                subscription.Dispose();
                renderer.Expire();
            });
            AddUntilStep("native lane-template owners detached", () => renderer.Parent == null);
        }

        [TestCase(BmsKeymode.Key5K, false)]
        [TestCase(BmsKeymode.Key9K_Bms, false)]
        [TestCase(BmsKeymode.Key9K_Pms, false)]
        [TestCase(BmsKeymode.Key7K, true)]
        public void TestLaneMaterialTemplatesDoNotExpandIntoUnmatchedBmsOrManiaModes(BmsKeymode keymode, bool mania)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            AddStep("select the same SP and DP lane-template package", () =>
            {
                (_, candidate) = createCandidate(writeLaneMaterialTemplatePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact lane-template revision selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                          && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount a mode with no matching INI material", () =>
            {
                var fixture = new ExactBmsProductionFixture(createC5MatrixChart(keymode),
                    keymode == BmsKeymode.Key9K_Pms ? "lane-fallback.pms" : "lane-fallback.bms", BmsPlayfieldStyle.Center,
                    keymodeOverride: keymode);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                if (mania)
                    renderer.ShowMania();
                else
                    renderer.ShowBms();
            });
            AddUntilStep("unmatched native mode and scene are ready", () =>
            {
                if (mania ? !renderer.ManiaReady : !renderer.BmsReady)
                    return false;
                sceneHost ??= ((IGameplaySkinSceneRuntimeSource)(mania ? renderer.ManiaDrawable : renderer.BmsDrawable)).GameplaySkinSceneRuntime!;
                return sceneHost.IsSceneReady && sceneHost.PendingCreationCount == 0;
            });
            AddStep("assert fallback has no author lane expansion or target fault", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sceneHost.PreparedScene.Roots, Has.Count.EqualTo(1));
                    Assert.That(sceneHost.PreparedScene.Roots.Single().Source.Id, Is.EqualTo("material.dispatch"));
                    Assert.That(sceneHost.PreparedScene.Roots.Single().Children, Is.Empty);
                    Assert.That(sceneHost.HostedSlots.SelectMany(slot => slot.RoutedNodes), Is.Empty);
                    Assert.That(sceneHost.MaterialSet.Entries.Any(entry => entry.Source.IsSelectedDocumentDeclaration), Is.False);
                    Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                    Assert.That(sceneHost.PreparedScene.ReservedRuntimeInstanceCount, Is.GreaterThan(0));
                });
                if (mania)
                    Assert.That(renderer.ManiaDrawable.Playfield.Stages.All(stage => stage.Columns.All(column => column.IsLoaded)), Is.True);
                else
                    Assert.That(renderer.BmsDrawable.Playfield.Lanes.All(lane => lane.IsLoaded), Is.True);
                renderer.Expire();
            });
            AddUntilStep("fallback native mode detached", () => renderer.Parent == null);
        }

        private static void assertLaneMaterialNativeOwner(
            GameplaySkinSpecialisedSceneVisual visual, Drawable native, BmsLane lane, string kind, BmsGameplayLayoutSnapshot layout)
        {
            GameplaySkinLaneTopologyEntry exact = lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry;
            Assert.Multiple(() =>
            {
                Assert.That(visual.IsApplied, Is.True);
                Assert.That(isDescendantOf(visual, (osu.Framework.Graphics.Containers.CompositeDrawable)native), Is.True);
                Assert.That(visual.Key.Target.LaneId, Is.EqualTo(exact.Identity.Id));
                Assert.That(visual.Key.Target.GlobalLogicalIndex, Is.EqualTo(exact.GlobalLogicalIndex));
                Assert.That(visual.RuntimeNodes, Has.Count.EqualTo(2));
                Assert.That(visual.RuntimeNodes.All(node => node.PreparedNode.ResolvedTarget.Kind == GameplaySkinSceneTargetKind.Lane
                                                         && node.PreparedNode.ResolvedTarget.StableId == exact.Identity.Id.Value
                                                         && node.PreparedNode.ResolvedTarget.Index == exact.GlobalLogicalIndex), Is.True);
                Assert.That(visual.RuntimeNodes.All(node => node.PreparedNode.OwningSlotKey!.Equals(visual.Key)), Is.True);
                Assert.That(visual.RuntimeNodes.All(node => node.Rect.Equals(visual.RuntimeNodes[0].Rect)), Is.True,
                    "Both root and child use the exact owned native surface, rather than the global source placeholder.");
                Assert.That(visual.RuntimeNodes.All(node => node.PreparedNode.InstanceId.StartsWith($"instance.{kind}/lane-{exact.GlobalLogicalIndex}/", StringComparison.Ordinal)), Is.True);
                Assert.That(visual.RootDrawables.Single().Position, Is.EqualTo(Vector2.Zero));
                Assert.That(visual.RootDrawables.Single().Size, Is.EqualTo(Vector2.One));
                Assert.That(visual.Key.Target.Matches(layout.Neutral.Context.Topology), Is.True);
            });
        }

        private static GameplaySkinSceneRuntimeNode laneMaterialSprite(GameplaySkinSpecialisedSceneVisual visual, string kind)
            => visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == kind + ".sprite");

        private static void assertLaneMaterialPhase(GameplaySkinSpecialisedSceneVisual visual, float milliseconds)
            => Assert.That(laneMaterialSprite(visual, "hit").TransformDrawable.Rotation, Is.EqualTo(milliseconds).Within(0.001f));

        private static void assertLaneMaterialEvents(IReadOnlyList<GameplaySkinEventEnvelope> observed, BmsLane lane, long objectId)
        {
            GameplaySkinLaneTopologyEntry exact = lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry;
            GameplaySkinJudgementEventPayload judgement = observed.Where(envelope => envelope.EventKind == GameplaySkinEventKind.JudgementApplied)
                .Select(envelope => envelope.Payload).OfType<GameplaySkinJudgementEventPayload>().Single(payload => payload.State.ObjectId == objectId);
            Assert.Multiple(() =>
            {
                Assert.That(observed.Any(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                    && envelope.GroupId?.Equals(exact.Identity.Group.Id) == true
                                                    && envelope.LaneId?.Equals(exact.Identity.Id) == true), Is.True);
                Assert.That(judgement.State.GroupId, Is.EqualTo(exact.Identity.Group.Id));
                Assert.That(judgement.State.LaneId, Is.EqualTo(exact.Identity.Id));
            });
        }

        private static void writeLaneMaterialTemplatePackage(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "notes"));
            var ini = new StringBuilder("[General]\nName: SP and DP material-selected lane templates\nAuthor: OMS tests\nVersion: 2.7\n"
                                       + "[Bms]\nKeymode: 7K\n[Bms]\nKeymode: 14K\n[GameplaySkin.Common:1]\n");
            foreach (var mode in new[]
                     {
                         (Keymode: BmsKeymode.Key7K, Style: BmsPlayfieldStyle.P1),
                         (Keymode: BmsKeymode.Key7K, Style: BmsPlayfieldStyle.P2),
                         (Keymode: BmsKeymode.Key14K, Style: BmsPlayfieldStyle.Center),
                     })
            {
                GameplaySkinLaneTopologySnapshot topology = BmsGameplaySkinLaneTopologyFactory.Create(mode.Keymode, mode.Style).Topology;
                foreach (GameplaySkinLaneTopologyGroup group in topology.GroupsInLogicalOrder)
                    foreach (GameplaySkinLaneTopologyEntry lane in group.LanesInLogicalOrder)
                    {
                        appendDocumentTarget(ini, GameplaySkinResolvedMaterialTarget.ForLane(group, lane), getKeymodeId(mode.Keymode),
                            topology.GroupsInLogicalOrder.Count == 2 ? "dual" : "single");
                        ini.AppendLine("playfield.key: resource Provide \"notes/material-key\"");
                        ini.AppendLine("effect.hit-explosion: resource Provide \"notes/material-hit\"");
                    }
            }
            File.WriteAllText(Path.Combine(root, "skin.ini"), ini.ToString());
            using (var image = new Image<Rgba32>(13, 17, new Rgba32(40, 180, 225, 255)))
                image.SaveAsPng(Path.Combine(root, "notes", "material-key.png"));
            using (var image = new Image<Rgba32>(19, 23, new Rgba32(230, 190, 25, 255)))
                image.SaveAsPng(Path.Combine(root, "notes", "material-hit.png"));
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(new[] { "key", "hit" }.Select(kind => new JObject
                {
                    ["id"] = "texture." + kind,
                    ["type"] = "texture",
                    ["path"] = $"notes/material-{kind}.png",
                })),
            }.ToString());
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = JObject.Parse("""
                    {"id":"material.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]}
                    """),
                ["templates"] = new JArray(laneMaterialTemplate("key", "playfield.key"), laneMaterialTemplate("hit", "effect.hit-explosion")),
                ["instances"] = new JArray(new[] { (Kind: "key", Slot: "playfield.key"), (Kind: "hit", Slot: "effect.hit-explosion") }
                    .Select(item => new JObject
                    {
                        ["id"] = "instance." + item.Kind,
                        ["template"] = "template." + item.Kind,
                        ["material"] = new JObject { ["slot"] = item.Slot, ["resource"] = "notes/material-" + item.Kind },
                    })),
                ["tracks"] = new JArray(new JObject
                {
                    ["id"] = "hit.rotation",
                    ["type"] = "tween",
                    ["target"] = "hit.sprite",
                    ["property"] = "rotation",
                    ["easing"] = "linear",
                    ["loop"] = false,
                    ["trigger"] = "judgement.hit",
                    ["keyframes"] = new JArray(new JObject { ["id"] = "hit.start", ["time"] = 0, ["value"] = 0 },
                        new JObject { ["id"] = "hit.end", ["time"] = 100, ["value"] = 100 }),
                }),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(new JObject { ["id"] = "key.pressed", ["target"] = "key.sprite", ["property"] = "visible", ["source"] = "input.pressed" }),
            }.ToString());
        }

        private static JObject laneMaterialTemplate(string kind, string slot)
            => new JObject
            {
                ["id"] = "template." + kind,
                ["root"] = new JObject
                {
                    ["id"] = kind + ".owner",
                    ["type"] = "container",
                    ["slot"] = slot,
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["blend"] = "alpha",
                    ["properties"] = new JObject(),
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(new JObject
                    {
                        ["id"] = kind + ".sprite",
                        ["type"] = "sprite",
                        ["resource"] = "texture." + kind,
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["blend"] = kind == "hit" ? "additive" : "alpha",
                        ["properties"] = new JObject { ["visible"] = kind != "key" },
                        ["effects"] = new JArray(),
                        ["children"] = new JArray(),
                    }),
                },
            };

        private static string createLaneMaterialTemplateChart(BmsKeymode keymode)
        {
            string[] channels = keymode == BmsKeymode.Key14K
                ? new[] { "11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26" }
                : new[] { "11", "12", "13", "14", "15", "18", "19", "16" };
            return "#TITLE Material-selected native lane templates\n#BPM 120\n#RANK 2\n#WAV01 note.wav\n"
                   + string.Join("\n", channels.Select(channel => $"#001{channel}:01")) + "\n";
        }
    }
}
