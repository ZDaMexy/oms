using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace oms.Input.Devices
{
    public sealed class OmsHidDeviceCaptureSession : IDisposable
    {
        private static readonly long device_refresh_interval = Stopwatch.Frequency / 2;
        private readonly Dictionary<string, IOmsHidButtonDevice> devicesByIdentifier = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
        private readonly IOmsHidButtonDeviceProvider deviceProvider;
        private readonly List<KeyValuePair<string, IOmsHidButtonDevice>> pollingDevices = new List<KeyValuePair<string, IOmsHidButtonDevice>>(1);
        private readonly List<IOmsHidButtonDevice> retiredDevices = new List<IOmsHidButtonDevice>(1);
        private readonly object deviceGate = new object();
        private Task<DiscoverySnapshot?>? discoveryTask;

        private volatile bool devicesDirty = true;
        private long nextDeviceRefresh;
        private bool isDisposed;

        public string DeviceIdentifier { get; }

        public bool HasConnectedDevice => devicesByIdentifier.Count > 0;

        public bool IsDiscovering => discoveryTask != null;

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
            lock (deviceGate)
            {
                if (isDisposed)
                    return Array.Empty<OmsHidDeviceChange>();

                if (discoveryTask?.IsCompleted == true)
                {
                    var completed = discoveryTask;
                    discoveryTask = null;
                    using var discovered = completed.GetAwaiter().GetResult();
                    if (discovered != null)
                    {
                        commitDiscovery(discovered);
                        nextDeviceRefresh = Stopwatch.GetTimestamp() + device_refresh_interval;
                    }
                }

                List<OmsHidDeviceChange>? capturedChanges = null;

                foreach (var entry in devicesByIdentifier)
                    pollingDevices.Add(entry);

                try
                {
                    foreach (var entry in pollingDevices)
                    {
                        do
                        {
                            if (!entry.Value.TryReadChanges(out var deviceChanges))
                                break;

                            if (deviceChanges.Count > 0)
                                (capturedChanges ??= new List<OmsHidDeviceChange>()).AddRange(deviceChanges);

                            if (!entry.Value.IsConnected)
                                break;
                        } while (entry.Value.HasPendingChanges);

                        if (!entry.Value.IsConnected)
                            disconnectDevice(entry.Key);
                    }
                }
                finally
                {
                    pollingDevices.Clear();
                }

                queueMaintenance();

                return capturedChanges ?? (IReadOnlyList<OmsHidDeviceChange>)Array.Empty<OmsHidDeviceChange>();
            }
        }

        public void Dispose()
        {
            Task<DiscoverySnapshot?>? pending;
            IOmsHidButtonDevice[] retired;
            lock (deviceGate)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                deviceProvider.DevicesChanged -= onDevicesChanged;

                foreach (string identifier in devicesByIdentifier.Keys.ToArray())
                    disconnectDevice(identifier);
                pending = discoveryTask;
                discoveryTask = null;
                retired = retiredDevices.ToArray();
                retiredDevices.Clear();
            }

            try
            {
                using var uncommitted = pending?.GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    foreach (var device in retired)
                        device.Dispose();
                }
                finally
                {
                    deviceProvider.Dispose();
                }
            }
        }

        private void queueMaintenance()
        {
            if (discoveryTask != null)
                return;

            bool discover = devicesDirty || (devicesByIdentifier.Count == 0 && Stopwatch.GetTimestamp() >= nextDeviceRefresh);
            if (!discover && retiredDevices.Count == 0)
                return;

            var connected = discover ? devicesByIdentifier.Keys.ToHashSet(StringComparer.Ordinal) : null;
            var retired = retiredDevices.ToArray();
            retiredDevices.Clear();
            if (discover)
                devicesDirty = false;

            discoveryTask = Task.Run(() =>
            {
                foreach (var device in retired)
                    device.Dispose();
                return discover ? discoverDevices(connected!) : null;
            });
        }

        private DiscoverySnapshot discoverDevices(HashSet<string> connected)
        {
            var discovered = new DiscoverySnapshot();
            try
            {
                foreach (var device in deviceProvider.GetDevices(new[] { DeviceIdentifier }))
                {
                    if (!discovered.Identifiers.Add(device.Identifier) || connected.Contains(device.Identifier))
                        device.Dispose();
                    else
                        discovered.NewDevices.Add(device.Identifier, device);
                }
                return discovered;
            }
            catch
            {
                discovered.Dispose();
                throw;
            }
        }

        private void commitDiscovery(DiscoverySnapshot discovered)
        {
            foreach (string identifier in devicesByIdentifier.Keys.Except(discovered.Identifiers).ToArray())
                disconnectDevice(identifier);

            foreach (var entry in discovered.NewDevices)
                devicesByIdentifier.Add(entry.Key, entry.Value);
            discovered.NewDevices.Clear();
        }

        private void disconnectDevice(string identifier)
        {
            if (!devicesByIdentifier.Remove(identifier, out var device))
                return;

            retiredDevices.Add(device);
        }

        private void onDevicesChanged(object? sender, EventArgs e) => devicesDirty = true;

        private sealed class DiscoverySnapshot : IDisposable
        {
            public readonly HashSet<string> Identifiers = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, IOmsHidButtonDevice> NewDevices = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);

            public void Dispose()
            {
                foreach (var entry in NewDevices)
                    entry.Value.Dispose();
                NewDevices.Clear();
            }
        }
    }
}
