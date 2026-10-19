// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Input
{
    [Cached]
    public partial class BmsInputManager : RulesetInputManager<BmsAction>, IOmsKeyboardEventSink
    {
        public const int MAX_GAMEPLAY_KEYS = 16;

        public BindableBool LaneCoverFocusPressed { get; } = new BindableBool();

        public BindableBool PreStartHoldPressed { get; } = new BindableBool();

        public OmsInputRouter Router { get; } = new OmsInputRouter();

        private readonly HashSet<BmsAction> forwardedPressedActions = new HashSet<BmsAction>();
        private readonly object rawKeyboardGate = new object();
        private readonly Queue<RawKeyboardTransition> rawKeyboardTransitions = new Queue<RawKeyboardTransition>(32);
        private bool rawKeyboardClosed;
        private bool rawKeyboardBindingsSupported = true;
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
        }

        internal void ActivateRawKeyboard() => keyboardEventSource?.RegisterSink(this);

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

            if (!changed || !UseParentInput || !OmsBmsActionMap.TryMapToBmsAction(variant, action, out var bmsAction))
                return changed;

            if (!shouldForwardToGameplay(bmsAction))
                return changed;

            KeyBindingContainer.TriggerPressed(bmsAction);
            forwardedPressedActions.Add(bmsAction);

            return changed;
        }

        public bool TriggerOmsActionReleased(OmsAction action)
        {
            bool changed = Router.TriggerReleased(action);

            if (!changed || !UseParentInput || !OmsBmsActionMap.TryMapToBmsAction(variant, action, out var bmsAction))
                return changed;

            if (!forwardedPressedActions.Remove(bmsAction))
                return changed;

            KeyBindingContainer.TriggerReleased(bmsAction);

            return changed;
        }

        protected override void Update()
        {
            // Native window callbacks run on the input thread. Drain before the usual input processing so
            // the current gameplay frame owns both the judgement time and every drawable mutation.
            while (true)
            {
                RawKeyboardTransition transition;
                lock (rawKeyboardGate)
                {
                    if (!rawKeyboardTransitions.TryDequeue(out transition))
                        break;
                }

                if (transition.Pressed is bool pressed)
                    tryHandleKeyboardEvent(transition.Key, pressed);
                else
                    keyboardInputHandler.ReleaseAllPressed();
            }

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
        {
            KeyCombination combination = KeyCombination.FromKey(e.Key);
            return isOwnedRawKeyboardEvent(combination)
                   || tryHandleKeyboardEvent(combination, pressed: true)
                   || base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            KeyCombination combination = KeyCombination.FromKey(e.Key);
            if (!isOwnedRawKeyboardEvent(combination) && !tryHandleKeyboardEvent(combination, pressed: false))
                base.OnKeyUp(e);
        }

        private bool isOwnedRawKeyboardEvent(KeyCombination combination)
        {
            if (!rawKeyboardBindingsSupported || keyboardEventSource?.IsActiveFor(this) != true)
                return false;

            foreach (InputKey key in combination.Keys)
            {
                if (keyboardEventSource.SupportsKey(key) && keyboardInputHandler.HasBindingForKey(key))
                    return true;
            }

            return false;
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
                lock (rawKeyboardGate)
                {
                    rawKeyboardClosed = true;
                    rawKeyboardTransitions.Clear();
                }

                keyboardEventSource?.UnregisterSink(this);
                hidDeviceHandler?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        bool IOmsKeyboardEventSink.HandleRawKeyPressed(InputKey key) => enqueueRawKeyboardTransition(key, pressed: true);

        bool IOmsKeyboardEventSink.HandleRawKeyReleased(InputKey key) => enqueueRawKeyboardTransition(key, pressed: false);

        void IOmsKeyboardEventSink.ResetRawKeyboardState()
        {
            lock (rawKeyboardGate)
            {
                if (rawKeyboardClosed)
                    return;

                // Focus loss/pause ends this input epoch. Pending presses must not hit a note when gameplay
                // resumes; release only the keyboard state which has already reached the update thread.
                rawKeyboardTransitions.Clear();
                rawKeyboardTransitions.Enqueue(new RawKeyboardTransition(InputKey.None, null));
            }
        }

        private bool enqueueRawKeyboardTransition(InputKey key, bool pressed)
        {
            if (!rawKeyboardBindingsSupported || !keyboardInputHandler.HasBindingForKey(key))
                return false;

            lock (rawKeyboardGate)
            {
                if (rawKeyboardClosed)
                    return false;

                rawKeyboardTransitions.Enqueue(new RawKeyboardTransition(key, pressed));
            }

            return true;
        }

        private readonly record struct RawKeyboardTransition(InputKey Key, bool? Pressed);

        private void applyBindings(IReadOnlyList<OmsBinding> bindings)
        {
            // Keep an entire binding group on one source. Splitting a chord between queued native modifiers
            // and unsupported SDL keys can lose a complete tap before the two batches meet in this update.
            rawKeyboardBindingsSupported = keyboardEventSource == null || bindings.SelectMany(binding => binding.KeyboardCombinations)
                .SelectMany(combination => combination.Keys).Where(key => key != InputKey.None).All(keyboardEventSource.SupportsKey);
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
            if (!keyboardInputHandler.HasBindingForKey(key))
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
                LaneCoverFocusPressed.Value = true;

            if (action == OmsAction.UI_PreStartHold)
                PreStartHoldPressed.Value = true;
        }

        private void onOmsActionReleased(OmsAction action)
        {
            if (action == OmsAction.UI_LaneCoverFocus)
                LaneCoverFocusPressed.Value = false;

            if (action == OmsAction.UI_PreStartHold)
                PreStartHoldPressed.Value = false;
        }

        private bool shouldForwardToGameplay(BmsAction action)
            => action.IsLaneAction() && !PreStartHoldPressed.Value;

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

                return e.Action == BmsAction.LaneCoverFocus;
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

        [LocalisableDescription(typeof(BmsInputStrings), nameof(BmsInputStrings.CycleScrollAdjustmentTarget))]
        LaneCoverFocus,

        [LocalisableDescription(typeof(BmsInputStrings), nameof(BmsInputStrings.PreStartHold))]
        PreStartHold,
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

        public static int GetHiSpeedAdjustmentDirection(this BmsAction action, int variant)
        {
            variant = OmsBmsActionMap.NormalizeVariant(variant);

            return variant switch
            {
                6 => action switch
                {
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 => 1,
                    BmsAction.Key2 or BmsAction.Key4 => -1,
                    _ => 0,
                },
                8 => action switch
                {
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 or BmsAction.Key7 => 1,
                    BmsAction.Key2 or BmsAction.Key4 or BmsAction.Key6 => -1,
                    _ => 0,
                },
                9 => action switch
                {
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 or BmsAction.Key7 or BmsAction.Key9 => 1,
                    BmsAction.Key2 or BmsAction.Key4 or BmsAction.Key6 or BmsAction.Key8 => -1,
                    _ => 0,
                },
                16 => action switch
                {
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 or BmsAction.Key7 or BmsAction.Key8 or BmsAction.Key10 or BmsAction.Key12 or BmsAction.Key14 => 1,
                    BmsAction.Key2 or BmsAction.Key4 or BmsAction.Key6 or BmsAction.Key9 or BmsAction.Key11 or BmsAction.Key13 => -1,
                    _ => 0,
                },
                _ => 0,
            };
        }
    }
}
