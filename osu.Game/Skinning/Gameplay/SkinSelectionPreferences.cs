// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Mode defaults and sparse per-key overrides. Editing this map never selects a gameplay mode or chart.</summary>
    public sealed class SkinSelectionPreferences
    {
        private const int max_bytes = 8192;
        private readonly Bindable<string> bms;
        private readonly Bindable<string> mania;
        private readonly Bindable<string> legacy;
        private readonly Bindable<string> keyOverrides;

        public event Action? Changed;

        public SkinSelectionPreferences(OsuConfigManager config)
        {
            bms = config.GetBindable<string>(OsuSetting.SkinBms);
            mania = config.GetBindable<string>(OsuSetting.SkinMania);
            legacy = config.GetBindable<string>(OsuSetting.Skin);
            keyOverrides = config.GetBindable<string>(OsuSetting.SkinKeyOverrides);
            bms.BindValueChanged(_ => Changed?.Invoke());
            mania.BindValueChanged(_ => Changed?.Invoke());
            keyOverrides.BindValueChanged(_ => Changed?.Invoke());
        }

        public Guid? GetConfiguredSkinId(SkinSelectionScope scope)
        {
            string? value = scope.KeyCount is int keys
                ? read()[scope.RulesetId]?[keys.ToString(CultureInfo.InvariantCulture)]?.Value<string>()
                : mode(scope.RulesetId).Value;
            return Guid.TryParse(value, out Guid id) ? id : null;
        }

        public Guid GetEffectiveSkinId(SkinSelectionScope scope)
            => GetConfiguredSkinId(scope) ?? GetConfiguredSkinId(new SkinSelectionScope(scope.RulesetId))
                ?? (Guid.TryParse(legacy.Value, out Guid id) ? id : SkinInfo.OMS_SKIN);

        public void SetSkin(SkinSelectionScope scope, Guid skinId)
        {
            if (SkinFilesystemStorageResolver.IsFixedSkinId(skinId) && skinId != SkinInfo.OMS_SKIN)
                throw new ArgumentException("This skin cannot be assigned to a gameplay scope.", nameof(skinId));
            if (scope.KeyCount is not int keys)
            {
                mode(scope.RulesetId).Value = skinId.ToString();
                return;
            }

            JObject profiles = read();
            JObject profile = profiles[scope.RulesetId] as JObject ?? new JObject();
            profile[keys.ToString(CultureInfo.InvariantCulture)] = skinId.ToString();
            profiles[scope.RulesetId] = profile;
            keyOverrides.Value = profiles.ToString(Formatting.None);
        }

        /// <summary>Clears only this key-count assignment. A mode-default reset selects the built-in fallback.</summary>
        public void Reset(SkinSelectionScope scope)
        {
            if (scope.KeyCount is not int keys)
            {
                mode(scope.RulesetId).Value = SkinInfo.OMS_SKIN.ToString();
                return;
            }

            JObject profiles = read();
            if (profiles[scope.RulesetId] is JObject profile)
            {
                profile.Remove(keys.ToString(CultureInfo.InvariantCulture));
                if (profile.Count == 0)
                    profiles.Remove(scope.RulesetId);
            }
            keyOverrides.Value = profiles.ToString(Formatting.None);
        }

        /// <summary>Returns explicit assignments; inherited uses follow their mode default automatically.</summary>
        public IReadOnlyList<SkinSelectionScope> GetUsages(Guid skinId)
        {
            var result = new List<SkinSelectionScope>();
            foreach (string ruleset in new[] { "bms", "mania" })
            {
                var modeScope = new SkinSelectionScope(ruleset);
                if (GetEffectiveSkinId(modeScope) == skinId)
                    result.Add(modeScope);
                foreach (int keys in SkinSelectionScope.GetSupportedKeyCounts(ruleset))
                {
                    var scope = new SkinSelectionScope(ruleset, keys);
                    if (GetConfiguredSkinId(scope) == skinId)
                        result.Add(scope);
                }
            }
            return new ReadOnlyCollection<SkinSelectionScope>(result);
        }

        private Bindable<string> mode(string ruleset) => ruleset switch
        {
            "bms" => bms,
            "mania" => mania,
            _ => throw new ArgumentException("Unknown skin ruleset.", nameof(ruleset)),
        };

        private JObject read()
        {
            string value = keyOverrides.Value;
            try
            {
                if (value.Length > max_bytes)
                    throw new JsonSerializationException();
                JObject profiles = JObject.Parse(value, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                foreach (JProperty property in profiles.Properties())
                {
                    if (property.Name is not ("bms" or "mania") || property.Value is not JObject profile)
                        throw new JsonSerializationException();
                    foreach (JProperty key in profile.Properties())
                    {
                        if (!int.TryParse(key.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                            || key.Name != count.ToString(CultureInfo.InvariantCulture)
                            || !SkinSelectionScope.GetSupportedKeyCounts(property.Name).Contains(count)
                            || key.Value.Type != JTokenType.String || !Guid.TryParse(key.Value.Value<string>(), out Guid id)
                            || (SkinFilesystemStorageResolver.IsFixedSkinId(id) && id != SkinInfo.OMS_SKIN))
                            throw new JsonSerializationException();
                    }
                }
                return profiles;
            }
            catch (JsonException)
            {
                Logger.Log("Invalid per-key skin assignments; mode defaults will be used.", level: LogLevel.Important);
                return new JObject();
            }
        }
    }
}
