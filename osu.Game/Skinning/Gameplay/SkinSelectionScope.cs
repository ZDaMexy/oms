// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>A settings scope. Key counts describe playable columns; BMS scratch lanes are not keys.</summary>
    public readonly record struct SkinSelectionScope
    {
        public string RulesetId { get; }
        public int? KeyCount { get; }

        public SkinSelectionScope(string rulesetId, int? keyCount = null)
        {
            if (rulesetId is not ("bms" or "mania"))
                throw new ArgumentException("Unknown skin ruleset.", nameof(rulesetId));
            if (keyCount != null && !GetSupportedKeyCounts(rulesetId).Contains(keyCount.Value))
                throw new ArgumentOutOfRangeException(nameof(keyCount));
            RulesetId = rulesetId;
            KeyCount = keyCount;
        }

        public static IReadOnlyList<int> GetSupportedKeyCounts(string rulesetId) => rulesetId switch
        {
            "bms" => bms_keys,
            // Mania supports 1–10 keys in a single stage and 1–10 per side in dual stages.
            "mania" => mania_keys,
            _ => throw new ArgumentException("Unknown skin ruleset.", nameof(rulesetId)),
        };

        private static readonly IReadOnlyList<int> bms_keys = Array.AsReadOnly(new[] { 5, 7, 9, 14 });
        private static readonly IReadOnlyList<int> mania_keys = Array.AsReadOnly(Enumerable.Range(1, 10).Concat(new[] { 12, 14, 16, 18, 20 }).ToArray());

        internal string PreferenceSuffix => RulesetId + (KeyCount is int keys ? "/" + keys.ToString(CultureInfo.InvariantCulture) : string.Empty);

        public override string ToString() => (RulesetId == "bms" ? "BMS" : "osu!mania") + (KeyCount is int keys ? $" · {keys}K" : " · 模式默认");
    }
}
