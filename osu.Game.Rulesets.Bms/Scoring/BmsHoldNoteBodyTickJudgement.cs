// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>
    /// Gauge-only long-note body ticks never affect EX-SCORE or combo directly.
    /// </summary>
    public class BmsHoldNoteBodyTickJudgement : Judgement
    {
        public override HitResult MaxResult => HitResult.IgnoreHit;

        public override HitResult MinResult => HitResult.IgnoreMiss;
    }
}
