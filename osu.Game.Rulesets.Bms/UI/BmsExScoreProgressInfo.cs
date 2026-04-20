// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsExScoreProgressInfo : IEquatable<BmsExScoreProgressInfo>
    {
        public readonly long CurrentExScore;

        public readonly long MaximumExScore;

        public double ExRatio => MaximumExScore > 0 ? CurrentExScore / (double)MaximumExScore : 0;

        public BmsDjLevel DjLevel => BmsDjLevelCalculator.Calculate(CurrentExScore, MaximumExScore);

        public BmsExScoreProgressInfo(long currentExScore, long maximumExScore)
        {
            MaximumExScore = Math.Max(0, maximumExScore);
            CurrentExScore = Math.Clamp(currentExScore, 0, MaximumExScore);
        }

        public static BmsExScoreProgressInfo? Create(long currentExScore, long maximumExScore)
            => maximumExScore <= 0 ? null : new BmsExScoreProgressInfo(currentExScore, maximumExScore);

        public bool Equals(BmsExScoreProgressInfo other)
            => CurrentExScore == other.CurrentExScore
               && MaximumExScore == other.MaximumExScore;

        public override bool Equals(object? obj)
            => obj is BmsExScoreProgressInfo other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(CurrentExScore, MaximumExScore);

        public static bool operator ==(BmsExScoreProgressInfo left, BmsExScoreProgressInfo right)
            => left.Equals(right);

        public static bool operator !=(BmsExScoreProgressInfo left, BmsExScoreProgressInfo right)
            => !left.Equals(right);
    }
}
