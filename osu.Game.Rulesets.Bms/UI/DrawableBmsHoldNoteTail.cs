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

        public override void PlaySamples()
        {
            // BMS long-note ends do not sound in LR2/beatoraja — only the head keysound plays. The tail object's WAV
            // still arms the lane's empty-press keysound (BmsBeatmap timeline), but auto-playing it on release / auto
            // end would double the sound (an LNTYPE1 tail commonly repeats the head WAV — e.g. GOODBOUNCE's scratch LN
            // produced "stomp your fee feet") and, with the per-WAV cut, even cut the head. Intentionally silent: do
            // not call base.PlaySamples().
        }

        protected override void OnApply()
        {
            base.OnApply();
            HandleUserInput = false;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
        }

        protected internal override JudgementResult CreateResult(Judgement judgement)
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
