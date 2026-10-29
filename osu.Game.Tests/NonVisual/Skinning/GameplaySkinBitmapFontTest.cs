// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinBitmapFontTest
    {
        [Test]
        public void TestPreparedGlyphKeepsExactResourceAndSourcePixelMetrics()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            var source = new GameplaySkinBitmapFont("font.bpm", 28, new[] { new GameplaySkinBitmapGlyph('0', "glyph.image", 36) });
            var prepared = new GameplaySkinPreparedBitmapFont(source, resources(texture));
            ITexturedCharacterGlyph glyph = prepared.Get(source.Id, '0')!;

            Assert.Multiple(() =>
            {
                Assert.That(glyph.Texture, Is.SameAs(texture));
                Assert.That(glyph.Character, Is.EqualTo('0'));
                Assert.That(glyph.Width, Is.EqualTo(40f / 28).Within(0.0001));
                Assert.That(glyph.Height, Is.EqualTo(1).Within(0.0001));
                Assert.That(glyph.XAdvance, Is.EqualTo(36f / 28).Within(0.0001));
                Assert.That(glyph.Baseline, Is.EqualTo(1).Within(0.0001));
                Assert.That(glyph.XOffset, Is.Zero);
                Assert.That(glyph.YOffset, Is.Zero);
                Assert.That(prepared.Get(source.Id, '0'), Is.SameAs(glyph));
                Assert.That(prepared.GetAsync(source.Id, '0'), Is.SameAs(prepared.GetAsync(source.Id, '0')));
            });

            var builder = new TextBuilder(prepared, new FontUsage(source.Id, 28));
            builder.AddText("000");
            Assert.Multiple(() =>
            {
                Assert.That(builder.Characters, Has.Count.EqualTo(3));
                Assert.That(builder.Bounds.X, Is.EqualTo(108).Within(0.001));
                Assert.That(builder.Bounds.Y, Is.EqualTo(28).Within(0.001));
                Assert.That(builder.Characters[1].DrawRectangle.X, Is.EqualTo(36).Within(0.001));
                Assert.That(builder.Characters[1].DrawRectangle.Width, Is.EqualTo(40).Within(0.001));
                Assert.That(builder.Characters[1].DrawRectangle.Height, Is.EqualTo(28).Within(0.001));
            });
        }

        [Test]
        public void TestInitialAuthorTextDoesNotEnterSystemFontPrecache()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture), 28);
            text.SetAuthorText("150");

            Assert.Multiple(() =>
            {
                Assert.That(text.AuthorText, Is.EqualTo("150"));
                Assert.That(((SpriteText)text).Text.ToString(), Is.Empty);
                Assert.That(text.Font.FontName, Is.EqualTo("font.digits"));
                Assert.That(text.Shadow, Is.False);
                Assert.That(text.AllowMultiline, Is.False);
            });
        }

        [TestCase(150, "", "150")]
        [TestCase(149.75, "integer", "150")]
        [TestCase(1.25, "", "1.25")]
        [TestCase(1.2, "fixed-2", "1.20")]
        [TestCase(0.5, "fixed-2-pad2", "00.50")]
        [TestCase(1.7533334096272786, "fixed-2-pad2", "01.75")]
        [TestCase(10, "fixed-2-pad2", "10.00")]
        [TestCase(-1.2, "fixed-2-pad2", "-01.20")]
        [TestCase(0.987, "percent", "99%")]
        [TestCase(-12, "integer", "-12")]
        [TestCase(1000000, "integer", "1000000")]
        public void TestNumericBindingsUseOnlyExplicitGlyphs(double value, string format, string expected)
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture, "0123456789.,%+-x"), 28);
            text.SetNumber(value, format);
            Assert.That(text.AuthorText, Is.EqualTo(expected));
        }

        [Test]
        public void TestMissingGlyphFailsBeforeReplacingBoundText()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            GameplaySkinPreparedBitmapFont font = prepareDigits(texture);
            using var text = new GameplaySkinBitmapText(font, 28);
            text.SetNumber(42, "integer");

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => text.SetNumber(42.25, string.Empty));
                Assert.Throws<InvalidOperationException>(() => text.SetAuthorText("42%"));
                Assert.Throws<InvalidOperationException>(() => font.Get(font.Source.Id, '?'));
                Assert.That(font.Get("another.font", '0'), Is.Null);
                Assert.That(text.AuthorText, Is.EqualTo("42"));
            });
        }

        [TestCase(" ")]
        [TestCase("A")]
        [TestCase("\n")]
        [TestCase("💎")]
        public void TestOnlyDeclaredSingleLineNumericCharactersAreAccepted(string value)
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture), 28);
            Assert.Throws<InvalidOperationException>(() => text.SetAuthorText(value));
        }

        [Test]
        public void TestCharacterBudgetRejectsOversizedTextAndNumber()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture), 28);
            string largest = new string('0', GameplaySkinBitmapFontBudgets.MAX_TEXT_CHARACTERS);
            text.SetAuthorText(largest);

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => text.SetAuthorText(largest + "0"));
                Assert.Throws<InvalidOperationException>(() => text.SetNumber(1e100, "integer"));
                Assert.That(text.AuthorText, Is.SameAs(largest));
            });
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestNonFiniteNumbersAreComponentFaults(double value)
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture), 28);
            Assert.Throws<InvalidOperationException>(() => text.SetNumber(value, "integer"));
        }

        [Test]
        public void TestUnchangedNumericBindingDoesNotAllocateStringsOrGlyphs()
        {
            using Texture texture = new DummyRenderer().CreateTexture(40, 28);
            using var text = new GameplaySkinBitmapText(prepareDigits(texture), 28);
            text.SetNumber(150, "integer");
            string original = text.AuthorText;

            // Warm up formatting before measuring repeated snapshots whose BPM did not change.
            for (int i = 0; i < 128; i++)
                text.SetNumber(150, "integer");

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1024; i++)
                text.SetNumber(150, "integer");

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Multiple(() =>
            {
                Assert.That(allocated, Is.Zero);
                Assert.That(text.AuthorText, Is.SameAs(original));
            });
        }

        [Test]
        public void TestUnknownOrUnpreparedGlyphResourceFailsPreparation()
        {
            var source = new GameplaySkinBitmapFont("font.digits", 28, new[] { new GameplaySkinBitmapGlyph('0', "glyph.image", 40) });
            var empty = new Dictionary<string, GameplaySkinPreparedSceneResource>();
            var missing = Assert.Throws<GameplaySkinScenePreparationException>(() => new GameplaySkinPreparedBitmapFont(source, empty));
            var notPrepared = Assert.Throws<GameplaySkinScenePreparationException>(() => new GameplaySkinPreparedBitmapFont(source, resources(null)));

            Assert.Multiple(() =>
            {
                Assert.That(missing!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.UnknownResource));
                Assert.That(notPrepared!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
            });
        }

        [TestCase(513, 28, 28)]
        [TestCase(40, 513, 512)]
        [TestCase(40, 29, 28)]
        public void TestOversizedGlyphsDoNotReachRuntime(int width, int height, double lineHeight)
        {
            using Texture texture = new DummyRenderer().CreateTexture(width, height);
            var source = new GameplaySkinBitmapFont("font.digits", lineHeight, new[] { new GameplaySkinBitmapGlyph('0', "glyph.image", 40) });
            var exception = Assert.Throws<GameplaySkinScenePreparationException>(() => new GameplaySkinPreparedBitmapFont(source, resources(texture)));
            Assert.That(exception!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
        }

        [Test]
        public void TestDisposingBitmapTextKeepsSceneOwnedTextureAvailable()
        {
            using Texture backing = new DummyRenderer().CreateTexture(40, 28);
            using var texture = new TrackedTexture(backing);
            GameplaySkinPreparedBitmapFont font = prepareDigits(texture);
            var text = new GameplaySkinBitmapText(font, 28);
            text.SetNumber(150, "integer");
            text.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(texture.DisposeCount, Is.Zero);
                Assert.That(font.Get(font.Source.Id, '0')!.Texture, Is.SameAs(texture));
            });

            texture.Dispose();
            Assert.That(texture.DisposeCount, Is.EqualTo(1));
        }

        private static GameplaySkinPreparedBitmapFont prepareDigits(Texture texture, string characters = "0123456789")
        {
            var source = new GameplaySkinBitmapFont("font.digits", 28,
                characters.Select(character => new GameplaySkinBitmapGlyph(character, "glyph.image", 40)));
            return new GameplaySkinPreparedBitmapFont(source, resources(texture));
        }

        private static IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources(Texture? texture)
            => new Dictionary<string, GameplaySkinPreparedSceneResource>
            {
                ["glyph.image"] = new GameplaySkinPreparedSceneResource(
                    new GameplaySkinSceneResource("glyph.image", GameplaySkinSceneResourceType.Texture, "digit.png"),
                    "captured-bitmap-test", 4, 4, texture),
            };

        private sealed class TrackedTexture : Texture
        {
            public int DisposeCount { get; private set; }

            public TrackedTexture(Texture parent)
                : base(parent)
            {
            }

            protected override void Dispose(bool isDisposing)
            {
                DisposeCount++;
                base.Dispose(isDisposing);
            }
        }
    }
}
