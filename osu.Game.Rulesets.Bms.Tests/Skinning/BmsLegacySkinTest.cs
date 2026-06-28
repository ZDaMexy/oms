// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsLegacySkinTest
    {
        private const string skin_ini =
            "[General]\n" +
            "Name: Test\n" +
            "[Mania]\n" + // a mania section must coexist untouched (BmsLegacySkin still extends core LegacySkin)
            "Keys: 7\n" +
            "ColumnWidth: 40\n" +
            "[Bms]\n" +
            "Keymode: 7K\n" +
            "PlayfieldWidth: 0.7\n" +
            "NoteColourWhite: 243,243,243\n" +
            "NoteImage1: notes/white\n" +
            "NoteImageSH: notes/scratch_head\n" +
            "HitTargetImage: stage/target\n";

        private static BmsLegacySkin createSkin() => new TestBmsLegacySkin(skin_ini);

        [Test]
        public void TestGeometryResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, BmsKeymode.Key7K)?.Value, Is.EqualTo(0.7f));

        [Test]
        public void TestColourResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.NoteColourWhite, BmsKeymode.Key7K)?.Value, Is.EqualTo(new Color4(243, 243, 243, 255)));

        [Test]
        public void TestPerLaneImageResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.NoteImage, BmsKeymode.Key7K, laneIndex: 1)?.Value, Is.EqualTo("notes/white"));

        [Test]
        public void TestScratchHoldHeadImageResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.HoldNoteHeadImage, BmsKeymode.Key7K, isScratch: true)?.Value, Is.EqualTo("notes/scratch_head"));

        [Test]
        public void TestGlobalImageResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.HitTargetImage, BmsKeymode.Key7K)?.Value, Is.EqualTo("stage/target"));

        [Test]
        public void TestUnsetKeyOrKeymodeReturnsNull()
        {
            var skin = createSkin();
            Assert.That(skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldHeight, BmsKeymode.Key7K), Is.Null); // key not set
            Assert.That(skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, BmsKeymode.Key14K), Is.Null); // keymode not declared
        }

        [Test]
        public void TestWrongTypeReturnsNullNotThrow()
            // PlayfieldWidth is a float key; querying as Color4 must return null (typeof guard), never throw a cast.
            => Assert.That(createSkin().GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.PlayfieldWidth, BmsKeymode.Key7K), Is.Null);

        [Test]
        public void TestManiaSectionStillParsed()
            // The BMS layer must not break core mania-section parsing.
            => Assert.That(createSkin().GetConfig<LegacyManiaSkinConfigurationLookup, float>(
                new LegacyManiaSkinConfigurationLookup(7, LegacyManiaSkinConfigurationLookups.ColumnWidth, 0))?.Value, Is.EqualTo(40f * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR));

        [Test]
        public void TestTypeStringsUsedByCoreResolveToThisType()
        {
            // osu.Game can't reference the ruleset, so SkinImporter (assembly-qualified name) and SkinnableSprite (full
            // name) hard-code BmsLegacySkin's type strings to route imported skins through it. Guard both so a namespace
            // or assembly rename can't silently disable BMS skinning.
            Assert.Multiple(() =>
            {
                Assert.That(Type.GetType(@"osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms"), Is.EqualTo(typeof(BmsLegacySkin)));
                Assert.That(typeof(BmsLegacySkin).FullName, Is.EqualTo(@"osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin"));
            });
        }

        private class TestBmsLegacySkin : BmsLegacySkin
        {
            public TestBmsLegacySkin(string ini)
                : base(new SkinInfo { Name = @"test" }, new TestResourceProvider(), new IniStore(ini))
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

        // Fallback store serving a single in-memory skin.ini — the same route OmsSkin uses for its built-in ini
        // (osu skins read skin.ini through the realm-backed store + fallbackStore, not resources.Files directly).
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
