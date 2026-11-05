// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase("score.personal-best")]
        [TestCase("score.maximum")]
        [TestCase("score.rank-a")]
        [TestCase("score.rank-aa")]
        [TestCase("score.rank-aaa")]
        [TestCase("score.pacemaker-next")]
        [TestCase("option.target")]
        [TestCase("value.target.fixed")]
        public void TestScoreCrossingStableThresholdRoundTrips(string source)
        {
            JObject scene = crossingScene(source);
            GameplaySkinSceneManifest manifest = crossingManifest();
            var decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            var roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(decoded.Value!), manifest);
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(roundTrip.Value!.Root.ScoreCrossingThresholds.Single().SourceId, Is.EqualTo(source));
            Assert.That(roundTrip.Value.Root.ScoreCrossingThresholds.Single().Comparison, Is.EqualTo(GameplaySkinScoreCrossingComparison.Greater));
        }

        [TestCase("score.value")]
        [TestCase("score.progress")]
        [TestCase("score.accuracy")]
        [TestCase("combo.value")]
        [TestCase("gauge.maximum")]
        [TestCase("gauge.value")]
        [TestCase("judgement.count.perfect")]
        [TestCase("timing.time")]
        [TestCase("input.pressed")]
        public void TestScoreCrossingRejectsLiveThresholdEvenThroughDerivedValues(string source)
        {
            JObject scene = crossingScene(source);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), crossingManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["root"]!["scoreCrossing"]!["thresholds"]![0]!["source"] = "value.target.fixed";
            scene["numericSources"]![0]!["left"] = source;
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), crossingManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid),
                "A derived expression may not hide a moving threshold, even when an author multiplies that dependency by zero.");
        }

        [TestCase("empty")]
        [TestCase("six")]
        [TestCase("duplicate")]
        [TestCase("comparison")]
        [TestCase("missing-source")]
        [TestCase("literal")]
        [TestCase("unknown-field")]
        [TestCase("container")]
        [TestCase("clock")]
        public void TestScoreCrossingRejectsMalformedOrUnboundedDeclaration(string kind)
        {
            JObject scene = crossingScene();
            var thresholds = (JArray)scene["root"]!["scoreCrossing"]!["thresholds"]!;
            switch (kind)
            {
                case "empty": thresholds.Clear(); break;
                case "six":
                    for (int i = 0; i < 5; i++)
                        thresholds.Add(thresholds[0].DeepClone());
                    break;
                case "duplicate": thresholds.Add(thresholds[0].DeepClone()); break;
                case "comparison": thresholds[0]!["comparison"] = "equal"; break;
                case "missing-source": ((JObject)thresholds[0]!).Remove("source"); break;
                case "literal": thresholds[0]!["source"] = 100; break;
                case "unknown-field": scene["root"]!["scoreCrossing"]!["repeat"] = true; break;
                case "container": scene["root"]!["type"] = "container"; ((JObject)scene["root"]!).Remove("animation"); break;
                case "clock": scene["root"]!["clock"] = "timing.time"; break;
            }
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), crossingManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [TestCase("score.crossing", false, true, true)]
        [TestCase("score.crossing", false, false, false)]
        [TestCase("score.crossing", true, true, false)]
        [TestCase("gameplay.attach", false, true, false)]
        [TestCase("gameplay.attach", false, false, true)]
        public void TestScoreCrossingRequiresPairedNonLoopingProgram(string trigger, bool loop, bool declaration, bool valid)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = crossingScene();
            if (!declaration)
                ((JObject)scene["root"]!).Remove("scoreCrossing");
            if (valid)
            {
                using GameplaySkinLayoutPublication publication = crossingPublication(texture, scene, trigger, loop);
                Assert.That(publication.PreparedScene.RecordedAnimations.Count, Is.EqualTo(1));
            }
            else
                Assert.Throws<GameplaySkinScenePreparationException>(() => crossingPublication(texture, scene, trigger, loop));
        }

        [TestCase("greater", 100, false)]
        [TestCase("greater", 101, true)]
        [TestCase("at-least", 100, true)]
        [TestCase("at-least", 99, false)]
        public void TestScoreCrossingUsesActualScoreEdgeAndExactComparison(string comparison, long score, bool starts)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = crossingScene();
            scene["root"]!["scoreCrossing"]!["thresholds"]![0]!["comparison"] = comparison;
            using GameplaySkinLayoutPublication publication = crossingPublication(texture, scene);
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            // Native Combo/Gauge callbacks can already carry the new total before TotalScore's own event.
            publishScore(stream, producer, 9, GameplaySkinEventKind.ComboChanged, crossingScore(score));
            publishScore(stream, producer, 9, GameplaySkinEventKind.GaugeChanged, crossingScore(score));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(score));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(starts ? 1 : 0));
            producer.SynchroniseTiming(20, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            publishScore(stream, producer, 20, GameplaySkinEventKind.ScoreChanged, crossingScore(score));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "Accuracy/statistics delivery with the same total cannot replay a threshold.");
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "A stationary gameplay clock is also the animation clock.");
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestScoreCrossingFiveThresholdsRestartOneSharedNodeOnlyForNewMilestones()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = crossingScene();
            scene["root"]!["scoreCrossing"]!["thresholds"] = JArray.Parse("""
                [{"source":"score.personal-best","comparison":"greater"},
                 {"source":"value.target.fixed","comparison":"greater"},
                 {"source":"score.rank-a","comparison":"at-least"},
                 {"source":"score.rank-aa","comparison":"at-least"},
                 {"source":"score.rank-aaa","comparison":"at-least"}]
                """);
            using GameplaySkinLayoutPublication publication = crossingPublication(texture, scene);
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(101));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "PB and fixed target cross in the same immutable envelope.");
            Assert.That(host.RuntimeNodeCount, Is.EqualTo(1), "The shared flash remains one drawable, not one copy for each milestone.");
            publishScore(stream, producer, 20, GameplaySkinEventKind.ScoreChanged, crossingScore(150));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            publishScore(stream, producer, 25, GameplaySkinEventKind.ScoreChanged, crossingScore(200));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "A new A milestone restarts the same movie.");
            producer.SynchroniseTiming(35, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            publishScore(stream, producer, 40, GameplaySkinEventKind.ScoreChanged, crossingScore(300));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "AA and AAA share a single new origin when crossed together.");
            publishScore(stream, producer, 50, GameplaySkinEventKind.ScoreChanged, crossingScore(300));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestScoreCrossingSnapshotLateAttachRetryAndRewindDoNotInventOrigins()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = crossingPublication(texture);
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(101));
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "The real edge can arrive before the drawable's first creation frame.");
                using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(late), Is.Zero, "Late attachment has no historical per-node crossing time.");
                producer = producer.Reset(20, crossingSnapshot(publication, 101), GameplaySkinEventResetReason.ConsumerRebuilt);
                host.ProcessFrame();
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "A known time10 is retained, not restamped20.");
                Assert.That(gaugeEffectAlpha(late), Is.Zero);
                producer = producer.Reset(25, crossingSnapshot(publication, 101), GameplaySkinEventResetReason.Seek);
                host.ProcessFrame();
                producer.SynchroniseTiming(30, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "The retained original phase remains valid after a forward seek.");
                producer = producer.Reset(5, crossingSnapshot(publication, 99), GameplaySkinEventResetReason.Rewind);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                producer.SynchroniseTiming(100, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "Removed future history cannot reappear by waiting.");
                publishScore(stream, producer, 110, GameplaySkinEventKind.ScoreChanged, crossingScore(101));
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
                producer = producer.Reset(0, crossingSnapshot(publication, 0), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                producer = producer.Reset(20, crossingSnapshot(publication, 200), GameplaySkinEventResetReason.Seek);
                host.ProcessFrame();
                publishScore(stream, producer, 21, GameplaySkinEventKind.ScoreChanged, crossingScore(201));
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "A skipped crossing is spent with unknown origin, never replayed at the next judgement.");
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestScoreCrossingVisibilityDoesNotRearmAndMissingBestKeepsExistingDerivedTargetMeaning()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = crossingScene("value.target.fixed");
            scene["numericSources"]![0]!["left"] = "score.personal-best";
            scene["numericSources"]![0]!["right"] = 100;
            scene["bindings"] = JArray.Parse("""[{"id":"gate","target":"gauge.clip-node","property":"visible","source":"combo.active"}]""");
            using GameplaySkinLayoutPublication publication = crossingPublication(texture, scene);
            Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.True);
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0, best: null));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(101));
            host.ProcessFrame();
            Assert.That(runtime(host, "gauge.clip-node").TransformDrawable.Alpha, Is.Zero);
            publishScore(stream, producer, 25, GameplaySkinEventKind.ComboChanged, crossingScore(101, 1));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "Opening the gate samples time15, not a new frame0.");
            producer.SynchroniseTiming(30, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1), "Missing PB remains numeric0, so the explicit derived positive target was valid.");
        }

        [TestCase(null)]
        [TestCase(0L)]
        public void TestScoreCrossingMissingOrZeroPersonalBestCannotTrigger(long? best)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = crossingPublication(texture);
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0, best));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(101));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero);
        }

        [Test]
        public void TestScoreCrossingChangedMaximumRebasesInsteadOfMakingAMetadataCrossing()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = crossingPublication(texture, crossingScene("score.rank-a"));
            using var stream = new GameplaySkinEventStream(publication, 0, crossingSnapshot(publication, 0));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            publishScore(stream, producer, 10, GameplaySkinEventKind.ScoreChanged, crossingScore(150, maximum: 150));
            host.ProcessFrame();
            Assert.That(gaugeEffectAlpha(host), Is.Zero, "The threshold changed200->100; a current snapshot above it is not a known score crossing.");
        }

        private static GameplaySkinScoreStateSnapshot crossingScore(long score, int combo = 0, long maximum = 300)
            => new(score, combo, combo, 1, 0.5, maximumScore: maximum);

        private static GameplaySkinEventStateSnapshot crossingSnapshot(GameplaySkinLayoutPublication publication, long score, long? best = 100)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                crossingScore(score), initial.Timing, initial.BgaViewports,
                chartPresentation: new GameplaySkinChartPresentation(Array.Empty<GameplaySkinLaneArrival>(), true, best));
        }

        private static GameplaySkinSceneManifest crossingManifest()
            => new(new[] { new GameplaySkinSceneResource("gauge.texture", GameplaySkinSceneResourceType.Texture, "gauge.png") },
                options: new[] { new GameplaySkinOptionDefinition("target", "Target", GameplaySkinOptionKind.Number, "100",
                    Array.Empty<GameplaySkinOptionChoice>(), 0, 300, 1, Array.Empty<string>(), Array.Empty<string>(), GameplaySkinOptionEffect.Presentation) },
                animations: new[] { new GameplaySkinRecordedAnimationDefinition("gauge.clip", "clip.json", new[] { "gauge.texture" }) });

        private static JObject crossingScene(string source = "score.personal-best")
        {
            JObject scene = JObject.Parse("""
                {"contract":"CONTRACT","root":{"id":"gauge.clip-node","type":"recorded-animation","target":{"kind":"global"},
                  "slot":"decoration","animation":"gauge.clip","blend":"alpha","properties":{},"effects":[],"children":[],
                  "scoreCrossing":{"thresholds":[{"source":"score.personal-best","comparison":"greater"}]}},
                 "numericSources":[{"id":"target.fixed","operation":"add","left":100,"right":0}],
                 "tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                """.Replace("CONTRACT", GameplaySkinSceneContracts.SCENE_CONTRACT_ID, StringComparison.Ordinal));
            scene["root"]!["scoreCrossing"]!["thresholds"]![0]!["source"] = source;
            return scene;
        }

        private static GameplaySkinLayoutPublication crossingPublication(Texture texture, JObject? authored = null, string trigger = "score.crossing", bool loop = false)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            var materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.Decoration, global, texture, source()) });
            GameplaySkinSceneManifest manifest = crossingManifest();
            var decoded = GameplaySkinSceneCodec.DecodeScene((authored ?? crossingScene()).ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            string json = """
                {"contract":"CONTRACT","width":8,"height":8,"fps":100,"loop":LOOP,"trigger":"TRIGGER",
                 "geometries":[{"resource":"gauge.texture","vertices":[[0,0,0,0,0],[8,0,0,1,0],[0,8,0,0,1]],"triangles":[0,1,2]}],
                 "nodes":[{"parent":-1,"geometry":0,"blend":"normal"}],
                 "states":[{"matrix":[1,0,0,0,1,0,0,0,1,0,0,0],"origin":[0,0,0],"projection":"affine","multiply":[1,1,1,1],
                 "add":[0,0,0,0],"hsl":[0,0,0],"mask":null,"colourMatrix":null}],
                 "frames":[{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null},{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null}]}
                """.Replace("CONTRACT", GameplaySkinRecordedAnimationCodec.CONTRACT, StringComparison.Ordinal)
                     .Replace("LOOP", loop ? "true" : "false", StringComparison.Ordinal).Replace("TRIGGER", trigger, StringComparison.Ordinal);
            GameplaySkinRecordedAnimationDefinition definition = manifest.Animations.Single();
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(Encoding.UTF8.GetBytes(json), definition);
            var package = new GameplaySkinPreparedPackage("score-crossing", manifest, document, Array.Empty<GameplaySkinCapturedSceneResource>(), null, null,
                recordedAnimations: new Dictionary<string, GameplaySkinRecordedAnimationProgram> { [definition.Id] = program });
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "score-crossing", manifest, document,
                new[] { new GameplaySkinPreparedSceneResource(manifest.Resources.Single(), "score-crossing", 8, 8, texture) },
                new[] { prepared(document.Root, layout.StageRect, global, GameplaySkinSlotCatalog.Decoration, null) }, package: package);
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }
    }
}
