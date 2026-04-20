// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsGameplayFeedbackStateTest
    {
        [Test]
        public void TestEqualityUsesValueSemantics()
        {
            var left = createState();
            var right = createState();
            var different = createState(exScorePacemakerInfo: BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 2, 1, 2));
            var differentCounts = createState(judgementCounts: new BmsJudgementCounts(1, 0, 0, 0, 0, 0));
            var differentExScoreProgress = createState(exScoreProgressInfo: BmsExScoreProgressInfo.Create(2, 2));
            var differentRange = createState(timingFeedbackVisualRange: 9);

            Assert.Multiple(() =>
            {
                Assert.That(left, Is.EqualTo(right));
                Assert.That(left == right, Is.True);
                Assert.That(left != right, Is.False);
                Assert.That(left.Equals(different), Is.False);
                Assert.That(left == different, Is.False);
                Assert.That(left.Equals(differentCounts), Is.False);
                Assert.That(left == differentCounts, Is.False);
                Assert.That(left.Equals(differentExScoreProgress), Is.False);
                Assert.That(left == differentExScoreProgress, Is.False);
                Assert.That(left.Equals(differentRange), Is.False);
                Assert.That(left == differentRange, Is.False);
            });
        }

        [Test]
        public void TestEqualityTreatsJudgementOccurrenceAsDistinct()
        {
            var left = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true, 1));
            var refreshed = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true, 2));

            Assert.Multiple(() =>
            {
                Assert.That(left.Equals(refreshed), Is.False);
                Assert.That(left == refreshed, Is.False);
                Assert.That(left != refreshed, Is.True);
            });
        }

        private static BmsGameplayFeedbackState createState(BmsScrollSpeedMetrics? speedMetrics = null, BmsGameplayAdjustmentTarget? activeAdjustmentTarget = BmsGameplayAdjustmentTarget.Sudden,
                                    int enabledAdjustmentTargetCount = 3, int activeAdjustmentTargetIndex = 0, bool isAdjustmentTargetTemporarilyOverridden = false,
                                    BmsJudgementTimingFeedback? latestJudgementFeedback = null, BmsJudgementCounts judgementCounts = default,
                                    BmsExScoreProgressInfo? exScoreProgressInfo = null,
                                    BmsExScorePacemakerInfo? exScorePacemakerInfo = null,
                                    double timingFeedbackVisualRange = 18)
            => new BmsGameplayFeedbackState(
                speedMetrics ?? BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150),
                activeAdjustmentTarget,
                enabledAdjustmentTargetCount,
                activeAdjustmentTargetIndex,
                isAdjustmentTargetTemporarilyOverridden,
                latestJudgementFeedback,
                judgementCounts,
                exScoreProgressInfo ?? BmsExScoreProgressInfo.Create(1, 2),
                exScorePacemakerInfo ?? BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 1, 1, 2),
                timingFeedbackVisualRange);
    }
}
