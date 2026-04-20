// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsJudgementTimingFeedback : IEquatable<BmsJudgementTimingFeedback>
    {
        public HitResult Result { get; }

        public double TimeOffset { get; }

        public bool ShowsTimingDirection { get; }

        public ulong OccurrenceId { get; }

        public BmsJudgementTimingFeedback(HitResult result, double timeOffset, bool showsTimingDirection, ulong occurrenceId = 0)
        {
            Result = result;
            TimeOffset = timeOffset;
            ShowsTimingDirection = showsTimingDirection;
            OccurrenceId = occurrenceId;
        }

        public static BmsJudgementTimingFeedback? FromResult(JudgementResult judgementResult, ulong occurrenceId = 0)
        {
            if (!judgementResult.Type.IsBasic())
                return null;

            return new BmsJudgementTimingFeedback(
                judgementResult.Type,
                judgementResult.TimeOffset,
                judgementResult.HitObject is not BmsEmptyPoorHitObject && judgementResult.HitObject.HitWindows != HitWindows.Empty,
                occurrenceId);
        }

        public bool Equals(BmsJudgementTimingFeedback other)
            => Result == other.Result
               && TimeOffset.Equals(other.TimeOffset)
               && ShowsTimingDirection == other.ShowsTimingDirection
               && OccurrenceId == other.OccurrenceId;

        public override bool Equals(object? obj)
            => obj is BmsJudgementTimingFeedback other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine((int)Result, TimeOffset, ShowsTimingDirection, OccurrenceId);

        public static bool operator ==(BmsJudgementTimingFeedback left, BmsJudgementTimingFeedback right) => left.Equals(right);

        public static bool operator !=(BmsJudgementTimingFeedback left, BmsJudgementTimingFeedback right) => !left.Equals(right);
    }
}
