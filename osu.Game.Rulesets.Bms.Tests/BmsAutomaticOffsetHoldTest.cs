// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsAutomaticOffsetHoldTest
    {
        [Test]
        public void HeldThroughLnUsesHeadTimingInsteadOfFrameOvershoot()
        {
            var sample = DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                BmsLongNoteMode.LN, HitResult.Great, -32, HitResult.Perfect, 45, false);

            Assert.That(sample, Is.EqualTo((HitResult.Great, -32d)));
        }

        [TestCase(HitResult.Perfect, -18, HitResult.Good, -85, HitResult.Good, -85)]
        [TestCase(HitResult.Good, 85, HitResult.Perfect, -18, HitResult.Good, 85)]
        [TestCase(HitResult.Great, 32, HitResult.Great, -32, HitResult.Great, -32)]
        public void ReleasedLnUsesCombinedGradeAndLargerTimingError(
            HitResult head, double headError, HitResult tail, double tailError, HitResult expectedGrade, double expectedError)
        {
            var sample = DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                BmsLongNoteMode.LN, head, headError, tail, tailError, true);

            Assert.That(sample, Is.EqualTo((expectedGrade, expectedError)));
        }

        [TestCase(HitResult.Miss, HitResult.Perfect)]
        [TestCase(HitResult.Perfect, HitResult.Miss)]
        public void BrokenLnCannotTrainFromSuccessfulHalf(HitResult head, HitResult tail)
        {
            Assert.That(DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                BmsLongNoteMode.LN, head, 30, tail, -30, true), Is.Null);
        }

        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void ChargeNoteOnlySamplesRealRelease(BmsLongNoteMode mode)
        {
            Assert.That(DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                mode, HitResult.Good, 85, HitResult.Perfect, 18, false), Is.Null);

            Assert.That(DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                mode, HitResult.Good, 85, HitResult.Perfect, -18, true), Is.EqualTo((HitResult.Perfect, -18d)));
        }

        [Test]
        public void RegrabbedHcnReleaseCanTrainDespiteMissedHead()
        {
            Assert.That(DrawableBmsHoldNote.SelectAutomaticOffsetCompletionSample(
                BmsLongNoteMode.HCN, HitResult.Miss, 200, HitResult.Great, -32, true), Is.EqualTo((HitResult.Great, -32d)));
        }
    }
}
