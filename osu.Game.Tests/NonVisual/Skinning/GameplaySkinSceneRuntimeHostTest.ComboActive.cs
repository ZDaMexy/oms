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
        [TestCase("bms", 39, 120)]
        [TestCase("mania", 987654, 1000000)]
        public void TestComboActiveFollowsTheSameScoreSnapshotAcrossBreaksLateAttachAndResets(string ruleset, long scoreValue, long maximumScore)
        {
            using Texture idle = new DummyRenderer().CreateTexture(8, 8);
            using Texture positive = new DummyRenderer().CreateTexture(16, 16);
            using GameplaySkinLayoutPublication publication = comboActivePublication(ruleset, idle, positive);
            using var stream = new GameplaySkinEventStream(publication, 0, comboActiveSnapshot(publication, scoreValue, maximumScore, 0));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                host.ProcessFrame();
                assertComboActive(host, idle, positive, 0);
                int time = 0;
                foreach (int combo in new[] { 1, 23, 0 })
                {
                    GameplaySkinScoreStateSnapshot score = comboActiveSnapshot(publication, scoreValue, maximumScore, combo).Score;
                    GameplaySkinEventValue compact = GameplaySkinEventValue.Score(GameplaySkinEventKind.ComboChanged, score);
                    Assert.That(compact.GetScore().Score, Is.EqualTo(scoreValue));
                    Assert.That(compact.GetScore().MaximumScore, Is.EqualTo(maximumScore));
                    Assert.That(compact.GetScore().Combo, Is.EqualTo(combo));
                    stream.Publish(producer, ++time, compact, null, null);
                    host.ProcessFrame();
                    assertComboActive(host, idle, positive, combo);
                    using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                    late.ProcessFrame();
                    assertComboActive(late, idle, positive, combo);
                }

                foreach (GameplaySkinEventResetReason reason in new[]
                         {
                             GameplaySkinEventResetReason.Seek, GameplaySkinEventResetReason.Rewind, GameplaySkinEventResetReason.Retry,
                         })
                {
                    producer = producer.Reset(0, comboActiveSnapshot(publication, scoreValue, maximumScore, 7), reason);
                    host.ProcessFrame();
                    assertComboActive(host, idle, positive, 7);
                    producer = producer.Reset(0, comboActiveSnapshot(publication, scoreValue, maximumScore, 0), reason);
                    host.ProcessFrame();
                    assertComboActive(host, idle, positive, 0);
                    using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                    late.ProcessFrame();
                    assertComboActive(late, idle, positive, 0);
                }
            }
            finally
            {
                producer.Dispose();
            }
        }

        [TestCase("text", GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase("visible", GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase("rotation", GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase("opacity", GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestComboActiveIsABooleanBindingAndKeepsItsTypedSource(string property, GameplaySkinSceneDecodeStatus expected)
        {
            JObject scene = comboActiveScene();
            scene["bindings"]![0]!["property"] = property;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest());
            Assert.That(result.Status, Is.EqualTo(expected));
            if (expected != GameplaySkinSceneDecodeStatus.Valid)
                return;
            Assert.That(result.Value!.Bindings[0].Source, Is.EqualTo(GameplaySkinSceneBindingSource.ComboActive));
            Assert.That((int)result.Value.Bindings[0].Source, Is.EqualTo(104));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(result.Value), comboActiveManifest());
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(roundTrip.Value!.Variants.All(variant => variant.Source == GameplaySkinSceneBindingSource.ComboActive), Is.True);
        }

        [TestCase("visible")]
        [TestCase("resource")]
        public void TestComboActiveVariantsAcceptOnlyBooleanKeys(string property)
        {
            JObject scene = comboActiveScene();
            int index = property == "visible" ? 0 : 1;
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            scene["variants"]![index]!["cases"]![0]!["key"] = "positive";
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> invalid = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest());
            Assert.That(invalid.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(invalid.Diagnostics.Select(item => item.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue));
        }

        [Test]
        public void TestComboActiveCannotDriveNumericTrackOrNodeClocks()
        {
            JObject scene = comboActiveScene();
            var track = new JObject
            {
                ["id"] = "combo.clock", ["type"] = "tween", ["target"] = "combo.art", ["property"] = "rotation", ["easing"] = "linear", ["loop"] = false,
                ["clock"] = "combo.value", ["keyframes"] = new JArray(
                    new JObject { ["id"] = "combo.clock.start", ["time"] = 0, ["value"] = 0 },
                    new JObject { ["id"] = "combo.clock.end", ["time"] = 10, ["value"] = 90 }),
            };
            scene["tracks"] = new JArray(track);
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            track["clock"] = "combo.active";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            scene["tracks"] = new JArray();
            scene["root"]!["clock"] = "combo.active";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestComboActiveWordsCannotBeSilentlyMappedIntoNumericBitmapFonts()
        {
            GameplaySkinSceneManifest normal = comboActiveManifest();
            var digits = new GameplaySkinBitmapFont("combo.digits", 8, "0123456789".Select(character => new GameplaySkinBitmapGlyph(character, "combo.idle", 8)));
            var manifest = new GameplaySkinSceneManifest(normal.Resources, bitmapFonts: new[] { digits });
            JObject scene = comboActiveScene();
            scene["root"]!["children"]![0]!["properties"]!["bitmap-font"] = "combo.digits";
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        private static GameplaySkinEventStateSnapshot comboActiveSnapshot(GameplaySkinLayoutPublication publication, long score, long maximumScore, int combo)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            // Current combo, highest combo and EX/score totals are independent facts. A broken combo remains
            // inactive even while this real snapshot retains positive score and a larger previous maximum combo.
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(score, combo, 40, 0.75, 0.6, initial.Score.Statistics, maximumScore: maximumScore),
                initial.Timing, initial.BgaViewports);
        }

        private static void assertComboActive(GameplaySkinSceneRuntimeHost host, Texture idle, Texture positive, int combo)
        {
            bool active = combo > 0;
            Assert.That(((SpriteText)runtime(host, "combo.text").ContentDrawable).Text.ToString(), Is.EqualTo(active ? "true" : "false"));
            Assert.That(((SpriteText)runtime(host, "combo.value").ContentDrawable).Text.ToString(), Is.EqualTo(combo.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(runtime(host, "combo.bound").TransformDrawable.Alpha, Is.EqualTo(active ? 1 : 0));
            Assert.That(runtime(host, "combo.variant").TransformDrawable.Alpha, Is.EqualTo(active ? 1 : 0));
            Assert.That(((Sprite)runtime(host, "combo.art").ContentDrawable).Texture, Is.SameAs(active ? positive : idle));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        private static GameplaySkinSceneManifest comboActiveManifest() => new GameplaySkinSceneManifest(new[]
        {
            new GameplaySkinSceneResource("combo.idle", GameplaySkinSceneResourceType.Texture, "textures/idle.png"),
            new GameplaySkinSceneResource("combo.positive", GameplaySkinSceneResourceType.Texture, "textures/positive.png"),
        });

        private static GameplaySkinLayoutPublication comboActivePublication(string ruleset, Texture idle, Texture positive)
        {
            LayoutFixture layout = createLayout(ruleset);
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, idle, source()) });
            GameplaySkinSceneManifest manifest = comboActiveManifest();
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(comboActiveScene().ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(item => item.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            var resources = new[]
            {
                new GameplaySkinPreparedSceneResource(manifest.Resources[0], "idle", 8, 8, idle),
                new GameplaySkinPreparedSceneResource(manifest.Resources[1], "positive", 16, 16, positive),
            };
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface(ruleset + ".hud").Rect;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "combo-active", manifest, document, resources, new[]
            {
                prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null,
                    document.Root.Children.Select(child =>
                    {
                        GameplaySkinPreparedSceneResource? resource = child.ResourceId == null ? null : resources.Single(item => item.Id == child.ResourceId);
                        return new GameplaySkinPreparedSceneNode(child.Id, child, child.Target, rect, global, null, resource, resource?.Texture,
                            Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false);
                    }).ToArray()),
            });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }

        private static JObject comboActiveScene() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1",
             "root":{"id":"combo.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
               {"id":"combo.text","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"combo.value","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":""},"effects":[],"children":[]},
               {"id":"combo.bound","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"visible":false},"effects":[],"children":[]},
               {"id":"combo.variant","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"visible":false},"effects":[],"children":[]},
               {"id":"combo.art","type":"sprite","target":{"kind":"global"},"resource":"combo.idle","blend":"alpha","properties":{},"effects":[],"children":[]}]},
             "bindings":[
               {"id":"combo.active.text","target":"combo.text","property":"text","source":"combo.active"},
               {"id":"combo.value.text","target":"combo.value","property":"text","source":"combo.value"},
               {"id":"combo.active.visible","target":"combo.bound","property":"visible","source":"combo.active"}],
             "variants":[
               {"id":"combo.variant.visible","target":"combo.variant","property":"visible","source":"combo.active","default":false,
                "cases":[{"id":"combo.visible.true","key":"true","value":true},{"id":"combo.visible.false","key":"false","value":false}]},
               {"id":"combo.variant.art","target":"combo.art","property":"resource","source":"combo.active","default":"combo.idle",
                "cases":[{"id":"combo.art.true","key":"true","resource":"combo.positive"},{"id":"combo.art.false","key":"false","resource":"combo.idle"}]}],
             "tracks":[],"stateMachines":[],"templates":[],"instances":[]}
            """);
    }
}
