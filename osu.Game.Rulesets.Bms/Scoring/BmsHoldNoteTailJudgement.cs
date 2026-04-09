// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>
    /// Allows long-note tail release points to switch between scoring and ignored behaviour based on the active mode.
    /// </summary>
    public class BmsHoldNoteTailJudgement : Judgement
    {
        public bool CountsForScore { get; set; } = true;

        public override HitResult MaxResult => CountsForScore ? HitResult.Perfect : HitResult.IgnoreHit;

        public override HitResult MinResult => CountsForScore ? HitResult.Miss : HitResult.IgnoreMiss;
    }
}
