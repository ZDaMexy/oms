// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Resolves a single-fill BMS skin element (note family, lane background, divider, backdrop, …) to its visual,
    /// following the shared "asset skin = sprite, no asset = colour/palette fallback" layering: a user-skin texture
    /// (a <see cref="Sprite"/> that owns the look — no programmatic tint) takes precedence; otherwise a flat
    /// <see cref="Box"/> using the skin's colour override or the programmatic default.
    /// </summary>
    /// <remarks>
    /// Only covers elements whose fallback is a single box. Multi-element composites (hit target, lane cover) read the
    /// texture and branch themselves, since their colour fallback is a layered programmatic build, not one box.
    /// </remarks>
    internal static class BmsSkinnableVisual
    {
        /// <summary>
        /// Resolves the visual for a single-fill element.
        /// </summary>
        /// <param name="skin">The skin to query (any skin in the fallback chain is safe — a missing override returns the default).</param>
        /// <param name="imageLookup">The texture slot to check first.</param>
        /// <param name="colourLookup">The colour slot used by the box fallback when no texture is supplied.</param>
        /// <param name="keymode">The keymode bucket.</param>
        /// <param name="defaultColour">The programmatic colour used when the skin overrides neither texture nor colour.</param>
        /// <param name="hasTexture">True when a texture was resolved (the sprite owns the look, no programmatic tint).</param>
        /// <param name="laneIndex">For per-lane texture slots, the lane index; null for global slots.</param>
        /// <param name="isScratch">For per-lane texture slots, whether the lane is the scratch lane.</param>
        public static Drawable Resolve(ISkin skin, BmsSkinConfigurationLookups imageLookup, BmsSkinConfigurationLookups colourLookup,
                                       BmsKeymode keymode, Color4 defaultColour, out bool hasTexture, int? laneIndex = null, bool isScratch = false)
        {
            string? imagePath = skin.GetBmsSkinConfig<string>(imageLookup, keymode, laneIndex, isScratch)?.Value;
            var texture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            if (texture != null)
            {
                hasTexture = true;
                return new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture };
            }

            hasTexture = false;
            var colour = skin.GetBmsSkinConfig<Color4>(colourLookup, keymode)?.Value ?? defaultColour;
            return new Box { RelativeSizeAxes = Axes.Both, Colour = colour };
        }
    }
}
