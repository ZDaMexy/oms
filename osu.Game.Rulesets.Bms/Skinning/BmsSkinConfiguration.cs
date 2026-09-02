// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Parsed BMS skin configuration for a single keymode bucket (one <c>[Bms]</c> section of <c>skin.ini</c>),
    /// mirroring <see cref="Game.Skinning.LegacyManiaSkinConfiguration"/>. Only keys the author actually set are
    /// stored; anything absent falls back to the built-in programmatic default (<see cref="UI.BmsPlayfieldLayoutProfile"/> /
    /// <see cref="UI.BmsDefaultPlayfieldPalette"/>) at query time, per the fail-open contract.
    /// </summary>
    public class BmsSkinConfiguration
    {
        private readonly Dictionary<(GameplaySkinLaneResourceField Field, string LaneToken), string> acceptedLaneResources = new();
        private readonly Dictionary<BmsSkinConfigurationLookups, float> acceptedGeometry = new();

        public readonly BmsKeymode Keymode;

        /// <summary>Numeric geometry overrides (the geometry subset of <see cref="BmsSkinConfigurationLookups"/>).</summary>
        public readonly Dictionary<BmsSkinConfigurationLookups, float> Geometry = new Dictionary<BmsSkinConfigurationLookups, float>();

        /// <summary>Colour overrides (the colour subset of <see cref="BmsSkinConfigurationLookups"/>, e.g. IIDX note colour groups).</summary>
        public readonly Dictionary<BmsSkinConfigurationLookups, Color4> Colours = new Dictionary<BmsSkinConfigurationLookups, Color4>();

        /// <summary>
        /// Texture path overrides, keyed by the full ini key as written by the author (e.g. <c>NoteImage1</c>,
        /// <c>NoteImageSH</c>, <c>HitTargetImage</c>) — matching mania's per-name <c>ImageLookups</c>. Paths are stored
        /// as written (without extension); lane-specific keys preserve the decoder's raw <c>\d+</c>, <c>S</c> or
        /// <c>S2</c> token without normalisation.
        /// </summary>
        public readonly Dictionary<string, string> ImageLookups = new Dictionary<string, string>();

        public BmsSkinConfiguration(BmsKeymode keymode)
        {
            Keymode = keymode;
        }

        /// <summary>
        /// Captures one exact native BMS geometry declaration immediately after successful invariant float parsing.
        /// The public dictionary remains the production compatibility view; the private copy is decoder provenance.
        /// </summary>
        internal void AcceptGeometry(BmsSkinConfigurationLookups field, float value)
        {
            BmsGameplaySkinBucketGeometryFieldCatalog.Validate(field, nameof(field));

            Geometry[field] = value;
            acceptedGeometry[field] = value;
        }

        internal GameplaySkinConfigurationDeclaration<float> GetAcceptedGeometry(BmsSkinConfigurationLookups field)
        {
            BmsGameplaySkinBucketGeometryFieldCatalog.Validate(field, nameof(field));

            return acceptedGeometry.TryGetValue(field, out float value)
                ? GameplaySkinConfigurationDeclaration<float>.Declared(value)
                : GameplaySkinConfigurationDeclaration<float>.Absent;
        }

        /// <summary>
        /// Captures one exact native BMS colour declaration immediately after successful RGB/RGBA parsing.
        /// The public dictionary is the production legacy compatibility view; C4 has no separate process-local colour snapshot.
        /// </summary>
        internal void AcceptColour(BmsSkinConfigurationLookups field, Color4 value)
        {
            BmsGameplaySkinBucketColourFieldCatalog.Validate(field, nameof(field));

            Colours[field] = value;
        }

        /// <summary>
        /// Captures one of the six exact gameplay lane-resource declarations accepted by the native BMS decoder.
        /// The raw lane token and resource name are preserved without numeric normalisation or filename validation.
        /// </summary>
        internal void AcceptLaneResource(GameplaySkinLaneResourceField field, string laneToken, string resourceName)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(resourceName);
            validateLaneToken(laneToken);

            string sourceKey = LegacyManiaGameplaySkinLaneResourceSnapshotFactory.GetImageLookupKey(field, laneToken);

            ImageLookups[sourceKey] = resourceName;
            acceptedLaneResources[(field, laneToken)] = resourceName;
        }

        internal GameplaySkinConfigurationDeclaration<string> GetAcceptedLaneResource(
            GameplaySkinLaneResourceField field,
            string laneToken)
        {
            ArgumentNullException.ThrowIfNull(field);
            validateLaneToken(laneToken);

            // Also validates that callers use one of the six canonical shared lane-resource fields.
            LegacyManiaGameplaySkinLaneResourceSnapshotFactory.GetImageLookupKey(field, laneToken);

            return acceptedLaneResources.TryGetValue((field, laneToken), out string? resourceName)
                ? GameplaySkinConfigurationDeclaration<string>.Declared(resourceName)
                : GameplaySkinConfigurationDeclaration<string>.Absent;
        }

        private static void validateLaneToken(string laneToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(laneToken);

            if (laneToken is "S" or "S2")
                return;

            foreach (char character in laneToken)
            {
                if (!char.IsDigit(character))
                    throw new ArgumentException("A BMS lane resource token must contain only decoder-compatible digits, S or S2.", nameof(laneToken));
            }
        }
    }
}
