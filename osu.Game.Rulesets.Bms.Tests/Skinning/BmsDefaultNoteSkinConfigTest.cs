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
            AddAssert("lane bg uses ini colour", () => lane.Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(10, 20, 30, 255)));
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
        public void TestLaneDividerColourFromConfig()
        {
            DefaultBmsLaneDividerDisplay divider = null!;

            AddStep("load divider overriding LaneDividerColour", () =>
            {
                var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLaneDividerColour: 5,6,7\n");
                Child = new SkinProvidingContainer(skin) { Child = divider = new DefaultBmsLaneDividerDisplay(false, BmsKeymode.Key7K) };
            });

            AddUntilStep("loaded", () => divider.IsLoaded);
            AddAssert("divider uses ini colour", () => divider.Colour.TopLeft.SRGB, () => Is.EqualTo(new Color4(5, 6, 7, 255)));
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
