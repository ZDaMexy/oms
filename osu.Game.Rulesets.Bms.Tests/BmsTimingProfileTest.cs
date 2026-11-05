// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsTimingProfileTest
    {
        private BmsBeatmap beatmap = null!;

        [SetUp]
        public void SetUp()
        {
            var decoded = new BmsBeatmapDecoder().DecodeText(@"
#TITLE Timing Profile
#BPM 120
#BPMAA 240
#BPMAE 999
#STOPAB 96
#SCROLLAC 0.5
#00102:0.5
#001SC:AC00
#00108:00AA
#00109:000000AB
#00111:0000CC00
#00211:DD00
", "timing-profile.bme");

            beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
        }

        [Test]
        public void TestBpmRangeIgnoresStopScrollAndUnusedDefinitions()
        {
            Assert.That(beatmap.TimingProfile!.MinimumBpm, Is.EqualTo(120));
            Assert.That(beatmap.TimingProfile.MaximumBpm, Is.EqualTo(240));
            Assert.That(beatmap.TimingProfile.MinimumReadingRate, Is.EqualTo(60));
            Assert.That(beatmap.TimingProfile.MaximumReadingRate, Is.EqualTo(120));
        }

        [Test]
        public void TestReadingRangeUsesPairedRatesAndKeepsTheMusicalReferenceDuringFreezeOrReverse()
        {
            var profile = new BmsTimingProfile(new[]
            {
                new BmsTimingSegment(0, 0, 120, false, .5),
                new BmsTimingSegment(1000, 2, 240, false, .25),
                new BmsTimingSegment(2000, 6, 150, false, 2),
                new BmsTimingSegment(3000, 8.5, 180, false, 0),
                new BmsTimingSegment(4000, 11.5, 210, false, -2),
                new BmsTimingSegment(5000, 15, 60, true, 1),
            }, new[] { 0d });
            Assert.That(profile.MinimumReadingRate, Is.EqualTo(60));
            Assert.That(profile.MaximumReadingRate, Is.EqualTo(300),
                "BPM 240 never has SCROLL 2, and zero/reverse SCROLL must not invent a zero/absolute forward reading rate.");
        }

        [Test]
        public void TestReadingRangeUsesTheSameMinimumEffectiveBpmAsNativeScrolling()
        {
            var profile = new BmsTimingProfile(new[]
            {
                new BmsTimingSegment(0, 0, .0001, false, 1),
                new BmsTimingSegment(1000, 0, 120, false, 1),
            }, new[] { 0d });
            Assert.That(profile.MinimumReadingRate, Is.EqualTo(.001));
            Assert.That(profile.MaximumReadingRate, Is.EqualTo(120));
        }

        [Test]
        public void TestConverterProjectionKeepsRealBpmAndFreezesBeatDuringStop()
        {
            BmsTimingProfile profile = beatmap.TimingProfile!;
            BmsTimingSample beforeStop = profile.Sample(2600);
            BmsTimingSample duringStop = profile.Sample(2700);
            BmsTimingSample stopEnd = profile.Sample(3125);
            BmsTimingSample afterStop = profile.Sample(3250);

            Assert.Multiple(() =>
            {
                Assert.That(beforeStop.Bpm, Is.EqualTo(240));
                Assert.That(beforeStop.IsStopped, Is.False);
                Assert.That(duringStop.Bpm, Is.EqualTo(240));
                Assert.That(duringStop.IsStopped, Is.True);
                Assert.That(duringStop.Beat, Is.EqualTo(5.5).Within(0.001));
                Assert.That(stopEnd.Bpm, Is.EqualTo(240));
                Assert.That(stopEnd.IsStopped, Is.False);
                Assert.That(stopEnd.Beat, Is.EqualTo(duringStop.Beat).Within(0.001));
                Assert.That(afterStop.Beat, Is.EqualTo(6).Within(0.001));
            });
        }

        [Test]
        public void TestBarIndexUsesConverterMeasureStartsAcrossVariableMeasureAndStop()
        {
            BmsTimingProfile profile = beatmap.TimingProfile!;

            Assert.Multiple(() =>
            {
                Assert.That(profile.Sample(-100).BarIndex, Is.EqualTo(-1));
                Assert.That(profile.Sample(1999).BarIndex, Is.Zero);
                Assert.That(profile.Sample(2000).BarIndex, Is.EqualTo(1));
                Assert.That(profile.Sample(2700).BarIndex, Is.EqualTo(1));
                Assert.That(profile.Sample(3249).BarIndex, Is.EqualTo(1));
                Assert.That(profile.Sample(3250).BarIndex, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestNeutralProjectionUsesScrollProfileWithoutExposingZeroMultiplier()
        {
            var projection = new BmsGameplaySkinTimingProjection(beatmap);
            var beforeStop = projection.Sample(2550);
            var duringStop = projection.Sample(2700);
            var afterStop = projection.Sample(3200);

            Assert.Multiple(() =>
            {
                Assert.That(beforeStop.ScrollMultiplier, Is.Not.Zero);
                Assert.That(duringStop.IsStopped, Is.True);
                Assert.That(duringStop.ScrollMultiplier, Is.EqualTo(1e-9));
                Assert.That(afterStop.IsStopped, Is.False);
                Assert.That(afterStop.ScrollMultiplier, Is.Not.Zero);
                Assert.That(new[] { beforeStop.Bpm, duringStop.Bpm, afterStop.Bpm }, Is.All.EqualTo(240));
            });
        }
    }
}
