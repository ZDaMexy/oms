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
        private readonly BindableFloat liftUnits = new BindableFloat();

        public IBindable<double> ScrollLengthRatio => scrollLengthRatio;

        public BmsHitTarget HitTarget { get; }

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Container content;

        private BmsPlayfieldLayoutProfile layoutProfile;
        private float appliedOffset = float.NaN;
        private bool? appliedReverse;

        public BmsHitObjectArea(BmsHitTarget hitTarget, BmsPlayfieldLayoutProfile layoutProfile, Drawable hitObjectContainer, BindableFloat? liftUnits = null)
        {
            this.layoutProfile = layoutProfile;

            if (liftUnits != null)
                this.liftUnits.BindTo(liftUnits);

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
            liftUnits.BindValueChanged(_ => updateLayoutState(), true);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updateLayoutState();

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

            float availableHeight = Math.Max(0, DrawHeight);
            float liftOffset = availableHeight > 0 ? availableHeight * Math.Clamp(liftUnits.Value, 0, 1000) / 1000f : 0;
            float effectiveOffset = Math.Clamp(layoutProfile.HitTargetVerticalOffset + liftOffset, 0, availableHeight);

            if (Math.Abs(appliedOffset - effectiveOffset) <= 0.01f && appliedReverse == reverse)
                return;

            Padding = reverse
                ? new MarginPadding { Top = effectiveOffset }
                : new MarginPadding { Bottom = effectiveOffset };

            HitTarget.Anchor = HitTarget.Origin = reverse ? Anchor.TopLeft : Anchor.BottomLeft;
            appliedOffset = effectiveOffset;
            appliedReverse = reverse;
        }
    }
}
