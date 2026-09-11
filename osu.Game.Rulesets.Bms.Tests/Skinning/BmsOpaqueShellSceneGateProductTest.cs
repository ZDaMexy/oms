// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
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

                    // The independent native partitions are retained for the historical gate contract, but the
                    // C7 product supplies every omitted surface from the complete canonical author package.
                    Assert.That(stages[0].StageBackgroundVisual.Alpha, Is.Zero);
                    Assert.That(stages[0].BackdropVisual.Alpha, Is.Zero);
                    Assert.That(stages[1].StageBackgroundVisual.Alpha, Is.Zero);
                    Assert.That(stages[1].BackdropVisual.Alpha, Is.Zero);
                    Assert.That(stages[0].BaseplateVisual.Alpha, Is.Zero);
                    Assert.That(stages[1].BaseplateVisual.Alpha, Is.Zero);
                });

                foreach (GameplaySkinResolvedMaterialTarget deck in new[] { deck1, deck2 })
                {
                    Assert.That(sceneHost.TryGetHostedDrawable(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageBackground, deck), out Drawable? stageBackground), Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBackdrop, deck), out Drawable? backdrop), Is.True);
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

                foreach (GameplaySkinSlotDescriptor slot in new[] { GameplaySkinSlotCatalog.StageBackground, GameplaySkinSlotCatalog.PlayfieldBackdrop, GameplaySkinSlotCatalog.PlayfieldBaseplate })
                {
                    Assert.That(sceneHost.TryGetHostedDrawable(new GameplaySkinResolvedMaterialKey(slot, deck1), out Drawable? left), Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(new GameplaySkinResolvedMaterialKey(slot, deck2), out Drawable? right), Is.True);
                    Assert.That(left, Is.Not.SameAs(right), "Each surface must retain independent per-deck ownership.");
                    Assert.That(left!.ScreenSpaceDrawQuad.AABBFloat.Right, Is.LessThanOrEqualTo(right!.ScreenSpaceDrawQuad.AABBFloat.Left + 0.01f),
                        "The deck-1 surface must not cover the canonical deck-2 surface.");
                }

                void assertRoute(GameplaySkinResolvedMaterialKey key, bool provided)
                {
                    string resource = provided ? "notes/shell"
                        : ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.StageBackground) ? "bms/stage"
                        : ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop) ? "bms/backdrop" : "bms/plate";
                    assertPackagedSemanticSurface(sceneHost, key, provided, resource);
                }
            });
            AddStep("detach opaque-shell renderer", () => renderer.Expire());
            AddUntilStep("wait for opaque-shell renderer detach", () => renderer.Parent == null);
        }

        private static Drawable assertPackagedSemanticSurface(
            GameplaySkinSceneRuntimeHost scene,
            GameplaySkinResolvedMaterialKey key,
            bool selected,
            string resource,
            bool visible = true)
        {
            GameplaySkinPublicSlotMaterial material = assertPackagedSurfaceMaterial(scene, key, selected, resource, GameplaySkinSceneHostRoute.Semantic);
            Assert.That(scene.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
            Assert.That(scene.TryGetHostedDrawable(key, out Drawable? visual), Is.True);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual!.Parent, Is.SameAs(scene.Layers.Get(gate!.Layer)));
            Assert.That(visual.Alpha, visible ? Is.GreaterThan(0) : Is.Zero);
            Assert.That(visual.Position, Is.EqualTo(new Vector2(gate.PreparedRect.X, gate.PreparedRect.Y)));
            Assert.That(visual.Size, Is.EqualTo(new Vector2(gate.PreparedRect.Width, gate.PreparedRect.Height)));
            Assert.That(visual.ScreenSpaceDrawQuad.AABBFloat.Width, Is.GreaterThan(0));
            Assert.That(visual.ScreenSpaceDrawQuad.AABBFloat.Height, Is.GreaterThan(0));
            Assert.That(visual.ChildrenOfType<Sprite>().Single().Texture, Is.SameAs(material.Texture));
            return visual;
        }

        private static GameplaySkinPublicSlotMaterial assertPackagedSurfaceMaterial(
            GameplaySkinSceneRuntimeHost scene,
            GameplaySkinResolvedMaterialKey key,
            bool selected,
            string resource,
            GameplaySkinSceneHostRoute route)
        {
            Assert.That(scene.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
            Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
            Assert.That(entry.Source.Kind, Is.EqualTo(selected ? GameplaySkinResolvedMaterialSourceKind.SelectedPackage : GameplaySkinResolvedMaterialSourceKind.CanonicalPackage));
            Assert.That(entry.Material, Is.TypeOf<GameplaySkinPublicSlotMaterial>());
            var material = (GameplaySkinPublicSlotMaterial)entry.Material;
            Assert.That(material.IsProgrammaticFallback, Is.False);
            Assert.That(material.ResourceName, Is.EqualTo(resource));
            Assert.That(material.Texture, Is.Not.Null);
            Assert.That(scene.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
            Assert.That(gate!.Route, Is.EqualTo(route));
            Assert.That(gate.IsReplacementReady, Is.True);
            Assert.That(gate.AllowsProgrammaticVisual, Is.False);
            Assert.That(gate.SuppressesProgrammaticVisual, Is.True);
            return material;
        }

        private static GameplaySkinSpecialisedSceneVisual assertPackagedNativeSurface(
            GameplaySkinSceneRuntimeHost scene,
            CompositeDrawable owner,
            GameplaySkinResolvedMaterialKey key,
            bool selected,
            string resource)
        {
            GameplaySkinPublicSlotMaterial material = assertPackagedSurfaceMaterial(scene, key, selected, resource, GameplaySkinSceneHostRoute.Specialised);
            GameplaySkinSpecialisedSceneVisual visual = owner.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single(candidate => candidate.Key.Equals(key));
            Assert.That(visual.IsApplied, Is.True);
            Assert.That(visual.Alpha, Is.GreaterThan(0));
            Assert.That(visual.RuntimeNodes, Is.Empty);
            var sprite = (Sprite)visual.RootDrawables.Single();
            Assert.That(sprite.Texture, Is.SameAs(material.Texture));
            Assert.That(sprite.Alpha, Is.GreaterThan(0));
            Assert.That(sprite.ScreenSpaceDrawQuad.AABBFloat.Width, Is.GreaterThan(0));
            Assert.That(sprite.ScreenSpaceDrawQuad.AABBFloat.Height, Is.GreaterThan(0));
            return visual;
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
