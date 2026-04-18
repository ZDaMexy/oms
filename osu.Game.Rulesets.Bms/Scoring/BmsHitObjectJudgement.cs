// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>
    /// Allows a playable BMS note to opt out of score contribution while still completing judgement flow.
    /// </summary>
    public class BmsHitObjectJudgement : Judgement
    {
        public bool CountsForScore { get; set; } = true;

        public override HitResult MaxResult => CountsForScore ? HitResult.Perfect : HitResult.IgnoreHit;

        public override HitResult MinResult => CountsForScore ? HitResult.Miss : HitResult.IgnoreMiss;
    }
}
