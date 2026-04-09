using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    public sealed class OmsHidDeviceCaptureSession : IDisposable
    {
        private readonly string deviceIdentifier;
        private readonly Dictionary<string, IOmsHidButtonDevice> devicesByIdentifier = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
        private readonly IOmsHidButtonDeviceProvider deviceProvider;

        private bool devicesDirty = true;
        private bool isDisposed;

        public string DeviceIdentifier => deviceIdentifier;

        public bool HasConnectedDevice => devicesByIdentifier.Count > 0;

        public OmsHidDeviceCaptureSession(string deviceIdentifier, IOmsHidButtonDeviceProvider? deviceProvider = null)
        {
            this.deviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);

            if (string.IsNullOrWhiteSpace(this.deviceIdentifier))
                throw new ArgumentException("A non-empty HID device identifier is required to capture HID input.", nameof(deviceIdentifier));

            this.deviceProvider = deviceProvider ?? OmsHidDeviceHandler.CreateDefaultDeviceProvider();
            this.deviceProvider.DevicesChanged += onDevicesChanged;
        }

        public IReadOnlyList<OmsHidDeviceChange> PollOnce()
        {
            if (isDisposed)
                return Array.Empty<OmsHidDeviceChange>();

            if (devicesDirty || devicesByIdentifier.Count == 0)
                refreshDevices();

            var capturedChanges = new List<OmsHidDeviceChange>();

            foreach (var entry in devicesByIdentifier.ToArray())
            {
                while (entry.Value.HasPendingChanges)
                {
                    if (!entry.Value.TryReadChanges(out var deviceChanges))
                        break;

                    if (deviceChanges.Count > 0)
                        capturedChanges.AddRange(deviceChanges);

                    if (!entry.Value.IsConnected)
                    {
                        disconnectDevice(entry.Key, markDirty: true);
                        break;
                    }
                }

                if (!entry.Value.IsConnected)
                    disconnectDevice(entry.Key, markDirty: true);
            }

            return capturedChanges;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            deviceProvider.DevicesChanged -= onDevicesChanged;

            foreach (var identifier in devicesByIdentifier.Keys.ToArray())
                disconnectDevice(identifier, markDirty: false);

            deviceProvider.Dispose();
        }

        private void refreshDevices()
        {
            devicesDirty = false;

            var discoveredDevices = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);

            foreach (var device in deviceProvider.GetDevices(new[] { deviceIdentifier }))
            {
                if (!discoveredDevices.TryAdd(device.Identifier, device))
                    device.Dispose();
            }

            foreach (var identifier in devicesByIdentifier.Keys.Except(discoveredDevices.Keys).ToArray())
                disconnectDevice(identifier, markDirty: false);

            foreach (var entry in discoveredDevices)
            {
                if (!devicesByIdentifier.TryAdd(entry.Key, entry.Value))
                    entry.Value.Dispose();
            }
        }

        private void disconnectDevice(string identifier, bool markDirty)
        {
            if (!devicesByIdentifier.Remove(identifier, out var device))
                return;

            device.Dispose();

            if (markDirty)
                devicesDirty = true;
        }

        private void onDevicesChanged(object? sender, EventArgs e) => devicesDirty = true;
    }
}
