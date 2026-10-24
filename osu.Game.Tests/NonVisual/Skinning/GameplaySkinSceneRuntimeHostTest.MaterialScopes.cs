// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(GameplaySkinSlotScope.Group)]
        [TestCase(GameplaySkinSlotScope.Stage)]
        [TestCase(GameplaySkinSlotScope.Lane)]
        public void TestMaterialTemplatePreparationRetainsExactScope(GameplaySkinSlotScope scope)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            DualLayoutFixture layout = createDualStageLayout();
            GameplaySkinLaneTopologyGroup group = layout.Snapshot.Context.Topology.GroupsInLogicalOrder[1];
            GameplaySkinLaneTopologyEntry lane = group.LanesInLogicalOrder.Single();
            GameplaySkinSlotDescriptor slot = scope switch
            {
                GameplaySkinSlotScope.Group => GameplaySkinSlotCatalog.BarLine,
                GameplaySkinSlotScope.Stage => GameplaySkinSlotCatalog.JudgementDisplay,
                GameplaySkinSlotScope.Lane => GameplaySkinSlotCatalog.KeyVisual,
                _ => throw new InvalidOperationException(),
            };
            GameplaySkinResolvedMaterialTarget materialTarget = scope switch
            {
                GameplaySkinSlotScope.Group => GameplaySkinResolvedMaterialTarget.ForGroup(group),
                GameplaySkinSlotScope.Stage => GameplaySkinResolvedMaterialTarget.ForStage(group),
                GameplaySkinSlotScope.Lane => GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                _ => throw new InvalidOperationException(),
            };
            GameplaySkinSceneTargetKind sceneKind = scope switch
            {
                GameplaySkinSlotScope.Group => GameplaySkinSceneTargetKind.Group,
                GameplaySkinSlotScope.Stage => GameplaySkinSceneTargetKind.Stage,
                GameplaySkinSlotScope.Lane => GameplaySkinSceneTargetKind.Lane,
                _ => throw new InvalidOperationException(),
            };
            const string resourceName = "materials/scope";
            var document = new GameplaySkinSceneDocument(
                node("scope.dispatcher", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null,
                    blend: GameplaySkinSceneBlendMode.Inherit),
                Array.Empty<GameplaySkinSceneTrack>(), Array.Empty<GameplaySkinSceneStateMachine>(), Array.Empty<GameplaySkinSceneBinding>(),
                new[] { new GameplaySkinSceneTemplate("scope.template", node("scope.sprite", GameplaySkinSceneNodeType.Sprite,
                    target(GameplaySkinSceneTargetKind.Global), slot.Id, null)) },
                new[] { new GameplaySkinSceneInstance("scope.instance", "scope.template", slot.Id, resourceName) });
            string coordinates = $"group={group.Identity.Id.Value} group-logical={group.LogicalIndex} group-visual={group.VisualIndex}";
            if (scope == GameplaySkinSlotScope.Lane)
                coordinates += $" lane={lane.Identity.Id.Value} global-logical={lane.GlobalLogicalIndex} global-visual={lane.GlobalVisualIndex}"
                    + $" group-local-logical={lane.GroupLocalLogicalIndex} group-local-visual={lane.GroupLocalVisualIndex}";
            var files = new Dictionary<string, byte[]>
            {
                ["skin.ini"] = Encoding.UTF8.GetBytes($"[GameplaySkin.Common:1]\nTarget: {scope} ruleset=any keymode=any stage-mode=any {coordinates}\n{slot.Id}: resource Provide \"{resourceName}\"\n"),
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
            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(context, layout.Snapshot.GroupsInLogicalOrder,
                layout.Snapshot.LanesInLogicalOrder, layout.Snapshot.Surfaces);
            GameplaySkinResolvedMaterialSourceIdentity selected = GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument(
                skin.SkinInfo.ID.ToString("D"), skin.GameplaySkinDocument.Identity.ContentRevision);
            GameplaySkinResolvedMaterialEntry entry = GameplaySkinResolvedMaterialEntry.Provide(slot, materialTarget, selected,
                GameplaySkinPublicSlotMaterial.FromPreparedResource(slot, resourceName, texture));
            GameplaySkinResolvedMaterialSet materials = GameplaySkinResolvedMaterialSet.Create(snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(snapshot), new[] { entry });
            var source = new Mock<ISkinSource>();
            source.SetupGet(value => value.AllSources).Returns(new ISkin[] { skin });

            Assert.That(skin.GameplaySkinDocument.Diagnostics, Is.Empty);
            GameplaySkinDocument bound = skin.GetGameplaySkinDocument(context).BindToPublication(snapshot);
            Assert.That(bound.GetEntry(slot, materialTarget).Operation, Is.EqualTo(GameplaySkinDocumentOperation.Provide));
            Assert.That(entry.Source.IsSelectedDocumentDeclaration, Is.True);

            using GameplaySkinPreparedScene prepared = GameplaySkinScenePreparer.Prepare(snapshot, materials, source.Object);
            GameplaySkinPreparedSceneNode[] instances = prepared.Roots.Where(root => root.Source.Id == "scope.sprite").ToArray();
            Assert.That(instances, Has.Length.EqualTo(1), "An exact selected C4 Provide must instantiate its material template at the original scope.");
            GameplaySkinPreparedSceneNode instance = instances.Single();
            Assert.Multiple(() =>
            {
                Assert.That(instance.ResolvedTarget.Kind, Is.EqualTo(sceneKind));
                Assert.That(instance.ResolvedTarget.StableId, Is.EqualTo(scope == GameplaySkinSlotScope.Lane ? lane.Identity.Id.Value : group.Identity.Id.Value));
                Assert.That(instance.ResolvedTarget.Index, Is.EqualTo(scope == GameplaySkinSlotScope.Lane ? lane.GlobalLogicalIndex : group.LogicalIndex));
                Assert.That(instance.MaterialTarget, Is.EqualTo(materialTarget));
                Assert.That(instance.MaterialTarget!.Scope, Is.EqualTo(scope));
                Assert.That(instance.OwningSlotKey, Is.EqualTo(entry.Key));
                Assert.That(instance.ResolvedTexture, Is.SameAs(texture));
                Assert.That(instance.Source, Is.SameAs(skin.PreparedGameplaySkinPackage!.Document!.Templates.Single().Root));
                Assert.That(materials.Entries.Single().Source, Is.SameAs(selected));
            });
        }

        private sealed class MaterialScopeSkin : Skin
        {
            public MaterialScopeSkin(IResourceStore<byte[]> store)
                : base(new SkinInfo("Material scope fixture") { ID = Guid.NewGuid() }, null, store)
            {
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;
            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;
            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }
    }
}
