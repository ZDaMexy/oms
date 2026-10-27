// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Configuration
{
    internal class BmsModStatePersistence : IRulesetModStatePersistence
    {
        private readonly BmsRuleset ruleset;
        private readonly Bindable<string> storedState;
        private Bindable<IReadOnlyList<Mod>>? trackedSelectedMods;
        private ModSettingChangeTracker? changeTracker;

        private readonly Dictionary<string, APIMod> rememberedMods = new Dictionary<string, APIMod>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> selectedModAcronyms = new List<string>();

        public BmsModStatePersistence(BmsRuleset ruleset, BmsRulesetConfigManager config)
        {
            this.ruleset = ruleset;
            storedState = config.GetBindable<string>(BmsRulesetSetting.PersistedModState);

            var snapshot = ReadStoredState(storedState.Value);

            selectedModAcronyms.AddRange(snapshot.SelectedModAcronyms.Where(acronym => !IsLegacyReadingMod(acronym)));

            foreach (var mod in snapshot.RememberedMods)
            {
                if (!IsLegacyReadingMod(mod.Acronym))
                    rememberedMods[mod.Acronym] = mod;
            }
        }

        public void ApplyStoredState(Dictionary<ModType, IReadOnlyList<Mod>> availableMods)
        {
            foreach (var mod in flattenMods(availableMods))
            {
                if (!rememberedMods.TryGetValue(mod.Acronym, out var storedMod))
                    continue;

                if (storedMod.ToMod(ruleset) is not Mod restoredMod || restoredMod.GetType() != mod.GetType())
                    continue;

                mod.CopyCommonSettingsFrom(restoredMod);
            }
        }

        public IReadOnlyList<Mod>? GetStoredSelection(IReadOnlyDictionary<ModType, IReadOnlyList<Mod>> availableMods)
        {
            if (selectedModAcronyms.Count == 0)
                return null;

            var availableByAcronym = flattenMods(availableMods)
                                    .GroupBy(mod => mod.Acronym, StringComparer.OrdinalIgnoreCase)
                                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var restoredSelection = new List<Mod>();

            foreach (string acronym in selectedModAcronyms)
            {
                if (availableByAcronym.TryGetValue(acronym, out var mod))
                    restoredSelection.Add(mod);
            }

            return restoredSelection;
        }

        public void BindSelectedMods(IBindable<IReadOnlyList<Mod>> selectedMods)
        {
            trackedSelectedMods?.UnbindAll();
            trackedSelectedMods = (Bindable<IReadOnlyList<Mod>>)selectedMods.GetBoundCopy();
            trackedSelectedMods.BindValueChanged(onSelectedModsChanged, true);
        }

        public void Dispose()
        {
            trackedSelectedMods?.UnbindAll();
            trackedSelectedMods = null;

            changeTracker?.Dispose();
            changeTracker = null;
        }

        private void onSelectedModsChanged(ValueChangedEvent<IReadOnlyList<Mod>> mods)
        {
            if (mods.NewValue.Any(mod => IsLegacyReadingMod(mod.Acronym)))
            {
                // Old presets can still contain these mods. Change the live selection, never the preset or its reading settings.
                trackedSelectedMods!.Value = mods.NewValue.Where(mod => !IsLegacyReadingMod(mod.Acronym)).ToArray();
                return;
            }

            changeTracker?.Dispose();
            changeTracker = new ModSettingChangeTracker(mods.NewValue);
            changeTracker.SettingChanged += _ => saveCurrentState(mods.NewValue);

            saveCurrentState(mods.NewValue);
        }

        private void saveCurrentState(IReadOnlyList<Mod> selectedMods)
        {
            selectedModAcronyms.Clear();
            selectedModAcronyms.AddRange(selectedMods.Select(mod => mod.Acronym));

            foreach (var mod in selectedMods)
            {
                var storedMod = new APIMod(mod);

                if (storedMod.Settings.Count == 0)
                    rememberedMods.Remove(mod.Acronym);
                else
                    rememberedMods[mod.Acronym] = storedMod;
            }

            if (selectedModAcronyms.Count == 0 && rememberedMods.Count == 0)
            {
                storedState.Value = string.Empty;
                return;
            }

            storedState.Value = JsonConvert.SerializeObject(new PersistedState
            {
                SelectedModAcronyms = selectedModAcronyms.ToList(),
                RememberedMods = rememberedMods.Values.OrderBy(mod => mod.Acronym, StringComparer.OrdinalIgnoreCase).ToList(),
            });
        }

        private static IEnumerable<Mod> flattenMods(IReadOnlyDictionary<ModType, IReadOnlyList<Mod>> mods)
            => mods.Values.SelectMany(group => group).SelectMany(ModUtils.FlattenMod);

        internal static bool IsLegacyReadingMod(string acronym)
            => acronym.Equals("SUD", StringComparison.OrdinalIgnoreCase)
               || acronym.Equals("HID", StringComparison.OrdinalIgnoreCase)
               || acronym.Equals("LIFT", StringComparison.OrdinalIgnoreCase);

        internal static PersistedState ReadStoredState(string rawState)
        {
            if (string.IsNullOrWhiteSpace(rawState))
                return new PersistedState();

            try
            {
                var state = JsonConvert.DeserializeObject<PersistedState>(rawState) ?? new PersistedState();

                if (state.SelectedModAcronyms == null || state.RememberedMods == null
                    || state.SelectedModAcronyms.Any(acronym => acronym == null)
                    || state.RememberedMods.Any(mod => mod == null || mod.Acronym == null || mod.Settings == null))
                    throw new JsonSerializationException("The saved BMS mod state contains null collections or entries.");

                return state;
            }
            catch (JsonException exception)
            {
                Logger.Log($"The saved BMS mod state could not be read; defaults will be used. {exception.Message}", level: LogLevel.Important);
                return new PersistedState();
            }
        }

        internal class PersistedState
        {
            public List<string> SelectedModAcronyms { get; set; } = new List<string>();

            public List<APIMod> RememberedMods { get; set; } = new List<APIMod>();
        }
    }
}
