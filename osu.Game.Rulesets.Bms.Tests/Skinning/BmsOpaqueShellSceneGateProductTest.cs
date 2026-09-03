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
        [TestCase(true, false, false, TestName = "StageBackground alone preserves the independent native backdrop")]
        [TestCase(false, true, false, TestName = "PlayfieldBackdrop alone preserves the independent native stage background")]
        [TestCase(true, true, false, TestName = "StageBackground and PlayfieldBackdrop replace independent native owners")]
        [TestCase(false, false, true, TestName = "PlayfieldBaseplate remains an independent native partition")]
        public void TestFourteenKeyOpaqueShellUsesExactIndependentSceneGates(
            bool provideStageBackground,
            bool providePlayfieldBackdrop,
            bool provideBaseplate)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create and select exact opaque-shell package", () =>
            {
                (_, candidate) = createCandidate(
                    root => writeOpaqueShellPackage(root, provideStageBackground, providePlayfieldBackdrop, provideBaseplate),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact opaque-shell revision", () =>
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
            AddStep("capture mounted opaque-shell scene host", () =>
                sceneHost = renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for bounded opaque-shell replacements", () => sceneHost.PendingCreationCount == 0);
            AddStep("assert exact deck gate and deterministic author stacking", () =>
            {
                BmsGameplayLayoutSnapshot layout = renderer.BmsLayoutProbe.Publication!.GetAdapter<BmsGameplayLayoutSnapshot>();
                GameplaySkinLaneTopologyGroup[] groups = layout.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();
                GameplaySkinResolvedMaterialTarget deck1 = GameplaySkinResolvedMaterialTarget.ForStage(groups[0]);
                GameplaySkinResolvedMaterialTarget deck2 = GameplaySkinResolvedMaterialTarget.ForStage(groups[1]);
                BmsPlayfieldStageFallbackVisual[] stages = renderer.BmsDrawable.Playfield.GameplaySkinStageFallbackVisuals.ToArray();
                var stageBackgroundKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageBackground, deck1);
                var backdropKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBackdrop, deck1);
                var baseplateKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBaseplate, deck1);
                var deck2StageBackgroundKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageBackground, deck2);
                var deck2BackdropKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBackdrop, deck2);
                var deck2BaseplateKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBaseplate, deck2);

                Assert.That(stages, Has.Length.EqualTo(2));
                Assert.Multiple(() =>
                {
                    assertRoute(stageBackgroundKey, provideStageBackground);
                    assertRoute(backdropKey, providePlayfieldBackdrop);
                    assertRoute(baseplateKey, provideBaseplate);
                    assertRoute(deck2StageBackgroundKey, false);
                    assertRoute(deck2BackdropKey, false);
                    assertRoute(deck2BaseplateKey, false);

                    Assert.That(stages[0].StageBackgroundVisual.Alpha,
                        provideStageBackground ? Is.Zero : Is.GreaterThan(0),
                        "StageBackground must own an exact gate which cannot suppress PlayfieldBackdrop.");
                    Assert.That(stages[0].BackdropVisual.Alpha,
                        providePlayfieldBackdrop ? Is.Zero : Is.GreaterThan(0),
                        "PlayfieldBackdrop must own an exact gate which cannot suppress StageBackground.");
                    Assert.That(stages[1].StageBackgroundVisual.Alpha, Is.GreaterThan(0),
                        "A deck-1 author stage background must not gate the deck-2 native stage owner.");
                    Assert.That(stages[1].BackdropVisual.Alpha, Is.GreaterThan(0),
                        "A deck-1 author backdrop must not gate the deck-2 native backdrop.");
                    Assert.That(stages[0].BaseplateVisual.Alpha,
                        provideBaseplate ? Is.Zero : Is.GreaterThan(0),
                        "Background replacement must not consume the independent baseplate partition.");
                    Assert.That(stages[1].BaseplateVisual.Alpha, Is.GreaterThan(0));
                });

                if (provideStageBackground && providePlayfieldBackdrop)
                {
                    Assert.That(sceneHost.TryGetHostedDrawable(stageBackgroundKey, out Drawable? stageBackground), Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(backdropKey, out Drawable? backdrop), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(stageBackground, Is.Not.Null);
                        Assert.That(backdrop, Is.Not.Null);
                        Assert.That(stageBackground!.Parent, Is.SameAs(sceneHost.Layers.Background));
                        Assert.That(backdrop!.Parent, Is.SameAs(sceneHost.Layers.Background));
                        Assert.That(stageBackground.Alpha, Is.GreaterThan(0));
                        Assert.That(backdrop.Alpha, Is.GreaterThan(0));
                        Assert.That(stageBackground.Depth, Is.GreaterThan(backdrop.Depth),
                            "StageBackground is the deterministic rear surface; PlayfieldBackdrop stays above it.");
                    });
                }

                void assertRoute(GameplaySkinResolvedMaterialKey key, bool provided)
                {
                    Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    Assert.That(gate!.Route, Is.EqualTo(provided
                        ? GameplaySkinSceneHostRoute.Semantic
                        : GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(gate.IsReplacementReady, Is.EqualTo(provided));
                    Assert.That(sceneHost.TryGetHostedDrawable(key, out Drawable? authored), Is.EqualTo(provided));

                    if (provided)
                        Assert.That(authored!.Alpha, Is.GreaterThan(0));
                }
            });
            AddStep("detach opaque-shell renderer", () => renderer.Expire());
            AddUntilStep("wait for opaque-shell renderer detach", () => renderer.Parent == null);
        }

        private static void writeOpaqueShellPackage(
            string root,
            bool provideStageBackground,
            bool providePlayfieldBackdrop,
            bool provideBaseplate)
        {
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllBytes(Path.Combine(notes, "shell.png"), createPng(new Rgba32(35, 185, 225, 255)));

            string declarations = string.Empty;

            if (provideStageBackground)
                declarations += "stage.background: resource Provide \"notes/shell\"\n";

            if (providePlayfieldBackdrop)
                declarations += "playfield.backdrop: resource Provide \"notes/shell\"\n";

            if (provideBaseplate)
                declarations += "playfield.baseplate: resource Provide \"notes/shell\"\n";

            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\n" +
                "Name: C5 BMS opaque shell gate\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Stage ruleset=bms keymode=14k stage-mode=dual group=bms.group.deck-1 group-logical=0 group-visual=0\n" +
                declarations);
        }
    }
}
