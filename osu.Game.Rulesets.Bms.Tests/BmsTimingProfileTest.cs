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
