// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// F1 acceptance gate: the reference skin (the creator-facing <c>skin.ini</c> template documented under
    /// <c>doc_md/other/oms-bms-reference-skin/</c>) must reproduce the built-in programmatic default look exactly.
    /// Every key is asserted against the actual palette / profile constant, so a wrong value in the template — or a
    /// drift in a default — fails here. This is the authoritative copy of the reference ini; keep the doc file in sync.
    /// </summary>
    [TestFixture]
    public class BmsReferenceSkinTest
    {
        // Mirrors doc_md/other/oms-bms-reference-skin/skin.ini (7K). Comments use // (stripped by the decoder).
        private const string reference_skin_ini =
            "// OMS BMS reference skin (7K) — reproduces the built-in programmatic default look.\n" +
            "[General]\n" +
            "Keymodes: 7K\n" +
            "\n" +
            "[Bms]\n" +
            "Keymode: 7K\n" +
            "\n" +
            "// --- Geometry ---\n" +
            "PlayfieldWidth: 0.396\n" +
            "PlayfieldHeight: 0.92\n" +
            "NormalLaneWidth: 1.0\n" +
            "ScratchLaneWidth: 1.5\n" +
            "NormalLaneSpacing: 0.0\n" +
            "ScratchLaneSpacing: 0.12\n" +
            "HitTargetHeight: 16\n" +
            "HitTargetBarHeight: 12\n" +
            "HitTargetLineHeight: 3\n" +
            "HitTargetGlowRadius: 6\n" +
            "BarLineHeight: 2\n" +
            "LongNoteBodyWidth: 0.5775\n" +
            "\n" +
            "// --- Note colours (IIDX key-colour groups) ---\n" +
            "NoteColourWhite: 243,243,243\n" +
            "NoteColourCyan: 53,234,255\n" +
            "NoteColourYellow: 255,222,53\n" +
            "NoteColourScratch: 252,0,20\n" +
            "\n" +
            "// --- Lane backgrounds / dividers ---\n" +
            "LaneBackgroundEvenColour: 24,30,45\n" +
            "LaneBackgroundOddColour: 28,34,50\n" +
            "ScratchLaneBackgroundColour: 40,29,20\n" +
            "LaneDividerColour: 88,102,128\n" +
            "ScratchLaneDividerColour: 220,170,100\n" +
            "\n" +
            "// --- Hit target (judgement line) ---\n" +
            "HitTargetBarColour: 8,12,20,232\n" +
            "HitTargetLineColour: 238,243,251\n" +
            "HitTargetGlowColour: 120,196,255,172\n" +
            "ScratchHitTargetBarColour: 22,15,10,236\n" +
            "ScratchHitTargetLineColour: 255,198,116\n" +
            "ScratchHitTargetGlowColour: 255,186,104,172\n" +
            "\n" +
            "// --- Bar lines ---\n" +
            "MajorBarLineColour: 214,224,243,182\n" +
            "MinorBarLineColour: 138,152,182,102\n" +
            "\n" +
            "// --- Lane cover ---\n" +
            "LaneCoverFillColour: 8,12,20\n" +
            "LaneCoverShadeColour: 19,26,39\n" +
            "LaneCoverFocusColour: 255,196,112\n" +
            "\n" +
            "// --- Playfield shell ---\n" +
            "PlayfieldBackdropColour: 4,8,14\n" +
            "PlayfieldBaseplateColour: 10,16,28\n";

        [Test]
        public void TestReferenceSkinReproducesProgrammaticGeometryDefaults()
        {
            var skin = new TestBmsLegacySkin(reference_skin_ini);
            var profile = BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8);

            Assert.Multiple(() =>
            {
                assertGeometry(skin, BmsSkinConfigurationLookups.PlayfieldWidth, profile.PlayfieldWidth);
                assertGeometry(skin, BmsSkinConfigurationLookups.PlayfieldHeight, profile.PlayfieldHeight);
                assertGeometry(skin, BmsSkinConfigurationLookups.NormalLaneWidth, profile.NormalLaneRelativeWidth);
                assertGeometry(skin, BmsSkinConfigurationLookups.ScratchLaneWidth, profile.ScratchLaneRelativeWidth);
                assertGeometry(skin, BmsSkinConfigurationLookups.NormalLaneSpacing, profile.NormalLaneRelativeSpacing);
                assertGeometry(skin, BmsSkinConfigurationLookups.ScratchLaneSpacing, profile.ScratchLaneRelativeSpacing);
                assertGeometry(skin, BmsSkinConfigurationLookups.HitTargetHeight, profile.HitTargetHeight);
                assertGeometry(skin, BmsSkinConfigurationLookups.HitTargetBarHeight, profile.HitTargetBarHeight);
                assertGeometry(skin, BmsSkinConfigurationLookups.HitTargetLineHeight, profile.HitTargetLineHeight);
                assertGeometry(skin, BmsSkinConfigurationLookups.HitTargetGlowRadius, profile.HitTargetGlowRadius);
                assertGeometry(skin, BmsSkinConfigurationLookups.BarLineHeight, profile.BarLineHeight);
                // LongNoteBodyWidth lives on the LN body element (not the profile); its default is 0.5775.
                assertGeometry(skin, BmsSkinConfigurationLookups.LongNoteBodyWidth, 0.5775f);
            });
        }

        [Test]
        public void TestReferenceSkinReproducesProgrammaticColourDefaults()
        {
            var skin = new TestBmsLegacySkin(reference_skin_ini);

            Assert.Multiple(() =>
            {
                assertColour(skin, BmsSkinConfigurationLookups.NoteColourWhite, BmsDefaultPlayfieldPalette.WhiteKeyNote);
                assertColour(skin, BmsSkinConfigurationLookups.NoteColourCyan, BmsDefaultPlayfieldPalette.CyanKeyNote);
                assertColour(skin, BmsSkinConfigurationLookups.NoteColourYellow, BmsDefaultPlayfieldPalette.YellowKeyNote);
                assertColour(skin, BmsSkinConfigurationLookups.NoteColourScratch, BmsDefaultPlayfieldPalette.ScratchNote);

                assertColour(skin, BmsSkinConfigurationLookups.LaneBackgroundEvenColour, BmsDefaultPlayfieldPalette.LaneBackgroundEven);
                assertColour(skin, BmsSkinConfigurationLookups.LaneBackgroundOddColour, BmsDefaultPlayfieldPalette.LaneBackgroundOdd);
                assertColour(skin, BmsSkinConfigurationLookups.ScratchLaneBackgroundColour, BmsDefaultPlayfieldPalette.ScratchLaneBackground);
                assertColour(skin, BmsSkinConfigurationLookups.LaneDividerColour, BmsDefaultPlayfieldPalette.LaneDivider);
                assertColour(skin, BmsSkinConfigurationLookups.ScratchLaneDividerColour, BmsDefaultPlayfieldPalette.ScratchLaneDivider);

                assertColour(skin, BmsSkinConfigurationLookups.HitTargetBarColour, BmsDefaultPlayfieldPalette.HitTargetBar);
                assertColour(skin, BmsSkinConfigurationLookups.HitTargetLineColour, BmsDefaultPlayfieldPalette.HitTargetLine);
                assertColour(skin, BmsSkinConfigurationLookups.HitTargetGlowColour, BmsDefaultPlayfieldPalette.HitTargetGlow);
                assertColour(skin, BmsSkinConfigurationLookups.ScratchHitTargetBarColour, BmsDefaultPlayfieldPalette.ScratchHitTargetBar);
                assertColour(skin, BmsSkinConfigurationLookups.ScratchHitTargetLineColour, BmsDefaultPlayfieldPalette.ScratchHitTargetLine);
                assertColour(skin, BmsSkinConfigurationLookups.ScratchHitTargetGlowColour, BmsDefaultPlayfieldPalette.ScratchHitTargetGlow);

                assertColour(skin, BmsSkinConfigurationLookups.MajorBarLineColour, BmsDefaultPlayfieldPalette.MajorBarLine);
                assertColour(skin, BmsSkinConfigurationLookups.MinorBarLineColour, BmsDefaultPlayfieldPalette.MinorBarLine);

                assertColour(skin, BmsSkinConfigurationLookups.LaneCoverFillColour, BmsDefaultPlayfieldPalette.LaneCoverFill);
                assertColour(skin, BmsSkinConfigurationLookups.LaneCoverShadeColour, BmsDefaultPlayfieldPalette.LaneCoverShade);
                assertColour(skin, BmsSkinConfigurationLookups.LaneCoverFocusColour, BmsDefaultPlayfieldPalette.FocusAccent);

                assertColour(skin, BmsSkinConfigurationLookups.PlayfieldBackdropColour, BmsDefaultPlayfieldPalette.PlayfieldBackdrop);
                assertColour(skin, BmsSkinConfigurationLookups.PlayfieldBaseplateColour, BmsDefaultPlayfieldPalette.PlayfieldBaseplate);
            });
        }

        private static void assertGeometry(BmsLegacySkin skin, BmsSkinConfigurationLookups lookup, float expected)
            => Assert.That(skin.GetBmsSkinConfig<float>(lookup, BmsKeymode.Key7K)?.Value, Is.EqualTo(expected).Within(0.0001f), lookup.ToString());

        private static void assertColour(BmsLegacySkin skin, BmsSkinConfigurationLookups lookup, Color4 expected)
            => Assert.That(skin.GetBmsSkinConfig<Color4>(lookup, BmsKeymode.Key7K)?.Value, Is.EqualTo(expected), lookup.ToString());

        private class TestBmsLegacySkin : BmsLegacySkin
        {
            public TestBmsLegacySkin(string ini)
                : base(new SkinInfo { Name = @"reference" }, new TestResourceProvider(), new IniStore(ini))
            {
            }
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
