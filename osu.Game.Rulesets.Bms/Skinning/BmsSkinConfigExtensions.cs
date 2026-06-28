// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Skinning
{
    public static class BmsSkinConfigExtensions
    {
        /// <summary>
        /// Retrieve a per-keymode BMS skin configuration value (geometry <see cref="float"/> / colour / texture path),
        /// mirroring mania's <c>GetManiaSkinConfig</c>. Returns null when the active skin does not override the key
        /// (callers fall back to the programmatic default), so this is safe against any skin in the fallback chain.
        /// </summary>
        /// <param name="skin">The skin to query.</param>
        /// <param name="lookup">The logical key to retrieve.</param>
        /// <param name="keymode">The keymode bucket.</param>
        /// <param name="laneIndex">For per-lane texture keys, the lane index; null for global keys.</param>
        /// <param name="isScratch">For per-lane texture keys, whether the lane is the scratch lane.</param>
        public static IBindable<T>? GetBmsSkinConfig<T>(this ISkin skin, BmsSkinConfigurationLookups lookup, BmsKeymode keymode, int? laneIndex = null, bool isScratch = false)
            where T : notnull
            => skin.GetConfig<BmsSkinConfigurationLookup, T>(new BmsSkinConfigurationLookup(keymode, lookup, laneIndex, isScratch));
    }
}
