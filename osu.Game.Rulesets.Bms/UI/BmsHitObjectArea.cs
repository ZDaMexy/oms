// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsHitObjectArea : Container
    {
        private readonly BindableDouble scrollLengthRatio = new BindableDouble(1);

        public IBindable<double> ScrollLengthRatio => scrollLengthRatio;

        public BmsHitTarget HitTarget { get; }

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Container content;

        private BmsPlayfieldLayoutProfile layoutProfile;

        public BmsHitObjectArea(BmsHitTarget hitTarget, BmsPlayfieldLayoutProfile layoutProfile, Drawable hitObjectContainer)
        {
            this.layoutProfile = layoutProfile;

            RelativeSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Child = hitObjectContainer,
                },
                HitTarget = hitTarget,
            });
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(_ => updateLayoutState(), true);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            double newRatio = DrawHeight > 0 ? content.DrawHeight / DrawHeight : 1;

            if (Math.Abs(scrollLengthRatio.Value - newRatio) > 0.0001)
                scrollLengthRatio.Value = newRatio;
        }

        public void ApplyLayoutProfile(BmsPlayfieldLayoutProfile layoutProfile)
        {
            this.layoutProfile = layoutProfile;
            HitTarget.ApplyLayoutProfile(layoutProfile);
            updateLayoutState();
        }

        private void updateLayoutState()
        {
            bool reverse = direction.Value == ScrollingDirection.Up;

            Padding = reverse
                ? new MarginPadding { Top = layoutProfile.HitTargetVerticalOffset }
                : new MarginPadding { Bottom = layoutProfile.HitTargetVerticalOffset };

            HitTarget.Anchor = HitTarget.Origin = reverse ? Anchor.TopLeft : Anchor.BottomLeft;
        }
    }
}
