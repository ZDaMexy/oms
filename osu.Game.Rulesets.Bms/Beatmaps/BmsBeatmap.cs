// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Audio;
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

        /// <summary>
        /// Per-lane, time-ordered keysound assignments used to drive empty (note-less) key-press keysounds.
        /// Keyed by canonical lane index. Built at conversion time from visible notes, long-note head/tail
        /// keysounds and invisible (channel 31-49) keysound objects.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<BmsLaneKeysoundEntry>> LaneKeysoundTimelines { get; set; }
            = new Dictionary<int, IReadOnlyList<BmsLaneKeysoundEntry>>();

        public IReadOnlyList<BmsLaneKeysoundEntry> GetLaneKeysoundTimeline(int laneIndex)
            => LaneKeysoundTimelines.TryGetValue(laneIndex, out var timeline) ? timeline : Array.Empty<BmsLaneKeysoundEntry>();

        /// <summary>
        /// Landmine (channel D/E) visual placements. These are deliberately kept OUT of <see cref="Beatmap{T}.HitObjects"/>
        /// and are added straight to their lane like bar lines, so mines never touch the scoring / statistics / judged path.
        /// </summary>
        public IReadOnlyList<BmsMine> Mines { get; set; } = Array.Empty<BmsMine>();

        /// <summary>
        /// Pre-computed unclamped scroll distance D(t) for the BMS stop-motion visual bypass (P1-L Phase 2). Rendering-only
        /// data consumed by the gated BMS scroll algorithm; it never enters <see cref="Beatmap{T}.HitObjects"/> nor feeds
        /// judgement/scoring, which continue on the time-based path. Null until built by <see cref="BmsBeatmapConverter"/>.
        /// </summary>
        public BmsScrollProfile? ScrollProfile { get; set; }

        public void SetMeasureStartTimes(IEnumerable<double> startTimes)
        {
            measureStartTimes.Clear();
            measureStartTimes.AddRange(startTimes.Distinct().OrderBy(time => time));
        }

        public override IEnumerable<BeatmapStatistic> GetStatistics()
        {
            var filterStats = BeatmapInfo.Metadata.GetChartFilterStats();

            if (filterStats == null)
            {
                filterStats = BmsChartFilterStats.FromBeatmap(this);
                BeatmapInfo.Metadata.SetChartFilterStats(filterStats);
            }

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
