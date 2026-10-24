// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestLaneCoverOpacityMultipliesAuthoredAndCanonicalDualDeckVisuals()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            BmsLaneCover[] covers = Array.Empty<BmsLaneCover>();
            GameplaySkinSpecialisedSceneVisual[] initialVisuals = Array.Empty<GameplaySkinSpecialisedSceneVisual>();
            int initialInstances = 0;

            AddStep("select an authored first-deck cover with a canonical second deck", () =>
            {
                (_, candidate) = createCandidate(writeLaneCoverOpacityPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("the exact authored revision is committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real dual-deck covers", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFourteenKeyBeatmap: true, useFixedBmsClock: true));
                renderer.ShowBms();
            });
            AddUntilStep("the author scene and native owners finish loading", () => renderer.BmsReady
                && renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady == true
                && renderer.BmsDrawable.Playfield.LaneCovers.All(cover => cover.IsLoaded));
            AddStep("show both SUD and HID without changing their native coverage", () =>
            {
                scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                covers = renderer.BmsDrawable.Playfield.LaneCovers.ToArray();
                Assert.That(covers.Select(cover => cover.CoverPosition),
                    Is.EquivalentTo(new[] { BmsLaneCoverPosition.Sudden, BmsLaneCoverPosition.Hidden }));
                foreach (BmsLaneCover cover in covers)
                    cover.CoverPercent.Value = 200;
            });
            AddUntilStep("both decks retain their loaded replacement surfaces", () => covers.All(cover =>
                cover.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Count(visual =>
                    ReferenceEquals(visual.Key.Slot, GameplaySkinSlotCatalog.LaneCoverFill) && visual.IsLoaded && visual.IsApplied) == 2));
            AddStep("retain the real surfaces before adjusting transparency", () =>
            {
                scene.ProcessFrame();
                initialVisuals = covers.SelectMany(cover => cover.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()).ToArray();
                initialInstances = scene.RuntimeInstanceCount;
                Assert.That(renderer.BmsLayoutProbe.Publication!.PreparedScene.HasAuthorScene, Is.True);
            });

            foreach (int opacity in new[] { 1000, 250, 0, 250, 1000 })
            {
                AddStep($"set native cover opacity to {opacity}", () =>
                {
                    foreach (BmsLaneCover cover in covers)
                        cover.CoverOpacity.Value = opacity;
                });
                AddStep($"both cover positions multiply authored opacity by {opacity}/1000", () =>
                {
                    scene.ProcessFrame();
                    foreach (BmsLaneCover cover in covers)
                        assertLaneCoverOpacity(scene, cover, opacity / 1000f);
                    Assert.That(covers.SelectMany(cover => cover.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()),
                        Is.EqualTo(initialVisuals), "Changing transparency must retain the mounted visual instances.");
                    Assert.That(scene.RuntimeInstanceCount, Is.EqualTo(initialInstances));
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                });
            }

            AddStep("detach the dual-deck renderer", () => renderer.Expire());
            AddUntilStep("the renderer detaches", () => renderer.Parent == null);
        }

        private static void assertLaneCoverOpacity(GameplaySkinSceneRuntimeHost scene, BmsLaneCover cover, float opacity)
        {
            GameplaySkinSpecialisedSceneVisual[] fills = cover.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                .Where(visual => ReferenceEquals(visual.Key.Slot, GameplaySkinSlotCatalog.LaneCoverFill)).ToArray();
            GameplaySkinSpecialisedSceneVisual author = fills.Single(visual => visual.Key.Target.GroupId!.Value == "bms.group.deck-1");
            GameplaySkinSpecialisedSceneVisual canonical = fills.Single(visual => visual.Key.Target.GroupId!.Value == "bms.group.deck-2");
            GameplaySkinSceneRuntimeNode authorRoot = author.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "cover.author");
            GameplaySkinSceneRuntimeNode animationNode = author.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "cover.animation");
            var animation = (GameplaySkinRecordedAnimationDrawable)animationNode.ContentDrawable;
            Sprite authorSprite = animation.ChildrenOfType<Sprite>().Single();
            var canonicalSprite = (Sprite)canonical.RootDrawables.Single();

            Assert.Multiple(() =>
            {
                Assert.That(scene.MaterialSet.TryGet(author.Key, out GameplaySkinResolvedMaterialEntry? authorEntry), Is.True);
                Assert.That(authorEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                Assert.That(scene.MaterialSet.TryGet(canonical.Key, out GameplaySkinResolvedMaterialEntry? canonicalEntry), Is.True);
                Assert.That(canonicalEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.CanonicalPackage));
                Assert.That(authorSprite.Texture, Is.Not.Null);
                Assert.That(canonicalSprite.Texture, Is.Not.Null);
                Assert.That(authorSprite.ScreenSpaceDrawQuad.AABBFloat.Height, Is.GreaterThan(0));
                Assert.That(canonicalSprite.ScreenSpaceDrawQuad.AABBFloat.Height, Is.GreaterThan(0));
                Assert.That(authorSprite.ScreenSpaceDrawQuad.AABBFloat.Right,
                    Is.LessThanOrEqualTo(canonicalSprite.ScreenSpaceDrawQuad.AABBFloat.Left + 0.01f));

                // The final sprite colour includes its actual native owner chain; a local Alpha-only assertion
                // would miss the authored sibling bypassing the legacy display's CoverOpacity multiplier.
                Assert.That(authorSprite.DrawColourInfo.Colour.AverageColour.Linear.A,
                    Is.EqualTo(0.4f * 0.5f * opacity).Within(0.00001f), $"{cover.CoverPosition}: author cover opacity");
                Assert.That(canonicalSprite.DrawColourInfo.Colour.AverageColour.Linear.A,
                    Is.EqualTo(opacity).Within(0.00001f), $"{cover.CoverPosition}: canonical second-deck opacity");
                Assert.That(authorRoot.TransformDrawable.Alpha, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(animationNode.TransformDrawable.Alpha, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(authorSprite.Alpha, Is.EqualTo(1), "The recorded piece retains its own animation alpha.");
                Assert.That(author.Alpha, Is.EqualTo(1), "The scene lease remains responsible for its visibility gate.");
                Assert.That(canonical.Alpha, Is.EqualTo(1));
                Assert.That(cover.CoverPercent.Value, Is.EqualTo(200));
                Assert.That(cover.GameplaySkinStageFallbackVisuals.All(stage => stage.FillVisual.Alpha == 0), Is.True);
            });
        }

        private static void writeLaneCoverOpacityPackage(string root)
        {
            writeFourteenKeyPartialStagePackage(root);
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(new JObject { ["id"] = "texture.cover", ["type"] = "texture", ["path"] = "notes/deck-1.png" }),
                ["animations"] = new JArray(new JObject
                {
                    ["id"] = "animation.cover",
                    ["path"] = "cover.json",
                    ["resources"] = new JArray("texture.cover"),
                }),
            }.ToString());
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), """
                {"contract":"oms-gameplay-skin-scene.v1",
                 "root":{"id":"cover.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[
                   {"id":"cover.author","type":"container","target":{"kind":"stage","id":"bms.group.deck-1","index":0},"slot":"playfield.lane-cover.fill",
                    "blend":"alpha","properties":{"opacity":0.4},"effects":[],"children":[
                     {"id":"cover.animation","type":"recorded-animation","target":{"kind":"stage","id":"bms.group.deck-1","index":0},
                      "animation":"animation.cover","blend":"alpha","properties":{"opacity":0.5},"effects":[],"children":[]}]}]},
                 "tracks":[],"stateMachines":[],"bindings":[],"templates":[],"instances":[]}
                """);
            File.WriteAllText(Path.Combine(root, "cover.json"), """
                {"contract":"oms-gameplay-recorded-animation.v1","width":100,"height":100,"fps":2,"loop":true,"trigger":"gameplay.attach",
                 "geometries":[{"resource":"texture.cover","vertices":[[0,0,0,0,0],[100,0,0,1,0],[0,100,0,0,1]],"triangles":[0,1,2]}],
                 "nodes":[{"parent":-1,"geometry":0,"blend":"normal"}],
                 "states":[{"matrix":[1,0,0,0,1,0,0,0,1,0,0,0],"origin":[0,0,0],"projection":"affine","multiply":[1,1,1,1],
                            "add":[0,0,0,0],"hsl":[0,0,0],"mask":null,"colourMatrix":null}],
                 "frames":[{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null}]}
                """);
        }
    }
}
