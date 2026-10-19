// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidAxisPerformanceTest
    {
        [Test]
        public void TestRepeatedAxisDirectionChangesDoNotAllocateAfterWarmup()
        {
            int presses = 0;
            int releases = 0;
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive),
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Negative)),
            }, _ => presses++, _ => releases++);

            for (int i = 0; i < 256; i++)
                poll();
            presses = releases = 0;

            const int polls = 16384;
            long start = Stopwatch.GetTimestamp();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < polls; i++)
                poll();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            TestContext.Progress.WriteLine($"hid-axis: polls={polls} transitions={presses + releases} allocated={allocated} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(presses, Is.EqualTo(polls * 2));
                Assert.That(releases, Is.EqualTo(polls * 2));
                Assert.That(allocated, Is.Zero);
            });

            void poll()
            {
                handler.BeginPolling();
                handler.ApplyAxisDelta("turntable", 0, 5);
                handler.ApplyAxisDelta("turntable", 0, -5);
                handler.FinishPolling();
            }
        }
    }
}
