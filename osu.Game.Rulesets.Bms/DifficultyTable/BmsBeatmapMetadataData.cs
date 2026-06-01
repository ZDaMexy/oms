// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsBeatmapMetadataData
    {
        [JsonProperty("difficulty_table_entries")]
        public List<BmsDifficultyTableEntry> DifficultyTableEntries { get; set; } = new List<BmsDifficultyTableEntry>();

        [JsonProperty("chart_metadata")]
        public BmsChartMetadata? ChartMetadata { get; set; }

        [JsonProperty("chart_filter_stats")]
        public BmsChartFilterStats? ChartFilterStats { get; set; }

        // The single BeatmapMetadata.RulesetData column is shared with osu.Game's BmsPersistedMetadataData
        // (converted_star_ratings). Capture fields we don't model here so re-serialising a difficulty-table
        // write does NOT wipe the persisted converted star ratings (which would force a full recompute next
        // launch, which in turn used to wipe the table entries — a destructive ping-pong).
        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtensionData { get; set; }

        [JsonIgnore]
        public bool IsEmpty => DifficultyTableEntries.Count == 0 && ChartMetadata == null && ChartFilterStats == null && (ExtensionData == null || ExtensionData.Count == 0);
    }

    public class BmsChartFilterStats : IEquatable<BmsChartFilterStats>
    {
        [JsonProperty("total_playable_object_count")]
        public int TotalPlayableObjectCount { get; set; }

        [JsonProperty("regular_note_count")]
        public int RegularNoteCount { get; set; }

        [JsonProperty("long_note_count")]
        public int LongNoteCount { get; set; }

        [JsonProperty("scratch_note_count")]
        public int ScratchNoteCount { get; set; }

        [JsonIgnore]
        public bool IsEmpty => TotalPlayableObjectCount == 0;

        [JsonIgnore]
        public float RegularNotePercentage => getPercentage(RegularNoteCount);

        [JsonIgnore]
        public float LongNotePercentage => getPercentage(LongNoteCount);

        [JsonIgnore]
        public float ScratchNotePercentage => getPercentage(ScratchNoteCount);

        public static BmsChartFilterStats FromBeatmap(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            var playableObjects = beatmap.HitObjects.OfType<BmsHitObject>().ToArray();

            return new BmsChartFilterStats
            {
                TotalPlayableObjectCount = playableObjects.Length,
                RegularNoteCount = playableObjects.Count(hitObject => !hitObject.IsScratch && hitObject is not BmsHoldNote),
                LongNoteCount = playableObjects.Count(hitObject => hitObject is BmsHoldNote && !hitObject.IsScratch),
                ScratchNoteCount = playableObjects.Count(hitObject => hitObject.IsScratch),
            };
        }

        public bool Equals(BmsChartFilterStats? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return TotalPlayableObjectCount == other.TotalPlayableObjectCount
                   && RegularNoteCount == other.RegularNoteCount
                   && LongNoteCount == other.LongNoteCount
                   && ScratchNoteCount == other.ScratchNoteCount;
        }

        public override bool Equals(object? obj) => Equals(obj as BmsChartFilterStats);

        public override int GetHashCode() => HashCode.Combine(TotalPlayableObjectCount, RegularNoteCount, LongNoteCount, ScratchNoteCount);

        private float getPercentage(int count)
            => TotalPlayableObjectCount == 0 ? 0 : count / (float)TotalPlayableObjectCount * 100;
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

        public static BmsChartFilterStats? GetChartFilterStats(this IBeatmapMetadataInfo metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata is BeatmapMetadata beatmapMetadata
                ? beatmapMetadata.GetChartFilterStats()
                : null;
        }

        public static BmsChartFilterStats? GetChartFilterStats(this BeatmapMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return metadata.GetRulesetData<BmsBeatmapMetadataData>()?.ChartFilterStats;
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

            metadata.SetRulesetData(data.IsEmpty ? null : data);

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

            metadata.SetRulesetData(data.IsEmpty ? null : data);

            return true;
        }

        public static bool SetChartFilterStats(this BeatmapMetadata metadata, BmsChartFilterStats? chartFilterStats)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            chartFilterStats = chartFilterStats?.IsEmpty == true ? null : chartFilterStats;

            if (EqualityComparer<BmsChartFilterStats?>.Default.Equals(metadata.GetChartFilterStats(), chartFilterStats))
                return false;

            var data = metadata.GetRulesetData<BmsBeatmapMetadataData>() ?? new BmsBeatmapMetadataData();

            data.ChartFilterStats = chartFilterStats;

            metadata.SetRulesetData(data.IsEmpty ? null : data);

            return true;
        }
    }
}
