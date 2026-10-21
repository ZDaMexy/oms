// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinBitmapFontIntegrationTest
    {
        [Test]
        public void TestManifestAndSceneRoundTripKeepsExplicitGlyphs()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            GameplaySkinSceneManifest second = decodeManifest(JObject.Parse(encoded));
            GameplaySkinSceneDocument document = decodeScene(sceneJson(), second);
            GameplaySkinSceneDocument roundTrip = decodeScene(JObject.Parse(GameplaySkinSceneCodec.EncodeScene(document)), second);

            Assert.Multiple(() =>
            {
                Assert.That(second.BitmapFonts, Has.Count.EqualTo(1));
                Assert.That(second.BitmapFonts[0].LineHeight, Is.EqualTo(28));
                Assert.That(second.BitmapFonts[0].Glyphs.Select(glyph => glyph.Character), Is.EqualTo("0123456789".ToCharArray()));
                Assert.That(second.BitmapFonts[0].Glyphs.All(glyph => glyph.ResourceId == "glyph.image" && glyph.Advance == 40), Is.True);
                Assert.That(roundTrip.Root.Children.Single().Properties["bitmap-font"].StringValue, Is.EqualTo("font.digits"));
                Assert.That(GameplaySkinSceneVocabulary.ParseProperty("bitmap-font"), Is.EqualTo(GameplaySkinSceneProperty.BitmapFont));
            });
        }

        [Test]
        public void TestOldManifestDoesNotGainBitmapFontDeclaration()
        {
            JObject json = manifestJson();
            json.Remove("bitmapFonts");
            GameplaySkinSceneManifest manifest = decodeManifest(json);
            Assert.Multiple(() =>
            {
                Assert.That(manifest.BitmapFonts, Is.Empty);
                Assert.That(JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(manifest)).ContainsKey("bitmapFonts"), Is.False);
            });
        }

        [Test]
        public void TestTenDistinctOriginalFontMetricsAreAcceptedAndEleventhIsRejected()
        {
            JObject json = manifestJson();
            var fonts = (JArray)json["bitmapFonts"]!;
            for (int i = 1; i < 10; i++)
            {
                var another = (JObject)fonts[0]!.DeepClone();
                another["id"] = $"font.original-{i}";
                another["lineHeight"] = 14 + i;
                fonts.Add(another);
            }
            Assert.That(decodeManifest(json).BitmapFonts, Has.Count.EqualTo(10));
            var excess = (JObject)fonts[0]!.DeepClone();
            excess["id"] = "font.eleventh";
            fonts.Add(excess);
            Assert.That(GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None)).Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        [TestCase("unknown-field", GameplaySkinSceneDiagnosticCode.UnknownField)]
        [TestCase("empty", GameplaySkinSceneDiagnosticCode.InvalidResource)]
        [TestCase("duplicate-character", GameplaySkinSceneDiagnosticCode.InvalidResource)]
        [TestCase("duplicate-font", GameplaySkinSceneDiagnosticCode.DuplicateStableId)]
        [TestCase("letter", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("space", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("surrogate", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("multiple-characters", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("missing-texture", GameplaySkinSceneDiagnosticCode.UnknownResource)]
        [TestCase("zero-height", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("large-height", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("zero-advance", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("large-advance", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("too-many-fonts", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("too-many-glyphs", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        public void TestInvalidFontDeclarationsAreRejectedBeforePreparation(string change, GameplaySkinSceneDiagnosticCode expected)
        {
            JObject json = manifestJson();
            var fonts = (JArray)json["bitmapFonts"]!;
            var font = (JObject)fonts[0]!;
            var glyphs = (JArray)font["glyphs"]!;
            JToken glyph = glyphs[0]!;
            switch (change)
            {
                case "unknown-field": font["path"] = "system-font.ttf"; break;
                case "empty": glyphs.Clear(); break;
                case "duplicate-character": glyphs.Add(glyph.DeepClone()); break;
                case "duplicate-font": fonts.Add(font.DeepClone()); break;
                case "letter": glyph["character"] = "A"; break;
                case "space": glyph["character"] = " "; break;
                case "surrogate": glyph["character"] = "💎"; break;
                case "multiple-characters": glyph["character"] = "10"; break;
                case "missing-texture": glyph["resource"] = "texture.absent"; break;
                case "zero-height": font["lineHeight"] = 0; break;
                case "large-height": font["lineHeight"] = 513; break;
                case "zero-advance": glyph["advance"] = 0; break;
                case "large-advance": glyph["advance"] = 513; break;
                case "too-many-fonts":
                    for (int i = 1; i <= GameplaySkinBitmapFontBudgets.MAX_FONTS; i++)
                    {
                        var another = (JObject)font.DeepClone();
                        another["id"] = $"font.digits-{i}";
                        fonts.Add(another);
                    }
                    break;
                case "too-many-glyphs":
                    while (glyphs.Count <= GameplaySkinBitmapFontBudgets.MAX_GLYPHS_PER_FONT)
                        glyphs.Add(glyph.DeepClone());
                    break;
            }

            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expected));
            });
        }

        [TestCase("unknown-font", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("missing-literal-glyph", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("long-literal", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("uppercase", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("ellipsis", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("fractional-format", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("percent-format", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("default-fractional-bpm", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("metadata-binding", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("boolean-binding", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("font-binding", GameplaySkinSceneDiagnosticCode.UnknownProperty)]
        [TestCase("font-state", GameplaySkinSceneDiagnosticCode.UnknownProperty)]
        [TestCase("font-track", GameplaySkinSceneDiagnosticCode.UnknownProperty)]
        [TestCase("state-missing-glyph", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        public void TestUnsupportedTextProgramsCannotSilentlyUseSystemGlyphs(string change, GameplaySkinSceneDiagnosticCode expected)
        {
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            var properties = (JObject)scene["root"]!["children"]![0]!["properties"]!;
            JToken binding = scene["bindings"]![0]!;
            switch (change)
            {
                case "unknown-font": properties["bitmap-font"] = "font.absent"; break;
                case "missing-literal-glyph": properties["text"] = "10%"; break;
                case "long-literal": properties["text"] = new string('0', 65); break;
                case "uppercase": properties["format"] = "uppercase"; break;
                case "ellipsis": properties["text-overflow"] = "ellipsis"; break;
                case "fractional-format": properties["format"] = "fixed-2"; break;
                case "percent-format": properties["format"] = "percent"; break;
                case "default-fractional-bpm": properties.Remove("format"); break;
                case "metadata-binding": binding["source"] = "song.title"; break;
                case "boolean-binding": binding["source"] = "input.pressed"; break;
                case "font-binding": binding["property"] = "bitmap-font"; break;
                case "font-state": scene["stateMachines"] = new JArray(stateMachine("bitmap-font", "font.digits")); break;
                case "font-track":
                    scene["tracks"] = new JArray(new JObject
                    {
                        ["id"] = "track.font",
                        ["type"] = "tween",
                        ["target"] = "value",
                        ["property"] = "bitmap-font",
                        ["easing"] = "linear",
                        ["loop"] = false,
                        ["keyframes"] = new JArray(new JObject { ["id"] = "frame.font", ["time"] = 0, ["value"] = "font.digits" }),
                    });
                    break;
                case "state-missing-glyph": scene["stateMachines"] = new JArray(stateMachine("text", "10x")); break;
            }

            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(scene.ToString(Formatting.None), manifest);
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expected));
            });
        }

        [Test]
        public void TestRealBpmScoreComboSnapshotsEventsResetAndReattachUseAuthorDigits()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            var children = (JArray)scene["root"]!["children"]!;
            children[0]!["id"] = "bpm";
            var score = (JObject)children[0]!.DeepClone();
            score["id"] = "score";
            var combo = (JObject)score.DeepClone();
            combo["id"] = "combo";
            children.Add(score);
            children.Add(combo);
            scene["bindings"] = new JArray(binding("bpm", "timing.bpm"), binding("score", "score.value"), binding("combo", "combo.value"));
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            GameplaySkinEventStateSnapshot initial = state(publication, 1234, 56, 149.75);
            using var stream = new GameplaySkinEventStream(publication, 0, initial);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using (var host = new GameplaySkinSceneRuntimeHost(publication, stream))
                {
                    host.ProcessFrame();
                    Assert.Multiple(() =>
                    {
                        Assert.That(authorText(host, "bpm"), Is.EqualTo("150"));
                        Assert.That(authorText(host, "score"), Is.EqualTo("1234"));
                        Assert.That(authorText(host, "combo"), Is.EqualTo("56"));
                        Assert.That(publication.PreparedScene.GetTextGlyphReservation("bpm"), Is.EqualTo(64));
                        Assert.That(publication.PreparedScene.ReservedTextGlyphs, Is.EqualTo(192));
                        Assert.That(publication.PreparedScene.ReservedTextGlyphPixels, Is.Zero);
                        Assert.That(publication.PreparedScene.GetBitmapFont("bpm")!.Get("font.digits", '0')!.Texture, Is.SameAs(texture));
                    });

                    stream.Publish(producer, 10, GameplaySkinEventValue.Score(GameplaySkinEventKind.ScoreChanged,
                        new GameplaySkinScoreStateSnapshot(5432, 78, 78, 1, 0.5)), null, null);
                    stream.Publish(producer, 10, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBpmChanged,
                        new GameplaySkinTimingStateSnapshot(0, 0, 203.2, false, 1)), null, null);
                    host.ProcessFrame();
                    Assert.That(authorText(host, "bpm"), Is.EqualTo("203"));
                    Assert.That(authorText(host, "score"), Is.EqualTo("5432"));
                    Assert.That(authorText(host, "combo"), Is.EqualTo("78"));

                    producer = producer.Reset(0, state(publication, 0, 0, 120), GameplaySkinEventResetReason.Retry);
                    host.ProcessFrame();
                    Assert.That(authorText(host, "bpm"), Is.EqualTo("120"));
                    Assert.That(authorText(host, "score"), Is.EqualTo("0"));
                    Assert.That(authorText(host, "combo"), Is.EqualTo("0"));
                    Assert.That(host.RuntimeFaults, Is.Empty);
                }

                using var reattached = new GameplaySkinSceneRuntimeHost(publication, stream);
                reattached.ProcessFrame();
                Assert.That(authorText(reattached, "bpm"), Is.EqualTo("120"));
                Assert.That(authorText(reattached, "score"), Is.EqualTo("0"));
                Assert.That(reattached.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestMaximumComboDigitsUseAchievedPeakAcrossBreakSeekRetryAndReattach()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            ((JObject)scene["root"]!["children"]![0]!["properties"]!).Remove("format");
            scene["bindings"]![0]!["source"] = "combo.maximum";
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            GameplaySkinEventStateSnapshot initial = withScore(new GameplaySkinScoreStateSnapshot(100, 3, 19, 1, 0.5));
            using var stream = new GameplaySkinEventStream(publication, 0, initial);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                Assert.That(authorText(host, "value"), Is.EqualTo("19"), "The achieved peak differs from the current combo on first attachment.");

                stream.Publish(producer, 10, GameplaySkinEventValue.Score(GameplaySkinEventKind.ComboChanged,
                    new GameplaySkinScoreStateSnapshot(100, 0, 19, 0.5, 0.5)), null, null);
                host.ProcessFrame();
                Assert.That(authorText(host, "value"), Is.EqualTo("19"), "Breaking the current combo does not erase the engine's achieved maximum.");

                long applications = host.BindingApplicationCount;
                producer.SynchroniseTiming(15, new GameplaySkinTimingStateSnapshot(1, 0, 120, false, 1));
                host.ProcessFrame();
                Assert.That(host.BindingApplicationCount, Is.EqualTo(applications), "A timing-only frame must not reformat a score-family display.");

                stream.Publish(producer, 20, GameplaySkinEventValue.Score(GameplaySkinEventKind.ComboChanged,
                    new GameplaySkinScoreStateSnapshot(200, 23, 23, 0.75, 0.5)), null, null);
                host.ProcessFrame();
                Assert.That(authorText(host, "value"), Is.EqualTo("23"));

                producer = producer.Reset(5, withScore(new GameplaySkinScoreStateSnapshot(50, 1, 7, 1, 0.5)), GameplaySkinEventResetReason.Seek);
                host.ProcessFrame();
                Assert.That(authorText(host, "value"), Is.EqualTo("7"), "A seek takes the rebuilt snapshot's peak, not a skin-maintained maximum.");
                using (var reattached = new GameplaySkinSceneRuntimeHost(publication, stream))
                {
                    reattached.ProcessFrame();
                    Assert.That(authorText(reattached, "value"), Is.EqualTo("7"));
                    Assert.That(reattached.RuntimeFaults, Is.Empty);
                }

                producer = producer.Reset(0, withScore(new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0.5)), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(authorText(host, "value"), Is.EqualTo("0"));
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }

            GameplaySkinEventStateSnapshot withScore(GameplaySkinScoreStateSnapshot score)
            {
                GameplaySkinEventStateSnapshot state = publication.PreparedScene.InitialEventState;
                return new GameplaySkinEventStateSnapshot(state.LifecycleState, state.Inputs, state.ActiveObjects, state.CurrentJudgements,
                    score, state.Timing, state.BgaViewports, state.SongInformation);
            }
        }

        [Test]
        public void TestStateTextAssignmentsUseAuthorGlyphPath()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            scene["bindings"] = new JArray();
            scene["stateMachines"] = new JArray(stateMachine("text", "34"));
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            using var stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(authorText(host, "value"), Is.EqualTo("34"));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestTemplateInstancesSharePreparedFontAndBindRealScore()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            var templateRoot = (JObject)scene["root"]!.DeepClone();
            templateRoot["id"] = "template.owner";
            templateRoot["children"]![0]!["id"] = "template.value";
            scene["root"]!["children"] = new JArray();
            scene["templates"] = new JArray(new JObject { ["id"] = "template.digits", ["root"] = templateRoot });
            scene["instances"] = new JArray(new[] { "first", "second" }.Select(id => new JObject
            {
                ["id"] = "instance." + id,
                ["template"] = "template.digits",
                ["target"] = new JObject { ["kind"] = "global" },
            }));
            scene["bindings"] = new JArray(binding("template.value", "score.value"));
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 12345, 0, 120));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.Multiple(() =>
            {
                Assert.That(authorText(host, "instance.first/template.value"), Is.EqualTo("12345"));
                Assert.That(authorText(host, "instance.second/template.value"), Is.EqualTo("12345"));
                Assert.That(publication.PreparedScene.ReservedTextGlyphs, Is.EqualTo(128));
                Assert.That(publication.PreparedScene.ReservedTextGlyphPixels, Is.Zero);
                Assert.That(host.RuntimeFaults, Is.Empty);
            });

            templateRoot["children"]![0]!["properties"]!["text"] = "10x";
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> invalid = GameplaySkinSceneCodec.DecodeScene(scene.ToString(Formatting.None), manifest);
            Assert.That(invalid.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(invalid.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidReference));
        }

        [TestCase("timing.bpm", "fixed-2", "1.20")]
        [TestCase("score.accuracy", "percent", "99%")]
        [TestCase("timing.progress", "percent", "42%")]
        public void TestDeclaredNumericSymbolsAndFormatsReachBitmapRenderer(string source, string format, string expected)
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson("0123456789.%"));
            JObject scene = sceneJson();
            scene["bindings"]![0]!["source"] = source;
            scene["root"]!["children"]![0]!["properties"]!["format"] = format;
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            var snapshot = new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 0.987, 0.5), new GameplaySkinTimingStateSnapshot(0, 0, 1.2, false, 1, 0.42),
                initial.BgaViewports, initial.SongInformation);
            using var stream = new GameplaySkinEventStream(publication, 0, snapshot);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(authorText(host, "value"), Is.EqualTo(expected));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestOrdinaryTextKeepsDefaultFontAndSupportsIntegerPresentation()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            JObject scene = sceneJson();
            ((JObject)scene["root"]!["children"]![0]!["properties"]!).Remove("bitmap-font");
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(scene, manifest));
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 0, 0, 149.75));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("value", out GameplaySkinSceneRuntimeNode? value), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(value!.ContentDrawable, Is.TypeOf<OsuSpriteText>());
                Assert.That(((SpriteText)value.ContentDrawable).Text.ToString(), Is.EqualTo("150"));
                Assert.That(((SpriteText)value.ContentDrawable).Font, Is.EqualTo(FontUsage.Default.With(size: 28)));
                Assert.That(host.RuntimeFaults, Is.Empty);
            });
        }

        [Test]
        public void TestUnrepresentableEngineValueFallsBackOnlyItsOwner()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinSceneManifest manifest = decodeManifest(manifestJson());
            using GameplaySkinLayoutPublication publication = prepare(texture, manifest, decodeScene(sceneJson(), manifest));
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 0, 0, 1e100));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            Assert.DoesNotThrow(() => host.ProcessFrame());
            Assert.Multiple(() =>
            {
                Assert.That(host.RuntimeFaults.Select(fault => fault.Code), Does.Contain("OMS-SKIN-SCENE-RUNTIME-006"));
                Assert.That(host.TryGetRuntimeNode("value", out _), Is.False);
                Assert.That(host.TryGetVisualGate(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.Global), out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.Route, Is.Not.EqualTo(GameplaySkinSceneHostRoute.Scene));
            });
        }

        private static JObject manifestJson(string characters = "0123456789")
            => new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(new JObject { ["id"] = "glyph.image", ["type"] = "texture", ["path"] = "digits.png" }),
                ["bitmapFonts"] = new JArray(new JObject
                {
                    ["id"] = "font.digits",
                    ["lineHeight"] = 28,
                    ["glyphs"] = new JArray(characters.Select(character => new JObject
                    {
                        ["character"] = character.ToString(),
                        ["resource"] = "glyph.image",
                        ["advance"] = 40,
                    })),
                }),
            };

        private static JObject sceneJson()
            => JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1",
                 "root":{"id":"hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                   {"id":"value","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"0","font-size":28,"bitmap-font":"font.digits","format":"integer"},"effects":[],"children":[]}]},
                 "tracks":[],"stateMachines":[],"templates":[],"instances":[],"bindings":[{"id":"value.binding","target":"value","property":"text","source":"timing.bpm"}]}
                """);

        private static JObject binding(string target, string source)
            => new JObject { ["id"] = target + ".binding", ["target"] = target, ["property"] = "text", ["source"] = source };

        private static JObject stateMachine(string property, string value)
            => new JObject
            {
                ["id"] = "state-machine.digits",
                ["initial"] = "state.ready",
                ["states"] = new JArray(new JObject
                {
                    ["id"] = "state.ready",
                    ["set"] = new JArray(new JObject
                    {
                        ["id"] = "assignment.digits",
                        ["target"] = "value",
                        ["property"] = property,
                        ["value"] = value,
                    }),
                }),
                ["transitions"] = new JArray(),
            };

        private static GameplaySkinSceneManifest decodeManifest(JObject json)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
            return result.Value!;
        }

        private static GameplaySkinSceneDocument decodeScene(JObject json, GameplaySkinSceneManifest manifest)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(json.ToString(Formatting.None), manifest);
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
            return result.Value!;
        }

        private static GameplaySkinLayoutPublication prepare(Texture texture, GameplaySkinSceneManifest manifest, GameplaySkinSceneDocument document)
        {
            using GameplaySkinLayoutPublication baseline = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            GameplaySkinLayoutRect rect = baseline.Snapshot.GetSurface("mania.hud").Rect;
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            var resources = manifest.Resources.Select(resource => new GameplaySkinPreparedSceneResource(resource, "bitmap-test", 4, 4, texture));
            var roots = new List<GameplaySkinPreparedSceneNode> { prepareNode(document.Root, true, string.Empty) };
            foreach (GameplaySkinSceneInstance instance in document.Instances)
                roots.Add(prepareNode(document.Templates.Single(template => template.Id == instance.TemplateId).Root, true, instance.Id + "/"));
            var scene = new GameplaySkinPreparedScene(baseline.Snapshot, baseline.MaterialSet, "bitmap-test", manifest, document, resources, roots);
            return GameplaySkinLayoutPublication.Create(new Adapter(baseline.Snapshot), baseline.MaterialSet, scene);

            GameplaySkinPreparedSceneNode prepareNode(GameplaySkinSceneNode node, bool isRoot, string prefix)
                => new GameplaySkinPreparedSceneNode(prefix + node.Id, node, node.Target, rect, global, isRoot ? GameplaySkinSlotCatalog.TextHud : null,
                    null, null, node.Children.Select(child => prepareNode(child, false, prefix)), GameplaySkinSceneLayer.HudForeground, owner, false);
        }

        private static GameplaySkinEventStateSnapshot state(GameplaySkinLayoutPublication publication, long score, int combo, double bpm)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(score, combo, combo, 1, 0.5), new GameplaySkinTimingStateSnapshot(0, 0, bpm, false, 1),
                initial.BgaViewports, initial.SongInformation);
        }

        private static string authorText(GameplaySkinSceneRuntimeHost host, string nodeId)
        {
            Assert.That(host.TryGetRuntimeNode(nodeId, out GameplaySkinSceneRuntimeNode? node), Is.True, nodeId);
            Assert.That(node!.ContentDrawable, Is.TypeOf<GameplaySkinBitmapText>());
            var bitmap = (GameplaySkinBitmapText)node.ContentDrawable;
            Assert.That(((SpriteText)bitmap).Text.ToString(), Is.Empty, "The unloaded base SpriteText must not pre-cache system glyphs.");
            return bitmap.AuthorText;
        }

        private sealed class Adapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public Adapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }
    }
}
