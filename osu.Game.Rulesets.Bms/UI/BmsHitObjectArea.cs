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

        public Container PreviewContainer { get; }

        private readonly Container content;
        private readonly BmsHitObjectAreaLayoutController layoutController;

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public BmsHitObjectArea(
            BmsHitTarget hitTarget,
            BmsPlayfieldLayoutProfile layoutProfile,
            Drawable hitObjectContainer,
            BindableFloat? liftUnits = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            LayoutSnapshot = layoutSnapshot;
            HitTarget = hitTarget;
            layoutController = new BmsHitObjectAreaLayoutController(this, layoutProfile, liftUnits, hitTarget);

            RelativeSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        hitObjectContainer,
                        PreviewContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                },
                HitTarget,
            });
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            layoutController.Bind(scrollingInfo);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            layoutController.Update();

            double newRatio = DrawHeight > 0 ? content.DrawHeight / DrawHeight : 1;

            if (Math.Abs(scrollLengthRatio.Value - newRatio) > 0.0001)
                scrollLengthRatio.Value = newRatio;
        }

    }

    /// <summary>
    /// The sole runtime projection of the engine-owned hit-position offset shared by lane and group scrolling owners.
    /// It consumes the frozen layout profile plus the live Lift bindable; it does not solve another geometry model.
    /// </summary>
    internal sealed class BmsHitObjectAreaLayoutController
    {
        private readonly Container target;
        private readonly Drawable? anchoredTarget;
        private readonly BmsPlayfieldLayoutProfile layoutProfile;
        private readonly BindableFloat liftUnits = new BindableFloat();
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private float appliedOffset = float.NaN;
        private bool? appliedReverse;

        public BmsHitObjectAreaLayoutController(
            Container target,
            BmsPlayfieldLayoutProfile layoutProfile,
            BindableFloat? liftUnits = null,
            Drawable? anchoredTarget = null)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.layoutProfile = layoutProfile ?? throw new ArgumentNullException(nameof(layoutProfile));
            this.anchoredTarget = anchoredTarget;

            if (liftUnits != null)
                this.liftUnits.BindTo(liftUnits);
        }

        public void Bind(IScrollingInfo scrollingInfo)
        {
            ArgumentNullException.ThrowIfNull(scrollingInfo);
            direction.BindTo(scrollingInfo.Direction);
        }

        public void Update()
        {
            bool reverse = direction.Value == ScrollingDirection.Up;

            float availableHeight = Math.Max(0, target.DrawHeight);
            float liftOffset = availableHeight > 0 ? availableHeight * Math.Clamp(liftUnits.Value, 0, 1000) / 1000f : 0;
            float effectiveOffset = Math.Clamp(layoutProfile.HitTargetVerticalOffset + liftOffset, 0, availableHeight);

            if (Math.Abs(appliedOffset - effectiveOffset) <= 0.01f && appliedReverse == reverse)
                return;

            target.Padding = reverse
                ? new MarginPadding { Top = effectiveOffset }
                : new MarginPadding { Bottom = effectiveOffset };

            if (anchoredTarget != null)
                anchoredTarget.Anchor = anchoredTarget.Origin = reverse ? Anchor.TopLeft : Anchor.BottomLeft;

            appliedOffset = effectiveOffset;
            appliedReverse = reverse;
        }
    }
}
