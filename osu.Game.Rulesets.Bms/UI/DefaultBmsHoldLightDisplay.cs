// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DefaultBmsHoldLightDisplay : CompositeDrawable
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        private IBindable<bool>? holdSource;

        public DefaultBmsHoldLightDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            this.laneIndex = laneIndex;
            this.isScratch = isScratch;
            this.keymode = keymode;

            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            Color4 defaultColour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);

            string? texturePath = skinSource.GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.HoldLightImage, keymode, laneIndex, isScratch)?.Value;

            Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                BmsSkinConfigurationLookups.HoldLightColour, keymode)?.Value
                ?? defaultColour;

            if (!string.IsNullOrEmpty(texturePath))
            {
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                    Texture = skinSource.GetTexture(texturePath),
                };
            }
            else
            {
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour.Opacity(0.35f),
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var lane = Parent?.Parent as BmsLane;

            if (lane == null)
                return;

            holdSource = lane.AnyHolding.GetBoundCopy();
            holdSource.BindValueChanged(e => SetHolding(e.NewValue), true);
        }

        public void SetHolding(bool holding)
        {
            if (holding)
                this.FadeIn(80, Easing.OutQuint);
            else
                this.FadeOut(300, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            holdSource?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
