// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class LegacyBeatmapSkinLocalMetadataDisplayTest
    {
        [Test]
        public void TestBeatmapSkinUsesBmsCreatorFallback()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = BmsStarRatingResolver.RulesetShortName })
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Visible Artist /obj: Hidden Creator",
                    RulesetDataJson = "{\"chart_metadata\":{\"sub_artist\":\"obj: Hidden Creator\"}}"
                }
            };

            var skin = new LegacyBeatmapSkin(beatmap, null);

            Assert.That(skin.SkinInfo.Value.Creator, Is.EqualTo("Hidden Creator"));
        }

        [Test]
        public void TestBeatmapSkinPreservesNonBmsCreatorValue()
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

            var skin = new LegacyBeatmapSkin(beatmap, null);

            Assert.That(skin.SkinInfo.Value.Creator, Is.EqualTo("Visible Creator"));
        }
    }
}
