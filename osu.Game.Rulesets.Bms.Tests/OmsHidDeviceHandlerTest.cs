// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
            Assert.That(SpinWait.SpinUntil(() =>
            {
                handler.PollOnce();
                return handler.ConnectedDeviceCount == 0;
            }, TimeSpan.FromSeconds(5)), Is.True);

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

            var changes = waitForCapture(captureSession, changes => changes.Count == 2);

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
            waitForCapture(captureSession, _ => !captureSession.IsDiscovering);
            Assert.That(captureSession.HasConnectedDevice, Is.False);
        }

        [Test]
        public void TestCaptureSessionReconnectsAfterDeviceListChanges()
        {
            var provider = new FakeHidButtonDeviceProvider();
            var firstDevice = new FakeHidButtonDevice("turntable");

            firstDevice.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("turntable", 1, true)));
            provider.SetDevices(firstDevice);

            using var captureSession = new OmsHidDeviceCaptureSession("turntable", provider);

            Assert.That(waitForCapture(captureSession, changes => changes.Count == 1), Has.Count.EqualTo(1));

            provider.SetDevices();
            waitForCapture(captureSession, _ => !captureSession.HasConnectedDevice && !captureSession.IsDiscovering);

            var reconnectedDevice = new FakeHidButtonDevice("turntable");
            reconnectedDevice.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("turntable", 2, 5)));
            provider.SetDevices(reconnectedDevice);

            var reconnectedChanges = waitForCapture(captureSession, changes => changes.Count == 1);

            Assert.That(reconnectedChanges, Has.Count.EqualTo(1));
            Assert.That(reconnectedChanges[0].Kind, Is.EqualTo(OmsHidDeviceChangeKind.Axis));
            Assert.That(reconnectedChanges[0].AxisChange.AxisIndex, Is.EqualTo(2));
            Assert.That(reconnectedChanges[0].AxisChange.Delta, Is.EqualTo(5));
        }

        private static IReadOnlyList<OmsHidDeviceChange> waitForCapture(OmsHidDeviceCaptureSession session, Func<IReadOnlyList<OmsHidDeviceChange>, bool> condition)
        {
            IReadOnlyList<OmsHidDeviceChange> changes = Array.Empty<OmsHidDeviceChange>();
            Assert.That(SpinWait.SpinUntil(() =>
            {
                changes = session.PollOnce();
                return condition(changes);
            }, TimeSpan.FromSeconds(5)), Is.True, "Capture discovery must finish without blocking the calling UI update.");
            return changes;
        }

        [Test]
        public void TestHandlerRefreshesMissingBoundDeviceWithoutProviderEvent()
        {
            long timestamp = 0;
            var events = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();

            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("iidx-pad", 0))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"), provider, () => timestamp);

            Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(0));

            var device = new FakeHidButtonDevice("iidx-pad");
            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("iidx-pad", 0, true)));
            provider.SetDevicesSilently(device);

            timestamp = Stopwatch.Frequency / 2;
            handler.PollOnce();
            Assert.That(SpinWait.SpinUntil(() =>
            {
                handler.PollOnce();
                return events.Count > 0;
            }, TimeSpan.FromSeconds(5)), Is.True);

            Assert.That(events, Is.EqualTo(new[] { "+Key1P_1" }));
        }

        [Test]
        public void TestDefaultProviderFallsBackWhenHidSharpInitialisationFails()
        {
            using var provider = OmsHidDeviceHandler.CreateDefaultDeviceProvider(() => throw new InvalidOperationException("HidSharp RegisterClass failed."));

            Assert.That(provider.GetDevices(new[] { "iidx-pad" }), Is.Empty);
        }

        [Test]
        public void TestHidSharpDisabledByDefaultOnWindows()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OmsHidSharpRuntime.ShouldEnableHidSharp(null, isWindows: true), Is.False);
                Assert.That(OmsHidSharpRuntime.ShouldEnableHidSharp("1", isWindows: true), Is.True);
                Assert.That(OmsHidSharpRuntime.ShouldEnableHidSharp("true", isWindows: true), Is.True);
                Assert.That(OmsHidSharpRuntime.ShouldEnableHidSharp("0", isWindows: true), Is.False);
                Assert.That(OmsHidSharpRuntime.ShouldEnableHidSharp("false", isWindows: true), Is.False);
            });
        }

        [Test]
        public void TestDefaultProviderPrefersWindowsBackend()
        {
            using var provider = OmsHidDeviceHandler.CreateDefaultDeviceProvider(
                isWindows: true,
                windowsProviderFactory: () => new FakeHidButtonDeviceProvider(),
                hidSharpProviderFactory: () => throw new AssertionException("Windows builds should not select the HidSharp backend by default."));

            Assert.That(provider, Is.TypeOf<FakeHidButtonDeviceProvider>());
        }

        [Test]
        public void TestDirectInputIdentifierParsing()
        {
            Guid instanceGuid = Guid.Parse("9f2a5e01-4fd1-4e7b-b531-c0db3273f8b1");

            Assert.Multiple(() =>
            {
                Assert.That(
                    OmsHidDeviceIdentifier.FromDirectInputDevice(0x1ccf, 0x8048, @"\\?\hid#vid_1ccf&pid_8048#DJDAO123#{4d1e55b2-f16f-11cf-88cb-001111000030}", instanceGuid),
                    Is.EqualTo($"dinput:instance_{instanceGuid:N}"));

                Assert.That(
                    OmsHidDeviceIdentifier.FromDirectInputDevice(0x1ccf, 0x8048, @"\\?\hid#vid_1ccf&pid_8048#7&35ab3d4f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", instanceGuid),
                    Is.EqualTo($"dinput:instance_{instanceGuid:N}"));

                Assert.That(OmsHidDeviceIdentifier.LegacyFromDirectInputDevice(0x1ccf, 0x8048,
                    @"\\?\hid#vid_1ccf&pid_8048#DJDAO123#{4d1e55b2-f16f-11cf-88cb-001111000030}"),
                    Is.EqualTo("hid:vid_1ccf&pid_8048&serial_djdao123"));

                Assert.That(
                    OmsHidDeviceIdentifier.FromDirectInputDevice(0, 0, null, instanceGuid),
                    Is.EqualTo($"dinput:instance_{instanceGuid:N}".ToLowerInvariant()));
            });
        }

        [Test]
        public void TestLegacyDeviceBindingRequiresOnePhysicalCandidate()
        {
            const string legacy = "hid:vid_1ccf&pid_8048";
            var first = new OmsHidDeviceInfo("dinput:instance_first", "Left controller", legacy);
            var second = new OmsHidDeviceInfo("dinput:instance_second", "Right controller", legacy);
            Assert.Multiple(() =>
            {
                Assert.That(OmsHidDeviceDiscovery.TryResolveIdentifier(legacy, new[] { first }, out string resolved, out string? diagnostic), Is.True);
                Assert.That(resolved, Is.EqualTo(first.Identifier));
                Assert.That(diagnostic, Is.Null);
                Assert.That(OmsHidDeviceDiscovery.TryResolveIdentifier(legacy, new[] { first, second }, out _, out diagnostic), Is.False);
                Assert.That(diagnostic, Does.Contain("多个同型号设备"));
                Assert.That(OmsHidDeviceDiscovery.TryResolveIdentifier(second.Identifier, new[] { first, second }, out resolved, out diagnostic), Is.True);
                Assert.That(resolved, Is.EqualTo(second.Identifier));
                Assert.That(diagnostic, Is.Null);
            });
        }

        [Test]
        public void TestCanonicalAndLegacyBindingsShareOneDeviceAndOneActionEdge()
        {
            var events = new List<bool>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("unit-left") { BindingAliases = new[] { "legacy-pad" } };
            provider.SetDevices(device);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("legacy-pad", 0), OmsBindingTrigger.HidButton("unit-left", 0)),
            }, _ => events.Add(true), _ => events.Add(false), provider);
            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("unit-left", 0, true)));
            handler.PollOnce();
            device.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("unit-left", 0, false)));
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(new[] { true, false }));
            });
        }

        [Test]
        public void TestAliasBecomingAmbiguousReleasesOnlyItsReferencesAndPublishesDisconnect()
        {
            var actions = new List<string>();
            var disconnected = new List<string>();
            var provider = new FakeHidButtonDeviceProvider();
            var connected = new FakeHidButtonDevice("unit-left") { BindingAliases = new[] { "legacy-pad" } };
            provider.SetDevices(connected);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("legacy-pad", 0)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("unit-left", 0)),
            }, action => actions.Add($"+{action}"), action => actions.Add($"-{action}"), provider, () => 0);
            handler.DeviceDisconnected += disconnected.Add;
            connected.EnqueueBatch(OmsHidDeviceChange.Button(new OmsHidButtonChange("unit-left", 0, true)));
            handler.PollOnce();
            provider.SetDevices(new FakeHidButtonDevice("unit-left"));
            Assert.That(SpinWait.SpinUntil(() =>
            {
                handler.PollOnce();
                return disconnected.Count > 0;
            }, TimeSpan.FromSeconds(5)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(disconnected, Is.EqualTo(new[] { "legacy-pad" }));
                Assert.That(actions, Does.Contain("-Key1P_1"));
                Assert.That(actions, Does.Not.Contain("-Key1P_2"));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCapturedAxisReportsKeepDistinctStrokesButDoNotDuplicateAliases(bool aliases)
        {
            var edges = new List<bool>();
            var captured = new List<OmsHidInputEvent>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("unit-left") { BindingAliases = new[] { "legacy-pad" } };
            provider.SetDevices(device);
            var triggers = aliases
                ? new[] { OmsBindingTrigger.HidAxis("unit-left", 0, OmsAxisDirection.Positive), OmsBindingTrigger.HidAxis("legacy-pad", 0, OmsAxisDirection.Positive) }
                : new[] { OmsBindingTrigger.HidAxis("unit-left", 0, OmsAxisDirection.Positive) };
            using var handler = new OmsHidDeviceHandler(new[] { new OmsBinding(OmsAction.Key1P_Scratch, triggers) },
                _ => edges.Add(true), _ => edges.Add(false), provider);
            device.EnqueueBatch(
                OmsHidDeviceChange.Axis(new OmsHidAxisChange("unit-left", 0, 4)),
                OmsHidDeviceChange.Axis(new OmsHidAxisChange("unit-left", 0, 5)));
            handler.CaptureOnce(100, captured.Add);
            Assert.That(edges, Is.Empty, "hardware observation cannot dispatch gameplay during an earlier catch-up stop");
            foreach (OmsHidInputEvent input in captured)
                handler.DispatchCaptured(input);
            Assert.That(edges, Is.EqualTo(new[] { true, false, true, false }), "each physical report owns exactly one attack");
        }

        [Test]
        public void TestCapturedFutureStickPositionsCannotChangeHistoricalMotion()
        {
            var movement = new List<double>();
            var captured = new List<OmsHidInputEvent>();
            var provider = new FakeHidButtonDeviceProvider();
            var device = new FakeHidButtonDevice("stick");
            provider.SetDevices(device);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive,
                    axisSettings: new OmsAxisSettings(OmsAxisMode.StickToMotion))),
            }, _ => { }, _ => { }, provider, motion => movement.Add(motion.Delta));
            handler.UpdateAxes(0);
            device.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("stick", 0, 100, 100, -100, 100, false)));
            handler.CaptureOnce(100, captured.Add);
            handler.UpdateAxes(50);
            Assert.That(movement, Is.Empty, "a position observed at 100ms is not valid at 50ms");
            handler.UpdateAxes(100);
            foreach (OmsHidInputEvent input in captured)
                handler.DispatchCaptured(input);
            handler.UpdateAxes(150);
            captured.Clear();
            device.EnqueueBatch(OmsHidDeviceChange.Axis(new OmsHidAxisChange("stick", 0, -100, 0, -100, 100, false)));
            handler.CaptureOnce(200, captured.Add);
            handler.UpdateAxes(175);
            handler.UpdateAxes(200);
            Assert.That(movement.Sum(), Is.EqualTo(0.1).Within(0.000001), "the previous delivered deflection continues until the observed return");
            foreach (OmsHidInputEvent input in captured)
                handler.DispatchCaptured(input);
            int before = movement.Count;
            handler.UpdateAxes(250);
            Assert.That(movement.Count, Is.EqualTo(before), "returning to centre is not reverse motion");
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

            public void SetDevicesSilently(params IOmsHidButtonDevice[] devices)
            {
                this.devices.Clear();
                this.devices.AddRange(devices);
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeHidButtonDevice : IOmsHidButtonDevice
        {
            private readonly Queue<IReadOnlyList<OmsHidDeviceChange>> queuedChanges = new Queue<IReadOnlyList<OmsHidDeviceChange>>();

            public string Identifier { get; }

            public IReadOnlyList<string> BindingAliases { get; set; } = Array.Empty<string>();

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
