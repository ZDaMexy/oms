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
        [TestCase("NORMAL", "01", 1, 1)]
        [TestCase("normal", "1", 2, 2)]
        [TestCase("Normal", "1.0", 0, 0)]
        [TestCase(" NORMAL ", " 01 ", 2, 2)]
        [TestCase("NORMAL ", "01 ", 0, 0)]
        [TestCase("初心者★１２", "難易度１２", 2, 2)]
        [TestCase("é", "１", 1, 2)]
        [TestCase("e\u0301", "初", 2, 1)]
        [TestCase("unavailable", "unavailable", 2, 2)]
        [TestCase("unknown", "unknown", 0, 0)]
        [TestCase("", "", 0, 0)]
        public void TestSongTextVariantsSelectActualPreparedTexturesAndVisibility(string difficulty, string level, int difficultyTexture, int levelTexture)
        {
            using Texture fallback = new DummyRenderer().CreateTexture(8, 8);
            using Texture first = new DummyRenderer().CreateTexture(8, 8);
            using Texture second = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = songVariantPublication(fallback, first, second);
            using var stream = new GameplaySkinEventStream(publication, 0, songVariantSnapshot(publication,
                new GameplaySkinSongInformation("Title", "Artist", difficulty, level, 120, 180)));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertSongVariants(host, new[] { fallback, first, second }, difficultyTexture, levelTexture);
        }

        [Test]
        public void TestSongTextVariantsMatchTheMaximumMetadataLength()
        {
            using Texture fallback = new DummyRenderer().CreateTexture(8, 8);
            using Texture first = new DummyRenderer().CreateTexture(8, 8);
            using Texture second = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = songVariantPublication(fallback, first, second);
            string text = new string('界', GameplaySkinSongInformation.MAX_TEXT_LENGTH);
            using var stream = new GameplaySkinEventStream(publication, 0, songVariantSnapshot(publication,
                new GameplaySkinSongInformation("Title", "Artist", text, text, 120, 180)));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertSongVariants(host, new[] { fallback, first, second }, 1, 1);
        }

        [Test]
        public void TestSongTextEmptyLiteralRequiresActualMetadataAndDoesNotStandForMissingSource()
        {
            using Texture fallback = new DummyRenderer().CreateTexture(8, 8);
            using Texture first = new DummyRenderer().CreateTexture(8, 8);
            using Texture second = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = songVariantPublication(fallback, first, second, includeEmpty: true);
            var textures = new[] { fallback, first, second };
            var empty = songVariantSnapshot(publication, new GameplaySkinSongInformation("Title", "Artist", "", "", 120, 180));
            using var stream = new GameplaySkinEventStream(publication, 0, empty);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertSongVariants(host, textures, 1, 1);

            producer = producer.Reset(0, songVariantSnapshot(publication, null), GameplaySkinEventResetReason.ConsumerRebuilt);
            host.ProcessFrame();
            assertSongVariants(host, textures, 0, 0);
            using (var late = new GameplaySkinSceneRuntimeHost(publication, stream))
            {
                late.ProcessFrame();
                assertSongVariants(late, textures, 0, 0);
            }

            producer = producer.Reset(0, empty, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            assertSongVariants(host, textures, 1, 1);
            producer.Dispose();
        }

        [Test]
        public void TestSongTextVariantsKeepHostOwnershipThroughTimingLateAttachAndReset()
        {
            using Texture fallback = new DummyRenderer().CreateTexture(8, 8);
            using Texture first = new DummyRenderer().CreateTexture(8, 8);
            using Texture second = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = songVariantPublication(fallback, first, second);
            var textures = new[] { fallback, first, second };
            var original = songVariantSnapshot(publication, new GameplaySkinSongInformation("First song", "Artist", "NORMAL", "01", 120, 180));
            var other = songVariantSnapshot(publication, new GameplaySkinSongInformation("Second song", "Artist", "normal", "1", 120, 180));
            using var firstStream = new GameplaySkinEventStream(publication, 0, original);
            using var secondStream = new GameplaySkinEventStream(publication, 0, other);
            GameplaySkinEventProducer producer = firstStream.CreateProducer();
            using var firstHost = new GameplaySkinSceneRuntimeHost(publication, firstStream);
            using var secondHost = new GameplaySkinSceneRuntimeHost(publication, secondStream);
            firstHost.ProcessFrame();
            secondHost.ProcessFrame();
            assertSongVariants(firstHost, textures, 1, 1);
            assertSongVariants(secondHost, textures, 2, 2);

            long applications = firstHost.VariantApplicationCount;
            producer.SynchroniseTiming(100, new GameplaySkinTimingStateSnapshot(1, 0, 120, false, 1));
            firstHost.ProcessFrame();
            assertSongVariants(firstHost, textures, 1, 1);
            Assert.That(firstHost.VariantApplicationCount, Is.EqualTo(applications), "Immutable song metadata belongs to Layout, not the per-frame timing family.");
            using (var late = new GameplaySkinSceneRuntimeHost(publication, firstStream))
            {
                late.ProcessFrame();
                assertSongVariants(late, textures, 1, 1);
            }

            producer = producer.Reset(0, other, GameplaySkinEventResetReason.ConsumerRebuilt);
            firstHost.ProcessFrame();
            assertSongVariants(firstHost, textures, 2, 2);
            assertSongVariants(secondHost, textures, 2, 2);
            producer = producer.Reset(0, original, GameplaySkinEventResetReason.Retry);
            firstHost.ProcessFrame();
            assertSongVariants(firstHost, textures, 1, 1);
            assertSongVariants(secondHost, textures, 2, 2);
            producer.Dispose();
        }

        private static GameplaySkinEventStateSnapshot songVariantSnapshot(GameplaySkinLayoutPublication publication, GameplaySkinSongInformation? information)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                initial.Score, initial.Timing, initial.BgaViewports, information);
        }

        private static void assertSongVariants(GameplaySkinSceneRuntimeHost host, Texture[] textures, int difficulty, int level)
        {
            Assert.That(((Sprite)runtime(host, "song.difficulty").ContentDrawable).Texture, Is.SameAs(textures[difficulty]));
            Assert.That(runtime(host, "song.difficulty").TransformDrawable.Alpha, Is.EqualTo(difficulty == 0 ? 0 : 1));
            Assert.That(((Sprite)runtime(host, "song.level").ContentDrawable).Texture, Is.SameAs(textures[level]));
            Assert.That(runtime(host, "song.level").TransformDrawable.Alpha, Is.EqualTo(level == 0 ? 0 : 1));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        private static GameplaySkinLayoutPublication songVariantPublication(Texture fallback, Texture first, Texture second, bool includeEmpty = false)
        {
            LayoutFixture layout = createLayout("bms");
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, fallback, source()) });
            var manifest = new GameplaySkinSceneManifest(new[]
            {
                new GameplaySkinSceneResource("song.default", GameplaySkinSceneResourceType.Texture, "textures/default.png"),
                new GameplaySkinSceneResource("song.first", GameplaySkinSceneResourceType.Texture, "textures/first.png"),
                new GameplaySkinSceneResource("song.second", GameplaySkinSceneResourceType.Texture, "textures/second.png"),
            });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(songVariantScene(includeEmpty).ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(item => item.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            var resources = new[]
            {
                new GameplaySkinPreparedSceneResource(manifest.Resources[0], "default", 8, 8, fallback),
                new GameplaySkinPreparedSceneResource(manifest.Resources[1], "first", 8, 8, first),
                new GameplaySkinPreparedSceneResource(manifest.Resources[2], "second", 8, 8, second),
            };
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("bms.hud").Rect;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "song-variants", manifest, document, resources, new[]
            {
                prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null,
                    document.Root.Children.Select(child => new GameplaySkinPreparedSceneNode(child.Id, child, child.Target, rect, global, null, resources[0], fallback,
                        Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false)).ToArray()),
            });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }

        private static JObject songVariantScene(bool includeEmpty)
        {
            JObject scene = JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1",
                 "root":{"id":"song.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                   {"id":"song.difficulty","type":"sprite","target":{"kind":"global"},"resource":"song.default","blend":"alpha","properties":{},"effects":[],"children":[]},
                   {"id":"song.level","type":"sprite","target":{"kind":"global"},"resource":"song.default","blend":"alpha","properties":{},"effects":[],"children":[]}]},
                 "bindings":[],"variants":[],"tracks":[],"stateMachines":[],"templates":[],"instances":[]}
                """);
            var variants = (JArray)scene["variants"]!;
            foreach (string source in new[] { "song.difficulty", "song.level" })
            {
                string maximum = new string('界', GameplaySkinSongInformation.MAX_TEXT_LENGTH);
                string[] first = source == "song.difficulty" ? new[] { "NORMAL", "é", maximum } : new[] { "01", "初", maximum };
                string[] second = source == "song.difficulty" ? new[] { "normal", " NORMAL ", "初心者★１２", "e\u0301", "unavailable" } : new[] { "1", " 01 ", "難易度１２", "１", "unavailable" };
                var resourceCases = new JArray();
                var visibleCases = new JArray();
                addCases(first, "song.first");
                addCases(second, "song.second");
                if (includeEmpty)
                    addCases(new[] { "" }, "song.first");
                variants.Add(new JObject
                {
                    ["id"] = source + ".resource",
                    ["target"] = source,
                    ["property"] = "resource",
                    ["source"] = source,
                    ["default"] = "song.default",
                    ["cases"] = resourceCases,
                });
                variants.Add(new JObject
                {
                    ["id"] = source + ".visible",
                    ["target"] = source,
                    ["property"] = "visible",
                    ["source"] = source,
                    ["default"] = false,
                    ["cases"] = visibleCases,
                });

                void addCases(string[] keys, string resource)
                {
                    foreach (string key in keys)
                    {
                        int index = resourceCases.Count;
                        resourceCases.Add(new JObject { ["id"] = $"{source}.resource-{index}", ["key"] = key, ["resource"] = resource });
                        visibleCases.Add(new JObject { ["id"] = $"{source}.visible-{index}", ["key"] = key, ["value"] = true });
                    }
                }
            }
            return scene;
        }
    }
}
