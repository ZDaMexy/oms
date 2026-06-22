// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Rulesets;

namespace osu.Game.Beatmaps
{
    internal static class BmsPersistedMetadataResolver
    {
        private const string supported_converted_ruleset = "mania";

        // Bump this (use the change date as YYYYMMDD) whenever BmsToManiaBeatmapConverter changes how it
        // maps objects, timing, or star rating. Persisted converted star ratings tagged with an older
        // version are treated as stale and recomputed, so forgetting to bump it leaves outdated stars cached.
        private const int current_bms_to_mania_conversion_version = 20260526;

        public static BmsPersistedChartMetadata? GetChartMetadata(BeatmapMetadata? metadata)
        {
            return getPersistedData(metadata)?.ChartMetadata;
        }

        /// <summary>
        /// Reads the BMS difficulty-table entries persisted by the BMS ruleset's <c>BmsDifficultyTableManager</c>
        /// (the same <c>RulesetData</c> column, under <c>difficulty_table_entries</c>). This is a READ-ONLY view:
        /// the entries are pulled from the captured extension data so this never participates in a write-back that
        /// could drop the foreign <c>Symbol</c>/<c>Md5</c> fields we don't model here. Returns an empty list when the
        /// chart is not indexed by any enabled table.
        /// </summary>
        public static IReadOnlyList<BmsPersistedDifficultyTableEntry> GetDifficultyTableEntries(BeatmapMetadata? metadata)
        {
            var extensionData = getPersistedData(metadata)?.ExtensionData;

            if (extensionData == null || !extensionData.TryGetValue("difficulty_table_entries", out var token) || token is not JArray array)
                return Array.Empty<BmsPersistedDifficultyTableEntry>();

            var entries = new List<BmsPersistedDifficultyTableEntry>(array.Count);

            foreach (var element in array)
            {
                BmsPersistedDifficultyTableEntry? entry;

                try
                {
                    entry = element.ToObject<BmsPersistedDifficultyTableEntry>();
                }
                catch (JsonException)
                {
                    entry = null;
                }

                if (entry != null)
                    entries.Add(entry);
            }

            return entries;
        }

        public static bool TryGetConvertedStarRating(BeatmapMetadata? metadata, IRulesetInfo ruleset, out double starRating)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            starRating = default;

            if (!tryGetCurrentConvertedStarRating(metadata, ruleset, out var persistedRating))
                return false;

            var currentRating = persistedRating!;

            if (currentRating.Failed)
                return false;

            starRating = currentRating.StarRating;
            return true;
        }

        public static bool HasCurrentConvertedStarRatingState(BeatmapMetadata? metadata, IRulesetInfo ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            return tryGetCurrentConvertedStarRating(metadata, ruleset, out _);
        }

        public static bool SupportsConvertedStarRatings(IRulesetInfo ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            return supportsConvertedStarRatings(ruleset);
        }

        public static void SetConvertedStarRating(BeatmapMetadata metadata, IRulesetInfo ruleset, double starRating, int difficultyVersion)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(ruleset);

            if (!supportsConvertedStarRatings(ruleset))
                return;

            setConvertedStarRating(metadata, ruleset, starRating, difficultyVersion, failed: false);
        }

        public static void SetConvertedStarRatingFailure(BeatmapMetadata metadata, IRulesetInfo ruleset, int difficultyVersion)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(ruleset);

            if (!supportsConvertedStarRatings(ruleset))
                return;

            setConvertedStarRating(metadata, ruleset, 0, difficultyVersion, failed: true);
        }

        private static void setConvertedStarRating(BeatmapMetadata metadata, IRulesetInfo ruleset, double starRating, int difficultyVersion, bool failed)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(ruleset);

            // Snapshot the previous JSON before mutating so we can evict the stale parsedDataCache entry once
            // SetRulesetData re-serialises the payload. Otherwise the per-payload cache grows monotonically as
            // every star-rating recompute leaves the previous JSON keyed entry behind, even after no one will
            // ever read it again.
            string previousJson = metadata.RulesetDataJson;

            var data = getPersistedData(metadata) ?? new BmsPersistedMetadataData();

            data.ConvertedStarRatings ??= new Dictionary<string, BmsPersistedConvertedStarRating>(StringComparer.Ordinal);
            data.ConvertedStarRatings[ruleset.ShortName] = new BmsPersistedConvertedStarRating
            {
                StarRating = starRating,
                DifficultyVersion = difficultyVersion,
                ConversionVersion = current_bms_to_mania_conversion_version,
                Failed = failed,
            };

            metadata.SetRulesetData(data);

            if (!string.IsNullOrEmpty(previousJson) && previousJson != metadata.RulesetDataJson)
                parsedDataCache.TryRemove(previousJson, out _);
        }

        private static bool tryGetCurrentConvertedStarRating(BeatmapMetadata? metadata, IRulesetInfo ruleset, out BmsPersistedConvertedStarRating? persistedRating)
        {
            persistedRating = null;

            if (!supportsConvertedStarRatings(ruleset) || ruleset is not RulesetInfo localRuleset)
                return false;

            var convertedStarRatings = getPersistedData(metadata)?.ConvertedStarRatings;

            if (convertedStarRatings == null || !convertedStarRatings.TryGetValue(ruleset.ShortName, out persistedRating))
                return false;

            if (persistedRating.ConversionVersion != current_bms_to_mania_conversion_version)
                return false;

            // K10-B deferred: this compares against the consumer-supplied LastAppliedDifficultyVersion rather than
            // spinning up a fresh DifficultyCalculator to read the authoritative version every call. Real-library
            // testing (P1-K K10 second strike) confirmed that under the single-RulesetStore-instance ownership the
            // detached RulesetInfo is reliably synced at startup by clearOutdatedStarRatings, so the read-side
            // hardening (B) was deferred to keep the read path constant-cost. See P1-K TECHNICAL_CONSTRAINTS #5-6
            // and DEVELOPMENT_PLAN K10-B for the reasoning if this assumption ever needs to be revisited.
            if (persistedRating.DifficultyVersion != localRuleset.LastAppliedDifficultyVersion)
                return false;

            return true;
        }

        // Cache parsed metadata keyed by the raw JSON string so the carousel's per-filter star lookup over the BMS library
        // doesn't pay the JSON deserialisation cost on every pass (which was a multi-second hit for large libraries).
        // Keys are immutable strings persisted on Realm; identical persisted state shares an entry, mutations land at a new
        // key, and stale entries simply leak — bounded by the number of unique persisted payloads in the library.
        private static readonly ConcurrentDictionary<string, BmsPersistedMetadataData?> parsedDataCache = new ConcurrentDictionary<string, BmsPersistedMetadataData?>(StringComparer.Ordinal);

        private static BmsPersistedMetadataData? getPersistedData(BeatmapMetadata? metadata)
        {
            if (metadata == null)
                return null;

            string json = metadata.RulesetDataJson;

            if (string.IsNullOrWhiteSpace(json))
                return null;

            if (parsedDataCache.TryGetValue(json, out var cached))
                return cached;

            BmsPersistedMetadataData? parsed;

            try
            {
                parsed = JsonConvert.DeserializeObject<BmsPersistedMetadataData>(json);
            }
            catch (JsonException)
            {
                parsed = null;
            }

            parsedDataCache[json] = parsed;
            return parsed;
        }

        private static bool supportsConvertedStarRatings(IRulesetInfo ruleset)
            => string.Equals(ruleset.ShortName, supported_converted_ruleset, StringComparison.Ordinal);
    }

    internal class BmsPersistedMetadataData
    {
        [JsonProperty("chart_metadata")]
        public BmsPersistedChartMetadata? ChartMetadata { get; set; }

        [JsonProperty("converted_star_ratings")]
        public Dictionary<string, BmsPersistedConvertedStarRating>? ConvertedStarRatings { get; set; }

        // The single BeatmapMetadata.RulesetData column is shared with the BMS ruleset's own payload
        // (BmsBeatmapMetadataData: difficulty_table_entries / chart_filter_stats). Capture any fields we don't
        // model here so re-serialising a converted-star-rating write does NOT wipe the difficulty-table data
        // (and vice versa). Without this, recomputing converted star ratings dropped every chart's table entries.
        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtensionData { get; set; }
    }

    // Read-only mirror of the BMS ruleset's BmsDifficultyTableEntry record (serialised PascalCase). Only the fields
    // needed to render the inline classification are modelled; this type is NEVER serialised back, so omitting
    // Symbol/Md5 here is safe (the full entry round-trips through BmsPersistedMetadataData.ExtensionData).
    internal class BmsPersistedDifficultyTableEntry
    {
        [JsonProperty("TableName")]
        public string TableName { get; set; } = string.Empty;

        [JsonProperty("LevelLabel")]
        public string LevelLabel { get; set; } = string.Empty;

        [JsonProperty("Level")]
        public int Level { get; set; }

        [JsonProperty("TableSortOrder")]
        public int TableSortOrder { get; set; }
    }

    internal class BmsPersistedConvertedStarRating
    {
        [JsonProperty("star_rating")]
        public double StarRating { get; set; }

        [JsonProperty("difficulty_version")]
        public int DifficultyVersion { get; set; }

        [JsonProperty("conversion_version")]
        public int ConversionVersion { get; set; }

        [JsonProperty("failed")]
        public bool Failed { get; set; }
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
