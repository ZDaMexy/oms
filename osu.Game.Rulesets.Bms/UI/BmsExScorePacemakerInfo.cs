// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsExScorePacemakerInfo : IEquatable<BmsExScorePacemakerInfo>
    {
        private const int maximum_ex_score_per_judgement = 2;

        public readonly BmsDjLevel TargetLevel;

        public readonly long CurrentExScore;

        public readonly long CurrentTargetExScore;

        public readonly long FinalTargetExScore;

        public readonly long MaximumExScore;

        public readonly int JudgedHits;

        public long Delta => CurrentExScore - CurrentTargetExScore;

        public BmsExScorePacemakerInfo(BmsDjLevel targetLevel, long currentExScore, long currentTargetExScore, long finalTargetExScore, long maximumExScore, int judgedHits)
        {
            TargetLevel = targetLevel;
            CurrentExScore = currentExScore;
            CurrentTargetExScore = currentTargetExScore;
            FinalTargetExScore = finalTargetExScore;
            MaximumExScore = maximumExScore;
            JudgedHits = judgedHits;
        }

        public static BmsExScorePacemakerInfo? Create(BmsDjLevel targetLevel, long currentExScore, int judgedHits, long maximumExScore)
        {
            if (maximumExScore <= 0)
                return null;

            double threshold = BmsDjLevelDisplay.GetThreshold(targetLevel);
            long maximumExScoreSoFar = Math.Clamp((long)judgedHits * maximum_ex_score_per_judgement, 0, maximumExScore);

            return new BmsExScorePacemakerInfo(
                targetLevel,
                currentExScore,
                getTargetExScore(maximumExScoreSoFar, threshold),
                getTargetExScore(maximumExScore, threshold),
                maximumExScore,
                Math.Max(0, judgedHits));
        }

        public bool Equals(BmsExScorePacemakerInfo other)
            => TargetLevel == other.TargetLevel
               && CurrentExScore == other.CurrentExScore
               && CurrentTargetExScore == other.CurrentTargetExScore
               && FinalTargetExScore == other.FinalTargetExScore
               && MaximumExScore == other.MaximumExScore
               && JudgedHits == other.JudgedHits;

        public override bool Equals(object? obj) => obj is BmsExScorePacemakerInfo other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(TargetLevel, CurrentExScore, CurrentTargetExScore, FinalTargetExScore, MaximumExScore, JudgedHits);

        private static long getTargetExScore(long maximumExScore, double threshold)
            => Math.Clamp((long)Math.Ceiling(maximumExScore * threshold), 0, maximumExScore);
    }
}
