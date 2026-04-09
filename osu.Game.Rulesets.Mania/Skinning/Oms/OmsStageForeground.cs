// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsStageForeground : CompositeDrawable
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Drawable? sprite;

        public OmsStageForeground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            string bottomImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.BottomStageImage)?.Value
                                 ?? "mania-stage-bottom";

            sprite = skin.GetAnimation(bottomImage, true, true);

            if (sprite != null)
            {
                sprite.Scale = new Vector2(1.6f);
                InternalChild = sprite;
            }

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (sprite == null)
                return;

            if (direction.NewValue == ScrollingDirection.Up)
                sprite.Anchor = sprite.Origin = Anchor.TopCentre;
            else
                sprite.Anchor = sprite.Origin = Anchor.BottomCentre;
        }
    }
}