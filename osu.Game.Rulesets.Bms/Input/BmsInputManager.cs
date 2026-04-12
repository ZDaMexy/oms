// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Database;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Input
{
    [Cached]
    public partial class BmsInputManager : RulesetInputManager<BmsAction>, IOmsKeyboardEventSink
    {
        public const int MAX_GAMEPLAY_KEYS = 16;

        public BindableBool LaneCoverFocusPressed => laneCoverFocusPressed;

        public OmsInputRouter Router { get; } = new OmsInputRouter();

        private readonly BindableBool laneCoverFocusPressed = new BindableBool();
        private readonly int variant;
        private OmsKeyboardInputHandler keyboardInputHandler = null!;
        private OmsHidButtonInputHandler hidButtonInputHandler = null!;
        private OmsXInputButtonInputHandler xInputButtonInputHandler = null!;
        private OmsMouseAxisInputHandler mouseAxisInputHandler = null!;
        private OmsHidDeviceHandler? hidDeviceHandler;

        [Resolved(canBeNull: true)]
        private IOmsKeyboardEventSource? keyboardEventSource { get; set; }

        public BmsInputManager(RulesetInfo ruleset, int variant)
            : base(ruleset, variant, SimultaneousBindingMode.Unique)
        {
            this.variant = OmsBmsActionMap.NormalizeVariant(variant);
            applyBindings(OmsBmsBindingResolver.GetBindingsOrDefault(null, this.variant));

            Router.ActionPressed += onOmsActionPressed;
            Router.ActionReleased += onOmsActionReleased;

            AddInternal(new BmsActionRouterMirror(Router, this.variant));
        }

        [BackgroundDependencyLoader(true)]
        private void load(RealmAccess? realmAccess)
        {
            applyBindings(OmsBmsBindingResolver.GetBindingsOrDefault(realmAccess, variant));
            keyboardEventSource?.RegisterSink(this);
        }

        public bool TriggerKeyPressed(InputKey key) => keyboardInputHandler.TriggerPressed(key);

        public bool TriggerKeyReleased(InputKey key) => keyboardInputHandler.TriggerReleased(key);

        public bool TriggerHidButtonPressed(string deviceIdentifier, int buttonIndex) => hidButtonInputHandler.TriggerPressed(deviceIdentifier, buttonIndex);

        public bool TriggerHidButtonReleased(string deviceIdentifier, int buttonIndex) => hidButtonInputHandler.TriggerReleased(deviceIdentifier, buttonIndex);

        public bool TriggerXInputButtonPressed(int buttonIndex) => xInputButtonInputHandler.TriggerPressed(buttonIndex);

        public bool TriggerXInputButtonReleased(int buttonIndex) => xInputButtonInputHandler.TriggerReleased(buttonIndex);

        public bool TriggerMouseAxisDelta(OmsMouseAxis axis, float delta) => mouseAxisInputHandler.ApplyAxisDelta(axis, delta);

        public bool TriggerOmsActionPressed(OmsAction action)
        {
            bool changed = Router.TriggerPressed(action);

            if (changed && OmsBmsActionMap.TryMapToBmsAction(variant, action, out var bmsAction))
                KeyBindingContainer.TriggerPressed(bmsAction);

            return changed;
        }

        public bool TriggerOmsActionReleased(OmsAction action)
        {
            bool changed = Router.TriggerReleased(action);

            if (changed && OmsBmsActionMap.TryMapToBmsAction(variant, action, out var bmsAction))
                KeyBindingContainer.TriggerReleased(bmsAction);

            return changed;
        }

        protected override void Update()
        {
            mouseAxisInputHandler.FinishFrame();
            mouseAxisInputHandler.BeginFrame();
            hidDeviceHandler?.PollOnce();
            base.Update();
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (e.Delta.X != 0)
                TriggerMouseAxisDelta(OmsMouseAxis.X, e.Delta.X);

            if (e.Delta.Y != 0)
                TriggerMouseAxisDelta(OmsMouseAxis.Y, e.Delta.Y);

            return base.OnMouseMove(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
            => tryHandleKeyboardEvent(KeyCombination.FromKey(e.Key), pressed: true) || base.OnKeyDown(e);

        protected override void OnKeyUp(KeyUpEvent e)
        {
            if (!tryHandleKeyboardEvent(KeyCombination.FromKey(e.Key), pressed: false))
                base.OnKeyUp(e);
        }

        protected override bool OnJoystickPress(JoystickPressEvent e)
            => TriggerXInputButtonPressed((int)e.Button) || base.OnJoystickPress(e);

        protected override void OnJoystickRelease(JoystickReleaseEvent e)
        {
            if (!TriggerXInputButtonReleased((int)e.Button))
                base.OnJoystickRelease(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                keyboardEventSource?.UnregisterSink(this);
                hidDeviceHandler?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        bool IOmsKeyboardEventSink.HandleRawKeyPressed(InputKey key) => tryHandleKeyboardEvent(key, pressed: true);

        bool IOmsKeyboardEventSink.HandleRawKeyReleased(InputKey key) => tryHandleKeyboardEvent(key, pressed: false);

        void IOmsKeyboardEventSink.ResetRawKeyboardState() => keyboardInputHandler.ReleaseAllPressed();

        private void applyBindings(IReadOnlyList<OmsBinding> bindings)
        {
            keyboardInputHandler = new OmsKeyboardInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            hidButtonInputHandler = new OmsHidButtonInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            xInputButtonInputHandler = new OmsXInputButtonInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            mouseAxisInputHandler = new OmsMouseAxisInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));

            hidDeviceHandler?.Dispose();
            hidDeviceHandler = new OmsHidDeviceHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
        }

        private bool tryHandleKeyboardEvent(IEnumerable<InputKey> keys, bool pressed)
        {
            bool handled = false;

            foreach (var key in keys)
                handled |= tryHandleKeyboardEvent(key, pressed);

            return handled;
        }

        private bool tryHandleKeyboardEvent(InputKey key, bool pressed)
        {
            if (key == InputKey.None || keyboardInputHandler.GetActionsForKey(key).Count == 0)
                return false;

            if (pressed)
                keyboardInputHandler.TriggerPressed(key);
            else
                keyboardInputHandler.TriggerReleased(key);

            return true;
        }

        private bool tryHandleKeyboardEvent(KeyCombination keyCombination, bool pressed)
            => tryHandleKeyboardEvent(keyCombination.Keys, pressed);

        private bool tryHandleKeyboardEvent(IReadOnlyList<InputKey> keys, bool pressed)
        {
            bool handled = false;

            foreach (var key in keys)
            {
                handled |= tryHandleKeyboardEvent(key, pressed);
            }

            return handled;
        }

        private void onOmsActionPressed(OmsAction action)
        {
            if (action == OmsAction.UI_LaneCoverFocus)
                laneCoverFocusPressed.Value = true;
        }

        private void onOmsActionReleased(OmsAction action)
        {
            if (action == OmsAction.UI_LaneCoverFocus)
                laneCoverFocusPressed.Value = false;
        }

        private partial class BmsActionRouterMirror : Component, IKeyBindingHandler<BmsAction>
        {
            private readonly OmsInputRouter router;
            private readonly int variant;

            public BmsActionRouterMirror(OmsInputRouter router, int variant)
            {
                this.router = router;
                this.variant = variant;
            }

            public bool OnPressed(KeyBindingPressEvent<BmsAction> e)
            {
                if (OmsBmsActionMap.TryMapToOmsAction(variant, e.Action, out var action))
                    router.TriggerPressed(action);

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
            {
                if (OmsBmsActionMap.TryMapToOmsAction(variant, e.Action, out var action))
                    router.TriggerReleased(action);
            }
        }
    }

    public enum BmsAction
    {
        [System.ComponentModel.Description("Scratch 1")]
        Scratch1,

        [System.ComponentModel.Description("Key 1")]
        Key1,

        [System.ComponentModel.Description("Key 2")]
        Key2,

        [System.ComponentModel.Description("Key 3")]
        Key3,

        [System.ComponentModel.Description("Key 4")]
        Key4,

        [System.ComponentModel.Description("Key 5")]
        Key5,

        [System.ComponentModel.Description("Key 6")]
        Key6,

        [System.ComponentModel.Description("Key 7")]
        Key7,

        [System.ComponentModel.Description("Scratch 2")]
        Scratch2,

        [System.ComponentModel.Description("Key 8")]
        Key8,

        [System.ComponentModel.Description("Key 9")]
        Key9,

        [System.ComponentModel.Description("Key 10")]
        Key10,

        [System.ComponentModel.Description("Key 11")]
        Key11,

        [System.ComponentModel.Description("Key 12")]
        Key12,

        [System.ComponentModel.Description("Key 13")]
        Key13,

        [System.ComponentModel.Description("Key 14")]
        Key14,

        [System.ComponentModel.Description("Lane cover focus")]
        LaneCoverFocus,
    }

    public static class BmsActionExtensions
    {
        public static bool IsLaneAction(this BmsAction action)
            => action >= BmsAction.Scratch1 && action <= BmsAction.Key14;

        public static BmsAction GetScratchAction(int scratchIndex)
        {
            return scratchIndex switch
            {
                0 => BmsAction.Scratch1,
                1 => BmsAction.Scratch2,
                _ => throw new ArgumentOutOfRangeException(nameof(scratchIndex), scratchIndex, "Only dual-scratch BMS layouts are currently supported."),
            };
        }

        public static BmsAction GetKeyAction(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= BmsInputManager.MAX_GAMEPLAY_KEYS - 2)
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, $"Only {BmsInputManager.MAX_GAMEPLAY_KEYS - 2} BMS key buttons are currently supported.");

            if (keyIndex < 7)
                return (BmsAction)((int)BmsAction.Key1 + keyIndex);

            return (BmsAction)((int)BmsAction.Key8 + keyIndex - 7);
        }

        public static BmsAction GetLaneAction(int laneIndex, bool isScratch)
        {
            if (isScratch)
                return laneIndex == 0 ? BmsAction.Scratch1 : BmsAction.Scratch2;

            int keyIndex = laneIndex;

            if (laneIndex > 0)
                keyIndex--;

            return GetKeyAction(keyIndex);
        }
    }
}
