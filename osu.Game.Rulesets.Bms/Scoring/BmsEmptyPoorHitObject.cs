// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    internal sealed class BmsEmptyPoorHitObject : HitObject
    {
        public override Judgement CreateJudgement() => new BmsPoorJudgement();
    }

    internal sealed class BmsPoorJudgement : Judgement
    {
        public override HitResult MaxResult => HitResult.IgnoreHit;

        public override HitResult MinResult => HitResult.ComboBreak;
    }
}
