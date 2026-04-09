// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using oms.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Database;
using osu.Game.Input.Bindings;

namespace osu.Game.Rulesets.Bms.Input
{
    public static class OmsBmsBindingResolver
    {
        private static readonly OmsBindingStore defaultBindingStore = new OmsBindingStore();

        public static IReadOnlyList<OmsBinding> GetBindingsOrDefault(RealmAccess? realmAccess, int variant)
        {
            variant = OmsBmsActionMap.NormalizeVariant(variant);

            if (realmAccess == null)
                return defaultBindingStore.GetDefaultBindings(variant);

            var supplementalBindings = OmsBmsBindingSettingsStorage.GetSupplementalBindings(realmAccess, variant);

            var standardBindings = realmAccess.Run(realm =>
            {
                var realmBindings = realm.All<RealmKeyBinding>()
                                       .Where(binding => binding.RulesetName == BmsRuleset.SHORT_NAME && binding.Variant == variant)
                                       .Detach()
                                       .ToArray();

                if (realmBindings.Length == 0)
                    return defaultBindingStore.GetDefaultBindings(variant).ToArray();

                return realmBindings.Select(binding => tryCreateBinding(binding, variant))
                                    .Where(binding => binding.HasValue)
                                    .Select(binding => binding!.Value)
                                    .ToArray();
            });

            return standardBindings.Concat(supplementalBindings).ToArray();
        }

        private static OmsBinding? tryCreateBinding(RealmKeyBinding binding, int variant)
        {
            if (!OmsBmsActionMap.TryMapToOmsAction(variant, (BmsAction)binding.ActionInt, out var action))
                return null;

            var keys = binding.KeyCombination.Keys.Where(key => key != InputKey.None).ToArray();

            if (keys.Length == 1 && OmsBindingStore.TryGetXInputButtonIndex(keys[0], out int buttonIndex))
                return new OmsBinding(action, OmsBindingTrigger.XInputButton(buttonIndex));

            if (keys.Any(key => OmsBindingStore.TryGetXInputButtonIndex(key, out _)))
                return null;

            return keys.Length > 0 ? new OmsBinding(action, OmsBindingTrigger.Keyboard(keys)) : null;
        }
    }
}
