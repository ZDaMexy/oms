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

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class LegacyManiaGameplaySkinBucketArraySnapshotFactoryTest
    {
        [Test]
        public void TestMissingBucketIsAbsent()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "ColumnWidth: 1,2,3,4\n");

            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketArraySnapshot> declaration =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestEmptyBucketHasExpectedLengthsAndNoDeclarations()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceColumnCount, Is.EqualTo(4));
                Assert.That(snapshot.ColumnLineWidth, Has.Count.EqualTo(5));
                Assert.That(snapshot.ColumnSpacing, Has.Count.EqualTo(3));
                Assert.That(snapshot.ColumnWidth, Has.Count.EqualTo(4));
                Assert.That(snapshot.ExplosionWidth, Has.Count.EqualTo(4));
                Assert.That(snapshot.HoldNoteLightWidth, Has.Count.EqualTo(4));
                assertAllAbsent(snapshot);
            });
        }

        [Test]
        public void TestPreservesExistingLegacyScalingPerField()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnLineWidth: 1,2,3,4,5\n" +
                "ColumnSpacing: 1,2,3\n" +
                "ColumnWidth: 0,20,30,40\n" +
                "LightingNWidth: 1,2,3,4\n" +
                "LightingLWidth: -1,0,5,10\n");

            Assert.Multiple(() =>
            {
                assertValues(snapshot.ColumnLineWidth, 1, 2, 3, 4, 5);
                assertValues(snapshot.ColumnSpacing, 1.6f, 3.2f, 4.8f);
                assertValues(snapshot.ColumnWidth, 0, 32, 48, 64);
                assertValues(snapshot.ExplosionWidth, 1.6f, 3.2f, 4.8f, 6.4f);
                assertValues(snapshot.HoldNoteLightWidth, -1.6f, 0, 8, 16);
            });
        }

        [Test]
        public void TestShortAndOverlongArraysPreservePerIndexPresence()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnLineWidth: 1,2\n" +
                "ColumnSpacing: 1,2,3,4,5\n" +
                "ColumnWidth: 10,20\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ColumnLineWidth.Take(2).All(value => value.IsDeclared), Is.True);
                Assert.That(snapshot.ColumnLineWidth.Skip(2).All(value => !value.IsDeclared), Is.True);
                assertValues(snapshot.ColumnSpacing, 1.6f, 3.2f, 4.8f);
                Assert.That(snapshot.ColumnWidth.Take(2).All(value => value.IsDeclared), Is.True);
                Assert.That(snapshot.ColumnWidth.Skip(2).All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.ExplosionWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.HoldNoteLightWidth.All(value => !value.IsDeclared), Is.True);
            });
        }

        [Test]
        public void TestEmptyMalformedWhitespaceAndTrailingItemsAreAccepted()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnSpacing: 1, 2 ,3\n" +
                "ColumnWidth: 1,,invalid,\n");

            Assert.Multiple(() =>
            {
                assertValues(snapshot.ColumnSpacing, 1.6f, 3.2f, 4.8f);
                assertValues(snapshot.ColumnWidth, 1.6f, 0, 0, 0);
            });
        }

        [Test]
        public void TestDuplicatePartialArrayOverwritesOnlyProvidedPrefix()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n" +
                "ColumnWidth: 5,6\n");

            assertValues(snapshot.ColumnWidth, 8, 9.6f, 4.8f, 6.4f);
        }

        [Test]
        public void TestArrayBeforeKeysIsAttributedToBucket()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "ColumnWidth: 1,2,3,4\n" +
                "Keys: 4\n");

            assertValues(snapshot.ColumnWidth, 1.6f, 3.2f, 4.8f, 6.4f);
        }

        [Test]
        public void TestSingleColumnBucketCardinality()
        {
            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketArraySnapshot> declaration =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(
                    decode(
                        "[Mania]\n" +
                        "Keys: 1\n" +
                        "ColumnLineWidth: 1,2\n" +
                        "ColumnSpacing: 9\n" +
                        "ColumnWidth: 3\n" +
                        "LightingNWidth: 4\n" +
                        "LightingLWidth: 5\n"),
                    1);
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = declaration.Value;

            Assert.Multiple(() =>
            {
                assertValues(snapshot.ColumnLineWidth, 1, 2);
                Assert.That(snapshot.ColumnSpacing, Is.Empty);
                assertValues(snapshot.ColumnWidth, 4.8f);
                assertValues(snapshot.ExplosionWidth, 6.4f);
                assertValues(snapshot.HoldNoteLightWidth, 8);
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "ColumnLineWidth: 1,2,3,4,5,6,7,8\n");

            LegacyManiaGameplaySkinBucketArraySnapshot four =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 4).Value;
            LegacyManiaGameplaySkinBucketArraySnapshot seven =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 7).Value;

            Assert.Multiple(() =>
            {
                Assert.That(four.ColumnWidth.All(value => value.IsDeclared), Is.True);
                Assert.That(four.ColumnLineWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(seven.ColumnWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(seven.ColumnLineWidth.All(value => value.IsDeclared), Is.True);
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n" +
                "Keys: 4\n" +
                "LightingNWidth: 5,6,7,8\n");

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.ColumnWidth.All(value => value.IsDeclared), Is.True);
                Assert.That(snapshot.ExplosionWidth.All(value => !value.IsDeclared), Is.True);
            });
        }

        [Test]
        public void TestAcceptedValuesAreDetachedFromLaterNativeMutation()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n");

            configurations[0].ColumnWidth.AsSpan().Fill(-1);

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 4).Value;

            configurations[0].ColumnWidth.AsSpan().Fill(-2);

            assertValues(snapshot.ColumnWidth, 1.6f, 3.2f, 4.8f, 6.4f);
        }

        [Test]
        public void TestAcceptedDeclarationAccessorReturnsDefensiveCopy()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n");
            GameplaySkinConfigurationDeclaration<float>[] copiedDeclarations =
                configurations[0].CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.ColumnWidth);

            copiedDeclarations[0] = GameplaySkinConfigurationDeclaration<float>.Declared(99);

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(configurations, 4).Value;

            assertValues(snapshot.ColumnWidth, 1.6f, 3.2f, 4.8f, 6.4f);
        }

        [Test]
        public void TestManualNativeMutationCannotForgeDecoderDeclarations()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);
            configuration.ColumnLineWidth.AsSpan().Fill(32);
            configuration.ColumnSpacing.AsSpan().Fill(32);
            configuration.ColumnWidth.AsSpan().Fill(32);
            configuration.ExplosionWidth.AsSpan().Fill(32);
            configuration.HoldNoteLightWidth.AsSpan().Fill(32);

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(new[] { configuration }, 4).Value;

            assertAllAbsent(snapshot);
        }

        [Test]
        public void TestNonFiniteValuesRemainDeclaredButUnvalidated()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: NaN,Infinity,-Infinity,1\n");

            Assert.Multiple(() =>
            {
                Assert.That(float.IsNaN(snapshot.ColumnWidth[0].Value), Is.True);
                Assert.That(float.IsPositiveInfinity(snapshot.ColumnWidth[1].Value), Is.True);
                Assert.That(float.IsNegativeInfinity(snapshot.ColumnWidth[2].Value), Is.True);
                Assert.That(snapshot.ColumnWidth.All(value => value.IsDeclared), Is.True);
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
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(duplicate, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarRejectsUnknownCompositeOrOutOfRangeWithoutWriting()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptArrayValue(0, 0, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptArrayValue(
                    LegacyManiaSkinArrayField.ColumnLineWidth | LegacyManiaSkinArrayField.ColumnSpacing, 0, 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptArrayValue(LegacyManiaSkinArrayField.ColumnWidth, -1, 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptArrayValue(LegacyManiaSkinArrayField.ColumnWidth, 4, 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(configuration.ColumnWidth, Is.All.EqualTo(LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE));
            });

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot =
                LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(new[] { configuration }, 4).Value;
            assertAllAbsent(snapshot);
        }

        [Test]
        public void TestSnapshotCreateDefensivelyCopiesInputArrays()
        {
            GameplaySkinConfigurationDeclaration<float>[] lineWidth = createAbsentDeclarations(5);
            GameplaySkinConfigurationDeclaration<float>[] spacing = createAbsentDeclarations(3);
            GameplaySkinConfigurationDeclaration<float>[] width = createAbsentDeclarations(4);
            GameplaySkinConfigurationDeclaration<float>[] explosion = createAbsentDeclarations(4);
            GameplaySkinConfigurationDeclaration<float>[] holdLight = createAbsentDeclarations(4);
            width[0] = GameplaySkinConfigurationDeclaration<float>.Declared(1);

            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = LegacyManiaGameplaySkinBucketArraySnapshot.Create(
                4, lineWidth, spacing, width, explosion, holdLight);

            width[0] = GameplaySkinConfigurationDeclaration<float>.Declared(2);

            Assert.That(snapshot.ColumnWidth[0].Value, Is.EqualTo(1));
        }

        [Test]
        public void TestSnapshotCreateRejectsWrongCardinality()
        {
            GameplaySkinConfigurationDeclaration<float>[] lineWidth = createAbsentDeclarations(5);
            GameplaySkinConfigurationDeclaration<float>[] spacing = createAbsentDeclarations(3);
            GameplaySkinConfigurationDeclaration<float>[] width = createAbsentDeclarations(4);
            GameplaySkinConfigurationDeclaration<float>[] explosion = createAbsentDeclarations(4);
            GameplaySkinConfigurationDeclaration<float>[] holdLight = createAbsentDeclarations(4);

            Assert.Multiple(() =>
            {
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshot.Create(
                    4, lineWidth.Take(4), spacing, width, explosion, holdLight), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketArraySnapshot.Create(
                    int.MaxValue,
                    Array.Empty<GameplaySkinConfigurationDeclaration<float>>(),
                    Array.Empty<GameplaySkinConfigurationDeclaration<float>>(),
                    Array.Empty<GameplaySkinConfigurationDeclaration<float>>(),
                    Array.Empty<GameplaySkinConfigurationDeclaration<float>>(),
                    Array.Empty<GameplaySkinConfigurationDeclaration<float>>()), Throws.TypeOf<OverflowException>());
            });
        }

        [Test]
        public void TestPublicSurfaceIsSourceSpecificAndImmutable()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 1,2,3,4\n");
            Type snapshotType = typeof(LegacyManiaGameplaySkinBucketArraySnapshot);
            var mutableView = (IList<GameplaySkinConfigurationDeclaration<float>>)snapshot.ColumnWidth;

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(snapshotType.GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(LegacyManiaSkinConfiguration)));
                Assert.That(snapshot.ColumnWidth, Is.Not.InstanceOf<Array>());
                Assert.That(() => mutableView[0] = GameplaySkinConfigurationDeclaration<float>.Declared(99),
                    Throws.TypeOf<NotSupportedException>());
                Assert.That(typeof(LegacyManiaGameplaySkinBucketArraySnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinBucketArraySnapshotFactory).IsSealed, Is.True);
            });
        }

        [Test]
        public void TestSafeStringDoesNotExposeArrayValues()
        {
            LegacyManiaGameplaySkinBucketArraySnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ColumnWidth: 123.456,2,3,4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(LegacyManiaGameplaySkinBucketArraySnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("123.456"));
            });
        }

        private static LegacyManiaGameplaySkinBucketArraySnapshot createSnapshot(string skinIni)
            => LegacyManiaGameplaySkinBucketArraySnapshotFactory.Create(decode(skinIni), 4).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static GameplaySkinConfigurationDeclaration<float>[] createAbsentDeclarations(int count)
            => new GameplaySkinConfigurationDeclaration<float>[count];

        private static void assertAllAbsent(LegacyManiaGameplaySkinBucketArraySnapshot snapshot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ColumnLineWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.ColumnSpacing.All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.ColumnWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.ExplosionWidth.All(value => !value.IsDeclared), Is.True);
                Assert.That(snapshot.HoldNoteLightWidth.All(value => !value.IsDeclared), Is.True);
            });
        }

        private static void assertValues(
            IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> actual,
            params float[] expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Length));

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].IsDeclared, Is.True, $"Expected source index {i} to be declared.");
                Assert.That(actual[i].Value, Is.EqualTo(expected[i]).Within(0.0001f), $"Unexpected value at source index {i}.");
            }
        }
    }
}
