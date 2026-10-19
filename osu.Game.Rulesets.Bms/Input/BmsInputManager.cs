// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Bms.Input
{
    [Cached]
    public partial class BmsInputManager : RulesetInputManager<BmsAction>, IOmsKeyboardEventSink, IFrameStableInputHandler
    {
        public const int MAX_GAMEPLAY_KEYS = 16;

        public BindableBool LaneCoverFocusPressed { get; } = new BindableBool();

        public BindableBool PreStartHoldPressed { get; } = new BindableBool();

        public OmsInputRouter Router { get; } = new OmsInputRouter();

        private readonly HashSet<BmsAction> forwardedPressedActions = new HashSet<BmsAction>();
        private readonly object rawKeyboardGate = new object();
        private readonly Queue<RawKeyboardTransition> rawKeyboardTransitions = new Queue<RawKeyboardTransition>(32);
        private readonly Queue<TimedKeyboardTransition> timedKeyboardTransitions = new Queue<TimedKeyboardTransition>(32);
        private GameplayInputTime? previousInputTime;
        private long? preparedClockEpoch;
        private bool usesTimestampedInput;
        private bool releaseKeyboardOnUpdate;
        private int rawKeyboardEpoch;
        private int preparedRawKeyboardEpoch;
        private double lastPreparedInputTime;
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
            // A native reset can invalidate an already prepared batch between catch-up substeps.
            // Only this thread owns the mapped queue and the applied keyboard state.
            if (usesTimestampedInput && preparedRawKeyboardEpoch != Volatile.Read(ref rawKeyboardEpoch))
            {
                timedKeyboardTransitions.Clear();
                releaseKeyboardOnUpdate = true;
            }

            if (releaseKeyboardOnUpdate)
            {
                releaseKeyboardOnUpdate = false;
                keyboardInputHandler.ReleaseAllPressed();
            }

            if (usesTimestampedInput)
            {
                while (timedKeyboardTransitions.TryPeek(out TimedKeyboardTransition transition) && transition.Time <= Time.Current)
                {
                    if (preparedRawKeyboardEpoch != Volatile.Read(ref rawKeyboardEpoch))
                        break;

                    timedKeyboardTransitions.Dequeue();
                    tryHandleKeyboardEvent(transition.Key, transition.Pressed);
                }
            }
            else
            {
                // Non-gameplay previews without a timestamp authority retain the ordinary update-thread
                // path. Freeze its size so a producer cannot extend the batch while it is being delivered.
                int count;
                lock (rawKeyboardGate)
                    count = rawKeyboardTransitions.Count;

                while (count-- > 0)
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
            }

            mouseAxisInputHandler.FinishFrame();
            mouseAxisInputHandler.BeginFrame();
            hidDeviceHandler?.PollOnce();
            base.Update();
        }

        double? IFrameStableInputHandler.NextInputTime => usesTimestampedInput
                                                        && preparedRawKeyboardEpoch == Volatile.Read(ref rawKeyboardEpoch)
                                                        && timedKeyboardTransitions.TryPeek(out TimedKeyboardTransition transition)
            ? transition.Time
            : null;

        void IFrameStableInputHandler.ResetInput() => resetRawKeyboardState();

        void IFrameStableInputHandler.PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput)
        {
            usesTimestampedInput = timeSource != null;

            if (!isLiveInput || !UseParentInput)
            {
                lock (rawKeyboardGate)
                {
                    rawKeyboardTransitions.Clear();
                    preparedRawKeyboardEpoch = rawKeyboardEpoch;
                }

                timedKeyboardTransitions.Clear();
                previousInputTime = null;
                releaseKeyboardOnUpdate = true;
                return;
            }

            if (timeSource?.InputTime is not GameplayInputTime sample)
                return;

            lock (rawKeyboardGate)
            {
                bool clockChanged = preparedClockEpoch is long epoch && epoch != sample.Epoch;
                if (preparedRawKeyboardEpoch != rawKeyboardEpoch || clockChanged)
                {
                    timedKeyboardTransitions.Clear();
                    previousInputTime = null;
                    releaseKeyboardOnUpdate = true;
                }

                preparedClockEpoch = sample.Epoch;
                preparedRawKeyboardEpoch = rawKeyboardEpoch;

                if (sample.Time < currentTime)
                {
                    // Let the existing subtree finish restoring a backwards seek before applying new live
                    // edges. They remain raw until the next outer frame, then late edges clamp to that
                    // restored timeline rather than acting on the old hit-object results during rewind.
                    previousInputTime = sample;
                    return;
                }

                lastPreparedInputTime = timedKeyboardTransitions.Count == 0 ? currentTime : Math.Max(currentTime, lastPreparedInputTime);

                int count = rawKeyboardTransitions.Count;

                while (count-- > 0 && rawKeyboardTransitions.TryPeek(out RawKeyboardTransition transition))
                {
                    if (transition.Pressed == null)
                    {
                        rawKeyboardTransitions.Dequeue();
                        releaseKeyboardOnUpdate = true;
                        continue;
                    }

                    // A callback received after this clock sample belongs to the next outer frame. Leave it
                    // queued rather than extrapolating gameplay into a time the clock has not published yet.
                    if (transition.Timestamp > sample.Timestamp)
                        break;

                    rawKeyboardTransitions.Dequeue();

                    if (transition.Timestamp < sample.EpochStartTimestamp)
                        continue;

                    double inputTime = mapInputTime(transition.Timestamp, sample);
                    // Late callbacks cannot rewind a judgement already exposed to gameplay. Keeping receipt
                    // order also preserves modifier/chord transitions when several events share a timestamp.
                    lastPreparedInputTime = Math.Max(lastPreparedInputTime, inputTime);
                    timedKeyboardTransitions.Enqueue(new TimedKeyboardTransition(transition.Key, transition.Pressed.Value, lastPreparedInputTime));
                }

                previousInputTime = sample;
            }
        }

        private double mapInputTime(long timestamp, GameplayInputTime sample)
        {
            if (previousInputTime is GameplayInputTime previous && previous.Epoch == sample.Epoch
                && previous.Timestamp < sample.Timestamp && timestamp >= previous.Timestamp
                && previous.IsRunning && sample.IsRunning && sample.Time >= previous.Time)
            {
                // Both endpoints come from the same offset/rate-adjusted clock. The interval follows its
                // actual advance, including continuous rate changes and interpolation recovery, rather than
                // applying today's rate retroactively to all events received during a stalled frame.
                double fraction = (double)(timestamp - previous.Timestamp) / (sample.Timestamp - previous.Timestamp);
                return previous.Time + (sample.Time - previous.Time) * fraction;
            }

            return sample.IsRunning ? sample.Time + (timestamp - sample.Timestamp) * (1000.0 / Stopwatch.Frequency) * sample.Rate : sample.Time;
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

        bool IOmsKeyboardEventSink.HandleRawKeyPressed(InputKey key, long timestamp) => enqueueRawKeyboardTransition(key, timestamp, pressed: true);

        bool IOmsKeyboardEventSink.HandleRawKeyReleased(InputKey key, long timestamp) => enqueueRawKeyboardTransition(key, timestamp, pressed: false);

        void IOmsKeyboardEventSink.ResetRawKeyboardState() => resetRawKeyboardState();

        private void resetRawKeyboardState()
        {
            lock (rawKeyboardGate)
            {
                if (rawKeyboardClosed)
                    return;

                // Focus loss/pause ends this input epoch. Pending presses must not hit a note when gameplay
                // resumes; release only the keyboard state which has already reached the update thread.
                rawKeyboardEpoch++;
                rawKeyboardTransitions.Clear();
                rawKeyboardTransitions.Enqueue(new RawKeyboardTransition(InputKey.None, null, 0));
            }
        }

        private bool enqueueRawKeyboardTransition(InputKey key, long timestamp, bool pressed)
        {
            if (!rawKeyboardBindingsSupported || !keyboardInputHandler.HasBindingForKey(key))
                return false;

            lock (rawKeyboardGate)
            {
                if (rawKeyboardClosed)
                    return false;

                rawKeyboardTransitions.Enqueue(new RawKeyboardTransition(key, pressed, timestamp));
            }

            return true;
        }

        private readonly record struct RawKeyboardTransition(InputKey Key, bool? Pressed, long Timestamp);

        private readonly record struct TimedKeyboardTransition(InputKey Key, bool Pressed, double Time);

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
