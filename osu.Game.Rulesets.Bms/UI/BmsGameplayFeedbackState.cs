// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsGameplayFeedbackState : IEquatable<BmsGameplayFeedbackState>
    {
        public readonly BmsScrollSpeedMetrics SpeedMetrics;

        public readonly BmsGameplayAdjustmentTarget? ActiveAdjustmentTarget;

        public readonly int EnabledAdjustmentTargetCount;

        public readonly int ActiveAdjustmentTargetIndex;

        public readonly bool IsAdjustmentTargetTemporarilyOverridden;

        public readonly BmsJudgementTimingFeedback? LatestJudgementFeedback;

        public readonly BmsJudgementCounts JudgementCounts;

        public readonly BmsExScoreProgressInfo? ExScoreProgressInfo;

        public readonly BmsExScorePacemakerInfo? ExScorePacemakerInfo;

        public readonly double TimingFeedbackVisualRange;

        public BmsGameplayFeedbackState(BmsScrollSpeedMetrics speedMetrics, BmsGameplayAdjustmentTarget? activeAdjustmentTarget, int enabledAdjustmentTargetCount,
                                        int activeAdjustmentTargetIndex, bool isAdjustmentTargetTemporarilyOverridden,
                                        BmsJudgementTimingFeedback? latestJudgementFeedback, BmsJudgementCounts judgementCounts,
                                        BmsExScoreProgressInfo? exScoreProgressInfo,
                                        BmsExScorePacemakerInfo? exScorePacemakerInfo,
                                        double timingFeedbackVisualRange)
        {
            SpeedMetrics = speedMetrics;
            ActiveAdjustmentTarget = activeAdjustmentTarget;
            EnabledAdjustmentTargetCount = enabledAdjustmentTargetCount;
            ActiveAdjustmentTargetIndex = activeAdjustmentTargetIndex;
            IsAdjustmentTargetTemporarilyOverridden = isAdjustmentTargetTemporarilyOverridden;
            LatestJudgementFeedback = latestJudgementFeedback;
            JudgementCounts = judgementCounts;
            ExScoreProgressInfo = exScoreProgressInfo;
            ExScorePacemakerInfo = exScorePacemakerInfo;
            TimingFeedbackVisualRange = timingFeedbackVisualRange;
        }

        public bool Equals(BmsGameplayFeedbackState other)
            => SpeedMetrics.Equals(other.SpeedMetrics)
               && ActiveAdjustmentTarget == other.ActiveAdjustmentTarget
               && EnabledAdjustmentTargetCount == other.EnabledAdjustmentTargetCount
               && ActiveAdjustmentTargetIndex == other.ActiveAdjustmentTargetIndex
               && IsAdjustmentTargetTemporarilyOverridden == other.IsAdjustmentTargetTemporarilyOverridden
               && Nullable.Equals(LatestJudgementFeedback, other.LatestJudgementFeedback)
               && JudgementCounts.Equals(other.JudgementCounts)
               && Nullable.Equals(ExScoreProgressInfo, other.ExScoreProgressInfo)
               && Nullable.Equals(ExScorePacemakerInfo, other.ExScorePacemakerInfo)
               && TimingFeedbackVisualRange.Equals(other.TimingFeedbackVisualRange);

        public override bool Equals(object? obj)
            => obj is BmsGameplayFeedbackState other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SpeedMetrics);
            hash.Add(ActiveAdjustmentTarget);
            hash.Add(EnabledAdjustmentTargetCount);
            hash.Add(ActiveAdjustmentTargetIndex);
            hash.Add(IsAdjustmentTargetTemporarilyOverridden);
            hash.Add(LatestJudgementFeedback);
            hash.Add(JudgementCounts);
            hash.Add(ExScoreProgressInfo);
            hash.Add(ExScorePacemakerInfo);
            hash.Add(TimingFeedbackVisualRange);
            return hash.ToHashCode();
        }

        public static bool operator ==(BmsGameplayFeedbackState left, BmsGameplayFeedbackState right)
            => left.Equals(right);

        public static bool operator !=(BmsGameplayFeedbackState left, BmsGameplayFeedbackState right)
            => !left.Equals(right);
    }
}
