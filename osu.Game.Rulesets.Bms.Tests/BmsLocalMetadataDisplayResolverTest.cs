// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsLocalMetadataDisplayResolverTest
    {
        [Test]
        public void TestResolvesLegacyBmsMetadataFromArtistAndTags()
        {
            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Ym1024 feat. lamie* /obj:BAECON",
                    ArtistUnicode = "Ym1024 feat. lamie* /obj:BAECON",
                    Tags = "J-Airy Pop",
                }
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmap), Is.EqualTo("Ym1024 feat. lamie*"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtistUnicode(beatmap), Is.EqualTo("Ym1024 feat. lamie*"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("BAECON"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("J-Airy Pop"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
            });
        }

        [Test]
        public void TestResolvesBmsGenreAndCreatorFromRulesetData()
        {
            var metadata = new BeatmapMetadata
            {
                Artist = "Test Artist",
            };

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata
                {
                    Genre = "Hardcore",
                    SubArtist = "obj: Test Charter",
                }
            });

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                Metadata = metadata,
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("Test Charter"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("Hardcore"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
            });
        }
    }
}
