// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneMaterialTemplateTest
    {
        [TestCase("playfield.key")]
        [TestCase("effect.hit-explosion")]
        public void TestLaneMaterialSelectorRoundTripsWithoutAnExplicitLane(string slot)
        {
            JObject scene = sceneJson(slot);
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(diagnostic => diagnostic.Code)));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> second = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(decoded.Value!), manifest);
            Assert.Multiple(() =>
            {
                Assert.That(second.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                Assert.That(second.Value!.Instances.Single().Target, Is.Null);
                Assert.That(second.Value.Instances.Single().MaterialSlot, Is.EqualTo(slot));
                Assert.That(second.Value.Templates.Single().Root.Target.Kind, Is.EqualTo(GameplaySkinSceneTargetKind.Global));
            });

            scene["instances"]![0]!["material"]!["slot"] = slot == "playfield.key" ? "effect.hit-explosion" : "playfield.key";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid),
                "A material selector still cannot claim a template owned by a different slot.");
        }

        [TestCase(true, "v1", "notes/lane", 1)]
        [TestCase(false, "v1", "notes/lane", 0)]
        [TestCase(true, "different", "notes/lane", 0)]
        [TestCase(true, "v1", "notes/other", 0)]
        public void TestLaneTemplateUsesOnlyExactSelectedDocumentMaterial(bool selectedDocument, string revision, string resource, int expected)
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication baseline = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            GameplaySkinLaneTopologySnapshot topology = baseline.Snapshot.Context.Topology;
            GameplaySkinResolvedMaterialTarget lane = GameplaySkinResolvedMaterialTarget.ForLane(topology.GroupsInLogicalOrder.Single(), topology.LanesInLogicalOrder.Single());
            GameplaySkinResolvedMaterialSourceIdentity identity = selectedDocument
                ? GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument("selected", "v1")
                : GameplaySkinResolvedMaterialSourceIdentity.Create(GameplaySkinResolvedMaterialSourceKind.CanonicalPackage, "canonical", "v1");
            var entry = GameplaySkinResolvedMaterialEntry.Provide(GameplaySkinSlotCatalog.KeyVisual, lane, identity,
                GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.KeyVisual, "notes/lane", texture));
            GameplaySkinResolvedMaterialSet materials = GameplaySkinResolvedMaterialSet.Create(baseline.Snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(baseline.Snapshot), new[] { entry });
            var selector = new GameplaySkinSceneInstance("instance.lane", "template.lane", "playfield.key", resource);
            GameplaySkinDocument document = GameplaySkinSceneRuntimeHostTest.CreateMaterialTemplateDocument(materials, revision);
            GameplaySkinResolvedMaterialEntry[] matches = GameplaySkinScenePreparer.ResolveMaterialTemplateEntries(selector, materials, document).ToArray();
            Assert.That(matches, Has.Length.EqualTo(expected));
            if (expected > 0)
                Assert.That(matches.Single().Target, Is.EqualTo(lane));

            GameplaySkinResolvedMaterialSet absent = GameplaySkinResolvedMaterialSet.CreateEmpty(baseline.Snapshot);
            Assert.That(GameplaySkinScenePreparer.ResolveMaterialTemplateEntries(selector, absent, document), Is.Empty,
                "A mode with no matching INI material must not expand or resolve the template's placeholder target.");
        }

        [TestCase("Inherit")]
        [TestCase("Suppress")]
        [TestCase("Provide \"\"")]
        public void TestAWinningPayloadCannotBypassTheExactDeclarationState(string operation)
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication baseline = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            GameplaySkinLaneTopologySnapshot topology = baseline.Snapshot.Context.Topology;
            GameplaySkinResolvedMaterialTarget lane = GameplaySkinResolvedMaterialTarget.ForLane(topology.GroupsInLogicalOrder.Single(), topology.LanesInLogicalOrder.Single());
            var entry = GameplaySkinResolvedMaterialEntry.Provide(GameplaySkinSlotCatalog.KeyVisual, lane,
                GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument("selected", "v1"),
                GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.KeyVisual, "notes/lane", texture));
            GameplaySkinResolvedMaterialSet materials = GameplaySkinResolvedMaterialSet.Create(baseline.Snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(baseline.Snapshot), new[] { entry });
            GameplaySkinDocument valid = GameplaySkinSceneRuntimeHostTest.CreateMaterialTemplateDocument(materials, "v1");
            string changed = GameplaySkinDocumentCodec.Encode(valid).Replace("Provide \"notes/lane\"", operation, StringComparison.Ordinal);
            GameplaySkinDocument unavailable = GameplaySkinDocumentCodec.Decode(changed,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("v1")).BindToPublication(baseline.Snapshot);
            var selector = new GameplaySkinSceneInstance("instance.lane", "template.lane", "playfield.key", "notes/lane");
            Assert.That(GameplaySkinScenePreparer.ResolveMaterialTemplateEntries(selector, materials, unavailable), Is.Empty,
                "A resource name on a final payload cannot promote Inherit, Suppress or an invalid declaration into a scene owner.");
        }

        private static JObject sceneJson(string slot)
            => JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1",
                 "root":{"id":"root","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]},
                 "templates":[{"id":"template.lane","root":{"id":"template.owner","type":"container","target":{"kind":"global"},"slot":"playfield.key","blend":"alpha","properties":{},"effects":[],"children":[]}}],
                 "instances":[{"id":"instance.lane","template":"template.lane","material":{"slot":"playfield.key","resource":"notes/lane"}}],
                 "tracks":[],"stateMachines":[],"bindings":[]}
                """.Replace("playfield.key", slot));
    }
}
