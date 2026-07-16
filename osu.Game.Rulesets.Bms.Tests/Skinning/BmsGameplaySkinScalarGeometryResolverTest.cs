// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsGameplaySkinScalarGeometryResolverTest
    {
        [TestCase(0.000001f)]
        [TestCase(0.5775f)]
        [TestCase(1f)]
        public void TestAcceptedLongNoteBodyWidthIsPreserved(float value)
        {
            BmsGameplaySkinScalarGeometryResolution resolution = resolve(
                GameplaySkinConfigurationDeclaration<float>.Declared(value));

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Value, Is.EqualTo(value));
                Assert.That(resolution.RejectionReason, Is.Null);
                Assert.That(resolution.UsedDefault, Is.False);
            });
        }

        [Test]
        public void TestSmallestPositiveFloatIsAccepted()
        {
            BmsGameplaySkinScalarGeometryResolution resolution = resolve(
                GameplaySkinConfigurationDeclaration<float>.Declared(float.Epsilon));

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Value, Is.EqualTo(float.Epsilon));
                Assert.That(resolution.RejectionReason, Is.Null);
                Assert.That(resolution.UsedDefault, Is.False);
            });
        }

        [Test]
        public void TestAbsentUsesDefault()
            => assertDefault(
                GameplaySkinConfigurationDeclaration<float>.Absent,
                BmsGameplaySkinScalarGeometryRejectionReason.DeclarationAbsent);

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TestNonFiniteUsesDefault(float value)
            => assertDefault(
                GameplaySkinConfigurationDeclaration<float>.Declared(value),
                BmsGameplaySkinScalarGeometryRejectionReason.NonFinite);

        [TestCase(-1f)]
        [TestCase(0f)]
        public void TestAtOrBelowMinimumUsesDefault(float value)
            => assertDefault(
                GameplaySkinConfigurationDeclaration<float>.Declared(value),
                BmsGameplaySkinScalarGeometryRejectionReason.AtOrBelowMinimum);

        [Test]
        public void TestNegativeZeroUsesDefault()
        {
            float negativeZero = System.BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));

            assertDefault(
                GameplaySkinConfigurationDeclaration<float>.Declared(negativeZero),
                BmsGameplaySkinScalarGeometryRejectionReason.AtOrBelowMinimum);
        }

        [TestCase(1.000001f)]
        [TestCase(float.MaxValue)]
        public void TestAboveMaximumUsesDefault(float value)
            => assertDefault(
                GameplaySkinConfigurationDeclaration<float>.Declared(value),
                BmsGameplaySkinScalarGeometryRejectionReason.AboveMaximum);

        [Test]
        public void TestUnsupportedFieldIsRejected()
            => Assert.That(
                () => BmsGameplaySkinScalarGeometryResolver.Resolve(
                    BmsSkinConfigurationLookups.PlayfieldWidth,
                    GameplaySkinConfigurationDeclaration<float>.Declared(0.5f)),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());

        private static BmsGameplaySkinScalarGeometryResolution resolve(
            GameplaySkinConfigurationDeclaration<float> declaration)
            => BmsGameplaySkinScalarGeometryResolver.Resolve(
                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                declaration);

        private static void assertDefault(
            GameplaySkinConfigurationDeclaration<float> declaration,
            BmsGameplaySkinScalarGeometryRejectionReason expectedReason)
        {
            BmsGameplaySkinScalarGeometryResolution resolution = resolve(declaration);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Value, Is.EqualTo(BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH));
                Assert.That(resolution.RejectionReason, Is.EqualTo(expectedReason));
                Assert.That(resolution.UsedDefault, Is.True);
            });
        }
    }
}
