// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsExScorePacemakerInfoTest
    {
        [Test]
        public void TestCreateTracksCurrentJudgedHitPace()
        {
            BmsExScorePacemakerInfo? pacemakerInfo = BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 3, 2, 6);

            Assert.That(pacemakerInfo.HasValue, Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(pacemakerInfo!.Value.CurrentTargetExScore, Is.EqualTo(4));
                Assert.That(pacemakerInfo.Value.FinalTargetExScore, Is.EqualTo(6));
                Assert.That(pacemakerInfo.Value.Delta, Is.EqualTo(-1));
            });
        }

        [Test]
        public void TestCreateReturnsNullWhenNoMaximumExScoreExists()
            => Assert.That(BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 0, 0, 0), Is.Null);
    }
}
