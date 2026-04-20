// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsExScoreProgressInfoTest
    {
        [Test]
        public void TestCreateTracksDjLevelAndExRatio()
        {
            BmsExScoreProgressInfo? exScoreProgressInfo = BmsExScoreProgressInfo.Create(6, 9);

            Assert.That(exScoreProgressInfo.HasValue, Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(exScoreProgressInfo!.Value.CurrentExScore, Is.EqualTo(6));
                Assert.That(exScoreProgressInfo.Value.MaximumExScore, Is.EqualTo(9));
                Assert.That(exScoreProgressInfo.Value.ExRatio, Is.EqualTo(6d / 9).Within(0.0001));
                Assert.That(exScoreProgressInfo.Value.DjLevel, Is.EqualTo(BmsDjLevel.A));
            });
        }

        [Test]
        public void TestCreateReturnsNullWhenNoMaximumExScoreExists()
            => Assert.That(BmsExScoreProgressInfo.Create(0, 0), Is.Null);

        [Test]
        public void TestEqualityUsesValueSemantics()
        {
            var left = new BmsExScoreProgressInfo(1, 2);
            var right = new BmsExScoreProgressInfo(1, 2);
            var different = new BmsExScoreProgressInfo(2, 2);

            Assert.Multiple(() =>
            {
                Assert.That(left, Is.EqualTo(right));
                Assert.That(left == right, Is.True);
                Assert.That(left != right, Is.False);
                Assert.That(left.Equals(different), Is.False);
                Assert.That(left == different, Is.False);
                Assert.That(left != different, Is.True);
            });
        }
    }
}
