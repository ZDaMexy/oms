// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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
                => new GameplaySkinPreparedSceneNode(node.Id, node, node.Target, rect, global, isRoot ? GameplaySkinSlotCatalog.TextHud : null,
                    node.ResourceId == null ? null : resources.Single(resource => resource.Id == node.ResourceId), null,
                    node.Children.Select(child => prepareNode(child, false)), GameplaySkinSceneLayer.HudForeground, owner, false);
        }
    }
}
