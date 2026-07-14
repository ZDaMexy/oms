// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinConfigurationDeclarationFactoryTest
    {
        [Test]
        public void TestMissingKeymodeBucketIsAbsent()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "PlayfieldWidth: 0\n");

            GameplaySkinConfigurationDeclaration<BmsKeymode> declaration =
                BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(decoder.Configurations, BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(decoder.Configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestGeneralKeymodeMetadataDoesNotDeclareBmsBucket()
        {
            BmsSkinDecoder decoder = decode(
                "[General]\n" +
                "Keymodes: 7K\n");

            Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                decoder.Configurations, BmsKeymode.Key7K).IsDeclared, Is.False);
        }

        [Test]
        public void TestInvalidKeymodeDeclarationIsAbsent()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: future-mode\n");

            Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                decoder.Configurations, BmsKeymode.Key7K).IsDeclared, Is.False);
        }

        [Test]
        public void TestExplicitEmptyKeymodeBucketIsDeclared()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n");

            GameplaySkinConfigurationDeclaration<BmsKeymode> declaration =
                BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(decoder.Configurations, BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(declaration.Value, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(decoder.Configurations[0].Geometry, Is.Empty);
                Assert.That(decoder.Configurations[0].Colours, Is.Empty);
                Assert.That(decoder.Configurations[0].ImageLookups, Is.Empty);
            });
        }

        [Test]
        public void TestExplicitDefaultValuesRemainInDeclaredBucket()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: 0\n" +
                "HitTargetImage:\n");

            GameplaySkinConfigurationDeclaration<BmsKeymode> declaration =
                BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(decoder.Configurations, BmsKeymode.Key7K);
            BmsSkinConfiguration configuration = decoder.Configurations[0];

            Assert.Multiple(() =>
            {
                Assert.That(declaration.IsDeclared, Is.True);
                Assert.That(configuration.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.Zero);
                Assert.That(configuration.ImageLookups["HitTargetImage"], Is.Empty);
            });
        }

        [Test]
        public void TestMalformedFieldDoesNotEraseDeclaredBucket()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "PlayfieldWidth: not-a-number\n");

            Assert.Multiple(() =>
            {
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key7K).IsDeclared, Is.True);
                Assert.That(decoder.Configurations[0].Geometry, Does.Not.ContainKey(BmsSkinConfigurationLookups.PlayfieldWidth));
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 5K\n" +
                "[Bms]\n" +
                "Keymode: 14K\n");

            Assert.Multiple(() =>
            {
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key5K).Value, Is.EqualTo(BmsKeymode.Key5K));
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key14K).Value, Is.EqualTo(BmsKeymode.Key14K));
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key9K_Bms).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestBmsAndPmsNineKeyBucketsRemainDistinct()
        {
            BmsSkinDecoder decoder = decode(
                "[Bms]\n" +
                "Keymode: 9K_BMS\n" +
                "[Bms]\n" +
                "Keymode: 9K_PMS\n");

            Assert.Multiple(() =>
            {
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key9K_Bms).Value, Is.EqualTo(BmsKeymode.Key9K_Bms));
                Assert.That(BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    decoder.Configurations, BmsKeymode.Key9K_Pms).Value, Is.EqualTo(BmsKeymode.Key9K_Pms));
            });
        }

        [Test]
        public void TestAmbiguousOrInvalidDecoderOutputRejected()
        {
            var duplicate = new[]
            {
                new BmsSkinConfiguration(BmsKeymode.Key7K),
                new BmsSkinConfiguration(BmsKeymode.Key7K),
            };
            BmsSkinConfiguration[] containsNull = { null! };

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    null!, BmsKeymode.Key7K), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    containsNull, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    duplicate, BmsKeymode.Key7K), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinConfigurationDeclarationFactory.FindDeclaredBucket(
                    Array.Empty<BmsSkinConfiguration>(), (BmsKeymode)99), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        private static BmsSkinDecoder decode(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder;
        }
    }
}
