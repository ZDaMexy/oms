// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Menus
{
    [TestFixture]
    public class BeatmapDeleteDialogLocalMetadataDisplayTest
    {
        [Test]
        public void TestDeleteDialogUsesBeatmapTitleAuthorityWithoutCreator()
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

            var beatmapSet = new BeatmapSetInfo(new[] { beatmap });

            var result = BeatmapDeleteDialog.GetDisplayedTitleRomanisable(beatmapSet);

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题"));
            Assert.That(result.Romanised, Does.Not.Contain("Hidden Creator"));
            Assert.That(result.Original, Does.Not.Contain("Hidden Creator"));
            Assert.That(result.Romanised, Does.Not.Contain("[Another]"));
            Assert.That(result.Original, Does.Not.Contain("[Another]"));
        }

        [Test]
        public void TestDeleteDialogPreservesNonBmsValuesWithoutCreator()
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

            var beatmapSet = new BeatmapSetInfo(new[] { beatmap });

            var result = BeatmapDeleteDialog.GetDisplayedTitleRomanisable(beatmapSet);

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题"));
            Assert.That(result.Romanised, Does.Not.Contain("Visible Creator"));
            Assert.That(result.Original, Does.Not.Contain("Visible Creator"));
            Assert.That(result.Romanised, Does.Not.Contain("[Insane]"));
            Assert.That(result.Original, Does.Not.Contain("[Insane]"));
        }
    }
}
