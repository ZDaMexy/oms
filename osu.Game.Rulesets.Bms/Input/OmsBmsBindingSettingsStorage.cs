// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using oms.Input;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Database;

namespace osu.Game.Rulesets.Bms.Input
{
    public static class OmsBmsBindingSettingsStorage
    {
        private const string setting_key = "OmsInputBindingsJson";

        public static IReadOnlyList<OmsBinding> GetSupplementalBindings(RealmAccess? realmAccess, int variant)
        {
            if (realmAccess == null)
                return Array.Empty<OmsBinding>();

            variant = OmsBmsActionMap.NormalizeVariant(variant);

            string? json = realmAccess.Run(realm => realm.All<RealmRulesetSetting>()
                                                       .FirstOrDefault(setting => setting.RulesetName == BmsRuleset.SHORT_NAME && setting.Variant == variant && setting.Key == setting_key)
                                                       ?.Value);

            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<OmsBinding>();

            try
            {
                var persisted = JsonConvert.DeserializeObject<PersistedBindingCollection>(json);

                return persisted?.Bindings.Select(toBinding)
                               .Where(binding => binding.HasValue)
                               .Select(binding => binding!.Value)
                               .ToArray()
                       ?? Array.Empty<OmsBinding>();
            }
            catch (JsonException e)
            {
                Logger.Error(e, $"Failed to deserialize OMS input bindings for variant {variant}.");
                return Array.Empty<OmsBinding>();
            }
        }

        public static void SaveSupplementalBindings(RealmAccess? realmAccess, int variant, IEnumerable<OmsBinding> bindings)
        {
            if (realmAccess == null)
                return;

            variant = OmsBmsActionMap.NormalizeVariant(variant);

            var persisted = bindings.Select(toPersistedBinding)
                                    .Where(binding => binding != null)
                                    .ToArray();

            realmAccess.Write(realm =>
            {
                var setting = realm.All<RealmRulesetSetting>()
                                   .FirstOrDefault(candidate => candidate.RulesetName == BmsRuleset.SHORT_NAME && candidate.Variant == variant && candidate.Key == setting_key);

                if (persisted.Length == 0)
                {
                    if (setting != null)
                        realm.Remove(setting);

                    return;
                }

                string json = JsonConvert.SerializeObject(new PersistedBindingCollection
                {
                    Bindings = persisted!
                });

                if (setting == null)
                {
                    realm.Add(new RealmRulesetSetting
                    {
                        RulesetName = BmsRuleset.SHORT_NAME,
                        Variant = variant,
                        Key = setting_key,
                        Value = json,
                    });

                    return;
                }

                setting.Value = json;
            });
        }

        private static PersistedBinding? toPersistedBinding(OmsBinding binding)
        {
            var triggers = binding.HidButtonTriggers.Select(trigger => new PersistedTrigger
            {
                Kind = OmsBindingTriggerKind.HidButton,
                DeviceIdentifier = trigger.DeviceIdentifier ?? string.Empty,
                ButtonIndex = trigger.ButtonIndex,
            }).Concat(binding.HidAxisTriggers.Select(trigger => new PersistedTrigger
            {
                Kind = OmsBindingTriggerKind.HidAxis,
                DeviceIdentifier = trigger.DeviceIdentifier ?? string.Empty,
                AxisIndex = trigger.AxisIndex,
                AxisDirection = trigger.AxisDirection,
                AxisInverted = trigger.AxisInverted,
            })).Concat(binding.MouseAxisTriggers.Select(trigger => new PersistedTrigger
            {
                Kind = OmsBindingTriggerKind.MouseAxis,
                MouseAxis = trigger.MouseAxisKind,
                AxisDirection = trigger.AxisDirection,
                AxisInverted = trigger.AxisInverted,
            })).ToArray();

            return triggers.Length == 0
                ? null
                : new PersistedBinding
                {
                    Action = binding.Action,
                    Triggers = triggers,
                };
        }

        private static OmsBinding? toBinding(PersistedBinding persistedBinding)
        {
            var triggers = persistedBinding.Triggers.Select(toTrigger)
                                                   .Where(trigger => trigger.HasValue)
                                                   .Select(trigger => trigger!.Value)
                                                   .ToArray();

            return triggers.Length == 0
                ? null
                : new OmsBinding(persistedBinding.Action, triggers);
        }

        private static OmsBindingTrigger? toTrigger(PersistedTrigger persistedTrigger)
        {
            switch (persistedTrigger.Kind)
            {
                case OmsBindingTriggerKind.HidButton:
                    if (string.IsNullOrWhiteSpace(persistedTrigger.DeviceIdentifier) || persistedTrigger.ButtonIndex < 0)
                        return null;

                    return OmsBindingTrigger.HidButton(persistedTrigger.DeviceIdentifier, persistedTrigger.ButtonIndex);

                case OmsBindingTriggerKind.HidAxis:
                    if (string.IsNullOrWhiteSpace(persistedTrigger.DeviceIdentifier) || persistedTrigger.AxisIndex < 0)
                        return null;

                    return OmsBindingTrigger.HidAxis(persistedTrigger.DeviceIdentifier, persistedTrigger.AxisIndex, persistedTrigger.AxisDirection, persistedTrigger.AxisInverted);

                case OmsBindingTriggerKind.MouseAxis:
                    return OmsBindingTrigger.MouseAxis(persistedTrigger.MouseAxis, persistedTrigger.AxisDirection, persistedTrigger.AxisInverted);

                default:
                    return null;
            }
        }

        private sealed class PersistedBindingCollection
        {
            public PersistedBinding[] Bindings { get; set; } = Array.Empty<PersistedBinding>();
        }

        private sealed class PersistedBinding
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public OmsAction Action { get; set; }

            public PersistedTrigger[] Triggers { get; set; } = Array.Empty<PersistedTrigger>();
        }

        private sealed class PersistedTrigger
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public OmsBindingTriggerKind Kind { get; set; }

            public string DeviceIdentifier { get; set; } = string.Empty;

            public int ButtonIndex { get; set; } = -1;

            public int AxisIndex { get; set; } = -1;

            [JsonConverter(typeof(StringEnumConverter))]
            public OmsMouseAxis MouseAxis { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public OmsAxisDirection AxisDirection { get; set; } = OmsAxisDirection.Positive;

            public bool AxisInverted { get; set; }
        }
    }
}
