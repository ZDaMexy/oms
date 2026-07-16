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
using osu.Framework.Extensions;
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
using osu.Game.Skinning.Gameplay;
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
            "HitTargetImage: stage/target\n" +
            "[Bms]\n" +
            "Keymode: 14K\n" +
            "NoteImageS: notes/scratch_p1\n" +
            "NoteImageS2: notes/scratch_p2\n";

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
        public void TestGeneralSectionStillParsed()
        {
            var config = createSkin().Configuration;
            var legacyVersion = config.GetType().GetProperty("LegacyVersion");

            Assert.That(legacyVersion, Is.Not.Null);
            Assert.That(legacyVersion!.GetValue(config), Is.EqualTo(1));
        }

        [TestCase(0, "notes/scratch_p1")]
        [TestCase(8, "notes/scratch_p2")]
        public void TestDoublePlayScratchImageResolved(int laneIndex, string expected)
            => Assert.That(createSkin().GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.NoteImage,
                BmsKeymode.Key14K,
                laneIndex,
                isScratch: true)?.Value, Is.EqualTo(expected));

        [TestCaseSource(nameof(canonicalOrdinaryNoteDeclarations))]
        public void TestAcceptedOrdinaryNoteResourceUsesCanonicalLaneToken(
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch,
            string expected)
        {
            GameplaySkinConfigurationDeclaration<string> declaration =
                new TestBmsLegacySkin(createExactOrdinaryNoteSkinIni()).GetAcceptedBmsOrdinaryNoteResource(keymode, laneIndex, isScratch);

            Assert.Multiple(() =>
            {
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(declaration.Value, Is.EqualTo(expected));
            });
        }

        [Test]
        public void TestAcceptedNoteResourceUsesClosedElementMappingAndCanonicalSecondScratch()
        {
            var skin = new TestBmsLegacySkin(
                "[Bms]\n" +
                "Keymode: 14K\n" +
                 "NoteImageS2: ordinary-second-scratch\n" +
                 "NoteImageS2H: head-second-scratch\n" +
                 "NoteImageS2L: body-second-scratch\n" +
                 "NoteImageS2T: tail-second-scratch\n");

            GameplaySkinConfigurationDeclaration<string> ordinary = skin.GetAcceptedBmsNoteResource(
                BmsNoteSkinElements.Note,
                BmsKeymode.Key14K,
                laneIndex: 15,
                isScratch: true);
            GameplaySkinConfigurationDeclaration<string> head = skin.GetAcceptedBmsNoteResource(
                BmsNoteSkinElements.LongNoteHead,
                BmsKeymode.Key14K,
                laneIndex: 15,
                isScratch: true);
            GameplaySkinConfigurationDeclaration<string> body = skin.GetAcceptedBmsNoteResource(
                BmsNoteSkinElements.LongNoteBody,
                BmsKeymode.Key14K,
                laneIndex: 15,
                isScratch: true);
            GameplaySkinConfigurationDeclaration<string> tail = skin.GetAcceptedBmsNoteResource(
                BmsNoteSkinElements.LongNoteTail,
                BmsKeymode.Key14K,
                laneIndex: 15,
                isScratch: true);
            GameplaySkinConfigurationDeclaration<string> unsupported = skin.GetAcceptedBmsNoteResource(
                (BmsNoteSkinElements)999,
                BmsKeymode.Key14K,
                laneIndex: 15,
                isScratch: true);

            Assert.Multiple(() =>
            {
                Assert.That(ordinary.IsDeclared, Is.True);
                Assert.That(ordinary.Value, Is.EqualTo("ordinary-second-scratch"));
                Assert.That(head.IsDeclared, Is.True);
                Assert.That(head.Value, Is.EqualTo("head-second-scratch"));
                Assert.That(body.IsDeclared, Is.True);
                Assert.That(body.Value, Is.EqualTo("body-second-scratch"));
                Assert.That(tail.IsDeclared, Is.True);
                Assert.That(tail.Value, Is.EqualTo("tail-second-scratch"));
                Assert.That(unsupported.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestAcceptedGeometryUsesExactKeymodeBucketAndPreservesParserValue()
        {
            var skin = new TestBmsLegacySkin(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "LongNoteBodyWidth: NaN\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "LongNoteBodyWidth: 0.25\n");

            GameplaySkinConfigurationDeclaration<float> seven = skin.GetAcceptedBmsGeometry(
                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                BmsKeymode.Key7K);
            GameplaySkinConfigurationDeclaration<float> fourteen = skin.GetAcceptedBmsGeometry(
                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                BmsKeymode.Key14K);
            GameplaySkinConfigurationDeclaration<float> missingBucket = skin.GetAcceptedBmsGeometry(
                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                BmsKeymode.Key5K);
            GameplaySkinConfigurationDeclaration<float> missingField = skin.GetAcceptedBmsGeometry(
                BmsSkinConfigurationLookups.PlayfieldWidth,
                BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(seven.IsDeclared, Is.True);
                Assert.That(float.IsNaN(seven.Value), Is.True);
                Assert.That(fourteen.Value, Is.EqualTo(0.25f));
                Assert.That(missingBucket.IsDeclared, Is.False);
                Assert.That(missingField.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestGeometryCompatibilityMutationCannotForgeOrAlterAcceptedDeclaration()
        {
            var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nLongNoteBodyWidth: 0.75\n");
            BmsSkinConfiguration configuration = getBmsConfiguration(skin, BmsKeymode.Key7K);

            configuration.Geometry[BmsSkinConfigurationLookups.LongNoteBodyWidth] = 0.25f;
            configuration.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth] = 0.5f;

            Assert.Multiple(() =>
            {
                Assert.That(skin.GetAcceptedBmsGeometry(
                    BmsSkinConfigurationLookups.LongNoteBodyWidth,
                    BmsKeymode.Key7K).Value, Is.EqualTo(0.75f));
                Assert.That(skin.GetAcceptedBmsGeometry(
                    BmsSkinConfigurationLookups.PlayfieldWidth,
                    BmsKeymode.Key7K).IsDeclared, Is.False);
                Assert.That(() => skin.GetAcceptedBmsGeometry(
                    BmsSkinConfigurationLookups.NoteColourWhite,
                    BmsKeymode.Key7K), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestParsedConfigurationHashIsCapturedFromExactBytes()
        {
            const string ini = "[Bms]\r\nKeymode: 7K\r\nLongNoteBodyWidth: 0.75\r\n";
            var skin = new TestBmsLegacySkin(ini);
            string expected;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ini)))
                expected = stream.ComputeSHA2Hash();

            Assert.That(skin.CaptureManagedPackageSourceRevision().ParsedConfigurationContentHash, Is.EqualTo(expected));
        }

        [Test]
        public void TestManagedRevisionRequiresParsedConfigurationHashToMatchPackage()
        {
            string configurationHash = "a".PadLeft(64, 'a');
            var files = new[]
            {
                new BmsManagedPackageFileRevision("skin.ini", configurationHash, "aa/skin"),
                new BmsManagedPackageFileRevision("body.png", "b".PadLeft(64, 'b'), "bb/body"),
            };
            Guid skinId = Guid.NewGuid();
            var matching = new BmsManagedPackageSourceRevision(
                skinId, true, null, false, false, configurationHash, files);
            var mismatch = new BmsManagedPackageSourceRevision(
                skinId, true, null, false, false, "c".PadLeft(64, 'c'), files);
            var otherMismatch = new BmsManagedPackageSourceRevision(
                skinId, true, null, false, false, "d".PadLeft(64, 'd'), files);

            Assert.Multiple(() =>
            {
                Assert.That(matching.HasGameplayAuthority, Is.True);
                Assert.That(mismatch.HasGameplayAuthority, Is.False);
                Assert.That(otherMismatch.HasGameplayAuthority, Is.False);
                Assert.That(matching, Is.Not.EqualTo(mismatch));
                Assert.That(mismatch, Is.Not.EqualTo(otherMismatch));
            });
        }

        [TestCase(BmsKeymode.Key5K, -1, false)]
        [TestCase(BmsKeymode.Key5K, 6, false)]
        [TestCase(BmsKeymode.Key5K, 0, false)]
        [TestCase(BmsKeymode.Key5K, 1, true)]
        [TestCase(BmsKeymode.Key7K, -1, false)]
        [TestCase(BmsKeymode.Key7K, 8, false)]
        [TestCase(BmsKeymode.Key7K, 0, false)]
        [TestCase(BmsKeymode.Key7K, 1, true)]
        [TestCase(BmsKeymode.Key9K_Bms, -1, false)]
        [TestCase(BmsKeymode.Key9K_Bms, 9, false)]
        [TestCase(BmsKeymode.Key9K_Bms, 0, true)]
        [TestCase(BmsKeymode.Key9K_Pms, -1, false)]
        [TestCase(BmsKeymode.Key9K_Pms, 9, false)]
        [TestCase(BmsKeymode.Key9K_Pms, 8, true)]
        [TestCase(BmsKeymode.Key14K, -1, false)]
        [TestCase(BmsKeymode.Key14K, 16, false)]
        [TestCase(BmsKeymode.Key14K, 0, false)]
        [TestCase(BmsKeymode.Key14K, 15, false)]
        [TestCase(BmsKeymode.Key14K, 1, true)]
        [TestCase(BmsKeymode.Key14K, 14, true)]
        public void TestAcceptedOrdinaryNoteResourceRejectsOutOfRangeAndRoleMismatch(
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch)
        {
            GameplaySkinConfigurationDeclaration<string> declaration =
                new TestBmsLegacySkin(createExactOrdinaryNoteSkinIni()).GetAcceptedBmsOrdinaryNoteResource(keymode, laneIndex, isScratch);

            Assert.That(declaration.IsDeclared, Is.False);
        }

        [Test]
        public void TestAcceptedOrdinaryNoteResourcePreservesExplicitEmptyDeclaration()
        {
            var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nNoteImage2:\n");
            GameplaySkinConfigurationDeclaration<string> declaration =
                skin.GetAcceptedBmsOrdinaryNoteResource(BmsKeymode.Key7K, 2, isScratch: false);

            Assert.Multiple(() =>
            {
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(declaration.Value, Is.Empty);
            });
        }

        [Test]
        public void TestImageLookupMutationCannotChangeAcceptedOrdinaryNoteDeclaration()
        {
            var skin = new TestBmsLegacySkin("[Bms]\nKeymode: 7K\nNoteImage1: accepted\n");
            BmsSkinConfiguration configuration = getBmsConfiguration(skin, BmsKeymode.Key7K);

            configuration.ImageLookups["NoteImage1"] = "overwritten";
            assertAcceptedOrdinaryNote(skin, BmsKeymode.Key7K, 1, false, "accepted");

            configuration.ImageLookups.Remove("NoteImage1");
            assertAcceptedOrdinaryNote(skin, BmsKeymode.Key7K, 1, false, "accepted");

            configuration.ImageLookups.Clear();
            assertAcceptedOrdinaryNote(skin, BmsKeymode.Key7K, 1, false, "accepted");

            configuration.ImageLookups["NoteImage2"] = "late-added";
            Assert.That(skin.GetAcceptedBmsOrdinaryNoteResource(BmsKeymode.Key7K, 2, isScratch: false).IsDeclared, Is.False);
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

        private static IEnumerable<TestCaseData> canonicalOrdinaryNoteDeclarations()
        {
            yield return ordinaryNoteCase(BmsKeymode.Key5K, 0, true, "5k-S");

            for (int laneIndex = 1; laneIndex <= 5; laneIndex++)
                yield return ordinaryNoteCase(BmsKeymode.Key5K, laneIndex, false, $"5k-{laneIndex}");

            yield return ordinaryNoteCase(BmsKeymode.Key7K, 0, true, "7k-S");

            for (int laneIndex = 1; laneIndex <= 7; laneIndex++)
                yield return ordinaryNoteCase(BmsKeymode.Key7K, laneIndex, false, $"7k-{laneIndex}");

            for (int laneIndex = 0; laneIndex <= 8; laneIndex++)
            {
                yield return ordinaryNoteCase(BmsKeymode.Key9K_Bms, laneIndex, false, $"9k-bms-{laneIndex}");
                yield return ordinaryNoteCase(BmsKeymode.Key9K_Pms, laneIndex, false, $"9k-pms-{laneIndex}");
            }

            yield return ordinaryNoteCase(BmsKeymode.Key14K, 0, true, "14k-S");

            for (int laneIndex = 1; laneIndex <= 14; laneIndex++)
                yield return ordinaryNoteCase(BmsKeymode.Key14K, laneIndex, false, $"14k-{laneIndex}");

            yield return ordinaryNoteCase(BmsKeymode.Key14K, 15, true, "14k-S2");
        }

        private static TestCaseData ordinaryNoteCase(BmsKeymode keymode, int laneIndex, bool isScratch, string expected)
            => new TestCaseData(keymode, laneIndex, isScratch, expected)
               .SetName($"{nameof(TestAcceptedOrdinaryNoteResourceUsesCanonicalLaneToken)}({keymode},{laneIndex},{isScratch})");

        private static string createExactOrdinaryNoteSkinIni()
        {
            var builder = new StringBuilder();

            appendOrdinaryNoteBucket(builder, "5K", "5k", new[] { "S", "1", "2", "3", "4", "5" });
            appendOrdinaryNoteBucket(builder, "7K", "7k", new[] { "S", "1", "2", "3", "4", "5", "6", "7" });
            appendOrdinaryNoteBucket(builder, "9K_BMS", "9k-bms", new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" });
            appendOrdinaryNoteBucket(builder, "9K_PMS", "9k-pms", new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" });
            appendOrdinaryNoteBucket(builder, "14K", "14k", new[] { "S", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "S2" });

            return builder.ToString();
        }

        private static void appendOrdinaryNoteBucket(StringBuilder builder, string keymode, string resourcePrefix, IEnumerable<string> laneTokens)
        {
            builder.AppendLine("[Bms]");
            builder.AppendLine($"Keymode: {keymode}");

            foreach (string laneToken in laneTokens)
                builder.AppendLine($"NoteImage{laneToken}: {resourcePrefix}-{laneToken}");
        }

        private static BmsSkinConfiguration getBmsConfiguration(BmsLegacySkin skin, BmsKeymode keymode)
        {
            FieldInfo? field = typeof(BmsLegacySkin).GetField("bmsConfigurations", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);

            var configurations = (Dictionary<BmsKeymode, BmsSkinConfiguration>)field!.GetValue(skin)!;
            return configurations[keymode];
        }

        private static void assertAcceptedOrdinaryNote(
            BmsLegacySkin skin,
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch,
            string expected)
        {
            GameplaySkinConfigurationDeclaration<string> declaration =
                skin.GetAcceptedBmsOrdinaryNoteResource(keymode, laneIndex, isScratch);

            Assert.Multiple(() =>
            {
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(declaration.Value, Is.EqualTo(expected));
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
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files => Resources;
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
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
