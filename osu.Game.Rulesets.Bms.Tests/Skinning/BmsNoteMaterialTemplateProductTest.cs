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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(true, "notes/public-note", true)]
        [TestCase(false, "notes/public-note", false)]
        [TestCase(true, "notes/different-name", false)]
        public void TestTypedNoteAndLongNoteMaterialTemplatesConsumeExactDeclarationsThroughNativeOwners(
            bool publicDeclaration, string selectorResource, bool matches)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            DrawableBmsHitObject ordinary = null!;
            DrawableBmsHoldNote hold = null!;
            BmsAsyncNoteDrawable[] surfaces = Array.Empty<BmsAsyncNoteDrawable>();
            string? lastReadiness = null;

            AddStep("select a package with typed Note/LN payloads and material-selected templates", () =>
            {
                (_, candidate) = createCandidate(root => writeNoteMaterialTemplatePackage(root, publicDeclaration, selectorResource),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("the package revision is committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount actual five-key native pools with a charge long note", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true, useChargeLongNotes: true));
                renderer.ShowBms();
            });
            AddUntilStep("ordinary Note and the three nested long-note surfaces exist in the native pools", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                {
                    reportReadiness($"renderer={renderer.BmsReady} scene={renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady}");
                    return false;
                }
                scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                hold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().SingleOrDefault()!;
                ordinary = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                   .SingleOrDefault(note => note.HitObject.GetType() == typeof(BmsHitObject)
                                                            && ((BmsHitObject)note.HitObject).LaneIndex == 1)!;
                if (hold == null || ordinary == null)
                {
                    reportReadiness($"time={renderer.BmsDrawable.FrameStableClock.CurrentTime:0.###} mode={renderer.BmsBeatmap.BmsInfo.Keymode} "
                        + $"hold={hold != null} ordinary={ordinary != null} active="
                        + string.Join(",", renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                                  .Select(note => $"{note.HitObject.GetType().Name}:{((BmsHitObject)note.HitObject).LaneIndex}")));
                    return false;
                }
                surfaces = ordinary.ChildrenOfType<BmsAsyncNoteDrawable>()
                                   .Concat(hold.ChildrenOfType<BmsAsyncNoteDrawable>()).ToArray();
                reportReadiness($"time={renderer.BmsDrawable.FrameStableClock.CurrentTime:0.###} surfaces="
                    + string.Join(",", surfaces.Select(surface => $"{surface.Lookup.Element}:{surface.Drawable?.GetType().Name}:{surface.Drawable?.LoadState}")));
                return surfaces.Length == 4;
            });
            AddUntilStep("all native material callbacks publish their typed source-bound visual", () =>
            {
                // A C5 replacement intentionally hides the C4 wrapper. Its child may remain Ready rather than
                // Loaded because LoadComplete runs only on update; publication is the relevant boundary here.
                reportReadiness(string.Join(",", surfaces.Select(surface =>
                    $"{surface.Lookup.Element}:{surface.Drawable?.GetType().Name}:{surface.Drawable?.LoadState}:parent={surface.Drawable?.Parent != null}")));
                return surfaces.All(surface => surface.Drawable is BmsSourceBoundNoteDrawable or BmsSourceBoundLongNoteBodyDrawable
                                               && surface.Drawable!.Parent != null);
            });
            AddStep("verify actual typed materials, scene routing, native geometry and fallback visibility", () =>
            {
                scene.ProcessFrame();
                Assert.That(ordinary.Height, Is.EqualTo(22.5f), "The native note envelope and scrolling owner remain unchanged.");
                Assert.That(surfaces.Select(surface => surface.Lookup.Element), Is.EquivalentTo(new[]
                {
                    BmsNoteSkinElements.Note, BmsNoteSkinElements.LongNoteHead,
                    BmsNoteSkinElements.LongNoteBody, BmsNoteSkinElements.LongNoteTail,
                }));

                foreach (BmsAsyncNoteDrawable surface in surfaces)
                {
                    GameplaySkinResolvedMaterialKey key = surface.ResolvedMaterialKey;
                    Assert.That(scene.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(entry!.Material, Is.InstanceOf<IBmsResolvedNoteMaterial>());
                        Assert.That(entry.Material, Is.Not.InstanceOf<GameplaySkinPublicSlotMaterial>(),
                            "A typed BMS payload must not be replaced by a fake generic public texture wrapper.");
                        Assert.That(entry.Source.IsSelectedDocumentDeclaration, Is.EqualTo(publicDeclaration));
                        Assert.That(surface.Lookup.LaneIndex, Is.EqualTo(1));
                        Assert.That(surface.SceneVisualGate.Key, Is.EqualTo(key));
                        Assert.That(surface.SceneVisualGate.RoutedNodes.Count, Is.EqualTo(matches ? 1 : 0));
                        Assert.That(surface.Drawable!.Parent!.Alpha, Is.EqualTo(matches ? 0 : 1),
                            "The existing committed C4 visual is hidden only after this exact native scene replacement is ready.");
                    });

                    if (!matches)
                    {
                        Assert.That(surface.SpecialisedSceneVisual, Is.Null);
                        Assert.That(surface.AppliedSceneNodeIds, Is.Empty);
                        Assert.That(surface.SceneVisualGate.AllowsProgrammaticVisual, Is.True);
                        continue;
                    }

                    GameplaySkinSpecialisedSceneVisual visual = surface.SpecialisedSceneVisual!;
                    Assert.That(visual, Is.Not.Null);
                    GameplaySkinSceneRuntimeNode node = visual.RuntimeNodes.Single();
                    double height = noteMaterialHeight(surface.Lookup.Element);
                    Assert.Multiple(() =>
                    {
                        Assert.That(surface.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(surface.SceneVisualGate.IsReplacementReady, Is.True);
                        Assert.That(visual.IsApplied, Is.True);
                        Assert.That(isDescendantOf(visual, surface), Is.True);
                        Assert.That(visual.BoundObjectId, Is.EqualTo(renderer.BmsDrawable.GetGameplaySkinObjectId(
                            surface.Lookup.Element == BmsNoteSkinElements.Note ? ordinary.HitObject : hold.HitObject)));
                        Assert.That(surface.AppliedSceneNodeIds, Is.EquivalentTo(new[] { node.PreparedNode.InstanceId }));
                        Assert.That(node.PreparedNode.InstanceId, Does.Contain("/lane-1/"));
                        Assert.That(node.PreparedNode.ResolvedTarget.StableId, Is.EqualTo(surface.Lookup.LaneId!.Value));
                        Assert.That(node.PreparedNode.OwningSlotKey, Is.EqualTo(key));
                        Assert.That(node.RootDrawable.DrawHeight, Is.EqualTo(surface.DrawHeight).Within(0.001));
                        Assert.That(node.TransformDrawable.Height, Is.EqualTo(height).Within(0.0001));
                        Assert.That(node.TransformDrawable.DrawHeight, Is.EqualTo(surface.DrawHeight * height).Within(0.001));
                        Assert.That(((Sprite)node.ContentDrawable).ScreenSpaceDrawQuad.Height,
                            Is.EqualTo(surface.ScreenSpaceDrawQuad.Height * height).Within(0.01),
                            "The loaded sprite consumes authored thickness in the actual native coordinate system.");
                        Assert.That(((Sprite)node.ContentDrawable).Texture!.Height,
                            Is.EqualTo(surface.Lookup.Element == BmsNoteSkinElements.Note ? 15 : 24));
                        Assert.That(node.Rect, Is.EqualTo(surface.SceneVisualGate.PreparedRect),
                            "The template does not replace the frozen C3 lane or native scrolling geometry.");
                    });
                }

                GameplaySkinPreparedSceneNode[] noteRoots = scene.PreparedScene.Roots.Where(root => root.Source.Id.StartsWith("typed-note.", StringComparison.Ordinal)).ToArray();
                Assert.That(noteRoots, Has.Length.EqualTo(matches ? 4 : 0));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("typed native note template owners detach", () => renderer.Parent == null);

            void reportReadiness(string state)
            {
                if (state == lastReadiness)
                    return;
                lastReadiness = state;
                TestContext.Out.WriteLine($"Typed Note/LN material readiness: {state}");
            }
        }

        private static double noteMaterialHeight(BmsNoteSkinElements element)
            => element switch
            {
                BmsNoteSkinElements.Note => 15d * 768 / 1080 / 22.5,
                BmsNoteSkinElements.LongNoteHead or BmsNoteSkinElements.LongNoteTail => 24d * 768 / 1080 / 22.5,
                BmsNoteSkinElements.LongNoteBody => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(element)),
            };

        private static void writeNoteMaterialTemplatePackage(string root, bool publicDeclaration, string selectorResource)
        {
            writePublicCommonFiveKeyNotePackage(root);
            var definitions = new[]
            {
                (Kind: "note", Slot: "object.note", Element: BmsNoteSkinElements.Note),
                (Kind: "head", Slot: "object.long-note.head", Element: BmsNoteSkinElements.LongNoteHead),
                (Kind: "body", Slot: "object.long-note.body", Element: BmsNoteSkinElements.LongNoteBody),
                (Kind: "tail", Slot: "object.long-note.tail", Element: BmsNoteSkinElements.LongNoteTail),
            };
            if (!publicDeclaration)
            {
                string iniPath = Path.Combine(root, "skin.ini");
                string ini = File.ReadAllText(iniPath).Replace("Keymode: 5K", "Keymode: 5K\nNoteImage1: notes/public-note\n"
                    + "NoteImage1H: notes/public-note\nNoteImage1L: notes/public-note\nNoteImage1T: notes/public-note", StringComparison.Ordinal);
                foreach (var definition in definitions)
                    ini = ini.Replace(definition.Slot + ": resource Provide \"notes/public-note\"\n", string.Empty, StringComparison.Ordinal);
                File.WriteAllText(iniPath, ini);
            }

            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            foreach (var definition in definitions)
            {
                string path = "notes/template-" + definition.Kind + ".png";
                using (var image = new Image<Rgba32>(40, definition.Kind == "note" ? 15 : 24, new Rgba32(40, 110, 240, 255)))
                using (Stream output = File.Create(Path.Combine(root, path)))
                    image.SaveAsPng(output);
                ((JArray)manifest["resources"]!).Add(new JObject { ["id"] = "texture.typed-" + definition.Kind, ["type"] = "texture", ["path"] = path });
            }
            File.WriteAllText(manifestPath, manifest.ToString());
            var templates = new JArray();
            var instances = new JArray();
            foreach (var definition in definitions)
            {
                string id = "typed-note." + definition.Kind;
                templates.Add(new JObject
                {
                    ["id"] = "template." + id,
                    ["root"] = new JObject
                    {
                        ["id"] = id,
                        ["type"] = "sprite",
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["slot"] = definition.Slot,
                        ["resource"] = "texture.typed-" + definition.Kind,
                        ["blend"] = "alpha",
                        ["properties"] = new JObject { ["height"] = noteMaterialHeight(definition.Element) },
                        ["effects"] = new JArray(),
                        ["children"] = new JArray(),
                    },
                });
                instances.Add(new JObject
                {
                    ["id"] = "instance." + id,
                    ["template"] = "template." + id,
                    ["material"] = new JObject { ["slot"] = definition.Slot, ["resource"] = selectorResource },
                });
            }
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = JObject.Parse("""
                    {"id":"typed-material.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]}
                    """),
                ["templates"] = templates,
                ["instances"] = instances,
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(),
            }.ToString());
        }
    }
}
