// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, true)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, true)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center, true)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, false)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center, false)]
        public void TestExplicitLiftMaterialsFollowRealModGeometryAndFrozenStagePublication(BmsKeymode keymode, BmsPlayfieldStyle style, bool declared)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            BmsGameplayLayoutSnapshot snapshot = null!;
            GameplaySkinResolvedMaterialSet materials = null!;
            BmsLiftDisplay[] preparedDisplays = [];
            GameplaySkinSpecialisedSceneVisual[] preparedVisuals = [];
            var lift = new BmsModLift();
            lift.LiftUnits.Value = 250;

            AddStep("select a package with explicit optional LIFT parts", () =>
            {
                (_, candidate) = createCandidate(root => writeLiftDisplayPackage(root, declared), typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("selected package revision committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount the real native LIFT mod in the selected keymode", () =>
            {
                var fixture = new ExactBmsProductionFixture(createLaneMaterialTemplateChart(keymode), "lift-consumer.bme", style,
                    mods: new Mod[] { lift }, keymodeOverride: keymode);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("native hit-area projections and exact scene finish loading", () => renderer.BmsReady
                && renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady == true
                && renderer.BmsDrawable.Playfield.Lanes.All(lane => lane.HitObjectArea.HasAppliedProjection)
                && Math.Abs(renderer.BmsDrawable.Playfield.GameplaySkinLiftState.OffsetRatio - 0.25) < 0.0001);
            AddStep("verify stage-owned prepared visuals and the original native geometry", () =>
            {
                BmsPlayfield playfield = renderer.BmsDrawable.Playfield;
                scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                snapshot = playfield.LayoutSnapshot;
                materials = playfield.ResolvedMaterialSet;
                scene.ProcessFrame();
                int stages = snapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Count;
                Assert.That(playfield.LiftDisplays, Has.Count.EqualTo(declared ? stages : 0));
                Assert.That(playfield.GameplaySkinLiftState, Is.EqualTo(new GameplaySkinLiftState(true, 250, 0.25, false)));
                if (!declared)
                {
                    Assert.That(materials.Entries.Any(entry => entry.Slot == GameplaySkinSlotCatalog.LiftFill || entry.Slot == GameplaySkinSlotCatalog.LiftDecoration), Is.False,
                        "Undeclared skins do not acquire optional LIFT fallback nodes.");
                }
                foreach (BmsLiftDisplay display in playfield.LiftDisplays)
                {
                    BmsLane lane = playfield.Lanes.First(candidateLane => candidateLane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(display.Target.GroupId));
                    assertLiftProjection(display, lane, 0.25f);
                    foreach (BmsLiftDisplayPart part in new[] { display.FillPart!, display.DecorationPart! })
                    {
                        Assert.That(part, Is.Not.Null);
                        Assert.That(part.ResolvedMaterialSet, Is.SameAs(materials));
                        Assert.That(part.ResolvedMaterialKey.Target, Is.EqualTo(display.Target));
                        Assert.That(part.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(part.SceneVisualGate.SpecialisedPoolCapacity, Is.EqualTo(1));
                        Assert.That(part.SpecialisedSceneVisual!.IsApplied, Is.True);
                        Assert.That(part.AppliedSceneNodeIds, Is.Not.Empty);
                        Assert.That(part.SpecialisedSceneVisual.RuntimeNodes.All(node => node.PreparedNode.OwningSlotKey!.Equals(part.ResolvedMaterialKey)
                            && node.PreparedNode.ResolvedTarget.StableId == display.Target.GroupId!.Value), Is.True,
                            "One material template fans out into independently owned stage instances.");
                        Assert.That(materials.TryGet(part.ResolvedMaterialKey, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                        Assert.That(entry!.Source.IsSelectedDocumentDeclaration, Is.True);
                    }
                }
                preparedDisplays = [.. playfield.LiftDisplays];
                preparedVisuals = [.. preparedDisplays.SelectMany(display => new[]
                {
                    display.FillPart!.SpecialisedSceneVisual!, display.DecorationPart!.SpecialisedSceneVisual!,
                })];
                Assert.That(scene.RuntimeFaults, Is.Empty);
            });
            AddStep("change only the real LIFT mod's setting", () => lift.LiftUnits.Value = 500);
            AddUntilStep("the original controller applies the new offset", () => renderer.BmsDrawable.Playfield.GameplaySkinLiftState.Units == 500
                && Math.Abs(renderer.BmsDrawable.Playfield.GameplaySkinLiftState.OffsetRatio - 0.5) < 0.0001);
            AddUntilStep("prepared text reads the same immutable native projection", () => !declared
                || renderer.BmsDrawable.Playfield.LiftDisplays.All(display =>
                    ((OsuSpriteText)display.DecorationPart!.SpecialisedSceneVisual!.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "lift.value").ContentDrawable).Text.ToString() == "500"));
            AddStep("the visual update retains exact geometry material and bounded prepared instances", () =>
            {
                BmsPlayfield playfield = renderer.BmsDrawable.Playfield;
                Assert.That(playfield.LayoutSnapshot, Is.SameAs(snapshot));
                Assert.That(playfield.ResolvedMaterialSet, Is.SameAs(materials));
                Assert.That(playfield.LiftDisplays, Is.EqualTo(preparedDisplays));
                Assert.That(playfield.LiftDisplays.SelectMany(display => new[]
                {
                    display.FillPart!.SpecialisedSceneVisual!, display.DecorationPart!.SpecialisedSceneVisual!,
                }), Is.EqualTo(preparedVisuals), "Changing the mod reuses the same single prepared visual for each exact stage/slot.");
                Assert.That(lift.LiftUnits.Value, Is.EqualTo(500), "Presentation must never rewrite the native mod.");
                foreach (BmsLiftDisplay display in playfield.LiftDisplays)
                {
                    BmsLane lane = playfield.Lanes.First(candidateLane => candidateLane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(display.Target.GroupId));
                    assertLiftProjection(display, lane, 0.5f);
                }
                Assert.That(scene.RuntimeFaults, Is.Empty);
            });
            AddStep("disable the real offset without rebuilding the scene", () => lift.LiftUnits.Value = 0);
            AddUntilStep("LIFT visuals hide when the applied offset reaches zero", () => renderer.BmsDrawable.Playfield.GameplaySkinLiftState.Units == 0
                && renderer.BmsDrawable.Playfield.LiftDisplays.All(display => display.Alpha == 0 && display.FillClip.Height == 0));
            AddStep("retire exact native LIFT owners", () => renderer.Expire());
            AddUntilStep("native LIFT owners detach", () => renderer.Parent == null);
        }

        private static void assertLiftProjection(BmsLiftDisplay display, BmsLane lane, float ratio)
        {
            Assert.That(display.AppliedOffsetRatio, Is.EqualTo(ratio).Within(0.0001));
            Assert.That(display.FillClip.Height, Is.EqualTo(ratio).Within(0.0001));
            Assert.That(display.DecorationSceneOwner.Y, Is.EqualTo(1 - ratio).Within(0.0001));
            Assert.That(display.FillSceneOwner.DrawHeight, Is.EqualTo(display.DrawHeight).Within(0.001),
                "The full source canvas is cropped; its vertical metric is never compressed into the LIFT span.");
            Assert.That(display.FillClip.ScreenSpaceDrawQuad.Height, Is.EqualTo(lane.ScreenSpaceDrawQuad.Height * ratio).Within(1));
            Assert.That(display.FillClip.ScreenSpaceDrawQuad.TopLeft.Y, Is.EqualTo(lane.HitTarget.ScreenSpaceDrawQuad.BottomLeft.Y).Within(1));
            Assert.That(display.FillClip.ScreenSpaceDrawQuad.BottomLeft.Y, Is.EqualTo(lane.ScreenSpaceDrawQuad.BottomLeft.Y).Within(1));
            Assert.That(display.DecorationSceneOwner.ScreenSpaceDrawQuad.TopLeft.Y, Is.EqualTo(display.FillClip.ScreenSpaceDrawQuad.TopLeft.Y).Within(1));
        }

        private static void writeLiftDisplayPackage(string root, bool declared)
        {
            var ini = new StringBuilder("[General]\nName: Native LIFT visual contract\nAuthor: OMS tests\nVersion: 2.7\n"
                                       + "[Bms]\nKeymode: 7K\n[Bms]\nKeymode: 14K\n[GameplaySkin.Bms:1]\n");
            if (declared)
            {
                foreach (BmsKeymode keymode in new[] { BmsKeymode.Key7K, BmsKeymode.Key14K })
                {
                    GameplaySkinLaneTopologySnapshot topology = BmsGameplaySkinLaneTopologyFactory.Create(keymode, BmsPlayfieldStyle.Center).Topology;
                    foreach (GameplaySkinLaneTopologyGroup group in topology.GroupsInLogicalOrder)
                    {
                        appendDocumentTarget(ini, GameplaySkinResolvedMaterialTarget.ForStage(group), getKeymodeId(keymode), keymode == BmsKeymode.Key14K ? "dual" : "single");
                        ini.AppendLine("playfield.lift.fill: resource Provide \"lift\"");
                        ini.AppendLine("playfield.lift.decoration: resource Provide \"lift\"");
                    }
                }
            }
            File.WriteAllText(Path.Combine(root, "skin.ini"), ini.ToString());
            using (var image = new Image<Rgba32>(32, 8, new Rgba32(90, 140, 220, 255)))
                image.SaveAsPng(Path.Combine(root, "lift.png"));
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["options"] = testPresentationOptions(),
                ["resources"] = new JArray { new JObject { ["id"] = "texture.lift", ["type"] = "texture", ["path"] = "lift.png" } },
            }.ToString());
            var templates = new JArray();
            var instances = new JArray();
            foreach (string kind in new[] { "fill", "decoration" })
            {
                var children = new JArray
                {
                    new JObject
                    {
                        ["id"] = "lift." + kind + ".sprite", ["type"] = "sprite", ["resource"] = "texture.lift",
                        ["target"] = new JObject { ["kind"] = "global" }, ["blend"] = "alpha", ["properties"] = new JObject(),
                        ["effects"] = new JArray(), ["children"] = new JArray(),
                    },
                };
                if (kind == "decoration")
                    children.Add(JObject.Parse("""
                        {"id":"lift.value","type":"text","target":{"kind":"global"},"blend":"alpha",
                         "properties":{"text":"","font-size":16},"effects":[],"children":[]}
                        """));
                templates.Add(new JObject
                {
                    ["id"] = "template.lift." + kind,
                    ["root"] = new JObject
                    {
                        ["id"] = "lift." + kind,
                        ["type"] = "container",
                        ["slot"] = "playfield.lift." + kind,
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["blend"] = "alpha",
                        ["properties"] = new JObject(),
                        ["effects"] = new JArray(),
                        ["children"] = children,
                    },
                });
                instances.Add(new JObject
                {
                    ["id"] = "instance.lift." + kind,
                    ["template"] = "template.lift." + kind,
                    ["material"] = new JObject { ["slot"] = "playfield.lift." + kind, ["resource"] = "lift" },
                });
            }
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = JObject.Parse("""
                    {"id":"lift.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]}
                    """),
                ["templates"] = templates,
                ["instances"] = instances,
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray { new JObject { ["id"] = "lift.read-value", ["target"] = "lift.value", ["property"] = "text", ["source"] = "lift.value" } },
            }.ToString());
        }
    }
}
