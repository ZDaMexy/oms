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
    [TestFixture]
    public sealed class GameplaySkinHorizontalLayoutTest
    {
        [Test]
        public void TestFirstFrameAndOneToFourDigitsUseActualGlyphOverhangAndLeaveC3RectUnchanged()
        {
            using var label = new DummyRenderer().CreateTexture(250, 100);
            using var good = new DummyRenderer().CreateTexture(204, 100);
            using var bad = new DummyRenderer().CreateTexture(160, 100);
            using var glyph = new DummyRenderer().CreateTexture(74, 100);
            using GameplaySkinLayoutPublication publication = CreatePublication(label, good, bad, glyph);
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 1));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            foreach (int combo in new[] { 1, 12, 123, 1234 })
            {
                stream.Publish(producer, combo, GameplaySkinEventValue.Score(GameplaySkinEventKind.ScoreChanged,
                    new GameplaySkinScoreStateSnapshot(combo, combo, combo, 1, 0.5)), null, null);
                host.ProcessFrame();
                GameplaySkinSceneRuntimeNode digits = runtime(host, "row.blue.digits");
                var text = (GameplaySkinBitmapText)digits.ContentDrawable;
                int count = combo.ToString().Length;
                float naturalWidth = 74 + 55 * (count - 1);
                Assert.Multiple(() =>
                {
                    Assert.That(text.AuthorText, Is.EqualTo(combo.ToString()));
                    Assert.That(((SpriteText)text).Text.ToString(), Is.Empty, "Natural measurement must work before font loading.");
                    Assert.That(digits.RootDrawable.Width, Is.EqualTo(naturalWidth).Within(0.001));
                    Assert.That(digits.RootDrawable.RelativeSizeAxes, Is.EqualTo(Axes.None));
                    Assert.That(digits.RootDrawable.RelativePositionAxes, Is.EqualTo(Axes.None));
                    Assert.That(digits.TransformDrawable.RelativeSizeAxes, Is.EqualTo(Axes.Both));
                    Assert.That(digits.Rect, Is.EqualTo(runtime(host, "row.blue").Rect), "C3 anchors do not become flow rectangles.");
                });
                assertCentered(host, "row.blue", 250, naturalWidth, -16);
                assertCentered(host, "row.yellow", 250, naturalWidth, -16);
            }
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase(-16, 250, 74)]
        [TestCase(-400, 250, 74)]
        [TestCase(512, 250, 74)]
        public void TestSignedGapCentersTheOccupiedUnion(float gap, float labelWidth, float digitWidth)
        {
            using var label = new DummyRenderer().CreateTexture(250, 100);
            using var glyph = new DummyRenderer().CreateTexture(74, 100);
            using GameplaySkinLayoutPublication publication = CreatePublication(label, label, label, glyph, gap: gap);
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 1));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertCentered(host, "row.blue", labelWidth, digitWidth, gap);
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestExplicitPixelWidthKeepsItsAuthoredBox()
        {
            using var label = new DummyRenderer().CreateTexture(250, 100);
            using var glyph = new DummyRenderer().CreateTexture(74, 100);
            using GameplaySkinLayoutPublication publication = CreatePublication(label, label, label, glyph, explicitWidth: 200);
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 1));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertCentered(host, "row.blue", 200, 74, -16);
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestTypedVisibleVariantsSwitchRowsAndPreparedLabelAspectAfterJudgementResetAndLateAttach()
        {
            using var label = new DummyRenderer().CreateTexture(250, 100);
            using var good = new DummyRenderer().CreateTexture(204, 100);
            using var bad = new DummyRenderer().CreateTexture(160, 100);
            using var glyph = new DummyRenderer().CreateTexture(74, 100);
            using GameplaySkinLayoutPublication publication = CreatePublication(label, good, bad, glyph);
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 12));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                double time = 0;
                foreach ((GameplaySkinJudgementGrade grade, Texture image) in new[]
                         {
                             (GameplaySkinJudgementGrade.Perfect, label), (GameplaySkinJudgementGrade.Good, good),
                             (GameplaySkinJudgementGrade.Meh, bad), (GameplaySkinJudgementGrade.Perfect, label),
                         })
                {
                    stream.Publish(producer, ++time, GameplaySkinEventValue.Judgement(
                        new GameplaySkinJudgementStateSnapshot(null, null, null, grade, 0, 0)), null, null);
                    host.ProcessFrame();
                    bool perfect = grade == GameplaySkinJudgementGrade.Perfect;
                    Assert.Multiple(() =>
                    {
                        Assert.That(runtime(host, "row.blue").TransformDrawable.Alpha, Is.EqualTo(perfect ? 1 : 0));
                        Assert.That(runtime(host, "row.yellow").TransformDrawable.Alpha, Is.EqualTo(perfect ? 0 : 1));
                        Assert.That(((Sprite)runtime(host, "row.yellow.label").ContentDrawable).Texture, Is.SameAs(image));
                        Assert.That(((SpriteText)runtime(host, "row.blue.digits").ContentDrawable).Font.FontName, Is.EqualTo("font.blue"));
                        Assert.That(((SpriteText)runtime(host, "row.yellow.digits").ContentDrawable).Font.FontName, Is.EqualTo("font.yellow"));
                    });
                    assertCentered(host, "row.yellow", image.Width, 129, -16);
                }
                using (var late = new GameplaySkinSceneRuntimeHost(publication, stream))
                {
                    late.ProcessFrame();
                    Assert.That(runtime(late, "row.blue").TransformDrawable.Alpha, Is.EqualTo(1));
                    assertCentered(late, "row.blue", 250, 129, -16);
                    Assert.That(late.RuntimeFaults, Is.Empty);
                }

                producer = producer.Reset(0, state(publication, 1), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(runtime(host, "row.blue").TransformDrawable.Alpha, Is.Zero);
                Assert.That(runtime(host, "row.yellow").TransformDrawable.Alpha, Is.EqualTo(1));
                assertCentered(host, "row.yellow", 250, 74, -16);
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [TestCase("row.blue.label")]
        [TestCase("row.blue.digits")]
        public void TestVisibilityVariantAlsoControlsSpriteAndTextWithoutRemovingTheirOccupancy(string target)
        {
            using var label = new DummyRenderer().CreateTexture(250, 100);
            using var glyph = new DummyRenderer().CreateTexture(74, 100);
            using GameplaySkinLayoutPublication publication = CreatePublication(label, label, label, glyph, visibleTarget: target);
            using var stream = new GameplaySkinEventStream(publication, 0, state(publication, 12));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, target).TransformDrawable.Alpha, Is.Zero);
            assertCentered(host, "row.blue", 250, 129, -16);
            stream.Publish(producer, 1, GameplaySkinEventValue.Judgement(
                new GameplaySkinJudgementStateSnapshot(null, null, null, GameplaySkinJudgementGrade.Perfect, 0, 0)), null, null);
            host.ProcessFrame();
            Assert.That(runtime(host, target).TransformDrawable.Alpha, Is.EqualTo(1));
            assertCentered(host, "row.blue", 250, 129, -16);
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase("layout", "track")]
        [TestCase("layout", "binding")]
        [TestCase("layout", "state")]
        [TestCase("gap-px", "track")]
        [TestCase("gap-px", "binding")]
        [TestCase("gap-px", "state")]
        [TestCase("width-px", "track")]
        [TestCase("width-px", "binding")]
        [TestCase("width-px", "state")]
        [TestCase("height-px", "track")]
        [TestCase("height-px", "binding")]
        [TestCase("height-px", "state")]
        public void TestLayoutMetadataCannotBeWrittenByAProgram(string property, string writer)
        {
            JObject json = sceneJson();
            string target = property is "layout" or "gap-px" ? "row.blue" : "row.blue.label";
            JToken value = property == "layout" ? new JValue("horizontal") : new JValue(10);
            if (writer == "track")
                json["tracks"] = new JArray(new JObject
                {
                    ["id"] = "bad.track",
                    ["target"] = target,
                    ["property"] = property,
                    ["type"] = "tween",
                    ["easing"] = "step",
                    ["loop"] = false,
                    ["keyframes"] = new JArray(new JObject { ["id"] = "bad.frame", ["time"] = 0, ["value"] = value }),
                });
            else if (writer == "binding")
                ((JArray)json["bindings"]!).Add(new JObject { ["id"] = "bad.binding", ["target"] = target, ["property"] = property, ["source"] = "combo.value" });
            else
                json["stateMachines"] = new JArray(new JObject
                {
                    ["id"] = "bad.machine",
                    ["initial"] = "bad.state",
                    ["transitions"] = new JArray(),
                    ["states"] = new JArray(new JObject
                    {
                        ["id"] = "bad.state",
                        ["set"] = new JArray(new JObject
                        {
                            ["id"] = "bad.set",
                            ["target"] = target,
                            ["property"] = property,
                            ["value"] = value,
                        })
                    }),
                });
            Assert.That(GameplaySkinSceneCodec.DecodeScene(json.ToString(Formatting.None), manifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [TestCase("missing-height")]
        [TestCase("zero-height")]
        [TestCase("large-width")]
        [TestCase("large-gap")]
        [TestCase("unknown-layout")]
        [TestCase("ordinary-text")]
        [TestCase("nested-leaf")]
        [TestCase("leaf-effect")]
        [TestCase("row-effect")]
        [TestCase("ancestor-effect")]
        [TestCase("child-position")]
        [TestCase("orphan-pixels")]
        [TestCase("orphan-gap")]
        [TestCase("visible-string")]
        [TestCase("visible-resource-case")]
        [TestCase("visible-unknown-key")]
        [TestCase("visible-open-source")]
        public void TestInvalidRowsAndVisibilityContractsAreRejected(string change)
        {
            JObject json = sceneJson();
            var root = (JObject)json["root"]!;
            var row = (JObject)root["children"]![0]!;
            var label = (JObject)row["children"]![0]!;
            var digits = (JObject)row["children"]![1]!;
            var properties = (JObject)row["properties"]!;
            var labelProperties = (JObject)label["properties"]!;
            switch (change)
            {
                case "missing-height": labelProperties.Remove("height-px"); break;
                case "zero-height": labelProperties["height-px"] = 0; break;
                case "large-width": labelProperties["width-px"] = 513; break;
                case "large-gap": properties["gap-px"] = -513; break;
                case "unknown-layout": properties["layout"] = "vertical"; break;
                case "ordinary-text": ((JObject)digits["properties"]!).Remove("bitmap-font"); break;
                case "nested-leaf": label["children"] = new JArray(node("extra", "sprite", new JObject(), "image.label")); break;
                case "leaf-effect": label["effects"] = new JArray(effect()); break;
                case "row-effect": row["effects"] = new JArray(effect()); break;
                case "ancestor-effect": root["effects"] = new JArray(effect()); break;
                case "child-position": labelProperties["x"] = 0.5; break;
                case "orphan-pixels": properties.Remove("layout"); properties.Remove("gap-px"); break;
                case "orphan-gap": properties.Remove("layout"); labelProperties.Remove("height-px"); break;
                case "visible-string": json["variants"]![0]!["default"] = "false"; break;
                case "visible-resource-case": json["variants"]![0]!["cases"]![0]!["resource"] = "image.label"; break;
                case "visible-unknown-key": json["variants"]![0]!["cases"]![0]!["key"] = "critical"; break;
                case "visible-open-source": json["variants"]![0]!["source"] = "combo.value"; break;
            }
            Assert.That(GameplaySkinSceneCodec.DecodeScene(json.ToString(Formatting.None), manifest()).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestTypedVisibilityAndOldResourceVariantsRoundTripWithoutChangingSyntax()
        {
            GameplaySkinSceneManifest sourceManifest = manifest();
            GameplaySkinSceneDocument document = decode(sceneJson(), sourceManifest);
            JObject encoded = JObject.Parse(GameplaySkinSceneCodec.EncodeScene(document));
            GameplaySkinSceneDocument second = decode(encoded, sourceManifest);
            Assert.Multiple(() =>
            {
                Assert.That(second.Variants[0].Property, Is.EqualTo(GameplaySkinSceneProperty.Visible));
                Assert.That(second.Variants[0].DefaultValue.Kind, Is.EqualTo(GameplaySkinScenePropertyValueKind.Boolean));
                Assert.That(second.Variants[0].SelectValue("perfect").BooleanValue, Is.True);
                Assert.That(second.Variants[0].SelectValue("good").BooleanValue, Is.False);
                Assert.That(encoded["variants"]![0]!["cases"]![0]!["value"]!.Type, Is.EqualTo(JTokenType.Boolean));
                Assert.That(encoded["variants"]![2]!["cases"]![0]!["resource"]!.Type, Is.EqualTo(JTokenType.String));
                Assert.That(second.Variants[2].SelectResource("good"), Is.EqualTo("image.good"));
            });
        }

        internal static GameplaySkinLayoutPublication CreatePublication(Texture label, Texture good, Texture bad, Texture glyph,
            float gap = -16, float? explicitWidth = null, string? visibleTarget = null)
        {
            using GameplaySkinLayoutPublication baseline = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(label, false);
            GameplaySkinSceneManifest sourceManifest = manifest();
            JObject json = sceneJson(gap);
            if (explicitWidth.HasValue)
                json["root"]!["children"]![0]!["children"]![0]!["properties"]!["width-px"] = explicitWidth.Value;
            if (visibleTarget != null)
                json["variants"]![0]!["target"] = visibleTarget;
            GameplaySkinSceneDocument document = decode(json, sourceManifest);
            GameplaySkinLayoutRect rect = baseline.Snapshot.GetSurface("mania.hud").Rect;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, GameplaySkinResolvedMaterialTarget.Global);
            var textures = new[] { label, good, bad, glyph };
            GameplaySkinPreparedSceneResource[] resources = sourceManifest.Resources.Select((resource, index) =>
                new GameplaySkinPreparedSceneResource(resource, "horizontal-test", textures[index].Width, textures[index].Height, textures[index])).ToArray();
            var resourcesById = resources.ToDictionary(resource => resource.Id, StringComparer.Ordinal);
            var scene = new GameplaySkinPreparedScene(baseline.Snapshot, baseline.MaterialSet, "horizontal-test", sourceManifest, document,
                resources, new[] { prepare(document.Root, true) });
            return GameplaySkinLayoutPublication.Create(new Adapter(baseline.Snapshot), baseline.MaterialSet, scene);

            GameplaySkinPreparedSceneNode prepare(GameplaySkinSceneNode source, bool isRoot)
            {
                GameplaySkinPreparedSceneResource? resource = source.ResourceId == null ? null : resourcesById[source.ResourceId];
                return new GameplaySkinPreparedSceneNode(source.Id, source, source.Target, rect, GameplaySkinResolvedMaterialTarget.Global,
                    isRoot ? GameplaySkinSlotCatalog.TextHud : null, resource, resource?.Texture, source.Children.Select(child => prepare(child, false)),
                    GameplaySkinSceneLayer.HudForeground, owner, false);
            }
        }

        private static GameplaySkinSceneRuntimeNode runtime(GameplaySkinSceneRuntimeHost host, string id)
        {
            Assert.That(host.TryGetRuntimeNode(id, out GameplaySkinSceneRuntimeNode? node), Is.True,
                id + ": " + string.Join(", ", host.RuntimeFaults.Select(fault => fault.Code)));
            return node!;
        }

        private static void assertCentered(GameplaySkinSceneRuntimeHost host, string row, float labelWidth, float digitWidth, float gap)
        {
            Drawable label = runtime(host, row + ".label").RootDrawable;
            Drawable text = runtime(host, row + ".digits").RootDrawable;
            float left = Math.Min(0, labelWidth + gap);
            float right = Math.Max(labelWidth, labelWidth + gap + digitWidth);
            Assert.Multiple(() =>
            {
                Assert.That(label.Size.X, Is.EqualTo(labelWidth).Within(0.001));
                Assert.That(text.Size.X, Is.EqualTo(digitWidth).Within(0.001));
                Assert.That(label.X, Is.EqualTo(-(left + right) / 2).Within(0.001));
                Assert.That(text.X, Is.EqualTo(label.X + labelWidth + gap).Within(0.001));
                Assert.That(label.Y, Is.EqualTo(-50));
                Assert.That(text.Y, Is.EqualTo(-50));
                Assert.That(label.Anchor, Is.EqualTo(Anchor.Centre));
            });
        }

        private static GameplaySkinEventStateSnapshot state(GameplaySkinLayoutPublication publication, int combo)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(combo, combo, combo, 1, 0.5), initial.Timing, initial.BgaViewports, initial.SongInformation);
        }

        private static GameplaySkinSceneManifest manifest()
        {
            string[] resources = new[] { "image.label", "image.good", "image.bad", "image.glyph" };
            var json = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(resources.Select(id => new JObject { ["id"] = id, ["type"] = "texture", ["path"] = id + ".png" })),
                ["bitmapFonts"] = new JArray(new[] { "font.blue", "font.yellow" }.Select(id => new JObject
                {
                    ["id"] = id,
                    ["lineHeight"] = 100,
                    ["glyphs"] = new JArray("0123456789".Select(character => new JObject
                    {
                        ["character"] = character.ToString(),
                        ["resource"] = "image.glyph",
                        ["advance"] = 55,
                    })),
                })),
            };
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> decoded = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            return decoded.Value!;
        }

        private static JObject sceneJson(float gap = -16)
        {
            JObject blue = row("row.blue", "font.blue", gap);
            JObject yellow = row("row.yellow", "font.yellow", gap);
            JObject root = node("hud", "container", new JObject());
            root["slot"] = "hud.text";
            root["children"] = new JArray(blue, yellow);
            return new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = root,
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
                ["bindings"] = new JArray(new[] { "row.blue", "row.yellow" }.Select(id => new JObject
                {
                    ["id"] = id + ".combo-binding",
                    ["target"] = id + ".digits",
                    ["property"] = "text",
                    ["source"] = "combo.value",
                })),
                ["variants"] = new JArray(visible("row.blue", new[] { "perfect" }), visible("row.yellow", new[] { "miss", "meh", "ok", "good", "great" }),
                    new JObject
                    {
                        ["id"] = "label.variant",
                        ["target"] = "row.yellow.label",
                        ["property"] = "resource",
                        ["source"] = "judgement.result",
                        ["default"] = "image.label",
                        ["cases"] = new JArray(new JObject { ["id"] = "label.good", ["key"] = "good", ["resource"] = "image.good" },
                            new JObject { ["id"] = "label.bad", ["key"] = "meh", ["resource"] = "image.bad" }),
                    }),
            };
        }

        private static JObject row(string id, string font, float gap)
        {
            JObject result = node(id, "container", new JObject { ["layout"] = "horizontal", ["gap-px"] = gap, ["x"] = 0.5, ["y"] = 0.5, ["origin"] = "centre" });
            result["children"] = new JArray(node(id + ".label", "sprite", new JObject { ["height-px"] = 100 }, "image.label"),
                node(id + ".digits", "text", new JObject { ["text"] = "0", ["bitmap-font"] = font, ["font-size"] = 100, ["format"] = "integer" }));
            return result;
        }

        private static JObject visible(string row, IEnumerable<string> keys)
            => new JObject
            {
                ["id"] = row + ".visible",
                ["target"] = row,
                ["property"] = "visible",
                ["source"] = "judgement.result",
                ["default"] = false,
                ["cases"] = new JArray(keys.Select(key => new JObject { ["id"] = row + ".case-" + key, ["key"] = key, ["value"] = true })),
            };

        private static JObject node(string id, string type, JObject properties, string? resource = null)
        {
            var result = new JObject
            {
                ["id"] = id,
                ["type"] = type,
                ["target"] = new JObject { ["kind"] = "global" },
                ["blend"] = "alpha",
                ["properties"] = properties,
                ["effects"] = new JArray(),
                ["children"] = new JArray(),
            };
            if (resource != null)
                result["resource"] = resource;
            return result;
        }

        private static JObject effect() => new JObject { ["id"] = "bad.effect", ["type"] = "blur", ["properties"] = new JObject { ["radius"] = 1 } };

        private static GameplaySkinSceneDocument decode(JObject json, GameplaySkinSceneManifest sourceManifest)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(json.ToString(Formatting.None), sourceManifest);
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
            return result.Value!;
        }

        private sealed record Adapter(GameplaySkinLayoutSnapshot Snapshot) : IGameplaySkinLayoutAdapter;
    }
}
