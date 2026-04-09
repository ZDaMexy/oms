// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves XInput/joystick button transitions into <see cref="OmsAction"/> transitions.
    /// Alternate buttons for the same action share reference counting semantics so the
    /// action remains active until the final bound button is released.
    /// </summary>
    public class OmsXInputButtonInputHandler
    {
        private static readonly OmsAction[] empty_actions = Array.Empty<OmsAction>();

        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Dictionary<string, XInputButtonTrigger> xInputTriggersBySignature;
        private readonly Dictionary<int, string[]> triggerSignaturesByButton;
        private readonly HashSet<string> activeTriggerSignatures = new HashSet<string>();
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();

        public OmsXInputButtonInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;

            var xInputTriggers = bindings
                                 .SelectMany(binding => binding.XInputButtonTriggers.Select(trigger => new XInputButtonTrigger(binding.Action, trigger.ButtonIndex)))
                                 .GroupBy(trigger => trigger.Signature)
                                 .Select(group => group.First())
                                 .ToArray();

            xInputTriggersBySignature = xInputTriggers.ToDictionary(trigger => trigger.Signature);
            triggerSignaturesByButton = xInputTriggers
                                        .GroupBy(trigger => trigger.ButtonIndex)
                                        .ToDictionary(group => group.Key, group => group.Select(trigger => trigger.Signature).ToArray());
        }

        public IReadOnlyList<OmsAction> GetActionsForButton(int buttonIndex)
            => triggerSignaturesByButton.TryGetValue(buttonIndex, out var signatures)
                ? signatures.Select(signature => xInputTriggersBySignature[signature].Action).Distinct().ToArray()
                : empty_actions;

        public bool TriggerPressed(int buttonIndex)
            => updateButtonState(buttonIndex, pressed: true);

        public bool TriggerReleased(int buttonIndex)
            => updateButtonState(buttonIndex, pressed: false);

        private bool updateButtonState(int buttonIndex, bool pressed)
        {
            if (!triggerSignaturesByButton.TryGetValue(buttonIndex, out var signatures))
                return false;

            bool changed = false;

            foreach (var signature in signatures)
            {
                bool isActive = activeTriggerSignatures.Contains(signature);

                if (pressed)
                {
                    if (!activeTriggerSignatures.Add(signature))
                        continue;

                    var action = xInputTriggersBySignature[signature].Action;
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

                var releaseActionTarget = xInputTriggersBySignature[signature].Action;
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

        private readonly struct XInputButtonTrigger
        {
            public OmsAction Action { get; }

            public int ButtonIndex { get; }

            public string Signature => $"{(int)Action}:{ButtonIndex}";

            public XInputButtonTrigger(OmsAction action, int buttonIndex)
            {
                Action = action;
                ButtonIndex = buttonIndex;
            }
        }
    }
}
