// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [HeadlessTest]
    public partial class BmsDefaultNoteSkinConfigTest : OsuTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Test]
        public void TestNoteUsesSkinTextureWhenProvided()
        {
            DefaultBmsNoteDisplay note = null!;

            // With a NoteImage texture present the file skin owns the look: a Sprite is shown, not the programmatic box.
            AddStep("load note with NoteImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nNoteImage1: notes/white\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = note = new DefaultBmsNoteDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => note.IsLoaded);
            AddAssert("note shows sprite, not box", () => note.ChildrenOfType<Sprite>().Any() && !note.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestNoteColourOverriddenBySkinConfig()
        {
            DefaultBmsNoteDisplay note = null!;

            // 7K lane 1 is a white-group key, so the [Bms] NoteColourWhite override must apply to it.
            AddStep("load note under skin overriding NoteColourWhite", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nNoteColourWhite: 255,0,0\n");
                Child = new SkinProvidingContainer(skin) { Child = note = new DefaultBmsNoteDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("note loaded", () => note.IsLoaded);
            AddAssert("note uses ini colour", () => note.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(255, 0, 0, 255)));
        }

        [Test]
        public void TestNoteColourFallsBackToPaletteWhenUnset()
        {
            DefaultBmsNoteDisplay note = null!;

            AddStep("load note under skin with no note colour override", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\n");
                Child = new SkinProvidingContainer(skin) { Child = note = new DefaultBmsNoteDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("note loaded", () => note.IsLoaded);
            AddAssert("note uses palette default", () => note.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(BmsDefaultPlayfieldPalette.GetNote(1, false, BmsKeymode.Key7K)));
        }

        [Test]
        public void TestLongNoteHeadColourFromConfig()
        {
            DefaultBmsLongNoteHeadDisplay head = null!;

            AddStep("load LN head overriding NoteColourWhite", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nNoteColourWhite: 0,255,0\n");
                Child = new SkinProvidingContainer(skin) { Child = head = new DefaultBmsLongNoteHeadDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => head.IsLoaded);
            AddAssert("LN head uses ini colour", () => head.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(0, 255, 0, 255)));
        }

        [Test]
        public void TestLongNoteHeadUsesSkinTextureWhenProvided()
        {
            DefaultBmsLongNoteHeadDisplay head = null!;

            AddStep("load LN head with NoteImage1H texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nNoteImage1H: notes/head\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = head = new DefaultBmsLongNoteHeadDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => head.IsLoaded);
            AddAssert("LN head shows sprite, not box", () => head.ChildrenOfType<Sprite>().Any() && !head.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestProtectedLongNoteHeadFallbackIgnoresAggregateTexture()
        {
            DefaultBmsLongNoteHeadDisplay head = null!;

            AddStep("load protected LN head with texture and colour", () =>
            {
                var skin = new TexturedTestSkin(
                    "[Bms]\nKeymode: 7K\nNoteImage1H: notes/head\nNoteColourWhite: 0,255,0\n",
                    renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = head = new DefaultBmsLongNoteHeadDisplay(
                        1,
                        false,
                        BmsKeymode.Key7K,
                        allowAggregateTextureOverride: false),
                };
            });

            AddUntilStep("loaded", () => head.IsLoaded);
            AddAssert("protected LN head stays box", () => head.ChildrenOfType<Box>().Count(), () => Is.EqualTo(1));
            AddAssert("protected LN head keeps scalar colour", () => head.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(0, 255, 0, 255)));
        }

        [Test]
        public void TestLongNoteTailUsesLegacyStaticTextureWhenProvided()
        {
            DefaultBmsLongNoteTailDisplay tail = null!;

            AddStep("load LN tail with NoteImage1T texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nNoteImage1T: notes/tail\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = tail = new DefaultBmsLongNoteTailDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => tail.IsLoaded);
            AddAssert("LN tail shows legacy sprite", () => tail.ChildrenOfType<Sprite>().Any() && !tail.ChildrenOfType<Box>().Any());
            AddAssert("LN tail becomes visible", () => tail.Alpha, () => Is.EqualTo(1));
        }

        [Test]
        public void TestProtectedLongNoteTailFallbackStaysTransparentWithoutAggregateTextureLookup()
        {
            DefaultBmsLongNoteTailDisplay tail = null!;
            TexturedTestSkin skin = null!;

            AddStep("load protected LN tail with aggregate texture", () =>
            {
                skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nNoteImage1T: notes/tail\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = tail = new DefaultBmsLongNoteTailDisplay(
                        1,
                        false,
                        BmsKeymode.Key7K,
                        allowAggregateTextureOverride: false),
                };
            });

            AddUntilStep("loaded", () => tail.IsLoaded);
            AddAssert("protected LN tail stays transparent", () => tail.Alpha, () => Is.Zero);
            AddAssert("protected LN tail keeps migration fallback", () => tail.ChildrenOfType<Box>().Count(), () => Is.EqualTo(1));
            AddAssert("protected LN tail does not mount textured sprite", () => tail.ChildrenOfType<Sprite>().Where(sprite => sprite is not Box), () => Is.Empty);
            AddAssert("protected LN tail skips aggregate texture lookup", () => skin.TextureRequestCount, () => Is.Zero);
        }

        [Test]
        public void TestLaneBackgroundColourFromConfig()
        {
            DefaultBmsLaneBackgroundDisplay lane = null!;

            // 7K lane 1 is an odd-parity background → LaneBackgroundOddColour applies.
            AddStep("load lane bg overriding LaneBackgroundOddColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLaneBackgroundOddColour: 10,20,30\n");
                Child = new SkinProvidingContainer(skin) { Child = lane = new DefaultBmsLaneBackgroundDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => lane.IsLoaded);
            AddAssert("lane bg uses ini colour", () => lane.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(10, 20, 30, 255)));
        }

        [Test]
        public void TestLaneBackgroundUsesSkinTextureWhenProvided()
        {
            DefaultBmsLaneBackgroundDisplay lane = null!;

            // With a LaneBackgroundImage texture present the file skin owns the look: a Sprite is shown, not the box.
            AddStep("load lane bg with LaneBackgroundImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nLaneBackgroundImage1: lanes/white\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = lane = new DefaultBmsLaneBackgroundDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => lane.IsLoaded);
            AddAssert("lane bg shows sprite, not box", () => lane.ChildrenOfType<Sprite>().Any() && !lane.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestLongNoteBodyActiveColourFromConfig()
        {
            DefaultBmsLongNoteBodyDisplay body = null!;

            AddStep("load LN body overriding NoteColourWhite", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nNoteColourWhite: 0,0,255\n");
                Child = new SkinProvidingContainer(skin) { Child = body = new DefaultBmsLongNoteBodyDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => body.IsLoaded);
            AddAssert("LN body active uses ini colour", () => body.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(0, 0, 255, 255)));
        }

        [Test]
        public void TestLongNoteBodyWidthFromConfig()
        {
            DefaultBmsLongNoteBodyDisplay body = null!;

            AddStep("load LN body overriding LongNoteBodyWidth", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLongNoteBodyWidth: 0.9\n");
                Child = new SkinProvidingContainer(skin) { Child = body = new DefaultBmsLongNoteBodyDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => body.IsLoaded);
            AddAssert("LN body width uses ini value", () => body.Width, () => Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void TestLongNoteBodyInvalidWidthsUseSafeDefault()
        {
            foreach (string invalidWidth in new[] { "NaN", "Infinity", "0", "-0.1", "1.1" })
            {
                DefaultBmsLongNoteBodyDisplay body = null!;

                AddStep($"load LN body with invalid width {invalidWidth}", () =>
                {
                    var skin = new TestBmsLegacySkin($"[Bms]\nKeymode: 7K\nLongNoteBodyWidth: {invalidWidth}\n");
                    Child = new SkinProvidingContainer(skin) { Child = body = new DefaultBmsLongNoteBodyDisplay(1, false, BmsKeymode.Key7K) };
                });

                AddUntilStep("loaded", () => body.IsLoaded);
                AddAssert(
                    "LN body uses safe default width",
                    () => body.Width,
                    () => Is.EqualTo(BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH).Within(0.0001f));
            }
        }

        [Test]
        public void TestLongNoteBodyTextureUsesWhiteActiveTint()
        {
            DefaultBmsLongNoteBodyDisplay body = null!;

            AddStep("load LN body texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nNoteImage1L: notes/body\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = body = new DefaultBmsLongNoteBodyDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => body.IsLoaded);
            AddAssert("LN body shows textured sprite", () => body.ChildrenOfType<Sprite>().Count(sprite => sprite is not Box), () => Is.EqualTo(1));
            AddAssert(
                "LN body texture keeps white active tint",
                () => body.ChildrenOfType<Sprite>().Single(sprite => sprite is not Box).Colour.TopLeft.SRGB,
                () => Is.EqualTo(Color4.White));
        }

        [Test]
        public void TestProtectedLongNoteBodyFallbackIgnoresAggregateResourceAndGeometry()
        {
            DefaultBmsLongNoteBodyDisplay body = null!;
            TexturedTestSkin skin = null!;

            AddStep("load protected LN body with aggregate texture and width", () =>
            {
                skin = new TexturedTestSkin(
                    "[Bms]\nKeymode: 7K\nNoteImage1L: notes/body\nLongNoteBodyWidth: 0.9\nNoteColourWhite: 0,0,255\n",
                    renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = body = new DefaultBmsLongNoteBodyDisplay(
                        1,
                        false,
                        BmsKeymode.Key7K,
                        allowAggregateResourceAndGeometryOverride: false),
                };
            });

            AddUntilStep("loaded", () => body.IsLoaded);
            AddAssert("protected LN body stays box", () => body.ChildrenOfType<Box>().Count(), () => Is.EqualTo(1));
            AddAssert("protected LN body skips aggregate texture lookup", () => skin.TextureRequestCount, () => Is.Zero);
            AddAssert(
                "protected LN body ignores aggregate width",
                () => body.Width,
                () => Is.EqualTo(BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH).Within(0.0001f));
            AddAssert(
                "protected LN body keeps scalar colour",
                () => body.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB,
                () => Is.EqualTo(new Color4(0, 0, 255, 255)));
        }

        [Test]
        public void TestLaneDividerColourFromConfig()
        {
            DefaultBmsLaneDividerDisplay divider = null!;

            AddStep("load divider overriding LaneDividerColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLaneDividerColour: 5,6,7\n");
                Child = new SkinProvidingContainer(skin) { Child = divider = new DefaultBmsLaneDividerDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => divider.IsLoaded);
            AddAssert("divider uses ini colour", () => divider.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(5, 6, 7, 255)));
        }

        [Test]
        public void TestLaneDividerUsesSkinTextureWhenProvided()
        {
            DefaultBmsLaneDividerDisplay divider = null!;

            // With a LaneDividerImage texture present the file skin owns the look: a Sprite is shown, not the box.
            AddStep("load divider with LaneDividerImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nLaneDividerImage1: lanes/divider\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = divider = new DefaultBmsLaneDividerDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => divider.IsLoaded);
            AddAssert("divider shows sprite, not box", () => divider.ChildrenOfType<Sprite>().Any() && !divider.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestHitTargetBarColourFromConfig()
        {
            DefaultBmsHitTargetDisplay target = null!;

            AddStep("load hit target overriding HitTargetBarColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nHitTargetBarColour: 1,2,3\n");
                Child = new SkinProvidingContainer(skin)
                {
                    Child = target = new DefaultBmsHitTargetDisplay(false, BmsKeymode.Key7K, BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8))
                };
            });

            AddUntilStep("loaded", () => target.IsLoaded);
            AddAssert("hit target bar uses ini colour", () => target.ChildrenOfType<Box>().Any(b => b.Colour.TopLeft.SRGB == new Color4(1, 2, 3, 255)));
        }

        [Test]
        public void TestHitTargetUsesSkinTextureWhenProvided()
        {
            DefaultBmsHitTargetDisplay target = null!;

            // With a HitTargetImage the texture owns the static look: a Sprite is shown and the programmatic bar/line hide.
            AddStep("load hit target with HitTargetImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nHitTargetImage: stage/target\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = target = new DefaultBmsHitTargetDisplay(false, BmsKeymode.Key7K, BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8))
                };
            });

            AddUntilStep("loaded", () => target.IsLoaded);
            AddAssert("hit target shows sprite", () => target.ChildrenOfType<Sprite>().Any());
        }

        [Test]
        public void TestBarLineColourFromConfig()
        {
            DefaultBmsBarLineDisplay barLine = null!;

            AddStep("load major bar line overriding MajorBarLineColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nMajorBarLineColour: 9,8,7\n");
                Child = new SkinProvidingContainer(skin) { Child = barLine = new DefaultBmsBarLineDisplay(true, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => barLine.IsLoaded);
            AddAssert("bar line uses ini colour", () => barLine.Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(9, 8, 7, 255)));
        }

        [Test]
        public void TestLaneCoverFillColourFromConfig()
        {
            DefaultBmsLaneCoverDisplay cover = null!;

            AddStep("load lane cover overriding LaneCoverFillColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLaneCoverFillColour: 11,22,33\n");
                Child = new SkinProvidingContainer(skin) { Child = cover = new DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition.Sudden, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => cover.IsLoaded);
            AddAssert("lane cover fill uses ini colour", () => cover.ChildrenOfType<Box>().Any(b => b.Colour.TopLeft.SRGB == new Color4(11, 22, 33, 255)));
        }

        [Test]
        public void TestLaneCoverUsesSkinTextureWhenProvided()
        {
            DefaultBmsLaneCoverDisplay cover = null!;

            // A Sudden cover reads LaneCoverTopImage; with a texture present a Sprite is shown instead of the fill box.
            AddStep("load Sudden lane cover with LaneCoverTopImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nLaneCoverTopImage: covers/sudden\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = cover = new DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition.Sudden, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => cover.IsLoaded);
            AddAssert("lane cover shows sprite", () => cover.ChildrenOfType<Sprite>().Any());
        }

        [Test]
        public void TestBaseplateColourFromConfig()
        {
            DefaultBmsPlayfieldBaseplateDisplay baseplate = null!;

            AddStep("load baseplate overriding PlayfieldBaseplateColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nPlayfieldBaseplateColour: 3,4,5\n");
                Child = new SkinProvidingContainer(skin) { Child = baseplate = new DefaultBmsPlayfieldBaseplateDisplay(BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => baseplate.IsLoaded);
            AddAssert("baseplate uses ini colour", () => baseplate.Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(3, 4, 5, 255)));
        }

        [Test]
        public void TestBackdropUsesSkinTextureWhenProvided()
        {
            DefaultBmsPlayfieldBackdropDisplay backdrop = null!;

            // A PlayfieldBackdropImage owns the look: a plain Sprite is shown (not the blurred beatmap-background path).
            AddStep("load backdrop with PlayfieldBackdropImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nPlayfieldBackdropImage: bg/custom\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = backdrop = new DefaultBmsPlayfieldBackdropDisplay(BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => backdrop.IsLoaded);
            AddAssert("backdrop shows skin sprite, not blur path", () => backdrop.ChildrenOfType<Sprite>().Any() && !backdrop.ChildrenOfType<BufferedContainer>().Any());
        }

        private class TestBmsLegacySkin : BmsLegacySkin
        {
            public TestBmsLegacySkin(string ini)
                : base(new SkinInfo { Name = @"test" }, new TestResourceProvider(), new IniStore(ini))
            {
            }
        }

        private class TexturedTestSkin : BmsLegacySkin
        {
            private readonly Texture texture;

            public int TextureRequestCount { get; private set; }

            public TexturedTestSkin(string ini, Texture texture)
                : base(new SkinInfo { Name = @"test" }, new TestResourceProvider(), new IniStore(ini))
            {
                this.texture = texture;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                TextureRequestCount++;
                return texture;
            }
        }

        private class TestResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files { get; } = new ResourceStore<byte[]>();
            public IResourceStore<byte[]> Resources => Files;
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private class IniStore : IResourceStore<byte[]>
        {
            private readonly byte[] data;

            public IniStore(string ini) => data = Encoding.UTF8.GetBytes(ini);

            public byte[] Get(string name) => name == @"skin.ini" ? data : null!;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => name == @"skin.ini" ? new MemoryStream(data) : null!;
            public IEnumerable<string> GetAvailableResources() => new[] { @"skin.ini" };
            public void Dispose() { }
        }
    }
}
