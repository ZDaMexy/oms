// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.Ranking.Expanded;

namespace osu.Game.Tests.Scores
{
    [TestFixture]
    public class ExpandedPanelMiddleContentLocalMetadataDisplayTest
    {
        [Test]
        public void TestDisplayedMetadataUsesBmsLocalFallback()
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

            Assert.That(ExpandedPanelMiddleContent.GetDisplayedArtistText(beatmap), Is.EqualTo("Visible Artist"));
            Assert.That(ExpandedPanelMiddleContent.GetDisplayedArtistUnicodeText(beatmap), Is.EqualTo("表示名"));
            Assert.That(ExpandedPanelMiddleContent.GetDisplayedCreatorText(beatmap), Is.EqualTo("Hidden Creator"));
        }

        [Test]
        public void TestDisplayedMetadataPreservesNonBmsValues()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Visible Creator",
                    ArtistUnicode = "表示名 /obj: Visible Creator",
                    Author =
                    {
                        Username = "Visible Creator"
                    }
                }
            };

            Assert.That(ExpandedPanelMiddleContent.GetDisplayedArtistText(beatmap), Is.EqualTo("Visible Artist /obj: Visible Creator"));
            Assert.That(ExpandedPanelMiddleContent.GetDisplayedArtistUnicodeText(beatmap), Is.EqualTo("表示名 /obj: Visible Creator"));
            Assert.That(ExpandedPanelMiddleContent.GetDisplayedCreatorText(beatmap), Is.EqualTo("Visible Creator"));
        }
    }
}
