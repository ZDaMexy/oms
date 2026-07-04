// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Interface for the keyflash (column light) F2 display. The lane pushes pressed-state through this interface;
    /// custom skins implement it to receive key-press state without hardcoding parent traversal.
    /// </summary>
    public interface IBmsKeyFlashDisplay
    {
        void SetPressed(bool pressed);
    }

    public partial class DefaultBmsKeyFlashDisplay : CompositeDrawable, IBmsKeyFlashDisplay
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        private Sprite? keyImageSprite;
        private Texture? keyImageTexture;
        private Texture? keyImageDownTexture;

        public DefaultBmsKeyFlashDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            this.laneIndex = laneIndex;
            this.isScratch = isScratch;
            this.keymode = keymode;

            // Occupies the bottom half of the lane, centred horizontally.
            RelativeSizeAxes = Axes.X;
            Height = 0.5f;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            Color4 defaultColour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);

            string? texturePath = skinSource.GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.KeyFlashImage, keymode, laneIndex, isScratch)?.Value;

            if (!string.IsNullOrEmpty(texturePath))
            {
                // File-skin path: a full-lane sprite (the texture itself provides edge fade / shape).
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Stretch,
                    Texture = skinSource.GetTexture(texturePath),
                };
            }
            else
            {
                // KeyImage/KeyImageDown alternative route: gives skin authors a "key area" visual that swaps
                // texture on press, without needing a separate key-area component. Fills the full lane (always
                // visible), unlike the programmatic default which only occupies the bottom half and fades.
                string? keyImagePath = skinSource.GetBmsSkinConfig<string>(
                    BmsSkinConfigurationLookups.KeyImage, keymode, laneIndex, isScratch)?.Value;

                if (!string.IsNullOrEmpty(keyImagePath))
                {
                    keyImageTexture = skinSource.GetTexture(keyImagePath);

                    if (keyImageTexture != null)
                    {
                        string? keyImageDownPath = skinSource.GetBmsSkinConfig<string>(
                            BmsSkinConfigurationLookups.KeyImageDown, keymode, laneIndex, isScratch)?.Value;

                        keyImageDownTexture = !string.IsNullOrEmpty(keyImageDownPath)
                            ? skinSource.GetTexture(keyImageDownPath)
                            : null;

                        // KeyImage route: fill the full lane area, always visible.
                        RelativeSizeAxes = Axes.Both;
                        Anchor = Anchor.Centre;
                        Origin = Anchor.Centre;
                        Alpha = 1;

                        keyImageSprite = new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Stretch,
                            Texture = keyImageTexture,
                        };
                        InternalChild = keyImageSprite;
                        return;
                    }
                }

                // Programmatic default: layered vertical strips at each lane edge, stepping from
                // wide-dim → narrow-bright, giving a rough glow-fade from edge to centre.
                Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                    BmsSkinConfigurationLookups.KeyFlashColour, keymode)?.Value
                    ?? defaultColour;

                InternalChildren = new Drawable[]
                {
                    // Left edge — three stacked strips, inner to outer
                    new Box { RelativeSizeAxes = Axes.Y, Width = 4, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.6f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 10, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.2f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 20, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.06f) },
                    // Right edge
                    new Box { RelativeSizeAxes = Axes.Y, Width = 4, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.6f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 10, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.2f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 20, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.06f) },
                };
            }
        }

        public void SetPressed(bool pressed)
        {
            if (keyImageSprite != null)
            {
                // KeyImage route: swap textures on press (always visible, no fade).
                keyImageSprite.Texture = pressed && keyImageDownTexture != null
                    ? keyImageDownTexture
                    : keyImageTexture;
            }
            else
            {
                // Programmatic default: fade in/out.
                if (pressed)
                    this.FadeIn(40, Easing.OutQuint);
                else
                    this.FadeOut(250, Easing.OutQuint);
            }
        }
    }
}
