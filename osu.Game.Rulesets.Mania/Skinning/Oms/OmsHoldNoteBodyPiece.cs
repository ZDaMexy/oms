// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHoldNoteBodyPiece : OmsManiaColumnElement
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly IBindable<bool> isHitting = new Bindable<bool>();

        private Drawable? bodySprite;

        public OmsHoldNoteBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, DrawableHitObject drawableObject)
        {
            var holdNote = (DrawableHoldNote)drawableObject;

            string imageName = GetColumnSkinConfig<string>(skin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage)?.Value
                               ?? $"mania-note{FallbackColumnIndex}L";

            direction.BindTo(scrollingInfo.Direction);
            isHitting.BindTo(holdNote.IsHolding);

            bodySprite = skin.GetAnimation(imageName, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true, frameLength: 30)?.With(d =>
            {
                if (d is TextureAnimation animation)
                    animation.IsPlaying = false;

                d.Anchor = Anchor.TopCentre;
                d.RelativeSizeAxes = Axes.Both;
                d.Size = Vector2.One;
            });

            if (bodySprite != null)
                InternalChild = bodySprite;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            direction.BindValueChanged(onDirectionChanged, true);
            isHitting.BindValueChanged(onIsHittingChanged, true);
        }

        private void onIsHittingChanged(ValueChangedEvent<bool> isHitting)
        {
            if (bodySprite is TextureAnimation bodyAnimation)
                bodyAnimation.IsPlaying = isHitting.NewValue;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                if (bodySprite != null)
                {
                    bodySprite.Origin = Anchor.TopCentre;
                    bodySprite.Anchor = Anchor.BottomCentre;
                }
            }
            else
            {
                if (bodySprite != null)
                {
                    bodySprite.Origin = Anchor.TopCentre;
                    bodySprite.Anchor = Anchor.TopCentre;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!isHitting.Value)
                (bodySprite as TextureAnimation)?.GotoFrame(0);

            int scaleDirection = direction.Value == ScrollingDirection.Down ? 1 : -1;

            if (bodySprite != null)
                bodySprite.Scale = new Vector2(1, scaleDirection);
        }
    }
}
