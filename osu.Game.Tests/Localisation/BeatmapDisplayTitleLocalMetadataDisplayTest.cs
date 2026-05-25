// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Localisation
{
    [TestFixture]
    public class BeatmapDisplayTitleLocalMetadataDisplayTest
    {
        [Test]
        public void TestBeatmapDisplayTitleUsesBmsCreatorFallback()
        {
            var beatmap = createBmsBeatmap();

            Assert.That(beatmap.GetDisplayTitle(), Is.EqualTo("Visible Artist - Visible Title (Hidden Creator) [Another]"));
        }

        [Test]
        public void TestBeatmapDisplayTitlePreservesNonBmsCreatorValue()
        {
            var beatmap = createOsuBeatmap();

            Assert.That(beatmap.GetDisplayTitle(), Is.EqualTo("Visible Artist - Visible Title (Visible Creator) [Insane]"));
        }

        [Test]
        public void TestBeatmapSetDisplayTitleUsesFirstBeatmapDisplayAuthority()
        {
            var beatmapSet = new BeatmapSetInfo(new[] { createBmsBeatmap() });

            Assert.That(beatmapSet.GetDisplayTitle(), Is.EqualTo("Visible Artist - Visible Title (Hidden Creator)"));
        }

        [Test]
        public void TestBeatmapSetDisplayStringUsesFirstBeatmapDisplayAuthority()
        {
            IBeatmapSetInfo beatmapSet = new BeatmapSetInfo(new[] { createBmsBeatmap() });

            Assert.That(beatmapSet.GetDisplayString(), Is.EqualTo("Visible Artist - Visible Title (Hidden Creator)"));
        }

        private static BeatmapInfo createBmsBeatmap()
            => new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
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

        private static BeatmapInfo createOsuBeatmap()
            => new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
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
    }
}
