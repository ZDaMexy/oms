using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace oms.Input.Devices
{
    public sealed class OmsHidDeviceCaptureSession : IDisposable
    {
        private static readonly long device_refresh_interval = Stopwatch.Frequency / 2;
        private readonly Dictionary<string, IOmsHidButtonDevice> devicesByIdentifier = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
        private readonly IOmsHidButtonDeviceProvider deviceProvider;
        private readonly List<KeyValuePair<string, IOmsHidButtonDevice>> pollingDevices = new List<KeyValuePair<string, IOmsHidButtonDevice>>(1);

        private volatile bool devicesDirty = true;
        private long nextDeviceRefresh;
        private bool isDisposed;

        public string DeviceIdentifier { get; }

        public bool HasConnectedDevice => devicesByIdentifier.Count > 0;

        public OmsHidDeviceCaptureSession(string deviceIdentifier, IOmsHidButtonDeviceProvider? deviceProvider = null)
        {
            DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);

            if (string.IsNullOrWhiteSpace(DeviceIdentifier))
                throw new ArgumentException("A non-empty HID device identifier is required to capture HID input.", nameof(deviceIdentifier));

            this.deviceProvider = deviceProvider ?? OmsHidDeviceHandler.CreateDefaultDeviceProvider();
            this.deviceProvider.DevicesChanged += onDevicesChanged;
        }

        public IReadOnlyList<OmsHidDeviceChange> PollOnce()
        {
            if (isDisposed)
                return Array.Empty<OmsHidDeviceChange>();

            if (devicesDirty || (devicesByIdentifier.Count == 0 && Stopwatch.GetTimestamp() >= nextDeviceRefresh))
                refreshDevices();

            List<OmsHidDeviceChange>? capturedChanges = null;

            foreach (var entry in devicesByIdentifier)
                pollingDevices.Add(entry);

            foreach (var entry in pollingDevices)
            {
                do
                {
                    if (!entry.Value.TryReadChanges(out var deviceChanges))
                        break;

                    if (deviceChanges.Count > 0)
                        (capturedChanges ??= new List<OmsHidDeviceChange>()).AddRange(deviceChanges);

                    if (!entry.Value.IsConnected)
                    {
                        disconnectDevice(entry.Key);
                        break;
                    }
                } while (entry.Value.HasPendingChanges);

                if (!entry.Value.IsConnected)
                    disconnectDevice(entry.Key);
            }

            pollingDevices.Clear();
            return capturedChanges ?? (IReadOnlyList<OmsHidDeviceChange>)Array.Empty<OmsHidDeviceChange>();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            deviceProvider.DevicesChanged -= onDevicesChanged;

            foreach (string? identifier in devicesByIdentifier.Keys.ToArray())
                disconnectDevice(identifier);

            deviceProvider.Dispose();
        }

        private void refreshDevices()
        {
            devicesDirty = false;
            nextDeviceRefresh = Stopwatch.GetTimestamp() + device_refresh_interval;

            var discoveredDevices = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);

            foreach (var device in deviceProvider.GetDevices(new[] { DeviceIdentifier }))
            {
                if (!discoveredDevices.TryAdd(device.Identifier, device))
                    device.Dispose();
            }

            foreach (string? identifier in devicesByIdentifier.Keys.Except(discoveredDevices.Keys).ToArray())
                disconnectDevice(identifier);

            foreach (var entry in discoveredDevices)
            {
                if (!devicesByIdentifier.TryAdd(entry.Key, entry.Value))
                    entry.Value.Dispose();
            }
        }

        private void disconnectDevice(string identifier)
        {
            if (!devicesByIdentifier.Remove(identifier, out var device))
                return;

            device.Dispose();
        }

        private void onDevicesChanged(object? sender, EventArgs e) => devicesDirty = true;
    }
}
