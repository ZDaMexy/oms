// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Carries a BMS skin configuration query: which keymode bucket, which logical key, and (for per-lane keys)
    /// which lane. Mirrors <see cref="osu.Game.Skinning.LegacyManiaSkinConfigurationLookup"/> but is keyed by
    /// <see cref="BmsKeymode"/> + lane/scratch instead of mania's total-column count, matching the existing
    /// <see cref="BmsLaneSkinLookup"/> / <see cref="BmsNoteSkinLookup"/> lane semantics.
    /// </summary>
    public class BmsSkinConfigurationLookup
    {
        /// <summary>The keymode bucket this lookup applies to (skin.ini sections are bucketed per keymode).</summary>
        public readonly BmsKeymode Keymode;

        /// <summary>The logical configuration key being looked up.</summary>
        public readonly BmsSkinConfigurationLookups Lookup;

        /// <summary>
        /// The lane this lookup targets for per-lane keys (texture slots). Null for global keys (geometry, note colour
        /// groups, stage art) that do not vary per lane.
        /// </summary>
        public readonly int? LaneIndex;

        /// <summary>Whether <see cref="LaneIndex"/> refers to the scratch lane (disambiguates per-lane key resolution).</summary>
        public readonly bool IsScratch;

        public BmsSkinConfigurationLookup(BmsKeymode keymode, BmsSkinConfigurationLookups lookup, int? laneIndex = null, bool isScratch = false)
        {
            Keymode = keymode;
            Lookup = lookup;
            LaneIndex = laneIndex;
            IsScratch = isScratch;
        }

        public override string ToString() => $"[{nameof(BmsSkinConfigurationLookup)} keymode:{Keymode} lookup:{Lookup} lane:{LaneIndex} scratch:{IsScratch}]";
    }
}
