// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Builds the BMS difficulty-table group tree (table → level, with an "Unrated" bucket when the chart is not
    /// indexed by any enabled table) for a beatmap shown in a non-BMS ruleset — i.e. BMS charts displayed as mania
    /// converts. The entries are read via <see cref="BmsPersistedMetadataResolver"/>, the same persisted data the BMS
    /// ruleset's own grouping uses, so both rulesets present an identical tree.
    /// </summary>
    /// <remarks>
    /// Charts that are NOT BMS (e.g. native mania) return no group definitions, so the hierarchical grouping in
    /// <see cref="BeatmapCarouselFilterGrouping"/> drops them — the mania difficulty-table grouping therefore shows
    /// ONLY BMS converts. Whether those converts appear at all is still gated by the converted-beatmaps display
    /// setting in the matching stage; an empty result is surfaced with guidance by <see cref="NoResultsPlaceholder"/>.
    /// </remarks>
    public static class BmsConvertedDifficultyTableGrouping
    {
        private const string bms_ruleset_short_name = "bms";
        private const string unrated_group_title = "Unrated";

        // Guards the parse cache against unbounded growth; distinct keys are bounded by library size in practice.
        private const int cache_soft_cap = 200_000;

        // Caches the computed group tree keyed on the raw RulesetData JSON (stale-proof: any metadata change yields a
        // new key). Mirrors BmsTableGroupMode so the per-refilter, per-beatmap deserialisation cost stays bounded.
        private static readonly ConcurrentDictionary<string, GroupDefinition[]> group_cache = new ConcurrentDictionary<string, GroupDefinition[]>();

        public static IEnumerable<GroupDefinition> GetGroupDefinitions(IBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            // Only BMS charts participate; native mania (and anything else) is excluded from this grouping entirely.
            if (!string.Equals(beatmapInfo.Ruleset.ShortName, bms_ruleset_short_name, StringComparison.Ordinal))
                return Array.Empty<GroupDefinition>();

            // The cache key (RulesetDataJson) only exists on the concrete model; otherwise compute directly.
            if (beatmapInfo.Metadata is not BeatmapMetadata metadata)
                return compute(beatmapInfo);

            string json = metadata.RulesetDataJson ?? string.Empty;

            if (group_cache.TryGetValue(json, out var cached))
                return cached;

            var computed = compute(beatmapInfo);

            if (group_cache.Count >= cache_soft_cap)
                group_cache.Clear();

            group_cache[json] = computed;
            return computed;
        }

        // Internal for focused testing of the pure computation independent of the parse cache.
        internal static GroupDefinition[] ComputeGroupDefinitions(IBeatmapInfo beatmapInfo)
        {
            if (!string.Equals(beatmapInfo.Ruleset.ShortName, bms_ruleset_short_name, StringComparison.Ordinal))
                return Array.Empty<GroupDefinition>();

            return compute(beatmapInfo);
        }

        private static GroupDefinition[] compute(IBeatmapInfo beatmapInfo)
        {
            var entries = BmsPersistedMetadataResolver.GetDifficultyTableEntries(beatmapInfo.Metadata as BeatmapMetadata);

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
