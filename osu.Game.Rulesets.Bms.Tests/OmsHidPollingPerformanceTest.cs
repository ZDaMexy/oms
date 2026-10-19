// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidPollingPerformanceTest
    {
        [TestCase(0)]
        [TestCase(1)]
        public void TestMissingDeviceDiscoveryDoesNotRunAtGameplayFrequency(int connectedCount)
        {
            var provider = new CountingProvider(connectedCount);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("pad-0", 0)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("pad-1", 0)),
            }, _ => { }, _ => { }, provider);

            const int polls = 6000;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < polls; i++)
                handler.PollOnce();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            // This allows at most four periodic discoveries per second, plus initial/boundary slack. It does not
            // depend on a particular CI speed and still rejects discovery at every gameplay input update.
            int maximumDiscoveries = 2 + (int)Math.Ceiling(elapsed / 250);
            TestContext.Progress.WriteLine($"hid-missing: connected={connectedCount} polls={polls} discoveries={provider.Discoveries} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(provider.Discoveries, Is.LessThanOrEqualTo(maximumDiscoveries));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(connectedCount));
                Assert.That(provider.PollObservations, Is.GreaterThanOrEqualTo(polls * connectedCount),
                    "Discovery backoff must not throttle input from an already-connected controller.");
            });
        }

        [Test]
        public void TestConnectedIdlePollingDoesNotAllocateAfterWarmup()
        {
            var provider = new CountingProvider(1);
            using var handler = new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("pad-0", 0)),
            }, _ => { }, _ => { }, provider);

            for (int i = 0; i < 256; i++)
                handler.PollOnce();
            int observationsBefore = provider.PollObservations;

            const int polls = 6000;
            long start = Stopwatch.GetTimestamp();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < polls; i++)
                handler.PollOnce();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            TestContext.Progress.WriteLine($"hid-idle: polls={polls} allocated={allocated} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(allocated, Is.Zero);
                Assert.That(provider.Discoveries, Is.EqualTo(1));
                Assert.That(provider.PollObservations - observationsBefore, Is.GreaterThanOrEqualTo(polls));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestMissingDeviceIsRediscoveredAfterIntervalWithoutProviderEvent()
        {
            long timestamp = 0;
            var provider = new CountingProvider(0);
            using var handler = createTimedHandler(provider, () => timestamp);
            handler.PollOnce();
            provider.ConnectedCount = 1;

            timestamp = Stopwatch.Frequency / 2 - 1;
            handler.PollOnce();
            Assert.Multiple(() =>
            {
                Assert.That(provider.Discoveries, Is.EqualTo(1));
                Assert.That(handler.ConnectedDeviceCount, Is.Zero);
            });

            timestamp++;
            handler.PollOnce();
            waitFor(handler, () => handler.ConnectedDeviceCount == 1);
            Assert.Multiple(() =>
            {
                Assert.That(provider.Discoveries, Is.EqualTo(2));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
                Assert.That(provider.PollObservations, Is.GreaterThanOrEqualTo(1));
            });
        }

        [Test]
        public void TestProviderNotificationRefreshesBeforePeriodicDiscovery()
        {
            var provider = new CountingProvider(0);
            using var handler = createTimedHandler(provider, () => 0);
            handler.PollOnce();
            provider.ConnectedCount = 1;
            provider.NotifyDevicesChanged();
            handler.PollOnce();
            waitFor(handler, () => handler.ConnectedDeviceCount == 1);

            Assert.Multiple(() =>
            {
                Assert.That(provider.Discoveries, Is.EqualTo(2));
                Assert.That(handler.ConnectedDeviceCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestRepeatedPollFailureDoesNotBypassDiscoveryInterval()
        {
            long timestamp = 0;
            var provider = new CountingProvider(1) { DisconnectOnPoll = true };
            using var handler = createTimedHandler(provider, () => timestamp);
            for (int i = 0; i < 100; i++)
                handler.PollOnce();

            Assert.Multiple(() =>
            {
                Assert.That(provider.Discoveries, Is.EqualTo(1));
                Assert.That(handler.ConnectedDeviceCount, Is.Zero);
            });

            timestamp = Stopwatch.Frequency / 2;
            handler.PollOnce();
            waitFor(handler, () => provider.Discoveries == 2);
            Assert.That(provider.Discoveries, Is.EqualTo(2));
        }

        private static void waitFor(OmsHidDeviceHandler handler, Func<bool> condition)
            => Assert.That(SpinWait.SpinUntil(() =>
            {
                handler.PollOnce();
                return condition();
            }, TimeSpan.FromSeconds(5)), Is.True, "The completed device discovery must reach the gameplay owner.");

        private static OmsHidDeviceHandler createTimedHandler(CountingProvider provider, Func<long> getTimestamp)
            => new OmsHidDeviceHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("pad-0", 0)),
            }, _ => { }, _ => { }, provider, getTimestamp);

        private sealed class CountingProvider : IOmsHidButtonDeviceProvider
        {
            private int discoveries;
            public int ConnectedCount { get; set; }
            public bool DisconnectOnPoll { get; set; }
            public int Discoveries => Volatile.Read(ref discoveries);
            public int PollObservations;

            public event EventHandler? DevicesChanged;

            public CountingProvider(int connectedCount) => ConnectedCount = connectedCount;

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
            {
                Interlocked.Increment(ref discoveries);
                // Real providers return newly opened candidates on discovery. A duplicate candidate may be
                // disposed without disconnecting the existing device retained by the gameplay handler.
                return ConnectedCount == 0
                    ? Array.Empty<IOmsHidButtonDevice>()
                    : new IOmsHidButtonDevice[] { new IdleDevice(this) };
            }

            public void NotifyDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

            public void Dispose() { }
        }

        private sealed class IdleDevice : IOmsHidButtonDevice
        {
            private readonly CountingProvider provider;

            public string Identifier => "pad-0";
            public bool IsConnected { get; private set; } = true;

            public bool HasPendingChanges
            {
                get
                {
                    provider.PollObservations++;
                    if (provider.DisconnectOnPoll)
                        IsConnected = false;
                    return false;
                }
            }

            public IdleDevice(CountingProvider provider) => this.provider = provider;

            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                provider.PollObservations++;
                if (provider.DisconnectOnPoll)
                    IsConnected = false;
                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }

            public void Dispose() => IsConnected = false;
        }
    }
}
