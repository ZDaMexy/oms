// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Resolves chart-defined star ratings for BMS beatmaps from persisted metadata.
    /// </summary>
    public static class BmsStarRatingResolver
    {
        public const string RulesetShortName = "bms";

        private static readonly Regex first_numeric_value_regex = new Regex(@"(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static bool IsBmsBeatmap(IBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            return string.Equals(beatmapInfo.Ruleset.ShortName, RulesetShortName, StringComparison.Ordinal);
        }

        public static bool TryResolveFromBeatmapInfo(IBeatmapInfo beatmapInfo, out double starRating)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!IsBmsBeatmap(beatmapInfo))
            {
                starRating = default;
                return false;
            }

            if (beatmapInfo.StarRating >= 0)
            {
                starRating = beatmapInfo.StarRating;
                return true;
            }

            return TryResolveFromMetadata(beatmapInfo.Metadata as BeatmapMetadata, out starRating);
        }

        public static double ResolveOrDefault(IBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!IsBmsBeatmap(beatmapInfo))
                return beatmapInfo.StarRating;

            if (beatmapInfo.StarRating >= 0)
                return beatmapInfo.StarRating;

            return ResolveOrDefault(beatmapInfo.Metadata as BeatmapMetadata);
        }

        public static bool TryResolveFromMetadata(BeatmapMetadata? metadata, out double starRating)
        {
            starRating = default;

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.RulesetDataJson))
                return false;

            try
            {
                var root = JObject.Parse(metadata.RulesetDataJson);
                return TryParsePlayLevel(root.SelectToken("chart_metadata.play_level")?.Value<string>(), out starRating);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static double ResolveOrDefault(BeatmapMetadata? metadata)
            => TryResolveFromMetadata(metadata, out double starRating) ? starRating : 0;

        public static bool TryParsePlayLevel(string? playLevel, out double starRating)
        {
            starRating = default;

            if (string.IsNullOrWhiteSpace(playLevel))
                return false;

            var match = first_numeric_value_regex.Match(playLevel);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out starRating) || starRating <= 0)
            {
                starRating = default;
                return false;
            }

            return true;
        }
    }
}
