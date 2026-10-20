// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using Vortice.DirectInput;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsDirectInputPollingTest
    {
        private const string native_identifier = "dinput:instance_00112233445566778899aabbccddeeff";

        [Test]
        public void TestFirstReadAcquiresBeforePollingAndReadingBufferedEvents()
        {
            requireWindows();
            using var native = new NativeDevice { CaptureCalls = true };
            using var device = createDevice(native);
            device.TryReadChanges(out _);
            device.TryReadChanges(out _);
            Assert.That(native.Calls, Is.EqualTo(new[] { "acquire", "poll", "state", "data", "poll", "data" }));
        }

        [Test]
        public void TestCompleteTapAfterStartupSnapshotIsDeliveredExactlyOnce()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var device = createDevice(native);
            native.AfterStateRead = () =>
            {
                native.EnqueueSnapshot(true, 0);
                native.EnqueueSnapshot(false, 0);
            };

            Assert.That(device.TryReadChanges(out var first), Is.True);
            var buttons = first.Where(change => change.Kind == OmsHidDeviceChangeKind.Button).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(buttons.Select(change => change.ButtonChange.Pressed), Is.EqualTo(new[] { true, false }));
                Assert.That(buttons.All(change => change.NativeTimestamp.HasValue && change.NativeSequence.HasValue), Is.True);
                Assert.That(native.Polls, Is.EqualTo(1));
            });
            Assert.That(device.TryReadChanges(out var second), Is.False);
            Assert.That(second, Is.Empty);
        }

        [Test]
        public void TestStartupButtonHistoryKeepsNativeOrderWithoutDuplicatingSnapshotPresses()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var device = createDevice(native);
            native.SetButtonState(1, true);
            native.SetButtonState(0, true);
            native.SetButtonState(1, false);
            native.SetButtonState(2, true, bufferedChange: false); // Held before acquisition, without an edge in its buffer.

            Assert.That(device.TryReadChanges(out var first), Is.True);
            var buttons = first.Where(change => change.Kind == OmsHidDeviceChangeKind.Button).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(buttons.Select(change => (change.ButtonChange.ButtonIndex, change.ButtonChange.Pressed)),
                    Is.EqualTo(new[] { (1, true), (0, true), (1, false), (2, true) }));
                Assert.That(buttons.Take(3).All(change => change.NativeSequence.HasValue), Is.True);
                Assert.That(buttons[3].NativeSequence, Is.Null, "A snapshot level must not be presented as a timestamped physical edge.");
            });
            Assert.That(device.TryReadChanges(out _), Is.False);

            native.SetButtonState(0, false);
            native.SetButtonState(2, false);
            device.TryReadChanges(out var releases);
            Assert.That(releases.Select(change => (change.ButtonChange.ButtonIndex, change.ButtonChange.Pressed)),
                Is.EqualTo(new[] { (0, false), (2, false) }));
        }

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
                Assert.That(native.StateReads - readsBefore, Is.Zero, "Current state is the connection baseline, not a replacement for event history.");
                Assert.That(allocated, Is.Zero);
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestBufferedCompleteTapAndAxisOrderSurviveOneGameplayPoll()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var device = createDevice(native);
            device.TryReadChanges(out _);
            int pollsBefore = native.Polls;

            native.EnqueueSnapshot(true, 10);
            native.EnqueueSnapshot(false, -10);
            native.EnqueueSnapshot(true, 20);
            Assert.That(device.TryReadChanges(out var changes), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1),
                    "Reading buffered history must not poll fresh snapshots for each queued event.");
                Assert.That(changes.Select(change => change.Kind), Is.EqualTo(new[]
                {
                    OmsHidDeviceChangeKind.Button, OmsHidDeviceChangeKind.Axis,
                    OmsHidDeviceChangeKind.Button, OmsHidDeviceChangeKind.Axis,
                    OmsHidDeviceChangeKind.Button, OmsHidDeviceChangeKind.Axis,
                }));
                Assert.That(changes.Where(change => change.Kind == OmsHidDeviceChangeKind.Button).Select(change => change.ButtonChange.Pressed),
                    Is.EqualTo(new[] { true, false, true }));
                Assert.That(changes.Where(change => change.Kind == OmsHidDeviceChangeKind.Axis).Select(change => change.AxisChange.Delta),
                    Is.EqualTo(new[] { 10, -20, 30 }));
                Assert.That(changes.Where(change => change.Kind == OmsHidDeviceChangeKind.Axis).Select(change => change.AxisChange.Value),
                    Is.EqualTo(new int?[] { 10, -10, 20 }));
                Assert.That(changes.Where(change => change.Kind == OmsHidDeviceChangeKind.Axis).All(change => !change.AxisChange.IsRelative), Is.True);
                Assert.That(changes.All(change => change.NativeTimestamp.HasValue && change.NativeSequence.HasValue && change.ReceivedTimestamp.HasValue), Is.True);
            });

            Assert.That(device.TryReadChanges(out _), Is.False, "A previously drained event must not be delivered again.");
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
            using var capture = new OmsHidDeviceCaptureSession(native_identifier, new SingleDeviceProvider(createDevice(native)));
            Assert.That(SpinWait.SpinUntil(() =>
            {
                capture.PollOnce();
                return capture.HasConnectedDevice;
            }, TimeSpan.FromSeconds(5)), Is.True);
            int pollsBefore = native.Polls;
            native.EnqueueSnapshot(true, 10);
            native.EnqueueSnapshot(false, -10);

            var first = capture.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1));
                Assert.That(first, Has.Count.EqualTo(4));
                Assert.That(first[0].ButtonChange.Pressed, Is.True);
                Assert.That(first[1].AxisChange.Delta, Is.EqualTo(10));
            });

            var second = capture.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(2));
                Assert.That(second, Is.Empty);
                Assert.That(first[2].ButtonChange.Pressed, Is.False);
                Assert.That(first[3].AxisChange.Delta, Is.EqualTo(-20));
                Assert.That(first[0].ButtonChange.Pressed, Is.True);
                Assert.That(first[1].AxisChange.Delta, Is.EqualTo(10));
            });
        }

        [Test]
        public void TestInputResetReleasesAppliedButtonsAndDropsPausedBufferedTaps()
        {
            requireWindows();
            using var native = new NativeDevice();
            var events = new List<bool>();
            using var handler = createHandler(native, _ => events.Add(true), _ => events.Add(false));
            handler.PollOnce();
            native.EnqueueSnapshot(true, 0);
            handler.PollOnce();
            handler.ResetInput();
            Assert.That(events, Is.EqualTo(new[] { true, false }));

            native.EnqueueSnapshot(false, 0);
            native.EnqueueSnapshot(true, 0);
            native.EnqueueSnapshot(false, 0);
            int pollsBefore = native.Polls;
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(new[] { true, false }), "Paused complete taps must not hit a note on resume.");
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1), "Reset must not reconnect the device.");
            });

            native.EnqueueSnapshot(true, 0);
            native.EnqueueSnapshot(false, 0);
            handler.PollOnce();
            Assert.That(events, Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void TestInputResetDoesNotReleaseAnotherDevicesActionReference()
        {
            requireWindows();
            using var native = new NativeDevice();
            var router = new OmsInputRouter();
            router.TriggerPressed(OmsAction.Key1P_1); // An independent keyboard/mouse owner.
            using var handler = createHandler(native, action => router.TriggerPressed(action), action => router.TriggerReleased(action));
            handler.PollOnce();
            native.EnqueueSnapshot(true, 0);
            handler.PollOnce();
            handler.ResetInput();
            Assert.That(router.IsPressed(OmsAction.Key1P_1), Is.True);
            router.TriggerReleased(OmsAction.Key1P_1);
            Assert.That(router.IsPressed(OmsAction.Key1P_1), Is.False);
        }

        [Test]
        public void TestCircularFirstFreshMovementAfterDiscardedResetBatchIsNotLost()
        {
            requireWindows();
            using var native = new NativeDevice();
            var motions = new List<OmsAxisMotion>();
            var pulses = new List<bool>();
            var settings = new OmsAxisSettings(OmsAxisMode.CircularPosition, minimum: 0, maximum: 255);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis(native_identifier, 0, OmsAxisDirection.Positive, axisSettings: settings),
                    OmsBindingTrigger.HidAxis(native_identifier, 0, OmsAxisDirection.Negative, axisSettings: settings)),
            }, _ => pulses.Add(true), _ => pulses.Add(false), new SingleDeviceProvider(createDevice(native)), motions.Add);

            native.EnqueueSnapshot(false, 254);
            handler.PollOnce();
            Assert.That(motions, Is.Empty, "A new connection calibrates its position without a scratch.");
            handler.ResetInput();
            native.EnqueueSnapshot(false, 1);
            handler.PollOnce();
            Assert.That(motions, Is.Empty, "A paused wrap is drained but cannot hit a note.");

            native.EnqueueSnapshot(false, 4);
            handler.PollOnce();
            native.EnqueueSnapshot(false, 255);
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(motions.Select(motion => motion.Delta), Is.EqualTo(new[] { 3.0 / 256, -5.0 / 256 }));
                Assert.That(pulses, Is.EqualTo(new[] { true, false, true, false }));
                Assert.That(native.Polls, Is.EqualTo(4));
            });
        }

        [Test]
        public void TestBufferedDrainHasAFixedPerPollBudgetAndRetainsRemainingHistory()
        {
            requireWindows();
            using var native = new NativeDevice();
            using var device = createDevice(native);
            device.TryReadChanges(out _);
            int pollsBefore = native.Polls;
            int readsBefore = native.BufferedReads;
            for (int index = 0; index < 5000; index++)
                native.EnqueueSnapshot(index % 2 == 0, 0);

            device.TryReadChanges(out var first);
            Assert.Multiple(() =>
            {
                Assert.That(first, Has.Count.EqualTo(4096));
                Assert.That(native.BufferedReads - readsBefore, Is.EqualTo(4));
                Assert.That(native.Polls - pollsBefore, Is.EqualTo(1));
            });
            device.TryReadChanges(out var second);
            Assert.That(second, Has.Count.EqualTo(904));
        }

        [Test]
        public void TestBufferedOverflowReleasesHeldButtonWithoutInventingHistory()
        {
            requireWindows();
            using var native = new NativeDevice();
            var events = new List<bool>();
            using var handler = createHandler(native, _ => events.Add(true), _ => events.Add(false));
            handler.PollOnce();
            native.EnqueueSnapshot(true, 0);
            handler.PollOnce();
            native.OverflowNextRead = true;
            native.EnqueueSnapshot(false, 0);
            native.EnqueueSnapshot(true, 0);
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(new[] { true, false }));
                Assert.That(handler.ConnectedDeviceCount, Is.Zero);
            });
        }

        [Test]
        public void TestNativeButtonTapsDoNotAllocateAfterWarmup()
        {
            requireWindows();
            using var native = new NativeDevice();
            int presses = 0;
            int releases = 0;
            var provider = new SingleDeviceProvider(createDevice(native));
            using var handler = createHandler(provider, _ => presses++, _ => releases++);
            long[] stageAllocations = new long[2];
            int[] firstAllocation = new int[2];
            Array.Fill(firstAllocation, -1);
            handler.PollOnce();
            for (int index = 0; index < 256; index++)
                tap(index - 256);
            presses = releases = 0;
            Array.Clear(stageAllocations);
            Array.Fill(firstAllocation, -1);
            int discoveriesBefore = provider.Discoveries;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 6000; index++)
                tap(index);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"direct-input-buttons: taps=6000 presses={presses} releases={releases} allocated={allocated} " +
                                  $"enqueue={stageAllocations[0]}@{firstAllocation[0]} poll={stageAllocations[1]}@{firstAllocation[1]} " +
                                  $"discoveries={provider.Discoveries - discoveriesBefore}");
            Assert.Multiple(() =>
            {
                Assert.That(presses, Is.EqualTo(6000));
                Assert.That(releases, Is.EqualTo(6000));
                Assert.That(allocated, Is.Zero);
                Assert.That(provider.Discoveries, Is.EqualTo(discoveriesBefore), "A stable canonical binding must not run alias discovery during input polling.");
            });

            void tap(int iteration)
            {
                long start = GC.GetAllocatedBytesForCurrentThread();
                native.EnqueueSnapshot(true, 0);
                native.EnqueueSnapshot(false, 0);
                measure(0, iteration, start);
                start = GC.GetAllocatedBytesForCurrentThread();
                handler.PollOnce();
                measure(1, iteration, start);
            }

            void measure(int stage, int iteration, long start)
            {
                long difference = GC.GetAllocatedBytesForCurrentThread() - start;
                stageAllocations[stage] += difference;
                if (difference > 0 && firstAllocation[stage] == -1)
                    firstAllocation[stage] = iteration;
            }
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
            => createHandler(new SingleDeviceProvider(device), press, release);

        private static OmsHidDeviceHandler createHandler(SingleDeviceProvider provider, Action<OmsAction> press, Action<OmsAction> release)
            => new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton(native_identifier, 0)),
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis(native_identifier, 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis(native_identifier, 0, OmsAxisDirection.Negative)),
            }, press, release, provider);

        private static IOmsHidButtonDevice createDevice(NativeDevice native)
            => (IOmsHidButtonDevice)Activator.CreateInstance(typeof(OmsWindowsDirectInputButtonDevice),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { native_identifier, native.Wrapper }, null)!;

        private static void requireWindows()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("The pinned DirectInput COM ABI is a Windows-only input backend.");
        }

        private sealed class SingleDeviceProvider : IOmsHidButtonDeviceProvider
        {
            private readonly IOmsHidButtonDevice[] devices;
            private int discoveries;
            public int Discoveries => Volatile.Read(ref discoveries);

            public event EventHandler? DevicesChanged
            {
                add { }
                remove { }
            }

            public SingleDeviceProvider(IOmsHidButtonDevice device) => devices = new[] { device };
            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
            {
                Interlocked.Increment(ref discoveries);
                return devices;
            }
            public void Dispose() { }
        }

        /// <summary>
        /// Supplies IUnknown.Release, IDirectInputDevice8.Poll/Acquire/GetDeviceState/GetDeviceData through the real pinned
        /// Vortice wrapper. No installed controller, window, driver discovery or player data is accessed.
        /// </summary>
        private sealed class NativeDevice : IDisposable
        {
            private const int failed_input = unchecked((int)0x8007001E);
            private readonly IntPtr table;
            private readonly IntPtr instance;
            private readonly byte[] state = new byte[Marshal.SizeOf<RawJoystickState>()];
            private readonly Queue<(int Offset, int Data, uint Timestamp, uint Sequence)> buffered = new Queue<(int, int, uint, uint)>();
            private readonly ResultCall poll;
            private readonly ResultCall acquire;
            private readonly StateCall getState;
            private readonly BufferedCall getBuffered;
            private readonly ReferenceCall release;

            public IDirectInputDevice8 Wrapper { get; }
            public int Polls { get; private set; }
            public int StateReads { get; private set; }
            public int Releases { get; private set; }
            public bool FailReads { get; set; }
            public bool OverflowNextRead { get; set; }
            public int BufferedReads { get; private set; }
            public bool CaptureCalls { get; set; }
            public List<string> Calls { get; } = new List<string>();
            public Action? AfterStateRead { get; set; }
            private uint sequence;

            public NativeDevice()
            {
                poll = _ =>
                {
                    if (CaptureCalls)
                        Calls.Add("poll");
                    Polls++;
                    return FailReads ? failed_input : 0;
                };
                acquire = _ =>
                {
                    if (CaptureCalls)
                        Calls.Add("acquire");
                    return FailReads ? failed_input : 0;
                };
                getState = (_, size, destination) =>
                {
                    StateReads++;
                    if (CaptureCalls)
                        Calls.Add("state");
                    Marshal.Copy(state, 0, destination, Math.Min(size, state.Length));
                    var afterStateRead = AfterStateRead;
                    AfterStateRead = null;
                    afterStateRead?.Invoke();
                    return 0;
                };
                getBuffered = (IntPtr _, int objectSize, IntPtr destination, ref int count, int flags) =>
                {
                    BufferedReads++;
                    if (CaptureCalls)
                        Calls.Add("data");
                    if (OverflowNextRead)
                    {
                        OverflowNextRead = false;
                        buffered.Clear();
                        count = 0;
                        return 1;
                    }

                    int written = 0;
                    while (written < count && buffered.TryDequeue(out var item))
                    {
                        IntPtr target = destination + written * objectSize;
                        Marshal.WriteInt32(target, 0, item.Offset);
                        Marshal.WriteInt32(target, 4, item.Data);
                        Marshal.WriteInt32(target, 8, unchecked((int)item.Timestamp));
                        Marshal.WriteInt32(target, 12, unchecked((int)item.Sequence));
                        Marshal.WriteIntPtr(target, 16, IntPtr.Zero);
                        written++;
                    }
                    count = written;
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
                Marshal.WriteIntPtr(table, 10 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(getBuffered));
                Marshal.WriteIntPtr(table, 25 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(poll));
                instance = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(instance, table);
                Wrapper = new IDirectInputDevice8(instance);
            }

            public void EnqueueSnapshot(bool button, int x)
            {
                int previousX = BitConverter.ToInt32(state, 0);
                SetButtonState(0, button);
                if (x != previousX)
                    buffered.Enqueue((0, x, 1000 + sequence, sequence++));
                BitConverter.TryWriteBytes(state.AsSpan(0, sizeof(int)), x);
            }

            public void SetButtonState(int index, bool pressed, bool bufferedChange = true)
            {
                int offset = 48 + index;
                if (bufferedChange && pressed != (state[offset] != 0))
                    buffered.Enqueue((offset, pressed ? 0x80 : 0, 1000 + sequence, sequence++));
                state[offset] = pressed ? (byte)0x80 : (byte)0;
            }

            public void Dispose()
            {
                Wrapper.Dispose();
                Marshal.FreeHGlobal(instance);
                Marshal.FreeHGlobal(table);
                GC.KeepAlive(poll);
                GC.KeepAlive(acquire);
                GC.KeepAlive(getState);
                GC.KeepAlive(getBuffered);
                GC.KeepAlive(release);
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int ResultCall(IntPtr instance);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int StateCall(IntPtr instance, int size, IntPtr destination);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int BufferedCall(IntPtr instance, int objectSize, IntPtr destination, ref int count, int flags);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate uint ReferenceCall(IntPtr instance);
        }
    }
}
