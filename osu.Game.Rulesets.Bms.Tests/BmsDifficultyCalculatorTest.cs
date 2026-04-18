// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsDifficultyCalculatorTest
    {
        [Test]
        public void TestReturnsStaticStarRatingFromPlayLevel()
        {
            var beatmap = createPlayableBeatmap(
                BmsKeymode.Key7K,
                "★12",
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 0, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 500, EndTime = 1500, LaneIndex = 2 },
                new BmsHitObject { StartTime = 1500, LaneIndex = 3 });

            var calculator = new BmsDifficultyCalculator(new BmsRuleset().RulesetInfo, new TestWorkingBeatmap(beatmap, beatmap));

            var attributes = calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(attributes.StarRating, Is.EqualTo(12));
                Assert.That(attributes.MaxCombo, Is.EqualTo(4));
                Assert.That(attributes.Mods, Is.Empty);
            });
        }

        [Test]
        public void TestReturnsZeroWithoutPlayLevel()
        {
            var beatmap = createPlayableBeatmap(
                BmsKeymode.Key7K,
                string.Empty,
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 0, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 500, EndTime = 1500, LaneIndex = 2 },
                new BmsHitObject { StartTime = 1500, LaneIndex = 3 });

            var calculator = new BmsDifficultyCalculator(new BmsRuleset().RulesetInfo, new TestWorkingBeatmap(beatmap, beatmap));

            var attributes = calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(attributes.StarRating, Is.Zero);
                Assert.That(attributes.MaxCombo, Is.EqualTo(4));
                Assert.That(attributes.Mods, Is.Empty);
            });
        }

        [Test]
        public void TestReturnsStaticStarRatingFromDecodedBeatmapSource()
        {
            const string text = @"
#TITLE Difficulty Source
#BPM 120
#PLAYLEVEL 12
#00111:AA00
#00112:BB00
#00213:CC00
#LNTYPE 1
#00354:DD00EE00
";

            var decodedChart = new BmsBeatmapDecoder().DecodeText(text, "difficulty.bme");
            var sourceBeatmap = new BmsDecodedBeatmap(decodedChart);
            var playableBeatmap = (BmsBeatmap)new BmsBeatmapConverter(sourceBeatmap, new BmsRuleset()).Convert();
            var calculator = new BmsDifficultyCalculator(new BmsRuleset().RulesetInfo, new TestWorkingBeatmap(sourceBeatmap, playableBeatmap));

            var attributes = calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(attributes.StarRating, Is.EqualTo(12));
                Assert.That(attributes.MaxCombo, Is.EqualTo(4));
                Assert.That(attributes.Mods, Is.Empty);
            });
        }

        private static BmsBeatmap createPlayableBeatmap(BmsKeymode keymode, string playLevel, params BmsHitObject[] hitObjects)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo
                {
                    Keymode = keymode,
                    PlayLevel = playLevel,
                },
            };

            beatmap.Difficulty.CircleSize = BmsRuleset.GetKeyCount(keymode);
            beatmap.HitObjects.AddRange(hitObjects);
            return beatmap;
        }

        private class TestWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap sourceBeatmap;
            private readonly IBeatmap playableBeatmap;

            public TestWorkingBeatmap(IBeatmap sourceBeatmap, IBeatmap playableBeatmap)
                : base(sourceBeatmap.BeatmapInfo, null)
            {
                this.sourceBeatmap = sourceBeatmap;
                this.playableBeatmap = playableBeatmap;
            }

            protected override IBeatmap GetBeatmap() => sourceBeatmap;

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token) => playableBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
