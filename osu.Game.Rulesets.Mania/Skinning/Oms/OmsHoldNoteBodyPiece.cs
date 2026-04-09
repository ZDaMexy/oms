// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHoldNoteBodyPiece : OmsManiaColumnElement
    {
        private DrawableHoldNote holdNote = null!;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly IBindable<bool> isHitting = new Bindable<bool>();
        private readonly Bindable<double?> missFadeTime = new Bindable<double?>();

        private Drawable? bodySprite;

        private Drawable? lightContainer;

        private Drawable? light;
        private LegacyNoteBodyStyle? bodyStyle;

        public OmsHoldNoteBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, DrawableHitObject drawableObject)
        {
            holdNote = (DrawableHoldNote)drawableObject;

            string imageName = GetColumnSkinConfig<string>(skin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage)?.Value
                               ?? $"mania-note{FallbackColumnIndex}L";

            string lightImage = GetColumnSkinConfig<string>(skin, LegacyManiaSkinConfigurationLookups.HoldNoteLightImage)?.Value
                                ?? "lightingL";

            float lightScale = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.HoldNoteLightScale)?.Value
                               ?? 1;

            var tmp = skin.GetAnimation(lightImage, true, false);
            double frameLength = 0;

            if (tmp is IFramedAnimation tmpAnimation && tmpAnimation.FrameCount > 0)
                frameLength = Math.Max(1000 / 60.0, 170.0 / tmpAnimation.FrameCount);

            light = skin.GetAnimation(lightImage, true, true, frameLength: frameLength)?.With(d =>
            {
                d.Origin = Anchor.Centre;
                d.Blending = BlendingParameters.Additive;
                d.Scale = new Vector2(lightScale);
            });

            if (light != null)
            {
                lightContainer = new HitTargetInsetContainer
                {
                    Alpha = 0,
                    Child = light
                };
            }

            bodyStyle = skin.GetConfig<ManiaSkinConfigurationLookup, LegacyNoteBodyStyle>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.NoteBodyStyle))?.Value;

            var wrapMode = bodyStyle == LegacyNoteBodyStyle.Stretch ? WrapMode.ClampToEdge : WrapMode.Repeat;

            direction.BindTo(scrollingInfo.Direction);
            isHitting.BindTo(holdNote.IsHolding);

            bodySprite = skin.GetAnimation(imageName, wrapMode, wrapMode, true, true, frameLength: 30)?.With(d =>
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
            missFadeTime.BindValueChanged(onMissFadeTimeChanged, true);

            holdNote.ApplyCustomUpdateState += applyCustomUpdateState;
            applyCustomUpdateState(holdNote, holdNote.State.Value);
        }

        private void applyCustomUpdateState(DrawableHitObject hitObject, ArmedState state)
        {
            switch (hitObject)
            {
                case DrawableHoldNoteTail:
                case DrawableHoldNoteHead:
                case DrawableHoldNoteBody:
                    if (state == ArmedState.Miss)
                        missFadeTime.Value ??= hitObject.HitStateUpdateTime;

                    break;
            }
        }

        private void onIsHittingChanged(ValueChangedEvent<bool> isHitting)
        {
            if (bodySprite is TextureAnimation bodyAnimation)
                bodyAnimation.IsPlaying = isHitting.NewValue;

            if (lightContainer == null)
                return;

            if (isHitting.NewValue)
            {
                lightContainer.ClearTransforms();

                if (lightContainer.Parent == null)
                    Column.TopLevelContainer.Add(lightContainer);

                if (light is TextureAnimation lightAnimation)
                    lightAnimation.GotoFrame(0);

                lightContainer.FadeIn(80);
            }
            else
            {
                lightContainer.FadeOut(120)
                              .OnComplete(d => Column.TopLevelContainer.Remove(d, false));
            }
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

                if (light != null)
                    light.Anchor = Anchor.TopCentre;
            }
            else
            {
                if (bodySprite != null)
                {
                    bodySprite.Origin = Anchor.TopCentre;
                    bodySprite.Anchor = Anchor.TopCentre;
                }

                if (light != null)
                    light.Anchor = Anchor.BottomCentre;
            }
        }

        private void onMissFadeTimeChanged(ValueChangedEvent<double?> missFadeTimeChange)
        {
            if (missFadeTimeChange.NewValue == null)
                return;

            using (BeginAbsoluteSequence(missFadeTimeChange.NewValue.Value))
            {
                const double fade_duration = 60;

                holdNote.Head.FadeColour(Colour4.DarkGray, fade_duration);
                holdNote.Tail.FadeColour(Colour4.DarkGray, fade_duration);
                bodySprite?.FadeColour(Colour4.DarkGray, fade_duration);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!isHitting.Value)
                (bodySprite as TextureAnimation)?.GotoFrame(0);

            if (holdNote.Body.HasHoldBreak)
                missFadeTime.Value = holdNote.Body.Result.TimeAbsolute;

            int scaleDirection = direction.Value == ScrollingDirection.Down ? 1 : -1;

            switch (bodyStyle)
            {
                case LegacyNoteBodyStyle.Stretch:
                    if (bodySprite != null)
                        bodySprite.Scale = new Vector2(1, scaleDirection);
                    break;

                default:
                    if (bodySprite != null)
                    {
                        var sprite = bodySprite as Sprite ?? bodySprite.ChildrenOfType<Sprite>().Single();

                        bodySprite.FillMode = FillMode.Stretch;

                        if (sprite.DrawHeight > 0)
                            bodySprite.Scale = new Vector2(1, scaleDirection * MathF.Max(1, 32800 / sprite.DrawHeight));
                    }

                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (holdNote.IsNotNull())
                holdNote.ApplyCustomUpdateState -= applyCustomUpdateState;

            lightContainer?.Expire();
        }
    }
}