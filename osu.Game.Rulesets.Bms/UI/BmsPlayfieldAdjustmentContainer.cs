// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        protected override Container<Drawable> Content { get; }

        internal Vector2 AppliedScale => adjustmentContainer.Scale;

        internal float AppliedHorizontalOffset => adjustmentContainer.X;

        private readonly Container adjustmentContainer;

        public BmsPlayfieldAdjustmentContainer()
        {
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

            adjustmentContainer.Scale = Vector2.One;
        }
    }
}
