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
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Input;
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

        public event Action<OmsAxisMotion>? AdjustmentMotion;

        public event Action? AdjustmentInputReset;

        public OmsInputRouter Router { get; } = new OmsInputRouter();

        private readonly HashSet<BmsAction> forwardedPressedActions = new HashSet<BmsAction>();
        private readonly GameplayKeyboardInputQueue keyboardInputQueue;
        private readonly BmsScratchMotionTracker scratchMotion;
        private (double Counts1, double Revolutions1, double Counts2, double Revolutions2) scratchPresentationMotion;
        private bool hasScratchPresentationMotion;
        private readonly GameplayInputQueue<DeviceInput> deviceInputQueue;
        private readonly Action<OmsHidInputEvent> receiveHidInput;
        private readonly HashSet<int> boundJoystickButtons = new HashSet<int>();
        private readonly HashSet<int> heldJoystickButtons = new HashSet<int>(64);
        private readonly object mouseInputGate = new object();
        private int mouseInputEpoch;
        private IBindable<bool>? hostActive;
        private IDisposable? mouseCapture;
        private int resetAllInput;
        private bool mouseBindings;
        private bool liveInputEnabled = true;
        private bool hasPreparedInput;
        private bool wasLiveInput = true;
        private long? inputClockEpoch;
        private double previousObservationTime = double.NaN;
        private double hidObservationTime;
        private long? hidCaptureClockEpoch;
        private GameplayInputTime? inputTimeAnchor;
        private bool sourcesActivated;
        private bool rawKeyboardBindingsSupported = true;
        private readonly int variant;
        private readonly Func<IOmsHidButtonDeviceProvider?>? createHidProvider;
        private OmsKeyboardInputHandler keyboardInputHandler = null!;
        private OmsHidButtonInputHandler hidButtonInputHandler = null!;
        private OmsXInputButtonInputHandler xInputButtonInputHandler = null!;
        private OmsMouseAxisInputHandler mouseAxisInputHandler = null!;
        private OmsHidDeviceHandler? hidDeviceHandler;

        [Resolved(canBeNull: true)]
        private IOmsKeyboardEventSource? keyboardEventSource { get; set; }

        [Resolved(canBeNull: true)]
        private IOmsMouseAxisSource? mouseAxisSource { get; set; }

        [Resolved(canBeNull: true)]
        private GameHost? host { get; set; }

        public BmsInputManager(RulesetInfo ruleset, int variant)
            : this(ruleset, variant, null)
        {
        }

        internal BmsInputManager(RulesetInfo ruleset, int variant, Func<IOmsHidButtonDeviceProvider?>? createHidProvider)
            : base(ruleset, variant, SimultaneousBindingMode.Unique)
        {
            this.createHidProvider = createHidProvider;
            keyboardInputQueue = new GameplayKeyboardInputQueue(
                (key, pressed) => tryHandleKeyboardEvent(key, pressed),
                () =>
                {
                    AdjustmentInputReset?.Invoke();
                    keyboardInputHandler.ReleaseAllPressed();
                });
            scratchMotion = new BmsScratchMotionTracker(
                action => KeyBindingContainer.TriggerPressed(action),
                action => KeyBindingContainer.TriggerReleased(action));
            deviceInputQueue = new GameplayInputQueue<DeviceInput>(dispatchDeviceInput, releaseAppliedDeviceInput);
            receiveHidInput = input => deviceInputQueue.EnqueueObserved(new DeviceInput(DeviceInputKind.Hid, Hid: input), hidObservationTime, hidCaptureClockEpoch);
            this.variant = OmsBmsActionMap.NormalizeVariant(variant);
            applyBindings(OmsBmsBindingResolver.GetBindingsOrDefault(null, this.variant));

            Router.ActionPressed += onOmsActionPressed;
            Router.ActionReleased += onOmsActionReleased;

        }

        [BackgroundDependencyLoader(true)]
        private void load(RealmAccess? realmAccess)
        {
            applyBindings(OmsBmsBindingResolver.GetBindingsOrDefault(realmAccess, variant));
        }

        internal void ActivateRawKeyboard()
        {
            if (sourcesActivated)
                return;
            sourcesActivated = true;
            keyboardEventSource?.RegisterSink(this);
            if (mouseAxisSource != null)
            {
                mouseAxisSource.RelativeMouseMoved += onRelativeMouseMoved;
                mouseAxisSource.InputReset += onMouseInputReset;
            }
            hostActive = host?.IsActive.GetBoundCopy();
            hostActive?.BindValueChanged(_ => requestInputReset());
        }

        protected override bool CountsAsClick(BmsAction action) => action.IsLaneAction();

        public bool TriggerKeyPressed(InputKey key) => keyboardInputHandler.TriggerPressed(key);

        public bool TriggerKeyReleased(InputKey key) => keyboardInputHandler.TriggerReleased(key);

        public bool TriggerHidButtonPressed(string deviceIdentifier, int buttonIndex) => hidButtonInputHandler.TriggerPressed(deviceIdentifier, buttonIndex);

        public bool TriggerHidButtonReleased(string deviceIdentifier, int buttonIndex) => hidButtonInputHandler.TriggerReleased(deviceIdentifier, buttonIndex);

        public bool TriggerXInputButtonPressed(int buttonIndex) => xInputButtonInputHandler.TriggerPressed(buttonIndex);

        public bool TriggerXInputButtonReleased(int buttonIndex) => xInputButtonInputHandler.TriggerReleased(buttonIndex);

        public bool TriggerMouseAxisDelta(OmsMouseAxis axis, float delta) => mouseAxisInputHandler.ApplyAxisDelta(axis, delta);

        public bool IsLaneHeld(BmsAction action)
            => KeyBindingContainer.PressedActions.Contains(action)
               || IsScratchMotionHeld(action);

        internal bool IsScratchMotionHeld(BmsAction action)
            => action == BmsAction.Scratch1 && KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1Motion)
               || action == BmsAction.Scratch2 && KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2Motion);

        internal bool IsLaneHeldAfterRelease(BmsAction lane, BmsAction releasing)
            => lane != releasing && KeyBindingContainer.PressedActions.Contains(lane)
               || lane == BmsAction.Scratch1 && releasing != BmsAction.Scratch1Motion && KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1Motion)
               || lane == BmsAction.Scratch2 && releasing != BmsAction.Scratch2Motion && KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2Motion);

        internal bool ScratchPresentationMotionAvailable => UseParentInput && liveInputEnabled && hasScratchPresentationMotion;

        internal (double Counts1, double Revolutions1, double Counts2, double Revolutions2) GetScratchPresentationMotion()
            => scratchPresentationMotion;

        private void observeAxisMotion(OmsAxisMotion motion)
        {
            if (!UseParentInput || !liveInputEnabled)
                return;

            if (PreStartHoldPressed.Value)
            {
                AdjustmentMotion?.Invoke(motion);
                return;
            }

            scratchMotion.Observe(motion, toDeviceTime(Time.Current));

            // Presentation only: retain the accepted direction without changing lane attacks, judgement or replay.
            // Relative counts and calibrated revolutions deliberately remain separate physical units.
            switch (motion.Action)
            {
                case OmsAction.Key1P_Scratch:
                    if (motion.Mode == OmsAxisMode.RelativeMotion)
                        scratchPresentationMotion.Counts1 += motion.Delta;
                    else
                        scratchPresentationMotion.Revolutions1 += motion.Delta;
                    hasScratchPresentationMotion = true;
                    break;

                case OmsAction.Key2P_Scratch:
                    if (motion.Mode == OmsAxisMode.RelativeMotion)
                        scratchPresentationMotion.Counts2 += motion.Delta;
                    else
                        scratchPresentationMotion.Revolutions2 += motion.Delta;
                    hasScratchPresentationMotion = true;
                    break;
            }
        }

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

            if (!changed || !OmsBmsActionMap.TryMapToBmsAction(variant, action, out var bmsAction))
                return changed;

            if (!forwardedPressedActions.Remove(bmsAction))
                return changed;

            if (UseParentInput)
                KeyBindingContainer.TriggerReleased(bmsAction);

            return changed;
        }

        protected override void Update()
        {
            if (Interlocked.Exchange(ref resetAllInput, 0) != 0)
                hidDeviceHandler?.ResetInput(releaseApplied: false);

            // Only the already delivered stick position advances on historical catch-up stops.
            // Hardware itself is read once in PrepareFrame, never in this subtree update.
            hidDeviceHandler?.UpdateAxes(toDeviceTime(Time.Current));
            keyboardInputQueue.DispatchDue(Time.Current);
            deviceInputQueue.DispatchDue(Time.Current);
            mouseAxisInputHandler.FinishFrame();
            mouseAxisInputHandler.BeginFrame();
            // Once gameplay visits our published stop, consume it even if projecting
            // back to device time rounds just below the original deadline.
            double? reachedExpiry = scratchMotion.NextExpiry is double expiry && toGameplayTime(expiry) <= Time.Current ? expiry : null;
            scratchMotion.Update(toDeviceTime(Time.Current), reachedExpiry);
            if (!hasPreparedInput && UseParentInput)
                hidDeviceHandler?.PollOnce();
            base.Update();
        }

        double? IFrameStableInputHandler.NextInputTime
        {
            get
            {
                double? next = keyboardInputQueue.NextInputTime;
                if (deviceInputQueue.NextInputTime is double deviceTime)
                    next = next.HasValue ? Math.Min(next.Value, deviceTime) : deviceTime;
                if (scratchMotion.NextExpiry is double motionTime)
                {
                    double gameplayTime = toGameplayTime(motionTime);
                    next = next.HasValue ? Math.Min(next.Value, gameplayTime) : gameplayTime;
                }
                return next;
            }
        }

        void IFrameStableInputHandler.ResetInput() => requestInputReset();

        private void requestInputReset()
        {
            keyboardInputQueue.Reset();
            deviceInputQueue.Reset();
            Interlocked.Exchange(ref resetAllInput, 1);
        }

        private void resetInputEpoch()
        {
            keyboardInputQueue.Reset();
            deviceInputQueue.Reset();
            hidDeviceHandler?.ResetInput(releaseApplied: false);
        }

        void IFrameStableInputHandler.PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput)
            => prepareFrame(timeSource, currentTime, isLiveInput, timeSource?.InputTime?.Time ?? currentTime);

        void IFrameStableInputHandler.PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput, double observationTime)
            => prepareFrame(timeSource, currentTime, isLiveInput, observationTime);

        private void prepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput, double observationTime)
        {
            hasPreparedInput = true;
            bool live = isLiveInput && UseParentInput && hostActive?.Value != false;
            GameplayInputTime? sample = timeSource?.InputTime;
            inputTimeAnchor = sample;
            bool epochChanged = sample is GameplayInputTime clock && inputClockEpoch.HasValue && inputClockEpoch.Value != clock.Epoch;
            bool explicitlyReset = Interlocked.Exchange(ref resetAllInput, 0) != 0;
            if (!explicitlyReset && (!live && wasLiveInput || !epochChanged && observationTime < previousObservationTime))
                resetInputEpoch();
            else if (explicitlyReset || epochChanged || live != wasLiveInput)
                // Timestamp queues retain fresh reports from the new clock epoch themselves.
                // Only hardware baselines need an explicit drain; clearing raw queues here
                // would incorrectly erase freshly received keyboard/mouse input too.
                hidDeviceHandler?.ResetInput(releaseApplied: false);
            inputClockEpoch = sample?.Epoch;
            previousObservationTime = observationTime;
            wasLiveInput = live;
            liveInputEnabled = live;

            if (live && mouseBindings && mouseCapture == null)
                mouseCapture = mouseAxisSource?.AcquireCapture();
            else if ((!live || !mouseBindings) && mouseCapture != null)
            {
                mouseCapture.Dispose();
                mouseCapture = null;
            }

            if (live && hidDeviceHandler?.HasBoundDevices == true)
            {
                hidObservationTime = observationTime;
                hidCaptureClockEpoch = sample?.Epoch;
                hidDeviceHandler.CaptureOnce(observationTime, receiveHidInput);
            }
            keyboardInputQueue.PrepareFrame(timeSource, currentTime, live, observationTime);
            deviceInputQueue.PrepareFrame(timeSource, currentTime, live, observationTime);
        }

        private double toDeviceTime(double gameplayTime)
            => inputTimeAnchor is GameplayInputTime anchor && anchor.Rate > 0
                ? anchor.Timestamp * (1000.0 / Stopwatch.Frequency) + (gameplayTime - anchor.Time) / anchor.Rate
                : gameplayTime;

        private double toGameplayTime(double deviceTime)
            => inputTimeAnchor is GameplayInputTime anchor && anchor.Rate > 0
                ? anchor.Time + (deviceTime - anchor.Timestamp * (1000.0 / Stopwatch.Frequency)) * anchor.Rate
                : deviceTime;

        // PassThroughInputManager routes parent events through Handle directly. Drawable's
        // OnKeyDown/OnJoystickPress overrides are not an entry point on this input path.
        protected override bool Handle(UIEvent e)
        {
            // Pausing disables parent gameplay input, but its pre-start/scroll controls
            // still need the real parent events. Replays keep exclusive input ownership.
            if (ReplayInputHandler != null)
                return base.Handle(e);
            if (hostActive?.Value == false && e is KeyDownEvent or KeyUpEvent or JoystickPressEvent or JoystickReleaseEvent)
                return true;

            switch (e)
            {
                case KeyDownEvent keyDown:
                    if (routeParentKey(KeyCombination.FromKey(keyDown.Key), !keyDown.Repeat, pressed: true))
                        return true;
                    break;
                case KeyUpEvent keyUp:
                    if (routeParentKey(KeyCombination.FromKey(keyUp.Key), true, pressed: false))
                        return true;
                    break;
                case JoystickPressEvent joystickPress:
                    if (routeParentJoystick((int)joystickPress.Button, true))
                        return true;
                    break;
                case JoystickReleaseEvent joystickRelease:
                    if (routeParentJoystick((int)joystickRelease.Button, false))
                        return true;
                    break;
            }
            return base.Handle(e);
        }

        private bool routeParentKey(InputKey key, bool transition, bool pressed)
        {
            if (!keyboardInputHandler.HasBindingForKey(key))
                return false;
            if (!transition || UseParentInput && rawKeyboardBindingsSupported && keyboardEventSource?.IsActiveFor(this) == true && keyboardEventSource.SupportsKey(key))
                return true;
            if (!UseParentInput || !liveInputEnabled)
                tryHandleKeyboardEvent(key, pressed);
            else
                keyboardInputQueue.Enqueue(key, Stopwatch.GetTimestamp(), pressed);
            return true;
        }

        private bool routeParentJoystick(int button, bool pressed)
        {
            InputKey key = KeyCombination.FromJoystickButton((JoystickButton)button);
            if (!boundJoystickButtons.Contains(button) && !keyboardInputHandler.HasBindingForKey(key))
                return false;
            var input = new DeviceInput(DeviceInputKind.Joystick, Button: button, Pressed: pressed);
            if (!UseParentInput || !liveInputEnabled)
                dispatchDeviceInput(input);
            else
                deviceInputQueue.Enqueue(input, Stopwatch.GetTimestamp());
            return true;
        }

        private void onRelativeMouseMoved(OmsRelativeMouseMovement movement)
        {
            lock (mouseInputGate)
                deviceInputQueue.Enqueue(new DeviceInput(DeviceInputKind.Mouse, X: movement.X, Y: movement.Y, MouseEpoch: mouseInputEpoch), movement.Timestamp);
        }

        private void onMouseInputReset()
        {
            lock (mouseInputGate)
            {
                Interlocked.Increment(ref mouseInputEpoch);
                deviceInputQueue.Enqueue(new DeviceInput(DeviceInputKind.MouseReset), Stopwatch.GetTimestamp());
            }
        }

        private void dispatchDeviceInput(DeviceInput input)
        {
            switch (input.Kind)
            {
                case DeviceInputKind.Mouse:
                    if (input.MouseEpoch != Volatile.Read(ref mouseInputEpoch))
                        break;
                    mouseAxisInputHandler.FinishFrame();
                    mouseAxisInputHandler.BeginFrame();
                    if (input.X != 0)
                        mouseAxisInputHandler.ApplyAxisDelta(OmsMouseAxis.X, input.X);
                    if (input.Y != 0)
                        mouseAxisInputHandler.ApplyAxisDelta(OmsMouseAxis.Y, input.Y);
                    mouseAxisInputHandler.FinishFrame();
                    break;
                case DeviceInputKind.MouseReset:
                    AdjustmentInputReset?.Invoke();
                    mouseAxisInputHandler.ReleaseAll();
                    scratchMotion.ReleaseDevice(null);
                    break;
                case DeviceInputKind.Joystick:
                    if (input.Pressed)
                        heldJoystickButtons.Add(input.Button);
                    else
                        heldJoystickButtons.Remove(input.Button);
                    tryHandleKeyboardEvent(KeyCombination.FromJoystickButton((JoystickButton)input.Button), input.Pressed);
                    if (input.Pressed)
                        xInputButtonInputHandler.TriggerPressed(input.Button);
                    else
                        xInputButtonInputHandler.TriggerReleased(input.Button);
                    break;
                case DeviceInputKind.Hid:
                    hidDeviceHandler?.DispatchCaptured(input.Hid);
                    break;
            }
        }

        private void releaseAppliedDeviceInput()
        {
            AdjustmentInputReset?.Invoke();
            mouseAxisInputHandler.ReleaseAll();
            foreach (int button in heldJoystickButtons)
            {
                xInputButtonInputHandler.TriggerReleased(button);
                tryHandleKeyboardEvent(KeyCombination.FromJoystickButton((JoystickButton)button), false);
            }
            heldJoystickButtons.Clear();
            hidDeviceHandler?.ReleaseAppliedInput();
            scratchMotion.Clear();
            scratchPresentationMotion = default;
            hasScratchPresentationMotion = false;
        }

        private enum DeviceInputKind { Mouse, MouseReset, Joystick, Hid }

        private readonly record struct DeviceInput(DeviceInputKind Kind, float X = 0, float Y = 0, int Button = 0, bool Pressed = false,
                                                  OmsHidInputEvent Hid = default, int MouseEpoch = 0);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (mouseAxisSource != null)
                {
                    mouseAxisSource.RelativeMouseMoved -= onRelativeMouseMoved;
                    mouseAxisSource.InputReset -= onMouseInputReset;
                }
                mouseCapture?.Dispose();
                hostActive?.UnbindAll();
                deviceInputQueue.Dispose();
                releaseAppliedDeviceInput();
                keyboardInputHandler.ReleaseAllPressed();
                keyboardInputQueue.Dispose();
                keyboardEventSource?.UnregisterSink(this);
                hidDeviceHandler?.Dispose();
                scratchMotion.Clear();
            }

            base.Dispose(isDisposing);
        }

        bool IOmsKeyboardEventSink.HandleRawKeyPressed(InputKey key, long timestamp) => enqueueRawKeyboardTransition(key, timestamp, pressed: true);

        bool IOmsKeyboardEventSink.HandleRawKeyReleased(InputKey key, long timestamp) => enqueueRawKeyboardTransition(key, timestamp, pressed: false);

        void IOmsKeyboardEventSink.ResetRawKeyboardState()
        {
            keyboardInputQueue.Reset();
        }

        private bool enqueueRawKeyboardTransition(InputKey key, long timestamp, bool pressed)
            => rawKeyboardBindingsSupported && keyboardInputHandler.HasBindingForKey(key)
                                            && keyboardInputQueue.Enqueue(key, timestamp, pressed);

        private void applyBindings(IReadOnlyList<OmsBinding> bindings)
        {
            // Keep an entire binding group on one source. Splitting a chord between queued native modifiers
            // and unsupported SDL keys can lose a complete tap before the two batches meet in this update.
            keyboardInputHandler?.ReleaseAllPressed();
            hidDeviceHandler?.Dispose();
            mouseAxisInputHandler?.ReleaseAll();
            heldJoystickButtons.Clear();
            boundJoystickButtons.Clear();
            foreach (var trigger in bindings.SelectMany(binding => binding.XInputButtonTriggers))
                boundJoystickButtons.Add(trigger.ButtonIndex);
            mouseBindings = bindings.Any(binding => binding.MouseAxisTriggers.Length != 0);
            rawKeyboardBindingsSupported = keyboardEventSource == null || bindings.SelectMany(binding => binding.KeyboardCombinations)
                .SelectMany(combination => combination.Keys).Where(key => key != InputKey.None).All(keyboardEventSource.SupportsKey);
            keyboardInputHandler = new OmsKeyboardInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            hidButtonInputHandler = new OmsHidButtonInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            xInputButtonInputHandler = new OmsXInputButtonInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action));
            mouseAxisInputHandler = new OmsMouseAxisInputHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action), observeAxisMotion);

            scratchMotion.Clear();
            scratchPresentationMotion = default;
            hasScratchPresentationMotion = false;
            hidDeviceHandler = new OmsHidDeviceHandler(bindings, action => TriggerOmsActionPressed(action), action => TriggerOmsActionReleased(action),
                bindings.Any(binding => binding.HidButtonTriggers.Length != 0 || binding.HidAxisTriggers.Length != 0) ? createHidProvider?.Invoke() : null, observeAxisMotion);
            hidDeviceHandler.DeviceDisconnected += scratchMotion.ReleaseDevice;
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
            => liveInputEnabled && action.IsLaneAction() && !PreStartHoldPressed.Value;

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

        // Movement continuity is separate from scratch attack pulses. These actions
        // have no bindable note lane and are carried by BMS replays for long scratches.
        Scratch1Motion,
        Scratch2Motion,
    }

    public static class BmsActionExtensions
    {
        public static bool IsLaneAction(this BmsAction action)
            => action >= BmsAction.Scratch1 && action <= BmsAction.Key14;

        public static bool IsGameplayInputAction(this BmsAction action)
            => action.IsLaneAction() || action is BmsAction.Scratch1Motion or BmsAction.Scratch2Motion;

        public static bool IsMotionFor(this BmsAction action, BmsAction laneAction)
            => action == BmsAction.Scratch1Motion && laneAction == BmsAction.Scratch1
               || action == BmsAction.Scratch2Motion && laneAction == BmsAction.Scratch2;

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
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 => -1,
                    BmsAction.Key2 or BmsAction.Key4 => 1,
                    _ => 0,
                },
                8 => action switch
                {
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 or BmsAction.Key7 => -1,
                    BmsAction.Key2 or BmsAction.Key4 or BmsAction.Key6 => 1,
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
                    BmsAction.Key1 or BmsAction.Key3 or BmsAction.Key5 or BmsAction.Key7 or BmsAction.Key8 or BmsAction.Key10 or BmsAction.Key12 or BmsAction.Key14 => -1,
                    BmsAction.Key2 or BmsAction.Key4 or BmsAction.Key6 or BmsAction.Key9 or BmsAction.Key11 or BmsAction.Key13 => 1,
                    _ => 0,
                },
                _ => 0,
            };
        }
    }
}
