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
        public void TestNextReadsRealSnapshotMetadataAtLateAttachAndResetWithoutChangingPersonalBestOrAa()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture);
            GameplaySkinEventStateSnapshot snapshot = pacemakerSnapshot(publication, true, 100, 75);
            using var stream = new GameplaySkinEventStream(publication, 250, snapshot);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                for (int epoch = 0; epoch < 2; epoch++)
                {
                    using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                    host.ProcessFrame();
                    assertPacemaker(host, "78", "0.78", "LV AA", true, 78, 78);
                    Assert.That(pacemakerText(host, "personal-best"), Is.EqualTo("75"));
                    Assert.That(pacemakerText(host, "rank-aa"), Is.EqualTo("78"));
                    producer = producer.Reset(250, snapshot, GameplaySkinEventResetReason.Retry);
                }
            }
            finally
            {
                producer.Dispose();
            }
        }

        [TestCase(true, 100, null)]
        [TestCase(false, 100, 75)]
        [TestCase(true, 0, 0)]
        [TestCase(true, 100, 101)]
        [TestCase(null, 100, 75)]
        public void TestNextUnavailableMetadataClosesBooleanBindingsAndVariants(bool? usesExScore, long maximum, long? best)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture);
            using var stream = new GameplaySkinEventStream(publication, 0, pacemakerSnapshot(publication, usesExScore, maximum, best));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertPacemaker(host, "0", "0.00", string.Empty, false, 0, 0);
            if (best.HasValue && usesExScore.HasValue)
                Assert.That(pacemakerText(host, "personal-best"), Is.EqualTo(best.Value.ToString()));
        }

        [Test]
        public void TestNextAndItsNumericTracksUseMaximumAndMatchedBestRatherThanCurrentScoreOrGraphOptions()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture);
            GameplaySkinEventStateSnapshot snapshot = pacemakerSnapshot(publication, true, 100, 75);
            using var stream = new GameplaySkinEventStream(publication, 0, snapshot);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                assertPacemaker(host, "78", "0.78", "LV AA", true, 78, 78);
                publishScore(stream, producer, 1, GameplaySkinEventKind.ScoreChanged,
                    new GameplaySkinScoreStateSnapshot(100, 0, 0, 1, 0.5, snapshot.Score.Statistics, maximumScore: 120));
                host.ProcessFrame();
                assertPacemaker(host, "80", "0.67", "LV A", true, 80, 100d * 2 / 3);
                Assert.That(pacemakerText(host, "personal-best"), Is.EqualTo("75"));
                Assert.That(pacemakerText(host, "rank-aa"), Is.EqualTo("94"));

                producer = producer.Reset(0, pacemakerSnapshot(publication, true, 100, 85), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                assertPacemaker(host, "89", "0.89", "LV AAA", true, 89, 89);
                Assert.That(pacemakerText(host, "personal-best"), Is.EqualTo("85"));
                Assert.That(pacemakerText(host, "rank-aa"), Is.EqualTo("78"));
            }
            finally
            {
                producer.Dispose();
            }
        }

        [TestCase("score.pacemaker-next")]
        [TestCase("score.pacemaker-next-progress")]
        [TestCase("score.pacemaker-next-label")]
        [TestCase("score.pacemaker-next-available")]
        public void TestEachNextSourceAloneRequestsMatchedPersonalBestWithoutSkinOptions(string source)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: source);
            Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.True);
        }

        [TestCase("score.pacemaker-next")]
        [TestCase("score.pacemaker-next-progress")]
        public void TestNextTrackAloneRequestsMatchedPersonalBest(string source)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, trackClock: source);
            Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.True);
        }

        [Test]
        public void TestNextAvailabilityVariantAloneRequestsMatchedPersonalBest()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject document = pacemakerScene();
            document["bindings"] = new JArray();
            document["tracks"] = new JArray();
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture, document);
            Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.True);
        }

        [Test]
        public void TestOrdinaryScoreAndAaConsumersDoNotAcquirePersonalBestStorageCapability()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            foreach (string source in new[] { "score.value", "score.maximum", "score.rank-aa", "score.rank-available" })
            {
                using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: source);
                Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.False);
            }
        }

        [TestCase("score.pacemaker-next-label")]
        [TestCase("score.pacemaker-next-available")]
        public void TestNextStringAndBooleanSourcesCannotDriveNumericClocks(string source)
        {
            JObject document = pacemakerScene();
            document["tracks"]![0]!["clock"] = source;
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            Assert.That(GameplaySkinSceneCodec.DecodeScene(document.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestNextAvailabilityVariantRejectsNonBooleanKeys()
        {
            JObject document = pacemakerScene();
            document["variants"]![0]!["cases"]![0]!["key"] = "yes";
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            Assert.That(GameplaySkinSceneCodec.DecodeScene(document.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [TestCase("score.pacemaker-next", GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase("score.pacemaker-next-progress", GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase("score.pacemaker-next-label", GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase("score.pacemaker-next-available", GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestNextIntegerValueUsesOriginalDigitOnlyBitmapFontWithoutPretendingLabelsAreNumbers(string source, GameplaySkinSceneDecodeStatus expected)
        {
            var font = new GameplaySkinBitmapFont("digits", 20, "0123456789".Select(character => new GameplaySkinBitmapGlyph(character, "glyph", 10)));
            var manifest = new GameplaySkinSceneManifest(new[] { new GameplaySkinSceneResource("glyph", GameplaySkinSceneResourceType.Texture, "digit.png") },
                bitmapFonts: new[] { font });
            JObject document = pacemakerScene();
            document["root"]!["children"]![0]!["properties"]!["bitmap-font"] = "digits";
            document["bindings"]![0]!["source"] = source;
            Assert.That(GameplaySkinSceneCodec.DecodeScene(document.ToString(), manifest).Status, Is.EqualTo(expected));
        }

        private static GameplaySkinEventStateSnapshot pacemakerSnapshot(GameplaySkinLayoutPublication publication, bool? usesExScore, long maximum, long? best)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(Math.Min(29, maximum), 0, 0, 1, 0.5, initial.Score.Statistics, maximumScore: maximum),
                initial.Timing, initial.BgaViewports,
                chartPresentation: usesExScore is bool exScore ? new GameplaySkinChartPresentation(Array.Empty<GameplaySkinLaneArrival>(), exScore, best) : null);
        }

        private static string pacemakerText(GameplaySkinSceneRuntimeHost host, string id)
            => ((SpriteText)runtime(host, id).ContentDrawable).Text.ToString();

        private static void assertPacemaker(GameplaySkinSceneRuntimeHost host, string value, string progress, string label, bool available,
                                            double valueRotation, double progressRotation)
        {
            Assert.Multiple(() =>
            {
                Assert.That(pacemakerText(host, "next.value"), Is.EqualTo(value));
                Assert.That(pacemakerText(host, "next.progress"), Is.EqualTo(progress));
                Assert.That(pacemakerText(host, "next.label"), Is.EqualTo(label));
                Assert.That(pacemakerText(host, "next.available"), Is.EqualTo(available ? "true" : "false"));
                Assert.That(runtime(host, "next.binding").TransformDrawable.Alpha, Is.EqualTo(available ? 1 : 0));
                Assert.That(runtime(host, "next.variant").TransformDrawable.Alpha, Is.EqualTo(available ? 1 : 0));
                Assert.That(runtime(host, "next.track.value").TransformDrawable.Rotation, Is.EqualTo(valueRotation).Within(0.0001));
                Assert.That(runtime(host, "next.track.progress").TransformDrawable.Rotation, Is.EqualTo(progressRotation).Within(0.0001));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        private static GameplaySkinLayoutPublication pacemakerPublication(Texture texture, JObject? authoredScene = null)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene((authoredScene ?? pacemakerScene()).ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(document), manifest);
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(roundTrip.Value!.Bindings.Select(binding => binding.SourceId), Is.EqualTo(document.Bindings.Select(binding => binding.SourceId)));
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("mania.hud").Rect;
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "pacemaker-next", manifest, document,
                Array.Empty<GameplaySkinPreparedSceneResource>(), new[]
                {
                    prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null,
                        document.Root.Children.Select(child => new GameplaySkinPreparedSceneNode(child.Id, child, child.Target, rect, global, null, null, null,
                            Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false)).ToArray()),
                });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }

        private static JObject pacemakerScene() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1",
             "root":{"id":"next.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
               {"id":"next.value","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"next.progress","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","format":"fixed-2"},"effects":[],"children":[]},
               {"id":"next.label","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"next.available","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"personal-best","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"rank-aa","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"next.binding","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"visible":false},"effects":[],"children":[]},
               {"id":"next.variant","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"visible":false},"effects":[],"children":[]},
               {"id":"next.track.value","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]},
               {"id":"next.track.progress","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]}]},
             "bindings":[
               {"id":"next.value.text","target":"next.value","property":"text","source":"score.pacemaker-next"},
               {"id":"next.progress.text","target":"next.progress","property":"text","source":"score.pacemaker-next-progress"},
               {"id":"next.label.text","target":"next.label","property":"text","source":"score.pacemaker-next-label"},
               {"id":"next.available.text","target":"next.available","property":"text","source":"score.pacemaker-next-available"},
               {"id":"pb.text","target":"personal-best","property":"text","source":"score.personal-best"},
               {"id":"aa.text","target":"rank-aa","property":"text","source":"score.rank-aa"},
               {"id":"next.binding.visible","target":"next.binding","property":"visible","source":"score.pacemaker-next-available"}],
             "variants":[{"id":"next.variant.visible","target":"next.variant","property":"visible","source":"score.pacemaker-next-available","default":false,
               "cases":[{"id":"next.variant.yes","key":"true","value":true},{"id":"next.variant.no","key":"false","value":false}]}],
             "tracks":[
               {"id":"next.value.clock","type":"tween","target":"next.track.value","property":"rotation","easing":"linear","loop":false,"clock":"score.pacemaker-next",
                "keyframes":[{"id":"next.value.zero","time":0,"value":0},{"id":"next.value.end","time":100,"value":100}]},
               {"id":"next.progress.clock","type":"tween","target":"next.track.progress","property":"rotation","easing":"linear","loop":false,"clock":"score.pacemaker-next-progress",
                "keyframes":[{"id":"next.progress.zero","time":0,"value":0},{"id":"next.progress.end","time":1,"value":100}]}],
             "stateMachines":[],"templates":[],"instances":[]}
            """);
    }
}
