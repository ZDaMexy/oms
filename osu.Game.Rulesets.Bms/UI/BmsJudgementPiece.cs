// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class BmsJudgementPiece : DefaultJudgementPiece
    {
        public BmsJudgementPiece(HitResult result)
            : base(result)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            JudgementText.Font = JudgementText.Font.With(size: 22);
            JudgementText.Text = BmsHitResultDisplayNames.GetDisplayName(Result);
        }

        public override void PlayAnimation()
        {
            if (Result == HitResult.None)
            {
                this.FadeOutFromOne(800);
                return;
            }

            if (Result.IsMiss())
            {
                this.ScaleTo(1.6f);
                this.ScaleTo(1, 100, Easing.In);

                this.FadeOutFromOne(800);
                return;
            }

            this.ScaleTo(0.8f);
            this.ScaleTo(1, 250, Easing.OutElastic);

            this.Delay(50)
                .ScaleTo(0.75f, 250)
                .FadeOut(200);
        }
    }
}
