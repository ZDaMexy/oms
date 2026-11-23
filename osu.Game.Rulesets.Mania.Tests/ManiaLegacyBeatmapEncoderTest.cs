// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Tests
{
    public class ManiaLegacyBeatmapEncoderTest
    {
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(14)]
        [TestCase(16)]
        [TestCase(18)]
        public void TestNativeLanesRoundTrip(int keys)
        {
            var mapText = new StringBuilder(FormattableString.Invariant(
                $"osu file format v128\n[General]\nMode:3\n[Difficulty]\nCircleSize:{keys}\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n"));

            for (int lane = 0; lane < keys; lane++)
            {
                int centreX = (int)((lane + 0.5m) * 512 / keys);
                mapText.AppendLine(FormattableString.Invariant($"{centreX},192,{1000 * (lane + 1)},1,0,0:0:0:0:"));
                mapText.AppendLine(FormattableString.Invariant($"{centreX},192,{1000 * (lane + 1) + 125},128,0,{1000 * (lane + 1) + 375}:0:0:0:0:"));
            }

            ManiaBeatmap initial = decode(mapText.ToString());
            Assert.That(initial.HitObjects.Select(h => h.Column), Is.EqualTo(Enumerable.Range(0, keys).SelectMany(lane => new[] { lane, lane })));

            using var output = new StringWriter(CultureInfo.InvariantCulture);
            new LegacyBeatmapEncoder(initial, null).Encode(output);

            ManiaBeatmap reloaded = decode(output.ToString());
            Assert.That(reloaded.HitObjects.Select(h => (h.Column, h.StartTime, h.GetEndTime())),
                Is.EqualTo(initial.HitObjects.Select(h => (h.Column, h.StartTime, h.GetEndTime()))));
        }

        private static ManiaBeatmap decode(string beatmap)
        {
            var ruleset = new ManiaRuleset();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(beatmap));
            using var reader = new LineBufferedReader(stream);
            var decoded = new LegacyBeatmapDecoder(LegacyBeatmapEncoder.FIRST_LAZER_VERSION) { ApplyOffsets = false }.Decode(reader);
            decoded.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            return (ManiaBeatmap)new ManiaBeatmapConverter(decoded, ruleset).Convert();
        }
    }
}
