// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.OnlinePlay.DailyChallenge;

namespace osu.Game.Tests.OnlinePlay
{
    [TestFixture]
    public class DailyChallengeLocalMetadataDisplayTest
    {
        [Test]
        public void TestDailyChallengeUsesBmsCreatorFallback()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                }
            };

            Assert.That(DailyChallengeIntro.GetDisplayedCreatorText(beatmap), Is.EqualTo("Hidden Creator"));
        }

        [Test]
        public void TestDailyChallengeUsesBeatmapTitleAuthority()
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

            var result = DailyChallengeIntro.GetDisplayedTitleRomanisable(beatmap);

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title (Hidden Creator)"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题 (Hidden Creator)"));
        }

        [Test]
        public void TestDailyChallengePreservesNonBmsCreatorValues()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Visible Creator",
                    Author =
                    {
                        Username = "Visible Creator"
                    }
                }
            };

            Assert.That(DailyChallengeIntro.GetDisplayedCreatorText(beatmap), Is.EqualTo("Visible Creator"));
        }

        [Test]
        public void TestDailyChallengePreservesNonBmsTitleValues()
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

            var result = DailyChallengeIntro.GetDisplayedTitleRomanisable(beatmap);

            Assert.That(result.Romanised, Is.EqualTo("Visible Artist - Visible Title (Visible Creator)"));
            Assert.That(result.Original, Is.EqualTo("表示名 - 表示标题 (Visible Creator)"));
        }
    }
}
