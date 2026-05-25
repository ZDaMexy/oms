// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Menus
{
    [TestFixture]
    public class MenuBeatmapMetadataLocalDisplayTest
    {
        [Test]
        public void TestMenuMetadataUsesBmsArtistFallback()
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
                SongTicker.GetDisplayedArtistText,
                SongTicker.GetDisplayedArtistUnicodeText);

            assertArtistFallback(
                beatmap,
                NowPlayingOverlay.GetDisplayedArtistText,
                NowPlayingOverlay.GetDisplayedArtistUnicodeText);
        }

        [Test]
        public void TestMenuMetadataPreservesNonBmsArtistValues()
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
                SongTicker.GetDisplayedArtistText,
                SongTicker.GetDisplayedArtistUnicodeText);

            assertArtistPassthrough(
                beatmap,
                NowPlayingOverlay.GetDisplayedArtistText,
                NowPlayingOverlay.GetDisplayedArtistUnicodeText);
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
