// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Lightweight wrapper used to pass decoded BMS chart data through the ruleset conversion pipeline.
    /// </summary>
    public class BmsDecodedBeatmap : Beatmap, ICachedModlessPlayableBeatmapSource
    {
        private readonly Dictionary<string, IBeatmap> cachedModlessPlayableBeatmaps = new Dictionary<string, IBeatmap>(StringComparer.Ordinal);

        public BmsDecodedChart DecodedChart { get; }

        public BmsDecodedBeatmap(BmsDecodedChart decodedChart)
        {
            DecodedChart = decodedChart ?? throw new ArgumentNullException(nameof(decodedChart));
        }

        public bool TryGetCachedModlessPlayableBeatmap(IRulesetInfo ruleset, out IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            if (cachedModlessPlayableBeatmaps.TryGetValue(ruleset.ShortName, out var cachedBeatmap))
            {
                beatmap = cachedBeatmap;
                return true;
            }

            beatmap = null!;
            return false;
        }

        public void CacheModlessPlayableBeatmap(IRulesetInfo ruleset, IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(ruleset);
            ArgumentNullException.ThrowIfNull(beatmap);

            cachedModlessPlayableBeatmaps[ruleset.ShortName] = beatmap;
        }
    }
}
