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

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsGameplaySkinBucketGeometrySnapshotFactoryTest
    {
        private static readonly (string Key, string Value, float Expected)[] exact_geometry_cases =
        {
            ("PlayfieldWidth", "0.11", 0.11f),
            ("PlayfieldHeight", "0.22", 0.22f),
            ("NormalLaneWidth", "0.33", 0.33f),
            ("ScratchLaneWidth", "0.44", 0.44f),
            ("NormalLaneSpacing", "0.55", 0.55f),
            ("ScratchLaneSpacing", "0.66", 0.66f),
            ("HitTargetHeight", "7.7", 7.7f),
            ("HitTargetBarHeight", "8.8", 8.8f),
            ("HitTargetLineHeight", "9.9", 9.9f),
            ("HitTargetGlowRadius", "10.1", 10.1f),
            ("BarLineHeight", "11.2", 11.2f),
            ("LongNoteBodyWidth", "0.73", 0.73f),
        };

        [Test]
        public void TestMissingBucketIsAbsent()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "PlayfieldWidth: 0.5\n");

            var declaration = BmsGameplaySkinBucketGeometrySnapshotFactory.Create(
                decoder.Configurations, BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestExplicitEmptyBucketDeclaresAllGeometryAbsent()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceKeymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.All, Has.Count.EqualTo(12));

                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [Test]
        public void TestAllTwelveExactDeclarationsAreCapturedAndRemainInCompatibilityView()
        {
            var ini = new StringBuilder("[Bms]\nKeymode: 7K\n");

            foreach (var entry in exact_geometry_cases)
                ini.AppendLine($"{entry.Key}: {entry.Value}");

            BmsSkinDecoder decoder = decode(ini.ToString());
            BmsSkinConfiguration configuration = decoder.Configurations.Single();
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(exact_geometry_cases, Has.Length.EqualTo(12));

                foreach (var entry in exact_geometry_cases)
                {
                    var lookup = Enum.Parse<BmsSkinConfigurationLookups>(entry.Key);
                    Assert.That(snapshot.GetDeclaration(lookup).IsDeclared, Is.True, entry.Key);
                    Assert.That(snapshot.GetDeclaration(lookup).Value, Is.EqualTo(entry.Expected), entry.Key);
                    Assert.That(configuration.Geometry[lookup], Is.EqualTo(entry.Expected), $"compatibility:{entry.Key}");
                }
            });
        }

        [TestCase("+1", 1f)]
        [TestCase("-1.25", -1.25f)]
        [TestCase(".5", 0.5f)]
        [TestCase("5.", 5f)]
        [TestCase("001.500", 1.5f)]
        [TestCase("1e2", 100f)]
        [TestCase("-1E-2", -0.01f)]
        public void TestInvariantFloatSyntaxIsPreserved(string value, float expected)
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"PlayfieldWidth: {value}\n");
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(expected));
                Assert.That(decoder.Configurations.Single().Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.EqualTo(expected));
            });
        }

        [Test]
        public void TestNegativeZeroBitsArePreserved()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: -0\n");
            float accepted = BmsGameplaySkinBucketGeometrySnapshotFactory.Create(
                decoder.Configurations, BmsKeymode.Key7K).Value
                .GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value;
            float compatibility = decoder.Configurations.Single().Geometry[BmsSkinConfigurationLookups.PlayfieldWidth];

            Assert.Multiple(() =>
            {
                Assert.That(BitConverter.SingleToInt32Bits(accepted), Is.EqualTo(unchecked((int)0x80000000)));
                Assert.That(BitConverter.SingleToInt32Bits(compatibility), Is.EqualTo(unchecked((int)0x80000000)));
            });
        }

        [TestCase("NaN")]
        [TestCase("nan")]
        [TestCase("NAN")]
        public void TestNaNSpellingsAreDeclared(string value)
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"PlayfieldWidth: {value}\n");

            Assert.That(float.IsNaN(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value), Is.True);
        }

        [TestCase("Infinity", true)]
        [TestCase("infinity", true)]
        [TestCase("+Infinity", true)]
        [TestCase("-Infinity", false)]
        public void TestInfinitySpellingsAreDeclared(string value, bool positive)
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"PlayfieldWidth: {value}\n");
            float accepted = snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value;

            Assert.That(positive ? float.IsPositiveInfinity(accepted) : float.IsNegativeInfinity(accepted), Is.True);
        }

        [Test]
        public void TestOverflowAndUnderflowAcceptedValuesArePreserved()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 1e1000\n" +
                "PlayfieldHeight: -1e1000\n" +
                "NormalLaneWidth: 1e-1000\n" +
                "ScratchLaneWidth: -1e-1000\n");

            Assert.Multiple(() =>
            {
                Assert.That(float.IsPositiveInfinity(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value), Is.True);
                Assert.That(float.IsNegativeInfinity(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value), Is.True);
                Assert.That(BitConverter.SingleToInt32Bits(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NormalLaneWidth).Value), Is.Zero);
                Assert.That(BitConverter.SingleToInt32Bits(snapshot.GetDeclaration(BmsSkinConfigurationLookups.ScratchLaneWidth).Value),
                    Is.EqualTo(unchecked((int)0x80000000)));
            });
        }

        [TestCase("")]
        [TestCase("1,000")]
        [TestCase("0x1")]
        [TestCase("1_000")]
        [TestCase("0.5f")]
        [TestCase("--1")]
        [TestCase("1e")]
        [TestCase("\u221e")]
        [TestCase("\u0661")]
        public void TestMalformedGeometryDoesNotDeclare(string value)
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"PlayfieldWidth: {value}\n");
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).IsDeclared, Is.False);
                Assert.That(decoder.Configurations.Single().Geometry, Does.Not.ContainKey(BmsSkinConfigurationLookups.PlayfieldWidth));
            });
        }

        [Test]
        public void TestValidDuplicateUsesLastAcceptedValue()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 0.25\n" +
                "PlayfieldWidth: 0.75\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.75f));
        }

        [Test]
        public void TestMalformedDuplicateDoesNotEraseLastAcceptedValue()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 0.25\n" +
                "PlayfieldWidth: not-a-number\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.25f));
        }

        [Test]
        public void TestPendingAndRepeatedBucketsMergeIntoExactKeymode()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "PlayfieldWidth: 0.25\n" +
                "Keymode: 7K\n" +
                "[Bms]\n" +
                "Keymode: 14K\n" +
                "PlayfieldWidth: 0.9\n" +
                "[Bms]\n" +
                "PlayfieldHeight: 0.8\n" +
                "Keymode: 7K\n");

            BmsGameplaySkinBucketGeometrySnapshot seven =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;
            BmsGameplaySkinBucketGeometrySnapshot fourteen =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key14K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Has.Count.EqualTo(2));
                Assert.That(seven.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.25f));
                Assert.That(seven.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value, Is.EqualTo(0.8f));
                Assert.That(fourteen.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.9f));
                Assert.That(fourteen.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestRepeatedParseMergesAndMalformedLaterValueDoesNotEraseAcceptedValue()
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nKeymode: 7K\nPlayfieldWidth: 0.25\n");
            decoder.Parse("[Bms]\nKeymode: 7K\nPlayfieldWidth: 0.75\nPlayfieldHeight: 0.8\n");
            decoder.Parse("[Bms]\nKeymode: 7K\nPlayfieldWidth: invalid\n");

            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.75f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value, Is.EqualTo(0.8f));
            });
        }

        [Test]
        public void TestCompositeEnumKeysRemainCompatibilityOnly()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldHeight: 1.5\n" +
                "ScratchLaneWidth: 2.5\n" +
                "PlayfieldWidth, PlayfieldHeight: 9.5\n" +
                "PlayfieldHeight, NormalLaneWidth: 8.5\n" +
                "NormalLaneSpacing, NormalLaneSpacing: 7.5\n");
            BmsSkinConfiguration configuration = decoder.Configurations.Single();
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configuration.Geometry[BmsSkinConfigurationLookups.PlayfieldHeight], Is.EqualTo(9.5f));
                Assert.That(configuration.Geometry[BmsSkinConfigurationLookups.ScratchLaneWidth], Is.EqualTo(8.5f));
                Assert.That(configuration.Geometry[BmsSkinConfigurationLookups.NormalLaneSpacing], Is.EqualTo(7.5f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value, Is.EqualTo(1.5f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.ScratchLaneWidth).Value, Is.EqualTo(2.5f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NormalLaneSpacing).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestDefinedNonGeometryAndUndefinedCompositeAliasesAreIgnored()
        {
            const string defined_non_geometry = "HitTargetBarHeight, HitTargetLineHeight";
            const string undefined = "NormalLaneWidth, PlayfieldBaseplateColour";

            Assert.That(Enum.TryParse(defined_non_geometry, out BmsSkinConfigurationLookups definedNonGeometry), Is.True);
            Assert.That(Enum.IsDefined(definedNonGeometry), Is.True);
            Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.IsCanonical(definedNonGeometry), Is.False);
            Assert.That(Enum.TryParse(undefined, out BmsSkinConfigurationLookups undefinedLookup), Is.True);
            Assert.That(Enum.IsDefined(undefinedLookup), Is.False);

            BmsSkinConfiguration configuration = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                $"{defined_non_geometry}: 3.5\n" +
                $"{undefined}: 4.5\n").Configurations.Single();
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(new[] { configuration }, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configuration.Geometry, Is.Empty);
                Assert.That(configuration.Colours, Is.Empty);
                Assert.That(configuration.ImageLookups, Is.Empty);

                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [Test]
        public void TestNonExactKeysAndOtherSectionsDoNotDeclareGeometry()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 0.1\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "playfieldwidth: 0.2\n" +
                "0: 0.3\n" +
                "PlayfieldWidthSuffix: 0.4\n" +
                "FutureGeometry: 0.5\n" +
                "[General]\n" +
                "PlayfieldWidth: 0.6\n" +
                "[Mania]\n" +
                "PlayfieldHeight: 0.7\n");

            Assert.Multiple(() =>
            {
                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [Test]
        public void TestTrimmedCanonicalKeyDeclaresGeometry()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "  PlayfieldWidth  :  0.5  \n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.5f));
        }

        [Test]
        public void TestInvalidKeymodeClearsEarlierPendingButLaterPendingReplays()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "PlayfieldWidth: 0.25\n" +
                "Keymode: invalid\n" +
                "PlayfieldHeight: 0.75\n" +
                "Keymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value, Is.EqualTo(0.75f));
            });
        }

        [Test]
        public void TestSectionBoundaryDropsUnresolvedPendingGeometry()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "PlayfieldWidth: 0.25\n" +
                "[General]\n" +
                "Keymodes: 7K\n" +
                "[Bms]\n" +
                "Keymode: 7K\n");

            Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).IsDeclared, Is.False);
        }

        [Test]
        public void TestCompatibilityDictionaryCannotForgeEraseOrAlterAcceptedProvenance()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 0.25\n" +
                "PlayfieldHeight: 0.5\n" +
                "NormalLaneWidth: 0.75\n");
            BmsSkinConfiguration configuration = decoder.Configurations.Single();

            configuration.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth] = 9;
            configuration.Geometry.Remove(BmsSkinConfigurationLookups.PlayfieldHeight);
            configuration.Geometry.Clear();
            configuration.Geometry[BmsSkinConfigurationLookups.ScratchLaneWidth] = 8;

            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decoder.Configurations, BmsKeymode.Key7K).Value;

            configuration.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth] = 7;
            configuration.Geometry[BmsSkinConfigurationLookups.BarLineHeight] = 6;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.25f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).Value, Is.EqualTo(0.5f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.NormalLaneWidth).Value, Is.EqualTo(0.75f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.ScratchLaneWidth).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.BarLineHeight).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryCannotForgeAcceptedProvenance()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);

            foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
                configuration.Geometry[field] = 42;

            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(new[] { configuration }, BmsKeymode.Key7K).Value;

            Assert.Multiple(() =>
            {
                foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
                    Assert.That(snapshot.GetDeclaration(field).IsDeclared, Is.False, field.ToString());
            });
        }

        [TestCase(BmsKeymode.Key5K, "5K")]
        [TestCase(BmsKeymode.Key7K, "7K")]
        [TestCase(BmsKeymode.Key9K_Bms, "9K")]
        [TestCase(BmsKeymode.Key9K_Pms, "PMS")]
        [TestCase(BmsKeymode.Key14K, "14K")]
        public void TestEverySupportedKeymodeRetainsNativeBucketIdentity(BmsKeymode keymode, string sourceToken)
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshotFactory.Create(
                    decode($"[Bms]\nKeymode: {sourceToken}\nPlayfieldWidth: 0.5\n").Configurations,
                    keymode).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceKeymode, Is.EqualTo(keymode));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.5f));
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
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot("[Bms]\nKeymode: 7K\n");

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshotFactory.Create(null!, BmsKeymode.Key7K), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshotFactory.Create(containsNull, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshotFactory.Create(duplicate, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshotFactory.Create(
                    Array.Empty<BmsSkinConfiguration>(), (BmsKeymode)99), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => snapshot.GetDeclaration(BmsSkinConfigurationLookups.NoteColourWhite), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => snapshot.GetDeclaration((BmsSkinConfigurationLookups)999), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestDirectSnapshotCreationDefensivelyCopiesAndRejectsInvalidEntries()
        {
            var entries = new List<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>>
            {
                new(BmsSkinConfigurationLookups.PlayfieldWidth, GameplaySkinConfigurationDeclaration<float>.Declared(0.5f)),
            };
            BmsGameplaySkinBucketGeometrySnapshot snapshot =
                BmsGameplaySkinBucketGeometrySnapshot.Create(BmsKeymode.Key7K, entries);

            entries.Clear();
            entries.Add(new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                BmsSkinConfigurationLookups.PlayfieldHeight, GameplaySkinConfigurationDeclaration<float>.Declared(0.75f)));

            var duplicate = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                    BmsSkinConfigurationLookups.PlayfieldWidth, GameplaySkinConfigurationDeclaration<float>.Declared(0.1f)),
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                    BmsSkinConfigurationLookups.PlayfieldWidth, GameplaySkinConfigurationDeclaration<float>.Declared(0.2f)),
            };
            var absent = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                    BmsSkinConfigurationLookups.PlayfieldWidth, GameplaySkinConfigurationDeclaration<float>.Absent),
            };
            var nonGeometry = new[]
            {
                new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                    BmsSkinConfigurationLookups.NoteColourWhite, GameplaySkinConfigurationDeclaration<float>.Declared(1)),
            };
            BmsGameplaySkinBucketGeometrySnapshot nonFinite = BmsGameplaySkinBucketGeometrySnapshot.Create(
                BmsKeymode.Key7K,
                new[]
                {
                    new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(
                        BmsSkinConfigurationLookups.PlayfieldWidth, GameplaySkinConfigurationDeclaration<float>.Declared(float.NaN)),
                });

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value, Is.EqualTo(0.5f));
                Assert.That(snapshot.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldHeight).IsDeclared, Is.False);
                Assert.That(float.IsNaN(nonFinite.GetDeclaration(BmsSkinConfigurationLookups.PlayfieldWidth).Value), Is.True);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshot.Create(BmsKeymode.Key7K, null!), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshot.Create((BmsKeymode)99, Array.Empty<KeyValuePair<BmsSkinConfigurationLookups,
                    GameplaySkinConfigurationDeclaration<float>>>()), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshot.Create(BmsKeymode.Key7K, duplicate), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshot.Create(BmsKeymode.Key7K, absent), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinBucketGeometrySnapshot.Create(BmsKeymode.Key7K, nonGeometry),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarAndCatalogRejectNonGeometryFieldsAndNonExactSourceKeys()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptGeometry(BmsSkinConfigurationLookups.NoteColourWhite, 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.GetAcceptedGeometry(BmsSkinConfigurationLookups.NoteColourWhite),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => BmsGameplaySkinBucketGeometryFieldCatalog.Validate((BmsSkinConfigurationLookups)999, "field"),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact(
                    "PlayfieldWidth", out BmsSkinConfigurationLookups exact), Is.True);
                Assert.That(exact, Is.EqualTo(BmsSkinConfigurationLookups.PlayfieldWidth));
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact("playfieldwidth", out _), Is.False);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact("PlayfieldWidth, PlayfieldHeight", out _), Is.False);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact("0", out _), Is.False);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact(
                    nameof(BmsSkinConfigurationLookups.NoteColourWhite), out _), Is.False);
                Assert.That(() => BmsGameplaySkinBucketGeometryFieldCatalog.TryGetExact(null!, out _), Throws.ArgumentNullException);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.IsCanonical(BmsSkinConfigurationLookups.PlayfieldWidth), Is.True);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.IsCanonical(BmsSkinConfigurationLookups.NoteColourWhite), Is.False);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.IsCanonical((BmsSkinConfigurationLookups)999), Is.False);
            });
        }

        [Test]
        public void TestClosedInternalImmutableSurfaceAndSafeString()
        {
            BmsGameplaySkinBucketGeometrySnapshot snapshot = createSnapshot(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 123.45\n");
            Type snapshotType = typeof(BmsGameplaySkinBucketGeometrySnapshot);
            Type catalogType = typeof(BmsGameplaySkinBucketGeometryFieldCatalog);
            Type factoryType = typeof(BmsGameplaySkinBucketGeometrySnapshotFactory);

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsNotPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.Name),
                    Is.EquivalentTo(new[] { nameof(BmsGameplaySkinBucketGeometrySnapshot.SourceKeymode) }));
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(catalogType.IsNotPublic && catalogType.IsAbstract && catalogType.IsSealed, Is.True);
                Assert.That(factoryType.IsNotPublic && factoryType.IsAbstract && factoryType.IsSealed, Is.True);
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.All.Distinct().Count(), Is.EqualTo(12));
                Assert.That(exact_geometry_cases.Select(entry => Enum.Parse<BmsSkinConfigurationLookups>(entry.Key)),
                    Is.EquivalentTo(BmsGameplaySkinBucketGeometryFieldCatalog.All));
                Assert.That(BmsGameplaySkinBucketGeometryFieldCatalog.All, Is.InstanceOf<IList>());
                Assert.That(() => ((IList)BmsGameplaySkinBucketGeometryFieldCatalog.All).Add(
                    BmsSkinConfigurationLookups.NoteColourWhite), Throws.TypeOf<NotSupportedException>());
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(BmsGameplaySkinBucketGeometrySnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("123").And.Not.Contain("PlayfieldWidth").And.Not.Contain("Key7K"));
            });
        }

        private static BmsGameplaySkinBucketGeometrySnapshot createSnapshot(string skinIni)
            => BmsGameplaySkinBucketGeometrySnapshotFactory.Create(decode(skinIni).Configurations, BmsKeymode.Key7K).Value;

        private static BmsSkinDecoder decode(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder;
        }
    }
}
