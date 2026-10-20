// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsMouseAxisPerformanceTest
    {
        [Test]
        public void TestRepeatedMotionAndDirectionChangesDoNotAllocateAfterWarmup()
        {
            int presses = 0;
            int releases = 0;
            int motions = 0;
            var handler = new OmsMouseAxisInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative)),
            }, _ => presses++, _ => releases++, _ => motions++);

            for (int i = 0; i < 256; i++)
                frame();
            presses = releases = motions = 0;

            const int frames = 16384;
            long start = Stopwatch.GetTimestamp();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < frames; i++)
                frame();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            TestContext.Progress.WriteLine($"mouse-axis: frames={frames} transitions={presses + releases} motions={motions} allocated={allocated} elapsedMs={elapsed:F3}");
            Assert.Multiple(() =>
            {
                Assert.That(presses, Is.EqualTo(frames * 2));
                Assert.That(releases, Is.EqualTo(frames * 2));
                Assert.That(motions, Is.EqualTo(frames * 2));
                Assert.That(allocated, Is.Zero);
            });

            void frame()
            {
                handler.BeginFrame();
                handler.ApplyAxisDelta(OmsMouseAxis.X, 4);
                handler.ApplyAxisDelta(OmsMouseAxis.X, -4);
                handler.FinishFrame();
            }
        }
    }
}
