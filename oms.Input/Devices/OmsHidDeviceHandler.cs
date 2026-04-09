// Copyright (c) OMS contributors. Licensed under the MIT Licence.

extern alias OmsHidSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public OmsHidAxisChange(string deviceIdentifier, int axisIndex, int delta)
        {
            DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            AxisIndex = axisIndex;
            Delta = delta;
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

        private OmsHidDeviceChange(OmsHidDeviceChangeKind kind, OmsHidButtonChange buttonChange = default, OmsHidAxisChange axisChange = default)
        {
            Kind = kind;
            ButtonChange = buttonChange;
            AxisChange = axisChange;
        }

        public static OmsHidDeviceChange Button(OmsHidButtonChange change)
            => new OmsHidDeviceChange(OmsHidDeviceChangeKind.Button, buttonChange: change);

        public static OmsHidDeviceChange Axis(OmsHidAxisChange change)
            => new OmsHidDeviceChange(OmsHidDeviceChangeKind.Axis, axisChange: change);
    }

    public interface IOmsHidButtonDevice : IDisposable
    {
        string Identifier { get; }

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
            var identifier = $"hid:vid_{vendorId:x4}&pid_{productId:x4}";

            if (!string.IsNullOrWhiteSpace(serialNumber))
                identifier = $"{identifier}&serial_{serialNumber.Trim()}";

            return Normalize(identifier);
        }

        public static string FromHidDevice(HidDevice device)
            => ForVendorProduct(device.VendorID, device.ProductID, device.GetSerialNumber());

        internal static string FromDirectInputDevice(int vendorId, int productId, string? interfacePath, Guid instanceGuid)
        {
            if (vendorId > 0 || productId > 0)
                return ForVendorProduct(vendorId, productId, TryExtractSerialFromInterfacePath(interfacePath));

            if (tryParseVendorProductFromInterfacePath(interfacePath, out int parsedVendorId, out int parsedProductId))
                return ForVendorProduct(parsedVendorId, parsedProductId, TryExtractSerialFromInterfacePath(interfacePath));

            if (instanceGuid != Guid.Empty)
                return Normalize($"dinput:instance_{instanceGuid:N}");

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
        private readonly OmsHidButtonInputHandler hidButtonInputHandler;
        private readonly OmsHidAxisInputHandler hidAxisInputHandler;
        private readonly HashSet<string> boundDeviceIdentifiers;
        private readonly Dictionary<string, IOmsHidButtonDevice> devicesByIdentifier = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<int>> activeButtonsByDevice = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        private readonly IOmsHidButtonDeviceProvider? deviceProvider;

        private bool devicesDirty = true;
        private bool isDisposed;

        public int ConnectedDeviceCount => devicesByIdentifier.Count;

        public bool HasBoundDevices => boundDeviceIdentifiers.Count > 0;

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

        public OmsHidDeviceHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction, IOmsHidButtonDeviceProvider? deviceProvider = null)
        {
            var bindingArray = bindings.ToArray();

            hidButtonInputHandler = new OmsHidButtonInputHandler(bindingArray, pressAction, releaseAction);
            hidAxisInputHandler = new OmsHidAxisInputHandler(bindingArray, pressAction, releaseAction);
            boundDeviceIdentifiers = bindingArray.SelectMany(binding => binding.HidButtonTriggers.Concat(binding.HidAxisTriggers))
                                                .Select(trigger => OmsHidDeviceIdentifier.Normalize(trigger.DeviceIdentifier!))
                                                .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                                                .ToHashSet(StringComparer.Ordinal);

            if (boundDeviceIdentifiers.Count == 0)
                return;

            this.deviceProvider = deviceProvider ?? CreateDefaultDeviceProvider();
            this.deviceProvider.DevicesChanged += onDevicesChanged;
        }

        public void PollOnce()
        {
            if (isDisposed || boundDeviceIdentifiers.Count == 0 || deviceProvider == null)
                return;

            hidAxisInputHandler.BeginPolling();

            try
            {
                if (devicesDirty || shouldRefreshDevices())
                    refreshDevices();

                foreach (var entry in devicesByIdentifier.ToArray())
                {
                    pollDevice(entry.Key, entry.Value);

                    if (!entry.Value.IsConnected)
                        disconnectDevice(entry.Key, markDirty: true);
                }
            }
            finally
            {
                hidAxisInputHandler.FinishPolling();
            }
        }

        private bool shouldRefreshDevices()
            => devicesByIdentifier.Count < boundDeviceIdentifiers.Count;

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            if (deviceProvider != null)
                deviceProvider.DevicesChanged -= onDevicesChanged;

            foreach (var identifier in devicesByIdentifier.Keys.ToArray())
                disconnectDevice(identifier, markDirty: false);

            deviceProvider?.Dispose();
        }

        private void pollDevice(string identifier, IOmsHidButtonDevice device)
        {
            while (device.HasPendingChanges)
            {
                if (!device.TryReadChanges(out var changes))
                    break;

                foreach (var change in changes)
                    applyChange(change);

                if (!device.IsConnected)
                {
                    disconnectDevice(identifier, markDirty: true);
                    break;
                }
            }
        }

        private void refreshDevices()
        {
            if (deviceProvider == null)
                return;

            devicesDirty = false;

            var discoveredDevices = new Dictionary<string, IOmsHidButtonDevice>(StringComparer.Ordinal);

            foreach (var device in deviceProvider.GetDevices(boundDeviceIdentifiers))
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

            releaseActiveButtons(identifier);
            hidAxisInputHandler.ReleaseDevice(identifier);
            device.Dispose();

            if (markDirty)
                devicesDirty = true;
        }

        private void releaseActiveButtons(string identifier)
        {
            if (!activeButtonsByDevice.Remove(identifier, out var activeButtons))
                return;

            foreach (var buttonIndex in activeButtons.OrderBy(index => index).ToArray())
                hidButtonInputHandler.TriggerReleased(identifier, buttonIndex);
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
            if (!boundDeviceIdentifiers.Contains(change.DeviceIdentifier) || change.ButtonIndex < 0)
                return;

            if (!activeButtonsByDevice.TryGetValue(change.DeviceIdentifier, out var activeButtons))
            {
                activeButtons = new HashSet<int>();
                activeButtonsByDevice[change.DeviceIdentifier] = activeButtons;
            }

            if (change.Pressed)
            {
                if (!activeButtons.Add(change.ButtonIndex))
                    return;

                hidButtonInputHandler.TriggerPressed(change.DeviceIdentifier, change.ButtonIndex);
                return;
            }

            if (!activeButtons.Remove(change.ButtonIndex))
                return;

            hidButtonInputHandler.TriggerReleased(change.DeviceIdentifier, change.ButtonIndex);

            if (activeButtons.Count == 0)
                activeButtonsByDevice.Remove(change.DeviceIdentifier);
        }

        private void applyAxisChange(OmsHidAxisChange change)
        {
            if (!boundDeviceIdentifiers.Contains(change.DeviceIdentifier) || change.AxisIndex < 0)
                return;

            hidAxisInputHandler.ApplyAxisDelta(change.DeviceIdentifier, change.AxisIndex, change.Delta);
        }

        private void onDevicesChanged(object? sender, EventArgs e) => devicesDirty = true;

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

                if (!IsConnected)
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
                            int delta = dataItem.IsRelative
                                ? currentValue
                                : currentValue - axisValuesByIndex.GetValueOrDefault(axisIndex, currentValue);

                            if (!dataItem.IsRelative)
                                axisValuesByIndex[axisIndex] = currentValue;

                            if (delta == 0)
                                return;

                            queuedChanges.Add(OmsHidDeviceChange.Axis(new OmsHidAxisChange(Identifier, axisIndex, delta)));
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
                if (!IsConnected)
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
