// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinBucketColourSnapshotFactoryTest
    {
        private static readonly (string Key, string Value, Color4 Expected)[] exact_colour_cases =
        {
            ("NoteColourWhite", "1,2,3", new Color4(1, 2, 3, 255)),
            ("NoteColourCyan", "4,5,6,0", new Color4(4, 5, 6, 0)),
            ("NoteColourYellow", "7,8,9,10", new Color4(7, 8, 9, 10)),
            ("NoteColourScratch", "11,12,13", new Color4(11, 12, 13, 255)),
            ("LaneBackgroundEvenColour", "14,15,16,17", new Color4(14, 15, 16, 17)),
            ("LaneBackgroundOddColour", "18,19,20", new Color4(18, 19, 20, 255)),
            ("ScratchLaneBackgroundColour", "21,22,23,24", new Color4(21, 22, 23, 24)),
            ("LaneDividerColour", "25,26,27", new Color4(25, 26, 27, 255)),
            ("ScratchLaneDividerColour", "28,29,30,31", new Color4(28, 29, 30, 31)),
            ("HitTargetBarColour", "32,33,34", new Color4(32, 33, 34, 255)),
            ("HitTargetLineColour", "35,36,37,38", new Color4(35, 36, 37, 38)),
            ("HitTargetGlowColour", "39,40,41", new Color4(39, 40, 41, 255)),
            ("ScratchHitTargetBarColour", "42,43,44,45", new Color4(42, 43, 44, 45)),
            ("ScratchHitTargetLineColour", "46,47,48", new Color4(46, 47, 48, 255)),
            ("ScratchHitTargetGlowColour", "49,50,51,52", new Color4(49, 50, 51, 52)),
            ("MajorBarLineColour", "53,54,55", new Color4(53, 54, 55, 255)),
            ("MinorBarLineColour", "56,57,58,59", new Color4(56, 57, 58, 59)),
            ("LaneCoverFillColour", "60,61,62", new Color4(60, 61, 62, 255)),
            ("LaneCoverShadeColour", "63,64,65,66", new Color4(63, 64, 65, 66)),
            ("LaneCoverFocusColour", "67,68,69", new Color4(67, 68, 69, 255)),
            ("PlayfieldBackdropColour", "70,71,72,73", new Color4(70, 71, 72, 73)),
            ("PlayfieldBaseplateColour", "74,75,76", new Color4(74, 75, 76, 255)),
        };

        [Test]
        public void TestMissingBucketIsAbsent()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "NoteColourWhite: 1,2,3\n");

            var declaration = BmsGameplaySkinBucketColourSnapshotFactory.Create(
                decoder.Configurations, BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestExplicitEmptyBucketDeclaresAllColoursAbsent()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceKeymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.All, Has.Count.EqualTo(22));

                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketColourFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [Test]
        public void TestAllTwentyTwoExactRgbAndRgbaDeclarationsAreCaptured()
        {
            var ini = new StringBuilder("[Bms]\nKeymode: 7K\n");

            foreach (var entry in exact_colour_cases)
                ini.AppendLine($"{entry.Key}: {entry.Value}");

            BmsSkinDecoder decoder = decode(ini.ToString());
            BmsSkinConfiguration configuration = decoder.Configurations.Single();
            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                foreach (var entry in exact_colour_cases)
                {
                    var lookup = Enum.Parse<BmsSkinConfigurationLookups>(entry.Key);
                    Assert.That(snapshot.GetDeclaration(lookup).IsDeclared, Is.True, entry.Key);
                    Assert.That(snapshot.GetDeclaration(lookup).Value, Is.EqualTo(entry.Expected), entry.Key);
                    Assert.That(configuration.Colours[lookup], Is.EqualTo(entry.Expected), $"compatibility:{entry.Key}");
                }
            });
        }

        [Test]
        public void TestByteParserCompatibilityIsPreserved()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: +1,002,-0,+004\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                Is.EqualTo(new Color4(1, 2, 0, 4)));
        }

        [TestCase("1,2")]
        [TestCase("1,2,3,4,5")]
        [TestCase("256,2,3")]
        [TestCase("-1,2,3")]
        [TestCase("1.5,2,3")]
        [TestCase("0x1,2,3")]
        [TestCase("1,,3")]
        [TestCase("١,2,3")]
        [TestCase("")]
        public void TestMalformedColourDoesNotDeclare(string value)
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"NoteColourWhite: {value}\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).IsDeclared, Is.False);
        }

        [Test]
        public void TestValidDuplicateUsesLastAcceptedValue()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: 1,2,3\n" +
                "NoteColourWhite: 4,5,6,7\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                Is.EqualTo(new Color4(4, 5, 6, 7)));
        }

        [Test]
        public void TestMalformedDuplicateDoesNotEraseLastAcceptedValue()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: 1,2,3,4\n" +
                "NoteColourWhite: 256,5,6,7\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                Is.EqualTo(new Color4(1, 2, 3, 4)));
        }

        [Test]
        public void TestPendingAndRepeatedBucketsMergeIntoExactKeymode()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "NoteColourWhite: 1,2,3\n" +
                "Keymode: 7K\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "NoteColourWhite: 20,21,22\n" +
                "[Bms]\n" +
                "NoteColourCyan: 4,5,6,7\n" +
                "Keymode: 7K\n");

            BmsGameplaySkinBucketColourSnapshot seven =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;
            BmsGameplaySkinBucketColourSnapshot fourteen =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key14K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Has.Count.EqualTo(2));
                Assert.That(seven.SourceKeymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(seven.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 255)));
                Assert.That(seven.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).Value,
                    Is.EqualTo(new Color4(4, 5, 6, 7)));
                Assert.That(fourteen.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                    Is.EqualTo(new Color4(20, 21, 22, 255)));
                Assert.That(fourteen.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestRepeatedParseMergesAndUsesLastAcceptedExactValue()
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nKeymode: 7K\nNoteColourWhite: 1,2,3\n");
            decoder.Parse("[Bms]\nKeymode: 7K\nNoteColourWhite: 4,5,6,7\nNoteColourCyan: 8,9,10\n");

            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                    Is.EqualTo(new Color4(4, 5, 6, 7)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).Value,
                    Is.EqualTo(new Color4(8, 9, 10, 255)));
            });
        }

        [Test]
        public void TestCompositeEnumKeysRemainCompatibilityOnly()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourCyan: 1,2,3,4\n" +
                "NoteColourWhite, NoteColourCyan: 9,8,7,6\n" +
                "LaneBackgroundEvenColour, LaneBackgroundOddColour: 11,12,13,14\n");
            BmsSkinConfiguration configuration = decoder.Configurations.Single();
            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                // Enum.TryParse currently folds these comma-composites to a defined enum value. Keep that mutable
                // compatibility view unchanged, but do not promote the non-exact source spelling into provenance.
                Assert.That(configuration.Colours[BmsSkinConfigurationLookups.NoteColourCyan],
                    Is.EqualTo(new Color4(9, 8, 7, 6)));
                Assert.That(configuration.Colours[BmsSkinConfigurationLookups.LaneBackgroundOddColour],
                    Is.EqualTo(new Color4(11, 12, 13, 14)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 4)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.LaneBackgroundEvenColour).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.LaneBackgroundOddColour).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestNonExactKeysAndOtherSectionsDoNotDeclareColours()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: 1,2,3\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "notecolourwhite: 4,5,6\n" +
                "28: 7,8,9\n" +
                "GaugeColour: 10,11,12\n" +
                "NoteColourWhiteSuffix: 13,14,15\n" +
                "[Colours]\n" +
                "NoteColourWhite: 16,17,18\n" +
                "[Mania]\n" +
                "NoteColourCyan: 19,20,21\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestInvalidKeymodeClearsEarlierPendingButLaterPendingReplays()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "NoteColourWhite: 1,2,3\n" +
                "Keymode: invalid\n" +
                "NoteColourCyan: 4,5,6\n" +
                "Keymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).Value,
                    Is.EqualTo(new Color4(4, 5, 6, 255)));
            });
        }

        [Test]
        public void TestCompatibilityDictionaryCannotForgeEraseOrAlterAcceptedProvenance()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: 1,2,3,4\n" +
                "NoteColourCyan: 5,6,7,8\n" +
                "NoteColourYellow: 9,10,11,12\n");
            BmsSkinConfiguration configuration = decoder.Configurations.Single();

            configuration.Colours[BmsSkinConfigurationLookups.NoteColourWhite] = Color4.Red;
            configuration.Colours.Remove(BmsSkinConfigurationLookups.NoteColourCyan);
            configuration.Colours.Clear();
            configuration.Colours[BmsSkinConfigurationLookups.NoteColourScratch] = Color4.Blue;

            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            configuration.Colours[BmsSkinConfigurationLookups.NoteColourWhite] = Color4.Green;
            configuration.Colours[BmsSkinConfigurationLookups.PlayfieldBackdropColour] = Color4.White;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 4)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).Value,
                    Is.EqualTo(new Color4(5, 6, 7, 8)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourYellow).Value,
                    Is.EqualTo(new Color4(9, 10, 11, 12)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourScratch).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldBackdropColour).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryCannotForgeAcceptedProvenance()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);

            foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketColourFieldCatalog.All)
                configuration.Colours[field] = Color4.White;

            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(new[] { configuration }, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketColourFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [TestCase(BmsKeymode.Key5K, "5K")]
        [TestCase(BmsKeymode.Key7K, "7K")]
        [TestCase(BmsKeymode.Key9K_Bms, "9K_BMS")]
        [TestCase(BmsKeymode.Key9K_Pms, "9K_PMS")]
        [TestCase(BmsKeymode.Key14K, "14K")]
        public void TestEverySupportedKeymodeRetainsNativeBucketIdentity(BmsKeymode keymode, string sourceToken)
        {
            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshotFactory.Create(
                    decode($"[Bms]\nKeymode: {sourceToken}\nNoteColourWhite: 1,2,3\n").Configurations,
                    keymode).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceKeymode, Is.EqualTo(keymode));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).IsDeclared, Is.True);
            });
        }

        [Test]
        public void TestFactoryAndSnapshotRejectInvalidInputs()
        {
            var duplicate = new[]
            {
                new BmsSkinConfiguration(BmsKeymode.Key7K),
                new BmsSkinConfiguration(BmsKeymode.Key7K),
            };
            BmsSkinConfiguration[] containsNull = { null! };
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot("[Bms]\nKeymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinBucketColourSnapshotFactory.Create(null!, BmsKeymode.Key7K), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshotFactory.Create(containsNull, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshotFactory.Create(duplicate, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshotFactory.Create(
                    Array.Empty<BmsSkinConfiguration>(), (BmsKeymode)99), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => snapshot.GetDeclaration((BmsSkinConfigurationLookups)999), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestDirectSnapshotCreationDefensivelyCopiesAndRejectsInvalidEntries()
        {
            var entries = new List<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>>
            {
                new(BmsSkinConfigurationLookups.NoteColourWhite,
                    GameplaySkinConfigurationDeclaration<Color4>.Declared(new Color4(1, 2, 3, 4))),
            };
            BmsGameplaySkinBucketColourSnapshot snapshot =
                BmsGameplaySkinBucketColourSnapshot.Create(BmsKeymode.Key7K, entries);

            entries.Clear();
            entries.Add(new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(
                BmsSkinConfigurationLookups.NoteColourCyan,
                GameplaySkinConfigurationDeclaration<Color4>.Declared(Color4.Red)));

            var duplicate = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(
                    BmsSkinConfigurationLookups.NoteColourWhite,
                    GameplaySkinConfigurationDeclaration<Color4>.Declared(Color4.White)),
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(
                    BmsSkinConfigurationLookups.NoteColourWhite,
                    GameplaySkinConfigurationDeclaration<Color4>.Declared(Color4.Black)),
            };
            var absent = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(
                    BmsSkinConfigurationLookups.NoteColourWhite,
                    GameplaySkinConfigurationDeclaration<Color4>.Absent),
            };
            var nonColour = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(
                    BmsSkinConfigurationLookups.PlayfieldWidth,
                    GameplaySkinConfigurationDeclaration<Color4>.Declared(Color4.White)),
            };

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 4)));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourCyan).IsDeclared, Is.False);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshot.Create(BmsKeymode.Key7K, null!), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshot.Create((BmsKeymode)99, Array.Empty<KeyValuePair<BmsSkinConfigurationLookups,
                    GameplaySkinConfigurationDeclaration<Color4>>>()), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => BmsGameplaySkinBucketColourSnapshot.Create(BmsKeymode.Key7K, duplicate), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshot.Create(BmsKeymode.Key7K, absent), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketColourSnapshot.Create(BmsKeymode.Key7K, nonColour), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarAndCatalogRejectNonColourFieldsAndNonExactSourceKeys()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptColour(BmsSkinConfigurationLookups.PlayfieldWidth, Color4.White),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.GetAcceptedColour(BmsSkinConfigurationLookups.PlayfieldWidth),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => BmsGameplaySkinBucketColourFieldCatalog.Validate((BmsSkinConfigurationLookups)999, "field"),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.TryGetExact(
                    "NoteColourWhite", out BmsSkinConfigurationLookups exact), Is.True);
                Assert.That(exact, Is.EqualTo(BmsSkinConfigurationLookups.NoteColourWhite));
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.TryGetExact(
                    "notecolourwhite", out _), Is.False);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.TryGetExact(
                    "NoteColourWhite, NoteColourCyan", out _), Is.False);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.TryGetExact(
                    nameof(BmsSkinConfigurationLookups.PlayfieldWidth), out _), Is.False);
                Assert.That(() => BmsGameplaySkinBucketColourFieldCatalog.TryGetExact(null!, out _), Throws.ArgumentNullException);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.IsCanonical(BmsSkinConfigurationLookups.NoteColourWhite), Is.True);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.IsCanonical(BmsSkinConfigurationLookups.PlayfieldWidth), Is.False);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.IsCanonical((BmsSkinConfigurationLookups)999), Is.False);
            });
        }

        [Test]
        public void TestClosedInternalImmutableSurfaceAndSafeString()
        {
            BmsGameplaySkinBucketColourSnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldBackdropColour: 123,45,67,89\n");
            Type snapshotType = typeof(BmsGameplaySkinBucketColourSnapshot);
            Type catalogType = typeof(BmsGameplaySkinBucketColourFieldCatalog);
            Type factoryType = typeof(BmsGameplaySkinBucketColourSnapshotFactory);

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsNotPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.Name),
                    Is.EquivalentTo(new[] { nameof(BmsGameplaySkinBucketColourSnapshot.SourceKeymode) }));
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(catalogType.IsNotPublic && catalogType.IsAbstract && catalogType.IsSealed, Is.True);
                Assert.That(factoryType.IsNotPublic && factoryType.IsAbstract && factoryType.IsSealed, Is.True);
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.All.Distinct().Count(), Is.EqualTo(22));
                Assert.That(BmsGameplaySkinBucketColourFieldCatalog.All, Is.InstanceOf<IList>());
                Assert.That(() => ((IList)BmsGameplaySkinBucketColourFieldCatalog.All).Add(
                    BmsSkinConfigurationLookups.PlayfieldWidth), Throws.TypeOf<NotSupportedException>());
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(BmsGameplaySkinBucketColourSnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("123").And.Not.Contain("PlayfieldBackdropColour"));
            });
        }

        private static BmsGameplaySkinBucketColourSnapshot createSnapshot(string skinIni)
            => BmsGameplaySkinBucketColourSnapshotFactory.Create(decode(skinIni).Configurations, BmsKeymode.Key7K).Value;

        private static BmsSkinDecoder decode(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder;
        }
    }
}
