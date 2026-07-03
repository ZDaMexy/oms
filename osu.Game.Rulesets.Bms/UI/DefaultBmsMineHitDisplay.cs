// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
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
    public partial class DefaultBmsMineHitDisplay : CompositeDrawable
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        public DefaultBmsMineHitDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
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
            Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                BmsSkinConfigurationLookups.MineHitColour, keymode)?.Value
                ?? BmsDefaultPlayfieldPalette.ScratchNote;

            string? texturePath = skinSource.GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.MineHitImage, keymode, laneIndex, isScratch)?.Value;

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
                    Colour = colour.Opacity(0.5f),
                };
            }
        }

        public void Flash()
        {
            this.FadeIn(15, Easing.OutQuint)
                .Then()
                .FadeOut(400, Easing.OutQuint);
        }
    }
}
