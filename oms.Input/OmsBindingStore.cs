// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;

namespace oms.Input
{
    /// <summary>
    /// Default OMS input bindings grouped by gameplay profile.
    /// User remaps continue to persist through lazer's databased keybinding store;
    /// this class provides the profile-aware default source of truth for OMS actions.
    /// </summary>
    public class OmsBindingStore
    {
        private static readonly OmsBinding[] default_5k_bindings =
        {
            new OmsBinding(OmsAction.Key1P_Scratch,
                OmsBindingTrigger.Keyboard(InputKey.Q),
                OmsBindingTrigger.Keyboard(InputKey.A),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadLeftShoulder)),
            new OmsBinding(OmsAction.Key1P_1,
                OmsBindingTrigger.Keyboard(InputKey.Z),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadX)),
            new OmsBinding(OmsAction.Key1P_2,
                OmsBindingTrigger.Keyboard(InputKey.S),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadA)),
            new OmsBinding(OmsAction.Key1P_3,
                OmsBindingTrigger.Keyboard(InputKey.X),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadB)),
            new OmsBinding(OmsAction.Key1P_4,
                OmsBindingTrigger.Keyboard(InputKey.D),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadY)),
            new OmsBinding(OmsAction.Key1P_5,
                OmsBindingTrigger.Keyboard(InputKey.C),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadRightShoulder)),
            new OmsBinding(OmsAction.UI_LaneCoverFocus, InputKey.Tab),
        };

        private static readonly OmsBinding[] default_7k_bindings =
        {
            new OmsBinding(OmsAction.Key1P_Scratch,
                OmsBindingTrigger.Keyboard(InputKey.Q),
                OmsBindingTrigger.Keyboard(InputKey.A),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadLeftShoulder)),
            new OmsBinding(OmsAction.Key1P_1,
                OmsBindingTrigger.Keyboard(InputKey.Z),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadX)),
            new OmsBinding(OmsAction.Key1P_2,
                OmsBindingTrigger.Keyboard(InputKey.S),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadA)),
            new OmsBinding(OmsAction.Key1P_3,
                OmsBindingTrigger.Keyboard(InputKey.X),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadB)),
            new OmsBinding(OmsAction.Key1P_4,
                OmsBindingTrigger.Keyboard(InputKey.D),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadY)),
            new OmsBinding(OmsAction.Key1P_5,
                OmsBindingTrigger.Keyboard(InputKey.C),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadRightShoulder)),
            new OmsBinding(OmsAction.Key1P_6,
                OmsBindingTrigger.Keyboard(InputKey.F),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadLeftTrigger)),
            new OmsBinding(OmsAction.Key1P_7,
                OmsBindingTrigger.Keyboard(InputKey.V),
                OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadRightTrigger)),
            new OmsBinding(OmsAction.UI_LaneCoverFocus, InputKey.Tab),
        };

        private static readonly OmsBinding[] default_9k_bindings =
        {
            new OmsBinding(OmsAction.Key9K_1, InputKey.A),
            new OmsBinding(OmsAction.Key9K_2, InputKey.S),
            new OmsBinding(OmsAction.Key9K_3, InputKey.D),
            new OmsBinding(OmsAction.Key9K_4, InputKey.F),
            new OmsBinding(OmsAction.Key9K_5, InputKey.Space),
            new OmsBinding(OmsAction.Key9K_6, InputKey.J),
            new OmsBinding(OmsAction.Key9K_7, InputKey.K),
            new OmsBinding(OmsAction.Key9K_8, InputKey.L),
            new OmsBinding(OmsAction.Key9K_9, InputKey.Semicolon),
            new OmsBinding(OmsAction.UI_LaneCoverFocus, InputKey.Tab),
        };

        private static readonly OmsBinding[] default_14k_bindings =
        {
            new OmsBinding(OmsAction.Key1P_Scratch, InputKey.Q, InputKey.A),
            new OmsBinding(OmsAction.Key1P_1, InputKey.Z),
            new OmsBinding(OmsAction.Key1P_2, InputKey.S),
            new OmsBinding(OmsAction.Key1P_3, InputKey.X),
            new OmsBinding(OmsAction.Key1P_4, InputKey.D),
            new OmsBinding(OmsAction.Key1P_5, InputKey.C),
            new OmsBinding(OmsAction.Key1P_6, InputKey.F),
            new OmsBinding(OmsAction.Key1P_7, InputKey.V),
            new OmsBinding(OmsAction.Key2P_Scratch, InputKey.P, InputKey.Semicolon),
            new OmsBinding(OmsAction.Key2P_1, InputKey.N),
            new OmsBinding(OmsAction.Key2P_2, InputKey.J),
            new OmsBinding(OmsAction.Key2P_3, InputKey.M),
            new OmsBinding(OmsAction.Key2P_4, InputKey.K),
            new OmsBinding(OmsAction.Key2P_5, InputKey.Comma),
            new OmsBinding(OmsAction.Key2P_6, InputKey.L),
            new OmsBinding(OmsAction.Key2P_7, InputKey.Period),
            new OmsBinding(OmsAction.UI_LaneCoverFocus, InputKey.Tab),
        };

        public virtual IReadOnlyList<OmsBinding> GetDefaultBindings(int variant)
            => variant switch
            {
                6 => default_5k_bindings,
                8 => default_7k_bindings,
                9 => default_9k_bindings,
                16 => default_14k_bindings,
                _ => default_7k_bindings,
            };

        public static bool TryGetJoystickButtonInputKey(int buttonIndex, out InputKey inputKey)
        {
            if (buttonIndex < 1 || buttonIndex > 64)
            {
                inputKey = InputKey.None;
                return false;
            }

            inputKey = (InputKey)((int)InputKey.Joystick1 + buttonIndex - 1);
            return true;
        }

        public static bool TryGetXInputButtonIndex(InputKey inputKey, out int buttonIndex)
        {
            int rawValue = (int)inputKey;
            int firstJoystickValue = (int)InputKey.Joystick1;
            int lastJoystickValue = (int)InputKey.Joystick64;

            if (rawValue < firstJoystickValue || rawValue > lastJoystickValue)
            {
                buttonIndex = 0;
                return false;
            }

            buttonIndex = rawValue - firstJoystickValue + 1;
            return true;
        }
    }

    public enum OmsBindingTriggerKind
    {
        Keyboard,
        HidButton,
        HidAxis,
        MouseAxis,
        XInputButton,
    }

    public enum OmsAxisDirection
    {
        Negative = -1,
        Positive = 1,
    }

    public enum OmsMouseAxis
    {
        X,
        Y,
    }

    public readonly struct OmsBindingTrigger
    {
        public OmsBindingTriggerKind Kind { get; }

        public KeyCombination KeyCombination { get; }

        public string? DeviceIdentifier { get; }

        public int ButtonIndex { get; }

        public int AxisIndex { get; }

        public OmsMouseAxis MouseAxisKind { get; }

        public OmsAxisDirection AxisDirection { get; }

        public bool AxisInverted { get; }

        private OmsBindingTrigger(
            OmsBindingTriggerKind kind,
            KeyCombination keyCombination = default,
            string? deviceIdentifier = null,
            int buttonIndex = 0,
            int axisIndex = 0,
            OmsMouseAxis mouseAxis = default,
            OmsAxisDirection axisDirection = OmsAxisDirection.Positive,
            bool axisInverted = false)
        {
            Kind = kind;
            KeyCombination = keyCombination;
            DeviceIdentifier = deviceIdentifier;
            ButtonIndex = buttonIndex;
            AxisIndex = axisIndex;
            MouseAxisKind = mouseAxis;
            AxisDirection = axisDirection;
            AxisInverted = axisInverted;
        }

        public static OmsBindingTrigger Keyboard(params InputKey[] keys)
        {
            var filteredKeys = keys.Where(key => key != InputKey.None).ToArray();
            return new OmsBindingTrigger(OmsBindingTriggerKind.Keyboard, filteredKeys.Length > 0 ? new KeyCombination(filteredKeys) : new KeyCombination(InputKey.None));
        }

        public static OmsBindingTrigger HidButton(string deviceIdentifier, int buttonIndex)
            => new OmsBindingTrigger(OmsBindingTriggerKind.HidButton, deviceIdentifier: deviceIdentifier, buttonIndex: buttonIndex);

        public static OmsBindingTrigger HidAxis(string deviceIdentifier, int axisIndex, OmsAxisDirection direction, bool axisInverted = false)
            => new OmsBindingTrigger(OmsBindingTriggerKind.HidAxis, deviceIdentifier: deviceIdentifier, axisIndex: axisIndex, axisDirection: direction, axisInverted: axisInverted);

        public static OmsBindingTrigger MouseAxis(OmsMouseAxis axis, OmsAxisDirection direction, bool axisInverted = false)
            => new OmsBindingTrigger(OmsBindingTriggerKind.MouseAxis, mouseAxis: axis, axisDirection: direction, axisInverted: axisInverted);

        public static OmsBindingTrigger XInputButton(int buttonIndex)
            => new OmsBindingTrigger(OmsBindingTriggerKind.XInputButton, buttonIndex: buttonIndex);
    }

    public readonly struct OmsBinding
    {
        public OmsAction Action { get; }

        public OmsBindingTrigger[] Triggers { get; }

        public KeyCombination[] KeyboardCombinations
            => Triggers.Where(trigger => trigger.Kind == OmsBindingTriggerKind.Keyboard)
                       .Select(trigger => trigger.KeyCombination)
                       .ToArray();

        public InputKey[] Keys
            => KeyboardCombinations.Where(combination => combination.Keys.Length == 1)
                                   .Select(combination => combination.Keys[0])
                                   .ToArray();

        public OmsBindingTrigger[] HidButtonTriggers
            => Triggers.Where(trigger => trigger.Kind == OmsBindingTriggerKind.HidButton && !string.IsNullOrWhiteSpace(trigger.DeviceIdentifier))
                       .ToArray();

        public OmsBindingTrigger[] HidAxisTriggers
            => Triggers.Where(trigger => trigger.Kind == OmsBindingTriggerKind.HidAxis && !string.IsNullOrWhiteSpace(trigger.DeviceIdentifier))
                       .ToArray();

        public OmsBindingTrigger[] MouseAxisTriggers
            => Triggers.Where(trigger => trigger.Kind == OmsBindingTriggerKind.MouseAxis)
                       .ToArray();

        public OmsBindingTrigger[] XInputButtonTriggers
            => Triggers.Where(trigger => trigger.Kind == OmsBindingTriggerKind.XInputButton && trigger.ButtonIndex >= 0)
                       .ToArray();

        public OmsBinding(OmsAction action, params InputKey[] keys)
            : this(action, keys.Where(key => key != InputKey.None)
                               .Select(key => OmsBindingTrigger.Keyboard(key))
                               .ToArray())
        {
        }

        public OmsBinding(OmsAction action, params OmsBindingTrigger[] triggers)
        {
            Action = action;
            Triggers = triggers.Where(trigger =>
            {
                if (trigger.Kind != OmsBindingTriggerKind.Keyboard)
                    return true;

                return trigger.KeyCombination.Keys.Length > 0 && trigger.KeyCombination.Keys[0] != InputKey.None;
            }).ToArray();
        }
    }
}
