// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsJudgementCountsTest
    {
        [Test]
        public void TestCreateMapsBasicBmsJudgements()
        {
            var counts = BmsJudgementCounts.Create(new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 12,
                [HitResult.Great] = 3,
                [HitResult.Good] = 1,
                [HitResult.Meh] = 4,
                [HitResult.Miss] = 2,
                [HitResult.Ok] = 5,
                [HitResult.ComboBreak] = 9,
            });

            Assert.Multiple(() =>
            {
                Assert.That(counts.PerfectCount, Is.EqualTo(12));
                Assert.That(counts.GreatCount, Is.EqualTo(3));
                Assert.That(counts.GoodCount, Is.EqualTo(1));
                Assert.That(counts.BadCount, Is.EqualTo(4));
                Assert.That(counts.PoorCount, Is.EqualTo(2));
                Assert.That(counts.EmptyPoorCount, Is.EqualTo(5));
                Assert.That(counts.TotalCount, Is.EqualTo(27));
            });
        }

        [Test]
        public void TestEqualityUsesValueSemantics()
        {
            var left = new BmsJudgementCounts(1, 2, 3, 4, 5, 6);
            var right = new BmsJudgementCounts(1, 2, 3, 4, 5, 6);
            var different = new BmsJudgementCounts(1, 2, 3, 4, 5, 7);

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

        [Test]
        public void TestEligibilityFlagsReflectLiveRunState()
        {
            var perfect = new BmsJudgementCounts(4, 0, 0, 0, 0, 0);
            var fullComboOnly = new BmsJudgementCounts(4, 1, 0, 0, 0, 0);
            var broken = new BmsJudgementCounts(4, 1, 1, 0, 0, 0);

            Assert.Multiple(() =>
            {
                Assert.That(perfect.CanStillPerfect, Is.True);
                Assert.That(perfect.CanStillFullCombo, Is.True);

                Assert.That(fullComboOnly.CanStillPerfect, Is.False);
                Assert.That(fullComboOnly.CanStillFullCombo, Is.True);

                Assert.That(broken.CanStillPerfect, Is.False);
                Assert.That(broken.CanStillFullCombo, Is.False);
            });
        }

        [Test]
        public void TestLeastSevereFullComboBreakTracksRepresentativeBreakingJudgement()
        {
            var clean = new BmsJudgementCounts(4, 1, 0, 0, 0, 0);
            var goodBreak = new BmsJudgementCounts(4, 1, 2, 3, 4, 5);
            var badBreak = new BmsJudgementCounts(4, 1, 0, 3, 4, 5);
            var poorBreak = new BmsJudgementCounts(4, 1, 0, 0, 4, 5);
            var emptyPoorBreak = new BmsJudgementCounts(4, 1, 0, 0, 0, 5);

            Assert.Multiple(() =>
            {
                Assert.That(clean.LeastSevereFullComboBreakResult, Is.Null);
                Assert.That(clean.LeastSevereFullComboBreakCount, Is.EqualTo(0));

                Assert.That(goodBreak.LeastSevereFullComboBreakResult, Is.EqualTo(HitResult.Good));
                Assert.That(goodBreak.LeastSevereFullComboBreakCount, Is.EqualTo(2));

                Assert.That(badBreak.LeastSevereFullComboBreakResult, Is.EqualTo(HitResult.Meh));
                Assert.That(badBreak.LeastSevereFullComboBreakCount, Is.EqualTo(3));

                Assert.That(poorBreak.LeastSevereFullComboBreakResult, Is.EqualTo(HitResult.Miss));
                Assert.That(poorBreak.LeastSevereFullComboBreakCount, Is.EqualTo(4));

                Assert.That(emptyPoorBreak.LeastSevereFullComboBreakResult, Is.EqualTo(HitResult.Ok));
                Assert.That(emptyPoorBreak.LeastSevereFullComboBreakCount, Is.EqualTo(5));
            });
        }
    }
}
