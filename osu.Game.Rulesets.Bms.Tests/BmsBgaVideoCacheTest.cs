// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBgaVideoCacheTest
    {
        private string tempRoot = null!;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "oms-bga-cache-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
                // best-effort
            }
        }

        [TestCase("bga.mpg", true)]
        [TestCase("bga.MPG", true)]
        [TestCase("clip.avi", true)]
        [TestCase("clip.wmv", true)]
        [TestCase("clip.flv", true)]
        [TestCase("clip.mp4", false)]
        [TestCase("clip.webm", false)]
        [TestCase("clip.mov", false)]
        [TestCase("still.png", false)]
        public void TestRequiresTranscodeByExtension(string file, bool expected)
        {
            Assert.That(BmsBgaVideoCache.RequiresTranscode(file), Is.EqualTo(expected));
        }

        [Test]
        public void TestFindAvailableFfmpegSkipsMissingAndReturnsExistingCandidate()
        {
            string present = Path.Combine(tempRoot, "ffmpeg.exe");
            File.WriteAllText(present, "stub");

            // A non-existent candidate is skipped; the first existing candidate is returned (mirrors transcode resolution).
            string? found = BmsBgaVideoCache.FindAvailableFfmpeg(new[] { Path.Combine(tempRoot, "missing.exe"), present });

            Assert.That(found, Is.EqualTo(present));
        }

        [Test]
        public void TestTranscodeArgumentsSpecifyMp4MuxerForTmpOutput()
        {
            // Regression: the output temp path ends in ".mp4.tmp"; without an explicit "-f mp4" ffmpeg infers the
            // container from the ".tmp" extension and fails the muxer ("Unable to choose an output format").
            string[] args = BmsBgaVideoCache.BuildTranscodeArguments("in.mpg", "out.mp4.tmp", "libx264");

            int formatIndex = Array.IndexOf(args, "-f");
            int encoderIndex = Array.IndexOf(args, "-c:v");
            int profileIndex = Array.IndexOf(args, "-profile:v");
            int presetIndex = Array.IndexOf(args, "-preset");

            Assert.Multiple(() =>
            {
                Assert.That(formatIndex, Is.GreaterThanOrEqualTo(0), "expected an explicit -f format flag");
                Assert.That(args[formatIndex + 1], Is.EqualTo("mp4"));
                Assert.That(args[encoderIndex + 1], Is.EqualTo("libx264"));
                Assert.That(args[^1], Is.EqualTo("out.mp4.tmp"), "the output temp path must be the final argument");
                // Regression: a default High-profile output failed to decode in-framework; baseline is required for libx264.
                Assert.That(profileIndex, Is.GreaterThanOrEqualTo(0), "expected an explicit -profile:v for libx264");
                Assert.That(args[profileIndex + 1], Is.EqualTo("baseline"));
                // P1-L Phase 5.2 R4: ultrafast keeps the load-time transcode short.
                Assert.That(presetIndex, Is.GreaterThanOrEqualTo(0), "expected an explicit -preset for libx264");
                Assert.That(args[presetIndex + 1], Is.EqualTo("ultrafast"));
            });
        }

        [Test]
        public void TestTranscodeArgumentsOmitH264ProfileForMpeg4Fallback()
        {
            // mpeg4 (the no-libx264 fallback) has no "baseline" profile; passing one would make ffmpeg reject the command.
            string[] args = BmsBgaVideoCache.BuildTranscodeArguments("in.mpg", "out.mp4.tmp", "mpeg4");

            Assert.Multiple(() =>
            {
                Assert.That(args, Does.Not.Contain("-profile:v"));
                // The mpeg4 encoder has no -preset either; passing one would make ffmpeg reject the command.
                Assert.That(args, Does.Not.Contain("-preset"));
                Assert.That(args[Array.IndexOf(args, "-c:v") + 1], Is.EqualTo("mpeg4"));
                Assert.That(args[Array.IndexOf(args, "-f") + 1], Is.EqualTo("mp4"));
            });
        }

        [Test]
        public void TestFriendlyFormatResolvesReadyAsIs()
        {
            var cache = new BmsBgaVideoCache(Path.Combine(tempRoot, "cache"), Array.Empty<string>(), (_, _, _, _) => false);
            string source = Path.Combine(tempRoot, "clip.mp4");

            var resolved = cache.Resolve(source);

            Assert.Multiple(() =>
            {
                Assert.That(resolved.State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Ready));
                Assert.That(resolved.Path, Is.EqualTo(source));
            });
        }

        [Test]
        public void TestLegacyWithoutCacheDirectoryIsUnavailable()
        {
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");

            var cache = new BmsBgaVideoCache(null, Array.Empty<string>(), (_, _, _, _) => true);

            Assert.That(cache.Resolve(source).State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Unavailable));
        }

        [Test]
        public void TestLegacyTranscodesThenCacheHitReturnsSamePath()
        {
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");
            string cacheDir = Path.Combine(tempRoot, "cache");

            var cache = new BmsBgaVideoCache(cacheDir, Array.Empty<string>(), (_, _, tmp, _) =>
            {
                File.WriteAllText(tmp, "transcoded mp4");
                return true;
            });

            // First call kicks off the background transcode.
            Assert.That(cache.Resolve(source).State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Pending));

            var ready = pollUntil(() => cache.Resolve(source), BmsBgaVideoCache.VideoSourceState.Ready);

            Assert.Multiple(() =>
            {
                Assert.That(ready.State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Ready));
                Assert.That(ready.Path, Is.Not.Null);
                Assert.That(File.Exists(ready.Path!), Is.True);
                Assert.That(Path.GetDirectoryName(ready.Path!), Is.EqualTo(cacheDir));
            });

            // A subsequent resolve is an immediate cache hit on the same destination (stable key).
            var cacheHit = cache.Resolve(source);
            Assert.That(cacheHit.State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Ready));
            Assert.That(cacheHit.Path, Is.EqualTo(ready.Path));
        }

        [Test]
        public void TestLegacyTranscodeFailureBecomesUnavailableAndLeavesNoPartialFile()
        {
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");
            string cacheDir = Path.Combine(tempRoot, "cache");

            // Runner writes a partial temp file but reports failure: the cache must NOT publish it.
            var cache = new BmsBgaVideoCache(cacheDir, Array.Empty<string>(), (_, _, tmp, _) =>
            {
                File.WriteAllText(tmp, "partial");
                return false;
            });

            Assert.That(cache.Resolve(source).State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Pending));

            var result = pollUntil(() => cache.Resolve(source), BmsBgaVideoCache.VideoSourceState.Unavailable);

            Assert.Multiple(() =>
            {
                Assert.That(result.State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Unavailable));
                Assert.That(Directory.GetFiles(cacheDir, "*.mp4"), Is.Empty);
                Assert.That(Directory.GetFiles(cacheDir, "*.tmp"), Is.Empty);
            });
        }

        [Test]
        public void TestPrewarmAndWaitBlocksUntilTranscodeCompletes()
        {
            // R1: the load-time wait must hold until the transcode finishes, so the result is immediately Ready (not
            // Pending) afterwards — that is what lets the BGA play from the first frame instead of hot-swapping in late.
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");
            string cacheDir = Path.Combine(tempRoot, "cache");

            var cache = new BmsBgaVideoCache(cacheDir, Array.Empty<string>(), (_, _, tmp, _) =>
            {
                Thread.Sleep(150);
                File.WriteAllText(tmp, "transcoded mp4");
                return true;
            });

            cache.PrewarmAndWait(new[] { source }, 5000);

            Assert.That(cache.Resolve(source).State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Ready));
        }

        [Test]
        public void TestPrewarmAndWaitReturnsAtCapWhenTranscodeIsSlow()
        {
            // R1 safety net: a long transcode must not block loading past the cap; it keeps running in the background.
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");

            using var gate = new ManualResetEventSlim(false);

            var cache = new BmsBgaVideoCache(Path.Combine(tempRoot, "cache"), Array.Empty<string>(), (_, _, _, _) =>
            {
                gate.Wait(5000);
                return false;
            });

            var stopwatch = Stopwatch.StartNew();
            cache.PrewarmAndWait(new[] { source }, 200);
            stopwatch.Stop();

            gate.Set();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2500), "PrewarmAndWait must return near the cap, not wait for the slow transcode");
        }

        [Test]
        public void TestPrewarmAndWaitDoesNotWaitForFriendlyOrCachedSources()
        {
            // Friendly formats and already-cached outputs add no wait (nothing to transcode).
            var friendly = new BmsBgaVideoCache(Path.Combine(tempRoot, "cache"), Array.Empty<string>(), (_, _, _, _) =>
            {
                Assert.Fail("a framework-friendly format must not be transcoded");
                return false;
            });

            var stopwatch = Stopwatch.StartNew();
            friendly.PrewarmAndWait(new[] { Path.Combine(tempRoot, "clip.mp4") }, 5000);
            stopwatch.Stop();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
        }

        [Test]
        public void TestClearCacheDirectoryDeletesAllFiles()
        {
            string cacheDir = Path.Combine(tempRoot, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "old.mp4"), "x");
            File.WriteAllText(Path.Combine(cacheDir, "stale.mp4.tmp"), "y");

            BmsBgaVideoCache.ClearCacheDirectory(cacheDir);

            Assert.That(Directory.GetFiles(cacheDir), Is.Empty);
        }

        [Test]
        public void TestParseFfmpegProgressFromDurationAndTimeLines()
        {
            double total = 0;

            // The "Duration:" header sets the total (and is not itself a progress reading).
            Assert.That(BmsBgaVideoCache.TryParseFfmpegProgressLine("  Duration: 00:00:10.00, start: 0.000000, bitrate: 1234 kb/s", ref total, out _), Is.False);
            Assert.That(total, Is.EqualTo(10).Within(0.01));

            // A subsequent "time=" line yields its fraction of the total.
            Assert.That(BmsBgaVideoCache.TryParseFfmpegProgressLine("frame=120 fps=30 q=28.0 size=1kB time=00:00:05.00 bitrate=1.0kbits/s", ref total, out double half), Is.True);
            Assert.That(half, Is.EqualTo(0.5).Within(0.01));

            // A time beyond the duration clamps to 1.
            Assert.That(BmsBgaVideoCache.TryParseFfmpegProgressLine("frame=240 time=00:00:12.00 bitrate=1.0kbits/s", ref total, out double full), Is.True);
            Assert.That(full, Is.EqualTo(1).Within(0.001));
        }

        [Test]
        public void TestParseFfmpegProgressReturnsFalseWithoutKnownDuration()
        {
            double total = 0;

            // Without a Duration seen yet, a time= line can't be turned into a fraction.
            Assert.That(BmsBgaVideoCache.TryParseFfmpegProgressLine("frame=10 time=00:00:01.00 bitrate=1.0kbits/s", ref total, out _), Is.False);
        }

        private static BmsBgaVideoCache.VideoSource pollUntil(Func<BmsBgaVideoCache.VideoSource> resolve, BmsBgaVideoCache.VideoSourceState target, int timeoutMs = 5000)
        {
            var stopwatch = Stopwatch.StartNew();
            BmsBgaVideoCache.VideoSource source;

            do
            {
                source = resolve();

                if (source.State == target)
                    return source;

                Thread.Sleep(20);
            } while (stopwatch.ElapsedMilliseconds < timeoutMs);

            return source;
        }
    }
}
