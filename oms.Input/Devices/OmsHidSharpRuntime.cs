extern alias OmsHidSharp;

using System;
using System.Reflection;
using System.Threading;
using osu.Framework.Logging;

using DeviceList = OmsHidSharp::HidSharp.DeviceList;

namespace oms.Input.Devices
{
    internal static class OmsHidSharpRuntime
    {
        private const string enable_hidsharp_env_var = "OMS_ENABLE_HIDSHARP";

        private static int hidInitialisationFailureLogged;
        private static int hidDisabledLogged;

        public static bool IsEnabled { get; } = ShouldEnableHidSharp(Environment.GetEnvironmentVariable(enable_hidsharp_env_var), OperatingSystem.IsWindows());

        public static bool TryGetDeviceList(out DeviceList deviceList)
        {
            if (!IsEnabled)
            {
                deviceList = null!;
                logDisabledByDefault();
                return false;
            }

            try
            {
                deviceList = DeviceList.Local;
                return true;
            }
            catch (Exception e) when (IsRecoverableFailure(e))
            {
                deviceList = null!;
                logInitialisationFailure(e);
                return false;
            }
        }

        internal static bool ShouldEnableHidSharp(string? configurationValue, bool isWindows)
        {
            if (tryParseBoolean(configurationValue, out bool parsed))
                return parsed;

            return !isWindows;
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
                or BadImageFormatException;
        }

        public static void LogRecoverableFailure(Exception exception)
            => logInitialisationFailure(exception);

        private static void logInitialisationFailure(Exception exception)
        {
            if (Interlocked.Exchange(ref hidInitialisationFailureLogged, 1) != 0)
                return;

            Logger.Error(exception, "HidSharp failed to initialise. OMS will continue with HID support disabled.");
        }

        private static void logDisabledByDefault()
        {
            if (Interlocked.Exchange(ref hidDisabledLogged, 1) != 0)
                return;

            Logger.Log($"HID support backed by HidSharp is disabled by default on Windows because DeviceList initialisation can crash the process. Set {enable_hidsharp_env_var}=1 to force-enable it.", LoggingTarget.Runtime, LogLevel.Important);
        }

        private static bool tryParseBoolean(string? value, out bool parsed)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    parsed = true;
                    return true;

                case "0":
                case "false":
                case "no":
                case "off":
                    parsed = false;
                    return true;

                default:
                    parsed = false;
                    return false;
            }
        }
    }
}
