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

        [Test]
        public void TestCircularAndHeldStickMotionDoNotAllocateAfterWarmup()
        {
            int motions = 0;
            double time = 0;
            int position = 0;
            long[] stageAllocations = new long[4];
            int[] firstAllocation = new int[4];
            Array.Fill(firstAllocation, -1);
            var handler = new OmsHidAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidAxis("turntable", 0, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition))),
                new OmsBinding(OmsAction.Key2P_Scratch,
                    OmsBindingTrigger.HidAxis("stick", 0, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(OmsAxisMode.StickToMotion))),
            }, _ => { }, _ => { }, _ => motions++);
            handler.Update(time);
            handler.ApplyAxisChange(new OmsHidAxisChange("stick", 0, 100, 100, -100, 100, false));
            handler.ApplyAxisChange(new OmsHidAxisChange("turntable", 0, 0, position, 0, 255, false));

            for (int i = 0; i < 256; i++)
                poll(i - 256);
            motions = 0;
            Array.Clear(stageAllocations);
            Array.Fill(firstAllocation, -1);

            const int polls = 16384;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < polls; i++)
                poll(i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            TestContext.WriteLine($"hid-position-motion: polls={polls} motions={motions} allocated={allocated} " +
                                  $"update={stageAllocations[0]}@{firstAllocation[0]} construct={stageAllocations[1]}@{firstAllocation[1]} " +
                                  $"apply={stageAllocations[2]}@{firstAllocation[2]} finish={stageAllocations[3]}@{firstAllocation[3]}");
            Assert.That(motions, Is.EqualTo(polls * 2));
            Assert.That(allocated, Is.Zero);

            void poll(int iteration)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                handler.BeginPolling();
                handler.Update(time += 1);
                measure(0, iteration, before);
                position = (position + 5) & 255;
                before = GC.GetAllocatedBytesForCurrentThread();
                var change = new OmsHidAxisChange("turntable", 0, 5, position, 0, 255, false);
                measure(1, iteration, before);
                before = GC.GetAllocatedBytesForCurrentThread();
                handler.ApplyAxisChange(change);
                measure(2, iteration, before);
                before = GC.GetAllocatedBytesForCurrentThread();
                handler.FinishPolling();
                measure(3, iteration, before);
            }

            void measure(int stage, int iteration, long before)
            {
                long difference = GC.GetAllocatedBytesForCurrentThread() - before;
                stageAllocations[stage] += difference;
                if (difference > 0 && firstAllocation[stage] == -1)
                    firstAllocation[stage] = iteration;
            }
        }
    }
}
