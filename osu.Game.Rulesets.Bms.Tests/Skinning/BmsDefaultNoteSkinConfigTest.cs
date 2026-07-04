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
                Child = new SkinProvidingContainer(skin) { Child = cover = new DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition.Sudden) };
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
                Child = new SkinProvidingContainer(skin) { Child = cover = new DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition.Sudden) };
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

        [Test]
        public void TestKeyFlashColourOverriddenBySkinConfig()
        {
            DefaultBmsKeyFlashDisplay display = null!;

            AddStep("load key flash overriding KeyFlashColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nKeyFlashColour: 255,0,0\n");
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsKeyFlashDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("key flash uses ini colour", () => display.ChildrenOfType<Box>().Any(b =>
            {
                var c = b.Colour.TopLeft.SRGB;
                return c.R == 1f && c.G == 0f && c.B == 0f;
            }));
        }

        [Test]
        public void TestKeyFlashUsesSkinTextureWhenProvided()
        {
            DefaultBmsKeyFlashDisplay display = null!;

            AddStep("load key flash with KeyFlashImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nKeyFlashImage1: flashes/white\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsKeyFlashDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("key flash shows sprite, not box", () => display.ChildrenOfType<Sprite>().Any() && !display.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestKeyFlashUsesKeyImageWhenProvided()
        {
            DefaultBmsKeyFlashDisplay display = null!;

            // When KeyImage is provided (but no KeyFlashImage), the KeyFlash display enters the KeyImage route:
            // full-lane sprite, always visible, texture swap on press.
            AddStep("load key flash with KeyImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nKeyImage1: keys/white\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsKeyFlashDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("key flash shows sprite via KeyImage route", () => display.ChildrenOfType<Sprite>().Any() && !display.ChildrenOfType<Box>().Any());
            AddAssert("key flash is always visible", () => display.Alpha, () => Is.EqualTo(1f));
        }

        [Test]
        public void TestHitLightingColourOverriddenBySkinConfig()
        {
            DefaultBmsHitLightingDisplay display = null!;

            AddStep("load hit lighting overriding HitLightingColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nHitLightingColour: 0,255,0\n");
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsHitLightingDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("hit lighting uses ini colour", () => display.ChildrenOfType<Box>().Any(b =>
            {
                var c = b.Colour.TopLeft.SRGB;
                return c.R == 0f && c.G == 1f && c.B == 0f;
            }));
        }

        [Test]
        public void TestHitLightingUsesSkinTextureWhenProvided()
        {
            DefaultBmsHitLightingDisplay display = null!;

            AddStep("load hit lighting with HitLightingImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nHitLightingImage1: lighting/hit\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsHitLightingDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("hit lighting shows sprite, not box", () => display.ChildrenOfType<Sprite>().Any() && !display.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestHoldLightColourOverriddenBySkinConfig()
        {
            DefaultBmsHoldLightDisplay display = null!;

            AddStep("load hold light overriding HoldLightColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nHoldLightColour: 0,0,255\n");
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsHoldLightDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("hold light uses ini colour", () => display.ChildrenOfType<Box>().Any(b =>
            {
                var c = b.Colour.TopLeft.SRGB;
                return c.R == 0f && c.G == 0f && c.B == 1f;
            }));
        }

        [Test]
        public void TestHoldLightUsesSkinTextureWhenProvided()
        {
            DefaultBmsHoldLightDisplay display = null!;

            AddStep("load hold light with HoldLightImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nHoldLightImage1: lighting/hold\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsHoldLightDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("hold light shows sprite, not box", () => display.ChildrenOfType<Sprite>().Any() && !display.ChildrenOfType<Box>().Any());
        }

        [Test]
        public void TestMineHitColourOverriddenBySkinConfig()
        {
            DefaultBmsMineHitDisplay display = null!;

            AddStep("load mine hit overriding MineHitColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nMineHitColour: 255,0,255\n");
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsMineHitDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("mine hit uses ini colour", () => display.ChildrenOfType<Box>().Any(b =>
            {
                var c = b.Colour.TopLeft.SRGB;
                return c.R == 1f && c.G == 0f && c.B == 1f;
            }));
        }

        [Test]
        public void TestMineHitUsesSkinTextureWhenProvided()
        {
            DefaultBmsMineHitDisplay display = null!;

            AddStep("load mine hit with MineHitImage texture", () =>
            {
                var skin = new TexturedTestSkin("[Bms]\nKeymode: 7K\nMineHitImage1: explosions/mine\n", renderer.WhitePixel);
                Child = new SkinProvidingContainer(skin) { Child = display = new DefaultBmsMineHitDisplay(1, false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
            AddAssert("mine hit shows sprite, not box", () => display.ChildrenOfType<Sprite>().Any() && !display.ChildrenOfType<Box>().Any());
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

            public TexturedTestSkin(string ini, Texture texture)
                : base(new SkinInfo { Name = @"test" }, new TestResourceProvider(), new IniStore(ini))
            {
                this.texture = texture;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => texture;
        }

        private class TestResourceProvider : IStorageResourceProvider
        {
            private readonly IResourceStore<byte[]> empty = new ResourceStore<byte[]>();

            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files => empty;
            public IResourceStore<byte[]> Resources => empty;
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
