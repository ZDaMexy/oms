// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves HID button transitions into <see cref="OmsAction"/> transitions.
    /// Multiple buttons may target the same action and will only emit a release
    /// callback after the final active button is released.
    /// </summary>
    public class OmsHidButtonInputHandler
    {
        private static readonly OmsAction[] empty_actions = Array.Empty<OmsAction>();

        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Dictionary<string, HidButtonTrigger> hidTriggersBySignature;
        private readonly Dictionary<string, string[]> triggerSignaturesByButton;
        private readonly HashSet<string> activeTriggerSignatures = new HashSet<string>();
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();

        public OmsHidButtonInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;

            var hidTriggers = bindings
                              .SelectMany(binding => binding.HidButtonTriggers.Select(trigger => new HidButtonTrigger(binding.Action, OmsHidDeviceIdentifier.Normalize(trigger.DeviceIdentifier!), trigger.ButtonIndex)))
                              .GroupBy(trigger => trigger.Signature)
                              .Select(group => group.First())
                              .ToArray();

            hidTriggersBySignature = hidTriggers.ToDictionary(trigger => trigger.Signature);
            triggerSignaturesByButton = hidTriggers
                                        .GroupBy(trigger => trigger.DeviceButtonSignature)
                                        .ToDictionary(group => group.Key, group => group.Select(trigger => trigger.Signature).ToArray());
        }

        public IReadOnlyList<OmsAction> GetActionsForButton(string deviceIdentifier, int buttonIndex)
            => triggerSignaturesByButton.TryGetValue(buildDeviceButtonSignature(deviceIdentifier, buttonIndex), out var signatures)
                ? signatures.Select(signature => hidTriggersBySignature[signature].Action).Distinct().ToArray()
                : empty_actions;

        public bool TriggerPressed(string deviceIdentifier, int buttonIndex)
            => updateButtonState(deviceIdentifier, buttonIndex, pressed: true);

        public bool TriggerReleased(string deviceIdentifier, int buttonIndex)
            => updateButtonState(deviceIdentifier, buttonIndex, pressed: false);

        private bool updateButtonState(string deviceIdentifier, int buttonIndex, bool pressed)
        {
            if (!triggerSignaturesByButton.TryGetValue(buildDeviceButtonSignature(deviceIdentifier, buttonIndex), out var signatures))
                return false;

            bool changed = false;

            foreach (var signature in signatures)
            {
                bool isActive = activeTriggerSignatures.Contains(signature);

                if (pressed)
                {
                    if (!activeTriggerSignatures.Add(signature))
                        continue;

                    var action = hidTriggersBySignature[signature].Action;
                    int count = actionPressCounts.GetValueOrDefault(action);
                    actionPressCounts[action] = count + 1;

                    if (count == 0)
                    {
                        pressAction(action);
                        changed = true;
                    }

                    continue;
                }

                if (!isActive)
                    continue;

                activeTriggerSignatures.Remove(signature);

                var releaseActionTarget = hidTriggersBySignature[signature].Action;
                int releaseCount = actionPressCounts.GetValueOrDefault(releaseActionTarget);

                if (releaseCount <= 1)
                {
                    actionPressCounts.Remove(releaseActionTarget);

                    if (releaseCount == 1)
                    {
                        releaseAction(releaseActionTarget);
                        changed = true;
                    }

                    continue;
                }

                actionPressCounts[releaseActionTarget] = releaseCount - 1;
            }

            return changed;
        }

        private static string buildDeviceButtonSignature(string deviceIdentifier, int buttonIndex)
            => $"{OmsHidDeviceIdentifier.Normalize(deviceIdentifier)}:{buttonIndex}";

        private readonly struct HidButtonTrigger
        {
            public OmsAction Action { get; }

            public string DeviceIdentifier { get; }

            public int ButtonIndex { get; }

            public string DeviceButtonSignature => buildDeviceButtonSignature(DeviceIdentifier, ButtonIndex);

            public string Signature => $"{(int)Action}:{DeviceButtonSignature}";

            public HidButtonTrigger(OmsAction action, string deviceIdentifier, int buttonIndex)
            {
                Action = action;
                DeviceIdentifier = deviceIdentifier;
                ButtonIndex = buttonIndex;
            }
        }
    }
}
