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
    public sealed class LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactoryTest
    {
        [Test]
        public void TestMissingBucketIsAbsent()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "NoteBodyStyle: Stretch\n");

            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot> declaration =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [TestCase("1.0")]
        [TestCase("2.5")]
        [TestCase("latest")]
        public void TestExplicitBucketDoesNotDeriveStyleFromGlobalVersion(string version)
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[General]\n" +
                $"Version: {version}\n" +
                "[Mania]\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceColumnCount, Is.EqualTo(4));
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.False);
            });
        }

        [TestCase("Stretch", LegacyNoteBodyStyle.Stretch)]
        [TestCase("RepeatTop", LegacyNoteBodyStyle.RepeatTop)]
        [TestCase("RepeatBottom", LegacyNoteBodyStyle.RepeatBottom)]
        [TestCase("RepeatTopAndBottom", LegacyNoteBodyStyle.RepeatTopAndBottom)]
        public void TestDefinedSymbolicStyleRemainsDeclared(string sourceValue, LegacyNoteBodyStyle expected)
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                $"NoteBodyStyle: {sourceValue}\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.True);
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo(expected));
            });
        }

        [TestCase("1", 1)]
        [TestCase("99", 99)]
        [TestCase("-1", -1)]
        public void TestUnnamedNumericStylePreservesEnumTryParseCompatibility(string sourceValue, int expected)
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                $"NoteBodyStyle: {sourceValue}\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.True);
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo((LegacyNoteBodyStyle)expected));
                Assert.That(Enum.IsDefined(snapshot.NoteBodyStyle.Value), Is.False);
            });
        }

        [TestCase("+2")]
        [TestCase("02")]
        public void TestNonCanonicalDefinedNumericStylePreservesEnumTryParseCompatibility(string sourceValue)
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                $"NoteBodyStyle: {sourceValue}\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.True);
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
                Assert.That(Enum.IsDefined(snapshot.NoteBodyStyle.Value), Is.True);
            });
        }

        [Test]
        public void TestCompositeSymbolicStylePreservesEnumTryParseCompatibility()
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop, RepeatTopAndBottom\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.True);
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo((LegacyNoteBodyStyle)6));
                Assert.That(Enum.IsDefined(snapshot.NoteBodyStyle.Value), Is.False);
            });
        }

        [Test]
        public void TestOnlyExactKeyAndCaseSensitiveParseEnterSnapshot()
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "notebodystyle: Stretch\n" +
                "NoteBodyStyle: stretch\n" +
                "NoteBodyStyle: Repeat\n" +
                "NoteBodyStyle: RepeatTopSuffix\n" +
                "NoteBodyStyle:\n");

            Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.False);
        }

        [Test]
        public void TestStyleBeforeKeysIsAttributedToBucket()
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.True);
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
            });
        }

        [Test]
        public void TestPendingStyleWithoutKeysIsDiscardedAtEofOrSectionBoundary()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> eofConfigurations = decode(
                "[Mania]\n" +
                "NoteBodyStyle: RepeatTop\n");
            IReadOnlyList<LegacyManiaSkinConfiguration> sectionBoundaryConfigurations = decode(
                "[Mania]\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "[General]\n" +
                "Version: 1.0\n" +
                "[Mania]\n" +
                "Keys: 4\n");
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot afterSectionBoundary =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(sectionBoundaryConfigurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(eofConfigurations, Is.Empty);
                Assert.That(sectionBoundaryConfigurations, Has.Count.EqualTo(1));
                Assert.That(afterSectionBoundary.NoteBodyStyle.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestDuplicateStyleUsesLastAcceptedAndMalformedDoesNotOverwrite()
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot lastAccepted = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "NoteBodyStyle: RepeatBottom\n");
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot malformedLast = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTopAndBottom\n" +
                "NoteBodyStyle: invalid\n");

            Assert.Multiple(() =>
            {
                Assert.That(lastAccepted.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatBottom));
                Assert.That(malformedLast.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTopAndBottom));
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "NoteBodyStyle: RepeatBottom\n");

            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot four =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4).Value;
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot seven =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 7).Value;

            Assert.Multiple(() =>
            {
                Assert.That(four.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
                Assert.That(seven.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatBottom));
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatBottom\n");

            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
            });
        }

        [Test]
        public void TestMalformedKeysKeepsPriorCurrentBucketCompatibility()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop\n" +
                "Keys: invalid\n" +
                "NoteBodyStyle: RepeatBottom\n");

            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatBottom));
            });
        }

        [Test]
        public void TestManualPublicFieldMutationCannotForgeDecoderDeclaration()
        {
            var configuration = new LegacyManiaSkinConfiguration(4)
            {
                NoteBodyStyle = LegacyNoteBodyStyle.Stretch,
            };

            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(new[] { configuration }, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configuration.NoteBodyStyle, Is.EqualTo(LegacyNoteBodyStyle.Stretch));
                Assert.That(snapshot.NoteBodyStyle.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestAcceptedStyleIsDetachedFromPublicFieldEraseAndAlter()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTop\n");

            configurations[0].NoteBodyStyle = null;
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot afterErase =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4).Value;

            configurations[0].NoteBodyStyle = LegacyNoteBodyStyle.Stretch;
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot afterAlter =
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(configurations, 4).Value;

            configurations[0].NoteBodyStyle = LegacyNoteBodyStyle.RepeatBottom;

            Assert.Multiple(() =>
            {
                Assert.That(afterErase.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
                Assert.That(afterAlter.NoteBodyStyle.Value, Is.EqualTo(LegacyNoteBodyStyle.RepeatTop));
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
                Assert.That(() => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(duplicate, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestPublicSurfaceRemainsClosedSourceSpecificAndImmutable()
        {
            Type snapshotType = typeof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot);
            string[] propertyNames = snapshotType.GetProperties().Select(property => property.Name).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(propertyNames, Is.EquivalentTo(new[]
                {
                    nameof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot.SourceColumnCount),
                    nameof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot.NoteBodyStyle),
                }));
                Assert.That(snapshotType.GetProperty(nameof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot.NoteBodyStyle))?.PropertyType,
                    Is.EqualTo(typeof(GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle>)));
                Assert.That(snapshotType.GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(LegacyManiaSkinConfiguration)));
                Assert.That(typeof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory).IsSealed, Is.True);
            });
        }

        [Test]
        public void TestSafeStringDoesNotExposeAcceptedStyleOrSourceData()
        {
            LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "NoteBodyStyle: RepeatTopAndBottom\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain(nameof(LegacyNoteBodyStyle.RepeatTopAndBottom)));
                Assert.That(snapshot.ToString(), Does.Not.Contain("Keys: 4"));
            });
        }

        private static LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot createSnapshot(string skinIni)
            => LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory.Create(decode(skinIni), 4).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
