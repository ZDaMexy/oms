extern alias OmsHidSharp;

using System;
using System.Collections.Generic;
using System.Linq;

using DeviceList = OmsHidSharp::HidSharp.DeviceList;
using HidDevice = OmsHidSharp::HidSharp.HidDevice;

namespace oms.Input.Devices
{
    public readonly struct OmsHidDeviceInfo
    {
        public string Identifier { get; }

        public string DisplayName { get; }

        public OmsHidDeviceInfo(string identifier, string displayName)
        {
            Identifier = OmsHidDeviceIdentifier.Normalize(identifier);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Identifier : displayName.Trim();
        }
    }

    public static class OmsHidDeviceDiscovery
    {
        public static IReadOnlyList<OmsHidDeviceInfo> GetConnectedDevices()
            => DeviceList.Local.GetHidDevices()
                          .Select(toDeviceInfo)
                          .Where(info => !string.IsNullOrWhiteSpace(info.Identifier))
                          .GroupBy(info => info.Identifier, StringComparer.Ordinal)
                          .Select(group => group.First())
                          .OrderBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase)
                          .ToArray();

        private static OmsHidDeviceInfo toDeviceInfo(HidDevice device)
        {
            string identifier = OmsHidDeviceIdentifier.FromHidDevice(device);
            string displayName = $@"{buildDeviceName(device)} ({identifier})";

            return new OmsHidDeviceInfo(identifier, displayName);
        }

        private static string buildDeviceName(HidDevice device)
        {
            var parts = new[] { device.GetManufacturer(), device.GetProductName() }
                        .Where(part => !string.IsNullOrWhiteSpace(part))
                        .ToArray();

            if (parts.Length > 0)
                return string.Join(" ", parts);

            return $@"HID {device.VendorID:x4}:{device.ProductID:x4}";
        }
    }
}
