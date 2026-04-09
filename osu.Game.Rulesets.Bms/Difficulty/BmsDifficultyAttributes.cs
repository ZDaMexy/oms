// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Bms.Difficulty
{
    public class BmsDifficultyAttributes : DifficultyAttributes
    {
        private const int attrib_id_total_note_count = 1001;
        private const int attrib_id_scratch_note_count = 1003;
        private const int attrib_id_ln_note_count = 1005;
        private const int attrib_id_peak_density_nps = 1007;
        private const int attrib_id_peak_density_ms = 1009;

        [JsonProperty("total_note_count")]
        public int TotalNoteCount { get; set; }

        [JsonProperty("scratch_note_count")]
        public int ScratchNoteCount { get; set; }

        [JsonProperty("ln_note_count")]
        public int LnNoteCount { get; set; }

        [JsonProperty("peak_density_nps")]
        public double PeakDensityNps { get; set; }

        [JsonProperty("peak_density_ms")]
        public double PeakDensityMs { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var value in base.ToDatabaseAttributes())
                yield return value;

            yield return (ATTRIB_ID_DIFFICULTY, StarRating);
            yield return (attrib_id_total_note_count, TotalNoteCount);
            yield return (attrib_id_scratch_note_count, ScratchNoteCount);
            yield return (attrib_id_ln_note_count, LnNoteCount);
            yield return (attrib_id_peak_density_nps, PeakDensityNps);
            yield return (attrib_id_peak_density_ms, PeakDensityMs);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            if (values.TryGetValue(ATTRIB_ID_DIFFICULTY, out double starRating))
                StarRating = starRating;

            if (values.TryGetValue(attrib_id_total_note_count, out double totalNoteCount))
                TotalNoteCount = (int)totalNoteCount;

            if (values.TryGetValue(attrib_id_scratch_note_count, out double scratchNoteCount))
                ScratchNoteCount = (int)scratchNoteCount;

            if (values.TryGetValue(attrib_id_ln_note_count, out double lnNoteCount))
                LnNoteCount = (int)lnNoteCount;

            if (values.TryGetValue(attrib_id_peak_density_nps, out double peakDensityNps))
                PeakDensityNps = peakDensityNps;

            if (values.TryGetValue(attrib_id_peak_density_ms, out double peakDensityMs))
                PeakDensityMs = peakDensityMs;
        }
    }
}
