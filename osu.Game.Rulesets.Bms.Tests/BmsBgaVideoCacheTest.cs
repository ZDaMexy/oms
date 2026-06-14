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
        public void TestFriendlyFormatResolvesReadyAsIs()
        {
            var cache = new BmsBgaVideoCache(Path.Combine(tempRoot, "cache"), Array.Empty<string>(), (_, _, _) => false);
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

            var cache = new BmsBgaVideoCache(null, Array.Empty<string>(), (_, _, _) => true);

            Assert.That(cache.Resolve(source).State, Is.EqualTo(BmsBgaVideoCache.VideoSourceState.Unavailable));
        }

        [Test]
        public void TestLegacyTranscodesThenCacheHitReturnsSamePath()
        {
            string source = Path.Combine(tempRoot, "bga.mpg");
            File.WriteAllText(source, "data");
            string cacheDir = Path.Combine(tempRoot, "cache");

            var cache = new BmsBgaVideoCache(cacheDir, Array.Empty<string>(), (_, _, tmp) =>
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
            var cache = new BmsBgaVideoCache(cacheDir, Array.Empty<string>(), (_, _, tmp) =>
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
