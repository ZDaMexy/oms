// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsBeatmapMetadataData
    {
        [JsonProperty("difficulty_table_entries")]
        public List<BmsDifficultyTableEntry> DifficultyTableEntries { get; set; } = new List<BmsDifficultyTableEntry>();

        [JsonProperty("chart_metadata")]
        public BmsChartMetadata? ChartMetadata { get; set; }
    }

    public class BmsChartMetadata : IEquatable<BmsChartMetadata>
    {
        private static readonly string[] creator_prefixes =
        {
            "obj",
            "chart",
            "charts",
            "pattern",
            "patterns",
            "note",
            "notes",
            "fumen",
            "譜面",
            "谱面",
        };

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        [JsonProperty("sub_artist")]
        public string SubArtist { get; set; } = string.Empty;

        [JsonProperty("comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("play_level")]
        public string PlayLevel { get; set; } = string.Empty;

        [JsonProperty("header_difficulty")]
        public int? HeaderDifficulty { get; set; }

        [JsonIgnore]
        public bool IsEmpty
            => string.IsNullOrWhiteSpace(Subtitle)
               && string.IsNullOrWhiteSpace(SubArtist)
               && string.IsNullOrWhiteSpace(Comment)
               && string.IsNullOrWhiteSpace(PlayLevel)
               && HeaderDifficulty == null;

        public static BmsChartMetadata FromBeatmapInfo(BmsBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            return new BmsChartMetadata
            {
                Subtitle = beatmapInfo.Subtitle,
                SubArtist = beatmapInfo.SubArtist,
                Comment = beatmapInfo.Comment,
                PlayLevel = beatmapInfo.PlayLevel,
                HeaderDifficulty = beatmapInfo.HeaderDifficulty,
            };
        }

        public string GetInternalLevelDisplay()
        {
            string difficultyLabel = HeaderDifficulty switch
            {
                1 => "Beginner",
                2 => "Normal",
                3 => "Hyper",
                4 => "Another",
                5 => "Insane",
                _ => string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(difficultyLabel) && !string.IsNullOrWhiteSpace(PlayLevel))
                return $"{difficultyLabel} {PlayLevel}";

            if (!string.IsNullOrWhiteSpace(difficultyLabel))
                return difficultyLabel;

            return PlayLevel;
        }

        public string? TryGetChartCreator()
        {
            string? creator = tryExtractCreator(SubArtist);

            if (!string.IsNullOrWhiteSpace(creator))
                return creator;

            return tryExtractCreator(Comment);
        }

        public bool Equals(BmsChartMetadata? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(Subtitle, other.Subtitle, StringComparison.Ordinal)
                   && string.Equals(SubArtist, other.SubArtist, StringComparison.Ordinal)
                   && string.Equals(Comment, other.Comment, StringComparison.Ordinal)
                   && string.Equals(PlayLevel, other.PlayLevel, StringComparison.Ordinal)
                   && HeaderDifficulty == other.HeaderDifficulty;
        }

        public override bool Equals(object? obj) => Equals(obj as BmsChartMetadata);

        public override int GetHashCode() => HashCode.Combine(Subtitle, SubArtist, Comment, PlayLevel, HeaderDifficulty);

        private static string? tryExtractCreator(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string trimmed = value.Trim();

            foreach (string prefix in creator_prefixes)
            {
                if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || trimmed.Length <= prefix.Length)
                    continue;

                string suffix = trimmed[prefix.Length..].TrimStart();

                if (suffix.Length == 0)
                    continue;

                char separator = suffix[0];

                if (separator is not (':' or '：' or '-' or '/' or '='))
                    continue;

                string creator = suffix[1..].Trim();

                if (!string.IsNullOrWhiteSpace(creator))
                    return creator;
            }

            return null;
        }
    }

    public static class BmsBeatmapMetadataExtensions
    {
        public static IReadOnlyList<BmsDifficultyTableEntry> GetDifficultyTableEntries(this IBeatmapMetadataInfo metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata is BeatmapMetadata beatmapMetadata
                ? beatmapMetadata.GetDifficultyTableEntries()
                : Array.Empty<BmsDifficultyTableEntry>();
        }

        public static IReadOnlyList<BmsDifficultyTableEntry> GetDifficultyTableEntries(this BeatmapMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata.GetRulesetData<BmsBeatmapMetadataData>()?.DifficultyTableEntries.ToArray()
                   ?? Array.Empty<BmsDifficultyTableEntry>();
        }

        public static BmsChartMetadata? GetChartMetadata(this IBeatmapMetadataInfo metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata is BeatmapMetadata beatmapMetadata
                ? beatmapMetadata.GetChartMetadata()
                : null;
        }

        public static BmsChartMetadata? GetChartMetadata(this BeatmapMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata.GetRulesetData<BmsBeatmapMetadataData>()?.ChartMetadata;
        }

        public static bool SetDifficultyTableEntries(this BeatmapMetadata metadata, IReadOnlyList<BmsDifficultyTableEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(entries);

            var orderedEntries = entries.OrderBy(entry => entry.TableSortOrder)
                                      .ThenBy(entry => entry.TableName, StringComparer.Ordinal)
                                      .ThenBy(entry => entry.Level)
                                      .ThenBy(entry => entry.LevelLabel, StringComparer.Ordinal)
                                      .ThenBy(entry => entry.Md5, StringComparer.Ordinal)
                                      .ToArray();

            if (metadata.GetDifficultyTableEntries().SequenceEqual(orderedEntries))
                return false;

            var data = metadata.GetRulesetData<BmsBeatmapMetadataData>() ?? new BmsBeatmapMetadataData();

            data.DifficultyTableEntries = orderedEntries.ToList();

            metadata.SetRulesetData(data.DifficultyTableEntries.Count == 0 && data.ChartMetadata == null
                ? null
                : data);

            return true;
        }

        public static bool SetChartMetadata(this BeatmapMetadata metadata, BmsChartMetadata? chartMetadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            chartMetadata = chartMetadata?.IsEmpty == true ? null : chartMetadata;

            if (EqualityComparer<BmsChartMetadata?>.Default.Equals(metadata.GetChartMetadata(), chartMetadata))
                return false;

            var data = metadata.GetRulesetData<BmsBeatmapMetadataData>() ?? new BmsBeatmapMetadataData();

            data.ChartMetadata = chartMetadata;

            metadata.SetRulesetData(data.DifficultyTableEntries.Count == 0 && data.ChartMetadata == null
                ? null
                : data);

            return true;
        }
    }
}
