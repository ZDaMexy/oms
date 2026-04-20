// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsJudgementCounts : IEquatable<BmsJudgementCounts>
    {
        public readonly int PerfectCount;

        public readonly int GreatCount;

        public readonly int GoodCount;

        public readonly int BadCount;

        public readonly int PoorCount;

        public readonly int EmptyPoorCount;

        public int TotalCount => PerfectCount + GreatCount + GoodCount + BadCount + PoorCount + EmptyPoorCount;

        public bool CanStillPerfect => GreatCount == 0 && CanStillFullCombo;

        public bool CanStillFullCombo => GoodCount == 0 && BadCount == 0 && PoorCount == 0 && EmptyPoorCount == 0;

        public HitResult? LeastSevereFullComboBreakResult => GoodCount > 0
            ? HitResult.Good
            : BadCount > 0
                ? HitResult.Meh
                : PoorCount > 0
                    ? HitResult.Miss
                    : EmptyPoorCount > 0
                        ? HitResult.Ok
                        : null;

        public int LeastSevereFullComboBreakCount => LeastSevereFullComboBreakResult switch
        {
            HitResult.Good => GoodCount,
            HitResult.Meh => BadCount,
            HitResult.Miss => PoorCount,
            HitResult.Ok => EmptyPoorCount,
            _ => 0,
        };

        public BmsJudgementCounts(int perfectCount, int greatCount, int goodCount, int badCount, int poorCount, int emptyPoorCount)
        {
            PerfectCount = Math.Max(0, perfectCount);
            GreatCount = Math.Max(0, greatCount);
            GoodCount = Math.Max(0, goodCount);
            BadCount = Math.Max(0, badCount);
            PoorCount = Math.Max(0, poorCount);
            EmptyPoorCount = Math.Max(0, emptyPoorCount);
        }

        public static BmsJudgementCounts Create(IReadOnlyDictionary<HitResult, int>? statistics)
        {
            if (statistics == null)
                return default;

            return new BmsJudgementCounts(
                statistics.GetValueOrDefault(HitResult.Perfect),
                statistics.GetValueOrDefault(HitResult.Great),
                statistics.GetValueOrDefault(HitResult.Good),
                statistics.GetValueOrDefault(HitResult.Meh),
                statistics.GetValueOrDefault(HitResult.Miss),
                statistics.GetValueOrDefault(HitResult.Ok));
        }

        public bool Equals(BmsJudgementCounts other)
            => PerfectCount == other.PerfectCount
               && GreatCount == other.GreatCount
               && GoodCount == other.GoodCount
               && BadCount == other.BadCount
               && PoorCount == other.PoorCount
               && EmptyPoorCount == other.EmptyPoorCount;

        public override bool Equals(object? obj)
            => obj is BmsJudgementCounts other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(PerfectCount, GreatCount, GoodCount, BadCount, PoorCount, EmptyPoorCount);

        public static bool operator ==(BmsJudgementCounts left, BmsJudgementCounts right)
            => left.Equals(right);

        public static bool operator !=(BmsJudgementCounts left, BmsJudgementCounts right)
            => !left.Equals(right);
    }
}
