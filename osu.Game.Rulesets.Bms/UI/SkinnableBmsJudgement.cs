// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class SkinnableBmsJudgement : CompositeDrawable, IAnimatableJudgement
    {
        private readonly SkinnableDrawable skinnableDrawable;

        public SkinnableBmsJudgement(HitResult result)
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.Centre;

            InternalChild = skinnableDrawable = new SkinnableDrawable(new BmsJudgementSkinLookup(result), _ => new BmsJudgementPiece(result))
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.Both,
            };
        }

        public void PlayAnimation()
        {
            if (skinnableDrawable.Drawable is IAnimatableJudgement animatable)
                animatable.PlayAnimation();
        }

        public Drawable? GetAboveHitObjectsProxiedContent()
            => (skinnableDrawable.Drawable as IAnimatableJudgement)?.GetAboveHitObjectsProxiedContent();
    }
}
