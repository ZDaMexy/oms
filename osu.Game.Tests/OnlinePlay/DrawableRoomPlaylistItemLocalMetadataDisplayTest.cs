// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.OnlinePlay;

namespace osu.Game.Tests.OnlinePlay
{
    [TestFixture]
    public class DrawableRoomPlaylistItemLocalMetadataDisplayTest
    {
        [Test]
        public void TestPlaylistItemUsesBmsCreatorFallbackWithoutLinkedProfile()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                }
            };

            Assert.That(DrawableRoomPlaylistItem.GetDisplayedCreatorText(beatmap), Is.EqualTo("Hidden Creator"));
            Assert.That(DrawableRoomPlaylistItem.HasLinkedDisplayedCreator(beatmap), Is.False);
        }

        [Test]
        public void TestPlaylistItemPreservesLinkedCreatorProfile()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist",
                    Author =
                    {
                        Username = "Visible Creator"
                    }
                }
            };

            Assert.That(DrawableRoomPlaylistItem.GetDisplayedCreatorText(beatmap), Is.EqualTo("Visible Creator"));
            Assert.That(DrawableRoomPlaylistItem.HasLinkedDisplayedCreator(beatmap), Is.True);
        }
    }
}
