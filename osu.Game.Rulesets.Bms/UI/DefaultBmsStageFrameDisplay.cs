// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Default display for the IIDX-style stage frame elements (StageLeft / StageRight / StageBottom / StageHint).
    /// These are purely decorative — no programmatic fallback. When no texture is provided the display is invisible.
    /// When a texture is provided it is shown as a sprite positioned according to the element type.
    /// </summary>
    public partial class DefaultBmsStageFrameDisplay : CompositeDrawable
    {
        private readonly BmsPlayfieldSkinElements element;
        private readonly BmsKeymode keymode;

        public DefaultBmsStageFrameDisplay(BmsPlayfieldSkinElements element, BmsKeymode keymode)
        {
            this.element = element;
            this.keymode = keymode;

            // Default to invisible; only shown when a texture is provided.
            Alpha = 0;

            // Positioning defaults per element type — the texture itself provides the visual.
            switch (element)
            {
                case BmsPlayfieldSkinElements.StageLeft:
                    RelativeSizeAxes = Axes.Y;
                    AutoSizeAxes = Axes.X;
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;
                    break;

                case BmsPlayfieldSkinElements.StageRight:
                    RelativeSizeAxes = Axes.Y;
                    AutoSizeAxes = Axes.X;
                    Anchor = Anchor.CentreRight;
                    Origin = Anchor.CentreRight;
                    break;

                case BmsPlayfieldSkinElements.StageBottom:
                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.Y;
                    Anchor = Anchor.BottomCentre;
                    Origin = Anchor.BottomCentre;
                    break;

                case BmsPlayfieldSkinElements.StageHint:
                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.Y;
                    Anchor = Anchor.BottomCentre;
                    Origin = Anchor.BottomCentre;
                    break;
            }
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            var lookup = element switch
            {
                BmsPlayfieldSkinElements.StageLeft => BmsSkinConfigurationLookups.StageLeftImage,
                BmsPlayfieldSkinElements.StageRight => BmsSkinConfigurationLookups.StageRightImage,
                BmsPlayfieldSkinElements.StageBottom => BmsSkinConfigurationLookups.StageBottomImage,
                BmsPlayfieldSkinElements.StageHint => BmsSkinConfigurationLookups.StageHintImage,
                _ => BmsSkinConfigurationLookups.StageLeftImage,
            };

            string? texturePath = skinSource.GetBmsSkinConfig<string>(lookup, keymode)?.Value;

            if (string.IsNullOrEmpty(texturePath))
                return;

            var texture = skinSource.GetTexture(texturePath);

            if (texture == null)
                return;

            // The sprite's size is driven by the texture's aspect ratio relative to the anchoring axis.
            // For left/right (Y-relative): height fills the playfield, width follows aspect ratio.
            // For bottom/hint (X-relative): width fills the playfield, height follows aspect ratio.
            InternalChild = new Sprite
            {
                RelativeSizeAxes = RelativeSizeAxes,
                FillAspectRatio = (float)texture.Width / texture.Height,
                FillMode = FillMode.Fill,
                Texture = texture,
            };

            Alpha = 1;
        }
    }
}
