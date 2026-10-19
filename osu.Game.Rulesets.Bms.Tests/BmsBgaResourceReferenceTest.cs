// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBgaResourceReferenceTest
    {
        [TestCase("clip.m1v")]
        [TestCase("clip.M2V")]
        public void TestElementaryMpegReferencesReachTheVideoTranscodePath(string filename)
        {
            BmsBeatmap beatmap = convert(filename);

            Assert.Multiple(() =>
            {
                Assert.That(BmsBgaTimelineEntry.IsVideoAsset(filename), Is.True);
                Assert.That(BmsBgaVideoCache.RequiresTranscode(filename), Is.True);
                Assert.That(beatmap.BgaTimeline.Single().IsVideo, Is.True,
                    "The preloader must see a video, rather than ask the image decoder to open an elementary MPEG stream.");
            });
        }

        [TestCase("../outside.mp4")]
        [TestCase(@"..\outside.mpg")]
        [TestCase(@"C:\outside.mp4")]
        [TestCase(@"\\server\share\outside.mp4")]
        [TestCase("/outside.mp4")]
        public void TestUnsafeProgrammaticResourceCannotEnterAnImmutableTimeline(string filename)
        {
            Assert.Throws<ArgumentException>(() => new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, filename, true));
        }

        [TestCase("../outside.mp4")]
        [TestCase(@"..\outside.mpg")]
        [TestCase(@"C:\outside.mp4")]
        [TestCase(@"\\server\share\outside.mp4")]
        [TestCase("/outside.mp4")]
        public void TestUnsafeBitmapReferenceIsSkippedWithoutRejectingThePlayableChart(string filename)
        {
            BmsBeatmap beatmap = null!;
            Assert.DoesNotThrow(() => beatmap = convert(filename));
            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects, Is.Not.Empty, "An invalid optional BGA reference must not make the chart unplayable.");
                Assert.That(beatmap.BgaTimeline, Is.Empty, "No video/preloader may resolve a path outside the chart directory.");
            });
        }

        [Test]
        public void TestValidSubdirectoryResourceIsNormalisedForBothTimelineEntryPoints()
        {
            const string reference = @"movie\intro.mp4";
            var entry = new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, reference, true);
            BmsBeatmap beatmap = convert(reference);

            Assert.Multiple(() =>
            {
                Assert.That(entry.AssetFile, Is.EqualTo("movie/intro.mp4"));
                Assert.That(beatmap.BgaTimeline.Single().AssetFile, Is.EqualTo("movie/intro.mp4"));
                Assert.That(beatmap.BgaTimeline.Single().IsVideo, Is.True);
            });
        }

        private static BmsBeatmap convert(string bitmapFile)
        {
            string text = $"#TITLE BGA resource boundary\n#BPM 120\n#WAV01 key.wav\n#BMP01 {bitmapFile}\n#00111:01\n#00104:01\n";
            BmsDecodedChart chart = new BmsBeatmapDecoder().DecodeText(text, "bga-resource.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), new BmsRuleset()).Convert();
        }
    }
}
