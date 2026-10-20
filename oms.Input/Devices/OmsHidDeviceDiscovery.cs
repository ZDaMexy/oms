extern alias OmsHidSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using HidDevice = OmsHidSharp::HidSharp.HidDevice;

namespace oms.Input.Devices
{
    public readonly struct OmsHidDeviceInfo
    {
        public string Identifier { get; }

        public string DisplayName { get; }

        public string LegacyIdentifier { get; }

        public OmsHidDeviceInfo(string identifier, string displayName, string? legacyIdentifier = null)
        {
            Identifier = OmsHidDeviceIdentifier.Normalize(identifier);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Identifier : displayName.Trim();
            LegacyIdentifier = OmsHidDeviceIdentifier.Normalize(legacyIdentifier ?? identifier);
        }
    }

    public static class OmsHidDeviceDiscovery
    {
        /// <summary>
        /// Resolves saved model-level bindings only when the connected inventory identifies one unit.
        /// Callers can present <paramref name="diagnostic"/> without opening a second input backend.
        /// </summary>
        public static bool TryResolveIdentifier(string deviceIdentifier, IReadOnlyList<OmsHidDeviceInfo> devices, out string identifier, out string? diagnostic)
        {
            string requested = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            foreach (var device in devices)
            {
                if (device.Identifier != requested)
                    continue;

                identifier = requested;
                diagnostic = null;
                return true;
            }

            string? candidate = null;
            foreach (var device in devices)
            {
                if (device.LegacyIdentifier != requested)
                    continue;

                if (candidate != null && candidate != device.Identifier)
                {
                    identifier = string.Empty;
                    diagnostic = $"已连接多个同型号设备，旧绑定 {requested} 无法确定对应哪一台，请重新捕获设备。";
                    return false;
                }

                candidate = device.Identifier;
            }

            identifier = candidate ?? string.Empty;
            diagnostic = candidate == null ? $"绑定设备 {requested} 未连接。" : null;
            return candidate != null;
        }

        public static IReadOnlyList<OmsHidDeviceInfo> GetConnectedDevices()
        {
            if (OperatingSystem.IsWindows())
                return OmsWindowsDirectInputDiscovery.GetConnectedDevices();

            if (!OmsHidSharpRuntime.TryGetDeviceList(out var deviceList))
                return Array.Empty<OmsHidDeviceInfo>();

            try
            {
                var hidDevices = deviceList.GetHidDevices().ToArray();

                return hidDevices.Select(toDeviceInfo)
                                 .Where(info => !string.IsNullOrWhiteSpace(info.Identifier))
                                 .GroupBy(info => info.Identifier, StringComparer.Ordinal)
                                 .Select(group => group.First())
                                 .OrderBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase)
                                 .ToArray();
            }
            catch (Exception e) when (OmsHidSharpRuntime.IsRecoverableFailure(e))
            {
                OmsHidSharpRuntime.LogRecoverableFailure(e);
                return Array.Empty<OmsHidDeviceInfo>();
            }
        }

        private static OmsHidDeviceInfo toDeviceInfo(HidDevice device)
        {
            string identifier = OmsHidDeviceIdentifier.FromHidDevice(device);
            string displayName = $@"{buildDeviceName(device)} ({identifier})";

            return new OmsHidDeviceInfo(identifier, displayName);
        }

        private static string buildDeviceName(HidDevice device)
        {
            string[] parts = new[] { device.GetManufacturer(), device.GetProductName() }
                        .Where(part => !string.IsNullOrWhiteSpace(part))
                        .ToArray();

            if (parts.Length > 0)
                return string.Join(" ", parts);

            return $@"HID {device.VendorID:x4}:{device.ProductID:x4}";
        }
    }
}
