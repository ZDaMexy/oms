// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase("bms")]
        [TestCase("mania")]
        public void TestDirectGlobalForegroundChildRetainsCanvasAboveStageJudgement(string ruleset)
            => checkDirectGlobalForeground(ruleset, provideHud: true, mountHud: true);

        [Test]
        public void TestDirectGlobalForegroundRequiresItsSelectedProvide()
            => checkDirectGlobalForeground("mania", provideHud: false, mountHud: true);

        [Test]
        public void TestAbsentDirectGlobalForegroundLeavesLegacyHudUnchanged()
            => checkDirectGlobalForeground("mania", provideHud: false, mountHud: false);

        [Test]
        public void TestStageHudOwnerCannotMountGlobalCanvasChild()
            => checkDirectGlobalForeground("mania", provideHud: true, mountHud: true, stageOwner: true);

        private static void checkDirectGlobalForeground(string ruleset, bool provideHud, bool mountHud, bool stageOwner = false)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            LayoutFixture layout = createLayout(ruleset);
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinSceneTarget stageTarget = target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, layout.Group.LogicalIndex);
            GameplaySkinSceneNode canvas = node("foreground.canvas", GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global), null, null, new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["x"] = GameplaySkinScenePropertyValue.FromNumber(0),
                    ["y"] = GameplaySkinScenePropertyValue.FromNumber(0),
                    ["width"] = GameplaySkinScenePropertyValue.FromNumber(1),
                    ["height"] = GameplaySkinScenePropertyValue.FromNumber(1),
                });
            GameplaySkinSceneNode foreground = withChildren(node("foreground.owner", GameplaySkinSceneNodeType.Container,
                stageOwner ? stageTarget : target(GameplaySkinSceneTargetKind.Global), GameplaySkinSlotCatalog.TextHud.Id, null,
                new Dictionary<string, GameplaySkinScenePropertyValue> { ["z"] = GameplaySkinScenePropertyValue.FromNumber(-100) }), canvas);
            GameplaySkinSceneNode judgement = node("foreground.judgement", GameplaySkinSceneNodeType.Container,
                stageTarget, GameplaySkinSlotCatalog.JudgementDisplay.Id, null);
            GameplaySkinSceneNode dispatcher = withChildren(node("foreground.dispatcher", GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global), null, null, blend: GameplaySkinSceneBlendMode.Inherit),
                mountHud ? new[] { foreground, judgement } : new[] { judgement });
            var document = new GameplaySkinSceneDocument(dispatcher, Array.Empty<GameplaySkinSceneTrack>(),
                Array.Empty<GameplaySkinSceneStateMachine>(), Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>());
            string stageCoordinates = $"Stage ruleset=any keymode=any stage-mode=any group={layout.Group.Identity.Id.Value} group-logical=0 group-visual=0";
            string hudDeclaration = provideHud
                ? $"[GameplaySkin.Common:1]\nTarget: {(stageOwner ? stageCoordinates : "Global ruleset=any keymode=any stage-mode=any")}\n"
                  + $"{GameplaySkinSlotCatalog.TextHud.Id}: resource Provide \"materials/foreground\"\n"
                : string.Empty;
            var files = new Dictionary<string, byte[]>
            {
                ["skin.ini"] = Encoding.UTF8.GetBytes(hudDeclaration + $"[GameplaySkin.Common:1]\nTarget: {stageCoordinates}\n"
                    + $"{GameplaySkinSlotCatalog.JudgementDisplay.Id}: resource Provide \"materials/judgement\"\n"),
                [GameplaySkinSceneContracts.MANIFEST_FILE_NAME] = GameplaySkinSceneCodec.EncodeManifestUtf8(
                    new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>())),
                [GameplaySkinSceneContracts.SCENE_FILE_NAME] = GameplaySkinSceneCodec.EncodeSceneUtf8(document),
            };
            var store = new Mock<IResourceStore<byte[]>>();
            store.Setup(value => value.Get(It.IsAny<string>())).Returns((string name) => files.GetValueOrDefault(name)!);
            store.Setup(value => value.GetStream(It.IsAny<string>())).Returns((string name) =>
                files.TryGetValue(name, out byte[]? bytes) ? new MemoryStream(bytes, writable: false) : null!);
            using var skin = new MaterialScopeSkin(store.Object);
            var package = GameplaySkinPackageRevision.Create(new SkinCurrentRevision(7, skin.SkinInfo.ID, skin.GetCurrentRevisionContentIdentity(),
                SkinCurrentRevisionSourceKind.ManagedFolder, skin, false, _ => { }));
            GameplaySkinLayoutContext baseline = layout.Snapshot.Context;
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(baseline.RulesetId, baseline.NativeContextId, baseline.KeymodeId,
                baseline.PresentationStyleId, baseline.Topology, baseline.ScreenBounds, baseline.SafeBounds, baseline.AspectRatio, baseline.DpiScale,
                baseline.ScrollDirection, package, baseline.TopologyRevision, baseline.LayoutRevision);
            var footer = GameplaySkinLayoutRect.Create(0, 0.92f, 1, 0.08f);
            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(context, layout.Snapshot.GroupsInLogicalOrder,
                layout.Snapshot.LanesInLogicalOrder, layout.Snapshot.Surfaces.Select(surface => surface.Id == ruleset + ".hud"
                    ? new GameplaySkinLayoutSurface(surface.Id, footer, 60, false, false)
                    : surface));
            GameplaySkinResolvedMaterialSourceIdentity selected = GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument(
                skin.SkinInfo.ID.ToString("D"), skin.GameplaySkinDocument.Identity.ContentRevision);
            var entries = new List<GameplaySkinResolvedMaterialEntry>
            {
                GameplaySkinResolvedMaterialEntry.Provide(GameplaySkinSlotCatalog.JudgementDisplay, stage, selected,
                    GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.JudgementDisplay, "materials/judgement", texture)),
            };
            if (provideHud)
                entries.Add(GameplaySkinResolvedMaterialEntry.Provide(GameplaySkinSlotCatalog.TextHud, stageOwner ? stage : global, selected,
                    GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.TextHud, "materials/foreground", texture)));
            GameplaySkinResolvedMaterialSet materials = materialSetFor(snapshot, entries);
            var source = new Mock<ISkinSource>();
            source.SetupGet(value => value.AllSources).Returns(new ISkin[] { skin });
            Assert.That(skin.GameplaySkinDocument.Diagnostics, Is.Empty);

            if (mountHud && (!provideHud || stageOwner))
            {
                GameplaySkinScenePreparationException exception = Assert.Throws<GameplaySkinScenePreparationException>(
                    () => GameplaySkinScenePreparer.Prepare(snapshot, materials, source.Object))!;
                Assert.That(exception.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidReference));
                return;
            }

            GameplaySkinPreparedScene prepared = GameplaySkinScenePreparer.Prepare(snapshot, materials, source.Object);
            using GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(snapshot), materials, prepared);
            using var stream = createStream(publication);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("foreground.judgement", out GameplaySkinSceneRuntimeNode? judgementRuntime), Is.True);
            // Before a non-visual container loads, its pending children do not yet
            // expose Parent. Its child collection still records the actual mount.
            Assert.That(host.Layers.HudForeground.Children, Does.Contain(judgementRuntime!.RootDrawable));
            Assert.That(judgementRuntime.PreparedNode.Layer, Is.EqualTo(GameplaySkinSceneLayer.HudForeground));
            Assert.That(host.TryGetRuntimeNode("foreground.owner", out GameplaySkinSceneRuntimeNode? ownerRuntime), Is.EqualTo(mountHud));
            Assert.That(host.TryGetRuntimeNode("foreground.canvas", out GameplaySkinSceneRuntimeNode? canvasRuntime), Is.EqualTo(mountHud));
            if (mountHud)
            {
                GameplaySkinPreparedSceneNode owner = prepared.Roots.Single().Children.Single(child => child.Source.Id == "foreground.owner");
                GameplaySkinPreparedSceneNode child = owner.Children.Single();
                GameplaySkinSceneRuntimeNode ownerNode = ownerRuntime!;
                GameplaySkinSceneRuntimeNode canvasNode = canvasRuntime!;
                Assert.Multiple(() =>
                {
                    Assert.That(owner.Rect, Is.EqualTo(footer), "The public hud.text surface remains the native footer.");
                    Assert.That(child.Rect, Is.EqualTo(context.SafeBounds), "An explicit Global child retains the frozen canvas.");
                    Assert.That(owner.Layer, Is.EqualTo(GameplaySkinSceneLayer.HudForeground));
                    Assert.That(child.Layer, Is.EqualTo(GameplaySkinSceneLayer.HudForeground));
                    Assert.That(child.OwningSlotKey, Is.EqualTo(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global)));
                    Assert.That(host.Layers.HudForeground.Children, Does.Contain(ownerNode.RootDrawable));
                    Assert.That(ownerNode.RootDrawable.Depth, Is.LessThan(judgementRuntime.RootDrawable.Depth));
                    Assert.That(ownerNode.TransformDrawable.Children, Does.Contain(canvasNode.RootDrawable));
                    Assert.That(((Container)ownerNode.RootDrawable).Masking, Is.False);
                    Assert.That(ownerNode.TransformDrawable.Masking, Is.False);
                    Assert.That(footer.X + footer.Width * canvasNode.RootDrawable.X, Is.EqualTo(context.SafeBounds.X).Within(0.00001));
                    Assert.That(footer.Y + footer.Height * canvasNode.RootDrawable.Y, Is.EqualTo(context.SafeBounds.Y).Within(0.00001));
                    Assert.That(footer.Width * canvasNode.RootDrawable.Width, Is.EqualTo(context.SafeBounds.Width).Within(0.00001));
                    Assert.That(footer.Height * canvasNode.RootDrawable.Height, Is.EqualTo(context.SafeBounds.Height).Within(0.00001));
                });
            }
            Assert.That(host.RuntimeFaults, Is.Empty);
        }
    }
}
