// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidDeviceHandlerTest
    {
        [Test]
        public void TestQueuedChangesRouteIntoOmsActions()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("iidx-pad");

            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("IIDX-PAD", 0, true)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("iidx-pad", 0))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();
            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("iidx-pad", 0, false)));
            handler.PollOnce();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "-Key1P_1" }));
        }

        [Test]
        public void TestDisconnectReleasesHeldButtons()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("iidx-pad");

            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("iidx-pad", 0, true)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidButton("iidx-pad", 0),
                    OmsBindingTrigger.HidButton("iidx-pad", 1))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();
            provider.SetDevices();
            handler.PollOnce();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestUnboundDevicesAreIgnored()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("other-pad");

            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("other-pad", 0, true)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("iidx-pad", 0))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();

            Assert.That(events, Is.Empty);
        }

        [Test]
        public void TestAxisChangesRouteIntoOmsActionsAndReleaseWhenIdle()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("turntable");

            device.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 0, 5)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();
            handler.PollOnce();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestAxisDirectionFlipWithinSinglePollEmitsDistinctPulses()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("turntable");

            device.EnqueueBatch(
                OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 0, 5)),
                OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 0, -5)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestDisconnectReleasesHeldAxisAction()
        {
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("turntable");

            device.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 0, 5)));
            provider.SetDevices(device);

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider);

            handler.PollOnce();
            provider.SetDevices();
            handler.PollOnce();

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
        }

        [Test]
        public void TestCaptureSessionDrainsQueuedDeviceChanges()
        {
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("iidx-pad");

            device.EnqueueBatch(
                OmsHidDeviceChange.Button(new OmsHidButtonChange("IIDX-PAD", 3, true)),
                OmsHidDeviceChange.Axis(new OmsHidAxisChange("IIDX-PAD", 1, -7)));
            provider.SetDevices(device);

            using var captureSession = new OmsHidDeviceCaptureSession("iidx-pad", provider);

            var changes = captureSession.PollOnce();

            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(changes[0].Kind, Is.EqualTo(OmsHidDeviceChangeKind.Button));
                Assert.That(changes[0].ButtonChange.DeviceIdentifier, Is.EqualTo("iidx-pad"));
                Assert.That(changes[0].ButtonChange.ButtonIndex, Is.EqualTo(3));
                Assert.That(changes[0].ButtonChange.Pressed, Is.True);

                Assert.That(changes[1].Kind, Is.EqualTo(OmsHidDeviceChangeKind.Axis));
                Assert.That(changes[1].AxisChange.DeviceIdentifier, Is.EqualTo("iidx-pad"));
                Assert.That(changes[1].AxisChange.AxisIndex, Is.EqualTo(1));
                Assert.That(changes[1].AxisChange.Delta, Is.EqualTo(-7));
            });
        }

        [Test]
        public void TestCaptureSessionIgnoresOtherDevices()
        {
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("other-pad");

            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("other-pad", 0, true)));
            provider.SetDevices(device);

            using var captureSession = new OmsHidDeviceCaptureSession("iidx-pad", provider);

            Assert.That(captureSession.PollOnce(), Is.Empty);
        }

        [Test]
        public void TestCaptureSessionReconnectsAfterDeviceListChanges()
        {
            var provider = new FakeHidButtonDeviceProvider();
            var firstDevice = new FakeHidButtonDevice("turntable");

            firstDevice.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("turntable", 1, true)));
            provider.SetDevices(firstDevice);

            using var captureSession = new OmsHidDeviceCaptureSession("turntable", provider);

            Assert.That(captureSession.PollOnce(), Has.Count.EqualTo(1));

            provider.SetDevices();
            Assert.That(captureSession.PollOnce(), Is.Empty);

            var reconnectedDevice = new FakeHidButtonDevice("turntable");
            reconnectedDevice.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 2, 5)));
            provider.SetDevices(reconnectedDevice);

            var reconnectedChanges = captureSession.PollOnce();

            Assert.That(reconnectedChanges, Has.Count.EqualTo(1));
            Assert.That(reconnectedChanges[0].Kind, Is.EqualTo(OmsHidDeviceChangeKind.Axis));
            Assert.That(reconnectedChanges[0].AxisChange.AxisIndex, Is.EqualTo(2));
            Assert.That(reconnectedChanges[0].AxisChange.Delta, Is.EqualTo(5));
        }

        private sealed class FakeHidButtonDeviceProvider : IOmsHidButtonDeviceProvider
        {
            private readonly List<IOmsHidButtonDevice> devices = new List<IOmsHidButtonDevice>();

            public event EventHandler? DevicesChanged;

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
            {
                var targetIdentifiers = deviceIdentifiers.ToHashSet(StringComparer.Ordinal);
                return devices.Where(device => targetIdentifiers.Contains(device.Identifier)).ToArray();
            }

            public void SetDevices(params IOmsHidButtonDevice[] devices)
            {
                this.devices.Clear();
                this.devices.AddRange(devices);
                DevicesChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeHidButtonDevice : IOmsHidButtonDevice
        {
            private readonly Queue<IReadOnlyList<OmsHidDeviceChange>> queuedChanges = new Queue<IReadOnlyList<OmsHidDeviceChange>>();

            public string Identifier { get; }

            public bool IsConnected { get; private set; } = true;

            public bool HasPendingChanges => IsConnected && queuedChanges.Count > 0;

            public FakeHidButtonDevice(string identifier)
            {
                Identifier = OmsHidDeviceIdentifier.Normalize(identifier);
            }

            public void EnqueueBatch(params OmsHidDeviceChange[] changes)
                => queuedChanges.Enqueue(changes);

            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                if (!HasPendingChanges)
                {
                    changes = Array.Empty<OmsHidDeviceChange>();
                    return false;
                }

                changes = queuedChanges.Dequeue();
                return true;
            }

            public void Dispose()
            {
                IsConnected = false;
            }
        }
    }
}
