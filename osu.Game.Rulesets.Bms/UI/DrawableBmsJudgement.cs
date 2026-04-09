// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class DrawableBmsJudgement : DrawableJudgement
    {
        public DrawableBmsJudgement()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(1f);
        }

        protected override Drawable CreateDefaultJudgement(HitResult result) => new BmsJudgementPiece(result);
    }
}
