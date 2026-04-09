// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        protected override Container<Drawable> Content { get; }

        internal Vector2 AppliedScale => adjustmentContainer.Scale;

        internal float AppliedHorizontalOffset => adjustmentContainer.X;

        private readonly Bindable<double>? externalPlayfieldScale;
        private readonly Bindable<double>? externalPlayfieldHorizontalOffset;

        private readonly BindableDouble playfieldScale = new BindableDouble
        {
            Default = 1,
        };

        private readonly BindableDouble playfieldHorizontalOffset = new BindableDouble();

        private readonly Container adjustmentContainer;

        public BmsPlayfieldAdjustmentContainer(Bindable<double>? playfieldScale = null, Bindable<double>? playfieldHorizontalOffset = null)
        {
            externalPlayfieldScale = playfieldScale;
            externalPlayfieldHorizontalOffset = playfieldHorizontalOffset;

            InternalChild = adjustmentContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.Both,
                Child = Content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };

            if (externalPlayfieldScale != null)
                this.playfieldScale.BindTo(externalPlayfieldScale);

            if (externalPlayfieldHorizontalOffset != null)
                this.playfieldHorizontalOffset.BindTo(externalPlayfieldHorizontalOffset);

            this.playfieldScale.BindValueChanged(_ => updateAdjustment(), true);
            this.playfieldHorizontalOffset.BindValueChanged(_ => updateAdjustment(), true);
        }

        [BackgroundDependencyLoader]
        private void load(BmsRulesetConfigManager config)
        {
            if (externalPlayfieldScale == null)
                config.BindWith(BmsRulesetSetting.PlayfieldScale, playfieldScale);

            if (externalPlayfieldHorizontalOffset == null)
                config.BindWith(BmsRulesetSetting.PlayfieldHorizontalOffset, playfieldHorizontalOffset);
        }

        private void updateAdjustment()
        {
            adjustmentContainer.Scale = new Vector2((float)playfieldScale.Value);
            adjustmentContainer.X = (float)playfieldHorizontalOffset.Value;
        }
    }
}
