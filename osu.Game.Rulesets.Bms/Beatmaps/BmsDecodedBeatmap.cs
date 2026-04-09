// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Lightweight wrapper used to pass decoded BMS chart data through the ruleset conversion pipeline.
    /// </summary>
    public class BmsDecodedBeatmap : Beatmap
    {
        public BmsDecodedChart DecodedChart { get; }

        public BmsDecodedBeatmap(BmsDecodedChart decodedChart)
        {
            DecodedChart = decodedChart ?? throw new ArgumentNullException(nameof(decodedChart));
        }
    }
}
