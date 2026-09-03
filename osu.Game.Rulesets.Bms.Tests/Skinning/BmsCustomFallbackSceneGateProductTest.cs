// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestNoPublicDeclarationRetainsActualLegacyCustomFallbackInstances()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            var compatibilitySkin = new CustomGameplayFallbackSkin();

            AddStep("create and select no-public 14K package", () =>
            {
                (_, candidate) = createCandidate(
                    writeNoPublicCustomFallbackPackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact no-public revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real 14K custom fallback tree", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, compatibilitySkin, useFourteenKeyBeatmap: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for no-public exact production tree", () => renderer.BmsReady);
            AddStep("all-fallback publication preserves legacy components", () =>
            {
                BmsHudLayoutSnapshotCarrier carrier = renderer.BmsDrawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single();
                BmsBgaPanel bgaPanel = renderer.BmsDrawable.ChildrenOfType<BmsBgaPanel>().Single();
                BmsLaneCover[] covers = renderer.BmsDrawable.Playfield.LaneCovers.ToArray();
                BmsHitTarget[] targets = renderer.BmsDrawable.Playfield.Lanes.Select(lane => lane.HitTarget).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(carrier.GaugeBar, Is.SameAs(compatibilitySkin.Gauge));
                    Assert.That(carrier.ComboCounter, Is.SameAs(compatibilitySkin.Combo));
                    Assert.That(carrier.GaugeProgrammaticVisualOwner!.Alpha, Is.GreaterThan(0));
                    Assert.That(carrier.ComboProgrammaticVisualOwner!.Alpha, Is.GreaterThan(0));
                    Assert.That(bgaPanel.Drawable, Is.SameAs(compatibilitySkin.Bga));
                    Assert.That(compatibilitySkin.Bga.SourceSetCount, Is.EqualTo(1));
                    Assert.That(bgaPanel.TryGetContentState(0, out GameplaySkinBgaContentState state, out long revision), Is.False);
                    Assert.That(state, Is.EqualTo(GameplaySkinBgaContentState.Empty));
                    Assert.That(revision, Is.Zero);
                    Assert.That(covers.SelectMany(cover => cover.ChildrenOfType<CustomLaneCoverDisplay>()).Count(), Is.EqualTo(2));
                    Assert.That(covers.All(cover => cover.GameplaySkinCustomFallbackGateOwner.Alpha > 0), Is.True);
                    Assert.That(targets.SelectMany(target => target.ChildrenOfType<CustomHitTargetDisplay>()).Count(),
                        Is.EqualTo(renderer.BmsDrawable.LayoutSnapshot.LanesInLogicalOrder.Count));
                    Assert.That(targets.All(target => target.GameplaySkinCustomFallbackGateOwner.Alpha > 0), Is.True);
                });
            });
            AddStep("detach no-public custom fallback tree", () => renderer.Expire());
            AddUntilStep("wait for no-public custom detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestSelectedPublicSlotsGateActualLegacyCustomFallbackInstances()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            DefaultBmsBgaPanelDisplay defaultBgaDisplay = null!;
            BmsBgaPanel bgaPanel = null!;
            BmsGameplayLayoutSnapshot layout = null!;
            var compatibilitySkin = new CustomGameplayFallbackSkin();

            AddStep("create and select custom-gate 14K package", () =>
            {
                (_, candidate) = createCandidate(
                    writeCustomFallbackGatePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact custom-gate revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real 14K renderer over legacy custom fallback", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(
                    manager,
                    compatibilitySkin,
                    useFourteenKeyBeatmap: true,
                    includeBgaTimeline: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact custom fallback publication", () => renderer.BmsReady);
            AddStep("capture mounted scene host", () =>
                sceneHost = renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for selected custom gates", () => sceneHost.PendingCreationCount == 0);
            AddStep("assert actual custom instances cannot overlap author slots", () =>
            {
                layout = renderer.BmsLayoutProbe.Publication!.GetAdapter<BmsGameplayLayoutSnapshot>();
                GameplaySkinLaneTopologyGroup[] groups = layout.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();
                BmsHudLayoutSnapshotCarrier carrier = renderer.BmsDrawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single();
                bgaPanel = renderer.BmsDrawable.ChildrenOfType<BmsBgaPanel>().Single();
                GameplaySkinResolvedMaterialTarget deck1 = GameplaySkinResolvedMaterialTarget.ForStage(groups[0]);
                var laneCoverFillKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneCoverFill, deck1);
                var bgaViewportKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.BgaViewport, GameplaySkinResolvedMaterialTarget.Global);
                var bgaFrameKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.BgaFrame, GameplaySkinResolvedMaterialTarget.Global);

                Assert.That(groups, Has.Length.EqualTo(2));
                Assert.That(carrier.GaugeBar, Is.TypeOf<BmsGaugeBar>(),
                    "A partial stage author declaration requires the typed stage-partitioned gauge host.");
                Assert.That(carrier.ComboCounter, Is.TypeOf<BmsComboCounter>(),
                    "A partial stage author declaration requires the typed stage-partitioned combo host.");
                Assert.That(sceneHost.TryGetVisualGate(laneCoverFillKey, out GameplaySkinSceneHostedSlot? laneCoverFillGate), Is.True);
                Assert.That(laneCoverFillGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(laneCoverFillGate.IsReplacementReady, Is.True,
                    "The selected fill must mount into the native BMS cover host even when the direct fallback is custom.");
                Assert.That(bgaPanel.Drawable, Is.TypeOf<DefaultBmsBgaPanelDisplay>(),
                    "An opaque custom BGA without the closed state/surface seam must fail closed to the engine display.");
                defaultBgaDisplay = (DefaultBmsBgaPanelDisplay)bgaPanel.Drawable!;
                Assert.That(compatibilitySkin.Bga.SourceSetCount, Is.Zero,
                    "The rejected custom BGA must never receive or own the P1-L timeline.");
                Assert.That(bgaPanel.Drawable.ChildrenOfType<BmsBgaPlayer>().Count(), Is.EqualTo(layout.BgaViewports.Count),
                    "The protected engine display remains the sole P1-L timeline/content owner.");
                Assert.That(sceneHost.TryGetVisualGate(bgaViewportKey, out GameplaySkinSceneHostedSlot? bgaViewportGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(bgaFrameKey, out GameplaySkinSceneHostedSlot? bgaFrameGate), Is.True);
                Assert.That(bgaViewportGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(bgaFrameGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(bgaViewportGate.IsReplacementReady, Is.True);
                Assert.That(bgaFrameGate.IsReplacementReady, Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(layout.BgaViewports, Has.Count.EqualTo(4));
                    Assert.That(defaultBgaDisplay.GameplaySkinViewportSceneVisuals, Has.Count.EqualTo(4),
                        "The one global BgaViewport material factory must mount once in every exact 14K viewport.");
                    Assert.That(defaultBgaDisplay.GameplaySkinFrameSceneVisuals, Has.Count.EqualTo(4),
                        "The one global BgaFrame material factory must mount once in every exact 14K viewport.");
                    Assert.That(defaultBgaDisplay.GameplaySkinViewportSceneVisualsByViewport, Has.Count.EqualTo(4));
                    Assert.That(defaultBgaDisplay.GameplaySkinFrameSceneVisualsByViewport, Has.Count.EqualTo(4));
                    Assert.That(defaultBgaDisplay.GameplaySkinViewportSceneVisualsByViewport.All(visual => visual != null), Is.True);
                    Assert.That(defaultBgaDisplay.GameplaySkinFrameSceneVisualsByViewport.All(visual => visual != null), Is.True);
                    Assert.That(defaultBgaDisplay.NativeFrameVisuals, Has.Count.EqualTo(4));
                    Assert.That(defaultBgaDisplay.NativeFrameVisuals.All(frame => frame.Alpha == 0), Is.True,
                        "Each ready author frame hides only its corresponding native frame instance.");
                    Assert.That(Enumerable.Range(0, layout.BgaViewports.Count).All(index =>
                    {
                        bool found = bgaPanel.TryGetContentState(index, out GameplaySkinBgaContentState state, out _);
                        return found && state != GameplaySkinBgaContentState.Empty;
                    }), Is.True, "Every solved viewport retains its engine-owned P1-L content-state source.");
                });

                for (int index = 0; index < layout.BgaViewports.Count; index++)
                {
                    GameplaySkinSpecialisedSceneVisual viewportVisual = defaultBgaDisplay.GameplaySkinViewportSceneVisualsByViewport[index]!;
                    GameplaySkinSpecialisedSceneVisual frameVisual = defaultBgaDisplay.GameplaySkinFrameSceneVisualsByViewport[index]!;
                    GameplaySkinPreparedSceneResource resource = sceneHost.PreparedScene.Resources.Single(candidateResource =>
                        candidateResource.Id == $"texture.bga-{index}");

                    Assert.Multiple(() =>
                    {
                        Assert.That(viewportVisual.RuntimeNodes.Single().PreparedNode.Source.Id, Is.EqualTo($"node.bga-viewport-{index}"));
                        Assert.That(viewportVisual.RuntimeNodes.Single().PreparedNode.ResolvedTarget.Kind, Is.EqualTo(GameplaySkinSceneTargetKind.Bga));
                        Assert.That(viewportVisual.RuntimeNodes.Single().PreparedNode.ResolvedTarget.Index, Is.EqualTo(index));
                        Assert.That(((Sprite)viewportVisual.RuntimeNodes.Single().ContentDrawable).Texture, Is.SameAs(resource.Texture));
                        Assert.That(frameVisual.RuntimeNodes.Single().PreparedNode.Source.Id, Is.EqualTo($"node.bga-frame-{index}"));
                        Assert.That(frameVisual.RuntimeNodes.Single().PreparedNode.ResolvedTarget.Kind, Is.EqualTo(GameplaySkinSceneTargetKind.Bga));
                        Assert.That(frameVisual.RuntimeNodes.Single().PreparedNode.ResolvedTarget.Index, Is.EqualTo(index));
                        Assert.That(((Sprite)frameVisual.RuntimeNodes.Single().ContentDrawable).Texture, Is.SameAs(resource.Texture));
                    });
                }

                BmsLaneCover[] coverHosts = renderer.BmsDrawable.Playfield.LaneCovers.ToArray();
                CustomLaneCoverDisplay[] covers = coverHosts
                    .SelectMany(cover => cover.ChildrenOfType<CustomLaneCoverDisplay>())
                    .ToArray();
                BmsHitTarget[] targetHosts = renderer.BmsDrawable.Playfield.Lanes.Select(lane => lane.HitTarget).ToArray();
                CustomHitTargetDisplay[] targets = targetHosts
                    .SelectMany(target => target.ChildrenOfType<CustomHitTargetDisplay>())
                    .ToArray();

                foreach (BmsLaneCover cover in coverHosts)
                {
                    cover.CoverOpacity.Value = 1000;
                    cover.IsFocused.Value = true;
                }

                foreach (BmsHitTarget target in targetHosts)
                {
                    target.IsPressed.Value = true;
                    target.IsFocused.Value = true;
                }

                Assert.Multiple(() =>
                {
                    var gauge = (BmsGaugeBar)carrier.GaugeBar!;
                    var combo = (BmsComboCounter)carrier.ComboCounter!;
                    Assert.That(carrier.GaugeProgrammaticVisualOwner, Is.SameAs(gauge));
                    Assert.That(carrier.ComboProgrammaticVisualOwner, Is.SameAs(combo));
                    Assert.That(gauge.GameplaySkinStageFallbackVisuals[0].Alpha, Is.Zero);
                    Assert.That(gauge.GameplaySkinStageFallbackVisuals[1].Alpha, Is.GreaterThan(0),
                        "A deck-1 Gauge Provide must not aggregate-hide the inherited deck-2 gauge.");
                    Assert.That(combo.GameplaySkinStageFallbackVisuals[0].Alpha, Is.Zero);
                    Assert.That(combo.GameplaySkinStageFallbackVisuals[1].Alpha, Is.GreaterThan(0),
                        "A deck-1 Combo Suppress must not aggregate-hide the inherited deck-2 combo.");
                    Assert.That(covers, Is.Empty,
                        "Opaque custom lane covers cannot participate in an exact independently partitioned publication.");
                    Assert.That(targets, Has.Length.EqualTo(groups[1].LanesInLogicalOrder.Count),
                        "Only deck 1 fails closed; the all-fallback sibling deck retains its custom lane targets.");
                    Assert.That(targets.All(target => target.LaneId != null
                                                      && layout.GetLane(target.LaneId).NeutralLane.TopologyEntry.Identity.Group.Id.Equals(groups[1].Identity.Id)),
                        Is.True);
                    Assert.That(coverHosts.All(cover => cover.GameplaySkinCustomFallbackGateOwner.Alpha > 0), Is.True,
                        "The typed part children, not an aggregate parent gate, own exact fallback visibility.");
                    Assert.That(targetHosts.All(target => target.GameplaySkinCustomFallbackGateOwner.Alpha > 0), Is.True,
                        "A lane/stage part must never aggregate-hide the complete target owner.");
                    Assert.That(coverHosts.All(cover => cover.GameplaySkinStageFallbackVisuals.Count == 2), Is.True);
                    Assert.That(coverHosts.All(cover => cover.GameplaySkinStageFallbackVisuals[0].Target!.Equals(deck1)
                                                   && cover.GameplaySkinStageFallbackVisuals[0].FillVisual.Alpha == 0
                                                   && cover.GameplaySkinStageFallbackVisuals[0].DecorationVisual.Alpha == 0), Is.True,
                        "Deck-1 Provide and Suppress independently hide the exact typed Fill and Decoration parts.");
                    Assert.That(coverHosts.All(cover => cover.GameplaySkinStageFallbackVisuals[1].Target!.Equals(
                                                       GameplaySkinResolvedMaterialTarget.ForStage(groups[1]))
                                                   && cover.GameplaySkinStageFallbackVisuals[1].FillVisual.Alpha > 0
                                                   && cover.GameplaySkinStageFallbackVisuals[1].DecorationVisual.Alpha > 0), Is.True,
                        "Deck-2 inherited Fill and Decoration remain visible; no deck-1 aggregate gate may swallow them.");
                });

                BmsLane scratch1 = renderer.BmsDrawable.Playfield.Lanes.Single(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Id.Value == "bms.lane.scratch-1");
                BmsLane scratch2 = renderer.BmsDrawable.Playfield.Lanes.Single(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Id.Value == "bms.lane.scratch-2");
                BmsLane deck1Regular = renderer.BmsDrawable.Playfield.Lanes.First(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(groups[0].Identity.Id)
                    && !lane.LayoutSnapshotLane.IsScratch);
                BmsLane deck2Regular = renderer.BmsDrawable.Playfield.Lanes.First(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(groups[1].Identity.Id)
                    && !lane.LayoutSnapshotLane.IsScratch);

                Assert.Multiple(() =>
                {
                    Assert.That(scratch1.GameplaySkinLaneSurfaceFallbackVisual.Alpha, Is.Zero,
                        "The lane-owned SkinnableDrawable already gates the actual custom surface generically.");
                    Assert.That(scratch1.GameplaySkinLaneDividerFallbackVisual.Alpha, Is.Zero,
                        "The lane-owned SkinnableDrawable already gates the actual custom divider generically.");
                    Assert.That(scratch2.GameplaySkinLaneSurfaceFallbackVisual.Alpha, Is.GreaterThan(0));
                    Assert.That(scratch2.GameplaySkinLaneDividerFallbackVisual.Alpha, Is.GreaterThan(0));
                    Assert.That(scratch1.GameplaySkinLaneSurfaceFallbackVisual.ChildrenOfType<CustomLanePartDisplay>().Single().Element,
                        Is.EqualTo(BmsLaneSkinElements.Background));
                    Assert.That(scratch1.GameplaySkinLaneDividerFallbackVisual.ChildrenOfType<CustomLanePartDisplay>().Single().Element,
                        Is.EqualTo(BmsLaneSkinElements.Divider));
                    Assert.That(scratch1.HitTarget.GameplaySkinHitTargetFallbackVisual!.Alpha, Is.Zero,
                        "Only the exact scratch-1 HitTarget Provide hides its typed fallback part.");
                    Assert.That(scratch1.HitTarget.GameplaySkinKeyFlashFallbackVisual!.Alpha, Is.Zero,
                        "Only the exact scratch-1 KeyFlash Suppress hides its typed fallback part.");
                    Assert.That(scratch1.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero,
                        "Deck-1 JudgementLine Provide hides the stage part independently.");
                    Assert.That(deck1Regular.HitTarget.GameplaySkinHitTargetFallbackVisual!.Alpha, Is.GreaterThan(0),
                        "An inherited lane HitTarget remains visible even when its stage JudgementLine is authored.");
                    Assert.That(deck1Regular.HitTarget.GameplaySkinKeyFlashFallbackVisual!.Alpha, Is.GreaterThan(0),
                        "An inherited KeyFlash remains visible; scratch-1 suppression cannot aggregate-hide it.");
                    Assert.That(deck1Regular.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero);
                    Assert.That(scratch2.HitTarget.ChildrenOfType<CustomHitTargetDisplay>().Single().Alpha, Is.EqualTo(1));
                    Assert.That(scratch2.HitTarget.ChildrenOfType<CustomHitTargetDisplay>().Single().Scale, Is.EqualTo(new Vector2(1.15f)));
                    Assert.That(deck2Regular.HitTarget.ChildrenOfType<CustomHitTargetDisplay>().Single().Alpha, Is.EqualTo(1));
                    Assert.That(deck2Regular.HitTarget.ChildrenOfType<CustomHitTargetDisplay>().Single().Scale, Is.EqualTo(new Vector2(1.15f)),
                        "Deck-1 authoring cannot replace, aggregate-hide or freeze deck-2 custom target state.");
                });
            });
            AddUntilStep("all four BGA indices consume their own read-only content state", () =>
                sceneHost.StateMachineStates.Where(pair => pair.Key.Contains("machine.bga-")).Count() == 8
                && sceneHost.StateMachineStates.Where(pair => pair.Key.Contains("machine.bga-"))
                            .All(pair => pair.Value.EndsWith("-content", System.StringComparison.Ordinal)));
            AddStep("retire one exact frame scene instance", () =>
                defaultBgaDisplay.GameplaySkinFrameSceneVisualsByViewport[2]!.OnFree());
            AddUntilStep("only the unready viewport restores its native frame", () =>
                defaultBgaDisplay.NativeFrameVisuals[2].Alpha > 0
                && defaultBgaDisplay.NativeFrameVisuals.Where((_, index) => index != 2).All(frame => frame.Alpha == 0));
            AddStep("instance-local frame fallback never touches P1-L content", () => Assert.Multiple(() =>
            {
                Assert.That(bgaPanel.Drawable!.ChildrenOfType<BmsBgaPlayer>().Count(), Is.EqualTo(4));
                Assert.That(Enumerable.Range(0, 4).All(index =>
                    bgaPanel.TryGetContentState(index, out GameplaySkinBgaContentState state, out _)
                    && state != GameplaySkinBgaContentState.Empty), Is.True);
                Assert.That(defaultBgaDisplay.GameplaySkinFrameSceneVisualsByViewport.Where((_, index) => index != 2)
                                            .All(visual => visual?.Alpha > 0), Is.True,
                    "One unready frame clone must not fail or hide another exact viewport clone.");
            }));
            AddStep("detach custom fallback renderer", () => renderer.Expire());
            AddUntilStep("wait for custom fallback detach", () => renderer.Parent == null);
        }

        private static void writeNoPublicCustomFallbackPackage(string root)
        {
            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\n" +
                "Name: C5 BMS no-public custom fallback\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 14K\n");
        }

        private static void writeCustomFallbackGatePackage(string root)
        {
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllBytes(Path.Combine(notes, "author.png"), createPng(new Rgba32(230, 110, 40, 255)));

            Rgba32[] bgaColours =
            {
                new Rgba32(225, 55, 65, 255),
                new Rgba32(55, 205, 95, 255),
                new Rgba32(55, 105, 225, 255),
                new Rgba32(215, 175, 45, 255),
            };

            for (int index = 0; index < bgaColours.Length; index++)
                File.WriteAllBytes(Path.Combine(notes, $"bga-{index}.png"), createPng(bgaColours[index]));

            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\n" +
                "Name: C5 BMS custom fallback gate\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Global ruleset=bms keymode=14k stage-mode=dual\n" +
                "bga.viewport: resource Provide \"notes/author\"\n" +
                "bga.frame: resource Provide \"notes/author\"\n" +
                "Target: Stage ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-1 group-logical=0 group-visual=0\n" +
                "playfield.judgement-line: resource Provide \"notes/author\"\n" +
                "playfield.lane-cover.fill: resource Provide \"notes/author\"\n" +
                "playfield.lane-cover.decoration: resource Suppress\n" +
                "hud.combo: resource Suppress\n" +
                "hud.gauge: resource Provide \"notes/author\"\n" +
                "Target: Lane ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-1 lane=bms.lane.scratch-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "playfield.lane-surface: resource Provide \"notes/author\"\n" +
                "playfield.lane-divider: resource Provide \"notes/author\"\n" +
                "playfield.hit-target: resource Provide \"notes/author\"\n" +
                "effect.key-flash: resource Suppress\n");

            var resources = new JArray();
            var sceneChildren = new JArray();
            var stateMachines = new JArray();

            for (int index = 0; index < bgaColours.Length; index++)
            {
                string resourceId = $"texture.bga-{index}";
                resources.Add(new JObject
                {
                    ["id"] = resourceId,
                    ["type"] = "texture",
                    ["path"] = $"notes/bga-{index}.png",
                });

                addBgaNode("viewport", GameplaySkinSlotCatalog.BgaViewport.Id, 0.35);
                addBgaNode("frame", GameplaySkinSlotCatalog.BgaFrame.Id, 0.45);

                void addBgaNode(string part, string slot, double idleOpacity)
                {
                    string nodeId = $"node.bga-{part}-{index}";
                    string machineId = $"machine.bga-{part}-{index}";
                    string idleStateId = $"state.bga-{part}-{index}-idle";
                    string contentStateId = $"state.bga-{part}-{index}-content";
                    sceneChildren.Add(new JObject
                    {
                        ["id"] = nodeId,
                        ["type"] = "sprite",
                        ["target"] = new JObject
                        {
                            ["kind"] = "bga",
                            ["index"] = index,
                        },
                        ["slot"] = slot,
                        ["resource"] = resourceId,
                        ["blend"] = "alpha",
                        ["properties"] = new JObject
                        {
                            ["opacity"] = idleOpacity,
                            ["visible"] = true,
                        },
                        ["effects"] = new JArray(),
                        ["children"] = new JArray(),
                    });
                    stateMachines.Add(new JObject
                    {
                        ["id"] = machineId,
                        ["initial"] = idleStateId,
                        ["states"] = new JArray
                        {
                            state(idleStateId, idleOpacity),
                            state(contentStateId, 0.9),
                        },
                        ["transitions"] = new JArray
                        {
                            new JObject
                            {
                                ["id"] = $"transition.bga-{part}-{index}",
                                ["from"] = idleStateId,
                                ["to"] = contentStateId,
                                ["event"] = "bga.state",
                            },
                        },
                    });

                    JObject state(string stateId, double opacity)
                        => new JObject
                        {
                            ["id"] = stateId,
                            ["set"] = new JArray
                            {
                                new JObject
                                {
                                    ["id"] = $"assignment.bga-{part}-{index}-{stateId}",
                                    ["target"] = nodeId,
                                    ["property"] = "opacity",
                                    ["value"] = opacity,
                                },
                            },
                        };
                }
            }

            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                new JObject
                {
                    ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                    ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                    ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                    ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                    ["resources"] = resources,
                }.ToString(Formatting.Indented));
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                new JObject
                {
                    ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                    ["root"] = new JObject
                    {
                        ["id"] = "node.root",
                        ["type"] = "container",
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["blend"] = "inherit",
                        ["properties"] = new JObject(),
                        ["effects"] = new JArray(),
                        ["children"] = sceneChildren,
                    },
                    ["tracks"] = new JArray(),
                    ["stateMachines"] = stateMachines,
                    ["bindings"] = new JArray(),
                    ["templates"] = new JArray(),
                    ["instances"] = new JArray(),
                }.ToString(Formatting.Indented));
        }

        private sealed class CustomGameplayFallbackSkin : Skin
        {
            private readonly Dictionary<string, Drawable> laneComponents = new Dictionary<string, Drawable>();
            private readonly Dictionary<BmsLaneCoverPosition, CustomLaneCoverDisplay> covers = new Dictionary<BmsLaneCoverPosition, CustomLaneCoverDisplay>();

            public CustomGaugeDisplay Gauge { get; } = new CustomGaugeDisplay();

            public CustomComboCounter Combo { get; } = new CustomComboCounter();

            public CustomBgaDisplay Bga { get; } = new CustomBgaDisplay();

            public CustomGameplayFallbackSkin()
                : base(new SkinInfo(name: nameof(CustomGameplayFallbackSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                switch (lookup)
                {
                    case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar }:
                        return Gauge;

                    case BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter }:
                        return Combo;

                    case BmsSkinComponentLookup { Component: BmsSkinComponents.BgaPanel }:
                        return Bga;

                    case BmsLaneCoverSkinLookup coverLookup:
                        if (!covers.TryGetValue(coverLookup.Position, out CustomLaneCoverDisplay? cover))
                            covers.Add(coverLookup.Position, cover = new CustomLaneCoverDisplay());

                        return cover;

                    case BmsLaneSkinLookup { Element: BmsLaneSkinElements.HitTarget } laneLookup:
                        return getLaneComponent(laneLookup, () => new CustomHitTargetDisplay(laneLookup.LaneId));

                    case BmsLaneSkinLookup { Element: BmsLaneSkinElements.Background or BmsLaneSkinElements.Divider } laneLookup:
                        return getLaneComponent(laneLookup, () => new CustomLanePartDisplay(laneLookup.Element));

                    default:
                        return null;
                }
            }

            private Drawable getLaneComponent(BmsLaneSkinLookup lookup, System.Func<Drawable> create)
            {
                string key = $"{lookup.Element}:{lookup.LaneId?.Value ?? lookup.LaneIndex.ToString()}";

                if (!laneComponents.TryGetValue(key, out Drawable? component))
                    laneComponents.Add(key, component = create());

                return component;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed partial class CustomBgaDisplay : CompositeDrawable, IBmsBgaPanelDisplay, IBmsBgaPanelLayoutDisplay
        {
            public int SourceSetCount { get; private set; }

            public void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
                => SourceSetCount++;

            public void SetLayout(BmsBgaPlacement placement)
            {
            }

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
            {
            }

            public void NotifyMiss()
            {
            }
        }

        private sealed partial class CustomGaugeDisplay : CompositeDrawable
        {
            public CustomGaugeDisplay()
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }

            public void RevealAfterGate()
            {
                Alpha = 1;
                Scale = new Vector2(1.2f);
            }
        }

        private sealed partial class CustomComboCounter : ComboCounter
        {
            public void RevealAfterGate()
            {
                Alpha = 1;
                Scale = new Vector2(1.1f);
            }
        }

        private sealed partial class CustomLaneCoverDisplay : CompositeDrawable, IBmsLaneCoverDisplay
        {
            public CustomLaneCoverDisplay()
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }

            public void SetFocused(bool isFocused)
            {
                Alpha = 1;
                Scale = new Vector2(1.25f);
            }
        }

        private sealed partial class CustomHitTargetDisplay : CompositeDrawable, IBmsHitTargetDisplay
        {
            public GameplaySkinLaneId? LaneId { get; }

            public CustomHitTargetDisplay(GameplaySkinLaneId? laneId)
            {
                LaneId = laneId;
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }

            public void SetPressed(bool isPressed)
            {
                Alpha = 1;
                Scale = new Vector2(1.15f);
            }

            public void SetFocused(bool isFocused)
            {
                Alpha = 1;
                Scale = new Vector2(1.15f);
            }
        }

        private sealed partial class CustomLanePartDisplay : CompositeDrawable
        {
            public BmsLaneSkinElements Element { get; }

            public CustomLanePartDisplay(BmsLaneSkinElements element)
            {
                Element = element;
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box { RelativeSizeAxes = Axes.Both };
            }
        }
    }
}
