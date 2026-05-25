// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Overlays.Profile.Sections.Historical;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class ProfileBeatmapMetadataLocalDisplayTest
    {
        [Test]
        public void TestProfileMetadataUsesBmsArtistFallback()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    ArtistUnicode = "表示名 /obj: Hidden Creator"
                }
            };

            assertArtistFallback(
                beatmap,
                DrawableProfileScore.GetDisplayedArtistText,
                DrawableProfileScore.GetDisplayedArtistUnicodeText);

            assertArtistFallback(
                beatmap,
                DrawableMostPlayedBeatmap.GetDisplayedArtistText,
                DrawableMostPlayedBeatmap.GetDisplayedArtistUnicodeText);
        }

        [Test]
        public void TestProfileMetadataPreservesNonBmsArtistValues()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Visible Creator",
                    ArtistUnicode = "表示名 /obj: Visible Creator"
                }
            };

            assertArtistPassthrough(
                beatmap,
                DrawableProfileScore.GetDisplayedArtistText,
                DrawableProfileScore.GetDisplayedArtistUnicodeText);

            assertArtistPassthrough(
                beatmap,
                DrawableMostPlayedBeatmap.GetDisplayedArtistText,
                DrawableMostPlayedBeatmap.GetDisplayedArtistUnicodeText);
        }

        private static void assertArtistFallback(IBeatmapInfo beatmap, System.Func<IBeatmapInfo, string> artistText, System.Func<IBeatmapInfo, string> artistUnicodeText)
        {
            Assert.That(artistText(beatmap), Is.EqualTo("Visible Artist"));
            Assert.That(artistUnicodeText(beatmap), Is.EqualTo("表示名"));
        }

        private static void assertArtistPassthrough(IBeatmapInfo beatmap, System.Func<IBeatmapInfo, string> artistText, System.Func<IBeatmapInfo, string> artistUnicodeText)
        {
            Assert.That(artistText(beatmap), Is.EqualTo("Visible Artist /obj: Visible Creator"));
            Assert.That(artistUnicodeText(beatmap), Is.EqualTo("表示名 /obj: Visible Creator"));
        }
    }
}
