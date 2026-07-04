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
    /// Interface for the hit-lighting (hit explosion flash) F2 display. The lane triggers Flash() through this interface
    /// on note judgement; custom skins implement it to receive hit events without concrete-type casting.
    /// </summary>
    public interface IBmsHitLightingDisplay
    {
        void Flash();
    }

    public partial class DefaultBmsHitLightingDisplay : CompositeDrawable, IBmsHitLightingDisplay
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        public DefaultBmsHitLightingDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
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
                BmsSkinConfigurationLookups.HitLightingImage, keymode, laneIndex, isScratch)?.Value;

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
                    BmsSkinConfigurationLookups.HitLightingColour, keymode)?.Value
                    ?? defaultColour;

                var box = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour.Opacity(0.6f),
                };
                InternalChild = box;
            }
        }

        /// <summary>
        /// Trigger a brief flash animation simulating a hit explosion on this lane.
        /// Called from the hit-object judgement pipeline.
        /// </summary>
        public void Flash()
        {
            this.FadeIn(20, Easing.OutQuint)
                .Then()
                .FadeOut(200, Easing.OutQuint);
        }
    }
}
