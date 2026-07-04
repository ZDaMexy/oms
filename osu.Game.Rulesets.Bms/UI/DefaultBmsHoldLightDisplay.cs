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
    /// <summary>
    /// Interface for the hold-light (LN hold sustained glow) F2 display. The lane pushes holding-state through this
    /// interface; custom skins implement it to receive hold state without hardcoding parent traversal.
    /// </summary>
    public interface IBmsHoldLightDisplay
    {
        void SetHolding(bool holding);
    }

    public partial class DefaultBmsHoldLightDisplay : CompositeDrawable, IBmsHoldLightDisplay
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

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
                Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                    BmsSkinConfigurationLookups.HoldLightColour, keymode)?.Value
                    ?? defaultColour;

                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour.Opacity(0.35f),
                };
            }
        }

        public void SetHolding(bool holding)
        {
            if (holding)
                this.FadeIn(80, Easing.OutQuint);
            else
                this.FadeOut(300, Easing.OutQuint);
        }
    }
}
