// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Parsed BMS skin configuration for a single keymode bucket (one <c>[Bms]</c> section of <c>skin.ini</c>),
    /// mirroring <see cref="osu.Game.Skinning.LegacyManiaSkinConfiguration"/>. Only keys the author actually set are
    /// stored; anything absent falls back to the built-in programmatic default (<see cref="UI.BmsPlayfieldLayoutProfile"/> /
    /// <see cref="UI.BmsDefaultPlayfieldPalette"/>) at query time, per the fail-open contract.
    /// </summary>
    public class BmsSkinConfiguration
    {
        public readonly BmsKeymode Keymode;

        /// <summary>Numeric geometry overrides (the geometry subset of <see cref="BmsSkinConfigurationLookups"/>).</summary>
        public readonly Dictionary<BmsSkinConfigurationLookups, float> Geometry = new Dictionary<BmsSkinConfigurationLookups, float>();

        /// <summary>Colour overrides (the colour subset of <see cref="BmsSkinConfigurationLookups"/>, e.g. IIDX note colour groups).</summary>
        public readonly Dictionary<BmsSkinConfigurationLookups, Color4> Colours = new Dictionary<BmsSkinConfigurationLookups, Color4>();

        /// <summary>
        /// Texture path overrides, keyed by the full ini key as written by the author (e.g. <c>NoteImage1</c>,
        /// <c>NoteImageSH</c>, <c>HitTargetImage</c>) — matching mania's per-name <c>ImageLookups</c>. Paths are stored
        /// as written (without extension); lane-specific keys embed the lane token (a digit, or <c>S</c> for scratch).
        /// </summary>
        public readonly Dictionary<string, string> ImageLookups = new Dictionary<string, string>();

        public BmsSkinConfiguration(BmsKeymode keymode)
        {
            Keymode = keymode;
        }
    }
}
