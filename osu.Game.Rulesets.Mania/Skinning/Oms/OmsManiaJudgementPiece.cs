// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsManiaJudgementPiece : CompositeDrawable, IAnimatableJudgement
    {
        private readonly HitResult result;
        private readonly Drawable animation;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public OmsManiaJudgementPiece(HitResult result, Drawable animation)
        {
            this.result = result;
            this.animation = animation;

            Origin = Anchor.Centre;

            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ManiaGameplaySkinStageContext stageContext)
        {
            LayoutSnapshot = stageContext.Snapshot;
            InternalChild = animation.With(d =>
            {
                d.Anchor = Anchor.Centre;
                d.Origin = Anchor.Centre;
            });
            ManiaGameplaySkinLayoutProjection.ApplyJudgementPlacement(this, stageContext);
        }

        public void PlayAnimation()
        {
            (animation as IFramedAnimation)?.GotoFrame(0);

            this.FadeInFromZero(20, Easing.Out)
                .Then().Delay(160)
                .FadeOutFromOne(40, Easing.In);

            switch (result)
            {
                case HitResult.None:
                    break;

                case HitResult.Miss:
                    animation.ScaleTo(1.2f).Then().ScaleTo(1, 100, Easing.Out);

                    animation.RotateTo(0);
                    animation.RotateTo(RNG.NextSingle(-5.73f, 5.73f), 100, Easing.Out);
                    break;

                default:
                    animation.ScaleTo(0.8f)
                             .Then().ScaleTo(1, 40)
                             .Then().ScaleTo(0.85f)
                             .Then().ScaleTo(0.7f, 40)
                             .Then().Delay(100)
                             .Then().ScaleTo(0.4f, 40, Easing.In);
                    break;
            }
        }

        public Drawable? GetAboveHitObjectsProxiedContent() => null;
    }
}
