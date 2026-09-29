// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase("idle")]
        [TestCase("playing")]
        [TestCase("stopped")]
        public void TestBmsSampleSteadyUpdateAllocation(string state)
        {
            Live<SkinInfo> candidate = null!;
            AudioPerformanceSampleHost sampleHost = null!;
            SkinCurrentRevision revision = null!;

            AddStep("select real sample performance package", () =>
            {
                (_, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "performance", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for sample performance revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount measured production sample", () =>
            {
                revision = manager.CurrentRevision;
                Add(sampleHost = new AudioPerformanceSampleHost(manager));
            });
            AddUntilStep("real WAV is loaded", () =>
                sampleHost.Sample.IsLoaded && sampleHost.Sample.Sample?.Length > 500);
            AddStep("prepare audio state", () =>
            {
                if (state == "idle")
                    return;

                sampleHost.Sample.Looping = true;
                sampleHost.Sample.Play();
            });
            AddUntilStep("real playback requested", () => state == "idle" || sampleHost.Sample.Playing);
            AddStep("stop completed-state sample", () =>
            {
                if (state == "stopped")
                    sampleHost.Sample.Stop();
            });
            AddUntilStep("revision work matches audio state", () =>
                state == "playing"
                    ? sampleHost.Sample.Playing && !revision.WorkDetached.IsCompleted
                    : !sampleHost.Sample.Playing && revision.WorkDetached.IsCompleted);
            AddStep("measure unchanged sample update and stop", () =>
            {
                const int warmup_iterations = 256;
                const int measured_iterations = 2048;
                var sample = sampleHost.Sample;
                var nativeSample = sample.Sample;

                for (int i = 0; i < warmup_iterations; i++)
                    sample.UpdateForMeasurement();

                long startTicks = Stopwatch.GetTimestamp();
                long startAllocated = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < measured_iterations; i++)
                    sample.UpdateForMeasurement();

                long updateAllocated = GC.GetAllocatedBytesForCurrentThread() - startAllocated;
                long updateTicks = Stopwatch.GetTimestamp() - startTicks;

                TestContext.WriteLine($"audio-performance state={state} updateCount={measured_iterations} allocatedBytes={updateAllocated} elapsedTicks={updateTicks}");

                Assert.Multiple(() =>
                {
                    Assert.That(updateAllocated, Is.Zero, "A loaded sample must not allocate during unchanged idle or playing updates.");
                    Assert.That(sample.Sample, Is.SameAs(nativeSample), "Steady updates must retain the decoded sample.");
                    Assert.That(sample.Playing, Is.EqualTo(state == "playing"));
                    Assert.That(revision.WorkDetached.IsCompleted, Is.EqualTo(state != "playing"));
                });

                sample.Stop();

                for (int i = 0; i < warmup_iterations; i++)
                    sample.Stop();

                startTicks = Stopwatch.GetTimestamp();
                startAllocated = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < measured_iterations; i++)
                    sample.Stop();

                long stopAllocated = GC.GetAllocatedBytesForCurrentThread() - startAllocated;
                long stopTicks = Stopwatch.GetTimestamp() - startTicks;

                TestContext.WriteLine($"audio-performance state={state} stopCount={measured_iterations} allocatedBytes={stopAllocated} elapsedTicks={stopTicks}");
                Assert.Multiple(() =>
                {
                    Assert.That(stopAllocated, Is.Zero, "Repeatedly stopping an idle sample must not create cleanup collections.");
                    Assert.That(sample.Playing, Is.False);
                });
            });
            AddUntilStep("sample work releases after stop", () => revision.WorkDetached.IsCompleted);
        }

        [Test]
        public void TestBmsCompletedSampleBatchReleasesEveryRevisionLease()
        {
            Live<SkinInfo> candidate = null!;
            AudioPerformanceSampleHost sampleHost = null!;
            SkinCurrentRevision revision = null!;

            AddStep("select overlapping sample package", () =>
            {
                (_, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "overlapping", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for overlapping sample revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount overlapping sample", () =>
            {
                revision = manager.CurrentRevision;
                Add(sampleHost = new AudioPerformanceSampleHost(manager));
            });
            AddUntilStep("overlapping WAV loaded", () =>
                sampleHost.Sample.IsLoaded && sampleHost.Sample.Sample?.Length > 500);
            AddStep("play two voices without draining completion", () =>
            {
                sampleHost.Sample.SuppressAutomaticUpdates = true;
                sampleHost.Sample.Play();
                sampleHost.Sample.Play();
                Assert.That(revision.WorkDetached.IsCompleted, Is.False);
            });
            AddUntilStep("both one-shots finish", () => !sampleHost.Sample.Playing);
            AddStep("drain one completed batch", () =>
            {
                Assert.That(revision.WorkDetached.IsCompleted, Is.False);
                var decodedSample = sampleHost.Sample.Sample;
                sampleHost.Sample.UpdateForMeasurement();

                Assert.Multiple(() =>
                {
                    Assert.That(revision.WorkDetached.IsCompleted, Is.True, "Every completed voice must release its exact work lease.");
                    Assert.That(sampleHost.Sample.Sample, Is.SameAs(decodedSample));
                    Assert.That(sampleHost.Sample.IsOwnedSampleDisposed(decodedSample), Is.False);
                });
                sampleHost.Sample.SuppressAutomaticUpdates = false;
            });
        }

        private sealed partial class AudioPerformanceSampleHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            public MeasuredAudioSample Sample { get; } = new MeasuredAudioSample();

            public AudioPerformanceSampleHost(SkinManager manager)
            {
                skinManager = manager;
                skinSource = manager;
                InternalChild = Sample;
            }
        }

        private sealed partial class MeasuredAudioSample : PoolableSkinnableSample
        {
            public bool SuppressAutomaticUpdates { get; set; }

            public MeasuredAudioSample()
                : base(new SampleInfo("test-sample"))
            {
            }

            public void UpdateForMeasurement() => base.Update();

            protected override void Update()
            {
                if (!SuppressAutomaticUpdates)
                    base.Update();
            }
        }
    }
}
