// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class DrawableBmsJudgement : DrawableJudgement
    {
        private IBindable<ScrollingDirection> direction = null!;

        public DrawableBmsJudgement()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(1f);
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => updateJudgementPosition());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateJudgementPosition();
        }

        protected override Drawable CreateDefaultJudgement(HitResult result) => new BmsJudgementPiece(result);

        private void updateJudgementPosition()
        {
            if (JudgementBody == null)
                return;

            BmsGameplayFeedbackLayout.ApplyJudgementDefaults(JudgementBody, direction.Value);
        }
    }
}
