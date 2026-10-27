// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsReadingScrollProfileTest
    {
        [TestCase(0)]
        [TestCase(-0.5)]
        public void TestReadingSampleRetainsZeroAndNegativeScrollWithoutChangingLegacyRendering(double scroll)
        {
            string text = FormattableString.Invariant($"#TITLE Retain original SCROLL\n#BPM 120\n#SCROLL01 {scroll}\n#001SC:01\n#00411:01\n");
            var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text, "reading-scroll-boundary.bme"));
            var beatmap = (BmsBeatmap)new BmsBeatmapConverter(source, new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.TimingProfile!.Sample(3000).ScrollSpeed, Is.EqualTo(scroll), "Reading metadata must not take abs or clamp the original sign.");
                Assert.That(beatmap.ControlPointInfo.EffectPointAt(3000).ScrollSpeed, Is.EqualTo(0.01), "The existing base clamp remains unchanged.");
                Assert.That(beatmap.ScrollProfile!.PositionDelta(3000, 3100), Is.EqualTo(scroll * 100).Within(0.000001),
                    "This preserves the old distance data only; it does not claim that a negative profile has a supported inverse/rendering path.");
            });
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void TestNonFiniteScrollNeverReachesTheReadingTimeline(string scroll)
        {
            var decoded = new BmsBeatmapDecoder().DecodeText($"#TITLE Invalid SCROLL\n#BPM 120\n#SCROLL01 {scroll}\n#001SC:01\n#00411:01\n", "invalid-reading-scroll.bme");
            var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScrollEvents, Is.Empty);
                Assert.That(decoded.Warnings.Any(warning => warning.Contains("SCROLL")), Is.True);
                Assert.That(beatmap.TimingProfile!.Sample(3000).ScrollSpeed, Is.EqualTo(1));
                Assert.That(beatmap.ScrollProfile!.PositionDelta(3000, 3100), Is.EqualTo(100).Within(0.000001));
            });
        }
    }
}
