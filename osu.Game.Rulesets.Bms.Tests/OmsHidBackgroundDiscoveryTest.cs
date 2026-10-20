// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Extensions;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidBackgroundDiscoveryTest
    {
        [Test]
        public void TestBlockedCaptureDiscoveryDoesNotBlockPollingAndDrainsOnExit()
        {
            using var discoveryEntered = new ManualResetEventSlim();
            using var discoveryAllowed = new ManualResetEventSlim();
            var provider = new BlockingProvider(discoveryEntered, discoveryAllowed);
            using var capture = new OmsHidDeviceCaptureSession("pad-0", provider);
            Assert.That(SpinWait.SpinUntil(() =>
            {
                capture.PollOnce();
                return capture.HasConnectedDevice;
            }, TimeSpan.FromSeconds(5)), Is.True);
            provider.NotifyDevicesChanged();
            capture.PollOnce();
            Task<IReadOnlyList<OmsHidDeviceChange>>? poll = null;
            try
            {
                Assert.That(discoveryEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                provider.ConnectedDevice.Enqueue(OmsHidDeviceChange.Button(new OmsHidButtonChange("pad-0", 0, true)));
                poll = Task.Run(capture.PollOnce);
                Assert.That(poll.Wait(TimeSpan.FromSeconds(5)), Is.True, "Capture polling must not wait on the blocked driver discovery.");
                IReadOnlyList<OmsHidDeviceChange> observed = poll.GetResultSafely();
                Assert.Multiple(() =>
                {
                    Assert.That(observed, Has.Count.EqualTo(1));
                    Assert.That(observed[0].ButtonChange.Pressed, Is.True);
                    Assert.That(provider.Discoveries, Is.EqualTo(2));
                    Assert.That(discoveryAllowed.IsSet, Is.False);
                });
            }
            finally
            {
                discoveryAllowed.Set();
                poll?.GetAwaiter().GetResult();
            }

            capture.Dispose();
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
            foreach (var device in provider.CreatedDevices)
                Assert.That(device.DisposeCount, Is.EqualTo(1));
            capture.PollOnce();
            Assert.That(provider.Discoveries, Is.EqualTo(2));
        }

        [Test]
        public void TestBlockedRediscoveryDoesNotBlockGameplayOrConnectedInput()
        {
            using var discoveryEntered = new ManualResetEventSlim();
            using var discoveryAllowed = new ManualResetEventSlim();
            var provider = new BlockingProvider(discoveryEntered, discoveryAllowed);
            var events = new List<OmsAction>();
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("pad-0", 0)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("pad-1", 0)),
            }, events.Add, _ => { }, provider);

            handler.PollOnce();
            Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            provider.NotifyDevicesChanged();
            Task poll = Task.Run(handler.PollOnce);
            Task? connectedPoll = null;

            try
            {
                Assert.That(discoveryEntered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "The provider must actually enter the blocked discovery before checking the gameplay update.");
                Assert.That(poll.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "Gameplay PollOnce must return while device discovery remains blocked.");

                provider.ConnectedDevice.Enqueue(OmsHidDeviceChange.Button(new OmsHidButtonChange("pad-0", 0, true)));
                connectedPoll = Task.Run(handler.PollOnce);
                Assert.That(connectedPoll.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "A second gameplay update must continue polling the already connected controller.");
                Assert.Multiple(() =>
                {
                    Assert.That(events, Is.EqualTo(new[] { OmsAction.Key1P_1 }));
                    Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
                    Assert.That(provider.Discoveries, Is.EqualTo(2), "Only one discovery may be in flight.");
                    Assert.That(discoveryAllowed.IsSet, Is.False, "The driver gate must remain closed throughout both updates.");
                });
            }
            finally
            {
                discoveryAllowed.Set();
                poll.GetAwaiter().GetResult();
                connectedPoll?.GetAwaiter().GetResult();
            }
        }

        [Test]
        public void TestNotificationDuringBlockedDiscoveryIsNotLost()
        {
            using var discoveryEntered = new ManualResetEventSlim();
            using var discoveryAllowed = new ManualResetEventSlim();
            var provider = new BlockingProvider(discoveryEntered, discoveryAllowed) { RediscoveredDeviceCount = 1 };
            using var handler = createHandler(provider);
            handler.PollOnce();
            provider.NotifyDevicesChanged();
            Task poll = Task.Run(handler.PollOnce);

            try
            {
                Assert.That(discoveryEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(poll.Wait(TimeSpan.FromSeconds(5)), Is.True);
                provider.RediscoveredDeviceCount = 2;
                provider.NotifyDevicesChanged();
                discoveryAllowed.Set();

                Assert.That(SpinWait.SpinUntil(() =>
                {
                    handler.PollOnce();
                    return handler.ConnectedDeviceCount == 2;
                }, TimeSpan.FromSeconds(5)), Is.True,
                    "An inventory captured before the notification must not consume the later notification.");
                Assert.That(provider.Discoveries, Is.EqualTo(3));
            }
            finally
            {
                discoveryAllowed.Set();
                poll.GetAwaiter().GetResult();
            }

            handler.Dispose();
            foreach (var device in provider.CreatedDevices)
                Assert.That(device.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestDisposeJoinsDiscoveryAndReleasesUncommittedDevicesBeforeProvider()
        {
            using var discoveryEntered = new ManualResetEventSlim();
            using var discoveryAllowed = new ManualResetEventSlim();
            using var discoveryFinished = new ManualResetEventSlim();
            using var unsubscribed = new ManualResetEventSlim();
            var provider = new BlockingProvider(discoveryEntered, discoveryAllowed, discoveryFinished, unsubscribed);
            using var handler = createHandler(provider);
            handler.PollOnce();
            provider.NotifyDevicesChanged();
            Task poll = Task.Run(handler.PollOnce);
            Task? dispose = null;

            try
            {
                Assert.That(discoveryEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(poll.Wait(TimeSpan.FromSeconds(5)), Is.True);
                dispose = Task.Run(handler.Dispose);
                Assert.That(unsubscribed.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "Disposal must actually seal the handler before the driver is released.");
                discoveryAllowed.Set();
                Assert.That(dispose.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(discoveryFinished.Wait(TimeSpan.FromSeconds(5)), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(provider.DisposedDuringDiscovery, Is.False);
                    Assert.That(provider.DisposeCalls, Is.EqualTo(1));
                    Assert.That(handler.ConnectedDeviceCount, Is.Zero);
                    Assert.That(provider.CreatedDevices.Count, Is.EqualTo(3));
                });
                foreach (var device in provider.CreatedDevices)
                    Assert.That(device.DisposeCount, Is.EqualTo(1));

                provider.NotifyDevicesChanged();
                handler.PollOnce();
                Assert.That(provider.Discoveries, Is.EqualTo(2));
            }
            finally
            {
                discoveryAllowed.Set();
                poll.GetAwaiter().GetResult();
                dispose?.GetAwaiter().GetResult();
            }
        }

        private static OmsHidDeviceHandler createHandler(BlockingProvider provider)
            => new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("pad-0", 0)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("pad-1", 0)),
            }, _ => { }, _ => { }, provider);

        private sealed class BlockingProvider : IOmsHidButtonDeviceProvider
        {
            private readonly ManualResetEventSlim discoveryEntered;
            private readonly ManualResetEventSlim discoveryAllowed;
            private readonly ManualResetEventSlim? discoveryFinished;
            private readonly ManualResetEventSlim? unsubscribed;
            private event EventHandler? devicesChanged;
            private int discoveries;
            private int rediscoveredDeviceCount = 2;
            private volatile bool discoveryBlocked;

            public event EventHandler? DevicesChanged
            {
                add => devicesChanged += value;
                remove
                {
                    devicesChanged -= value;
                    unsubscribed?.Set();
                }
            }

            public int Discoveries => Volatile.Read(ref discoveries);
            public Device ConnectedDevice { get; } = new Device("pad-0");
            public ConcurrentBag<Device> CreatedDevices { get; } = new ConcurrentBag<Device>();
            public int DisposeCalls { get; private set; }
            public bool DisposedDuringDiscovery { get; private set; }
            public int RediscoveredDeviceCount
            {
                get => Volatile.Read(ref rediscoveredDeviceCount);
                set => Volatile.Write(ref rediscoveredDeviceCount, value);
            }

            public BlockingProvider(ManualResetEventSlim discoveryEntered, ManualResetEventSlim discoveryAllowed,
                                    ManualResetEventSlim? discoveryFinished = null, ManualResetEventSlim? unsubscribed = null)
            {
                this.discoveryEntered = discoveryEntered;
                this.discoveryAllowed = discoveryAllowed;
                this.discoveryFinished = discoveryFinished;
                this.unsubscribed = unsubscribed;
                CreatedDevices.Add(ConnectedDevice);
            }

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
            {
                if (Interlocked.Increment(ref discoveries) == 1)
                    return new IOmsHidButtonDevice[] { ConnectedDevice };

                int count = RediscoveredDeviceCount;
                discoveryBlocked = true;
                try
                {
                    discoveryEntered.Set();
                    if (!discoveryAllowed.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("The test did not release the blocked HID discovery.");

                    var devices = new IOmsHidButtonDevice[count];
                    for (int index = 0; index < count; index++)
                    {
                        var device = new Device($"pad-{index}");
                        CreatedDevices.Add(device);
                        devices[index] = device;
                    }

                    return devices;
                }
                finally
                {
                    discoveryBlocked = false;
                    discoveryFinished?.Set();
                }
            }

            public void NotifyDevicesChanged() => devicesChanged?.Invoke(this, EventArgs.Empty);

            public void Dispose()
            {
                DisposeCalls++;
                DisposedDuringDiscovery |= discoveryBlocked;
            }
        }

        private sealed class Device : IOmsHidButtonDevice
        {
            private readonly Queue<IReadOnlyList<OmsHidDeviceChange>> batches = new Queue<IReadOnlyList<OmsHidDeviceChange>>();
            private int disposeCount;

            public string Identifier { get; }
            public bool IsConnected { get; private set; } = true;
            public bool HasPendingChanges => batches.Count > 0;
            public int DisposeCount => Volatile.Read(ref disposeCount);

            public Device(string identifier) => Identifier = identifier;
            public void Enqueue(OmsHidDeviceChange change) => batches.Enqueue(new[] { change });

            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                if (batches.TryDequeue(out var batch))
                {
                    changes = batch;
                    return true;
                }

                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }

            public void Dispose()
            {
                Interlocked.Increment(ref disposeCount);
                IsConnected = false;
            }
        }
    }
}
