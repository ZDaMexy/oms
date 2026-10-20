// Copyright (c) OMS contributors. Licensed under the MIT Licence.

extern alias OmsHidSharp;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataItem = OmsHidSharp::HidSharp.Reports.DataItem;
using DeviceList = OmsHidSharp::HidSharp.DeviceList;
using DeviceListChangedEventArgs = OmsHidSharp::HidSharp.DeviceListChangedEventArgs;
using HidDevice = OmsHidSharp::HidSharp.HidDevice;
using HidDeviceInputReceiver = OmsHidSharp::HidSharp.Reports.Input.HidDeviceInputReceiver;
using HidStream = OmsHidSharp::HidSharp.HidStream;
using Report = OmsHidSharp::HidSharp.Reports.Report;
using ReportDescriptor = OmsHidSharp::HidSharp.Reports.ReportDescriptor;

namespace oms.Input.Devices
{
    public readonly struct OmsHidButtonChange
    {
        public string DeviceIdentifier { get; }

        public int ButtonIndex { get; }

        public bool Pressed { get; }

        public OmsHidButtonChange(string deviceIdentifier, int buttonIndex, bool pressed)
        {
            DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            ButtonIndex = buttonIndex;
            Pressed = pressed;
        }
    }

    public readonly struct OmsHidAxisChange
    {
        public string DeviceIdentifier { get; }

        public int AxisIndex { get; }

        public int Delta { get; }

        public int? Value { get; }

        public int? Minimum { get; }

        public int? Maximum { get; }

        public bool IsRelative { get; }

        public OmsHidAxisChange(string deviceIdentifier, int axisIndex, int delta)
        {
            DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            AxisIndex = axisIndex;
            Delta = delta;
            Value = Minimum = Maximum = null;
            IsRelative = true;
        }

        public OmsHidAxisChange(string deviceIdentifier, int axisIndex, int delta, int value, int? minimum, int? maximum, bool isRelative)
        {
            DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            AxisIndex = axisIndex;
            Delta = delta;
            Value = value;
            Minimum = minimum;
            Maximum = maximum;
            IsRelative = isRelative;
        }
    }

    public enum OmsHidDeviceChangeKind
    {
        Button,
        Axis,
    }

    public readonly struct OmsHidDeviceChange
    {
        public OmsHidDeviceChangeKind Kind { get; }

        public OmsHidButtonChange ButtonChange { get; }

        public OmsHidAxisChange AxisChange { get; }

        public uint? NativeTimestamp { get; }

        public uint? NativeSequence { get; }

        public long? ReceivedTimestamp { get; }

        private OmsHidDeviceChange(OmsHidDeviceChangeKind kind, OmsHidButtonChange buttonChange = default, OmsHidAxisChange axisChange = default,
                                  uint? nativeTimestamp = null, uint? nativeSequence = null, long? receivedTimestamp = null)
        {
            Kind = kind;
            ButtonChange = buttonChange;
            AxisChange = axisChange;
            NativeTimestamp = nativeTimestamp;
            NativeSequence = nativeSequence;
            ReceivedTimestamp = receivedTimestamp;
        }

        public static OmsHidDeviceChange Button(OmsHidButtonChange change, uint? nativeTimestamp = null, uint? nativeSequence = null, long? receivedTimestamp = null)
            => new OmsHidDeviceChange(OmsHidDeviceChangeKind.Button, buttonChange: change,
                nativeTimestamp: nativeTimestamp, nativeSequence: nativeSequence, receivedTimestamp: receivedTimestamp);

        public static OmsHidDeviceChange Axis(OmsHidAxisChange change, uint? nativeTimestamp = null, uint? nativeSequence = null, long? receivedTimestamp = null)
            => new OmsHidDeviceChange(OmsHidDeviceChangeKind.Axis, axisChange: change,
                nativeTimestamp: nativeTimestamp, nativeSequence: nativeSequence, receivedTimestamp: receivedTimestamp);
    }

    public interface IOmsHidButtonDevice : IDisposable
    {
        string Identifier { get; }

        IReadOnlyList<string> BindingAliases => Array.Empty<string>();

        bool IsConnected { get; }

        bool HasPendingChanges { get; }

        bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes);
    }

    public interface IOmsHidButtonDeviceProvider : IDisposable
    {
        event EventHandler? DevicesChanged;

        IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers);
    }

    public static class OmsHidDeviceIdentifier
    {
        public static string Normalize(string deviceIdentifier)
            => string.IsNullOrWhiteSpace(deviceIdentifier) ? string.Empty : deviceIdentifier.Trim().ToLowerInvariant();

        public static string ForVendorProduct(int vendorId, int productId, string? serialNumber = null)
        {
            string identifier = $"hid:vid_{vendorId:x4}&pid_{productId:x4}";

            if (!string.IsNullOrWhiteSpace(serialNumber))
                identifier = $"{identifier}&serial_{serialNumber.Trim()}";

            return Normalize(identifier);
        }

        public static string FromHidDevice(HidDevice device)
            => ForVendorProduct(device.VendorID, device.ProductID, device.GetSerialNumber());

        internal static string FromDirectInputDevice(int vendorId, int productId, string? interfacePath, Guid instanceGuid)
        {
            if (instanceGuid != Guid.Empty)
                return Normalize($"dinput:instance_{instanceGuid:N}");

            if (!string.IsNullOrWhiteSpace(interfacePath))
                return Normalize($"hid:path_{interfacePath}");

            return string.Empty;
        }

        internal static string LegacyFromDirectInputDevice(int vendorId, int productId, string? interfacePath)
        {
            if (vendorId > 0 || productId > 0)
                return ForVendorProduct(vendorId, productId, TryExtractSerialFromInterfacePath(interfacePath));

            if (tryParseVendorProductFromInterfacePath(interfacePath, out int parsedVendorId, out int parsedProductId))
                return ForVendorProduct(parsedVendorId, parsedProductId, TryExtractSerialFromInterfacePath(interfacePath));

            return string.Empty;
        }

        internal static string? TryExtractSerialFromInterfacePath(string? interfacePath)
        {
            if (string.IsNullOrWhiteSpace(interfacePath))
                return null;

            string[] parts = interfacePath.Split('#', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                return null;

            string candidate = parts[2].Trim();
            int guidIndex = candidate.IndexOf('{');

            if (guidIndex >= 0)
                candidate = candidate[..guidIndex];

            if (candidate.Length == 0 || candidate.Contains('&', StringComparison.Ordinal) || candidate.Contains('\\', StringComparison.Ordinal))
                return null;

            return candidate;
        }

        private static bool tryParseVendorProductFromInterfacePath(string? interfacePath, out int vendorId, out int productId)
        {
            vendorId = 0;
            productId = 0;

            if (string.IsNullOrWhiteSpace(interfacePath))
                return false;

            string normalizedPath = interfacePath.ToLowerInvariant();
            int vendorIndex = normalizedPath.IndexOf("vid_", StringComparison.Ordinal);
            int productIndex = normalizedPath.IndexOf("pid_", StringComparison.Ordinal);

            if (vendorIndex < 0 || productIndex < 0 || vendorIndex + 8 > normalizedPath.Length || productIndex + 8 > normalizedPath.Length)
                return false;

            return int.TryParse(normalizedPath.AsSpan(vendorIndex + 4, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out vendorId)
                && int.TryParse(normalizedPath.AsSpan(productIndex + 4, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out productId);
        }
    }

    /// <summary>
    /// Enumerates compatible HID devices, drains queued input reports, and forwards
    /// HID button and axis transitions into OMS action callbacks.
    /// </summary>
    public sealed class OmsHidDeviceHandler : IDisposable
    {
        private static readonly long device_refresh_interval = Stopwatch.Frequency / 2;

        private readonly OmsHidButtonInputHandler hidButtonInputHandler;
        private readonly OmsHidAxisInputHandler hidAxisInputHandler;
        private readonly object deviceGate = new object();
        private readonly HashSet<string> boundDeviceIdentifiers;
        private readonly Dictionary<string, IOmsHidButtonDevice> devicesByIdentifier = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<int>> activeButtonsByDevice = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> bindingIdentifiersByDevice = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly IOmsHidButtonDeviceProvider? deviceProvider;
        private readonly Func<long> getTimestamp;
        private readonly List<KeyValuePair<string, IOmsHidButtonDevice>> pollingDevices;
        private readonly List<IOmsHidButtonDevice> retiredDevices;
        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Action<OmsAxisMotion>? acceptedMotion;
        private readonly Dictionary<OmsAction, int> appliedReferences = new Dictionary<OmsAction, int>();
        private Action<OmsHidInputEvent>? capture;
        private bool suppressDelivery;

        private volatile bool devicesDirty = true;
        private Task<DeviceDiscoverySnapshot?>? discoveryTask;
        private long nextDeviceRefresh;
        private bool hasUnresolvedBindings = true;
        private bool usesLegacyAliases;
        private bool discardNextPoll;
        private bool isDisposed;

        public int ConnectedDeviceCount => devicesByIdentifier.Count;

        public bool HasBoundDevices => boundDeviceIdentifiers.Count > 0;

        public event Action<string>? DeviceDisconnected;

        public void UpdateAxes(double gameplayTime)
        {
            hidAxisInputHandler.FinishPolling();
            hidAxisInputHandler.Update(gameplayTime);
            hidAxisInputHandler.FinishPolling();
        }

        public void ResetAxes() => hidAxisInputHandler.ReleaseAll();

        /// <summary>
        /// Ends the applied device-input epoch on the gameplay thread. Buffered transitions which accumulated
        /// before the next poll update device baselines, but cannot become new gameplay presses after a pause.
        /// </summary>
        public void ResetInput(bool releaseApplied = true)
        {
            lock (deviceGate)
            {
                suppressDelivery = true;
                try
                {
                    foreach (var entry in activeButtonsByDevice)
                    {
                        foreach (int button in entry.Value)
                        {
                            foreach (string identifier in bindingIdentifiersByDevice[entry.Key])
                                hidButtonInputHandler.TriggerReleased(identifier, button);
                        }
                        entry.Value.Clear();
                    }
                    hidAxisInputHandler.ReleaseAll();
                    discardNextPoll = true;
                }
                finally
                {
                    suppressDelivery = false;
                }
                if (releaseApplied)
                    ReleaseAppliedInput();
            }
        }

        /// <summary>Releases only device action references which actually reached gameplay.</summary>
        public void ReleaseAppliedInput()
        {
            foreach (OmsAction action in appliedReferences.Keys)
            {
                int count = appliedReferences[action];
                appliedReferences[action] = 0;
                for (int i = 0; i < count; i++)
                    releaseAction(action);
            }
        }

        /// <summary>
        /// Captures a single outer-frame device batch. No external gameplay callbacks run here.
        /// Values are copied immediately, so backend-owned reusable report lists are never retained.
        /// </summary>
        public void CaptureOnce(double gameplayTime, Action<OmsHidInputEvent> receive)
        {
            capture = receive;
            try
            {
                PollOnce();
                receive(new OmsHidInputEvent(OmsHidInputEventKind.FinishAxes));
            }
            finally
            {
                capture = null;
            }
        }

        public void DispatchCaptured(OmsHidInputEvent input)
        {
            switch (input.Kind)
            {
                case OmsHidInputEventKind.Press:
                    appliedReferences[input.Action]++;
                    pressAction(input.Action);
                    break;
                case OmsHidInputEventKind.Release:
                    if (appliedReferences[input.Action] == 0)
                        return;
                    appliedReferences[input.Action]--;
                    releaseAction(input.Action);
                    break;
                case OmsHidInputEventKind.Motion:
                    acceptedMotion?.Invoke(input.Motion);
                    break;
                case OmsHidInputEventKind.Disconnect:
                    hidAxisInputHandler.ReleaseDevice(input.DeviceIdentifier!);
                    DeviceDisconnected?.Invoke(input.DeviceIdentifier!);
                    break;
                case OmsHidInputEventKind.Axis:
                    hidAxisInputHandler.ApplyAxisChange(input.AxisChange);
                    break;
                case OmsHidInputEventKind.FinishAxes:
                    hidAxisInputHandler.FinishPolling();
                    break;
            }
        }

        private void route(OmsHidInputEvent input)
        {
            if (suppressDelivery)
                return;
            if (capture != null)
                capture(input);
            else
                DispatchCaptured(input);
        }

        public static IOmsHidButtonDeviceProvider CreateDefaultDeviceProvider()
            => CreateDefaultDeviceProvider(OperatingSystem.IsWindows(), () => new OmsWindowsDirectInputButtonDeviceProvider(), () => new HidSharpHidButtonDeviceProvider());

        internal static IOmsHidButtonDeviceProvider CreateDefaultDeviceProvider(Func<IOmsHidButtonDeviceProvider> providerFactory)
            => CreateHidSharpDeviceProvider(providerFactory);

        internal static IOmsHidButtonDeviceProvider CreateDefaultDeviceProvider(bool isWindows, Func<IOmsHidButtonDeviceProvider> windowsProviderFactory, Func<IOmsHidButtonDeviceProvider> hidSharpProviderFactory)
            => isWindows ? CreateWindowsDeviceProvider(windowsProviderFactory) : CreateHidSharpDeviceProvider(hidSharpProviderFactory);

        internal static IOmsHidButtonDeviceProvider CreateWindowsDeviceProvider(Func<IOmsHidButtonDeviceProvider> providerFactory)
        {
            try
            {
                return providerFactory();
            }
            catch (Exception e) when (OmsWindowsDirectInputRuntime.IsRecoverableFailure(e))
            {
                OmsWindowsDirectInputRuntime.LogRecoverableFailure(e);
                return new UnavailableHidButtonDeviceProvider();
            }
        }

        internal static IOmsHidButtonDeviceProvider CreateHidSharpDeviceProvider(Func<IOmsHidButtonDeviceProvider> providerFactory)
        {
            if (!OmsHidSharpRuntime.IsEnabled)
                return new UnavailableHidButtonDeviceProvider();

            try
            {
                return providerFactory();
            }
            catch (Exception e) when (OmsHidSharpRuntime.IsRecoverableFailure(e))
            {
                OmsHidSharpRuntime.LogRecoverableFailure(e);
                return new UnavailableHidButtonDeviceProvider();
            }
        }

        public OmsHidDeviceHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction,
                                   IOmsHidButtonDeviceProvider? deviceProvider = null, Action<OmsAxisMotion>? acceptedMotion = null)
            : this(bindings, pressAction, releaseAction, deviceProvider, Stopwatch.GetTimestamp, acceptedMotion)
        {
        }

        internal OmsHidDeviceHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction,
                                    IOmsHidButtonDeviceProvider? deviceProvider, Func<long> getTimestamp, Action<OmsAxisMotion>? acceptedMotion = null)
        {
            this.getTimestamp = getTimestamp;
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;
            this.acceptedMotion = acceptedMotion;
            var bindingArray = bindings.ToArray();
            foreach (var binding in bindingArray)
                appliedReferences.TryAdd(binding.Action, 0);

            hidButtonInputHandler = new OmsHidButtonInputHandler(bindingArray,
                action => route(new OmsHidInputEvent(OmsHidInputEventKind.Press, action)),
                action => route(new OmsHidInputEvent(OmsHidInputEventKind.Release, action)));
            hidAxisInputHandler = new OmsHidAxisInputHandler(bindingArray,
                action => route(new OmsHidInputEvent(OmsHidInputEventKind.Press, action)),
                action => route(new OmsHidInputEvent(OmsHidInputEventKind.Release, action)),
                motion => route(new OmsHidInputEvent(OmsHidInputEventKind.Motion, Motion: motion)));
            boundDeviceIdentifiers = bindingArray.SelectMany(binding => binding.HidButtonTriggers.Concat(binding.HidAxisTriggers))
                                                .Select(trigger => OmsHidDeviceIdentifier.Normalize(trigger.DeviceIdentifier!))
                                                .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                                                .ToHashSet(StringComparer.Ordinal);
            pollingDevices = new List<KeyValuePair<string, IOmsHidButtonDevice>>(boundDeviceIdentifiers.Count);
            retiredDevices = new List<IOmsHidButtonDevice>(boundDeviceIdentifiers.Count);

            if (boundDeviceIdentifiers.Count == 0)
                return;

            this.deviceProvider = deviceProvider ?? CreateDefaultDeviceProvider();
            this.deviceProvider.DevicesChanged += onDevicesChanged;

            // Saved gameplay bindings are constructed in BmsInputManager's background dependency loader.
            // Complete the initial discovery there, before the gameplay update loop starts.
            devicesDirty = false;
            try
            {
                using var initialDevices = discoverDevices(new HashSet<string>(StringComparer.Ordinal));
                commitDiscovery(initialDevices);
                nextDeviceRefresh = getTimestamp() + device_refresh_interval;
            }
            catch
            {
                this.deviceProvider.DevicesChanged -= onDevicesChanged;
                this.deviceProvider.Dispose();
                throw;
            }
        }

        public void PollOnce()
        {
            if (boundDeviceIdentifiers.Count == 0 || deviceProvider == null)
                return;

            lock (deviceGate)
            {
                if (isDisposed)
                    return;

                hidAxisInputHandler.BeginPolling();
                bool discardChanges = discardNextPoll;
                discardNextPoll = false;

                try
                {
                    if (discoveryTask?.IsCompleted == true)
                    {
                        var completed = discoveryTask;
                        discoveryTask = null;
                        using var discovered = completed.GetAwaiter().GetResult();
                        if (discovered != null)
                        {
                            commitDiscovery(discovered);
                            nextDeviceRefresh = getTimestamp() + device_refresh_interval;
                        }
                    }

                    foreach (var entry in devicesByIdentifier)
                        pollingDevices.Add(entry);

                    foreach (var entry in pollingDevices)
                    {
                        pollDevice(entry.Key, entry.Value, discardChanges);

                        if (!entry.Value.IsConnected)
                            disconnectDevice(entry.Key);
                    }

                    queueDeviceMaintenance();
                }
                finally
                {
                    pollingDevices.Clear();
                    if (capture == null)
                        hidAxisInputHandler.FinishPolling();
                }
            }
        }

        private bool shouldRefreshDevices()
            => (hasUnresolvedBindings || usesLegacyAliases) && getTimestamp() >= nextDeviceRefresh;

        public void Dispose()
        {
            Task<DeviceDiscoverySnapshot?>? pendingDiscovery;
            IOmsHidButtonDevice[] retired;
            lock (deviceGate)
            {
                if (isDisposed)
                    return;

                isDisposed = true;

                if (deviceProvider != null)
                    deviceProvider.DevicesChanged -= onDevicesChanged;

                foreach (string? identifier in devicesByIdentifier.Keys.ToArray())
                    disconnectDevice(identifier);
                ReleaseAppliedInput();

                pendingDiscovery = discoveryTask;
                discoveryTask = null;
                retired = retiredDevices.ToArray();
                retiredDevices.Clear();
            }

            try
            {
                using var uncommitted = pendingDiscovery?.GetAwaiter().GetResult();
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
                    deviceProvider?.Dispose();
                }
            }
        }

        private void pollDevice(string identifier, IOmsHidButtonDevice device, bool discardChanges)
        {
            // Polled backends acquire one snapshot here. Only buffered reports are drained afterwards.
            do
            {
                if (!device.TryReadChanges(out var changes))
                    break;

                if (!discardChanges)
                {
                    for (int index = 0; index < changes.Count; index++)
                        applyChange(changes[index]);
                }

                if (!device.IsConnected)
                {
                    disconnectDevice(identifier);
                    break;
                }
            } while (device.HasPendingChanges);
        }

        private void queueDeviceMaintenance()
        {
            if (discoveryTask != null)
                return;

            bool discover = devicesDirty || shouldRefreshDevices();
            if (!discover && retiredDevices.Count == 0)
                return;

            startDeviceMaintenance(discover);
        }

        private void startDeviceMaintenance(bool discover)
        {
            var connectedIdentifiers = discover ? devicesByIdentifier.Keys.ToHashSet(StringComparer.Ordinal) : null;
            var retired = retiredDevices.ToArray();
            retiredDevices.Clear();

            // Only the owner consumes this bit. A notification during discovery remains set for the next task.
            if (discover)
                devicesDirty = false;

            discoveryTask = Task.Run(() =>
            {
                foreach (var device in retired)
                    device.Dispose();

                return discover ? discoverDevices(connectedIdentifiers!) : null;
            });
        }

        private DeviceDiscoverySnapshot discoverDevices(HashSet<string> connectedIdentifiers)
        {
            var discovered = new DeviceDiscoverySnapshot();
            try
            {
                foreach (var device in deviceProvider!.GetDevices(boundDeviceIdentifiers))
                {
                    if (!discovered.Identifiers.Add(device.Identifier))
                    {
                        device.Dispose();
                        continue;
                    }

                    discovered.BindingIdentifiers.Add(device.Identifier, device.BindingAliases.Prepend(device.Identifier)
                        .Where(boundDeviceIdentifiers.Contains).Distinct(StringComparer.Ordinal).ToArray());
                    if (connectedIdentifiers.Contains(device.Identifier))
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

        private void commitDiscovery(DeviceDiscoverySnapshot discovered)
        {
            foreach (var entry in devicesByIdentifier)
                pollingDevices.Add(entry);

            foreach (var entry in pollingDevices)
            {
                if (!discovered.Identifiers.Contains(entry.Key))
                    disconnectDevice(entry.Key);
                else
                {
                    string[] nextIdentifiers = discovered.BindingIdentifiers[entry.Key];
                    foreach (string oldIdentifier in bindingIdentifiersByDevice[entry.Key])
                    {
                        if (nextIdentifiers.Contains(oldIdentifier, StringComparer.Ordinal))
                            continue;

                        route(new OmsHidInputEvent(OmsHidInputEventKind.Disconnect, DeviceIdentifier: oldIdentifier));
                        foreach (int button in activeButtonsByDevice[entry.Key])
                            hidButtonInputHandler.TriggerReleased(oldIdentifier, button);
                        if (capture == null)
                            hidAxisInputHandler.ReleaseDevice(oldIdentifier);
                    }
                    bindingIdentifiersByDevice[entry.Key] = nextIdentifiers;
                }
            }

            pollingDevices.Clear();
            foreach (var entry in discovered.NewDevices)
            {
                devicesByIdentifier.Add(entry.Key, entry.Value);
                activeButtonsByDevice.Add(entry.Key, new HashSet<int>(128));
                bindingIdentifiersByDevice.Add(entry.Key, discovered.BindingIdentifiers[entry.Key]);
            }
            discovered.NewDevices.Clear();

            hasUnresolvedBindings = boundDeviceIdentifiers.Any(identifier => !bindingIdentifiersByDevice.Values.Any(identifiers => identifiers.Contains(identifier)));
            usesLegacyAliases = bindingIdentifiersByDevice.Any(entry => entry.Value.Any(identifier => identifier != entry.Key));
        }

        private void disconnectDevice(string identifier)
        {
            if (!devicesByIdentifier.Remove(identifier, out var device))
                return;

            route(new OmsHidInputEvent(OmsHidInputEventKind.Disconnect, DeviceIdentifier: identifier));
            foreach (string bindingIdentifier in bindingIdentifiersByDevice[identifier])
            {
                if (bindingIdentifier != identifier)
                    route(new OmsHidInputEvent(OmsHidInputEventKind.Disconnect, DeviceIdentifier: bindingIdentifier));
            }
            releaseActiveButtons(identifier);
            if (capture == null)
            {
                foreach (string bindingIdentifier in bindingIdentifiersByDevice[identifier])
                    hidAxisInputHandler.ReleaseDevice(bindingIdentifier);
            }
            bindingIdentifiersByDevice.Remove(identifier);
            hasUnresolvedBindings = true;
            retiredDevices.Add(device);
        }

        private void releaseActiveButtons(string identifier)
        {
            if (!activeButtonsByDevice.Remove(identifier, out var activeButtons))
                return;

            foreach (int buttonIndex in activeButtons)
            {
                foreach (string bindingIdentifier in bindingIdentifiersByDevice[identifier])
                    hidButtonInputHandler.TriggerReleased(bindingIdentifier, buttonIndex);
            }
        }

        private void applyChange(OmsHidDeviceChange change)
        {
            switch (change.Kind)
            {
                case OmsHidDeviceChangeKind.Button:
                    applyButtonChange(change.ButtonChange);
                    break;

                case OmsHidDeviceChangeKind.Axis:
                    applyAxisChange(change.AxisChange);
                    break;
            }
        }

        private void applyButtonChange(OmsHidButtonChange change)
        {
            if (change.ButtonIndex < 0 || !activeButtonsByDevice.TryGetValue(change.DeviceIdentifier, out var activeButtons))
                return;

            if (change.Pressed)
            {
                if (!activeButtons.Add(change.ButtonIndex))
                    return;

                foreach (string bindingIdentifier in bindingIdentifiersByDevice[change.DeviceIdentifier])
                    hidButtonInputHandler.TriggerPressed(bindingIdentifier, change.ButtonIndex);
                return;
            }

            if (!activeButtons.Remove(change.ButtonIndex))
                return;

            foreach (string bindingIdentifier in bindingIdentifiersByDevice[change.DeviceIdentifier])
                hidButtonInputHandler.TriggerReleased(bindingIdentifier, change.ButtonIndex);
        }

        private void applyAxisChange(OmsHidAxisChange change)
        {
            if (change.AxisIndex < 0 || !bindingIdentifiersByDevice.TryGetValue(change.DeviceIdentifier, out string[]? bindingIdentifiers))
                return;

            // A newly accepted stroke owns a new attack even when several same-direction
            // reports arrived in one outer frame. Position buttons are unaffected by poll end.
            if (capture == null)
                hidAxisInputHandler.FinishPolling();
            else
                capture(new OmsHidInputEvent(OmsHidInputEventKind.FinishAxes));
            foreach (string identifier in bindingIdentifiers)
            {
                var resolved = change.Value.HasValue
                    ? new OmsHidAxisChange(identifier, change.AxisIndex, change.Delta, change.Value.Value, change.Minimum, change.Maximum, change.IsRelative)
                    : new OmsHidAxisChange(identifier, change.AxisIndex, change.Delta);
                if (capture != null)
                    capture(new OmsHidInputEvent(OmsHidInputEventKind.Axis, AxisChange: resolved));
                else
                    hidAxisInputHandler.ApplyAxisChange(resolved);
            }
        }

        private void onDevicesChanged(object? sender, EventArgs e) => devicesDirty = true;

        private sealed class DeviceDiscoverySnapshot : IDisposable
        {
            public readonly HashSet<string> Identifiers = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, IOmsHidButtonDevice> NewDevices = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
            public readonly Dictionary<string, string[]> BindingIdentifiers = new Dictionary<string, string[]>(StringComparer.Ordinal);

            public void Dispose()
            {
                foreach (var entry in NewDevices)
                    entry.Value.Dispose();
                NewDevices.Clear();
            }
        }

        private sealed class UnavailableHidButtonDeviceProvider : IOmsHidButtonDeviceProvider
        {
            public event EventHandler? DevicesChanged
            {
                add
                {
                }
                remove
                {
                }
            }

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
                => Array.Empty<IOmsHidButtonDevice>();

            public void Dispose()
            {
            }
        }

        private sealed class HidSharpHidButtonDeviceProvider : IOmsHidButtonDeviceProvider
        {
            private readonly DeviceList deviceList;

            public event EventHandler? DevicesChanged;

            public HidSharpHidButtonDeviceProvider()
            {
                deviceList = DeviceList.Local;
                deviceList.Changed += onDeviceListChanged;
            }

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
            {
                var targetIdentifiers = deviceIdentifiers.ToHashSet(StringComparer.Ordinal);

                if (targetIdentifiers.Count == 0)
                    yield break;

                HidDevice[] hidDevices;

                try
                {
                    hidDevices = deviceList.GetHidDevices().ToArray();
                }
                catch (Exception e) when (OmsHidSharpRuntime.IsRecoverableFailure(e))
                {
                    OmsHidSharpRuntime.LogRecoverableFailure(e);
                    yield break;
                }

                foreach (var device in hidDevices)
                {
                    string identifier = OmsHidDeviceIdentifier.FromHidDevice(device);

                    if (!targetIdentifiers.Contains(identifier))
                        continue;

                    if (HidSharpHidButtonDevice.TryCreate(device, identifier, out var hidDevice))
                        yield return hidDevice;
                }
            }

            public void Dispose()
            {
                deviceList.Changed -= onDeviceListChanged;
            }

            private void onDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
                => DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class HidSharpHidButtonDevice : IOmsHidButtonDevice
        {
            private readonly HidStream stream;
            private readonly HidDeviceInputReceiver inputReceiver;
            private readonly byte[] inputReportBuffer;
            private readonly Dictionary<Report, ReportButtonMap> buttonMapsByReport;
            private readonly Dictionary<Report, ReportAxisMap> axisMapsByReport;
            private readonly HashSet<int> pressedButtons = new HashSet<int>();
            private readonly Dictionary<int, int> axisValuesByIndex = new Dictionary<int, int>();
            private int isDisposed;

            public string Identifier { get; }

            public bool IsConnected { get; private set; } = true;

            public bool HasPendingChanges
            {
                get
                {
                    if (!IsConnected)
                        return false;

                    try
                    {
                        return inputReceiver.WaitHandle.WaitOne(0);
                    }
                    catch (ObjectDisposedException)
                    {
                        IsConnected = false;
                        return false;
                    }
                }
            }

            private HidSharpHidButtonDevice(string identifier, HidStream stream, HidDeviceInputReceiver inputReceiver, byte[] inputReportBuffer, Dictionary<Report, ReportButtonMap> buttonMapsByReport, Dictionary<Report, ReportAxisMap> axisMapsByReport)
            {
                Identifier = identifier;
                this.stream = stream;
                this.inputReceiver = inputReceiver;
                this.inputReportBuffer = inputReportBuffer;
                this.buttonMapsByReport = buttonMapsByReport;
                this.axisMapsByReport = axisMapsByReport;
            }

            public static bool TryCreate(HidDevice device, string identifier, out IOmsHidButtonDevice hidButtonDevice)
            {
                hidButtonDevice = null!;

                try
                {
                    var reportDescriptor = device.GetReportDescriptor();
                    var buttonMapsByReport = buildButtonMaps(reportDescriptor);
                    var axisMapsByReport = buildAxisMaps(reportDescriptor);

                    if ((buttonMapsByReport.Count == 0 && axisMapsByReport.Count == 0) || !device.TryOpen(out HidStream stream))
                        return false;

                    var inputReceiver = reportDescriptor.CreateHidDeviceInputReceiver();
                    inputReceiver.Start(stream);

                    int bufferLength = Math.Max(1, Math.Max(device.GetMaxInputReportLength(), reportDescriptor.MaxInputReportLength));
                    hidButtonDevice = new HidSharpHidButtonDevice(identifier, stream, inputReceiver, new byte[bufferLength], buttonMapsByReport, axisMapsByReport);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }

            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                changes = Array.Empty<OmsHidDeviceChange>();

                if (!HasPendingChanges)
                    return false;

                var queuedChanges = new List<OmsHidDeviceChange>();

                try
                {
                    while (inputReceiver.TryRead(inputReportBuffer, 0, out Report report))
                    {
                        buttonMapsByReport.TryGetValue(report, out var buttonMap);
                        axisMapsByReport.TryGetValue(report, out var axisMap);

                        if (buttonMap == null && axisMap == null)
                            continue;

                        report.Read(inputReportBuffer, 0, (reportData, bitOffset, dataItem, elementIndex) =>
                        {
                            if (buttonMap != null && buttonMap.TryGetButtonIndex(dataItem, elementIndex, out int buttonIndex))
                            {
                                bool pressed = dataItem.ReadLogical(reportData, bitOffset, elementIndex) != 0;
                                bool wasPressed = pressedButtons.Contains(buttonIndex);

                                if (pressed != wasPressed)
                                {
                                    if (pressed)
                                        pressedButtons.Add(buttonIndex);
                                    else
                                        pressedButtons.Remove(buttonIndex);

                                    queuedChanges.Add(OmsHidDeviceChange.Button(new OmsHidButtonChange(Identifier, buttonIndex, pressed)));
                                }
                            }

                            if (axisMap == null || !axisMap.TryGetAxisIndex(dataItem, elementIndex, out int axisIndex))
                                return;

                            int currentValue = dataItem.ReadLogical(reportData, bitOffset, elementIndex);
                            bool initial = !axisValuesByIndex.ContainsKey(axisIndex);
                            int delta = dataItem.IsRelative
                                ? currentValue
                                : currentValue - axisValuesByIndex.GetValueOrDefault(axisIndex, currentValue);

                            if (!dataItem.IsRelative)
                                axisValuesByIndex[axisIndex] = currentValue;

                            if (delta == 0 && (!initial || dataItem.IsRelative))
                                return;

                            queuedChanges.Add(OmsHidDeviceChange.Axis(new OmsHidAxisChange(Identifier, axisIndex, delta, currentValue,
                                dataItem.LogicalMinimum, dataItem.LogicalMaximum, dataItem.IsRelative), receivedTimestamp: Stopwatch.GetTimestamp()));
                        });
                    }
                }
                catch (IOException)
                {
                    IsConnected = false;
                }
                catch (ObjectDisposedException)
                {
                    IsConnected = false;
                }
                catch (InvalidOperationException)
                {
                    IsConnected = false;
                }

                changes = queuedChanges;
                return queuedChanges.Count > 0;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref isDisposed, 1) != 0)
                    return;

                IsConnected = false;
                stream.Dispose();
            }

            private static Dictionary<Report, ReportButtonMap> buildButtonMaps(ReportDescriptor reportDescriptor)
            {
                var buttonMaps = new Dictionary<Report, ReportButtonMap>();
                int nextFallbackButtonIndex = 0;

                foreach (var report in reportDescriptor.InputReports)
                {
                    var buttonIndicesByElement = new Dictionary<(DataItem Item, int ElementIndex), int>();

                    foreach (var dataItem in report.DataItems)
                    {
                        if (!isDigitalButton(dataItem))
                            continue;

                        for (int elementIndex = 0; elementIndex < dataItem.ElementCount; elementIndex++)
                        {
                            int buttonIndex = tryGetButtonIndex(dataItem, elementIndex, out int explicitButtonIndex)
                                ? explicitButtonIndex
                                : nextFallbackButtonIndex;

                            buttonIndicesByElement[(dataItem, elementIndex)] = buttonIndex;
                            nextFallbackButtonIndex = Math.Max(nextFallbackButtonIndex, buttonIndex + 1);
                        }
                    }

                    if (buttonIndicesByElement.Count > 0)
                        buttonMaps.Add(report, new ReportButtonMap(buttonIndicesByElement));
                }

                return buttonMaps;
            }

            private static Dictionary<Report, ReportAxisMap> buildAxisMaps(ReportDescriptor reportDescriptor)
            {
                var axisMaps = new Dictionary<Report, ReportAxisMap>();
                int nextFallbackAxisIndex = 9;

                foreach (var report in reportDescriptor.InputReports)
                {
                    var axisIndicesByElement = new Dictionary<(DataItem Item, int ElementIndex), int>();

                    foreach (var dataItem in report.DataItems)
                    {
                        if (!isAnalogAxis(dataItem))
                            continue;

                        for (int elementIndex = 0; elementIndex < dataItem.ElementCount; elementIndex++)
                        {
                            int axisIndex = tryGetAxisIndex(dataItem, elementIndex, out int explicitAxisIndex)
                                ? explicitAxisIndex
                                : nextFallbackAxisIndex;

                            axisIndicesByElement[(dataItem, elementIndex)] = axisIndex;
                            nextFallbackAxisIndex = Math.Max(nextFallbackAxisIndex, axisIndex + 1);
                        }
                    }

                    if (axisIndicesByElement.Count > 0)
                        axisMaps.Add(report, new ReportAxisMap(axisIndicesByElement));
                }

                return axisMaps;
            }

            private static bool isDigitalButton(DataItem dataItem)
                => dataItem.IsBoolean && dataItem.IsVariable && !dataItem.IsConstant;

            private static bool isAnalogAxis(DataItem dataItem)
                => !dataItem.IsBoolean && dataItem.IsVariable && !dataItem.IsConstant;

            private static bool tryGetButtonIndex(DataItem dataItem, int elementIndex, out int buttonIndex)
            {
                foreach (uint usage in dataItem.Usages.GetValuesFromIndex(elementIndex))
                {
                    uint usageId = usage > ushort.MaxValue ? usage & 0xffffu : usage;

                    if (usageId == 0 || usageId > int.MaxValue)
                        continue;

                    buttonIndex = (int)usageId - 1;
                    return true;
                }

                buttonIndex = -1;
                return false;
            }

            private static bool tryGetAxisIndex(DataItem dataItem, int elementIndex, out int axisIndex)
            {
                foreach (uint usage in dataItem.Usages.GetValuesFromIndex(elementIndex))
                {
                    uint usageId = usage > ushort.MaxValue ? usage & 0xffffu : usage;

                    if (usageId == 0 || usageId > int.MaxValue)
                        continue;

                    if (tryMapCommonAxisUsage((int)usageId, out axisIndex))
                        return true;
                }

                axisIndex = -1;
                return false;
            }

            private static bool tryMapCommonAxisUsage(int usageId, out int axisIndex)
            {
                switch (usageId)
                {
                    case 0x30:
                        axisIndex = 0;
                        return true;

                    case 0x31:
                        axisIndex = 1;
                        return true;

                    case 0x32:
                        axisIndex = 2;
                        return true;

                    case 0x33:
                        axisIndex = 3;
                        return true;

                    case 0x34:
                        axisIndex = 4;
                        return true;

                    case 0x35:
                        axisIndex = 5;
                        return true;

                    case 0x36:
                        axisIndex = 6;
                        return true;

                    case 0x37:
                        axisIndex = 7;
                        return true;

                    case 0x38:
                        axisIndex = 8;
                        return true;

                    default:
                        axisIndex = -1;
                        return false;
                }
            }

            private sealed class ReportButtonMap
            {
                private readonly Dictionary<(DataItem Item, int ElementIndex), int> buttonIndicesByElement;

                public ReportButtonMap(Dictionary<(DataItem Item, int ElementIndex), int> buttonIndicesByElement)
                {
                    this.buttonIndicesByElement = buttonIndicesByElement;
                }

                public bool TryGetButtonIndex(DataItem dataItem, int elementIndex, out int buttonIndex)
                    => buttonIndicesByElement.TryGetValue((dataItem, elementIndex), out buttonIndex);
            }

            private sealed class ReportAxisMap
            {
                private readonly Dictionary<(DataItem Item, int ElementIndex), int> axisIndicesByElement;

                public ReportAxisMap(Dictionary<(DataItem Item, int ElementIndex), int> axisIndicesByElement)
                {
                    this.axisIndicesByElement = axisIndicesByElement;
                }

                public bool TryGetAxisIndex(DataItem dataItem, int elementIndex, out int axisIndex)
                    => axisIndicesByElement.TryGetValue((dataItem, elementIndex), out axisIndex);
            }
        }
    }
}
