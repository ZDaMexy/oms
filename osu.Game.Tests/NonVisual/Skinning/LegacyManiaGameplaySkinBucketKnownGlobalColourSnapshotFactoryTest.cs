// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactoryTest
    {
        [Test]
        public void TestMissingBucketIsAbsent()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "ColourBarline: 1,2,3\n");

            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot> declaration =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestEmptyBucketDeclaresSnapshotWithAllColoursAbsent()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceColumnCount, Is.EqualTo(4));
                assertAllColoursAbsent(snapshot);
            });
        }

        [Test]
        public void TestExactKnownGlobalRgbAndRgbaValuesRemainDeclared()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourColumnLine: 1, 2, 3\n" +
                "ColourJudgementLine: 4,5,6,7\n" +
                "ColourBreak: 0,0,0,0\n" +
                "ColourBarline: 255,254,253,252\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ColumnLineColour.Value, Is.EqualTo(new Color4(1, 2, 3, 255)));
                Assert.That(snapshot.JudgementLineColour.Value, Is.EqualTo(new Color4(4, 5, 6, 7)));
                Assert.That(snapshot.ComboBreakColour.Value, Is.EqualTo(new Color4(0, 0, 0, 0)));
                Assert.That(snapshot.BarLineColour.Value, Is.EqualTo(new Color4(255, 254, 253, 252)));
            });
        }

        [Test]
        public void TestKnownColourBeforeKeysIsAttributedToBucket()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "ColourBarline: 1,2,3,4\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.BarLineColour.IsDeclared, Is.True);
                Assert.That(snapshot.BarLineColour.Value, Is.EqualTo(new Color4(1, 2, 3, 4)));
            });
        }

        [Test]
        public void TestDuplicateKnownColourUsesLastAcceptedValue()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourBreak: 1,2,3\n" +
                "ColourBreak: 4,5,6,7\n");

            Assert.That(snapshot.ComboBreakColour.Value, Is.EqualTo(new Color4(4, 5, 6, 7)));
        }

        [Test]
        public void TestMalformedKnownColourDoesNotDeclareOrOverwrite()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot onlyMalformed = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourBarline: 1,2\n");
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot malformedDuplicate = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourBarline: 1,2,3,4\n" +
                "ColourBarline: 256,2,3,4\n");

            Assert.Multiple(() =>
            {
                Assert.That(onlyMalformed.BarLineColour.IsDeclared, Is.False);
                Assert.That(malformedDuplicate.BarLineColour.Value, Is.EqualTo(new Color4(1, 2, 3, 4)));
            });
        }

        [Test]
        public void TestOnlyExactKnownGlobalKeysEnterClosedSnapshot()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourBarLine: 1,2,3\n" +
                "colourBarline: 4,5,6\n" +
                "Colour1: 7,8,9\n" +
                "ColourLight1: 10,11,12\n" +
                "ColourPrivateToken: 13,14,15\n");

            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                assertAllColoursAbsent(snapshot);
                Assert.That(configurations[0].CustomColours.Keys, Is.EquivalentTo(new[]
                {
                    "ColourBarLine",
                    "Colour1",
                    "ColourLight1",
                    "ColourPrivateToken",
                }));
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourColumnLine: 1,2,3\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "ColourBarline: 4,5,6\n");

            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot four =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 4).Value;
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot seven =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 7).Value;

            Assert.Multiple(() =>
            {
                Assert.That(four.ColumnLineColour.IsDeclared, Is.True);
                Assert.That(four.BarLineColour.IsDeclared, Is.False);
                Assert.That(seven.ColumnLineColour.IsDeclared, Is.False);
                Assert.That(seven.BarLineColour.IsDeclared, Is.True);
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourColumnLine: 1,2,3\n" +
                "Keys: 4\n" +
                "ColourBarline: 4,5,6\n");

            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.ColumnLineColour.IsDeclared, Is.True);
                Assert.That(snapshot.BarLineColour.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryMutationCannotForgeDecoderDeclaration()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);
            configuration.CustomColours["ColourColumnLine"] = Color4.Red;
            configuration.CustomColours["ColourJudgementLine"] = Color4.Green;
            configuration.CustomColours["ColourBreak"] = Color4.Blue;
            configuration.CustomColours["ColourBarline"] = Color4.White;

            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(new[] { configuration }, 4).Value;

            assertAllColoursAbsent(snapshot);
        }

        [Test]
        public void TestAcceptedValuesAreDetachedFromLaterCompatibilityDictionaryMutation()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourColumnLine: 1,2,3,4\n" +
                "ColourBarline: 5,6,7,8\n");

            configurations[0].CustomColours["ColourColumnLine"] = Color4.Red;
            configurations[0].CustomColours.Clear();

            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(configurations, 4).Value;

            configurations[0].CustomColours["ColourBarline"] = Color4.Blue;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ColumnLineColour.Value, Is.EqualTo(new Color4(1, 2, 3, 4)));
                Assert.That(snapshot.BarLineColour.Value, Is.EqualTo(new Color4(5, 6, 7, 8)));
            });
        }

        [Test]
        public void TestRejectsInvalidDecoderOutputOrColumnCount()
        {
            var duplicate = new[]
            {
                new LegacyManiaSkinConfiguration(4),
                new LegacyManiaSkinConfiguration(4),
            };
            LegacyManiaSkinConfiguration[] containsNull = { null! };

            Assert.Multiple(() =>
            {
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(duplicate, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarRejectsUnknownOrCompositeKnownGlobalColourField()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptKnownGlobalColour(0, Color4.White), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptKnownGlobalColour(
                    LegacyManiaSkinKnownGlobalColourField.ColumnLine | LegacyManiaSkinKnownGlobalColourField.BarLine,
                    Color4.White), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestPublicSurfaceRemainsClosedSourceSpecificAndImmutable()
        {
            Type snapshotType = typeof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot);
            string[] propertyNames = snapshotType.GetProperties().Select(property => property.Name).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(propertyNames, Is.EquivalentTo(new[]
                {
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.SourceColumnCount),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.ColumnLineColour),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.JudgementLineColour),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.ComboBreakColour),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.BarLineColour),
                }));
                Assert.That(snapshotType.GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(Dictionary<string, Color4>)));
                Assert.That(snapshotType.GetMethods().Select(method => method.GetParameters()).SelectMany(parameters => parameters),
                    Has.None.Property(nameof(System.Reflection.ParameterInfo.ParameterType)).EqualTo(typeof(string)));
                Assert.That(typeof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory).IsSealed, Is.True);
            });
        }

        [Test]
        public void TestSafeStringDoesNotExposeColourValuesOrSourceKeys()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColourBarline: 123,45,67,89\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("123"));
                Assert.That(snapshot.ToString(), Does.Not.Contain("ColourBarline"));
            });
        }

        private static LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot createSnapshot(string skinIni)
            => LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory.Create(decode(skinIni), 4).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static void assertAllColoursAbsent(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot snapshot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ColumnLineColour.IsDeclared, Is.False);
                Assert.That(snapshot.JudgementLineColour.IsDeclared, Is.False);
                Assert.That(snapshot.ComboBreakColour.IsDeclared, Is.False);
                Assert.That(snapshot.BarLineColour.IsDeclared, Is.False);
            });
        }
    }
}
