// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Menus
{
    [TestFixture]
    public class BeatmapSetArtistLocalMetadataDisplayTest
    {
        [Test]
        public void TestBeatmapSetArtistUsesFirstBeatmapDisplayAuthority()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    ArtistUnicode = "表示名 /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                }
            };

            var beatmapSet = new BeatmapSetInfo(new[] { beatmap });

            var result = beatmapSet.GetDisplayArtistRomanisable();

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist"));
            Assert.That(result.Original, Is.EqualTo("表示名"));
            Assert.That(result.Romanised, Does.Not.Contain("Hidden Creator"));
            Assert.That(result.Original, Does.Not.Contain("Hidden Creator"));
        }

        [Test]
        public void TestBeatmapSetArtistPreservesNonBmsValues()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist",
                    ArtistUnicode = "表示名"
                }
            };

            var beatmapSet = new BeatmapSetInfo(new[] { beatmap });

            var result = beatmapSet.GetDisplayArtistRomanisable();

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist"));
            Assert.That(result.Original, Is.EqualTo("表示名"));
        }
    }
}
