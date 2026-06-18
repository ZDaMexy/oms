// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.Bms.SongSelect
{
    public static class BmsTableGroupMode
    {
        private const string unrated_group_title = "Unrated";

        // Guards the parse cache against unbounded growth under pathological churn.
        // Distinct keys are bounded by library size in practice; the cap is a safety valve.
        private const int cache_soft_cap = 200_000;

        // Caches the computed group-definition tree keyed on the raw RulesetData JSON.
        //
        // GetDifficultyTableEntries() deserialises the entire BmsBeatmapMetadataData (chart metadata,
        // star ratings, filter stats, table entries) on every call, and hierarchical grouping calls this
        // once per beatmap on every refilter. For large libraries the repeated full-JSON deserialisation
        // dominates the grouping phase (see P1-I TECHNICAL_CONSTRAINTS "展示层级与层级导航约束" #11/#12).
        //
        // Correctness: group definitions are a pure function of the metadata JSON, and GroupDefinition is an
        // immutable record, so caching+sharing the result array across beatmaps is correctness-neutral.
        // Stale-proof: the key is the JSON content, so any metadata change (e.g. difficulty-table reassignment)
        // produces a new key and a fresh compute.
        private static readonly ConcurrentDictionary<string, GroupDefinition[]> group_cache = new ConcurrentDictionary<string, GroupDefinition[]>();

        public static IEnumerable<GroupDefinition> GetGroupDefinitions(IBeatmapInfo beatmapInfo)
        {
            // The cache key (RulesetDataJson) only exists on the concrete model; otherwise compute directly.
            if (beatmapInfo.Metadata is not BeatmapMetadata metadata)
                return ComputeGroupDefinitions(beatmapInfo);

            string json = metadata.RulesetDataJson ?? string.Empty;

            if (group_cache.TryGetValue(json, out var cached))
                return cached;

            var computed = ComputeGroupDefinitions(beatmapInfo);

            if (group_cache.Count >= cache_soft_cap)
                group_cache.Clear();

            group_cache[json] = computed;
            return computed;
        }

        // Internal for focused testing of the pure computation independent of the parse cache.
        internal static GroupDefinition[] ComputeGroupDefinitions(IBeatmapInfo beatmapInfo)
        {
            var entries = beatmapInfo.Metadata.GetDifficultyTableEntries();

            if (entries.Count == 0)
                return new[] { new GroupDefinition(int.MaxValue, unrated_group_title) };

            return entries.GroupBy(entry => (entry.TableSortOrder, entry.TableName, entry.Level, entry.LevelLabel))
                         .Select(group => group.First())
                         .Select(entry =>
                         {
                             var tableGroup = new GroupDefinition(entry.TableSortOrder, entry.TableName);
                             return new GroupDefinition(entry.Level, entry.LevelLabel, tableGroup);
                         })
                         .ToArray();
        }
    }
}
