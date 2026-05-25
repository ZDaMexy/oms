// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class BeatmapAttributeTextLocalMetadataDisplayTest
    {
        [Test]
        public void TestBeatmapAttributeTextUsesBmsArtistAndCreatorFallback()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    ArtistUnicode = "表示名 /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}",
                    Author =
                    {
                        Username = string.Empty
                    }
                }
            };

            var artist = BeatmapAttributeText.GetDisplayedArtist(beatmap);
            var creator = BeatmapAttributeText.GetDisplayedCreator(beatmap);

            Assert.That(artist.Romanised, Is.EqualTo("Visible Artist"));
            Assert.That(artist.Original, Is.EqualTo("表示名"));
            Assert.That(artist.Romanised, Does.Not.Contain("Hidden Creator"));
            Assert.That(artist.Original, Does.Not.Contain("Hidden Creator"));
            Assert.That(creator, Is.EqualTo("Hidden Creator"));
        }

        [Test]
        public void TestBeatmapAttributeTextPreservesNonBmsArtistAndCreatorValue()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist",
                    ArtistUnicode = "表示名",
                    Author =
                    {
                        Username = "Visible Creator"
                    }
                }
            };

            var artist = BeatmapAttributeText.GetDisplayedArtist(beatmap);
            var creator = BeatmapAttributeText.GetDisplayedCreator(beatmap);

            Assert.That(artist.Romanised, Is.EqualTo("Visible Artist"));
            Assert.That(artist.Original, Is.EqualTo("表示名"));
            Assert.That(creator, Is.EqualTo("Visible Creator"));
        }
    }
}
