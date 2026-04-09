// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Runtime beatmap produced from decoded BMS chart data.
    /// </summary>
    public class BmsBeatmap : Beatmap<HitObject>
    {
        private readonly List<double> measureStartTimes = new List<double>();

        public BmsBeatmapInfo BmsInfo { get; set; } = new BmsBeatmapInfo();

        public IReadOnlyList<double> MeasureStartTimes => measureStartTimes;

        public void SetMeasureStartTimes(IEnumerable<double> startTimes)
        {
            measureStartTimes.Clear();
            measureStartTimes.AddRange(startTimes.Distinct().OrderBy(time => time));
        }
    }
}
