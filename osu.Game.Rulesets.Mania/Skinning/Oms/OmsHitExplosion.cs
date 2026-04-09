// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHitExplosion : LegacyManiaColumnElement, IHitExplosion
    {
        public const double FadeInDuration = 80;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Drawable? explosion;

        public OmsHitExplosion()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            string imageName = GetColumnSkinConfig<string>(skin, LegacyManiaSkinConfigurationLookups.ExplosionImage)?.Value
                               ?? "lightingN";

            float explosionScale = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.ExplosionScale)?.Value
                                   ?? 1;

            var tempAnimation = skin.GetAnimation(imageName, true, false);
            double frameLength = 0;

            if (tempAnimation is IFramedAnimation framedAnimation && framedAnimation.FrameCount > 0)
                frameLength = Math.Max(1000 / 60.0, 170.0 / framedAnimation.FrameCount);

            explosion = skin.GetAnimation(imageName, true, false, frameLength: frameLength)?.With(d =>
            {
                d.Origin = Anchor.Centre;
                d.Blending = BlendingParameters.Additive;
                d.Scale = new Vector2(explosionScale);
            });

            if (explosion != null)
                InternalChild = explosion;

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (explosion != null)
                explosion.Anchor = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        public void Animate(JudgementResult result)
        {
            (explosion as IFramedAnimation)?.GotoFrame(0);

            explosion?.FadeInFromZero(FadeInDuration)
                     .Then().FadeOut(120);
        }
    }
}
