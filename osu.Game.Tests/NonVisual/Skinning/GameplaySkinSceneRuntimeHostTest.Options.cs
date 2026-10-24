// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(-2, 0.8)]
        [TestCase(-1, 0.4)]
        [TestCase(0, 0)]
        [TestCase(1, -0.1)]
        [TestCase(10, -1)]
        public void TestSignedNumericOptionClockKeepsPositionAcrossPauseResetAndPublication(double selected, double expected)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            JToken definition = manifest["options"]![3]!;
            definition["min"] = -2;
            definition["max"] = 10;
            definition["step"] = 1;
            definition["default"] = 0;
            JObject scene = optionScene();
            scene["tracks"] = new JArray(JObject.Parse("""
                {"id":"option.position","type":"tween","target":"option.hud","property":"y","easing":"linear","loop":false,"clock":"option.cover",
                 "keyframes":[{"id":"option.position-low","time":-2,"value":0.8},
                              {"id":"option.position-zero","time":0,"value":0},
                              {"id":"option.position-high","time":10,"value":-1}]}
                """));
            var saved = new Dictionary<string, string> { ["cover"] = selected.ToString(CultureInfo.InvariantCulture) };
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate, saved, manifest, scene);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                host.ProcessFrame();
                assertPosition(host, expected);
                Assert.That(publication.PreparedScene.Program.Tracks.Single().Keyframes.Select(frame => frame.Time), Is.EqualTo(new[] { -2.0, 0, 10 }));

                saved["cover"] = "0";
                producer.SynchroniseTiming(500, new GameplaySkinTimingStateSnapshot(1, 0, 120, true, 1, startTime: 0));
                host.ProcessFrame();
                host.ProcessFrame();
                assertPosition(host, expected);
                producer = producer.Reset(1500, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Seek);
                host.ProcessFrame();
                assertPosition(host, expected);
                producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                assertPosition(host, expected);
                using (var reattached = new GameplaySkinSceneRuntimeHost(publication, stream))
                {
                    reattached.ProcessFrame();
                    assertPosition(reattached, expected);
                }

                saved.Remove("cover");
                using GameplaySkinLayoutPublication restored = optionPublication(normal, alternate, saved, manifest, scene);
                using GameplaySkinEventStream restoredStream = createStream(restored);
                using var restoredHost = new GameplaySkinSceneRuntimeHost(restored, restoredStream);
                restoredHost.ProcessFrame();
                assertPosition(restoredHost, 0);
                assertPosition(host, expected);
            }
            finally
            {
                producer.Dispose();
            }

            void assertPosition(GameplaySkinSceneRuntimeHost current, double position)
            {
                GameplaySkinSceneRuntimeNode root = runtime(current, "option.hud");
                Assert.That(root.TransformDrawable.Y, Is.EqualTo(position).Within(0.00001));
                Assert.That(root.TransformDrawable.RelativePositionAxes.HasFlag(Axes.Y), Is.True);
                Assert.That(((Sprite)runtime(current, "option.note").ContentDrawable).Texture, Is.SameAs(normal));
                Assert.That(current.RuntimeFaults, Is.Empty);
            }
        }

        internal static GameplaySkinLayoutPublication CreateIndependentOptionGatePublication(Texture texture)
        {
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            JToken definition = manifest["options"]![3]!;
            definition["min"] = -2;
            definition["max"] = 10;
            definition["step"] = 1;
            definition["default"] = 0;
            JObject scene = optionScene();
            scene["tracks"] = new JArray(JObject.Parse("""
                {"id":"option.position","type":"tween","target":"option.hud","property":"y","easing":"linear","loop":false,"clock":"option.cover",
                 "keyframes":[{"id":"option.position-low","time":-2,"value":0.8},
                              {"id":"option.position-zero","time":0,"value":0},
                              {"id":"option.position-high","time":10,"value":-1}]}
                """));
            JToken root = scene["root"]!;
            JToken sprite = root["children"]![0]!;
            sprite["properties"] = new JObject { ["colour"] = "#80402080" };
            JToken text = root["children"]![3]!;
            root["properties"] = new JObject { ["opacity"] = 0.4, ["brightness"] = 0.5 };
            JObject gate = JObject.Parse("""
                {"id":"option.gate","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[
                 {"id":"option.viewport","type":"clip","target":{"kind":"global"},"blend":"inherit","properties":{"width":0.75,"height":0.5,"clip-mode":"bounds"},"effects":[],"children":[
                  {"id":"option.row","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{"x":0.25,"y":0.3},"effects":[],"children":[]}]}]}
                """);
            gate["children"]![0]!["children"]![0]!["children"] = new JArray(sprite, text);
            root["children"] = new JArray(gate);
            scene["bindings"] = new JArray(JObject.Parse("""
                {"id":"option.gate-input","target":"option.gate","property":"visible","source":"input.pressed"}
                """));
            scene["variants"] = new JArray();
            return optionPublication(texture, texture,
                new Dictionary<string, string> { ["cover"] = "-1" }, manifest, scene);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFrozenAuthorChoicesDrivePreparedTextureVisibilityAndCoverHeight(bool overridden)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            var saved = new Dictionary<string, string>();
            if (overridden)
            {
                saved["notes"] = "alternate";
                saved["bomb"] = "false";
                saved["cover"] = "0.5";
            }
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate, saved);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertChoices();

            // Neither mutable saved preferences nor clock/epoch events may change this root's frozen selection.
            saved["notes"] = overridden ? "normal" : "alternate";
            saved["bomb"] = overridden ? "true" : "false";
            saved["cover"] = overridden ? "0.25" : "0.5";
            producer.SynchroniseTiming(500, new GameplaySkinTimingStateSnapshot(1, 0, 120, true, 1));
            host.ProcessFrame();
            assertChoices();
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Rewind);
            host.ProcessFrame();
            assertChoices();
            using (var reattached = new GameplaySkinSceneRuntimeHost(publication, stream))
            {
                reattached.ProcessFrame();
                Assert.That(((Sprite)runtime(reattached, "option.note").ContentDrawable).Texture, Is.SameAs(overridden ? alternate : normal));
                Assert.That(reattached.RuntimeFaults, Is.Empty);
            }
            producer.Dispose();

            using GameplaySkinLayoutPublication next = optionPublication(normal, alternate, saved);
            using var nextStream = new GameplaySkinEventStream(next, 0, next.PreparedScene.InitialEventState);
            using var nextHost = new GameplaySkinSceneRuntimeHost(next, nextStream);
            nextHost.ProcessFrame();
            Assert.That(((Sprite)runtime(nextHost, "option.note").ContentDrawable).Texture, Is.SameAs(overridden ? normal : alternate));
            Assert.That(nextHost.RuntimeFaults, Is.Empty);

            void assertChoices()
            {
                Assert.Multiple(() =>
                {
                    Assert.That(((Sprite)runtime(host, "option.note").ContentDrawable).Texture, Is.SameAs(overridden ? alternate : normal));
                    Assert.That(runtime(host, "option.bomb").TransformDrawable.Alpha, Is.EqualTo(overridden ? 0 : 1));
                    Assert.That(runtime(host, "option.cover").TransformDrawable.Height, Is.EqualTo(overridden ? 0.5 : 0.25).Within(0.00001));
                    Assert.That(((SpriteText)runtime(host, "option.text").ContentDrawable).Text.ToString(), Is.EqualTo(overridden ? "alternate" : "normal"));
                    Assert.That(host.RuntimeFaults, Is.Empty);
                    Assert.That(publication.Snapshot.Context.OptionValues.Values, Has.Count.EqualTo(3), "The BMS-only layout option is absent in mania.");
                });
            }
        }

        [Test]
        public void TestInapplicableOptionsRetainAuthoredBindingValueAndVariantDefault()
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            foreach (JToken option in (JArray)manifest["options"]!)
                option["rulesets"] = new JArray("bms");
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate,
                new Dictionary<string, string> { ["notes"] = "alternate", ["bomb"] = "false", ["cover"] = "0.5" }, manifest);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.Multiple(() =>
            {
                Assert.That(publication.Snapshot.Context.OptionValues, Is.SameAs(GameplaySkinOptionValues.Empty));
                Assert.That(((Sprite)runtime(host, "option.note").ContentDrawable).Texture, Is.SameAs(normal));
                Assert.That(runtime(host, "option.bomb").TransformDrawable.Alpha, Is.EqualTo(1));
                Assert.That(runtime(host, "option.cover").TransformDrawable.Height, Is.EqualTo(0.15).Within(0.00001));
                Assert.That(((SpriteText)runtime(host, "option.text").ContentDrawable).Text.ToString(), Is.EqualTo("author-base"));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestToggleCanSelectVisibleVariantAndNumericBitmapText(bool enabled)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            manifest["bitmapFonts"] = new JArray(new JObject
            {
                ["id"] = "option.digits",
                ["lineHeight"] = 2,
                ["glyphs"] = new JArray("0123456789".Select(character => new JObject
                {
                    ["character"] = character.ToString(),
                    ["resource"] = "texture.normal",
                    ["advance"] = 2,
                })),
            });
            JObject scene = optionScene();
            scene["root"]!["children"]![3]!["properties"] = new JObject { ["text"] = "0", ["font-size"] = 12, ["bitmap-font"] = "option.digits", ["format"] = "integer" };
            scene["bindings"]![2]!["source"] = "option.bomb";
            ((JArray)scene["bindings"]!).RemoveAt(0);
            ((JArray)scene["variants"]!).Add(new JObject
            {
                ["id"] = "option.bomb-variant",
                ["target"] = "option.bomb",
                ["property"] = "visible",
                ["source"] = "option.bomb",
                ["default"] = false,
                ["cases"] = new JArray(new JObject { ["id"] = "option.bomb-on", ["key"] = "true", ["value"] = true }),
            });
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate,
                new Dictionary<string, string> { ["bomb"] = enabled ? "true" : "false" }, manifest, scene);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.Multiple(() =>
            {
                Assert.That(((GameplaySkinBitmapText)runtime(host, "option.text").ContentDrawable).AuthorText, Is.EqualTo(enabled ? "1" : "0"));
                Assert.That(runtime(host, "option.bomb").TransformDrawable.Alpha, Is.EqualTo(enabled ? 1 : 0));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [Test]
        public void TestUnselectedOptionBranchMustStillHaveItsExactPreparedTexture()
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            // Defaults select normal, but the alternate remains part of the same immutable program and budget.
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                optionPublication(normal, alternate, new Dictionary<string, string>(), omitAlternateResource: true));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.UnknownResource));
        }

        [TestCase("bms")]
        [TestCase("mania")]
        public void TestRulesetContextSelectsAuthorPresentationWithoutAUserOption(string ruleset)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            manifest.Remove("options");
            JObject scene = optionScene();
            scene["bindings"] = new JArray(new JObject
            {
                ["id"] = "option.ruleset-name",
                ["target"] = "option.text",
                ["property"] = "text",
                ["source"] = "layout.ruleset",
            });
            scene["variants"] = new JArray(new JObject
            {
                ["id"] = "option.ruleset-texture",
                ["target"] = "option.note",
                ["property"] = "resource",
                ["source"] = "layout.ruleset",
                ["default"] = "texture.normal",
                ["cases"] = new JArray(new JObject { ["id"] = "option.mania-texture", ["key"] = "mania", ["resource"] = "texture.alternate" }),
            }, new JObject
            {
                ["id"] = "option.ruleset-visible",
                ["target"] = "option.bomb",
                ["property"] = "visible",
                ["source"] = "layout.ruleset",
                ["default"] = false,
                ["cases"] = new JArray(new JObject { ["id"] = "option.mania-visible", ["key"] = "mania", ["value"] = true }),
            });
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate, new Dictionary<string, string>(), manifest, scene, rulesetId: ruleset);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.Multiple(() =>
            {
                Assert.That(((SpriteText)runtime(host, "option.text").ContentDrawable).Text.ToString(), Is.EqualTo(ruleset));
                Assert.That(((Sprite)runtime(host, "option.note").ContentDrawable).Texture, Is.SameAs(ruleset == "mania" ? alternate : normal));
                Assert.That(runtime(host, "option.bomb").TransformDrawable.Alpha, Is.EqualTo(ruleset == "mania" ? 1 : 0));
                Assert.That(publication.PreparedScene.Program.Bindings.Single().StateFamily, Is.EqualTo(GameplaySkinSceneStateFamily.Layout));
                Assert.That(publication.PreparedScene.Program.Variants.Select(variant => variant.StateFamily), Is.All.EqualTo(GameplaySkinSceneStateFamily.Layout));
                Assert.That(publication.Snapshot.Context.OptionValues, Is.SameAs(GameplaySkinOptionValues.Empty));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestToggleNumericProjectionAndNumberBindingKeepExistingPropertyRanges(bool enabled)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            manifest["options"]![3]!["min"] = -100;
            manifest["options"]![3]!["max"] = 100;
            manifest["options"]![3]!["step"] = 10;
            manifest["options"]![3]!["default"] = 100;
            JObject scene = optionScene();
            // Visible uses the typed boolean projection; opacity uses its explicit numeric 0/1 projection.
            ((JArray)scene["bindings"]!).Add(new JObject
            {
                ["id"] = "option.toggle-opacity",
                ["target"] = "option.note",
                ["property"] = "opacity",
                ["source"] = "option.bomb",
            });
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate,
                new Dictionary<string, string> { ["bomb"] = enabled ? "true" : "false" }, manifest, scene);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.Multiple(() =>
            {
                Assert.That(runtime(host, "option.note").TransformDrawable.Alpha, Is.EqualTo(enabled ? 1 : 0));
                Assert.That(runtime(host, "option.cover").TransformDrawable.Height, Is.EqualTo(GameplaySkinSceneBudgets.MAX_RELATIVE_SIZE));
                Assert.That(publication.Snapshot.Context.OptionValues.Values["cover"].NumberValue, Is.EqualTo(100), "Clamping a visual does not mutate the frozen choice.");
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [TestCase(false, "0.25")]
        [TestCase(true, "-0.25")]
        public void TestNumericOptionUsesAuthorBitmapSymbols(bool signed, string expected)
        {
            using Texture normal = new DummyRenderer().CreateTexture(2, 2);
            using Texture alternate = new DummyRenderer().CreateTexture(3, 3);
            JObject manifest = optionFontManifest("0123456789.-");
            if (signed)
            {
                manifest["options"]![3]!["min"] = -1;
                manifest["options"]![3]!["default"] = -0.25;
            }
            JObject scene = optionScene();
            scene["root"]!["children"]![3]!["properties"] = new JObject { ["text"] = "0", ["font-size"] = 12, ["bitmap-font"] = "option.digits", ["format"] = "fixed-2" };
            scene["bindings"]![2]!["source"] = "option.cover";
            using GameplaySkinLayoutPublication publication = optionPublication(normal, alternate, new Dictionary<string, string>(), manifest, scene);
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(((GameplaySkinBitmapText)runtime(host, "option.text").ContentDrawable).AuthorText, Is.EqualTo(expected));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase("fractional-without-dot", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("negative-without-sign", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("choice-is-not-numeric", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("ruleset-is-not-numeric", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        public void TestOptionBitmapBindingsRejectUnrepresentableDeclaredValues(string change, GameplaySkinSceneDiagnosticCode code)
        {
            JObject manifestJson = optionFontManifest("0123456789");
            JObject scene = optionScene();
            scene["root"]!["children"]![3]!["properties"] = new JObject { ["text"] = "0", ["font-size"] = 12, ["bitmap-font"] = "option.digits" };
            scene["bindings"]![2]!["source"] = "option.cover";
            if (change == "negative-without-sign")
            {
                manifestJson["options"]![3]!["min"] = -1;
                scene["root"]!["children"]![3]!["properties"]!["format"] = "integer";
            }
            else if (change == "choice-is-not-numeric")
                scene["bindings"]![2]!["source"] = "option.notes";
            else if (change == "ruleset-is-not-numeric")
                scene["bindings"]![2]!["source"] = "layout.ruleset";
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(Formatting.None), GameplaySkinOptionsTest.DecodeManifest(manifestJson));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(code));
        }

        [TestCase("unknown-source", GameplaySkinSceneDiagnosticCode.UnknownBindingSource)]
        [TestCase("choice-number", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("number-boolean", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("static-layout", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("numeric-variant", GameplaySkinSceneDiagnosticCode.UnknownBindingSource)]
        [TestCase("unknown-choice", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("unselected-missing-resource", GameplaySkinSceneDiagnosticCode.UnknownResource)]
        [TestCase("ruleset-number", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("unknown-ruleset-case", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        public void TestOptionSourcesStayWithinExistingTypedPropertyAndResourceContracts(string change, GameplaySkinSceneDiagnosticCode code)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest();
            JObject scene = optionScene();
            switch (change)
            {
                case "unknown-source": scene["bindings"]![0]!["source"] = "option.not-declared"; break;
                case "choice-number": scene["bindings"]![1]!["source"] = "option.notes"; break;
                case "number-boolean": scene["bindings"]![0]!["source"] = "option.cover"; break;
                case "static-layout": scene["bindings"]![0]!["property"] = "layout"; break;
                case "numeric-variant": scene["variants"]![0]!["source"] = "option.cover"; break;
                case "unknown-choice": scene["variants"]![0]!["cases"]![0]!["key"] = "undeclared"; break;
                case "unselected-missing-resource": scene["variants"]![0]!["cases"]![0]!["resource"] = "texture.missing"; break;
                case "ruleset-number": scene["bindings"]![1]!["source"] = "layout.ruleset"; break;
                case "unknown-ruleset-case": scene["variants"]![0]!["source"] = "layout.ruleset"; scene["variants"]![0]!["cases"]![0]!["key"] = "taiko"; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(Formatting.None), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(d => d.Code), Does.Contain(code));
        }

        private static GameplaySkinSceneRuntimeNode runtime(GameplaySkinSceneRuntimeHost host, string id)
        {
            Assert.That(host.TryGetRuntimeNode(id, out GameplaySkinSceneRuntimeNode? node), Is.True, string.Join(",", host.RuntimeFaults.Select(fault => fault.Code)));
            return node!;
        }

        private static JObject optionScene() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1",
             "root":{"id":"option.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
               {"id":"option.note","type":"sprite","target":{"kind":"global"},"resource":"texture.normal","blend":"alpha","properties":{},"effects":[],"children":[]},
               {"id":"option.bomb","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"visible":true},"effects":[],"children":[]},
               {"id":"option.cover","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"height":0.15},"effects":[],"children":[]},
               {"id":"option.text","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"author-base","font-size":12},"effects":[],"children":[]}]},
             "tracks":[],"stateMachines":[],"templates":[],"instances":[],
             "bindings":[
               {"id":"option.bomb-binding","target":"option.bomb","property":"visible","source":"option.bomb"},
               {"id":"option.cover-binding","target":"option.cover","property":"height","source":"option.cover"},
               {"id":"option.text-binding","target":"option.text","property":"text","source":"option.notes"}],
             "variants":[{"id":"option.note-variant","target":"option.note","property":"resource","source":"option.notes","default":"texture.normal",
               "cases":[{"id":"option.note-alternate","key":"alternate","resource":"texture.alternate"}]}]}
            """);

        private static JObject optionFontManifest(string characters)
        {
            JObject manifest = GameplaySkinOptionsTest.ManifestJson();
            manifest["bitmapFonts"] = new JArray(new JObject
            {
                ["id"] = "option.digits",
                ["lineHeight"] = 2,
                ["glyphs"] = new JArray(characters.Select(character => new JObject
                {
                    ["character"] = character.ToString(),
                    ["resource"] = "texture.normal",
                    ["advance"] = 2,
                })),
            });
            return manifest;
        }

        private static GameplaySkinLayoutPublication optionPublication(Texture normal, Texture alternate, IReadOnlyDictionary<string, string> saved,
                                                                      JObject? manifestJson = null, JObject? sceneJson = null, bool omitAlternateResource = false, string rulesetId = "mania")
        {
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest(manifestJson);
            GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(manifest.Options, saved, rulesetId, rulesetId == "bms" ? "7k" : "1k");
            LayoutFixture layout = createLayout(rulesetId, optionValues: values);
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, normal, source()) });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene((sceneJson ?? optionScene()).ToString(Formatting.None), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface($"{rulesetId}.hud").Rect;
            GameplaySkinPreparedSceneResource[] resources = manifest.Resources.Where(resource => !omitAlternateResource || resource.Id != "texture.alternate")
                .Select(resource => new GameplaySkinPreparedSceneResource(resource, "options-test", 2, 2,
                resource.Id == "texture.alternate" ? alternate : normal)).ToArray();
            GameplaySkinPreparedSceneNode root = prepareNode(document.Root, true);
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materialSet, "options-test", manifest, document, resources, new[] { root });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene);

            GameplaySkinPreparedSceneNode prepareNode(GameplaySkinSceneNode node, bool isRoot)
            {
                GameplaySkinPreparedSceneResource? resource = node.ResourceId == null ? null : resources.Single(candidate => candidate.Id == node.ResourceId);
                return new GameplaySkinPreparedSceneNode(node.Id, node, node.Target, rect, global, isRoot ? GameplaySkinSlotCatalog.TextHud : null,
                    resource, resource?.Texture,
                    node.Children.Select(child => prepareNode(child, false)), GameplaySkinSceneLayer.HudForeground, owner, false);
            }
        }
    }

    [HeadlessTest]
    public partial class TestSceneGameplaySkinIndependentOptionGate : OsuTestScene
    {
        [Test]
        public void TestIndependentInnerVisibilityGatePreservesLifecycleAlphaBrightnessAndArtwork()
        {
            GateHost loaded = null!;
            GameplaySkinSceneRuntimeNode lifecycle = null!;
            GameplaySkinSceneRuntimeNode visibility = null!;
            GameplaySkinSceneRuntimeNode viewport = null!;
            GameplaySkinSceneRuntimeNode row = null!;
            GameplaySkinSceneRuntimeNode artwork = null!;
            ColourInfo originalColour = default;
            Vector2 originalRowPosition = default;
            Vector2 originalClipSize = default;
            int instances = 0;

            AddStep("mount the option gate and its real scene layer", () => Child = loaded = new GateHost());
            AddUntilStep("the scene controller loads", () => loaded.Scene?.IsLoaded == true);
            AddUntilStep("the prepared scene queue completes", () => loaded.Scene.IsSceneReady);
            AddStep("the ready scene contains the exact gate and artwork without faults", () =>
            {
                Assert.That(loaded.Scene.RuntimeFaults, Is.Empty);
                lifecycle = runtime("option.hud");
                visibility = runtime("option.gate");
                viewport = runtime("option.viewport");
                row = runtime("option.row");
                artwork = runtime("option.note");
            });
            AddUntilStep("the mounted HUD layer loads with real geometry", () => loaded.Scene.Layers.HudForeground.IsLoaded
                                                                                 && loaded.Scene.Layers.HudForeground.DrawWidth > 0);
            // The initial input is released, so the gate starts closed. A hidden composite does not
            // run its descendants' LoadComplete; open it through the real binding before awaiting them.
            AddStep("publish the first actual input visibility true", () => loaded.SetVisible(true));
            AddUntilStep("the first input opens the independent gate", () => visibility.TransformDrawable.Alpha == 1);
            AddUntilStep("artwork and clip finish loading inside the open gate", () => artwork.ContentDrawable.IsLoaded && viewport.ContentDrawable.IsLoaded);
            AddStep("retain the original artwork and clip geometry", () =>
            {
                originalColour = artwork.ContentDrawable.Colour;
                originalRowPosition = row.TransformDrawable.Position;
                originalClipSize = viewport.TransformDrawable.Size;
                instances = loaded.Scene.RuntimeInstanceCount;
            });

            // Real input transitions exercise the existing C5 visibility channel on loaded drawables;
            // this is independent alpha ownership, not a native OFF-menu semantic claim.
            int phase = 0;
            foreach (bool selected in new[] { true, false, true })
            {
                bool visible = selected;
                if (phase++ > 0)
                    AddStep($"publish input visibility {visible}", () => loaded.SetVisible(visible));
                AddUntilStep("the scene consumes the actual input transition", () => visibility.TransformDrawable.Alpha == (visible ? 1 : 0));
                AddStep("the independent gate multiplies lifecycle alpha without changing artwork", () =>
                {
                    var brightness = lifecycle.RootDrawable.Colour.TopLeft.SRGB;
                    Assert.Multiple(() =>
                    {
                        Assert.That(lifecycle.TransformDrawable.Alpha, Is.EqualTo(0.4f).Within(0.00001));
                        Assert.That(lifecycle.TransformDrawable.Y, Is.EqualTo(0.4f).Within(0.00001));
                        Assert.That(visibility.TransformDrawable.Alpha, Is.EqualTo(visible ? 1 : 0));
                        Assert.That(viewport.ContentDrawable.DrawColourInfo.Colour.TopLeft.SRGB.A, Is.EqualTo(visible ? 0.4 : 0).Within(0.00001));
                        Assert.That(artwork.ContentDrawable.Colour, Is.EqualTo(originalColour));
                        Assert.That(((Sprite)artwork.ContentDrawable).Texture, Is.SameAs(loaded.Texture));
                        Assert.That(row.TransformDrawable.Position, Is.EqualTo(originalRowPosition));
                        Assert.That(viewport.TransformDrawable.Size, Is.EqualTo(originalClipSize));
                        Assert.That(((Container)viewport.ContentDrawable).Masking, Is.True);
                        Assert.That(loaded.Scene.RuntimeInstanceCount, Is.EqualTo(instances));
                        Assert.That(loaded.Scene.RuntimeFaults, Is.Empty);
                        Assert.That(brightness.R, Is.EqualTo(0.5).Within(0.00001));
                        Assert.That(brightness.G, Is.EqualTo(0.5).Within(0.00001));
                        Assert.That(brightness.B, Is.EqualTo(0.5).Within(0.00001));
                        Assert.That(brightness.A, Is.EqualTo(1));
                        Assert.That(lifecycle.RootDrawable.Alpha, Is.EqualTo(1));
                    });
                });
            }

            GameplaySkinSceneRuntimeNode runtime(string id)
            {
                Assert.That(loaded.Scene.TryGetRuntimeNode(id, out GameplaySkinSceneRuntimeNode? node), Is.True);
                return node!;
            }
        }

        private partial class GateHost : Container
        {
            internal GameplaySkinSceneRuntimeHost Scene { get; private set; } = null!;
            internal Texture Texture { get; private set; } = null!;
            private GameplaySkinLayoutPublication publication = null!;
            private GameplaySkinEventStream stream = null!;
            private GameplaySkinEventProducer producer = null!;
            private double time;

            internal GateHost() => RelativeSizeAxes = Axes.Both;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                Texture = renderer.WhitePixel;
                publication = GameplaySkinSceneRuntimeHostTest.CreateIndependentOptionGatePublication(Texture);
                stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
                producer = stream.CreateProducer();
                Scene = new GameplaySkinSceneRuntimeHost(publication, stream);
                Children = new Drawable[] { Scene, Scene.Layers.HudForeground };
                Scene.MarkLayersMounted();
            }

            internal void SetVisible(bool visible)
            {
                GameplaySkinInputStateSnapshot lane = publication.PreparedScene.InitialEventState.Inputs.Single();
                var state = new GameplaySkinInputStateSnapshot(lane.GroupId, lane.LaneId, visible, visible ? 1 : 0);
                stream.Publish(producer, ++time,
                    GameplaySkinEventValue.Input(visible ? GameplaySkinEventKind.InputPressed : GameplaySkinEventKind.InputReleased, state), state.GroupId, state.LaneId);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (isDisposing)
                {
                    producer?.Dispose();
                    stream?.Dispose();
                    publication?.Dispose();
                }
            }
        }
    }
}
