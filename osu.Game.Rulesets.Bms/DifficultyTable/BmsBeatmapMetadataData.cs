// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Scoring;

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

        [JsonIgnore]
        public bool IsEmpty
            => string.IsNullOrWhiteSpace(Subtitle)
               && string.IsNullOrWhiteSpace(SubArtist)
               && string.IsNullOrWhiteSpace(Comment)
               && string.IsNullOrWhiteSpace(Genre)
               && string.IsNullOrWhiteSpace(PlayLevel)
               && HeaderDifficulty == null
               && JudgeRank == null;

        public static BmsChartMetadata FromBeatmapInfo(BmsBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            return new BmsChartMetadata
            {
                Subtitle = beatmapInfo.Subtitle,
                SubArtist = beatmapInfo.SubArtist,
                Comment = beatmapInfo.Comment,
                Genre = beatmapInfo.Genre,
                PlayLevel = beatmapInfo.PlayLevel,
                HeaderDifficulty = beatmapInfo.HeaderDifficulty,
                JudgeRank = beatmapInfo.Rank,
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

        public string GetJudgeRankDisplay()
        {
            if (JudgeRank == null)
                return string.Empty;

            return OsuOdJudgementSystem.GetRankDisplayName(JudgeRank.Value);
        }

        public string? TryGetChartCreator(string? artist = null)
        {
            string? creator = TryExtractCreator(SubArtist);

            if (!string.IsNullOrWhiteSpace(creator))
                return creator;

            creator = TryExtractCreator(Comment);

            if (!string.IsNullOrWhiteSpace(creator))
                return creator;

            return TryExtractCreator(artist);
        }

        internal static string GetDisplayArtist(string? artist)
            => BeatmapLocalMetadataDisplayResolver.StripBmsCreatorFromArtist(artist);

        internal static string? TryExtractCreator(string? value)
            => BeatmapLocalMetadataDisplayResolver.TryExtractBmsCreator(value)?.creator;

        public bool Equals(BmsChartMetadata? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(Subtitle, other.Subtitle, StringComparison.Ordinal)
                   && string.Equals(SubArtist, other.SubArtist, StringComparison.Ordinal)
                   && string.Equals(Comment, other.Comment, StringComparison.Ordinal)
                   && string.Equals(Genre, other.Genre, StringComparison.Ordinal)
                   && string.Equals(PlayLevel, other.PlayLevel, StringComparison.Ordinal)
                   && HeaderDifficulty == other.HeaderDifficulty
                   && JudgeRank == other.JudgeRank;
        }

        public override bool Equals(object? obj) => Equals(obj as BmsChartMetadata);

        public override int GetHashCode() => HashCode.Combine(Subtitle, SubArtist, Comment, Genre, PlayLevel, HeaderDifficulty, JudgeRank);
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
