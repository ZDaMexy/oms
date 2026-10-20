using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Logging;
using Vortice.DirectInput;

namespace oms.Input.Devices
{
    internal static class OmsWindowsDirectInputRuntime
    {
        private static int directInputInitialisationFailureLogged;

        public static bool TryCreateDirectInput(out IDirectInput8 directInput)
        {
            directInput = null!;

            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                var result = DInput.DirectInput8Create(out IDirectInput8? createdDirectInput);

                if (result.Failure || createdDirectInput == null)
                {
                    logInitialisationFailure(new InvalidOperationException("DirectInput8Create failed."));
                    return false;
                }

                directInput = createdDirectInput;
                return true;
            }
            catch (Exception e) when (IsRecoverableFailure(e))
            {
                logInitialisationFailure(e);
                return false;
            }
        }

        public static bool IsRecoverableFailure(Exception exception)
        {
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1)
                return IsRecoverableFailure(aggregateException.InnerExceptions[0]);

            if (exception is TypeInitializationException or TargetInvocationException)
                return exception.InnerException != null && IsRecoverableFailure(exception.InnerException);

            return exception is InvalidOperationException
                or PlatformNotSupportedException
                or DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or UnauthorizedAccessException
                or COMException;
        }

        public static void LogRecoverableFailure(Exception exception)
            => logInitialisationFailure(exception);

        private static void logInitialisationFailure(Exception exception)
        {
            if (Interlocked.Exchange(ref directInputInitialisationFailureLogged, 1) != 0)
                return;

            Logger.Error(exception, "DirectInput failed to initialise. OMS will continue with Windows HID support disabled.");
        }
    }

    internal static class OmsWindowsDirectInputDiscovery
    {
        public static IReadOnlyList<OmsHidDeviceInfo> GetConnectedDevices()
        {
            if (!OmsWindowsDirectInputRuntime.TryCreateDirectInput(out var directInput))
                return Array.Empty<OmsHidDeviceInfo>();

            using (directInput)
            {
                return OmsWindowsDirectInputButtonDeviceProvider.GetConnectedDevices(directInput);
            }
        }
    }

    internal sealed class OmsWindowsDirectInputButtonDeviceProvider : IOmsHidButtonDeviceProvider
    {
        private readonly IDirectInput8 directInput;
        private readonly HashSet<string> reportedAmbiguities = new HashSet<string>(StringComparer.Ordinal);

        public event EventHandler? DevicesChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public OmsWindowsDirectInputButtonDeviceProvider()
        {
            if (!OmsWindowsDirectInputRuntime.TryCreateDirectInput(out directInput))
                throw new InvalidOperationException("DirectInput is unavailable.");
        }

        public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
        {
            string[] requestedIdentifiers = deviceIdentifiers.Select(OmsHidDeviceIdentifier.Normalize).Distinct(StringComparer.Ordinal).ToArray();
            if (requestedIdentifiers.Length == 0)
                yield break;

            var instances = EnumerateCandidateDevices(directInput);
            var inventory = new List<OmsHidDeviceInfo>(instances.Count);
            foreach (var instance in instances)
            {
                if (tryCreateDeviceInfo(directInput, instance, out var info))
                    inventory.Add(info);
            }

            var aliasesByIdentifier = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string requested in requestedIdentifiers)
            {
                if (!OmsHidDeviceDiscovery.TryResolveIdentifier(requested, inventory, out string identifier, out string? diagnostic))
                {
                    if (inventory.Any(info => info.LegacyIdentifier == requested) && reportedAmbiguities.Add(requested))
                        Logger.Log(diagnostic!, level: LogLevel.Important);
                    continue;
                }

                reportedAmbiguities.Remove(requested);

                if (!aliasesByIdentifier.TryGetValue(identifier, out var aliases))
                    aliasesByIdentifier.Add(identifier, aliases = new List<string>());
                if (identifier != requested)
                    aliases.Add(requested);
            }

            foreach (var instance in instances)
            {
                string identifier = OmsHidDeviceIdentifier.FromDirectInputDevice(0, 0, null, instance.InstanceGuid);
                if (aliasesByIdentifier.TryGetValue(identifier, out var aliases)
                    && OmsWindowsDirectInputButtonDevice.TryCreate(directInput, instance, aliases.ToArray(), out var device))
                    yield return device;
            }
        }

        public void Dispose()
        {
            directInput.Dispose();
        }

        internal static IReadOnlyList<OmsHidDeviceInfo> GetConnectedDevices(IDirectInput8 directInput)
        {
            return EnumerateCandidateDevices(directInput)
                .Select(deviceInstance => tryCreateDeviceInfo(directInput, deviceInstance, out var deviceInfo) ? deviceInfo : default)
                .Where(deviceInfo => !string.IsNullOrWhiteSpace(deviceInfo.Identifier))
                .GroupBy(deviceInfo => deviceInfo.Identifier, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(deviceInfo => deviceInfo.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static IReadOnlyList<DeviceInstance> EnumerateCandidateDevices(IDirectInput8 directInput)
        {
            try
            {
                return directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                                  .Where(deviceInstance => deviceInstance.IsHumanInterfaceDevice
                                      && (deviceInstance.Type == DeviceType.Gamepad || deviceInstance.Type == DeviceType.Joystick))
                                  .ToArray();
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                return Array.Empty<DeviceInstance>();
            }
        }

        internal static bool TryCreateInputDevice(IDirectInput8 directInput, DeviceInstance deviceInstance, out IDirectInputDevice8 inputDevice)
        {
            inputDevice = null!;

            try
            {
                var result = directInput.CreateDevice(deviceInstance.InstanceGuid, out IDirectInputDevice8? createdDevice);

                if (result.Failure || createdDevice == null)
                    return false;

                inputDevice = createdDevice;
                return true;
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                return false;
            }
        }

        internal static bool TryCreateMetadata(IDirectInputDevice8 inputDevice, DeviceInstance deviceInstance, out DirectInputDeviceMetadata metadata)
        {
            metadata = default;

            try
            {
                int vendorId = inputDevice.Properties.VendorId;
                int productId = inputDevice.Properties.ProductId;
                string interfacePath = inputDevice.Properties.InterfacePath;
                string identifier = OmsHidDeviceIdentifier.FromDirectInputDevice(vendorId, productId, interfacePath, deviceInstance.InstanceGuid);
                string legacyIdentifier = OmsHidDeviceIdentifier.LegacyFromDirectInputDevice(vendorId, productId, interfacePath);

                if (string.IsNullOrWhiteSpace(identifier))
                    return false;

                metadata = new DirectInputDeviceMetadata(identifier, buildDisplayName(inputDevice, deviceInstance, vendorId, productId, identifier), legacyIdentifier);
                return true;
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                return false;
            }
        }

        private static bool tryCreateDeviceInfo(IDirectInput8 directInput, DeviceInstance deviceInstance, out OmsHidDeviceInfo deviceInfo)
        {
            deviceInfo = default;

            if (!TryCreateInputDevice(directInput, deviceInstance, out var inputDevice))
                return false;

            using (inputDevice)
            {
                if (!TryCreateMetadata(inputDevice, deviceInstance, out var metadata))
                    return false;

                deviceInfo = new OmsHidDeviceInfo(metadata.Identifier, metadata.DisplayName, metadata.LegacyIdentifier);
                return true;
            }
        }

        private static string buildDisplayName(IDirectInputDevice8 inputDevice, DeviceInstance deviceInstance, int vendorId, int productId, string identifier)
        {
            string displayName = new[]
            {
                inputDevice.Properties.ProductName,
                inputDevice.Properties.InstanceName,
                deviceInstance.ProductName,
            }.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?.Trim()
             ?? $"HID {vendorId:x4}:{productId:x4}";

            return $"{displayName} ({identifier})";
        }

        internal readonly struct DirectInputDeviceMetadata
        {
            public string Identifier { get; }

            public string DisplayName { get; }

            public string LegacyIdentifier { get; }

            public DirectInputDeviceMetadata(string identifier, string displayName, string legacyIdentifier)
            {
                Identifier = identifier;
                DisplayName = displayName;
                LegacyIdentifier = legacyIdentifier;
            }
        }
    }

    internal sealed class OmsWindowsDirectInputButtonDevice : IOmsHidButtonDevice
    {
        private const int buffered_event_capacity = 1024;
        private const int maximum_events_per_poll = buffered_event_capacity * 4;
        private readonly IDirectInputDevice8 inputDevice;
        private readonly List<OmsHidDeviceChange> currentChanges = new List<OmsHidDeviceChange>(maximum_events_per_poll + 128 + 8);
        private readonly BufferedObjectData[] bufferedEvents = new BufferedObjectData[buffered_event_capacity];
        private readonly bool[] previousButtons = new bool[128];
        private readonly bool[] initialButtonHistory = new bool[128];
        private readonly Dictionary<int, int> axisValuesByIndex = new Dictionary<int, int>(8);
        private readonly Dictionary<int, AxisDescription>? axisDescriptions;

        private JoystickState currentState = new JoystickState();
        private bool hasInitialState;
        private int isDisposed;

        public string Identifier { get; }

        public IReadOnlyList<string> BindingAliases { get; }

        public bool IsConnected { get; private set; } = true;

        // A single read polls once and drains a bounded batch. It must never be polled again by the owner
        // while draining the same gameplay update.
        public bool HasPendingChanges => false;

        private OmsWindowsDirectInputButtonDevice(string identifier, IDirectInputDevice8 inputDevice)
            : this(identifier, inputDevice, Array.Empty<string>(), null)
        {
        }

        private OmsWindowsDirectInputButtonDevice(string identifier, IDirectInputDevice8 inputDevice, IReadOnlyList<string> bindingAliases,
                                                 Dictionary<int, AxisDescription>? axisDescriptions)
        {
            Identifier = identifier;
            this.inputDevice = inputDevice;
            BindingAliases = bindingAliases;
            this.axisDescriptions = axisDescriptions;
        }

        public static bool TryCreate(IDirectInput8 directInput, DeviceInstance deviceInstance, IReadOnlyList<string> bindingAliases, out IOmsHidButtonDevice device)
        {
            device = null!;

            if (!OmsWindowsDirectInputButtonDeviceProvider.TryCreateInputDevice(directInput, deviceInstance, out var inputDevice))
                return false;

            try
            {
                if (!OmsWindowsDirectInputButtonDeviceProvider.TryCreateMetadata(inputDevice, deviceInstance, out var metadata))
                {
                    inputDevice.Dispose();
                    return false;
                }

                IntPtr windowHandle = getWindowHandle();

                if (windowHandle == IntPtr.Zero)
                {
                    inputDevice.Dispose();
                    return false;
                }

                inputDevice.SetCooperativeLevel(windowHandle, CooperativeLevel.NonExclusive | CooperativeLevel.Foreground);

                var setDataFormatResult = inputDevice.SetDataFormat<RawJoystickState>();

                if (setDataFormatResult.Failure)
                {
                    inputDevice.Dispose();
                    return false;
                }

                inputDevice.Properties.BufferSize = buffered_event_capacity;
                var descriptions = new Dictionary<int, AxisDescription>();
                bool relative = inputDevice.Properties.AxisMode == DeviceAxisMode.Relative;
                foreach (var axis in inputDevice.GetObjects(DeviceObjectTypeFlags.Axis))
                {
                    if (!tryGetAxisIndex(axis.Offset, out int axisIndex))
                        continue;

                    var range = inputDevice.GetObjectPropertiesByName(axis.Name).Range;
                    descriptions.Add(axisIndex, new AxisDescription(range.Minimum, range.Maximum, relative));
                }

                device = new OmsWindowsDirectInputButtonDevice(metadata.Identifier, inputDevice, bindingAliases, descriptions);
                return true;
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                inputDevice.Dispose();
                return false;
            }
        }

        public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
        {
            changes = Array.Empty<OmsHidDeviceChange>();
            if (!IsConnected)
                return false;

            currentChanges.Clear();

            try
            {
                // Event-driven devices may return DI_NOEFFECT from Poll even before acquisition.
                // Establish ownership explicitly instead of using Poll failure as an acquisition signal.
                if (!hasInitialState && inputDevice.Acquire().Failure)
                {
                    IsConnected = false;
                    return false;
                }

                var result = inputDevice.Poll();

                if (result.Failure)
                {
                    // Re-acquisition cannot recover changes missed while input was lost. End this device
                    // ownership so the caller releases its buttons before a fresh connection baseline.
                    IsConnected = false;
                    return false;
                }

                bool initial = !hasInitialState;
                if (initial)
                {
                    inputDevice.GetCurrentJoystickState(ref currentState);
                    queueAxisChanges(currentState, currentChanges);
                    hasInitialState = true;
                }

                int remaining = maximum_events_per_poll;
                while (remaining > 0)
                {
                    int count = Math.Min(bufferedEvents.Length, remaining);
                    int resultCode = readBufferedEvents(ref count);
                    if (resultCode == 1) // DI_BUFFEROVERFLOW: history is incomplete; do not invent a successful tap.
                    {
                        Logger.Log($"HID 输入缓冲溢出：{Identifier}。输入历史已丢失，释放该设备并重新连接。", level: LogLevel.Important);
                        currentChanges.Clear();
                        IsConnected = false;
                        return false;
                    }

                    Marshal.ThrowExceptionForHR(resultCode);
                    for (int index = 0; index < count; index++)
                        queueBufferedChange(bufferedEvents[index], initial);

                    remaining -= count;
                    if (count < bufferedEvents.Length)
                        break;
                }

                if (initial)
                    queueInitialButtonState(currentState);

                changes = currentChanges;
                return changes.Count > 0;
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                IsConnected = false;
                return false;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) != 0)
                return;

            IsConnected = false;
            inputDevice.Dispose();
        }

        private void queueInitialButtonState(JoystickState state)
        {
            for (int buttonIndex = 0; buttonIndex < Math.Min(previousButtons.Length, state.Buttons.Length); buttonIndex++)
            {
                // Buffered edges define their own order, including complete taps after GetDeviceState.
                // The snapshot supplies only held buttons with no buffered history; replaying it before
                // that history could duplicate or reorder the first real press.
                if (initialButtonHistory[buttonIndex])
                    continue;

                bool pressed = state.Buttons[buttonIndex];

                if (pressed == previousButtons[buttonIndex])
                    continue;

                previousButtons[buttonIndex] = pressed;
                currentChanges.Add(OmsHidDeviceChange.Button(new OmsHidButtonChange(Identifier, buttonIndex, pressed)));
            }
        }

        private void queueAxisChanges(JoystickState state, List<OmsHidDeviceChange> changes)
        {
            queueAxisChange(0, state.X, changes);
            queueAxisChange(1, state.Y, changes);
            queueAxisChange(2, state.Z, changes);
            queueAxisChange(3, state.RotationX, changes);
            queueAxisChange(4, state.RotationY, changes);
            queueAxisChange(5, state.RotationZ, changes);

            if (state.Sliders.Length > 0)
                queueAxisChange(6, state.Sliders[0], changes);

            if (state.Sliders.Length > 1)
                queueAxisChange(7, state.Sliders[1], changes);
        }

        private void queueAxisChange(int axisIndex, int currentValue, List<OmsHidDeviceChange> changes)
        {
            if (axisDescriptions != null && !axisDescriptions.ContainsKey(axisIndex))
                return;

            AxisDescription description = axisDescriptions?.GetValueOrDefault(axisIndex) ?? default;
            if (!hasInitialState)
            {
                axisValuesByIndex[axisIndex] = currentValue;
                if (!description.IsRelative)
                    changes.Add(OmsHidDeviceChange.Axis(new OmsHidAxisChange(Identifier, axisIndex, 0, currentValue,
                        description.Minimum, description.Maximum, false)));
                return;
            }

            int previousValue = axisValuesByIndex.GetValueOrDefault(axisIndex, currentValue);
            axisValuesByIndex[axisIndex] = currentValue;

            int delta = description.IsRelative ? currentValue : currentValue - previousValue;

            if (delta == 0)
                return;

            changes.Add(OmsHidDeviceChange.Axis(new OmsHidAxisChange(Identifier, axisIndex, delta, currentValue,
                description.Minimum, description.Maximum, description.IsRelative)));
        }

        private void queueBufferedChange(BufferedObjectData data, bool initial)
        {
            int previousCount = currentChanges.Count;
            if (data.Offset >= 48 && data.Offset < 48 + previousButtons.Length)
            {
                int buttonIndex = data.Offset - 48;
                if (initial)
                    initialButtonHistory[buttonIndex] = true;
                bool pressed = (data.Data & 0x80) != 0;
                if (previousButtons[buttonIndex] == pressed)
                    return;

                previousButtons[buttonIndex] = pressed;
                currentChanges.Add(OmsHidDeviceChange.Button(new OmsHidButtonChange(Identifier, buttonIndex, pressed)));
            }
            // Initial absolute positions have already been calibrated from the snapshot. Older axis
            // history cannot be measured against that newer position; subsequent polls use the buffer.
            else if (!initial && tryGetAxisIndex(data.Offset, out int axisIndex))
                queueAxisChange(axisIndex, data.Data, currentChanges);

            if (currentChanges.Count > previousCount)
            {
                var change = currentChanges[^1];
                long received = Stopwatch.GetTimestamp();
                currentChanges[^1] = change.Kind == OmsHidDeviceChangeKind.Button
                    ? OmsHidDeviceChange.Button(change.ButtonChange, data.Timestamp, data.Sequence, received)
                    : OmsHidDeviceChange.Axis(change.AxisChange, data.Timestamp, data.Sequence, received);
            }
        }

        private static bool tryGetAxisIndex(int offset, out int axisIndex)
        {
            axisIndex = offset / sizeof(int);
            return offset >= 0 && offset < 8 * sizeof(int) && offset % sizeof(int) == 0;
        }

        private unsafe int readBufferedEvents(ref int count)
        {
            // Vortice 2.4.2 exposes only an allocating public GetBufferedData<T>(). Its internal
            // GetDeviceData uses this fixed IDirectInputDevice8W ABI; keep the reusable native buffer here.
            fixed (BufferedObjectData* events = bufferedEvents)
            fixed (int* countPointer = &count)
            {
                var method = (delegate* unmanaged[Stdcall]<IntPtr, int, BufferedObjectData*, int*, int, int>)(*(void***)inputDevice.NativePointer)[10];
                return method(inputDevice.NativePointer, sizeof(BufferedObjectData), events, countPointer, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BufferedObjectData
        {
            public int Offset;
            public int Data;
            public uint Timestamp;
            public uint Sequence;
            public UIntPtr ApplicationData;
        }

        private readonly struct AxisDescription
        {
            public readonly int? Minimum;
            public readonly int? Maximum;
            public readonly bool IsRelative;

            public AxisDescription(int minimum, int maximum, bool isRelative)
            {
                Minimum = minimum;
                Maximum = maximum;
                IsRelative = isRelative;
            }
        }

        private static IntPtr getWindowHandle()
        {
            IntPtr windowHandle = Process.GetCurrentProcess().MainWindowHandle;

            if (windowHandle != IntPtr.Zero)
                return windowHandle;

            windowHandle = getForegroundWindow();

            if (windowHandle == IntPtr.Zero)
                return IntPtr.Zero;

            getWindowThreadProcessId(windowHandle, out uint processId);
            return processId == (uint)Environment.ProcessId ? windowHandle : IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr getForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint getWindowThreadProcessId(IntPtr windowHandle, out uint processId);
    }
}
