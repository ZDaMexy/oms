// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class BmsJudgementPiece : DefaultJudgementPiece
    {
        private const float judgement_y_offset = 140f;

        private IBindable<ScrollingDirection> direction = null!;

        public BmsJudgementPiece(HitResult result)
            : base(result)
        {
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => updatePosition(), true);
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

                this.MoveToY(direction.Value == ScrollingDirection.Up ? judgement_y_offset : -judgement_y_offset);
                this.MoveToOffset(new Vector2(0, 100), 800, Easing.InQuint);

                this.RotateTo(0);
                this.RotateTo(40, 800, Easing.InQuint);

                this.FadeOutFromOne(800);
                return;
            }

            this.ScaleTo(0.8f);
            this.ScaleTo(1, 250, Easing.OutElastic);

            this.Delay(50)
                .ScaleTo(0.75f, 250)
                .FadeOut(200);
        }

        private void updatePosition()
        {
            Anchor = direction.Value == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
            Y = direction.Value == ScrollingDirection.Up ? judgement_y_offset : -judgement_y_offset;
        }
    }
}
