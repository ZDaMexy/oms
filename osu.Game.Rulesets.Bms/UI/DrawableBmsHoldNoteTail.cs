// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHoldNoteTail : DrawableBmsHitObject
    {
        private BmsHoldNoteTailJudgement tailJudgement => (BmsHoldNoteTailJudgement)HitObject.Judgement;

        public override bool DisplayResult => tailJudgement.CountsForScore;

        public DrawableBmsHoldNoteTail(BmsHoldNoteTailEvent hitObject)
            : base(hitObject)
        {
            HandleUserInput = false;
        }

        protected override void OnApply()
        {
            base.OnApply();
            HandleUserInput = false;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
        }

        protected override JudgementResult CreateResult(Judgement judgement)
            => new JudgementResult(HitObject, judgement);

        internal void ApplyTailResult(HitResult result)
        {
            if (Judged)
                return;

            if (tailJudgement.CountsForScore)
                ApplyResult(result);
            else if (result.IsHit())
                ApplyMaxResult();
            else
                ApplyMinResult();
        }
    }
}
