// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsNotePiece : OmsManiaColumnElement
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;

        private Drawable noteAnimation = null!;

        private float? noteHeightReferenceWidth;

        public OmsNotePiece()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            noteHeightReferenceWidth = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale)?.Value;

            InternalChild = directionContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = noteAnimation = GetAnimation(skin) ?? Empty()
            };

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected override void Update()
        {
            base.Update();

            Texture? texture = null;

            if (noteAnimation is Sprite sprite)
                texture = sprite.Texture;
            else if (noteAnimation is TextureAnimation textureAnimation && textureAnimation.FrameCount > 0)
                texture = textureAnimation.CurrentFrame;

            if (texture != null)
            {
                float noteHeight = noteHeightReferenceWidth ?? DrawWidth;
                noteAnimation.Scale = Vector2.Divide(new Vector2(DrawWidth, noteHeight), texture.DisplayWidth);
            }
        }

        protected virtual void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
            => applyDisplayState(GetDisplayDirection(direction.NewValue));

        private void applyDisplayState(ScrollingDirection displayDirection)
        {
            directionContainer.Anchor = GetDisplayAnchor(displayDirection);
            directionContainer.Origin = GetDisplayOrigin(displayDirection);
            directionContainer.Scale = GetDisplayScale(displayDirection);
        }

        protected virtual ScrollingDirection GetDisplayDirection(ScrollingDirection direction) => direction;

        protected virtual Anchor GetDisplayAnchor(ScrollingDirection direction) => direction == ScrollingDirection.Up
            ? Anchor.TopCentre
            : Anchor.BottomCentre;

        protected virtual Anchor GetDisplayOrigin(ScrollingDirection direction) => Anchor.BottomCentre;

        protected virtual Vector2 GetDisplayScale(ScrollingDirection direction) => direction == ScrollingDirection.Up
            ? new Vector2(1, -1)
            : Vector2.One;

        protected virtual Drawable? GetAnimation(ISkinSource skin) => GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.NoteImage);

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
        {
            string suffix = string.Empty;

            switch (lookup)
            {
                case LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage:
                    suffix = "H";
                    break;

                case LegacyManiaSkinConfigurationLookups.HoldNoteTailImage:
                    suffix = "T";
                    break;
            }

            string noteImage = GetColumnSkinConfig<string>(skin, lookup)?.Value
                               ?? $"mania-note{FallbackColumnIndex}{suffix}";

            return skin.GetAnimation(noteImage, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);
        }
    }
}
