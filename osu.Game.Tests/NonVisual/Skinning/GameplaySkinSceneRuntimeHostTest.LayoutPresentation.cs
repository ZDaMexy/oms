// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase("bms", "7k", "p1", false, "p1")]
        [TestCase("bms", "7k", "p2", false, "p2")]
        [TestCase("bms", "14k", "center-p1", true, "p2")]
        [TestCase("mania", "7k", "p2", false, "p2")]
        [TestCase("mania", "7k-7k", "mania-dual", true, "p2")]
        public void TestPresentationBindingsAndVariantsUseTheAppliedContext(string ruleset, string keymode, string applied, bool dual, string saved)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            var preferences = new Dictionary<string, string> { ["layout"] = saved };
            using GameplaySkinLayoutPublication publication = presentationPublication(texture, ruleset, keymode, applied, dual, preferences);
            Assert.That(publication.Snapshot.Context.Topology.GroupsInLogicalOrder.Count, Is.EqualTo(dual ? 2 : 1));
            if (dual)
            {
                Assert.That(publication.Snapshot.Context.OptionValues.Presentation, Is.Null, "An SP-only saved P2 choice is not a DP presentation authority.");
                Assert.That(publication.Snapshot.Context.OptionValues.Values.ContainsKey("layout"), Is.False);
            }

            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertPresentation(host);
            long bindings = host.BindingApplicationCount;
            long variants = host.VariantApplicationCount;
            preferences["layout"] = saved == "p1" ? "p2" : "p1";
            producer.SynchroniseTiming(100, new GameplaySkinTimingStateSnapshot(1, 0, 120, false, 1));
            host.ProcessFrame();
            assertPresentation(host);
            Assert.That(host.BindingApplicationCount, Is.EqualTo(bindings), "Static layout presentation must not be resampled on timing changes.");
            Assert.That(host.VariantApplicationCount, Is.EqualTo(variants));
            using (var late = new GameplaySkinSceneRuntimeHost(publication, stream))
            {
                late.ProcessFrame();
                assertPresentation(late);
            }
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            assertPresentation(host);
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();

            void assertPresentation(GameplaySkinSceneRuntimeHost current)
            {
                Assert.That(((SpriteText)runtime(current, "presentation.text").ContentDrawable).Text.ToString(), Is.EqualTo(applied));
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.Alpha, Is.EqualTo(applied == "p2" ? 1 : 0));
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.X, Is.EqualTo(applied == "p2" ? 0.5 : -0.25));
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.Y, Is.EqualTo(dual ? 0.5 : 0.21749).Within(0.00001));
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.Width, Is.EqualTo(applied == "p2" ? 0.25 : 0.8).Within(0.00001));
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.RelativePositionAxes.HasFlag(Axes.X), Is.True);
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.RelativePositionAxes.HasFlag(Axes.Y), Is.True);
                Assert.That(runtime(current, "presentation.p2").TransformDrawable.RelativeSizeAxes.HasFlag(Axes.X), Is.True);
            }
        }

        [TestCase("x", -4, 4)]
        [TestCase("y", -4, 4)]
        [TestCase("width", 0, 4)]
        public void TestPresentationGeometryVariantsRoundTripAndPrepareTypedNumbers(string property, double first, double second)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            JObject json = presentationScene();
            JToken variant = json["variants"]!.Single(item => item["property"]!.Value<string>() == property);
            string key = variant["cases"]![0]!["key"]!.Value<string>()!;
            variant["default"] = first;
            variant["cases"]![0]!["value"] = second;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(decoded.Value!), manifest);
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            GameplaySkinSceneVariant selected = roundTrip.Value!.Variants.Single(item => GameplaySkinSceneVocabulary.PropertyId(item.Property) == property);
            var prepared = new GameplaySkinPreparedSceneVariant(selected, new Dictionary<string, GameplaySkinPreparedSceneResource>());
            Assert.That(prepared.DefaultValue.Kind, Is.EqualTo(GameplaySkinScenePropertyValueKind.Number));
            Assert.That(prepared.DefaultValue.NumberValue, Is.EqualTo(first));
            Assert.That(prepared.SelectValue(key).NumberValue, Is.EqualTo(second));
            Assert.That(prepared.SelectValue("custom-fallback"), Is.SameAs(prepared.DefaultValue), "Fallback retains its prepared numeric value.");
            Assert.That(prepared.SelectValue(key), Is.SameAs(prepared.Cases.Single(item => item.Key == key).Value), "Selection does not parse or allocate a new value.");
        }

        [TestCase("x", "default", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("x", "case", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("y", "default", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("y", "case", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("y", "default", "below-range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("y", "case", "below-range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("width", "default", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("width", "case", "range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("width", "default", "negative", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("width", "case", "negative", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("x", "default", "boolean", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("x", "case", "string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("y", "default", "boolean", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("y", "case", "string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("width", "default", "string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("width", "case", "boolean", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("x", "default", "nan", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        [TestCase("x", "case", "infinity", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        [TestCase("y", "default", "nan", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        [TestCase("y", "case", "infinity", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        [TestCase("width", "default", "infinity", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        [TestCase("width", "case", "nan", GameplaySkinSceneDiagnosticCode.InvalidJson)]
        public void TestPresentationGeometryVariantsRejectInvalidAuthoredValues(string property, string field, string change, GameplaySkinSceneDiagnosticCode diagnostic)
        {
            JObject json = presentationScene();
            JToken variant = json["variants"]!.Single(item => item["property"]!.Value<string>() == property);
            JToken value = change switch
            {
                "range" => new JValue(4.1),
                "below-range" => new JValue(-4.1),
                "negative" => new JValue(-0.1),
                "boolean" => new JValue(true),
                "string" => new JValue("0.5"),
                "nan" => new JValue(double.NaN),
                "infinity" => new JValue(double.PositiveInfinity),
                _ => throw new ArgumentOutOfRangeException(nameof(change)),
            };
            if (field == "default")
                variant["default"] = value;
            else
                variant["cases"]![0]!["value"] = value;
            string encoded = JsonConvert.SerializeObject(json, new JsonSerializerSettings { FloatFormatHandling = FloatFormatHandling.Symbol });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(encoded, GameplaySkinOptionsTest.DecodeManifest());
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(diagnostic));
        }

        [TestCase("height")]
        [TestCase("opacity")]
        [TestCase("scale-x")]
        [TestCase("scale-y")]
        [TestCase("rotation")]
        [TestCase("z")]
        public void TestPresentationGeometryVariantDoesNotAdmitOtherNumericProperties(string property)
        {
            JObject json = presentationScene();
            json["variants"]![1]!["property"] = property;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), GameplaySkinOptionsTest.DecodeManifest());
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.UnknownProperty));
        }

        [TestCase("layout.group", "test.group")]
        [TestCase("layout.ruleset", "bms")]
        [TestCase("combo.active", "true")]
        [TestCase("option.layout", "p1")]
        public void TestPresentationYVariantDoesNotAdmitOtherSources(string source, string key)
        {
            JObject json = presentationScene();
            JToken variant = json["variants"]!.Single(item => item["property"]!.Value<string>() == "y");
            variant["source"] = source;
            variant["cases"] = new JArray(new JObject { ["id"] = "presentation.y-other", ["key"] = key, ["value"] = 0.5 });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), GameplaySkinOptionsTest.DecodeManifest());
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.UnknownProperty));
        }

        [Test]
        public void TestPresentationYVariantDoesNotMakePresentationANumericBinding()
        {
            JObject json = presentationScene();
            json["bindings"] = new JArray(new JObject
            {
                ["id"] = "presentation.y-binding", ["target"] = "presentation.p2", ["property"] = "y", ["source"] = "layout.presentation",
            });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), GameplaySkinOptionsTest.DecodeManifest());
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue));
        }

        [TestCase("1p", true)]
        [TestCase("custom.layout-42", true)]
        [TestCase("center-p1", true)]
        [TestCase("", false)]
        [TestCase("P2", false)]
        [TestCase("custom/layout", false)]
        [TestCase("two players", false)]
        public void TestPresentationVariantKeysUseTheExistingContextTokenContract(string token, bool valid)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            JObject json = presentationScene();
            json["variants"]![0]!["cases"]![0]!["key"] = token;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(valid ? GameplaySkinSceneDecodeStatus.Valid : GameplaySkinSceneDecodeStatus.Invalid));
            if (!valid)
                Assert.That(decoded.Diagnostics.Select(d => d.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue));
        }

        private static JObject presentationScene() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1",
             "root":{"id":"presentation.root","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
               {"id":"presentation.text","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","font-size":24},"effects":[],"children":[]},
               {"id":"presentation.p2","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"x":0.1,"width":0.9},"effects":[],"children":[]}]},
             "tracks":[],"bindings":[{"id":"presentation.binding","target":"presentation.text","property":"text","source":"layout.presentation"}],
             "templates":[],"instances":[],"stateMachines":[],
             "variants":[{"id":"presentation.variant","target":"presentation.p2","property":"visible","source":"layout.presentation","default":false,
               "cases":[{"id":"presentation.p2-case","key":"p2","value":true}]},
               {"id":"presentation.x","target":"presentation.p2","property":"x","source":"layout.presentation","default":-0.25,
                "cases":[{"id":"presentation.x-p2","key":"p2","value":0.5}]},
               {"id":"presentation.width","target":"presentation.p2","property":"width","source":"layout.presentation","default":0.8,
                "cases":[{"id":"presentation.width-p2","key":"p2","value":0.25}]},
               {"id":"presentation.y","target":"presentation.p2","property":"y","source":"layout.presentation","default":0.21749,
                "cases":[{"id":"presentation.y-dp","key":"center-p1","value":0.5},{"id":"presentation.y-mania-dual","key":"mania-dual","value":0.5}]}]}
            """);

        private static GameplaySkinLayoutPublication presentationPublication(Texture texture, string ruleset, string keymode, string presentation, bool dual,
                                                                             IReadOnlyDictionary<string, string> preferences)
        {
            JObject manifestJson = GameplaySkinOptionsTest.ManifestJson();
            manifestJson["options"]![0]!["rulesets"] = new JArray("bms", "mania");
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest(manifestJson);
            GameplaySkinOptionValues options = GameplaySkinOptionValues.Resolve(manifest.Options, preferences, ruleset, keymode);
            GameplaySkinLayoutSnapshot original = dual ? createDualStageLayout().Snapshot : createLayout(ruleset).Snapshot;
            GameplaySkinLayoutContext before = original.Context;
            GameplaySkinLaneTopologySnapshot topology = before.Topology;
            GameplaySkinLayoutGroup[] groups = original.GroupsInLogicalOrder.ToArray();
            GameplaySkinLayoutLane[] lanes = original.LanesInLogicalOrder.ToArray();
            if (ruleset == "mania")
            {
                // Mania's public slot contract checks actual column counts against its complete stage vector.
                // The general scene helper has only one column per stage; build the declared 7K/7K-7K here.
                groups = groups.Select((group, stageIndex) => new GameplaySkinLayoutGroup(
                    GameplaySkinLaneTopologyGroup.Create(group.TopologyGroup.Identity, stageIndex, stageIndex,
                        Enumerable.Range(0, 7).Select(laneIndex => GameplaySkinLaneTopologyEntry.Create(
                            GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create($"presentation.g{stageIndex}.k{laneIndex}"), group.TopologyGroup.Identity, GameplaySkinLaneRole.Key),
                            stageIndex * 7 + laneIndex, laneIndex, stageIndex * 7 + laneIndex, laneIndex))), group.Rect)).ToArray();
                topology = GameplaySkinLaneTopologySnapshot.Create(groups.Select(group => group.TopologyGroup));
                lanes = groups.SelectMany(group => group.TopologyGroup.LanesInLogicalOrder.Select(lane => new GameplaySkinLayoutLane(lane,
                    GameplaySkinLayoutRect.Create(group.Rect.X + group.Rect.Width * lane.GroupLocalLogicalIndex / 7, group.Rect.Y,
                        group.Rect.Width / 7, group.Rect.Height)))).ToArray();
            }
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(ruleset, before.NativeContextId, keymode, presentation, topology,
                before.ScreenBounds, before.SafeBounds, before.AspectRatio, before.DpiScale, before.ScrollDirection, before.PackageRevision,
                before.TopologyRevision, before.LayoutRevision, before.RenderPixelWidth, before.RenderPixelHeight, options);
            GameplaySkinLayoutSurface[] surfaces = original.Surfaces.Any(surface => surface.Id == ruleset + ".hud") ? original.Surfaces.ToArray()
                : original.Surfaces.Append(new GameplaySkinLayoutSurface(ruleset + ".hud", before.SafeBounds, 60, false, false)).ToArray();
            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(context, groups, lanes, surfaces, original.BgaViewports);
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(presentationScene().ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            GameplaySkinLayoutRect rect = snapshot.GetSurface(ruleset + ".hud").Rect;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            GameplaySkinPreparedSceneNode[] children = document.Root.Children.Select(child => new GameplaySkinPreparedSceneNode(child.Id, child, child.Target, rect,
                global, null, null, null, Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false)).ToArray();
            var scene = new GameplaySkinPreparedScene(snapshot, materials, "presentation-test", manifest, document, Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[] { prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null, children) });
            return GameplaySkinLayoutPublication.Create(new Adapter(snapshot), materials, scene);
        }
    }
}
