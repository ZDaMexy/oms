// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestTimingDirectionsRemainIndependentForEachExactGroupAndPersistAfterGradeExpiry()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = timingDirectionPublication(texture);
            var groups = publication.Snapshot.Context.Topology.GroupsInLogicalOrder;
            GameplaySkinJudgementTimingStateSnapshot first = default;
            first = first.Apply(GameplaySkinLaneRole.Key, new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Fast, 10));
            first = first.Apply(GameplaySkinLaneRole.Scratch, new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Slow, 20));
            GameplaySkinJudgementTimingStateSnapshot second = default;
            second = second.Apply(GameplaySkinLaneRole.Key, new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Slow, 15));
            GameplaySkinEventStateSnapshot snapshot = timingDirectionSnapshot(publication,
                new GameplaySkinJudgementTimingGroupStateSnapshot(groups[0].Identity.Id, first),
                new GameplaySkinJudgementTimingGroupStateSnapshot(groups[1].Identity.Id, second));
            using var stream = new GameplaySkinEventStream(publication, 30, snapshot);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                assertDirections(host, 0, "slow", "fast", "slow");
                assertDirections(host, 1, "slow", "slow", "none");
                Assert.That(directionText(host, "td.global"), Is.EqualTo("none"), "A global visual cannot borrow either group's input.");

                producer.SynchroniseTiming(6030, snapshot.Timing);
                host.ProcessFrame();
                assertDirections(host, 0, "slow", "fast", "slow");
                assertDirections(host, 1, "slow", "slow", "none");

                using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                late.ProcessFrame();
                assertDirections(late, 0, "slow", "fast", "slow");
                producer = producer.Reset(0, timingDirectionSnapshot(publication), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                late.ProcessFrame();
                assertDirections(host, 0, "none", "none", "none");
                assertDirections(late, 1, "none", "none", "none");
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestTimingDirectionEdgeReplacesCompleteChannelsWithoutReinterpretingItsGradeOrOffset()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = timingDirectionPublication(texture);
            using var stream = new GameplaySkinEventStream(publication, 0, timingDirectionSnapshot(publication));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[0];
            var state = default(GameplaySkinJudgementTimingStateSnapshot).Apply(GameplaySkinLaneRole.Key,
                new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Fast, 25));
            var judgement = new GameplaySkinJudgementStateSnapshot(null, group.Identity.Id, group.LanesInLogicalOrder[0].Identity.Id,
                GameplaySkinJudgementGrade.Miss, 999, 0, false, state);
            stream.Publish(producer, 25, GameplaySkinEventValue.Judgement(judgement), group.Identity.Id, group.LanesInLogicalOrder[0].Identity.Id);
            host.ProcessFrame();
            assertDirections(host, 0, "fast", "fast", "none");
            assertDirections(host, 1, "none", "none", "none");
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase("judgement.timing-direction")]
        [TestCase("judgement.keys-timing-direction")]
        [TestCase("judgement.scratch-timing-direction")]
        public void TestTimingDirectionCodecClosesVariantsAndRejectsNumericClocks(string sourceId)
        {
            JObject scene = pacemakerScene();
            scene["bindings"] = new JArray(new JObject { ["id"] = "td.text", ["target"] = "next.label", ["property"] = "text", ["source"] = sourceId });
            scene["tracks"] = new JArray();
            scene["variants"] = new JArray(new JObject
            {
                ["id"] = "td.visible", ["target"] = "next.variant", ["property"] = "visible", ["source"] = sourceId, ["default"] = false,
                ["cases"] = new JArray(new JObject { ["id"] = "td.fast", ["key"] = "fast", ["value"] = true },
                    new JObject { ["id"] = "td.slow", ["key"] = "slow", ["value"] = true }),
            });
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            var decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(decoded.Value!), manifest).Status,
                Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            scene["variants"]![0]!["cases"]![0]!["key"] = "early";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["variants"] = new JArray();
            JObject clock = (JObject)pacemakerScene()["tracks"]![0]!.DeepClone();
            clock["clock"] = sourceId;
            scene["tracks"] = new JArray(clock);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        private static GameplaySkinEventStateSnapshot timingDirectionSnapshot(GameplaySkinLayoutPublication publication,
                                                                               params GameplaySkinJudgementTimingGroupStateSnapshot[] groups)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects,
                initial.CurrentJudgements, initial.Score, initial.Timing, initial.BgaViewports, judgementTimingGroups: groups);
        }

        private static string directionText(GameplaySkinSceneRuntimeHost host, string id)
            => ((SpriteText)runtime(host, id).ContentDrawable).Text.ToString();

        private static void assertDirections(GameplaySkinSceneRuntimeHost host, int group, string combined, string keys, string scratch)
        {
            Assert.That(directionText(host, $"td.g{group}.combined"), Is.EqualTo(combined));
            Assert.That(directionText(host, $"td.g{group}.keys"), Is.EqualTo(keys));
            Assert.That(directionText(host, $"td.g{group}.scratch"), Is.EqualTo(scratch));
        }

        private static GameplaySkinLayoutPublication timingDirectionPublication(Texture texture)
        {
            DualLayoutFixture layout = createDualStageLayout();
            var global = GameplaySkinResolvedMaterialTarget.Global;
            var materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            JObject scene = pacemakerScene();
            var children = new JArray();
            var bindings = new JArray();
            var preparedChildren = new System.Collections.Generic.List<GameplaySkinPreparedSceneNode>();
            for (int group = 0; group < 2; group++)
            {
                foreach ((string channel, string sourceId) in new[] { ("combined", "judgement.timing-direction"),
                             ("keys", "judgement.keys-timing-direction"), ("scratch", "judgement.scratch-timing-direction") })
                    add($"td.g{group}.{channel}", sourceId, group);
            }
            add("td.global", "judgement.timing-direction", null);
            scene["root"]!["children"] = children;
            scene["bindings"] = bindings;
            scene["tracks"] = new JArray();
            scene["variants"] = new JArray();
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            var decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(d => d.Code)));
            var document = decoded.Value!;
            for (int index = 0; index < document.Root.Children.Count; index++)
            {
                var node = document.Root.Children[index];
                var target = index < 6 ? GameplaySkinResolvedMaterialTarget.ForStage(layout.Groups[index / 3].TopologyGroup) : global;
                preparedChildren.Add(new GameplaySkinPreparedSceneNode(node.Id, node, node.Target, layout.Snapshot.Context.SafeBounds,
                    target, null, null, null, Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false));
            }
            var preparedScene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "timing-directions", manifest, document,
                Array.Empty<GameplaySkinPreparedSceneResource>(), new[]
                {
                    prepared(document.Root, layout.Snapshot.Context.SafeBounds, global, GameplaySkinSlotCatalog.TextHud, null, preparedChildren.ToArray()),
                });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, preparedScene);

            void add(string id, string sourceId, int? index)
            {
                JObject target = index.HasValue ? new JObject { ["kind"] = "stage", ["id"] = layout.Groups[index.Value].TopologyGroup.Identity.Id.Value,
                    ["index"] = index.Value } : new JObject { ["kind"] = "global" };
                children.Add(new JObject { ["id"] = id, ["type"] = "text", ["target"] = target, ["blend"] = "alpha",
                    ["properties"] = new JObject { ["text"] = "" }, ["effects"] = new JArray(), ["children"] = new JArray() });
                bindings.Add(new JObject { ["id"] = id + ".text", ["target"] = id, ["property"] = "text", ["source"] = sourceId });
            }
        }
    }
}
