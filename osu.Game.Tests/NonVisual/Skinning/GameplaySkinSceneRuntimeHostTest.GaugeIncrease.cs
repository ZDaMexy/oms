// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestGaugeIncreaseOriginRequiresRealPositiveNativeChange()
        {
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(null, 0, 1, 0, true), Is.Null,
                "An initial full gauge or restored snapshot is not a recovery edge.");
            double? origin = GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(null, 0.4, 0.401, 10, false);
            Assert.That(origin, Is.EqualTo(10), "OMS observes the native increment without imposing an artwork's quantisation.");
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(origin, 0.401, 0.401, 20, false), Is.EqualTo(10));
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(origin, 0.401, 0.2, 20, false), Is.EqualTo(10));
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(origin, 0.2, 0.8, 30, true), Is.EqualTo(10),
                "Replay reconstruction must not stamp its destination as a recovery.");
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(50, 0.2, 0.8, 30, true), Is.Null);
            Assert.That(GameplaySkinEventRuntimeHost.AdvanceGaugeIncreaseHistory(null, 0.8, 0.9, 31, false), Is.EqualTo(31));
        }

        [TestCase(null)]
        [TestCase(-25d)]
        [TestCase(0d)]
        [TestCase(20d)]
        public void TestGaugeIncreaseHistorySurvivesCompactPublicAndCompleteScoreDelivery(double? origin)
        {
            var score = gaugeScore(0.8, origin);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Score(GameplaySkinEventKind.GaugeChanged, score);
            Assert.That(compact.GetScore().LastGaugeIncreaseAt, Is.EqualTo(origin));
            var publicState = ((GameplaySkinScoreEventPayload)compact.Materialize(null, null)).State;
            Assert.That(publicState.LastGaugeIncreaseAt, Is.EqualTo(origin));
            Assert.That(publicState.Score, Is.EqualTo(score.Score));
            Assert.That(publicState.Gauge, Is.EqualTo(score.Gauge));
            Assert.That(GameplaySkinEventRuntimeHost.SnapshotScoreAt(publicState, 100).LastGaugeIncreaseAt, Is.EqualTo(origin));
            GameplaySkinScoreStateSnapshot rewound = GameplaySkinEventRuntimeHost.SnapshotScoreAt(publicState, -30);
            Assert.That(rewound.LastGaugeIncreaseAt, Is.Null);
            Assert.That(rewound.Score, Is.EqualTo(score.Score));
            Assert.That(rewound.Gauge, Is.EqualTo(score.Gauge));
            Assert.That(rewound.Statistics, Is.EqualTo(score.Statistics));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestGaugeIncreaseHistoryRejectsNonFiniteOrigin(double origin)
            => Assert.Throws<ArgumentOutOfRangeException>(() => gaugeScore(0.5, origin));

        [Test]
        public void TestGaugeIncreaseAvailabilityReadsRealHistoryInsteadOfInitialHealth()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: "gauge.increase-available");
            using var stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                host.ProcessFrame();
                assertAvailability(host, false);
                publishScore(stream, producer, 10, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.8, 10));
                host.ProcessFrame();
                assertAvailability(host, true);
                using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                late.ProcessFrame();
                assertAvailability(late, true);
                publishScore(stream, producer, 20, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.2, 10));
                host.ProcessFrame();
                assertAvailability(host, true);
                producer = producer.Reset(5, gaugeSnapshot(publication, GameplaySkinEventRuntimeHost.SnapshotScoreAt(gaugeScore(0.2, 10), 5)),
                    GameplaySkinEventResetReason.Rewind);
                host.ProcessFrame();
                late.ProcessFrame();
                assertAvailability(host, false);
                assertAvailability(late, false);
            }
            finally
            {
                producer.Dispose();
            }

            static void assertAvailability(GameplaySkinSceneRuntimeHost runtimeHost, bool expected)
            {
                Assert.That(runtimeHost.TryGetRuntimeNode("accuracy", out GameplaySkinSceneRuntimeNode? text), Is.True);
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo(expected ? "true" : "false"));
                Assert.That(runtimeHost.RuntimeFaults, Is.Empty);
            }
        }

        [TestCase("text", GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase("visible", GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase("opacity", GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase("rotation", GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestGaugeIncreaseAvailabilityOnlyAllowsBooleanBindingsAndVariants(string property, GameplaySkinSceneDecodeStatus expected)
        {
            JObject scene = JObject.Parse(comboActiveScene().ToString().Replace("combo.active", "gauge.increase-available", StringComparison.Ordinal));
            scene["bindings"]![0]!["property"] = property;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest());
            Assert.That(result.Status, Is.EqualTo(expected));
            if (expected != GameplaySkinSceneDecodeStatus.Valid)
                return;
            Assert.That(result.Value!.Bindings[0].Source, Is.EqualTo(GameplaySkinSceneBindingSource.GaugeIncreaseAvailable));
            Assert.That(result.Value.Variants.All(variant => variant.Source == GameplaySkinSceneBindingSource.GaugeIncreaseAvailable), Is.True);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(result.Value), comboActiveManifest()).Status,
                Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            scene["variants"]![0]!["cases"]![0]!["key"] = "increased";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestGaugeIncreaseAvailabilityCannotBecomeANumericOperandOrClock()
        {
            JObject scene = JObject.Parse(comboActiveScene().ToString().Replace("combo.active", "gauge.increase-available", StringComparison.Ordinal));
            scene["numericSources"] = JArray.Parse("""
                [{"id":"gauge.fake-number","operation":"add","left":"gauge.increase-available","right":1}]
                """);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene.Remove("numericSources");
            scene["tracks"] = JArray.Parse("""
                [{"id":"gauge.fake-clock","type":"tween","target":"combo.art","property":"rotation","easing":"linear","loop":false,
                  "clock":"gauge.increase-available","keyframes":[{"id":"gauge.fake-clock.start","time":0,"value":0}]}]
                """);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestGaugeRecordedTriggerUsesNativeOriginAcrossPauseLateAttachAndRewind(bool loop)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = createGaugeIncreasePublication(texture, loop);
            using var stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "An initial full gauge cannot invent an increase.");

            publishScore(stream, producer, 10, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.41, 10));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
            producer.SynchroniseTiming(20, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "A stationary gameplay clock does not advance animation.");

            publishScore(stream, producer, 25, GameplaySkinEventKind.ScoreChanged, gaugeScore(0.41, 10));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "Unrelated score updates must not restart the clip.");
            producer.SynchroniseTiming(30, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
            producer.SynchroniseTiming(40, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(loop ? 1 : 0));

            publishScore(stream, producer, 50, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.42, 50));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "A second real increase restarts both one-shot and loop programs.");
            publishScore(stream, producer, 60, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.2, 50));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "A decrease preserves the preceding increase phase.");
            using var lateHost = new GameplaySkinSceneRuntimeHost(publication, stream);
            lateHost.ProcessFrame();
            Assert.That(gaugeEffectAlpha(lateHost), Is.Zero, "Late attachment samples the same historical phase.");

            GameplaySkinScoreStateSnapshot rewound = GameplaySkinEventRuntimeHost.SnapshotScoreAt(gaugeScore(0.2, 50), 35);
            producer = producer.Reset(35, gaugeSnapshot(publication, rewound), GameplaySkinEventResetReason.Rewind);
            host.ProcessFrame();
            lateHost.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            Assert.That(gaugeEffectAlpha(lateHost), Is.Zero);
            producer.SynchroniseTiming(100, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "Discarded future history cannot reappear when time catches up.");
            publishScore(stream, producer, 110, GameplaySkinEventKind.GaugeChanged, gaugeScore(0.3, 110));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            Assert.That(host.RuntimeFaults, Is.Empty);
            Assert.That(lateHost.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        private static GameplaySkinScoreStateSnapshot gaugeScore(double gauge, double? origin)
            => new(24, 12, 12, 1, gauge, lastGaugeIncreaseAt: origin);

        private static GameplaySkinEventStateSnapshot gaugeSnapshot(GameplaySkinLayoutPublication publication, GameplaySkinScoreStateSnapshot score)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects,
                initial.CurrentJudgements, score, initial.Timing, initial.BgaViewports);
        }

        private static float gaugeEffectAlpha(GameplaySkinSceneRuntimeHost host)
        {
            Assert.That(host.TryGetRuntimeNode("gauge.clip-node", out GameplaySkinSceneRuntimeNode? node), Is.True);
            return node!.ContentDrawable.ChildrenOfType<Sprite>().Single().Alpha;
        }

        private static GameplaySkinLayoutPublication createGaugeIncreasePublication(Texture texture, bool loop, string trigger = "gauge.increase",
                                                                                     double failureOutro = 0, string? clock = null, bool mountClip = true)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            var materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.Decoration, global, texture, source()) });
            var definition = new GameplaySkinRecordedAnimationDefinition("gauge.clip", "clip.json", new[] { "gauge.texture" });
            string json = """
                {"contract":"CONTRACT","width":8,"height":8,"fps":100,"loop":LOOP,"trigger":"TRIGGER",
                 "geometries":[{"resource":"gauge.texture","vertices":[[0,0,0,0,0],[8,0,0,1,0],[0,8,0,0,1]],"triangles":[0,1,2]}],
                 "nodes":[{"parent":-1,"geometry":0,"blend":"normal"}],
                 "states":[{"matrix":[1,0,0,0,1,0,0,0,1,0,0,0],"origin":[0,0,0],"projection":"affine","multiply":[1,1,1,1],
                 "add":[0,0,0,0],"hsl":[0,0,0],"mask":null,"colourMatrix":null}],
                 "frames":[{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null},{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null}]}
                """.Replace("CONTRACT", GameplaySkinRecordedAnimationCodec.CONTRACT, StringComparison.Ordinal)
                     .Replace("LOOP", loop ? "true" : "false", StringComparison.Ordinal)
                     .Replace("TRIGGER", trigger, StringComparison.Ordinal);
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(Encoding.UTF8.GetBytes(json), definition);
            var resource = new GameplaySkinSceneResource("gauge.texture", GameplaySkinSceneResourceType.Texture, "gauge.png");
            var manifest = new GameplaySkinSceneManifest(new[] { resource }, animations: new[] { definition });
            var root = new GameplaySkinSceneNode("gauge.clip-node", GameplaySkinSceneNodeType.RecordedAnimation,
                target(GameplaySkinSceneTargetKind.Global), GameplaySkinSlotCatalog.Decoration.Id, null, GameplaySkinSceneBlendMode.Alpha,
                new Dictionary<string, GameplaySkinScenePropertyValue>(), Array.Empty<GameplaySkinSceneEffect>(), Array.Empty<GameplaySkinSceneNode>(), definition.Id, clockSourceId: clock);
            var document = new GameplaySkinSceneDocument(root, Array.Empty<GameplaySkinSceneTrack>(), Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(), Array.Empty<GameplaySkinSceneVariant>(), Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>(), failureOutro: failureOutro);
            var package = new GameplaySkinPreparedPackage("gauge-increase", manifest, document, Array.Empty<GameplaySkinCapturedSceneResource>(), null, null,
                recordedAnimations: new Dictionary<string, GameplaySkinRecordedAnimationProgram> { [definition.Id] = program });
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "gauge-increase", manifest, document,
                new[] { new GameplaySkinPreparedSceneResource(resource, "gauge-increase", 8, 8, texture) },
                mountClip ? new[] { prepared(root, layout.StageRect, global, GameplaySkinSlotCatalog.Decoration, null) } : Array.Empty<GameplaySkinPreparedSceneNode>(), package: package);
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }
    }
}
