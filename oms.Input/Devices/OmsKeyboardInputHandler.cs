// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves keyboard key transitions into <see cref="OmsAction"/> transitions.
    /// This handler supports alternate bindings for the same action and only emits
    /// release callbacks after the final bound key is released.
    /// </summary>
    public class OmsKeyboardInputHandler
    {
        private static readonly OmsAction[] empty_actions = Array.Empty<OmsAction>();

        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Dictionary<string, KeyboardTrigger> keyboardTriggersBySignature;
        private readonly Dictionary<InputKey, string[]> triggerSignaturesByKey;
        private readonly HashSet<string> activeTriggerSignatures = new HashSet<string>();
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();
        private readonly HashSet<InputKey> pressedKeys = new HashSet<InputKey>();

        public OmsKeyboardInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;

            var keyboardTriggers = bindings
                                   .SelectMany(binding => binding.KeyboardCombinations
                                                               .Where(combination => combination.Keys.Length > 0 && combination.Keys[0] != InputKey.None)
                                                               .Select(combination => new KeyboardTrigger(binding.Action, combination)))
                                   .GroupBy(trigger => trigger.Signature)
                                   .Select(group => group.First())
                                   .ToArray();

            keyboardTriggersBySignature = keyboardTriggers.ToDictionary(trigger => trigger.Signature);
            triggerSignaturesByKey = keyboardTriggers
                                     .SelectMany(trigger => trigger.KeyCombination.Keys
                                                                   .Where(key => key != InputKey.None)
                                                                   .SelectMany(indexKeys)
                                                                   .Select(key => (key, trigger.Signature)))
                                     .GroupBy(tuple => tuple.key)
                                     .ToDictionary(group => group.Key, group => group.Select(tuple => tuple.Signature).Distinct().ToArray());
        }

        public IReadOnlyList<OmsAction> GetActionsForKey(InputKey key)
            => triggerSignaturesByKey.TryGetValue(key, out string[]? signatures)
                ? signatures.Select(signature => keyboardTriggersBySignature[signature].Action).Distinct().ToArray()
                : empty_actions;

        public bool HasBindingForKey(InputKey key) => triggerSignaturesByKey.ContainsKey(key);

        public bool TriggerPressed(InputKey key)
        {
            if (!pressedKeys.Add(key))
                return false;

            return updateKeyState(key, pressed: true);
        }

        public bool TriggerReleased(InputKey key)
        {
            if (!pressedKeys.Remove(key))
                return false;

            return updateKeyState(key, pressed: false);
        }

        public bool ReleaseAllPressed()
        {
            if (pressedKeys.Count == 0)
                return false;

            bool changed = false;

            foreach (var key in pressedKeys.ToArray())
                changed |= TriggerReleased(key);

            return changed;
        }

        private bool updateKeyState(InputKey key, bool pressed)
        {
            if (!triggerSignaturesByKey.TryGetValue(key, out string[]? signatures))
                return false;

            bool changed = false;

            foreach (string signature in signatures)
            {
                var trigger = keyboardTriggersBySignature[signature];
                bool isActive = activeTriggerSignatures.Contains(signature);
                bool shouldBeActive = true;
                foreach (var combinationKey in trigger.KeyCombination.Keys)
                {
                    if (isPressed(combinationKey))
                        continue;

                    shouldBeActive = false;
                    break;
                }

                if (isActive == shouldBeActive)
                    continue;

                int count = actionPressCounts.GetValueOrDefault(trigger.Action);

                if (shouldBeActive)
                {
                    activeTriggerSignatures.Add(signature);
                    actionPressCounts[trigger.Action] = count + 1;

                    if (count == 0)
                    {
                        pressAction(trigger.Action);
                        changed = true;
                    }

                    continue;
                }

                activeTriggerSignatures.Remove(signature);

                if (count <= 1)
                {
                    actionPressCounts.Remove(trigger.Action);

                    if (count == 1)
                    {
                        releaseAction(trigger.Action);
                        changed = true;
                    }

                    continue;
                }

                actionPressCounts[trigger.Action] = count - 1;
            }

            return changed;
        }

        private bool isPressed(InputKey key)
            => pressedKeys.Contains(key) || key switch
            {
                InputKey.Control => pressedKeys.Contains(InputKey.LControl) || pressedKeys.Contains(InputKey.RControl),
                InputKey.Shift => pressedKeys.Contains(InputKey.LShift) || pressedKeys.Contains(InputKey.RShift),
                InputKey.Alt => pressedKeys.Contains(InputKey.LAlt) || pressedKeys.Contains(InputKey.RAlt),
                InputKey.Super => pressedKeys.Contains(InputKey.LSuper) || pressedKeys.Contains(InputKey.RSuper),
                _ => false,
            };

        private static IEnumerable<InputKey> indexKeys(InputKey key)
        {
            yield return key;
            switch (key)
            {
                case InputKey.Control:
                    yield return InputKey.LControl;
                    yield return InputKey.RControl;
                    break;

                case InputKey.Shift:
                    yield return InputKey.LShift;
                    yield return InputKey.RShift;
                    break;

                case InputKey.Alt:
                    yield return InputKey.LAlt;
                    yield return InputKey.RAlt;
                    break;

                case InputKey.Super:
                    yield return InputKey.LSuper;
                    yield return InputKey.RSuper;
                    break;
            }
        }

        private readonly struct KeyboardTrigger
        {
            public OmsAction Action { get; }

            public KeyCombination KeyCombination { get; }

            public string Signature { get; }

            public KeyboardTrigger(OmsAction action, KeyCombination keyCombination)
            {
                Action = action;
                KeyCombination = keyCombination;
                Signature = $"{(int)action}:{string.Join(",", keyCombination.Keys.OrderBy(key => key).Select(key => (int)key))}";
            }
        }
    }
}
