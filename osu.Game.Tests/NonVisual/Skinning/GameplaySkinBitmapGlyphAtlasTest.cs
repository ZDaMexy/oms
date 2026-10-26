// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinBitmapGlyphAtlasTest
    {
        [Test]
        public void TestPixelRegionsRoundTripAndShareOnePreparedNativeTextureWithOriginalMetrics()
        {
            GameplaySkinSceneManifest manifest = decode(manifestJson());
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            GameplaySkinSceneManifest roundTrip = decode(JObject.Parse(encoded));
            Assert.That(roundTrip.Resources, Has.Count.EqualTo(1));
            Assert.That(roundTrip.BitmapFonts.Single().Glyphs[0].Rect, Is.EqualTo(new GameplaySkinBitmapGlyphRect(16, 8, 74, 100)));
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(roundTrip), Is.EqualTo(encoded));
            using Texture atlas = new DummyRenderer().CreateTexture(512, 256);
            var font = new GameplaySkinPreparedBitmapFont(roundTrip.BitmapFonts.Single(), resources(atlas));
            ITexturedCharacterGlyph first = font.Get(font.Source.Id, '0')!;
            ITexturedCharacterGlyph second = font.Get(font.Source.Id, '1')!;
            RectangleF expectedUv = atlas.GetTextureRect(new RectangleF(16, 8, 74, 100));
            RectangleF actualUv = first.Texture.GetTextureRect();
            Assert.Multiple(() =>
            {
                Assert.That(first.Texture, Is.Not.SameAs(atlas));
                Assert.That(first.Texture, Is.TypeOf<TextureRegion>());
                Assert.That(second.Texture, Is.TypeOf<TextureRegion>());
                Assert.That(first.Texture.Width, Is.EqualTo(74));
                Assert.That(first.Texture.Height, Is.EqualTo(100));
                Assert.That(first.Width, Is.EqualTo(0.74).Within(0.000001));
                Assert.That(first.Height, Is.EqualTo(1));
                Assert.That(first.XAdvance, Is.EqualTo(0.55).Within(0.000001));
                Assert.That(actualUv.X, Is.EqualTo(expectedUv.X).Within(0.000001));
                Assert.That(actualUv.Y, Is.EqualTo(expectedUv.Y).Within(0.000001));
                Assert.That(actualUv.Width, Is.EqualTo(expectedUv.Width).Within(0.000001));
                Assert.That(actualUv.Height, Is.EqualTo(expectedUv.Height).Within(0.000001));
                Assert.That(font.Get(font.Source.Id, '0'), Is.SameAs(first));
                Assert.That(font.GetAsync(font.Source.Id, '0'), Is.SameAs(font.GetAsync(font.Source.Id, '0')));
                Assert.That(font.MeasureText("01", 100).X, Is.EqualTo(129).Within(0.0001), "The final 74px image retains its 19px overhang beyond55px advance.");
            });
            var builder = new TextBuilder(font, new FontUsage(font.Source.Id, 100));
            builder.AddText("01");
            Assert.That(builder.Characters[1].DrawRectangle.X, Is.EqualTo(55).Within(0.0001));
            Assert.That(builder.Characters[1].DrawRectangle.Width, Is.EqualTo(74).Within(0.0001));
            Assert.That(builder.Characters[1].DrawRectangle.Height, Is.EqualTo(100).Within(0.0001));
        }

        [TestCase("length", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("negative-x", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("negative-y", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("zero-width", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("zero-height", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("oversized-width", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("oversized-height", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("above-line-height", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("fractional", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("null", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("coordinate-overflow", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("large-integer", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("right-overflow", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        public void TestMalformedAtlasRectIsRejectedAtTheAuthorBoundary(string reason, GameplaySkinSceneDiagnosticCode expected)
        {
            JObject json = manifestJson();
            decode(json);
            JObject glyph = (JObject)json["bitmapFonts"]![0]!["glyphs"]![0]!;
            var rect = (JArray)glyph["rect"]!;
            switch (reason)
            {
                case "length": rect.RemoveAt(3); break;
                case "negative-x": rect[0] = -1; break;
                case "negative-y": rect[1] = -1; break;
                case "zero-width": rect[2] = 0; break;
                case "zero-height": rect[3] = 0; break;
                case "oversized-width": rect[2] = 513; break;
                case "oversized-height": rect[3] = 513; break;
                case "above-line-height": rect[3] = 101; break;
                case "fractional": rect[0] = 1.5; break;
                case "string": rect[0] = "1"; break;
                case "null": glyph["rect"] = null; break;
                case "coordinate-overflow": rect[0] = (long)int.MaxValue + 1; break;
                case "large-integer": rect[0] = JToken.Parse("999999999999999999999999999999999999"); break;
                case "right-overflow": rect[0] = int.MaxValue; break;
            }
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expected));
        }

        [TestCase(500, 8, 74, 100)]
        [TestCase(16, 200, 74, 100)]
        [TestCase(512, 0, 1, 1)]
        [TestCase(0, 256, 1, 1)]
        public void TestValidScalarRectCannotReadOutsideTheExactPreparedTexture(int x, int y, int width, int height)
        {
            JObject json = manifestJson();
            json["bitmapFonts"]![0]!["glyphs"]![0]!["rect"] = new JArray(x, y, width, height);
            GameplaySkinSceneManifest manifest = decode(json);
            using Texture atlas = new DummyRenderer().CreateTexture(512, 256);
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                new GameplaySkinPreparedBitmapFont(manifest.BitmapFonts.Single(), resources(atlas)));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
        }

        [Test]
        public void TestWholePngFontsKeepTheExistingTextureAndSerializedContract()
        {
            JObject json = manifestJson();
            foreach (JToken glyph in (JArray)json["bitmapFonts"]![0]!["glyphs"]!)
                ((JObject)glyph).Remove("rect");
            GameplaySkinSceneManifest manifest = decode(json);
            Assert.That(manifest.BitmapFonts.Single().Glyphs.All(glyph => glyph.Rect == null), Is.True);
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            Assert.That(encoded, Does.Not.Contain("\"rect\""));
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(decode(JObject.Parse(encoded))), Is.EqualTo(encoded));
            using Texture texture = new DummyRenderer().CreateTexture(74, 100);
            var font = new GameplaySkinPreparedBitmapFont(manifest.BitmapFonts.Single(), resources(texture));
            Assert.That(font.Get(font.Source.Id, '0')!.Texture, Is.SameAs(texture));
            Assert.That(font.Get(font.Source.Id, '1')!.Texture, Is.SameAs(texture));
            Assert.That(GameplaySkinBitmapFontBudgets.MAX_FONTS, Is.EqualTo(12));
            Assert.That(GameplaySkinSceneBudgets.MAX_RESOURCES, Is.EqualTo(256));
        }

        [Test]
        public void TestLargeAtlasAllowsSmallGlyphsWithoutRelaxingTheWholeImageGlyphLimit()
        {
            using Texture atlas = new DummyRenderer().CreateTexture(1024, 128);
            var atlasFont = new GameplaySkinBitmapFont("font.atlas", 24, new[]
            {
                new GameplaySkinBitmapGlyph('0', "atlas", 34, new GameplaySkinBitmapGlyphRect(990, 104, 34, 24)),
            });
            var font = new GameplaySkinPreparedBitmapFont(atlasFont, resources(atlas));
            Assert.That(font.Get(font.Source.Id, '0')!.Texture, Is.TypeOf<TextureRegion>());
            Assert.That(font.Get(font.Source.Id, '0')!.Texture.Width, Is.EqualTo(34));
            var whole = new GameplaySkinBitmapFont("font.whole", 24, new[] { new GameplaySkinBitmapGlyph('0', "atlas", 34) });
            Assert.Throws<GameplaySkinScenePreparationException>(() => new GameplaySkinPreparedBitmapFont(whole, resources(atlas)));
        }

        [Test]
        public void TestDisposingTextRetainsTheSceneOwnedAtlasAndOtherGlyphRegions()
        {
            using Texture backing = new DummyRenderer().CreateTexture(512, 256);
            using var atlas = new TrackedTexture(backing);
            var font = new GameplaySkinPreparedBitmapFont(decode(manifestJson()).BitmapFonts.Single(), resources(atlas));
            var first = new GameplaySkinBitmapText(font, 100);
            using var second = new GameplaySkinBitmapText(font, 100);
            first.SetAuthorText("01");
            second.SetAuthorText("10");
            first.Dispose();
            Assert.That(atlas.DisposeCount, Is.Zero);
            Assert.That(font.Get(font.Source.Id, '1')!.Texture.GetTextureRect(), Is.EqualTo(atlas.GetTextureRect(new RectangleF(96, 8, 74, 100))));
            second.SetAuthorText("11");
            Assert.That(second.AuthorText, Is.EqualTo("11"));
        }

        private static GameplaySkinSceneManifest decode(JObject json)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
            return result.Value!;
        }

        private static JObject manifestJson() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
             "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1",
             "resources":[{"id":"atlas","type":"texture","path":"digits.png","filter":"nearest"}],
             "bitmapFonts":[{"id":"font.digits","lineHeight":100,"glyphs":[
               {"character":"0","resource":"atlas","advance":55,"rect":[16,8,74,100]},
               {"character":"1","resource":"atlas","advance":55,"rect":[96,8,74,100]}]}]}
            """);

        private static IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources(Texture atlas)
            => new Dictionary<string, GameplaySkinPreparedSceneResource>
            {
                ["atlas"] = new GameplaySkinPreparedSceneResource(new GameplaySkinSceneResource("atlas", GameplaySkinSceneResourceType.Texture, "digits.png"),
                    "captured-atlas", 4, 4, atlas),
            };

        private sealed class TrackedTexture : Texture
        {
            public int DisposeCount { get; private set; }
            public TrackedTexture(Texture parent) : base(parent) { }
            protected override void Dispose(bool isDisposing)
            {
                DisposeCount++;
                base.Dispose(isDisposing);
            }
        }
    }
}
