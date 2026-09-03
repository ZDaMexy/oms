// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneManiaGameplaySkinLayoutProduction
    {
        [Test]
        public void TestDualStageLaneCoverAuthorSceneUsesOneNativeModHostPerExactStage()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneRuntime = null!;
            ManiaModCover mod = null!;
            ManiaGameplaySkinLaneCoverHost[] hosts = null!;

            AddStep("create isolated dual-stage lane-cover skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select dual-stage lane-cover scene package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-lane-cover-dual-{Guid.NewGuid():N}");
                writeManiaLaneCoverScenePackage(packageRoot, dualStage: true, suppressLastStageDecoration: false);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact dual-stage lane-cover revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("construct exact dual-stage mania renderer", () =>
                renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!, dualStage: true));
            AddStep("assert no engine cover means no authored cover host", () =>
            {
                Assert.That(renderer.Drawable.Playfield.Stages
                                    .SelectMany(stage => stage.ChildrenOfType<ManiaGameplaySkinLaneCoverHost>()),
                    Is.Empty);
                Assert.That(renderer.Drawable.Playfield.Stages
                                    .SelectMany(stage => stage.Columns)
                                    .All(column => findCoverAncestor(column.HitObjectContainer) == null),
                    Is.True);
            });
            AddStep("apply real configurable cover mod", () =>
            {
                mod = new ManiaModCover();
                mod.Coverage.Value = 0.5f;
                mod.Direction.Value = CoverExpandDirection.AlongScroll;
                renderer.Dispose();
                renderer = new CurrentRevisionManiaMaterialHost(
                    publicMaterialSkinManager!,
                    dualStage: true,
                    mods: new Mod[] { mod });
            });
            AddStep("mount exact dual-stage mania renderer", () => Add(renderer));
            AddUntilStep("wait for exact dual-stage ruleset", () =>
                renderer.Drawable.IsLoaded && renderer.Drawable.Playfield.Stages.Count == 2);
            AddStep("activate both native stages", () => renderer.AddProductionBarLine(1_150));
            AddUntilStep("wait for both native stages", () =>
                renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture exact scene runtime", () =>
                sceneRuntime = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for one author host per stage", () =>
            {
                hosts = renderer.Drawable.Playfield.Stages
                                .SelectMany(stage => stage.ChildrenOfType<ManiaGameplaySkinLaneCoverHost>())
                                .ToArray();
                return hosts.Length == 2
                       && hosts.All(host => host.LoadState >= LoadState.Ready && host.SceneVisuals.Count == 2);
            });
            AddUntilStep("wait for exact half-cover geometry", () =>
                hosts.All(host => Precision.AlmostEquals(host.Cover.GameplaySkinSceneCoverageHeight, 0.5f, 0.01f)));
            AddStep("assert exact stage-local author routing", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinLayoutGroup[] groups = publication.Snapshot.GroupsInLogicalOrder.ToArray();

                Assert.That(groups, Has.Length.EqualTo(2));
                Assert.That(hosts, Has.Length.EqualTo(groups.Length));

                for (int i = 0; i < groups.Length; i++)
                {
                    ManiaGameplaySkinLaneCoverHost host = hosts.Single(candidateHost =>
                        candidateHost.SceneVisuals.All(visual => visual.Key.Target.GroupId?.Equals(groups[i].GroupId) == true));
                    GameplaySkinSpecialisedSceneVisual fill = host.SceneVisuals.Single(visual =>
                        ReferenceEquals(visual.Key.Slot, GameplaySkinSlotCatalog.LaneCoverFill));
                    GameplaySkinSpecialisedSceneVisual decoration = host.SceneVisuals.Single(visual =>
                        ReferenceEquals(visual.Key.Slot, GameplaySkinSlotCatalog.LaneCoverDecoration));
                    var fillKey = new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.LaneCoverFill,
                        GameplaySkinResolvedMaterialTarget.ForStage(groups[i].TopologyGroup));
                    var decorationKey = new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.LaneCoverDecoration,
                        GameplaySkinResolvedMaterialTarget.ForStage(groups[i].TopologyGroup));

                    Assert.Multiple(() =>
                    {
                        Assert.That(host.Cover.GameplaySkinSceneScale, Is.EqualTo(osuTK.Vector2.One));
                        Assert.That(host.Cover.GameplaySkinSceneRotation,
                            Is.EqualTo(publication.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up ? 0 : 180));
                        Assert.That(fill.IsApplied, Is.True);
                        Assert.That(decoration.IsApplied, Is.True);
                        Assert.That(fill.Parent, Is.SameAs(host.Cover.GameplaySkinFillSceneOwner));
                        Assert.That(decoration.Parent, Is.SameAs(host.Cover.GameplaySkinDecorationSceneOwner));
                        Assert.That(fill.RuntimeNodes.Select(node => node.PreparedNode.Source.Id),
                            Is.EqualTo(new[] { $"node.stage-{i + 1}-fill" }));
                        Assert.That(decoration.RuntimeNodes.Select(node => node.PreparedNode.Source.Id),
                            Is.EqualTo(new[] { $"node.stage-{i + 1}-decoration" }));
                        Assert.That(fill.Key, Is.EqualTo(fillKey));
                        Assert.That(decoration.Key, Is.EqualTo(decorationKey));
                        Assert.That(fill.Key.Target.GroupLogicalIndex, Is.EqualTo(i));
                        Assert.That(fill.Key.Target.GroupVisualIndex, Is.EqualTo(i));
                        Assert.That(sceneRuntime.TryGetVisualGate(fillKey, out GameplaySkinSceneHostedSlot? fillGate), Is.True);
                        Assert.That(fillGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(fillGate.IsReplacementReady, Is.True);
                        Assert.That(sceneRuntime.TryGetVisualGate(decorationKey, out GameplaySkinSceneHostedSlot? decorationGate), Is.True);
                        Assert.That(decorationGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(decorationGate.IsReplacementReady, Is.True);
                        Assert.That(sceneRuntime.TryGetHostedDrawable(fillKey, out _), Is.False);
                        Assert.That(sceneRuntime.TryGetHostedDrawable(decorationKey, out _), Is.False);
                    });
                }

                foreach (Stage stage in renderer.Drawable.Playfield.Stages)
                {
                    ManiaGameplaySkinLaneCoverHost stageHost = stage.ChildrenOfType<ManiaGameplaySkinLaneCoverHost>().Single();

                    foreach (Column column in stage.Columns)
                    {
                        PlayfieldCoveringWrapper? nativeMask = findCoverAncestor(column.HitObjectContainer);
                        Assert.That(nativeMask, Is.Not.Null);
                        Assert.That(nativeMask, Is.Not.SameAs(stageHost.Cover));
                        Assert.That(nativeMask!.Alpha, Is.GreaterThan(0));
                        Assert.That(column.HitObjectContainer.Alpha, Is.GreaterThan(0));
                        Assert.That(isDescendantOf(column.HitObjectContainer, nativeMask), Is.True);
                    }
                }
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSuppressedDecorationAndZeroCoverageCannotDisableNativeContentMask(bool useFadeIn)
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneRuntime = null!;
            ManiaModHidden mod = null!;
            ManiaGameplaySkinLaneCoverHost host = null!;

            AddStep("create isolated suppressed lane-cover skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select suppressed-decoration scene package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-lane-cover-suppress-{Guid.NewGuid():N}");
                writeManiaLaneCoverScenePackage(packageRoot, dualStage: false, suppressLastStageDecoration: true);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact suppressed lane-cover revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("construct exact single-stage renderer with real hidden mod", () =>
            {
                mod = useFadeIn ? new ManiaModFadeIn() : new ManiaModHidden();
                renderer = new CurrentRevisionManiaMaterialHost(
                    publicMaterialSkinManager!,
                    mods: new Mod[] { mod });
            });
            AddStep("mount exact single-stage mania renderer", () => Add(renderer));
            AddUntilStep("wait for exact single-stage ruleset", () =>
                renderer.Drawable.IsLoaded && renderer.Drawable.Playfield.Stages.Count == 1);
            AddStep("activate native single stage", () => renderer.AddProductionObjects(1_100));
            AddUntilStep("wait for native single stage", () =>
                renderer.Drawable.Playfield.Stages.Single().LoadState >= LoadState.Ready);
            AddStep("capture exact scene runtime", () =>
                sceneRuntime = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for native lane-cover host", () =>
            {
                host = renderer.Drawable.Playfield.Stages.Single()
                               .ChildrenOfType<ManiaGameplaySkinLaneCoverHost>()
                               .SingleOrDefault()!;
                return host?.LoadState >= LoadState.Ready && host.SceneVisuals.Count == 1;
            });
            AddStep("enter a real hidden-mod break", () =>
            {
                getPrivateField<Bindable<bool>>(mod, "isBreakTime").Value = true;
                mod.Update(renderer.Drawable.Playfield);
            });
            AddUntilStep("real hidden mod publishes zero coverage", () =>
                Math.Abs(mod.Coverage.Value) < 0.001f);
            AddUntilStep("zero coverage clips all authored pixels", () =>
                Math.Abs(host.Cover.GameplaySkinSceneCoverageHeight) < 0.001f);
            AddStep("assert suppress affects decoration only", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder.Single();
                var fillKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneCoverFill,
                    GameplaySkinResolvedMaterialTarget.ForStage(group));
                var decorationKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneCoverDecoration,
                    GameplaySkinResolvedMaterialTarget.ForStage(group));

                Assert.Multiple(() =>
                {
                    Assert.That(host.SceneVisuals.Single().Key, Is.EqualTo(fillKey));
                    Assert.That(host.Cover.GameplaySkinSceneScale,
                        Is.EqualTo(useFadeIn ? osuTK.Vector2.One : new osuTK.Vector2(1, -1)));
                    Assert.That(GameplaySkinSlotCatalog.LaneCoverFill.SuppressEligibility,
                        Is.EqualTo(GameplaySkinSlotSuppressEligibility.Forbidden));
                    Assert.That(sceneRuntime.TryGetVisualGate(fillKey, out GameplaySkinSceneHostedSlot? fillGate), Is.True);
                    Assert.That(fillGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(fillGate.IsReplacementReady, Is.True);
                    Assert.That(sceneRuntime.TryGetVisualGate(decorationKey, out GameplaySkinSceneHostedSlot? decorationGate), Is.True);
                    Assert.That(decorationGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(decorationGate.IsReplacementReady, Is.True);
                    Assert.That(host.Cover.GameplaySkinDecorationSceneOwner.Count, Is.Zero);
                });

                foreach (Column column in renderer.Drawable.Playfield.Stages.Single().Columns)
                {
                    PlayfieldCoveringWrapper? nativeMask = findCoverAncestor(column.HitObjectContainer);
                    Assert.That(nativeMask, Is.Not.Null);
                    Assert.That(nativeMask, Is.Not.SameAs(host.Cover));
                    Assert.That(nativeMask!.Alpha, Is.GreaterThan(0));
                    Assert.That(column.HitObjectContainer.Alpha, Is.GreaterThan(0));
                }
            });
            AddStep("leave the real hidden-mod break", () =>
            {
                getPrivateField<Bindable<bool>>(mod, "isBreakTime").Value = false;
                mod.Update(renderer.Drawable.Playfield);
            });
            AddUntilStep("real hidden mod publishes non-zero coverage", () =>
                mod.Coverage.Value > 0.01f);
            AddUntilStep("author and every native mask share non-zero coverage", () =>
            {
                if (host.Cover.GameplaySkinSceneCoverageHeight <= 0.01f)
                    return false;

                float authorCoverage = host.Cover.GameplaySkinSceneCoverageHeight * host.Cover.LayoutSize.Y;

                return renderer.Drawable.Playfield.Stages.Single().Columns.All(column =>
                {
                    PlayfieldCoveringWrapper? nativeMask = findCoverAncestor(column.HitObjectContainer);
                    return nativeMask != null
                           && nativeMask.GameplaySkinSceneCoverageHeight > 0.01f
                           && Precision.AlmostEquals(
                               nativeMask.GameplaySkinSceneCoverageHeight * nativeMask.LayoutSize.Y,
                               authorCoverage,
                               0.01f);
                });
            });
            AddStep("return to a real hidden-mod break", () =>
            {
                getPrivateField<Bindable<bool>>(mod, "isBreakTime").Value = true;
                mod.Update(renderer.Drawable.Playfield);
            });
            AddUntilStep("author and content masks close together", () =>
                host.Cover.GameplaySkinSceneCoverageHeight < 0.01f
                && renderer.Drawable.Playfield.Stages.Single().Columns.All(column =>
                    findCoverAncestor(column.HitObjectContainer)?.GameplaySkinSceneCoverageHeight < 0.01f));
        }

        private static PlayfieldCoveringWrapper? findCoverAncestor(Drawable drawable)
        {
            Drawable? current = drawable.Parent;

            while (current != null)
            {
                if (current is PlayfieldCoveringWrapper cover)
                    return cover;

                current = current.Parent;
            }

            return null;
        }

        private static void writeManiaLaneCoverScenePackage(
            string packageRoot,
            bool dualStage,
            bool suppressLastStageDecoration)
        {
            string publicResources = Path.Combine(packageRoot, "public");
            Directory.CreateDirectory(publicResources);
            int stageCount = dualStage ? 2 : 1;
            string stageMode = dualStage ? "dual" : "single";
            var ini = new StringBuilder()
                      .AppendLine("[General]")
                      .AppendLine("Name: mania native lane-cover scene production")
                      .AppendLine("Author: OMS tests")
                      .AppendLine("Version: 2.7")
                      .AppendLine()
                      .AppendLine("[Mania]")
                      .AppendLine($"Keys: {(dualStage ? 9 : 4)}")
                      .AppendLine()
                      .AppendLine("[GameplaySkin.Common:1]");
            var sceneNodes = new List<string>();

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                string groupId = $"mania.group.stage-{stageIndex + 1}";
                bool suppressDecoration = suppressLastStageDecoration && stageIndex == stageCount - 1;
                ini.AppendLine(
                    $"Target: Stage ruleset=mania keymode=any stage-mode={stageMode} group={groupId} group-logical={stageIndex} group-visual={stageIndex}");
                ini.AppendLine("playfield.lane-cover.fill: resource Provide \"public/fill\"");
                ini.AppendLine(suppressDecoration
                    ? "playfield.lane-cover.decoration: resource Suppress"
                    : "playfield.lane-cover.decoration: resource Provide \"public/decoration\"");
                sceneNodes.Add(sceneNode(stageIndex, "fill", "playfield.lane-cover.fill", "texture.fill"));

                if (!suppressDecoration)
                    sceneNodes.Add(sceneNode(stageIndex, "decoration", "playfield.lane-cover.decoration", "texture.decoration"));
            }

            File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), ini.ToString());
            File.WriteAllBytes(Path.Combine(publicResources, "fill.png"), createPublicMaterialPng(16, 16, new Rgba32(30, 45, 70, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "decoration.png"), createPublicMaterialPng(16, 4, new Rgba32(210, 170, 55, 255)));
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v1",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": [
                    { "id": "texture.fill", "type": "texture", "path": "public/fill.png" },
                    { "id": "texture.decoration", "type": "texture", "path": "public/decoration.png" }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                $$"""
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.lane-cover-root",
                    "type": "container",
                    "target": { "kind": "global" },
                    "blend": "inherit",
                    "properties": {},
                    "effects": [],
                    "children": [
                      {{string.Join(",\n      ", sceneNodes)}}
                    ]
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);

            static string sceneNode(int stageIndex, string suffix, string slot, string resource)
            {
                string groupId = $"mania.group.stage-{stageIndex + 1}";
                return $$"""
                {
                  "id": "node.stage-{{stageIndex + 1}}-{{suffix}}",
                  "type": "sprite",
                  "target": { "kind": "stage", "id": "{{groupId}}", "index": {{stageIndex}} },
                  "slot": "{{slot}}",
                  "resource": "{{resource}}",
                  "blend": "alpha",
                  "properties": { "opacity": 1.0, "visible": true },
                  "effects": [],
                  "children": []
                }
                """;
            }
        }
    }
}
