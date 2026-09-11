// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestFourteenKeyPartialStageAuthoringDoesNotHideOtherDeckFallbacks()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create and select deck-1-only 14K package", () =>
            {
                (_, candidate) = createCandidate(
                    writeFourteenKeyPartialStagePackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact deck-1-only revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real 14K dual-deck renderer", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFourteenKeyBeatmap: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact 14K publication", () => renderer.BmsReady);
            AddStep("capture mounted partial-stage scene host", () =>
                sceneHost = renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for bounded semantic replacements", () => sceneHost.PendingCreationCount == 0);
            AddStep("enable both real lane-cover geometries", () =>
            {
                foreach (BmsLaneCover cover in renderer.BmsDrawable.Playfield.LaneCovers)
                    cover.CoverPercent.Value = 200;
            });
            AddStep("assert author and canonical parts retain independent deck owners", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                GameplaySkinResolvedMaterialSet materialSet = publication.MaterialSet;
                GameplaySkinLaneTopologyGroup[] groups = layout.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();

                Assert.That(groups.Select(group => group.Identity.Id.Value),
                    Is.EqualTo(new[] { "bms.group.deck-1", "bms.group.deck-2" }));

                GameplaySkinResolvedMaterialTarget deck1 = GameplaySkinResolvedMaterialTarget.ForStage(groups[0]);
                GameplaySkinResolvedMaterialTarget deck2 = GameplaySkinResolvedMaterialTarget.ForStage(groups[1]);
                GameplaySkinLaneTopologyEntry scratch1 = groups[0].LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.Scratch);
                GameplaySkinLaneTopologyEntry scratch2 = groups[1].LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.Scratch);
                GameplaySkinResolvedMaterialTarget scratch1Target = GameplaySkinResolvedMaterialTarget.ForLane(groups[0], scratch1);
                GameplaySkinResolvedMaterialTarget scratch2Target = GameplaySkinResolvedMaterialTarget.ForLane(groups[1], scratch2);
                (GameplaySkinSlotDescriptor Slot, GameplaySkinResolvedMaterialState State, GameplaySkinSceneHostRoute Route)[] deck1Contract =
                {
                    (GameplaySkinSlotCatalog.JudgementLine, GameplaySkinResolvedMaterialState.Provide, GameplaySkinSceneHostRoute.Semantic),
                    (GameplaySkinSlotCatalog.LaneCoverFill, GameplaySkinResolvedMaterialState.Provide, GameplaySkinSceneHostRoute.Specialised),
                    (GameplaySkinSlotCatalog.JudgementDisplay, GameplaySkinResolvedMaterialState.Suppress, GameplaySkinSceneHostRoute.Suppressed),
                    (GameplaySkinSlotCatalog.ComboDisplay, GameplaySkinResolvedMaterialState.Suppress, GameplaySkinSceneHostRoute.Suppressed),
                    (GameplaySkinSlotCatalog.GaugeVisual, GameplaySkinResolvedMaterialState.Suppress, GameplaySkinSceneHostRoute.Suppressed),
                    (GameplaySkinSlotCatalog.PlayfieldBackdrop, GameplaySkinResolvedMaterialState.Provide, GameplaySkinSceneHostRoute.Semantic),
                    (GameplaySkinSlotCatalog.PlayfieldBaseplate, GameplaySkinResolvedMaterialState.Provide, GameplaySkinSceneHostRoute.Semantic),
                    (GameplaySkinSlotCatalog.LaneCoverDecoration, GameplaySkinResolvedMaterialState.Suppress, GameplaySkinSceneHostRoute.Suppressed),
                };

                foreach ((GameplaySkinSlotDescriptor slot, GameplaySkinResolvedMaterialState state, GameplaySkinSceneHostRoute route) in deck1Contract)
                {
                    var deck1Key = new GameplaySkinResolvedMaterialKey(slot, deck1);
                    var deck2Key = new GameplaySkinResolvedMaterialKey(slot, deck2);
                    Assert.That(materialSet.TryGet(deck1Key, out GameplaySkinResolvedMaterialEntry? deck1Entry), Is.True);
                    Assert.That(materialSet.TryGet(deck2Key, out GameplaySkinResolvedMaterialEntry? deck2Entry), Is.True);
                    Assert.That(sceneHost.TryGetVisualGate(deck1Key, out GameplaySkinSceneHostedSlot? deck1Gate), Is.True);
                    Assert.That(sceneHost.TryGetVisualGate(deck2Key, out GameplaySkinSceneHostedSlot? deck2Gate), Is.True);

                    Assert.Multiple(() =>
                    {
                        Assert.That(deck1Entry!.State, Is.EqualTo(state), $"Unexpected deck-1 state for {slot.Id}.");
                        Assert.That(deck1Entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                        Assert.That(deck1Gate!.Route, Is.EqualTo(route), $"Unexpected deck-1 route for {slot.Id}.");
                        Assert.That(deck1Gate.SuppressesProgrammaticVisual, Is.True);
                        bool canonicalSuppress = ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverDecoration);
                        Assert.That(deck2Entry!.State, Is.EqualTo(canonicalSuppress ? GameplaySkinResolvedMaterialState.Suppress : GameplaySkinResolvedMaterialState.Provide));
                        Assert.That(deck2Entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.CanonicalPackage));
                        Assert.That(deck2Gate!.Route, Is.EqualTo(canonicalSuppress ? GameplaySkinSceneHostRoute.Suppressed
                            : ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverFill) ? GameplaySkinSceneHostRoute.Specialised
                            : GameplaySkinSceneHostRoute.Semantic));
                        Assert.That(deck2Gate.IsReplacementReady, Is.True);
                        Assert.That(deck2Gate.AllowsProgrammaticVisual, Is.False);
                        Assert.That(deck2Gate.SuppressesProgrammaticVisual, Is.True);
                    });

                    if (!ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverFill)
                        && !ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverDecoration))
                    {
                        string resource = ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementLine) ? "bms/target"
                            : ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBackdrop) ? "bms/backdrop"
                            : ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBaseplate) ? "bms/plate"
                            : ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual) ? "bms/gauge" : "bms/hud";
                        assertPackagedSemanticSurface(sceneHost, deck2Key, false, resource, !ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementDisplay));
                        if (state == GameplaySkinResolvedMaterialState.Provide)
                            assertPackagedSemanticSurface(sceneHost, deck1Key, true, "notes/deck-1");
                        else
                            Assert.That(sceneHost.TryGetHostedDrawable(deck1Key, out _), Is.False, "An author's explicit hidden information must not be revived by canonical fallback.");
                    }
                }

                foreach (GameplaySkinResolvedMaterialTarget scratchTarget in new[] { scratch1Target, scratch2Target })
                {
                    foreach (GameplaySkinSlotDescriptor slot in new[] { GameplaySkinSlotCatalog.Turntable, GameplaySkinSlotCatalog.Laser })
                    {
                        var key = new GameplaySkinResolvedMaterialKey(slot, scratchTarget);
                        Assert.That(materialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                        Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);

                        Assert.Multiple(() =>
                        {
                            Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                            Assert.That(entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                            Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                            Assert.That(gate.SuppressesProgrammaticVisual, Is.True);
                            Assert.That(sceneHost.TryGetHostedDrawable(key, out _), Is.True,
                                $"{slot.Id} must mount through the exact scratch-lane production target.");
                        });
                    }
                }

                Assert.Multiple(() =>
                {
                    Assert.That(scratch1Target.LaneId?.Value, Is.EqualTo("bms.lane.scratch-1"));
                    Assert.That(scratch1Target.GroupId?.Value, Is.EqualTo("bms.group.deck-1"));
                    Assert.That(scratch1Target.GlobalLogicalIndex, Is.Zero);
                    Assert.That(scratch1Target.GlobalVisualIndex, Is.Zero);
                    Assert.That(scratch1Target.GroupLocalLogicalIndex, Is.Zero);
                    Assert.That(scratch1Target.GroupLocalVisualIndex, Is.Zero);
                    Assert.That(scratch2Target.LaneId?.Value, Is.EqualTo("bms.lane.scratch-2"));
                    Assert.That(scratch2Target.GroupId?.Value, Is.EqualTo("bms.group.deck-2"));
                    Assert.That(scratch2Target.GlobalLogicalIndex, Is.EqualTo(15));
                    Assert.That(scratch2Target.GlobalVisualIndex, Is.EqualTo(15));
                    Assert.That(scratch2Target.GroupLocalLogicalIndex, Is.EqualTo(7));
                    Assert.That(scratch2Target.GroupLocalVisualIndex, Is.EqualTo(7));
                });

                BmsPlayfieldStageFallbackVisual[] playfieldStages = renderer.BmsDrawable.Playfield.GameplaySkinStageFallbackVisuals.ToArray();
                BmsGaugeBar gauge = renderer.BmsDrawable.ChildrenOfType<BmsGaugeBar>().Single();
                BmsComboCounter combo = renderer.BmsDrawable.ChildrenOfType<BmsComboCounter>().Single();
                BmsLaneCover[] covers = renderer.BmsDrawable.Playfield.LaneCovers.ToArray();
                BmsLane deck1Lane = renderer.BmsDrawable.Playfield.Lanes.First(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(groups[0].Identity.Id));
                BmsLane deck2Lane = renderer.BmsDrawable.Playfield.Lanes.First(lane =>
                    lane.LayoutSnapshotLane!.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(groups[1].Identity.Id));

                Assert.Multiple(() =>
                {
                    Assert.That(playfieldStages, Has.Length.EqualTo(2));
                    Assert.That(playfieldStages[0].Target, Is.EqualTo(deck1));
                    Assert.That(playfieldStages[1].Target, Is.EqualTo(deck2));
                    Assert.That(playfieldStages[0].BackdropVisual, Is.Not.SameAs(playfieldStages[1].BackdropVisual));
                    Assert.That(playfieldStages[0].BaseplateVisual, Is.Not.SameAs(playfieldStages[1].BaseplateVisual));
                    Assert.That(playfieldStages[0].JudgementVisual, Is.Not.SameAs(playfieldStages[1].JudgementVisual));
                    assertHiddenCompatibilityDecks(playfieldStages.Select(stage => (Drawable)stage.BackdropVisual), "playfield backdrop");
                    assertHiddenCompatibilityDecks(playfieldStages.Select(stage => (Drawable)stage.BaseplateVisual), "playfield baseplate");
                    assertHiddenCompatibilityDecks(playfieldStages.Select(stage => (Drawable)stage.JudgementVisual), "judgement display");
                    assertHiddenCompatibilityDecks(gauge.GameplaySkinStageFallbackVisuals, "gauge");
                    assertHiddenCompatibilityDecks(combo.GameplaySkinStageFallbackVisuals, "combo");
                    Assert.That(covers, Has.Length.EqualTo(2));
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals.Count == 2), Is.True);
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals[0].Target!.Equals(deck1)
                                                   && cover.GameplaySkinStageFallbackVisuals[0].FillVisual.Alpha == 0
                                                   && cover.GameplaySkinStageFallbackVisuals[0].DecorationVisual.Alpha == 0), Is.True);
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals[1].Target!.Equals(deck2)
                                                   && cover.GameplaySkinStageFallbackVisuals[1].FillVisual.Alpha == 0
                                                   && cover.GameplaySkinStageFallbackVisuals[1].DecorationVisual.Alpha == 0), Is.True,
                        "Canonical fills replace old geometry artwork; its explicitly absent optional decoration stays absent.");
                    Assert.That(deck1Lane.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero);
                    Assert.That(deck2Lane.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero);
                });
                foreach (BmsLaneCover cover in covers)
                {
                    GameplaySkinSpecialisedSceneVisual left = assertPackagedNativeSurface(sceneHost, cover, new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneCoverFill, deck1), true, "notes/deck-1");
                    GameplaySkinSpecialisedSceneVisual right = assertPackagedNativeSurface(sceneHost, cover, new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneCoverFill, deck2), false, "bms/cover");
                    Assert.That(left, Is.Not.SameAs(right));
                    Assert.That(left.RootDrawables.Single().ScreenSpaceDrawQuad.AABBFloat.Right,
                        Is.LessThanOrEqualTo(right.RootDrawables.Single().ScreenSpaceDrawQuad.AABBFloat.Left + 0.01f));
                    Assert.That(cover.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Any(visual => ReferenceEquals(visual.Key.Slot, GameplaySkinSlotCatalog.LaneCoverDecoration)), Is.False);
                }
            });
            AddStep("advance towards the real deck-2 note", () => renderer.AdvanceBmsTo(1_950));
            AddUntilStep("the real deck-2 ordinary note is ready for input", () =>
                renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().Any(note => note.IsLoaded && note.IsPresent && note.HandleUserInput
                    && note.HitObject is BmsHitObject { StartTime: 2_000 } hit
                    && !renderer.BmsDrawable.LayoutProvider.GetLaneForObject(hit).IsScratch
                    && renderer.BmsDrawable.LayoutProvider.GetLaneForObject(hit).NeutralLane.TopologyEntry.Identity.Group.Id.Value == "bms.group.deck-2"));
            AddStep("judge the real second-deck note", () =>
            {
                renderer.AdvanceBmsTo(2_000);
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key2P_1), Is.True);
                renderer.AdvanceBmsTo(2_020);
            });
            AddUntilStep("canonical deck-2 judgement is visibly produced by that input", () =>
            {
                GameplaySkinLaneTopologyGroup group = renderer.BmsDrawable.LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder[1];
                var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, GameplaySkinResolvedMaterialTarget.ForStage(group));
                return sceneHost.TryGetHostedDrawable(key, out Drawable? visual) && visual!.Alpha > 0;
            });
            AddStep("deck-1 suppression cannot consume the second-deck judgement and information", () =>
            {
                GameplaySkinLaneTopologyGroup[] groups = renderer.BmsDrawable.LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();
                GameplaySkinResolvedMaterialTarget deck1 = GameplaySkinResolvedMaterialTarget.ForStage(groups[0]);
                GameplaySkinResolvedMaterialTarget deck2 = GameplaySkinResolvedMaterialTarget.ForStage(groups[1]);
                Drawable judgement = assertPackagedSemanticSurface(sceneHost, new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, deck2), false, "bms/hud");
                Assert.That(judgement.ChildrenOfType<SpriteText>().Single().Text.ToString(), Is.Not.Empty.And.Not.EqualTo("miss"));
                Assert.That(sceneHost.TryGetHostedDrawable(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, deck1), out _), Is.False);
                Drawable combo = assertPackagedSemanticSurface(sceneHost, new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.ComboDisplay, deck2), false, "bms/hud");
                Assert.That(combo.ChildrenOfType<SpriteText>().Single().Text.ToString(), Is.Not.EqualTo("0"));
                assertPackagedSemanticSurface(sceneHost, new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.GaugeVisual, deck2), false, "bms/gauge");
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key2P_1), Is.True);
            });
            AddStep("detach 14K renderer", () => renderer.Expire());
            AddUntilStep("wait for 14K renderer detach", () => renderer.Parent == null);
        }

        private static void assertHiddenCompatibilityDecks(System.Collections.Generic.IEnumerable<Drawable> visuals, string slot)
        {
            Drawable[] exactStages = visuals.ToArray();
            Assert.That(exactStages, Has.Length.EqualTo(2), $"{slot} must expose one owner per exact 14K stage.");
            Assert.That(exactStages[0].Alpha, Is.Zero, $"The authored deck-1 {slot} fallback must be hidden.");
            Assert.That(exactStages[0], Is.Not.SameAs(exactStages[1]), $"{slot} must retain independent deck owners.");
            Assert.That(exactStages[1].Alpha, Is.Zero, $"The canonical deck-2 {slot} must not overlap its legacy owner.");
        }

        private static void writeFourteenKeyPartialStagePackage(string root)
        {
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllBytes(Path.Combine(notes, "deck-1.png"), createPng(new Rgba32(30, 190, 230, 255)));
            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\n" +
                "Name: C5 14K partial stage gate\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Stage ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-1 group-logical=0 group-visual=0\n" +
                "playfield.judgement-line: resource Provide \"notes/deck-1\"\n" +
                "playfield.lane-cover.fill: resource Provide \"notes/deck-1\"\n" +
                "hud.judgement: resource Suppress\n" +
                "hud.combo: resource Suppress\n" +
                "hud.gauge: resource Suppress\n" +
                "playfield.backdrop: resource Provide \"notes/deck-1\"\n" +
                "playfield.baseplate: resource Provide \"notes/deck-1\"\n" +
                "playfield.lane-cover.decoration: resource Suppress\n" +
                "\n[GameplaySkin.Bms:1]\n" +
                "Target: Lane ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-1 lane=bms.lane.scratch-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "playfield.turntable: resource Provide \"notes/deck-1\"\n" +
                "playfield.laser: resource Provide \"notes/deck-1\"\n" +
                "Target: Lane ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-2 lane=bms.lane.scratch-2 group-logical=1 group-visual=1 global-logical=15 global-visual=15 group-local-logical=7 group-local-visual=7\n" +
                "playfield.turntable: resource Provide \"notes/deck-1\"\n" +
                "playfield.laser: resource Provide \"notes/deck-1\"\n");
        }
    }
}
