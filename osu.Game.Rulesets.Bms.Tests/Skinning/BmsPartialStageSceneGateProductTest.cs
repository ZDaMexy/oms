// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
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
            AddStep("assert only deck 1 programmatic visuals are hidden", () =>
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
                        Assert.That(deck2Entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                        Assert.That(deck2Entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                        Assert.That(deck2Gate!.Route, Is.EqualTo(GameplaySkinSceneHostPolicy.RequiresNativeGeometry(slot, "bms")
                            ? GameplaySkinSceneHostRoute.Specialised
                            : GameplaySkinSceneHostRoute.Programmatic));
                        Assert.That(deck2Gate.AllowsProgrammaticVisual, Is.True);
                        Assert.That(deck2Gate.SuppressesProgrammaticVisual, Is.False);
                    });
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
                    assertHiddenOnlyOnDeck1(playfieldStages.Select(stage => (Drawable)stage.BackdropVisual), "playfield backdrop");
                    assertHiddenOnlyOnDeck1(playfieldStages.Select(stage => (Drawable)stage.BaseplateVisual), "playfield baseplate");
                    assertHiddenOnlyOnDeck1(playfieldStages.Select(stage => (Drawable)stage.JudgementVisual), "judgement display");
                    assertHiddenOnlyOnDeck1(gauge.GameplaySkinStageFallbackVisuals, "gauge");
                    assertHiddenOnlyOnDeck1(combo.GameplaySkinStageFallbackVisuals, "combo");
                    Assert.That(covers, Has.Length.EqualTo(2));
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals.Count == 2), Is.True);
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals[0].Target!.Equals(deck1)
                                                   && cover.GameplaySkinStageFallbackVisuals[0].FillVisual.Alpha == 0
                                                   && cover.GameplaySkinStageFallbackVisuals[0].DecorationVisual.Alpha == 0), Is.True);
                    Assert.That(covers.All(cover => cover.GameplaySkinStageFallbackVisuals[1].Target!.Equals(deck2)
                                                   && cover.GameplaySkinStageFallbackVisuals[1].FillVisual.Alpha > 0
                                                   && cover.GameplaySkinStageFallbackVisuals[1].DecorationVisual.Alpha > 0), Is.True,
                        "Both Sudden and Hidden owners must preserve the unauthored deck-2 fill/decoration.");
                    Assert.That(deck1Lane.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero);
                    Assert.That(deck2Lane.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.GreaterThan(0),
                        "A deck-1 judgement-line replacement must not hide deck 2.");
                });
            });
            AddStep("detach 14K renderer", () => renderer.Expire());
            AddUntilStep("wait for 14K renderer detach", () => renderer.Parent == null);
        }

        private static void assertHiddenOnlyOnDeck1(System.Collections.Generic.IEnumerable<Drawable> visuals, string slot)
        {
            Drawable[] exactStages = visuals.ToArray();
            Assert.That(exactStages, Has.Length.EqualTo(2), $"{slot} must expose one owner per exact 14K stage.");
            Assert.That(exactStages[0].Alpha, Is.Zero, $"The authored deck-1 {slot} fallback must be hidden.");
            Assert.That(exactStages[1].Alpha, Is.GreaterThan(0), $"The unauthored deck-2 {slot} fallback must remain visible.");
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
