// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using Newtonsoft.Json;

namespace osu.Game.Beatmaps
{
    internal static class BmsPersistedMetadataResolver
    {
        public static BmsPersistedChartMetadata? GetChartMetadata(BeatmapMetadata? metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.RulesetDataJson))
                return null;

            try
            {
                return metadata.GetRulesetData<BmsPersistedMetadataData>()?.ChartMetadata;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    internal class BmsPersistedMetadataData
    {
        [JsonProperty("chart_metadata")]
        public BmsPersistedChartMetadata? ChartMetadata { get; set; }
    }

    internal class BmsPersistedChartMetadata
    {
        [JsonProperty("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        [JsonProperty("sub_artist")]
        public string SubArtist { get; set; } = string.Empty;

        [JsonProperty("comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("genre")]
        public string Genre { get; set; } = string.Empty;

        [JsonProperty("play_level")]
        public string PlayLevel { get; set; } = string.Empty;

        [JsonProperty("header_difficulty")]
        public int? HeaderDifficulty { get; set; }

        [JsonProperty("judge_rank")]
        public int? JudgeRank { get; set; }
    }
}
