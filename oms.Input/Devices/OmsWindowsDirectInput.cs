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
            var targetIdentifiers = deviceIdentifiers.ToHashSet(StringComparer.Ordinal);

            if (targetIdentifiers.Count == 0)
                yield break;

            foreach (var deviceInstance in EnumerateCandidateDevices(directInput))
            {
                if (!OmsWindowsDirectInputButtonDevice.TryCreate(directInput, deviceInstance, out var device))
                    continue;

                if (!targetIdentifiers.Contains(device.Identifier))
                {
                    device.Dispose();
                    continue;
                }

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
                .Select(deviceInstance => TryCreateDeviceInfo(directInput, deviceInstance, out var deviceInfo) ? deviceInfo : default)
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

                if (string.IsNullOrWhiteSpace(identifier))
                    return false;

                metadata = new DirectInputDeviceMetadata(identifier, buildDisplayName(inputDevice, deviceInstance, vendorId, productId, identifier));
                return true;
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                return false;
            }
        }

        private static bool TryCreateDeviceInfo(IDirectInput8 directInput, DeviceInstance deviceInstance, out OmsHidDeviceInfo deviceInfo)
        {
            deviceInfo = default;

            if (!TryCreateInputDevice(directInput, deviceInstance, out var inputDevice))
                return false;

            using (inputDevice)
            {
                if (!TryCreateMetadata(inputDevice, deviceInstance, out var metadata))
                    return false;

                deviceInfo = new OmsHidDeviceInfo(metadata.Identifier, metadata.DisplayName);
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

            public DirectInputDeviceMetadata(string identifier, string displayName)
            {
                Identifier = identifier;
                DisplayName = displayName;
            }
        }
    }

    internal sealed class OmsWindowsDirectInputButtonDevice : IOmsHidButtonDevice
    {
        private readonly IDirectInputDevice8 inputDevice;
        private readonly Queue<IReadOnlyList<OmsHidDeviceChange>> queuedChanges = new Queue<IReadOnlyList<OmsHidDeviceChange>>();
        private readonly bool[] previousButtons = new bool[128];
        private readonly Dictionary<int, int> axisValuesByIndex = new Dictionary<int, int>();

        private bool hasInitialState;

        public string Identifier { get; }

        public bool IsConnected { get; private set; } = true;

        public bool HasPendingChanges
        {
            get
            {
                if (!IsConnected)
                    return false;

                if (queuedChanges.Count == 0)
                    pollCurrentStateIntoQueue();

                return queuedChanges.Count > 0;
            }
        }

        private OmsWindowsDirectInputButtonDevice(string identifier, IDirectInputDevice8 inputDevice)
        {
            Identifier = identifier;
            this.inputDevice = inputDevice;
        }

        public static bool TryCreate(IDirectInput8 directInput, DeviceInstance deviceInstance, out IOmsHidButtonDevice device)
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

                device = new OmsWindowsDirectInputButtonDevice(metadata.Identifier, inputDevice);
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
            if (queuedChanges.Count == 0)
                pollCurrentStateIntoQueue();

            if (queuedChanges.Count == 0)
            {
                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }

            changes = queuedChanges.Dequeue();
            return changes.Count > 0;
        }

        public void Dispose()
        {
            if (!IsConnected)
                return;

            IsConnected = false;
            inputDevice.Dispose();
        }

        private void pollCurrentStateIntoQueue()
        {
            if (!IsConnected || queuedChanges.Count > 0)
                return;

            try
            {
                var result = inputDevice.Poll();

                if (result.Failure)
                {
                    result = inputDevice.Acquire();

                    if (result.Failure)
                    {
                        IsConnected = false;
                        return;
                    }
                }

                var state = inputDevice.GetCurrentJoystickState();
                var changes = new List<OmsHidDeviceChange>();

                queueButtonChanges(state, changes);
                queueAxisChanges(state, changes);

                hasInitialState = true;

                if (changes.Count > 0)
                    queuedChanges.Enqueue(changes);
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                IsConnected = false;
            }
        }

        private void queueButtonChanges(JoystickState state, List<OmsHidDeviceChange> changes)
        {
            for (int buttonIndex = 0; buttonIndex < Math.Min(previousButtons.Length, state.Buttons.Length); buttonIndex++)
            {
                bool pressed = state.Buttons[buttonIndex];

                if (pressed == previousButtons[buttonIndex])
                    continue;

                previousButtons[buttonIndex] = pressed;
                changes.Add(OmsHidDeviceChange.Button(new OmsHidButtonChange(Identifier, buttonIndex, pressed)));
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
            if (!hasInitialState)
            {
                axisValuesByIndex[axisIndex] = currentValue;
                return;
            }

            int previousValue = axisValuesByIndex.GetValueOrDefault(axisIndex, currentValue);
            axisValuesByIndex[axisIndex] = currentValue;

            int delta = currentValue - previousValue;

            if (delta == 0)
                return;

            changes.Add(OmsHidDeviceChange.Axis(new OmsHidAxisChange(Identifier, axisIndex, delta)));
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
