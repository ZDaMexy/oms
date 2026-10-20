// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Input;
using osu.Game.Screens.Play;

namespace osu.Desktop.Windows
{
    [Cached(typeof(IOmsKeyboardEventSource))]
    public partial class WindowsRawKeyboardSource : Component, IOmsKeyboardEventSource
    {
        private const int gwlp_wndproc = -4;

        private const uint wm_input = 0x00FF;
        private const uint wm_input_device_change = 0x00FE;
        private const uint wm_setfocus = 0x0007;
        private const uint wm_killfocus = 0x0008;
        private const int gidc_removal = 2;
        private const uint rid_input = 0x10000003;
        private const uint rim_typekeyboard = 1;

        private const ushort hid_usage_page_generic = 0x01;
        private const ushort hid_usage_generic_keyboard = 0x06;

        private const ushort vk_back = 0x08;
        private const ushort vk_tab = 0x09;
        private const ushort vk_clear = 0x0C;
        private const ushort vk_return = 0x0D;
        private const ushort vk_shift = 0x10;
        private const ushort vk_control = 0x11;
        private const ushort vk_menu = 0x12;
        private const ushort vk_pause = 0x13;
        private const ushort vk_capslock = 0x14;
        private const ushort vk_escape = 0x1B;
        private const ushort vk_space = 0x20;
        private const ushort vk_pageup = 0x21;
        private const ushort vk_pagedown = 0x22;
        private const ushort vk_end = 0x23;
        private const ushort vk_home = 0x24;
        private const ushort vk_left = 0x25;
        private const ushort vk_up = 0x26;
        private const ushort vk_right = 0x27;
        private const ushort vk_down = 0x28;
        private const ushort vk_snapshot = 0x2C;
        private const ushort vk_insert = 0x2D;
        private const ushort vk_delete = 0x2E;
        private const ushort vk_0 = 0x30;
        private const ushort vk_9 = 0x39;
        private const ushort vk_a = 0x41;
        private const ushort vk_z = 0x5A;
        private const ushort vk_lwin = 0x5B;
        private const ushort vk_rwin = 0x5C;
        private const ushort vk_apps = 0x5D;
        private const ushort vk_numpad0 = 0x60;
        private const ushort vk_numpad9 = 0x69;
        private const ushort vk_multiply = 0x6A;
        private const ushort vk_add = 0x6B;
        private const ushort vk_separator = 0x6C;
        private const ushort vk_subtract = 0x6D;
        private const ushort vk_decimal = 0x6E;
        private const ushort vk_divide = 0x6F;
        private const ushort vk_f1 = 0x70;
        private const ushort vk_f24 = 0x87;
        private const ushort vk_numlock = 0x90;
        private const ushort vk_scroll = 0x91;
        private const ushort vk_lshift = 0xA0;
        private const ushort vk_rshift = 0xA1;
        private const ushort vk_lcontrol = 0xA2;
        private const ushort vk_rcontrol = 0xA3;
        private const ushort vk_lmenu = 0xA4;
        private const ushort vk_rmenu = 0xA5;
        private const ushort vk_oem_1 = 0xBA;
        private const ushort vk_oem_plus = 0xBB;
        private const ushort vk_oem_comma = 0xBC;
        private const ushort vk_oem_minus = 0xBD;
        private const ushort vk_oem_period = 0xBE;
        private const ushort vk_oem_2 = 0xBF;
        private const ushort vk_oem_3 = 0xC0;
        private const ushort vk_oem_4 = 0xDB;
        private const ushort vk_oem_5 = 0xDC;
        private const ushort vk_oem_6 = 0xDD;
        private const ushort vk_oem_7 = 0xDE;
        private const ushort vk_oem_102 = 0xE2;

        private const uint mapvk_vsc_to_vk_ex = 3;

        private static readonly HashSet<InputKey> supported_keys = getSupportedKeys();

        [Resolved]
        private GameHost host { get; set; } = null!;

        private IBindable<LocalUserPlayingState> localUserPlaying = null!;
        private IBindable<bool> isActive = null!;
        private IOmsKeyboardEventSink? sink;
        private readonly object sinkGate = new object();
        private IOmsKeyboardEventSink? deviceStateOwner;
        private readonly NativeKeyboardDeviceState keyboardDeviceState;
        private long requestedStateReset;
        private long appliedStateReset;

        private volatile bool rawInputEnabled;
        private volatile bool windowHasKeyboardFocus = true;
        private bool rawInputInitialisationAttempted;
        private IntPtr windowHandle;
        private IntPtr originalWindowProc;
        private WndProcDelegate? windowProcDelegate;
        private IntPtr rawInputBuffer;
        private uint rawInputBufferSize;

        public WindowsRawKeyboardSource()
        {
            keyboardDeviceState = new NativeKeyboardDeviceState((key, timestamp, pressed) =>
            {
                lock (sinkGate)
                {
                    IOmsKeyboardEventSink? target = deviceStateOwner;
                    if (target == null || !ReferenceEquals(Volatile.Read(ref sink), target)
                                       || !isActive.Value || localUserPlaying.Value != LocalUserPlayingState.Playing)
                        return;

                    if (pressed)
                        target.HandleRawKeyPressed(key, timestamp);
                    else
                        target.HandleRawKeyReleased(key, timestamp);
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load(ILocalUserPlayInfo localUserInfo)
        {
            localUserPlaying = localUserInfo.PlayingState.GetBoundCopy();
            localUserPlaying.BindValueChanged(state =>
            {
                if (state.NewValue != LocalUserPlayingState.Playing)
                    scheduleKeyboardReset();
            });

            isActive = host.IsActive.GetBoundCopy();
            isActive.BindValueChanged(state =>
            {
                if (!state.NewValue)
                    scheduleKeyboardReset();
            });
            scheduleUpdateHookState();
        }

        public void RegisterSink(IOmsKeyboardEventSink sink)
        {
            lock (sinkGate)
            {
                IOmsKeyboardEventSink? previous = Interlocked.Exchange(ref this.sink, sink);
                if (previous != null && !ReferenceEquals(previous, sink))
                    previous.ResetRawKeyboardState();
            }

            scheduleUpdateHookState();
        }

        public void UnregisterSink(IOmsKeyboardEventSink sink)
        {
            lock (sinkGate)
            {
                if (!ReferenceEquals(Interlocked.CompareExchange(ref this.sink, null, sink), sink))
                    return;

                sink.ResetRawKeyboardState();
            }

            scheduleUpdateHookState();
        }

        public bool IsActiveFor(IOmsKeyboardEventSink sink)
            => rawInputEnabled && ReferenceEquals(Volatile.Read(ref this.sink), sink)
                               && windowHasKeyboardFocus && isActive.Value && localUserPlaying.Value == LocalUserPlayingState.Playing;

        public bool SupportsKey(InputKey key) => supported_keys.Contains(key);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                disableRawInput();

                if (rawInputBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(rawInputBuffer);
            }

            base.Dispose(isDisposing);
        }

        private void scheduleUpdateHookState() => host.InputThread.Scheduler.Add(updateHookState);

        private void scheduleKeyboardReset()
        {
            lock (sinkGate)
            {
                // Invalidate pending gameplay immediately. The physical state belongs to Input and is
                // reset before its next report, even if focus resumes before this scheduled task runs.
                Volatile.Read(ref sink)?.ResetRawKeyboardState();
                Interlocked.Increment(ref requestedStateReset);
            }

            host.InputThread.Scheduler.Add(applyPendingKeyboardReset);
        }

        private void applyPendingKeyboardReset()
        {
            lock (sinkGate)
                synchroniseKeyboardOwner();
        }

        private void synchroniseKeyboardOwner()
        {
            long requested = Volatile.Read(ref requestedStateReset);
            if (appliedStateReset != requested)
            {
                keyboardDeviceState.Clear();
                appliedStateReset = requested;
            }

            IOmsKeyboardEventSink? target = Volatile.Read(ref sink);
            if (ReferenceEquals(deviceStateOwner, target))
                return;

            keyboardDeviceState.Clear();
            deviceStateOwner = target;
        }

        private void updateHookState()
        {
            // Install once during desktop initialisation. Reinstalling at each play boundary races already
            // queued SDL events against the native stream and can duplicate or lose a complete key tap.
            // A failed installation leaves the normal framework keyboard path available for this session.
            if (!rawInputInitialisationAttempted)
            {
                rawInputInitialisationAttempted = true;
                enableRawInput();
            }

            lock (sinkGate)
                synchroniseKeyboardOwner();
        }

        private void enableRawInput()
        {
            if (rawInputEnabled)
                return;

            windowHandle = getWindowHandle(host.Window);

            if (windowHandle == IntPtr.Zero)
            {
                Logger.Log("Could not enable Windows raw keyboard source because no native window handle was available.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            windowProcDelegate ??= windowProc;

            Marshal.GetLastWin32Error();
            originalWindowProc = setWindowLongPtr(windowHandle, gwlp_wndproc, Marshal.GetFunctionPointerForDelegate(windowProcDelegate));

            if (originalWindowProc == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            {
                Logger.Log("Could not subclass the game window for Windows raw keyboard input.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            if (!registerKeyboardRawInput(windowHandle))
            {
                restoreWindowProc();
                Logger.Log("Could not register the game window for Windows raw keyboard input.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            rawInputEnabled = true;
        }

        private void disableRawInput()
        {
            if (!rawInputEnabled)
                return;

            Volatile.Read(ref sink)?.ResetRawKeyboardState();
            unregisterKeyboardRawInput();
            restoreWindowProc();

            rawInputEnabled = false;
            windowHandle = IntPtr.Zero;
        }

        private void restoreWindowProc()
        {
            if (windowHandle == IntPtr.Zero || originalWindowProc == IntPtr.Zero)
                return;

            setWindowLongPtr(windowHandle, gwlp_wndproc, originalWindowProc);
            originalWindowProc = IntPtr.Zero;
        }

        private IntPtr windowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == wm_input)
                processRawInput(lParam, Stopwatch.GetTimestamp());
            else if (msg == wm_input_device_change && wParam.ToInt64() == gidc_removal)
            {
                lock (sinkGate)
                {
                    synchroniseKeyboardOwner();
                    keyboardDeviceState.RemoveDevice(lParam, Stopwatch.GetTimestamp());
                }
            }
            else if (msg == wm_killfocus)
            {
                windowHasKeyboardFocus = false;
                scheduleKeyboardReset();
            }
            else if (msg == wm_setfocus)
            {
                lock (sinkGate)
                {
                    Volatile.Read(ref sink)?.ResetRawKeyboardState();
                    Interlocked.Increment(ref requestedStateReset);
                    synchroniseKeyboardOwner();
                    // RAWINPUT stays foreground-only. Check only known old holds once on focus gain,
                    // since their break may have happened in another application. Use the native VKey
                    // captured on down, never the layout-independent gameplay InputKey value.
                    keyboardDeviceState.ReconcileReleasedKeys(isNativeKeyDown);
                    windowHasKeyboardFocus = true;
                }
            }

            return callWindowProc(originalWindowProc, hwnd, msg, wParam, lParam);
        }

        private void processRawInput(IntPtr rawInputHandle, long timestamp)
        {
            uint size = 0;

            if (getRawInputData(rawInputHandle, rid_input, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == uint.MaxValue || size == 0)
                return;

            if (size > rawInputBufferSize)
            {
                rawInputBuffer = rawInputBuffer == IntPtr.Zero
                    ? Marshal.AllocHGlobal((int)size)
                    : Marshal.ReAllocHGlobal(rawInputBuffer, (IntPtr)size);

                rawInputBufferSize = size;
            }

            if (getRawInputData(rawInputHandle, rid_input, rawInputBuffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == uint.MaxValue)
                return;

            var rawInput = Marshal.PtrToStructure<RAWINPUT>(rawInputBuffer);

            if (rawInput.Header.Type != rim_typekeyboard)
                return;

            RAWKEYBOARD keyboard = rawInput.Data.Keyboard;
            bool released = (keyboard.Flags & RawKeyboardFlags.Break) != 0;
            if (!tryMapInputKey(keyboard, out var key) && !released)
                return;

            int physicalKey = keyboard.MakeCode != 0
                ? keyboard.MakeCode | (int)(keyboard.Flags & (RawKeyboardFlags.E0 | RawKeyboardFlags.E1)) << 16
                : 0x10000000 | keyboard.VKey;
            lock (sinkGate)
            {
                synchroniseKeyboardOwner();
                bool suppressPress = deviceStateOwner == null || !windowHasKeyboardFocus || !isActive.Value || localUserPlaying.Value != LocalUserPlayingState.Playing;
                // Continue observing foreground breaks while paused or between sinks. A make in that
                // interval is suppressed and cannot block a fresh key from another physical keyboard.
                keyboardDeviceState.Apply(rawInput.Header.Device, physicalKey, key, timestamp, !released, suppressPress, normalizeVirtualKey(keyboard));
            }
        }

        private static bool isNativeKeyDown(int virtualKey) => getAsyncKeyState(virtualKey) < 0;

        private static bool tryMapInputKey(RAWKEYBOARD keyboard, out InputKey key)
        {
            if ((keyboard.Flags & (RawKeyboardFlags.E0 | RawKeyboardFlags.E1)) == 0)
            {
                // Match SDL's physical identity for layout-dependent keys (for example physical Z reports
                // VKey Y on QWERTZ). Keypad digits retain the VKey path below, including NumLock-off navigation.
                key = keyboard.MakeCode switch
                {
                    >= 0x02 and <= 0x0A => InputKey.Number1 + keyboard.MakeCode - 0x02,
                    0x0B => InputKey.Number0,
                    0x0C => InputKey.Minus,
                    0x0D => InputKey.Plus,
                    >= 0x10 and <= 0x19 => InputKey.A + "QWERTYUIOP"[keyboard.MakeCode - 0x10] - 'A',
                    0x1A => InputKey.BracketLeft,
                    0x1B => InputKey.BracketRight,
                    >= 0x1E and <= 0x26 => InputKey.A + "ASDFGHJKL"[keyboard.MakeCode - 0x1E] - 'A',
                    0x27 => InputKey.Semicolon,
                    0x28 => InputKey.Quote,
                    0x29 => InputKey.Tilde,
                    0x2B => InputKey.BackSlash,
                    >= 0x2C and <= 0x32 => InputKey.A + "ZXCVBNM"[keyboard.MakeCode - 0x2C] - 'A',
                    0x33 => InputKey.Comma,
                    0x34 => InputKey.Period,
                    0x35 => InputKey.Slash,
                    0x56 => InputKey.NonUSBackSlash,
                    _ => InputKey.None,
                };
                if (key != InputKey.None)
                    return true;
            }

            ushort virtualKey = normalizeVirtualKey(keyboard);

            switch (virtualKey)
            {
                case vk_back:
                    key = InputKey.BackSpace;
                    return true;

                case vk_tab:
                    key = InputKey.Tab;
                    return true;

                case vk_clear:
                    key = InputKey.Clear;
                    return true;

                case vk_return:
                    key = (keyboard.Flags & RawKeyboardFlags.E0) != 0 ? InputKey.KeypadEnter : InputKey.Enter;
                    return true;

                case vk_shift:
                    key = InputKey.Shift;
                    return true;

                case vk_control:
                    key = InputKey.Control;
                    return true;

                case vk_menu:
                    key = InputKey.Alt;
                    return true;

                case vk_pause:
                    key = InputKey.Pause;
                    return true;

                case vk_capslock:
                    key = InputKey.CapsLock;
                    return true;

                case vk_escape:
                    key = InputKey.Escape;
                    return true;

                case vk_space:
                    key = InputKey.Space;
                    return true;

                case vk_pageup:
                    key = InputKey.PageUp;
                    return true;

                case vk_pagedown:
                    key = InputKey.PageDown;
                    return true;

                case vk_end:
                    key = InputKey.End;
                    return true;

                case vk_home:
                    key = InputKey.Home;
                    return true;

                case vk_left:
                    key = InputKey.Left;
                    return true;

                case vk_up:
                    key = InputKey.Up;
                    return true;

                case vk_right:
                    key = InputKey.Right;
                    return true;

                case vk_down:
                    key = InputKey.Down;
                    return true;

                case vk_insert:
                    key = InputKey.Insert;
                    return true;

                case vk_delete:
                    key = InputKey.Delete;
                    return true;

                case vk_snapshot:
                    key = InputKey.PrintScreen;
                    return true;

                case >= vk_0 and <= vk_9:
                    key = InputKey.Number0 + (virtualKey - vk_0);
                    return true;

                case >= vk_a and <= vk_z:
                    key = InputKey.A + (virtualKey - vk_a);
                    return true;

                case vk_lwin:
                    key = InputKey.LSuper;
                    return true;

                case vk_rwin:
                    key = InputKey.RSuper;
                    return true;

                case vk_apps:
                    key = InputKey.Menu;
                    return true;

                case >= vk_numpad0 and <= vk_numpad9:
                    key = InputKey.Keypad0 + (virtualKey - vk_numpad0);
                    return true;

                case vk_multiply:
                    key = InputKey.KeypadMultiply;
                    return true;

                case vk_add:
                    key = InputKey.KeypadAdd;
                    return true;

                case vk_separator:
                    key = InputKey.KeypadDecimal;
                    return true;

                case vk_subtract:
                    key = InputKey.KeypadSubtract;
                    return true;

                case vk_decimal:
                    key = InputKey.KeypadDecimal;
                    return true;

                case vk_divide:
                    key = InputKey.KeypadDivide;
                    return true;

                case >= vk_f1 and <= vk_f24:
                    key = InputKey.F1 + (virtualKey - vk_f1);
                    return true;

                case vk_numlock:
                    key = InputKey.NumLock;
                    return true;

                case vk_scroll:
                    key = InputKey.ScrollLock;
                    return true;

                case vk_lshift:
                    key = InputKey.LShift;
                    return true;

                case vk_rshift:
                    key = InputKey.RShift;
                    return true;

                case vk_lcontrol:
                    key = InputKey.LControl;
                    return true;

                case vk_rcontrol:
                    key = InputKey.RControl;
                    return true;

                case vk_lmenu:
                    key = InputKey.LAlt;
                    return true;

                case vk_rmenu:
                    key = InputKey.RAlt;
                    return true;

                case vk_oem_1:
                    key = InputKey.Semicolon;
                    return true;

                case vk_oem_plus:
                    key = InputKey.Plus;
                    return true;

                case vk_oem_comma:
                    key = InputKey.Comma;
                    return true;

                case vk_oem_minus:
                    key = InputKey.Minus;
                    return true;

                case vk_oem_period:
                    key = InputKey.Period;
                    return true;

                case vk_oem_2:
                    key = InputKey.Slash;
                    return true;

                case vk_oem_3:
                    key = InputKey.Tilde;
                    return true;

                case vk_oem_4:
                    key = InputKey.BracketLeft;
                    return true;

                case vk_oem_5:
                    key = InputKey.BackSlash;
                    return true;

                case vk_oem_6:
                    key = InputKey.BracketRight;
                    return true;

                case vk_oem_7:
                    key = InputKey.Quote;
                    return true;

                case vk_oem_102:
                    key = InputKey.NonUSBackSlash;
                    return true;

                default:
                    key = InputKey.None;
                    return false;
            }
        }

        private static HashSet<InputKey> getSupportedKeys()
        {
            var keys = new HashSet<InputKey>();
            for (ushort virtualKey = 0; virtualKey <= byte.MaxValue; virtualKey++)
            {
                // Generic modifiers require a real scan code. Their explicit left/right virtual keys below
                // cover the same physical keys without calling layout-dependent Win32 conversion here.
                if (virtualKey is vk_shift or vk_control or vk_menu)
                    continue;

                for (int extended = 0; extended < 2; extended++)
                {
                    RawKeyboardFlags flags = extended == 0 ? 0 : RawKeyboardFlags.E0;
                    if (tryMapInputKey(new RAWKEYBOARD { VKey = virtualKey, Flags = flags }, out InputKey key))
                        keys.Add(key);
                }
            }

            // The binding layer treats these virtual families as either physical side, all of which are decoded.
            keys.UnionWith(new[] { InputKey.Control, InputKey.Shift, InputKey.Alt, InputKey.Super });
            return keys;
        }

        private static ushort normalizeVirtualKey(RAWKEYBOARD keyboard)
        {
            switch (keyboard.VKey)
            {
                case vk_shift:
                {
                    uint mapped = mapVirtualKey(keyboard.MakeCode, mapvk_vsc_to_vk_ex);
                    return mapped != 0 ? (ushort)mapped : keyboard.VKey;
                }

                case vk_control:
                    return (keyboard.Flags & RawKeyboardFlags.E0) != 0 ? vk_rcontrol : vk_lcontrol;

                case vk_menu:
                    return (keyboard.Flags & RawKeyboardFlags.E0) != 0 ? vk_rmenu : vk_lmenu;

                default:
                    return keyboard.VKey;
            }
        }

        private static IntPtr getWindowHandle(IWindow window)
        {
            PropertyInfo? property = window.GetType().GetProperty("WindowHandle", BindingFlags.Instance | BindingFlags.Public);

            return property?.GetValue(window) is IntPtr handle ? handle : IntPtr.Zero;
        }

        private static bool registerKeyboardRawInput(IntPtr target)
        {
            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    UsagePage = hid_usage_page_generic,
                    Usage = hid_usage_generic_keyboard,
                    Flags = RawInputDeviceFlags.DeviceNotify,
                    Target = target,
                }
            };

            return registerRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }

        private static void unregisterKeyboardRawInput()
        {
            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    UsagePage = hid_usage_page_generic,
                    Usage = hid_usage_generic_keyboard,
                    Flags = RawInputDeviceFlags.Remove,
                    Target = IntPtr.Zero,
                }
            };

            registerRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }

        private static IntPtr setWindowLongPtr(IntPtr window, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? setWindowLongPtr64(window, index, value)
                : new IntPtr(setWindowLong32(window, index, value));
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [Flags]
        private enum RawInputDeviceFlags : uint
        {
            None = 0,
            Remove = 0x00000001,
            DeviceNotify = 0x00002000,
        }

        [Flags]
        private enum RawKeyboardFlags : ushort
        {
            Break = 0x0001,
            E0 = 0x0002,
            E1 = 0x0004,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public RawInputDeviceFlags Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER Header;
            public RAWINPUTDATA Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTDATA
        {
            [FieldOffset(0)]
            public RAWKEYBOARD Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public RawKeyboardFlags Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [DllImport("user32.dll", EntryPoint = "RegisterRawInputDevices", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool registerRawInputDevices(RAWINPUTDEVICE[] devices, uint numDevices, uint size);

        [DllImport("user32.dll", EntryPoint = "GetRawInputData", SetLastError = true)]
        private static extern uint getRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
        private static extern uint mapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
        private static extern short getAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr callWindowProc(IntPtr previousWindowProc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int setWindowLong32(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr setWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    }
}
