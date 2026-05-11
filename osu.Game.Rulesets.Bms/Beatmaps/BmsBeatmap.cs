// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Objects;
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

        public override IEnumerable<BeatmapStatistic> GetStatistics()
        {
            var filterStats = BmsChartFilterStats.FromBeatmap(this);
            int sum = Math.Max(1, filterStats.TotalPlayableObjectCount);

            return new[]
            {
                createStatistic(BeatmapStatisticStrings.Notes, BeatmapStatisticsIconType.Circles, filterStats.RegularNoteCount, sum),
                createStatistic(BeatmapStatisticStrings.HoldNotes, BeatmapStatisticsIconType.Sliders, filterStats.LongNoteCount, sum),
                createStatistic(BeatmapStatisticStrings.Spinners, BeatmapStatisticsIconType.Spinners, filterStats.ScratchNoteCount, sum),
            };
        }

        private static BeatmapStatistic createStatistic(osu.Framework.Localisation.LocalisableString name, BeatmapStatisticsIconType iconType, int count, int total)
            => new BeatmapStatistic
            {
                Name = name,
                CreateIcon = () => new BeatmapStatisticIcon(iconType),
                Content = $"{count.ToString(CultureInfo.InvariantCulture)} ({formatPercentage(count, total)})",
                BarDisplayLength = count / (float)Math.Max(1, total),
            };

        private static string formatPercentage(int count, int total)
            => total == 0
                ? "0.0%"
                : $"{count * 100d / total:0.0}%";
    }
}
