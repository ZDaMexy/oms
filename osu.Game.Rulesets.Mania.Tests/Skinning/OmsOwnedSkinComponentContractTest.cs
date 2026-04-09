// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.Skinning.Oms;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [TestFixture]
    public class OmsOwnedSkinComponentContractTest
    {
        [Test]
        public void TestOmsJudgementPieceNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyManiaJudgementPiece).IsAssignableFrom(typeof(OmsManiaJudgementPiece)), Is.False);
            Assert.That(typeof(IAnimatableJudgement).IsAssignableFrom(typeof(OmsManiaJudgementPiece)), Is.True);
        }

        [Test]
        public void TestOmsHitExplosionNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyHitExplosion).IsAssignableFrom(typeof(OmsHitExplosion)), Is.False);
            Assert.That(typeof(IHitExplosion).IsAssignableFrom(typeof(OmsHitExplosion)), Is.True);
        }

        [Test]
        public void TestOmsComboCounterNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyManiaComboCounter).IsAssignableFrom(typeof(OmsManiaComboCounter)), Is.False);
            Assert.That(typeof(ISerialisableDrawable).IsAssignableFrom(typeof(OmsManiaComboCounter)), Is.True);
        }

        [Test]
        public void TestOmsBarLineNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyBarLine).IsAssignableFrom(typeof(OmsBarLine)), Is.False);
        }

        [Test]
        public void TestOmsNotePieceNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyNotePiece).IsAssignableFrom(typeof(OmsNotePiece)), Is.False);
        }

        [Test]
        public void TestOmsHoldNoteHeadPieceNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyHoldNoteHeadPiece).IsAssignableFrom(typeof(OmsHoldNoteHeadPiece)), Is.False);
        }

        [Test]
        public void TestOmsHoldNoteTailPieceNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyHoldNoteTailPiece).IsAssignableFrom(typeof(OmsHoldNoteTailPiece)), Is.False);
        }

        [Test]
        public void TestOmsHoldNoteBodyPieceNoLongerUsesLegacyImplementation()
        {
            Assert.That(typeof(LegacyBodyPiece).IsAssignableFrom(typeof(OmsHoldNoteBodyPiece)), Is.False);
        }
    }
}