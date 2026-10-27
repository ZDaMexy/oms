// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>
    /// Query-only BMS comparison for local PB/NEXT. Saved mods, replay settings and submission data remain exact.
    /// </summary>
    public static class BmsPersonalBestModComparison
    {
        private static readonly FrozenDictionary<string, KnownMod> knownMods = new BmsRuleset().CreateAllMods()
            .ToFrozenDictionary(mod => mod.Acronym, mod =>
            {
                var defaults = mod.GetSettingsSourceProperties().ToFrozenDictionary(item => item.Item2.Name.ToSnakeCase(),
                    item => token(item.Item2.GetValue(mod)!.GetUnderlyingSettingValue()), StringComparer.Ordinal);
                return new KnownMod(mod is BmsModSudden or BmsModHidden or BmsModLift, defaults,
                    defaults.Keys.Where(key => ignoredSetting(mod, key)).ToFrozenSet(StringComparer.Ordinal));
            }, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<APIMod> Normalize(IReadOnlyList<APIMod> mods)
        {
            var result = new List<APIMod>(mods.Count);
            foreach (APIMod source in mods)
            {
                knownMods.TryGetValue(source.Acronym, out KnownMod? known);
                var settings = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach ((string key, object value) in source.Settings)
                {
                    if (known != null && known.IgnoredSettings.Contains(key))
                        continue;
                    JToken setting = token(value);
                    if (known != null && known.Defaults.TryGetValue(key, out JToken? defaultValue) && JToken.DeepEquals(setting, defaultValue))
                        continue;
                    // Copy typed JSON without parsing/clamping unknown settings or sharing mutable tokens.
                    settings.Add(key, setting);
                }
                // Unknown settings keep even an otherwise ignored Mod present in the comparison.
                if (settings.Count == 0 && known?.IgnorePresence == true)
                    continue;
                result.Add(new APIMod { Acronym = source.Acronym, Settings = settings });
            }
            return result;
        }

        private static bool ignoredSetting(Mod? mod, string key)
            => mod switch
            {
                BmsModSudden or BmsModHidden => key is "cover_percent" or "cover_opacity" or "remember_gameplay_changes",
                BmsModLift => key is "lift_units" or "remember_gameplay_changes",
                BmsModAutoScratch => key is "tint_scratch_notes" or "scratch_tint_colour",
                BmsModAutoNote => key is "tint_notes" or "note_tint_colour",
                _ => false,
            };

        private static JToken token(object? value) => value == null ? JValue.CreateNull() : JToken.FromObject(value);

        private sealed record KnownMod(bool IgnorePresence, FrozenDictionary<string, JToken> Defaults, FrozenSet<string> IgnoredSettings);
    }
}
