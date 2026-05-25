// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class BeatmapLocalMetadataDisplayResolverTest
    {
        [Test]
        public void TestDisplayCreatorUsesPersistedBmsChartMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Example Artist",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Test Charter\"}}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("Test Charter"));
        }

        [Test]
        public void TestDisplayArtistStripsEmbeddedBmsCreator()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Test Artist /obj: Test Charter",
                    ArtistUnicode = "表示名 /obj: Test Charter"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmap), Is.EqualTo("Test Artist"));
            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtistUnicode(beatmap), Is.EqualTo("表示名"));
        }

        [Test]
        public void TestDisplayGenreAndMapperTagsUsePersistedBmsChartMetadata()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Tags = "Hardcore",
                    RulesetDataJson = "{\"chart_metadata\":{\"genre\":\"Hardcore\"}}"
                }
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("Hardcore"));
            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
        }
    }
}
