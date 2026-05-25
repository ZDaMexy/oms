// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Localisation
{
    [TestFixture]
    public class BeatmapInfoRomanisationLocalMetadataDisplayTest
    {
        [Test]
        public void TestDisplayTitleRomanisableUsesBmsCreatorFallback()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    ArtistUnicode = "表示名 /obj: Hidden Creator",
                    Title = "Visible Title",
                    TitleUnicode = "表示标题",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                },
                DifficultyName = "Another"
            };

            var result = beatmap.GetDisplayTitleRomanisable();

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title (Hidden Creator) [Another]"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题 (Hidden Creator) [Another]"));
        }

        [Test]
        public void TestDisplayTitleRomanisablePreservesNonBmsCreatorValue()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist",
                    ArtistUnicode = "表示名",
                    Title = "Visible Title",
                    TitleUnicode = "表示标题",
                    Author =
                    {
                        Username = "Visible Creator"
                    }
                },
                DifficultyName = "Insane"
            };

            var result = beatmap.GetDisplayTitleRomanisable();

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title (Visible Creator) [Insane]"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题 (Visible Creator) [Insane]"));
        }
    }
}
