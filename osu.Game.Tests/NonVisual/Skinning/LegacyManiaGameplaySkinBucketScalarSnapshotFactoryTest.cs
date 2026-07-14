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
    public sealed class LegacyManiaGameplaySkinBucketScalarSnapshotFactoryTest
    {
        [Test]
        public void TestMissingBucketIsAbsent()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "HitPosition: 400\n");

            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketScalarSnapshot> declaration =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestEmptyBucketDeclaresSnapshotWithAllScalarsAbsent()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceColumnCount, Is.EqualTo(4));
                assertAllScalarsAbsent(snapshot);
            });
        }

        [Test]
        public void TestExplicitDefaultValuesRemainDeclared()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "WidthForNoteHeightScale: 0\n" +
                "HitPosition: 402\n" +
                "LightPosition: 413\n" +
                "ComboPosition: 111\n" +
                "ScorePosition: 300\n" +
                "BarlineHeight: 1\n" +
                "JudgementLine: 1\n" +
                "KeysUnderNotes: 0\n" +
                "LightFramePerSecond: 60\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.WidthForNoteHeightScale.Value, Is.Zero);
                Assert.That(snapshot.HitPosition.Value, Is.EqualTo(LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION));
                Assert.That(snapshot.LightPosition.Value, Is.EqualTo((480 - 413) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR));
                Assert.That(snapshot.ComboPosition.Value, Is.EqualTo(111 * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR));
                Assert.That(snapshot.ScorePosition.Value, Is.EqualTo(300 * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR));
                Assert.That(snapshot.BarLineHeight.Value, Is.EqualTo(1));
                Assert.That(snapshot.ShowJudgementLine.Value, Is.True);
                Assert.That(snapshot.KeysUnderNotes.Value, Is.False);
                Assert.That(snapshot.LightFramePerSecond.Value, Is.EqualTo(60));
            });
        }

        [Test]
        public void TestPreservesExistingLegacyConversionAndBooleanRules()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "WidthForNoteHeightScale: 2\n" +
                "HitPosition: 200\n" +
                "LightPosition: 500\n" +
                "ComboPosition: 2\n" +
                "ScorePosition: 3\n" +
                "BarlineHeight: 2.5\n" +
                "JudgementLine: true\n" +
                "KeysUnderNotes: 1\n" +
                "LightFramePerSecond: 0\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.WidthForNoteHeightScale.Value, Is.EqualTo(3.2f).Within(0.0001f));
                Assert.That(snapshot.HitPosition.Value, Is.EqualTo(384f).Within(0.0001f));
                Assert.That(snapshot.LightPosition.Value, Is.EqualTo(-32f).Within(0.0001f));
                Assert.That(snapshot.ComboPosition.Value, Is.EqualTo(3.2f).Within(0.0001f));
                Assert.That(snapshot.ScorePosition.Value, Is.EqualTo(4.8f).Within(0.0001f));
                Assert.That(snapshot.BarLineHeight.Value, Is.EqualTo(2.5f));
                Assert.That(snapshot.ShowJudgementLine.Value, Is.False);
                Assert.That(snapshot.KeysUnderNotes.Value, Is.True);
                Assert.That(snapshot.LightFramePerSecond.Value, Is.EqualTo(24));
            });
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TestNonPositiveLightFramesPerSecondRemainsDeclaredAfterNormalisation(int sourceValue)
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                $"LightFramePerSecond: {sourceValue}\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.LightFramePerSecond.IsDeclared, Is.True);
                Assert.That(snapshot.LightFramePerSecond.Value, Is.EqualTo(24));
            });
        }

        [Test]
        public void TestScalarBeforeKeysIsAttributedToBucket()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "ScorePosition: 100\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ScorePosition.IsDeclared, Is.True);
                Assert.That(snapshot.ScorePosition.Value, Is.EqualTo(160f));
            });
        }

        [Test]
        public void TestMalformedNumericDoesNotBecomeDeclaredFromNativeDefault()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot malformedFloat = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "HitPosition: invalid\n");
            LegacyManiaGameplaySkinBucketScalarSnapshot malformedInteger = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "LightFramePerSecond: invalid\n");

            Assert.Multiple(() =>
            {
                Assert.That(malformedFloat.HitPosition.IsDeclared, Is.False);
                Assert.That(malformedInteger.LightFramePerSecond.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestNonFiniteValuesRemainDeclaredButUnvalidated()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "WidthForNoteHeightScale: NaN\n" +
                "LightPosition: Infinity\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.WidthForNoteHeightScale.IsDeclared, Is.True);
                Assert.That(float.IsNaN(snapshot.WidthForNoteHeightScale.Value), Is.True);
                Assert.That(snapshot.LightPosition.IsDeclared, Is.True);
                Assert.That(float.IsNegativeInfinity(snapshot.LightPosition.Value), Is.True);
            });
        }

        [Test]
        public void TestDuplicateScalarUsesLastAcceptedValue()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ComboPosition: 10\n" +
                "ComboPosition: 20\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ComboPosition.IsDeclared, Is.True);
                Assert.That(snapshot.ComboPosition.Value, Is.EqualTo(32f));
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "HitPosition: 400\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "ScorePosition: 200\n");

            LegacyManiaGameplaySkinBucketScalarSnapshot four =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(configurations, 4).Value;
            LegacyManiaGameplaySkinBucketScalarSnapshot seven =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(configurations, 7).Value;

            Assert.Multiple(() =>
            {
                Assert.That(four.HitPosition.IsDeclared, Is.True);
                Assert.That(four.ScorePosition.IsDeclared, Is.False);
                Assert.That(seven.HitPosition.IsDeclared, Is.False);
                Assert.That(seven.ScorePosition.IsDeclared, Is.True);
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "HitPosition: 400\n" +
                "Keys: 4\n" +
                "ScorePosition: 200\n");

            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.HitPosition.IsDeclared, Is.True);
                Assert.That(snapshot.ScorePosition.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestManualNativeMutationCannotForgeDecoderDeclaration()
        {
            var configuration = new LegacyManiaSkinConfiguration(4)
            {
                WidthForNoteHeightScale = 32,
                HitPosition = 32,
                LightPosition = 32,
                ComboPosition = 32,
                ScorePosition = 32,
                BarLineHeight = 32,
                ShowJudgementLine = false,
                KeysUnderNotes = true,
                LightFramePerSecond = 32,
            };

            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(new[] { configuration }, 4).Value;

            assertAllScalarsAbsent(snapshot);
        }

        [Test]
        public void TestAcceptedValuesAreDetachedFromLaterNativeMutation()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "HitPosition: 400\n" +
                "LightFramePerSecond: 60\n");

            configurations[0].HitPosition = -1;
            configurations[0].LightFramePerSecond = -1;

            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot =
                LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(configurations, 4).Value;

            configurations[0].HitPosition = -2;
            configurations[0].LightFramePerSecond = -2;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.HitPosition.Value, Is.EqualTo(128f).Within(0.0001f));
                Assert.That(snapshot.LightFramePerSecond.Value, Is.EqualTo(60));
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
                Assert.That(() => LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(duplicate, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarRejectsUnknownOrCompositeScalarField()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.MarkScalarDeclared(0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.MarkScalarDeclared(
                    LegacyManiaSkinScalarField.WidthForNoteHeightScale | LegacyManiaSkinScalarField.HitPosition),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestPublicSurfaceRemainsSourceSpecificAndImmutable()
        {
            Type snapshotType = typeof(LegacyManiaGameplaySkinBucketScalarSnapshot);
            string[] forbiddenProperties =
            {
                "ColumnLineWidth",
                "ColumnSpacing",
                "ColumnWidth",
                "ExplosionWidth",
                "HoldNoteLightWidth",
                "CustomColours",
                "ImageLookups",
                "NoteBodyStyle",
                "Topology",
                "Geometry",
            };

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(snapshotType.GetProperties().Select(property => property.Name).Intersect(forbiddenProperties), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(LegacyManiaSkinConfiguration)));
                Assert.That(typeof(LegacyManiaGameplaySkinBucketScalarSnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinBucketScalarSnapshotFactory).IsSealed, Is.True);
            });
        }

        [Test]
        public void TestSafeStringDoesNotExposeScalarValues()
        {
            LegacyManiaGameplaySkinBucketScalarSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "ComboPosition: 123.456\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(LegacyManiaGameplaySkinBucketScalarSnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("123.456"));
            });
        }

        private static LegacyManiaGameplaySkinBucketScalarSnapshot createSnapshot(string skinIni)
            => LegacyManiaGameplaySkinBucketScalarSnapshotFactory.Create(decode(skinIni), 4).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static void assertAllScalarsAbsent(LegacyManiaGameplaySkinBucketScalarSnapshot snapshot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.WidthForNoteHeightScale.IsDeclared, Is.False);
                Assert.That(snapshot.HitPosition.IsDeclared, Is.False);
                Assert.That(snapshot.LightPosition.IsDeclared, Is.False);
                Assert.That(snapshot.ComboPosition.IsDeclared, Is.False);
                Assert.That(snapshot.ScorePosition.IsDeclared, Is.False);
                Assert.That(snapshot.BarLineHeight.IsDeclared, Is.False);
                Assert.That(snapshot.ShowJudgementLine.IsDeclared, Is.False);
                Assert.That(snapshot.KeysUnderNotes.IsDeclared, Is.False);
                Assert.That(snapshot.LightFramePerSecond.IsDeclared, Is.False);
            });
        }
    }
}
