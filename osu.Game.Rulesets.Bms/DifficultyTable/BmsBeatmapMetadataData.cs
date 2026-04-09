// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsBeatmapMetadataData
    {
        [JsonProperty("difficulty_table_entries")]
        public List<BmsDifficultyTableEntry> DifficultyTableEntries { get; set; } = new List<BmsDifficultyTableEntry>();
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

            metadata.SetRulesetData(orderedEntries.Length == 0
                ? null
                : new BmsBeatmapMetadataData
                {
                    DifficultyTableEntries = orderedEntries.ToList(),
                });

            return true;
        }
    }
}
