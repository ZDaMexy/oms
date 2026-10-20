// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania
{
    [Cached] // Used for touch input, see Column.OnTouchDown/OnTouchUp.
    public partial class ManiaInputManager : RulesetInputManager<ManiaAction>, IOmsKeyboardEventSink, IFrameStableInputHandler
    {
        private readonly GameplayKeyboardInputQueue keyboardInputQueue;
        private bool rawKeyboardActivated;

        [Resolved(canBeNull: true)]
        private IOmsKeyboardEventSource? keyboardEventSource { get; set; }

        private NativeKeyboardBindingContainer? nativeKeyboardBindings => KeyBindingContainer as NativeKeyboardBindingContainer;

        public ManiaInputManager(RulesetInfo ruleset, int variant)
            : base(ruleset, variant, SimultaneousBindingMode.Unique)
        {
            keyboardInputQueue = new GameplayKeyboardInputQueue(
                (key, pressed) => nativeKeyboardBindings!.ApplyNativeKey(key, pressed),
                () => nativeKeyboardBindings?.ReleaseNativeKeys());
        }

        protected override KeyBindingContainer<ManiaAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new NativeKeyboardBindingContainer(this, ruleset, variant, unique);

        internal void ActivateRawKeyboard()
        {
            if (rawKeyboardActivated)
                return;

            rawKeyboardActivated = true;
            keyboardEventSource?.RegisterSink(this);
        }

        protected override void Update()
        {
            keyboardInputQueue.DispatchDue(Time.Current);
            base.Update();
        }

        bool IOmsKeyboardEventSink.HandleRawKeyPressed(InputKey key, long timestamp) => enqueueKey(key, timestamp, true);

        bool IOmsKeyboardEventSink.HandleRawKeyReleased(InputKey key, long timestamp) => enqueueKey(key, timestamp, false);

        void IOmsKeyboardEventSink.ResetRawKeyboardState() => keyboardInputQueue.Reset();

        double? IFrameStableInputHandler.NextInputTime => keyboardInputQueue.NextInputTime;

        void IFrameStableInputHandler.ResetInput() => keyboardInputQueue.Reset();

        void IFrameStableInputHandler.PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput)
            => keyboardInputQueue.PrepareFrame(timeSource, currentTime, isLiveInput && UseParentInput);

        private bool enqueueKey(InputKey key, long timestamp, bool pressed)
            => nativeKeyboardBindings?.CanProvideKey(key) == true && keyboardInputQueue.Enqueue(key, timestamp, pressed);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                keyboardInputQueue.Dispose();
                if (rawKeyboardActivated)
                    keyboardEventSource?.UnregisterSink(this);
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Uses the actual framework binding state for native and ordinary inputs together. In particular,
        /// releasing a native keyboard key cannot release the same action while a gamepad binding holds it.
        /// </summary>
        private partial class NativeKeyboardBindingContainer : RulesetKeyBindingContainer
        {
            private readonly ManiaInputManager owner;
            private readonly HashSet<InputKey> appliedNativeKeys = new HashSet<InputKey>();
            private readonly HashSet<InputKey> appliedFrameworkKeys = new HashSet<InputKey>();
            private HashSet<InputKey> boundKeyboardKeys = new HashSet<InputKey>();
            private bool nativeBindingsSupported;

            public NativeKeyboardBindingContainer(ManiaInputManager owner, RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
                this.owner = owner;
            }

            protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
            {
                ReleaseNativeKeys();
                owner.keyboardInputQueue.Reset();
                base.ReloadMappings(realmKeyBindings);

                var keys = new HashSet<InputKey>();
                bool supported = true;
                foreach (IKeyBinding binding in KeyBindings)
                {
                    bool hasKeyboard = binding.KeyCombination.Keys.Any(isKeyboardKey);
                    if (!hasKeyboard)
                        continue;

                    // A mixed keyboard/controller chord must stay entirely on the ordinary framework stream.
                    if (binding.KeyCombination.Keys.Any(key => !isKeyboardKey(key) || owner.keyboardEventSource?.SupportsKey(key) == false))
                        supported = false;

                    foreach (InputKey key in binding.KeyCombination.Keys.Where(isKeyboardKey))
                        keys.Add(key);
                }

                // The source publishes physical modifiers, while a saved binding may name either side.
                if (keys.Contains(InputKey.Control)) keys.UnionWith(new[] { InputKey.LControl, InputKey.RControl });
                if (keys.Contains(InputKey.Shift)) keys.UnionWith(new[] { InputKey.LShift, InputKey.RShift });
                if (keys.Contains(InputKey.Alt)) keys.UnionWith(new[] { InputKey.LAlt, InputKey.RAlt });
                if (keys.Contains(InputKey.Super)) keys.UnionWith(new[] { InputKey.LSuper, InputKey.RSuper });

                boundKeyboardKeys = keys;
                nativeBindingsSupported = supported;
            }

            public bool CanProvideKey(InputKey key) => nativeBindingsSupported && boundKeyboardKeys.Contains(key);

            private bool ownsKeyboardEvent(InputKey key)
                => CanProvideKey(key) && owner.keyboardEventSource?.IsActiveFor(owner) == true;

            protected override bool Handle(UIEvent e)
            {
                switch (e)
                {
                    case KeyDownEvent keyDown:
                    {
                        InputKey key = KeyCombination.FromKey(keyDown.Key);
                        if (ownsKeyboardEvent(key))
                            return true;

                        if (!keyDown.Repeat)
                        {
                            appliedFrameworkKeys.Add(key);
                            if (appliedNativeKeys.Contains(key))
                                return true;
                        }

                        break;
                    }

                    case KeyUpEvent keyUp:
                    {
                        InputKey key = KeyCombination.FromKey(keyUp.Key);
                        if (appliedFrameworkKeys.Remove(key))
                        {
                            // A key pressed before native ownership began still needs its SDL release.
                            // Keep the resolver held if a native source now owns the same physical key.
                            if (appliedNativeKeys.Contains(key))
                                return true;
                        }
                        else if (appliedNativeKeys.Contains(key) || ownsKeyboardEvent(key))
                            return true;

                        break;
                    }
                }

                return base.Handle(e);
            }

            public void ApplyNativeKey(InputKey key, bool pressed)
            {
                if (pressed ? !appliedNativeKeys.Add(key) : !appliedNativeKeys.Remove(key))
                    return;

                // Both streams resolve to the same framework key. Only their first press and final
                // release may change the resolver, whose ordinary bindings also retain gamepad refs.
                if (appliedFrameworkKeys.Contains(key))
                    return;

                Key physicalKey = toPhysicalKey(key);
                InputState state = owner.CurrentState;
                // Call the real binding resolver, bypassing only the SDL echo gate above.
                base.Handle(pressed ? new KeyDownEvent(state, physicalKey, false) : new KeyUpEvent(state, physicalKey));
            }

            public void ReleaseNativeKeys()
            {
                foreach (InputKey key in appliedNativeKeys.ToArray())
                    ApplyNativeKey(key, false);
            }

            private static bool isKeyboardKey(InputKey key)
                => key > InputKey.None && (key < InputKey.LastKey || key >= InputKey.Mute && key <= InputKey.RSuper);

            private static Key toPhysicalKey(InputKey key) => key switch
            {
                InputKey.LShift or InputKey.Shift => Key.LShift,
                InputKey.RShift => Key.RShift,
                InputKey.LControl or InputKey.Control => Key.LControl,
                InputKey.RControl => Key.RControl,
                InputKey.LAlt or InputKey.Alt => Key.LAlt,
                InputKey.RAlt => Key.RAlt,
                InputKey.LSuper or InputKey.Super => Key.LWin,
                InputKey.RSuper => Key.RWin,
                _ => (Key)key,
            };
        }
    }

    public enum ManiaAction
    {
        [Description("Key 1")]
        Key1,

        [Description("Key 2")]
        Key2,

        [Description("Key 3")]
        Key3,

        [Description("Key 4")]
        Key4,

        [Description("Key 5")]
        Key5,

        [Description("Key 6")]
        Key6,

        [Description("Key 7")]
        Key7,

        [Description("Key 8")]
        Key8,

        [Description("Key 9")]
        Key9,

        [Description("Key 10")]
        Key10,

        [Description("Key 11")]
        Key11,

        [Description("Key 12")]
        Key12,

        [Description("Key 13")]
        Key13,

        [Description("Key 14")]
        Key14,

        [Description("Key 15")]
        Key15,

        [Description("Key 16")]
        Key16,

        [Description("Key 17")]
        Key17,

        [Description("Key 18")]
        Key18,

        [Description("Key 19")]
        Key19,

        [Description("Key 20")]
        Key20,
    }
}
