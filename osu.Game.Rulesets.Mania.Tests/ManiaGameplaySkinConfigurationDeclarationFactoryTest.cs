// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed class ManiaGameplaySkinConfigurationDeclarationFactoryTest
    {
        [Test]
        public void TestMissingKeysDoesNotUseLegacySkinSyntheticDefault()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "ColumnWidth: 16,16,16,16\n");

            GameplaySkinConfigurationDeclaration<int> declaration =
                ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestGeneralKeyMetadataDoesNotDeclareManiaBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[General]\n" +
                "Keys: 4\n");

            Assert.That(ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 4).IsDeclared, Is.False);
        }

        [Test]
        public void TestExplicitEmptyKeysBucketIsDeclared()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n");

            GameplaySkinConfigurationDeclaration<int> declaration =
                ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(declaration.Value, Is.EqualTo(4));
                Assert.That(configurations[0].ImageLookups, Is.Empty);
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "[Mania]\n" +
                "Keys: 19\n");

            Assert.Multiple(() =>
            {
                Assert.That(ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 4).Value, Is.EqualTo(4));
                Assert.That(ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 7).Value, Is.EqualTo(7));
                Assert.That(ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 19).Value, Is.EqualTo(19));
                Assert.That(ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(configurations, 8).IsDeclared, Is.False);
            });
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(19)]
        [TestCase(20)]
        public void TestSupportedGameplayKeyCountsAccepted(int totalColumns)
        {
            Assert.That(() => ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                Array.Empty<LegacyManiaSkinConfiguration>(), totalColumns), Throws.Nothing);
        }

        [TestCase(0)]
        [TestCase(21)]
        public void TestUnsupportedGameplayKeyCountRejected(int totalColumns)
        {
            Assert.That(() => ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                Array.Empty<LegacyManiaSkinConfiguration>(), totalColumns), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TestAmbiguousOrInvalidDecoderOutputRejected()
        {
            var duplicate = new[]
            {
                new LegacyManiaSkinConfiguration(4),
                new LegacyManiaSkinConfiguration(4),
            };
            LegacyManiaSkinConfiguration[] containsNull = { null! };

            Assert.Multiple(() =>
            {
                Assert.That(() => ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(duplicate, 4), Throws.ArgumentException);
            });
        }

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
