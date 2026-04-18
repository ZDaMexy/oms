// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class BmsStarRatingResolverTest
    {
        [TestCase("12", 12)]
        [TestCase("★12", 12)]
        [TestCase("Lv.7", 7)]
        [TestCase("sl3", 3)]
        [TestCase("15.5", 15.5)]
        public void TestParsePlayLevel(string playLevel, double expected)
        {
            Assert.That(BmsStarRatingResolver.TryParsePlayLevel(playLevel, out double starRating), Is.True);
            Assert.That(starRating, Is.EqualTo(expected));
        }

        [Test]
        public void TestResolveFromMetadata()
        {
            var metadata = new BeatmapMetadata
            {
                RulesetDataJson = "{\"chart_metadata\":{\"play_level\":\"★12\"}}"
            };

            Assert.That(BmsStarRatingResolver.TryResolveFromMetadata(metadata, out double starRating), Is.True);
            Assert.That(starRating, Is.EqualTo(12));
        }

        [Test]
        public void TestResolveFromBeatmapInfoFallsBackToMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                StarRating = -1,
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson = "{\"chart_metadata\":{\"play_level\":\"Normal 8\"}}"
                }
            };

            Assert.That(BmsStarRatingResolver.TryResolveFromBeatmapInfo(beatmap, out double starRating), Is.True);
            Assert.That(starRating, Is.EqualTo(8));
        }

        [Test]
        public void TestDoesNotResolveForNonBmsBeatmap()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                StarRating = -1,
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson = "{\"chart_metadata\":{\"play_level\":\"12\"}}"
                }
            };

            Assert.That(BmsStarRatingResolver.TryResolveFromBeatmapInfo(beatmap, out _), Is.False);
        }

        [Test]
        public void TestResolveOrDefaultReturnsZeroForBmsWithoutParseableMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                StarRating = -1,
                Metadata = new BeatmapMetadata
                {
                    RulesetDataJson = "{\"chart_metadata\":{\"play_level\":\"insane\"}}"
                }
            };

            Assert.That(BmsStarRatingResolver.ResolveOrDefault(beatmap), Is.Zero);
        }
    }
}
