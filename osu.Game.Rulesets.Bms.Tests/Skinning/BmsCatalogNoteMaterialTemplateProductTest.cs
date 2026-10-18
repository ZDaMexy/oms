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
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void TestCatalogTypedNoteTemplatesUseMergedDeclarationIdentityAndNativeOwners(bool publicDeclaration)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            BmsManagedPackageSourceRevision sourceBefore = null!;
            BmsAsyncNoteDrawable[] surfaces = Array.Empty<BmsAsyncNoteDrawable>();
            AddStep("select a captured catalog whose note declarations and scene belong to one module", () =>
            {
                (_, candidate) = createCandidate(root => writeCatalogNoteMaterialTemplatePackage(root, publicDeclaration),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("the selected catalog package revision is committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount actual five-key native note and charge-note pools", () =>
            {
                sourceBefore = ((BmsLegacySkin)manager.CurrentRevision.Owner).CaptureManagedPackageSourceRevision();
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true, useChargeLongNotes: true));
                renderer.ShowBms();
            });
            AddUntilStep("the selected catalog and four typed native material callbacks are ready", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                    return false;
                DrawableBmsHoldNote? hold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().SingleOrDefault();
                DrawableBmsHitObject? ordinary = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                    .SingleOrDefault(note => note.HitObject.GetType() == typeof(BmsHitObject) && ((BmsHitObject)note.HitObject).LaneIndex == 1);
                if (ordinary == null || hold == null)
                    return false;
                surfaces = ordinary.ChildrenOfType<BmsAsyncNoteDrawable>().Concat(hold.ChildrenOfType<BmsAsyncNoteDrawable>()).ToArray();
                return surfaces.Length == 4
                       && surfaces.All(surface => surface.Drawable is BmsSourceBoundNoteDrawable or BmsSourceBoundLongNoteBodyDrawable
                                                  && surface.Drawable!.Parent != null);
            });
            AddStep("assert merged public provenance and all four actual native scene replacements", () =>
            {
                GameplaySkinSceneRuntimeHost scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                scene.ProcessFrame();
                GameplaySkinLayoutPublication publication = scene.Publication;
                GameplaySkinLayoutSnapshot snapshot = publication.Snapshot;
                GameplaySkinContentSelection selection = snapshot.Context.ContentSelection!;
                BmsLegacySkin selected = (BmsLegacySkin)manager.CurrentRevision.Owner;
                GameplaySkinDocument boundDocument = selected.GetGameplaySkinDocument(snapshot.Context).BindToPublication(snapshot);
                Assert.Multiple(() =>
                {
                    Assert.That(selection, Is.Not.Null);
                    Assert.That(selection.OptionValues.Values["notes"].CanonicalValue, Is.EqualTo("standard"));
                    Assert.That(selection.MaterialDocument.Identity.ContentRevision, Is.Not.EqualTo(sourceBefore.ParsedConfigurationContentHash),
                        "This regression requires a genuinely different merged-INI hash rather than a plain package.");
                    Assert.That(boundDocument.Identity.ContentRevision, Is.EqualTo(selection.MaterialDocument.Identity.ContentRevision));
                    Assert.That(boundDocument.BoundPublicationSnapshot, Is.SameAs(snapshot));
                    Assert.That(selected.CaptureManagedPackageSourceRevision().Equals(sourceBefore), Is.True,
                        "Selecting module content must not rewrite or replace the immutable original source guard.");
                    Assert.That(publication.MaterialSet.PackageRevision, Is.SameAs(snapshot.Context.PackageRevision));
                    Assert.That(surfaces.Select(surface => surface.Lookup.Element), Is.EquivalentTo(new[]
                    {
                        BmsNoteSkinElements.Note, BmsNoteSkinElements.LongNoteHead,
                        BmsNoteSkinElements.LongNoteBody, BmsNoteSkinElements.LongNoteTail,
                    }));
                });

                foreach (BmsAsyncNoteDrawable surface in surfaces)
                {
                    GameplaySkinResolvedMaterialKey key = surface.ResolvedMaterialKey;
                    Assert.That(scene.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(entry!.Material, Is.InstanceOf<IBmsResolvedNoteMaterial>());
                        Assert.That(entry.Source.IsSelectedDocumentDeclaration, Is.EqualTo(publicDeclaration));
                        Assert.That(entry.Source.ContentRevision, Is.EqualTo(publicDeclaration
                            ? boundDocument.Identity.ContentRevision : sourceBefore.ParsedConfigurationContentHash));
                        Assert.That(surface.SceneVisualGate.Key, Is.EqualTo(key));
                        Assert.That(surface.SceneVisualGate.RoutedNodes.Count, Is.EqualTo(publicDeclaration ? 1 : 0));
                    });
                    if (!publicDeclaration)
                    {
                        Assert.That(surface.SpecialisedSceneVisual, Is.Null, "A legacy NoteImage still cannot claim an authored Provide template.");
                        Assert.That(surface.AppliedSceneNodeIds, Is.Empty);
                        Assert.That(surface.Drawable!.Parent!.Alpha, Is.EqualTo(1));
                        continue;
                    }

                    GameplaySkinSpecialisedSceneVisual visual = surface.SpecialisedSceneVisual!;
                    Assert.That(visual, Is.Not.Null, $"{key}: selected declaration must have its real native scene owner.");
                    GameplaySkinSceneRuntimeNode node = visual.RuntimeNodes.Single();
                    Assert.Multiple(() =>
                    {
                        Assert.That(surface.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(surface.SceneVisualGate.IsReplacementReady, Is.True);
                        Assert.That(visual.IsApplied, Is.True);
                        Assert.That(surface.AppliedSceneNodeIds, Is.EquivalentTo(new[] { node.PreparedNode.InstanceId }));
                        Assert.That(node.PreparedNode.OwningSlotKey, Is.EqualTo(key));
                        Assert.That(node.PreparedNode.ResolvedTarget.StableId, Is.EqualTo(surface.Lookup.LaneId!.Value));
                        Assert.That(node.TransformDrawable.Height, Is.EqualTo(noteMaterialHeight(surface.Lookup.Element)).Within(0.0001));
                        Assert.That(((Sprite)node.ContentDrawable).Texture!.Height,
                            Is.EqualTo(surface.Lookup.Element == BmsNoteSkinElements.Note ? 15 : 24));
                        Assert.That(((Sprite)node.ContentDrawable).ScreenSpaceDrawQuad.Height,
                            Is.EqualTo(surface.ScreenSpaceDrawQuad.Height * noteMaterialHeight(surface.Lookup.Element)).Within(0.01));
                        Assert.That(surface.Drawable!.Parent!.Alpha, Is.Zero,
                            "The original committed material is hidden only when the selected native scene is applied.");
                    });
                }
                Assert.That(scene.PreparedScene.Roots.Count(root => root.Source.Id.StartsWith("typed-note.", StringComparison.Ordinal)),
                    Is.EqualTo(publicDeclaration ? 4 : 0));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("catalog note template owners detach", () => renderer.Parent == null);
        }

        private static void writeCatalogNoteMaterialTemplatePackage(string root, bool publicDeclaration)
        {
            writeNoteMaterialTemplatePackage(root, publicDeclaration, "notes/public-note");
            string directory = Path.Combine(root, "module");
            Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            JObject originalManifest = JObject.Parse(File.ReadAllText(manifestPath));
            JObject fragmentManifest = (JObject)originalManifest.DeepClone();
            fragmentManifest.Remove("options");
            File.WriteAllText(Path.Combine(directory, "manifest.json"), fragmentManifest.ToString());
            File.Copy(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), Path.Combine(directory, "scene.json"));
            originalManifest["resources"] = new JArray();
            originalManifest["contentCatalog"] = "content.json";
            File.WriteAllText(manifestPath, originalManifest.ToString());
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = JObject.Parse("""
                    {"id":"catalog.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]}
                    """),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(),
            }.ToString());

            string iniPath = Path.Combine(root, "skin.ini");
            string ini = File.ReadAllText(iniPath);
            string[] noteSlots = { "object.note", "object.long-note.head", "object.long-note.body", "object.long-note.tail" };
            foreach (string slot in noteSlots)
                ini = ini.Replace(slot + ": resource Provide \"notes/public-note\"\n", string.Empty, StringComparison.Ordinal);
            File.WriteAllText(iniPath, ini);
            File.WriteAllText(Path.Combine(directory, "materials.ini"), "[GameplaySkin.Common:1]\n"
                + "Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n"
                + string.Join("\n", noteSlots.Select(slot => slot + (publicDeclaration
                    ? ": resource Provide \"notes/public-note\"" : ": resource Inherit"))) + "\n");
            File.WriteAllText(Path.Combine(root, "content.json"), new JObject
            {
                ["contract"] = GameplaySkinContentBudgets.CONTRACT_ID,
                ["modules"] = new JArray(new JObject
                {
                    ["id"] = "notes",
                    ["label"] = "Notes",
                    ["default"] = "standard",
                    ["choices"] = new JArray(new JObject
                    {
                        ["value"] = "standard",
                        ["label"] = "Standard",
                        ["manifest"] = "module/manifest.json",
                        ["scene"] = "module/scene.json",
                        ["ini"] = "module/materials.ini",
                    }),
                }),
            }.ToString());
        }
    }
}
