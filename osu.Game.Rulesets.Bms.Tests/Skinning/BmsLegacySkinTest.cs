// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
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
            "Version: 1\n" +
            "[Colours]\n" +
            "Colour1: 255,0,0\n" +
            "[Mania]\n" + // a mania section must coexist untouched (BmsLegacySkin still extends core LegacySkin)
            "Keys: 7\n" +
            "ColumnWidth: 40\n" +
            "[Bms]\n" +
            "Keymode: 7K\n" +
            "PlayfieldWidth: 0.7\n" +
            "NoteColourWhite: 243,243,243\n" +
            "NoteImage1: notes/white\n" +
            "NoteImageSH: notes/scratch_head\n" +
            "LaneBackgroundImage1: lanes/white_bg\n" +
            "LaneDividerImageS: lanes/scratch_divider\n" +
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
        public void TestPerLaneBackgroundImageResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.LaneBackgroundImage, BmsKeymode.Key7K, laneIndex: 1)?.Value, Is.EqualTo("lanes/white_bg"));

        [Test]
        public void TestScratchLaneDividerImageResolved()
            => Assert.That(createSkin().GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.LaneDividerImage, BmsKeymode.Key7K, isScratch: true)?.Value, Is.EqualTo("lanes/scratch_divider"));

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
        public void TestGeneralSectionParsedAfterStreamCopy()
        {
            // Regression test for stream positioning bug (H1): BmsLegacySkin.ParseConfigurationStream copies the
            // stream before passing to base. Without resetting stream.Position = 0, the base LegacySkinDecoder
            // would read an empty stream and [General] values would be permanently lost.
            // LegacyVersion is set from [General] Version: 1 — it would be 0 (default) if the [General] section was lost.
            // LegacySkinConfiguration is not public, so use reflection to access LegacyVersion.
            var config = createSkin().Configuration;
            var legacyVersionProp = config.GetType().GetProperty("LegacyVersion");
            Assert.That(legacyVersionProp, Is.Not.Null, "Configuration should be a LegacySkinConfiguration with LegacyVersion");
            Assert.That(legacyVersionProp!.GetValue(config), Is.EqualTo(1));
        }

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

        [Test]
        public void TestFolderBackedSkinReadsIniDirectlyFromDisk()
        {
            // G1 刀①: a skin folder on disk (chartskin/<name>/skin.ini + assets) is read directly via the public folder
            // ctor + a StorageBackedResourceStore over the folder — no realm hash-backed copy. The empty realm Files list
            // falls through to the folder store, so the [Bms] config parses straight off disk.
            using var folder = new TemporaryNativeStorage($"oms-skin-folder-{Guid.NewGuid():N}");
            File.WriteAllText(folder.GetFullPath(@"skin.ini"), "[Bms]\nKeymode: 7K\nPlayfieldWidth: 0.42\nNoteColourWhite: 1,2,3\n");

            var skin = new BmsLegacySkin(new SkinInfo { Name = @"folder" }, new TestResourceProvider(), new StorageBackedResourceStore(folder));

            Assert.Multiple(() =>
            {
                Assert.That(skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, BmsKeymode.Key7K)?.Value, Is.EqualTo(0.42f));
                Assert.That(skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.NoteColourWhite, BmsKeymode.Key7K)?.Value, Is.EqualTo(new Color4(1, 2, 3, 255)));
            });
        }

        [Test]
        public void TestFolderCtorReflectableForSkinManagerGetSkinPath()
        {
            // G1 刀③: SkinManager.GetSkin reflects into the 3-param ctor
            // (SkinInfo, IStorageResourceProvider, IResourceStore<byte[]>) for folder-backed skins.
            // Pin the signature so a rename/refactor won't silently break the folder-backed instantiation path.
            var folderCtor = typeof(BmsLegacySkin).GetConstructor(
                new[] { typeof(SkinInfo), typeof(IStorageResourceProvider), typeof(IResourceStore<byte[]>) });

            Assert.That(folderCtor, Is.Not.Null);
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
