// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match.RoundResults;

namespace osu.Game.Tests.OnlinePlay
{
    [TestFixture]
    public class SubScreenRoundResultsLocalMetadataDisplayTest
    {
        [Test]
        public void TestPrefersLocalBeatmapInfoWhenAvailable()
        {
            var localBeatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                OnlineID = 1234,
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                }
            };

            var apiBeatmap = createApiBeatmap(1234, "API Artist", "API Title", string.Empty);

            BeatmapInfo result = SubScreenRoundResults.CreateScoreBeatmapInfo(apiBeatmap, localBeatmap);

            Assert.That(result, Is.SameAs(localBeatmap));
            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(result), Is.EqualTo("Hidden Creator"));
        }

        [Test]
        public void TestFallsBackToApiBeatmapInfoWhenLocalBeatmapUnavailable()
        {
            var apiBeatmap = createApiBeatmap(5678, "API Artist", "API Title", "API Creator");

            BeatmapInfo result = SubScreenRoundResults.CreateScoreBeatmapInfo(apiBeatmap);

            Assert.That(result.Metadata.Artist, Is.EqualTo("API Artist"));
            Assert.That(result.Metadata.Title, Is.EqualTo("API Title"));
            Assert.That(result.Metadata.Author.Username, Is.EqualTo("API Creator"));
        }

        private static APIBeatmap createApiBeatmap(int onlineId, string artist, string title, string creator)
        {
            var beatmapSet = new APIBeatmapSet
            {
                Artist = artist,
                Title = title,
                Author = new APIUser
                {
                    Username = creator
                }
            };

            return new APIBeatmap
            {
                OnlineID = onlineId,
                DifficultyName = "Another",
                StarRating = 7.2,
                Length = 123456,
                BPM = 180,
                BeatmapSet = beatmapSet
            };
        }
    }
}
