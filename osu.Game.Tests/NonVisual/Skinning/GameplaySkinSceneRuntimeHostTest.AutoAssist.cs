// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(GameplaySkinObjectAutoAssist.None)]
        [TestCase(GameplaySkinObjectAutoAssist.Scratch)]
        [TestCase(GameplaySkinObjectAutoAssist.Note)]
        public void TestAutoAssistSurvivesCompactDeliveryProgressAndCompleteSnapshot(GameplaySkinObjectAutoAssist assist)
        {
            var group = GameplaySkinLaneGroupId.Create("assist.group");
            var lane = GameplaySkinLaneId.Create("assist.lane");
            var hold = new GameplaySkinObjectStateSnapshot(17, GameplaySkinObjectKind.LongNote, GameplaySkinObjectState.Holding,
                group, lane, 100, 300, 0.25, GameplaySkinLongNoteMode.HellCharge, assist);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, hold);
            Assert.That(compact.GetObject(group, lane).AutoAssist, Is.EqualTo(assist));
            Assert.That(((GameplaySkinObjectEventPayload)compact.Materialize(group, lane)).State.AutoAssist, Is.EqualTo(assist));
            GameplaySkinObjectStateSnapshot progressed = GameplaySkinEventRuntimeHost.SnapshotObjectAt(hold, 200);
            Assert.That(progressed.Progress, Is.EqualTo(0.5));
            Assert.That(progressed.AutoAssist, Is.EqualTo(assist));

            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, false);
            Assert.That(projectionState(publication, new[] { hold }).ActiveObjects.Single().AutoAssist, Is.EqualTo(assist));
        }

        [Test]
        public void TestAutoAssistRejectsUnknownValuesAndNonNoteObjects()
        {
            var group = GameplaySkinLaneGroupId.Create("assist.group");
            var lane = GameplaySkinLaneId.Create("assist.lane");
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinObjectStateSnapshot(1, GameplaySkinObjectKind.Note,
                GameplaySkinObjectState.Visible, group, lane, 100, 100, 0, autoAssist: (GameplaySkinObjectAutoAssist)99));
            foreach (GameplaySkinObjectKind kind in new[] { GameplaySkinObjectKind.Mine, GameplaySkinObjectKind.BarLine })
                Assert.Throws<ArgumentException>(() => new GameplaySkinObjectStateSnapshot(1, kind, GameplaySkinObjectState.Visible,
                    group, lane, 100, 100, 0, autoAssist: GameplaySkinObjectAutoAssist.Scratch));
        }

        [Test]
        public void TestAutoAssistCannotChangeBeforeAnObjectIsRetired()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, false);
            GameplaySkinObjectStateSnapshot original = autoAssistObject(publication, 17, GameplaySkinObjectAutoAssist.Scratch);
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[] { original }));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            foreach (GameplaySkinEventKind kind in new[] { GameplaySkinEventKind.ObjectStateChanged, GameplaySkinEventKind.ObjectDespawned })
            {
                GameplaySkinObjectStateSnapshot changed = autoAssistObject(publication, 17, GameplaySkinObjectAutoAssist.Note,
                    kind == GameplaySkinEventKind.ObjectDespawned ? GameplaySkinObjectState.Despawned : GameplaySkinObjectState.Visible);
                Assert.Throws<InvalidOperationException>(() => stream.Publish(producer, 150, GameplaySkinEventValue.Object(kind, changed),
                    original.GroupId, original.LaneId));
            }
        }

        [Test]
        public void TestAutoAssistLateAttachBackpressureAndEpochReplacementKeepTheCompleteObject()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, false);
            GameplaySkinObjectStateSnapshot original = autoAssistObject(publication, 17, GameplaySkinObjectAutoAssist.Scratch);
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[] { original }), pendingEventBudget: 1);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription blocked = stream.Subscribe();
            GameplaySkinObjectStateSnapshot next = autoAssistObject(publication, 18, GameplaySkinObjectAutoAssist.Note);
            try
            {
                Assert.Throws<GameplaySkinEventBackpressureException>(() => stream.Publish(producer, 150,
                    GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, next), next.GroupId, next.LaneId));
                using (GameplaySkinEventSubscription late = stream.Subscribe())
                    assertSnapshotAssist(late, 17, GameplaySkinObjectAutoAssist.Scratch);

                assertSnapshotAssist(blocked, 17, GameplaySkinObjectAutoAssist.Scratch);
                stream.Publish(producer, 150, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, next), next.GroupId, next.LaneId);
                var delivered = new List<GameplaySkinEventEnvelope>();
                blocked.DrainFrame(delivered.Add);
                Assert.That(((GameplaySkinObjectEventPayload)delivered.Single().Payload).State.AutoAssist, Is.EqualTo(GameplaySkinObjectAutoAssist.Note));

                foreach (GameplaySkinEventResetReason reason in new[]
                         {
                             GameplaySkinEventResetReason.Seek, GameplaySkinEventResetReason.Rewind, GameplaySkinEventResetReason.Retry,
                         })
                {
                    producer = producer.Reset(150, projectionState(publication, new[] { next }), reason);
                    assertSnapshotAssist(blocked, 18, GameplaySkinObjectAutoAssist.Note);
                    using GameplaySkinEventSubscription late = stream.Subscribe();
                    assertSnapshotAssist(late, 18, GameplaySkinObjectAutoAssist.Note);
                }
            }
            finally
            {
                producer.Dispose();
            }

            static void assertSnapshotAssist(GameplaySkinEventSubscription subscription, long id, GameplaySkinObjectAutoAssist expected)
            {
                var delivered = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(delivered.Add);
                var state = (GameplaySkinStateEventPayload)delivered.Single().Payload;
                Assert.That(state.State.ActiveObjects.Single().ObjectId, Is.EqualTo(id));
                Assert.That(state.State.ActiveObjects.Single().AutoAssist, Is.EqualTo(expected));
            }
        }

        [Test]
        public void TestAutoAssistResourceAndTextVariantsStayWithEachExactPooledObjectAndResetOnReuse()
        {
            using Texture normal = new DummyRenderer().CreateTexture(8, 8);
            using Texture assisted = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = autoAssistFixture(normal, assisted);
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinResolvedMaterialKey key = publication.MaterialSet.Entries.Single().Key;
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(key, new Container())!;
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(key, new Container())!;
            first.OnApply(101);
            second.OnApply(202);
            try
            {
                GameplaySkinObjectStateSnapshot scratch = autoAssistObject(publication, 101, GameplaySkinObjectAutoAssist.Scratch);
                GameplaySkinObjectStateSnapshot note = autoAssistObject(publication, 202, GameplaySkinObjectAutoAssist.Note);
                publishObject(stream, producer, 150, GameplaySkinEventKind.ObjectSpawned, scratch);
                publishObject(stream, producer, 150, GameplaySkinEventKind.ObjectSpawned, note);
                host.ProcessFrame();
                assertVisual(first, assisted, "scratch");
                assertVisual(second, normal, "note");

                producer = producer.Reset(150, projectionState(publication, new[] { scratch, note }), GameplaySkinEventResetReason.Rewind);
                host.ProcessFrame();
                assertVisual(first, assisted, "scratch");
                assertVisual(second, normal, "note");

                first.OnFree();
                first.OnApply(303);
                host.ProcessFrame();
                assertVisual(first, normal, "none");
                assertVisual(second, normal, "note");
                producer = producer.Reset(0, projectionState(publication), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                assertVisual(first, normal, "none");
                assertVisual(second, normal, "none");
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }

            static void assertVisual(GameplaySkinSpecialisedSceneVisual visual, Texture expected, string text)
            {
                Assert.That(((Sprite)visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "assist.sprite").ContentDrawable).Texture, Is.SameAs(expected));
                Assert.That(((SpriteText)visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "assist.text").ContentDrawable).Text.ToString(), Is.EqualTo(text));
            }
        }

        [Test]
        public void TestAutoAssistGlobalBindingDoesNotBorrowTheFirstActiveObject()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: "object.auto-assist");
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[]
            {
                autoAssistObject(publication, 17, GameplaySkinObjectAutoAssist.Scratch),
            }));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertProjectionText(host, "none");
        }

        [Test]
        public void TestAutoAssistCodecAcceptsOnlyTypedStringsAndRejectsNumericPropertiesAndClocks()
        {
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            JObject scene = pacemakerScene();
            scene["bindings"] = new JArray(new JObject { ["id"] = "assist.text", ["target"] = "next.label", ["property"] = "text", ["source"] = "object.auto-assist" });
            scene["tracks"] = new JArray();
            scene["variants"] = new JArray(new JObject
            {
                ["id"] = "assist.visible", ["target"] = "next.variant", ["property"] = "visible", ["source"] = "object.auto-assist", ["default"] = false,
                ["cases"] = new JArray(new[] { "none", "scratch", "note" }.Select(key => new JObject
                {
                    ["id"] = "assist.case." + key, ["key"] = key, ["value"] = true,
                })),
            });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(decoded.Value!.Bindings.Single().Source, Is.EqualTo(GameplaySkinSceneBindingSource.ObjectAutoAssist));
            Assert.That((int)decoded.Value.Bindings.Single().Source, Is.EqualTo(103));
            Assert.That(GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(decoded.Value), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            scene["variants"]![0]!["cases"]![0]!["key"] = "autoplay";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["variants"] = new JArray();
            scene["bindings"]![0]!["property"] = "rotation";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["bindings"]![0]!["property"] = "text";
            JObject track = (JObject)pacemakerScene()["tracks"]![0]!.DeepClone();
            track["clock"] = "object.auto-assist";
            scene["tracks"] = new JArray(track);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["tracks"] = new JArray();
            scene["root"]!["clock"] = "object.auto-assist";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        private static GameplaySkinObjectStateSnapshot autoAssistObject(GameplaySkinLayoutPublication publication, long id,
                                                                        GameplaySkinObjectAutoAssist assist, GameplaySkinObjectState state = GameplaySkinObjectState.Visible)
        {
            GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder.Single();
            return new GameplaySkinObjectStateSnapshot(id, GameplaySkinObjectKind.Note, state, group.Identity.Id,
                group.LanesInLogicalOrder.First().Identity.Id, 100, 100, 1, autoAssist: assist);
        }

        private static RuntimeFixture autoAssistFixture(Texture normal, Texture assisted)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.Note, laneTarget, normal, source()) });
            var normalResource = new GameplaySkinSceneResource("assist.normal", GameplaySkinSceneResourceType.Texture, "textures/normal.png");
            var assistResource = new GameplaySkinSceneResource("assist.scratch", GameplaySkinSceneResourceType.Texture, "textures/scratch.png");
            var preparedNormal = new GameplaySkinPreparedSceneResource(normalResource, "normal", 8, 8, normal);
            var preparedAssist = new GameplaySkinPreparedSceneResource(assistResource, "scratch", 16, 16, assisted);
            GameplaySkinSceneTarget lane = target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0);
            GameplaySkinSceneNode text = node("assist.text", GameplaySkinSceneNodeType.Text, lane, null, null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    // Reserve the longest bounded variant string for every pooled note, without requesting the
                    // general dynamic-text capacity per clone. This fixture needs only these three short labels.
                    ["text"] = GameplaySkinScenePropertyValue.FromString("scratch"),
                    ["font-size"] = GameplaySkinScenePropertyValue.FromNumber(8),
                });
            GameplaySkinSceneNode sprite = node("assist.sprite", GameplaySkinSceneNodeType.Sprite, lane, null, normalResource.Id);
            GameplaySkinSceneNode root = withChildren(node("assist.root", GameplaySkinSceneNodeType.Container, lane, GameplaySkinSlotCatalog.Note.Id, null), sprite, text);
            var document = new GameplaySkinSceneDocument(root, Array.Empty<GameplaySkinSceneTrack>(), Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(),
                new[]
                {
                    new GameplaySkinSceneVariant("assist.resource", sprite.Id, "object.auto-assist", normalResource.Id,
                        new[] { new GameplaySkinSceneVariantCase("assist.resource.scratch", "scratch", assistResource.Id) }),
                    new GameplaySkinSceneVariant("assist.text.state", text.Id, GameplaySkinSceneProperty.Text, "object.auto-assist",
                        GameplaySkinScenePropertyValue.FromString("none"), new[]
                        {
                            new GameplaySkinSceneVariantCase("assist.text.scratch", "scratch", GameplaySkinScenePropertyValue.FromString("scratch")),
                            new GameplaySkinSceneVariantCase("assist.text.note", "note", GameplaySkinScenePropertyValue.FromString("note")),
                        }),
                }, Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "auto-assist", new GameplaySkinSceneManifest(new[] { normalResource, assistResource }),
                document, new[] { preparedNormal, preparedAssist }, new[]
                {
                    prepared(root, layout.LaneRect, laneTarget, GameplaySkinSlotCatalog.Note, null,
                        prepared(sprite, layout.LaneRect, laneTarget, null, preparedNormal),
                        prepared(text, layout.LaneRect, laneTarget, null, null)),
                });
            return new RuntimeFixture(GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene), layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);
        }
    }
}
