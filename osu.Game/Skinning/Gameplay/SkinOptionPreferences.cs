// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Player preferences only. Packages remain immutable and each gameplay root freezes its own resolved values.
    /// The author declaration, rather than this persistence map, decides whether a saved value is applicable.
    /// </summary>
    public sealed class SkinOptionPreferences
    {
        private const int max_profiles = 256;
        private const int max_options = 32;
        private const int max_bytes = 512 * 1024;
        private readonly Bindable<string> configuration;
        private readonly object sync = new object();

        public event Action? Changed;

        public SkinOptionPreferences(OsuConfigManager config)
        {
            configuration = config.GetBindable<string>(OsuSetting.SkinOptions);
            configuration.BindValueChanged(_ => Changed?.Invoke());
        }

        public IReadOnlyDictionary<string, string> GetOverrides(Guid skinId, string rulesetId, int? keyCount = null)
        {
            lock (sync)
            {
                JObject profiles = read();
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                if (profiles[profileKey(skinId, rulesetId, null)] is JObject defaults)
                    foreach (JProperty property in defaults.Properties())
                        result[property.Name] = property.Value.Value<string>()!;
                if (keyCount != null && profiles[profileKey(skinId, rulesetId, keyCount)] is JObject local)
                    foreach (JProperty property in local.Properties())
                        result[property.Name] = property.Value.Value<string>()!;
                return new ReadOnlyDictionary<string, string>(result);
            }
        }

        /// <summary>Only values explicitly stored at this scope; inherited mode values are excluded.</summary>
        public IReadOnlyDictionary<string, string> GetLocalOverrides(Guid skinId, string rulesetId, int? keyCount = null)
        {
            lock (sync)
            {
                JObject profiles = read();
                if (profiles[profileKey(skinId, rulesetId, keyCount)] is not JObject profile)
                    return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
                return new ReadOnlyDictionary<string, string>(profile.Properties()
                    .ToDictionary(p => p.Name, p => p.Value.Value<string>()!, StringComparer.Ordinal));
            }
        }

        public void SetOverride(Guid skinId, string rulesetId, string optionId, string? value, IReadOnlyCollection<string>? declaredIds = null, int? keyCount = null)
        {
            if (!token(optionId) || value is { Length: > GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH } || value?.Any(char.IsControl) == true)
                throw new ArgumentException("Invalid skin preference token.");

            lock (sync)
            {
                JObject profiles = read();
                string key = profileKey(skinId, rulesetId, keyCount);
                JObject profile = profiles[key] as JObject ?? new JObject();
                if (declaredIds != null)
                    foreach (JProperty obsolete in profile.Properties().Where(property => !declaredIds.Contains(property.Name)).ToArray())
                        obsolete.Remove();
                if (value == null)
                    profile.Remove(optionId);
                else
                    profile[optionId] = value;

                if (profile.Count > max_options)
                    throw new SkinOptionPreferenceBudgetException();
                if (profile.Count == 0)
                    profiles.Remove(key);
                else
                    profiles[key] = profile;
                write(profiles);
            }
        }

        public void Reset(Guid skinId, string rulesetId, int? keyCount = null)
        {
            lock (sync)
            {
                JObject profiles = read();
                profiles.Remove(profileKey(skinId, rulesetId, keyCount));
                write(profiles);
            }
        }

        private JObject read()
        {
            string value = configuration.Value;
            try
            {
                if (value.Length > max_bytes)
                    throw new JsonSerializationException();
                JObject profiles = JObject.Parse(value, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (profiles.Count > max_profiles)
                    throw new JsonSerializationException();

                foreach (JProperty property in profiles.Properties())
                {
                    string[] key = property.Name.Split('/');
                    if (key.Length is not (2 or 3) || !Guid.TryParseExact(key[0], "N", out _) || key[1] is not ("bms" or "mania")
                        || (key.Length == 3 && (!int.TryParse(key[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count)
                            || key[2] != count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            || !SkinSelectionScope.GetSupportedKeyCounts(key[1]).Contains(count)))
                        || property.Value is not JObject profile || profile.Count > max_options
                        || profile.Properties().Any(p => !token(p.Name) || p.Value.Type != JTokenType.String
                            || p.Value.Value<string>() is not { Length: <= GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH } text || text.Any(char.IsControl)))
                        throw new JsonSerializationException();
                }
                return profiles;
            }
            catch (JsonException)
            {
                // Leave malformed user configuration untouched on read. A deliberate edit replaces it.
                Logger.Log("Invalid skin option preferences; author defaults will be used.", level: LogLevel.Important);
                return new JObject();
            }
        }

        private void write(JObject profiles)
        {
            string value = profiles.ToString(Formatting.None);
            if (profiles.Count > max_profiles || value.Length > max_bytes)
                throw new SkinOptionPreferenceBudgetException();
            configuration.Value = value;
        }

        private static string profileKey(Guid id, string ruleset, int? keyCount)
            => id.ToString("N") + "/" + new SkinSelectionScope(ruleset, keyCount).PreferenceSuffix;

        private static bool token(string value)
            => value.Length is > 0 and <= GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-');
    }

    public sealed class SkinOptionPreferenceBudgetException : Exception
    {
        public SkinOptionPreferenceBudgetException() : base("The skin option preference budget is exhausted.") { }
    }
}
