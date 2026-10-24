// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(0)]
        [TestCase(0.375)]
        [TestCase(1)]
        public void TestBrightnessCodecRoundTripsStaticBindingTrackAndState(double value)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            JObject json = brightnessScene("container", value);
            json["bindings"] = new JArray(brightnessBinding("option.cover"));
            json["tracks"] = new JArray(brightnessTrack(value));
            json["stateMachines"]![0]!["states"]![0]!["set"]![0]!["property"] = "brightness";
            json["stateMachines"]![0]!["states"]![0]!["set"]![0]!["value"] = value;

            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = decodeBrightnessScene(json, manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), brightnessDiagnostics(decoded));
            string encoded = GameplaySkinSceneCodec.EncodeScene(decoded.Value!);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(encoded, manifest);
            Assert.Multiple(() =>
            {
                Assert.That((int)GameplaySkinSceneProperty.HeightPixels, Is.EqualTo(31));
                Assert.That((int)GameplaySkinSceneProperty.Brightness, Is.EqualTo(32));
                Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), brightnessDiagnostics(roundTrip));
                Assert.That(roundTrip.Value!.Bindings.Single().Property, Is.EqualTo(GameplaySkinSceneProperty.Brightness));
                Assert.That(roundTrip.Value.Tracks.Single().Property, Is.EqualTo(GameplaySkinSceneProperty.Brightness));
                Assert.That(roundTrip.Value.StateMachines.Single().States[0].Assignments[0].Property, Is.EqualTo(GameplaySkinSceneProperty.Brightness));
                Assert.That(GameplaySkinSceneCodec.EncodeScene(roundTrip.Value), Is.EqualTo(encoded));
            });
        }

        [TestCase(-0.001)]
        [TestCase(1.001)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestBrightnessCodecRejectsInvalidStaticTrackAndStateValues(double value)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            JObject json = brightnessScene("container", value);
            Assert.That(decodeBrightnessScene(json, manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));

            json = brightnessScene("container");
            json["tracks"] = new JArray(brightnessTrack(value));
            Assert.That(decodeBrightnessScene(json, manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));

            json = brightnessScene("container");
            json["stateMachines"]![0]!["states"]![0]!["set"]![0]!["property"] = "brightness";
            json["stateMachines"]![0]!["states"]![0]!["set"]![0]!["value"] = value;
            Assert.That(decodeBrightnessScene(json, manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [TestCase("sprite", false)]
        [TestCase("sprite", true)]
        [TestCase("container", false)]
        [TestCase("container", true)]
        [TestCase("mask", false)]
        [TestCase("mask", true)]
        public void TestBrightnessPreservesColourAlphaAndVisibilityAcrossPropertyOrderAndState(string type, bool brightnessFirst)
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            JObject json = brightnessScene(type, 0.5, brightnessFirst);
            json["bindings"] = new JArray(brightnessBinding("option.cover"));
            using GameplaySkinLayoutPublication publication = brightnessPublication(texture, json);
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinSceneRuntimeNode visual = runtime(host, "brightness.visual");
            Drawable originalContent = visual.ContentDrawable;
            var artwork = (Sprite)(type == "sprite" ? visual.ContentDrawable : runtime(host, "brightness.texture").ContentDrawable);
            int instances = host.RuntimeInstanceCount;
            assertBrightness(visual, 0.25);
            assertBrightnessAuthorColour(visual, 128, 64, 32, 128);
            Assert.That(visual.TransformDrawable.Alpha, Is.EqualTo(0.6f).Within(0.00001));
            if (type is "container" or "mask")
                Assert.That(visual.ContentDrawable, Is.SameAs(visual.TransformDrawable));

            GameplaySkinInputStateSnapshot lane = publication.PreparedScene.InitialEventState.Inputs.Single();
            publishInput(stream, producer, 1, new GameplaySkinInputStateSnapshot(lane.GroupId, lane.LaneId, true, 1));
            host.ProcessFrame();
            assertBrightness(visual, 0.25);
            assertBrightnessAuthorColour(visual, 32, 128, 64, 192);
            Assert.That(visual.TransformDrawable.Alpha, Is.Zero, "RGB brightness cannot reopen a hidden visual.");

            publishInput(stream, producer, 2, new GameplaySkinInputStateSnapshot(lane.GroupId, lane.LaneId, false, 0));
            host.ProcessFrame();
            assertBrightness(visual, 0.25);
            assertBrightnessAuthorColour(visual, 128, 64, 32, 128);
            Assert.Multiple(() =>
            {
                Assert.That(visual.TransformDrawable.Alpha, Is.EqualTo(0.6f).Within(0.00001));
                Assert.That(visual.ContentDrawable, Is.SameAs(originalContent));
                Assert.That(artwork.Texture, Is.SameAs(texture));
                Assert.That(host.RuntimeInstanceCount, Is.EqualTo(instances));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [TestCase(-2, 0)]
        [TestCase(0, 0)]
        [TestCase(0.25, 0.25)]
        [TestCase(2, 1)]
        public void TestBrightnessClampsFiniteEngineBindingsWithoutChangingOpacity(double time, double expected)
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            JObject json = brightnessScene("sprite");
            json["bindings"] = new JArray(brightnessBinding("timing.time"));
            using GameplaySkinLayoutPublication publication = brightnessPublication(texture, json);
            using var stream = new GameplaySkinEventStream(publication, time, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinSceneRuntimeNode visual = runtime(host, "brightness.visual");
            assertBrightness(visual, expected);
            Assert.That(visual.TransformDrawable.Alpha, Is.EqualTo(0.6f).Within(0.00001));
            Assert.That(((Sprite)visual.ContentDrawable).Texture, Is.SameAs(texture));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase("sprite")]
        [TestCase("container")]
        [TestCase("mask")]
        public void TestMissingBrightnessKeepsLegacyWhiteCarrierAndOriginalMaterial(string type)
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = brightnessPublication(texture, brightnessScene(type));
            using GameplaySkinEventStream stream = createStream(publication);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinSceneRuntimeNode visual = runtime(host, "brightness.visual");
            assertBrightness(visual, 1);
            assertBrightnessAuthorColour(visual, 128, 64, 32, 128);
            Assert.That(visual.TransformDrawable.Alpha, Is.EqualTo(0.6f).Within(0.00001));
            Assert.That(((Sprite)(type == "sprite" ? visual.ContentDrawable : runtime(host, "brightness.texture").ContentDrawable)).Texture, Is.SameAs(texture));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBrightnessElapsedClockRestoresAuthoredOrDefaultValueWithoutStart(bool authored)
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            JObject json = brightnessScene("container", authored ? 0.75 : null);
            json["tracks"] = new JArray(brightnessTrack(0.2));
            using GameplaySkinLayoutPublication publication = brightnessPublication(texture, json);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                host.ProcessFrame();
                GameplaySkinSceneRuntimeNode visual = runtime(host, "brightness.visual");
                assertBrightness(visual, authored ? 0.75 : 1);
                producer.SynchroniseTiming(500, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
                host.ProcessFrame();
                assertBrightness(visual, 0.4);
                producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                assertBrightness(visual, authored ? 0.75 : 1);
                producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
                host.ProcessFrame();
                assertBrightness(visual, 0.2);
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestBrightnessBindingsReachRealSpecialisedClonesAndPreserveTheirMaterialAndLeaseGate()
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            RuntimeFixture fixture = brightnessSpecialisedFixture(texture, 0.75);
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinResolvedMaterialKey key = publication.MaterialSet.Entries.Single().Key;
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(key, new Container())!;
            first.OnApply();
            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(key, new Container())!;
            GameplaySkinSceneRuntimeNode secondRoot = second.RuntimeNodes.Single(n => n.PreparedNode.Source.Id == "brightness.native");
            assertBrightness(secondRoot, 0.75);
            second.OnApply();
            host.ProcessFrame();
            int instances = host.RuntimeInstanceCount;
            foreach (GameplaySkinSpecialisedSceneVisual visual in new[] { first, second })
            {
                GameplaySkinSceneRuntimeNode root = visual.RuntimeNodes.Single(n => n.PreparedNode.Source.Id == "brightness.native");
                assertBrightness(root, 0.25);
                assertBrightnessAuthorColour(root, 128, 64, 32, 128);
                Assert.That(((Sprite)visual.RuntimeNodes.Single(n => n.PreparedNode.Source.Id == "brightness.native-sprite").ContentDrawable).Texture, Is.SameAs(texture));
                Assert.That(visual.Alpha, Is.EqualTo(1));
            }

            publishInput(stream, producer, 1, input(fixture, true));
            host.ProcessFrame();
            assertBrightness(secondRoot, 0.25);
            assertBrightnessAuthorColour(secondRoot, 32, 128, 64, 192);
            Assert.That(secondRoot.TransformDrawable.Alpha, Is.Zero);
            second.OnFree();
            Assert.That(second.Alpha, Is.Zero);
            second.OnApply();
            publishInput(stream, producer, 2, input(fixture, false));
            host.ProcessFrame();
            assertBrightness(secondRoot, 0.25);
            assertBrightnessAuthorColour(secondRoot, 128, 64, 32, 128);
            Assert.Multiple(() =>
            {
                Assert.That(second.Alpha, Is.EqualTo(1));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.6f).Within(0.00001));
                Assert.That(host.RuntimeInstanceCount, Is.EqualTo(instances));
                Assert.That(second.RuntimeNodes.Single(n => n.PreparedNode.Source.Id == "brightness.native"), Is.SameAs(secondRoot));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [TestCase("brightness", false)]
        [TestCase("brightness", true)]
        [TestCase("rotation", false)]
        public void TestBrightnessSpecialisedElapsedTracksKeepPreviewDefaultsAndRestoreAfterReset(string property, bool authored)
        {
            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            RuntimeFixture fixture = brightnessSpecialisedFixture(texture, authored ? 0.75 : null, property);
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(publication.MaterialSet.Entries.Single().Key, new Container())!;
            visual.OnApply();
            GameplaySkinSceneRuntimeNode root = visual.RuntimeNodes.Single(n => n.PreparedNode.Source.Id == "brightness.native");
            try
            {
                host.ProcessFrame();
                assertPreview();
                producer.SynchroniseTiming(500, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
                host.ProcessFrame();
                assertAnimated();
                visual.OnFree();
                visual.OnApply();
                host.ProcessFrame();
                assertAnimated();
                producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                assertPreview();
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }

            void assertPreview()
            {
                assertBrightness(root, authored ? 0.75 : 1);
                Assert.That(root.TransformDrawable.Rotation, Is.EqualTo(37));
            }

            void assertAnimated()
            {
                if (property == "brightness")
                    assertBrightness(root, 0.4);
                else
                {
                    assertBrightness(root, 1);
                    Assert.That(root.TransformDrawable.Rotation, Is.EqualTo(180).Within(0.00001));
                }
            }
        }

        private static void assertBrightness(GameplaySkinSceneRuntimeNode node, double expected)
        {
            var colour = node.RootDrawable.Colour.TopLeft.SRGB;
            Assert.Multiple(() =>
            {
                Assert.That(colour.R, Is.EqualTo(expected).Within(0.00001));
                Assert.That(colour.G, Is.EqualTo(expected).Within(0.00001));
                Assert.That(colour.B, Is.EqualTo(expected).Within(0.00001));
                Assert.That(colour.A, Is.EqualTo(1));
                Assert.That(node.RootDrawable.Alpha, Is.EqualTo(1));
            });
        }

        private static void assertBrightnessAuthorColour(GameplaySkinSceneRuntimeNode node, byte red, byte green, byte blue, byte alpha)
        {
            var colour = node.ContentDrawable.Colour.TopLeft.SRGB;
            Assert.Multiple(() =>
            {
                Assert.That(colour.R, Is.EqualTo(red / 255f).Within(0.00001));
                Assert.That(colour.G, Is.EqualTo(green / 255f).Within(0.00001));
                Assert.That(colour.B, Is.EqualTo(blue / 255f).Within(0.00001));
                Assert.That(colour.A, Is.EqualTo(alpha / 255f).Within(0.00001));
            });
        }

        private static JObject brightnessScene(string type, double? brightness = null, bool brightnessFirst = false)
        {
            var properties = new JObject();
            if (brightnessFirst && brightness.HasValue)
                properties["brightness"] = brightness.Value;
            properties["colour"] = "#80402080";
            properties["opacity"] = 0.6;
            if (!brightnessFirst && brightness.HasValue)
                properties["brightness"] = brightness.Value;
            if (type == "mask")
                properties["mask-mode"] = "ellipse";
            var visual = new JObject
            {
                ["id"] = "brightness.visual",
                ["type"] = type,
                ["target"] = new JObject { ["kind"] = "global" },
                ["blend"] = "alpha",
                ["properties"] = properties,
                ["effects"] = new JArray(),
                ["children"] = new JArray(),
            };
            if (type == "sprite")
                visual["resource"] = "texture.normal";
            else
                visual["children"] = new JArray(new JObject
                {
                    ["id"] = "brightness.texture",
                    ["type"] = "sprite",
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["resource"] = "texture.normal",
                    ["blend"] = "alpha",
                    ["properties"] = new JObject(),
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(),
                });
            var program = new GameplaySkinSceneDocument(
                node("brightness.visual", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null),
                Array.Empty<GameplaySkinSceneTrack>(), new[] { brightnessColourMachine("brightness.visual") },
                Array.Empty<GameplaySkinSceneBinding>(), Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>());
            return new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = new JObject
                {
                    ["id"] = "brightness.hud",
                    ["type"] = "container",
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["slot"] = "hud.text",
                    ["blend"] = "alpha",
                    ["properties"] = new JObject(),
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(visual),
                },
                ["tracks"] = new JArray(),
                ["bindings"] = new JArray(),
                ["variants"] = new JArray(),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
                ["stateMachines"] = JObject.Parse(GameplaySkinSceneCodec.EncodeScene(program))["stateMachines"],
            };
        }

        private static JObject brightnessBinding(string source) => new JObject
        {
            ["id"] = "brightness.binding",
            ["target"] = "brightness.visual",
            ["property"] = "brightness",
            ["source"] = source,
        };

        private static JObject brightnessTrack(double firstValue) => new JObject
        {
            ["id"] = "brightness.track",
            ["type"] = "tween",
            ["target"] = "brightness.visual",
            ["property"] = "brightness",
            ["easing"] = "linear",
            ["loop"] = false,
            ["clock"] = "timing.elapsed",
            ["keyframes"] = new JArray(new JObject { ["id"] = "brightness.begin", ["time"] = 0, ["value"] = firstValue },
                new JObject { ["id"] = "brightness.end", ["time"] = 1000, ["value"] = 0.6 }),
        };

        private static GameplaySkinSceneStateMachine brightnessColourMachine(string targetId) => new GameplaySkinSceneStateMachine(
            "brightness.machine", "brightness.idle",
            new[]
            {
                new GameplaySkinSceneState("brightness.idle", new[]
                {
                    new GameplaySkinSceneStateAssignment("brightness.base-colour", targetId, "colour", GameplaySkinScenePropertyValue.FromString("#80402080")),
                    new GameplaySkinSceneStateAssignment("brightness.base-opacity", targetId, "opacity", GameplaySkinScenePropertyValue.FromNumber(0.6)),
                }),
                new GameplaySkinSceneState("brightness.pressed", new[]
                {
                    new GameplaySkinSceneStateAssignment("brightness.pressed-colour", targetId, "colour", GameplaySkinScenePropertyValue.FromString("#208040c0")),
                    new GameplaySkinSceneStateAssignment("brightness.hide", targetId, "visible", GameplaySkinScenePropertyValue.FromBoolean(false)),
                }),
            },
            new[]
            {
                new GameplaySkinSceneTransition("brightness.press", "brightness.idle", "brightness.pressed", "input.key.down"),
                new GameplaySkinSceneTransition("brightness.release", "brightness.pressed", "brightness.idle", "input.key.up"),
            });

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decodeBrightnessScene(JObject json, GameplaySkinSceneManifest manifest)
            => GameplaySkinSceneCodec.DecodeScene(json.ToString(Formatting.None), manifest);

        private static string brightnessDiagnostics(GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result)
            => string.Join(",", result.Diagnostics.Select(d => d.Code));

        private static GameplaySkinLayoutPublication brightnessPublication(Texture texture, JObject json)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            GameplaySkinOptionValues options = GameplaySkinOptionValues.Resolve(manifest.Options, null, "mania", "1k");
            LayoutFixture layout = createLayout(optionValues: options);
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot,
                new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = decodeBrightnessScene(json, manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), brightnessDiagnostics(decoded));
            GameplaySkinSceneDocument document = decoded.Value!;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("mania.hud").Rect;
            GameplaySkinPreparedSceneResource[] resources = manifest.Resources
                .Select(resource => new GameplaySkinPreparedSceneResource(resource, "brightness-test", 2, 2, texture)).ToArray();
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "brightness-test", manifest, document,
                resources, new[] { prepareNode(document.Root, true) });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);

            GameplaySkinPreparedSceneNode prepareNode(GameplaySkinSceneNode node, bool isRoot)
            {
                GameplaySkinPreparedSceneResource? resource = node.ResourceId == null ? null : resources.Single(r => r.Id == node.ResourceId);
                // Static sprites need their final prepared texture. The options fixture instead exercises a
                // resource variant which supplies it later, and is not the static material path tested here.
                return new GameplaySkinPreparedSceneNode(node.Id, node, node.Target, rect, global,
                    isRoot ? GameplaySkinSlotCatalog.TextHud : null, resource, resource?.Texture,
                    node.Children.Select(child => prepareNode(child, false)), GameplaySkinSceneLayer.HudForeground, owner, false);
            }
        }

        private static RuntimeFixture brightnessSpecialisedFixture(Texture texture, double? brightness, string? animatedProperty = null)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            GameplaySkinOptionValues options = GameplaySkinOptionValues.Resolve(manifest.Options, null, "mania", "1k");
            LayoutFixture layout = createLayout(optionValues: options);
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.Note, laneTarget, texture, source()) });
            GameplaySkinSceneResource resource = manifest.Resources.Single(r => r.Id == "texture.normal");
            var preparedResource = new GameplaySkinPreparedSceneResource(resource, "brightness-test", 2, 2, texture);
            GameplaySkinSceneTarget lane = target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0);
            var properties = new Dictionary<string, GameplaySkinScenePropertyValue>
            {
                ["colour"] = GameplaySkinScenePropertyValue.FromString("#80402080"),
                ["opacity"] = GameplaySkinScenePropertyValue.FromNumber(0.6),
                ["rotation"] = GameplaySkinScenePropertyValue.FromNumber(37),
            };
            if (brightness.HasValue)
                properties["brightness"] = GameplaySkinScenePropertyValue.FromNumber(brightness.Value);
            GameplaySkinSceneNode sprite = node("brightness.native-sprite", GameplaySkinSceneNodeType.Sprite, lane, null, resource.Id);
            GameplaySkinSceneNode root = withChildren(node("brightness.native", GameplaySkinSceneNodeType.Container, lane, GameplaySkinSlotCatalog.Note.Id, null, properties), sprite);
            var tracks = new List<GameplaySkinSceneTrack>();
            var bindings = new List<GameplaySkinSceneBinding>();
            if (animatedProperty == null)
                bindings.Add(new GameplaySkinSceneBinding("brightness.native-binding", root.Id, "brightness", "option.cover"));
            else
                tracks.Add(new GameplaySkinSceneTrack("brightness.native-track", GameplaySkinSceneTrackType.Tween, root.Id, animatedProperty,
                    GameplaySkinSceneEasing.Linear, false, new[]
                    {
                        new GameplaySkinSceneKeyframe("brightness.native-begin", 0, GameplaySkinScenePropertyValue.FromNumber(animatedProperty == "brightness" ? 0.2 : 0)),
                        new GameplaySkinSceneKeyframe("brightness.native-end", 1000, GameplaySkinScenePropertyValue.FromNumber(animatedProperty == "brightness" ? 0.6 : 360)),
                    }, clockSourceId: "timing.elapsed"));
            var document = new GameplaySkinSceneDocument(root, tracks, new[] { brightnessColourMachine(root.Id) }, bindings,
                Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "brightness-specialised", manifest, document,
                new[] { preparedResource }, new[]
                {
                    prepared(root, layout.LaneRect, laneTarget, GameplaySkinSlotCatalog.Note, null,
                        prepared(sprite, layout.LaneRect, laneTarget, null, preparedResource)),
                });
            return new RuntimeFixture(GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene),
                layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);
        }
    }
}
