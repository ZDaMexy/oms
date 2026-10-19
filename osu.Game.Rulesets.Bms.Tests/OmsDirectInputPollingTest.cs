// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using Vortice.DirectInput;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsDirectInputPollingTest
    {
        [Test]
        public void TestNativeDirectInputIdlePollingDoesNotAllocateAfterWarmup()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var handler = createHandler(native, _ => { }, _ => { });
            for (int i = 0; i < 256; i++)
                handler.PollOnce();

            int pollsBefore = native.Polls;
            int readsBefore = native.StateReads;
            const int polls = 6000;
            long start = Stopwatch.GetTimestamp();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < polls; i++)
                handler.PollOnce();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            TestContext.Progress.WriteLine($"direct-input-idle: polls={polls} nativePolls={native.Polls - pollsBefore} stateReads={native.StateReads - readsBefore} allocated={allocated} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(polls));
                Assert.That(native.StateReads - readsBefore, Is.EqualTo(polls));
                Assert.That(allocated, Is.Zero);
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestOneNativeSnapshotPerGameplayPollKeepsButtonAndAxisEdges()
        {
            requireWindows();
            using var native = new NativeDevice();
            var events = new List<string>();
            using var handler = createHandler(native, action => events.Add($"+{action}"), action => events.Add($"-{action}"));
            handler.PollOnce();
            int pollsBefore = native.Polls;

            native.EnqueueSnapshot(true, 10);
            native.EnqueueSnapshot(false, -10);
            native.EnqueueSnapshot(true, 20);
            handler.PollOnce();

            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1),
                    "HasPendingChanges must not acquire fresh snapshots while draining this gameplay update.");
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "+Key1P_Scratch", "-Key1P_Scratch" }));
            });

            handler.PollOnce();
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(3));
                Assert.That(events, Is.EqualTo(new[]
                {
                    "+Key1P_1", "+Key1P_Scratch", "-Key1P_Scratch",
                    "-Key1P_1", "+Key1P_Scratch", "-Key1P_Scratch",
                    "+Key1P_1", "+Key1P_Scratch", "-Key1P_Scratch",
                }));
            });
        }

        [Test]
        public void TestNativeAxisMovementDoesNotAllocateOrLosePulsesAfterWarmup()
        {
            requireWindows();
            using var native = new NativeDevice();
            int presses = 0;
            int releases = 0;
            using var handler = createHandler(native, _ => presses++, _ => releases++);
            handler.PollOnce();
            for (int i = 0; i < 256; i++)
                poll(i);
            presses = releases = 0;
            int pollsBefore = native.Polls;

            const int polls = 6000;
            long start = Stopwatch.GetTimestamp();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < polls; i++)
                poll(i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            TestContext.Progress.WriteLine($"direct-input-axis: polls={polls} nativePolls={native.Polls - pollsBefore} presses={presses} releases={releases} allocated={allocated} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(polls));
                Assert.That(presses, Is.EqualTo(polls));
                Assert.That(releases, Is.EqualTo(polls));
                Assert.That(allocated, Is.Zero);
            });

            void poll(int index)
            {
                native.EnqueueSnapshot(false, index % 2 == 0 ? 10 : -10);
                handler.PollOnce();
            }
        }

        [Test]
        public void TestCaptureSessionPollsNativeOnceAndOwnsItsReturnedChanges()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var capture = new OmsHidDeviceCaptureSession("native-pad", new SingleDeviceProvider(createDevice(native)));
            capture.PollOnce();
            int pollsBefore = native.Polls;
            native.EnqueueSnapshot(true, 10);
            native.EnqueueSnapshot(false, -10);

            var first = capture.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1));
                Assert.That(first, Has.Count.EqualTo(2));
                Assert.That(first[0].ButtonChange.Pressed, Is.True);
                Assert.That(first[1].AxisChange.Delta, Is.EqualTo(10));
            });

            var second = capture.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(2));
                Assert.That(second, Has.Count.EqualTo(2));
                Assert.That(second[0].ButtonChange.Pressed, Is.False);
                Assert.That(second[1].AxisChange.Delta, Is.EqualTo(-20));
                Assert.That(first[0].ButtonChange.Pressed, Is.True);
                Assert.That(first[1].AxisChange.Delta, Is.EqualTo(10));
            });
        }

        [Test]
        public void TestNativeDisconnectReleasesHeldButtonInTheSamePoll()
        {
            requireWindows();
            using var native = new NativeDevice();
            var events = new List<string>();
            using var handler = createHandler(native, action => events.Add($"+{action}"), action => events.Add($"-{action}"));
            native.EnqueueSnapshot(true, 0);
            handler.PollOnce();
            native.FailReads = true;
            handler.PollOnce();

            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "-Key1P_1" }));
                Assert.That(handler.ConnectedDeviceCount, Is.Zero);
            });
            handler.Dispose();
            Assert.That(native.Releases, Is.EqualTo(1));
        }

        [Test]
        public void TestDisconnectedNativeDeviceReleasesItsComOwnerExactlyOnce()
        {
            requireWindows();
            using var native = new NativeDevice();
            IOmsHidButtonDevice device = createDevice(native);
            using var handler = createHandler(device, _ => { }, _ => { });
            handler.PollOnce();
            native.FailReads = true;
            handler.PollOnce();
            device.Dispose();
            device.Dispose();
            handler.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(device.IsConnected, Is.False);
                Assert.That(handler.ConnectedDeviceCount, Is.Zero);
                Assert.That(native.Releases, Is.EqualTo(1),
                    "The native COM handle must be released even after polling has already marked it disconnected.");
            });
        }

        private static OmsHidDeviceHandler createHandler(NativeDevice native, Action<OmsAction> press, Action<OmsAction> release)
            => createHandler(createDevice(native), press, release);

        private static OmsHidDeviceHandler createHandler(IOmsHidButtonDevice device, Action<OmsAction> press, Action<OmsAction> release)
            => new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("native-pad", 0)),
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("native-pad", 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis("native-pad", 0, OmsAxisDirection.Negative)),
            }, press, release, new SingleDeviceProvider(device));

        private static IOmsHidButtonDevice createDevice(NativeDevice native)
            => (IOmsHidButtonDevice)Activator.CreateInstance(typeof(OmsWindowsDirectInputButtonDevice),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "native-pad", native.Wrapper }, null)!;

        private static void requireWindows()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("The pinned DirectInput COM ABI is a Windows-only input backend.");
        }

        private sealed class SingleDeviceProvider : IOmsHidButtonDeviceProvider
        {
            private readonly IOmsHidButtonDevice[] devices;

            public event EventHandler? DevicesChanged
            {
                add { }
                remove { }
            }

            public SingleDeviceProvider(IOmsHidButtonDevice device) => devices = new[] { device };
            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers) => devices;
            public void Dispose() { }
        }

        /// <summary>
        /// Supplies only IUnknown.Release, IDirectInputDevice8.Poll/Acquire/GetDeviceState through the real pinned
        /// Vortice wrapper. No installed controller, window, driver discovery or player data is accessed.
        /// </summary>
        private sealed class NativeDevice : IDisposable
        {
            private const int failed_input = unchecked((int)0x8007001E);
            private readonly IntPtr table;
            private readonly IntPtr instance;
            private readonly byte[] state = new byte[Marshal.SizeOf<RawJoystickState>()];
            private readonly Queue<(bool Button, int X)> snapshots = new Queue<(bool, int)>();
            private readonly ResultCall poll;
            private readonly ResultCall acquire;
            private readonly StateCall getState;
            private readonly ReferenceCall release;

            public IDirectInputDevice8 Wrapper { get; }
            public int Polls { get; private set; }
            public int StateReads { get; private set; }
            public int Releases { get; private set; }
            public bool FailReads { get; set; }

            public NativeDevice()
            {
                poll = _ => { Polls++; return FailReads ? failed_input : 0; };
                acquire = _ => FailReads ? failed_input : 0;
                getState = (_, size, destination) =>
                {
                    StateReads++;
                    if (snapshots.TryDequeue(out var snapshot))
                    {
                        BitConverter.TryWriteBytes(state.AsSpan(0, sizeof(int)), snapshot.X);
                        // DIJOYSTATE2: six position axes, two sliders and four POV values precede 128 button bytes.
                        state[48] = snapshot.Button ? (byte)0x80 : (byte)0;
                    }
                    Marshal.Copy(state, 0, destination, Math.Min(size, state.Length));
                    return 0;
                };
                release = _ => { Releases++; return 0; };

                table = Marshal.AllocHGlobal(IntPtr.Size * 32);
                for (int index = 0; index < 32; index++)
                    Marshal.WriteIntPtr(table, index * IntPtr.Size, IntPtr.Zero);
                // These slot numbers are fixed by the Windows IDirectInputDevice8W ABI.
                Marshal.WriteIntPtr(table, 2 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(release));
                Marshal.WriteIntPtr(table, 7 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(acquire));
                Marshal.WriteIntPtr(table, 9 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(getState));
                Marshal.WriteIntPtr(table, 25 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(poll));
                instance = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(instance, table);
                Wrapper = new IDirectInputDevice8(instance);
            }

            public void EnqueueSnapshot(bool button, int x) => snapshots.Enqueue((button, x));

            public void Dispose()
            {
                Wrapper.Dispose();
                Marshal.FreeHGlobal(instance);
                Marshal.FreeHGlobal(table);
                GC.KeepAlive(poll);
                GC.KeepAlive(acquire);
                GC.KeepAlive(getState);
                GC.KeepAlive(release);
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int ResultCall(IntPtr instance);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int StateCall(IntPtr instance, int size, IntPtr destination);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate uint ReferenceCall(IntPtr instance);
        }
    }
}
