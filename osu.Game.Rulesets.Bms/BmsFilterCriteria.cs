// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Filter;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms
{
    public class BmsFilterCriteria : IRulesetFilterCriteria
    {
        private static readonly int[] supported_key_counts = { 5, 7, 9, 14 };

        private readonly HashSet<int> includedKeyCounts = supported_key_counts.ToHashSet();
        private FilterCriteria.OptionalRange<float> regularNotePercentage;
        private FilterCriteria.OptionalRange<float> longNotePercentage;
        private FilterCriteria.OptionalRange<float> scratchNotePercentage;

        public void ApplyVisualFilters(IEnumerable<int> keyCounts, FilterCriteria.OptionalRange<float> regularNotePercentage, FilterCriteria.OptionalRange<float> longNotePercentage,
                                       FilterCriteria.OptionalRange<float> scratchNotePercentage)
        {
            includedKeyCounts.Clear();
            includedKeyCounts.UnionWith(keyCounts.Intersect(supported_key_counts));

            this.regularNotePercentage = regularNotePercentage;
            this.longNotePercentage = longNotePercentage;
            this.scratchNotePercentage = scratchNotePercentage;
        }

        public bool Matches(BeatmapInfo beatmapInfo, FilterCriteria criteria)
        {
            bool keyCountMatch = !hasKeyCountFilter() || BmsRuleset.TryGetKeyCount(beatmapInfo, out int keyCount) && includedKeyCounts.Contains(keyCount);

            // Prefer metadata / cache data that was pre-populated off the filter loop.
            // Detached BeatmapInfo snapshots used in the carousel may have stale RulesetDataJson,
            // so GetChartFilterStats() on the snapshot is unreliable during matching.
            BmsChartFilterStats? filterStats = null;

            if (hasCompositionFilter())
            {
                filterStats = beatmapInfo.Metadata.GetChartFilterStats() ?? BmsChartFilterStatsBackfill.GetCachedStats(beatmapInfo.ID);

                // Keep runtime filter loops off working-beatmap I/O, but still allow tests to exercise
                // the backfill contract through the dedicated resolver path.
                if (filterStats == null && BmsChartFilterStatsBackfill.TestResolver != null)
                    filterStats = BmsChartFilterStatsBackfill.GetOrBackfill(beatmapInfo);
            }

            // Missing stats should not silently hide beatmaps from composition filters.
            // Once background backfill populates cache or tests resolve stats explicitly, matching tightens automatically.
            bool regularMatch = !regularNotePercentage.HasFilter || filterStats == null || regularNotePercentage.IsInRange(filterStats.RegularNotePercentage);
            bool longNoteMatch = !longNotePercentage.HasFilter || filterStats == null || longNotePercentage.IsInRange(filterStats.LongNotePercentage);
            bool scratchMatch = !scratchNotePercentage.HasFilter || filterStats == null || scratchNotePercentage.IsInRange(filterStats.ScratchNotePercentage);

            return keyCountMatch && regularMatch && longNoteMatch && scratchMatch;
        }

        public bool TryParseCustomKeywordCriteria(string key, Operator op, string strValues)
        {
            switch (key)
            {
                case "key":
                case "keys":
                    return tryParseKeyCountCriteria(op, strValues);

                case "rc":
                case "rice":
                case "regular":
                    return FilterQueryParser.TryUpdateCriteriaRange(ref regularNotePercentage, op, strValues);

                case "ln":
                    return FilterQueryParser.TryUpdateCriteriaRange(ref longNotePercentage, op, strValues);

                case "scr":
                case "scratch":
                    return FilterQueryParser.TryUpdateCriteriaRange(ref scratchNotePercentage, op, strValues);
            }

            return false;
        }

        public bool FilterMayChangeFromMods(ValueChangedEvent<IReadOnlyList<Mod>> mods) => false;

        private bool tryParseKeyCountCriteria(Operator op, string strValues)
        {
            var keyCounts = new HashSet<int>();

            foreach (string strValue in strValues.Split(','))
            {
                if (!int.TryParse(strValue, out int keyCount))
                    return false;

                keyCounts.Add(keyCount);
            }

            int? singleKeyCount = keyCounts.Count == 1 ? keyCounts.Single() : null;

            switch (op)
            {
                case Operator.Equal:
                    includedKeyCounts.IntersectWith(keyCounts);
                    return true;

                case Operator.NotEqual:
                    includedKeyCounts.ExceptWith(keyCounts);
                    return true;

                case Operator.Less:
                    if (singleKeyCount == null) return false;

                    includedKeyCounts.RemoveWhere(k => k >= singleKeyCount.Value);
                    return true;

                case Operator.LessOrEqual:
                    if (singleKeyCount == null) return false;

                    includedKeyCounts.RemoveWhere(k => k > singleKeyCount.Value);
                    return true;

                case Operator.Greater:
                    if (singleKeyCount == null) return false;

                    includedKeyCounts.RemoveWhere(k => k <= singleKeyCount.Value);
                    return true;

                case Operator.GreaterOrEqual:
                    if (singleKeyCount == null) return false;

                    includedKeyCounts.RemoveWhere(k => k < singleKeyCount.Value);
                    return true;

                default:
                    return false;
            }
        }

        private bool hasKeyCountFilter() => includedKeyCounts.Count != supported_key_counts.Length;

        private bool hasCompositionFilter() => regularNotePercentage.HasFilter || longNotePercentage.HasFilter || scratchNotePercentage.HasFilter;
    }
}
