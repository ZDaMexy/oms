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
        public void TestReturnsDensityBasedAttributes()
        {
            var beatmap = createPlayableBeatmap(
                BmsKeymode.Key7K,
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 0, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 500, EndTime = 1500, LaneIndex = 2 },
                new BmsHitObject { StartTime = 1500, LaneIndex = 3 });

            var calculator = new BmsDifficultyCalculator(new BmsRuleset().RulesetInfo, new TestWorkingBeatmap(beatmap, beatmap));

            var attributes = (BmsDifficultyAttributes)calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(attributes.StarRating, Is.EqualTo(1.74541).Within(0.0001));
                Assert.That(attributes.MaxCombo, Is.EqualTo(4));
                Assert.That(attributes.TotalNoteCount, Is.EqualTo(4));
                Assert.That(attributes.ScratchNoteCount, Is.EqualTo(1));
                Assert.That(attributes.LnNoteCount, Is.EqualTo(1));
                Assert.That(attributes.PeakDensityNps, Is.EqualTo(4.8).Within(0.001));
                Assert.That(attributes.PeakDensityMs, Is.EqualTo(0).Within(0.001));
                Assert.That(attributes.Mods, Is.Empty);
            });
        }

        [Test]
        public void TestCalculatesDifficultyFromDecodedBeatmapSource()
        {
            const string text = @"
#TITLE Difficulty Source
#BPM 120
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

            var attributes = (BmsDifficultyAttributes)calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(attributes.StarRating, Is.GreaterThan(0));
                Assert.That(attributes.MaxCombo, Is.EqualTo(4));
                Assert.That(attributes.TotalNoteCount, Is.EqualTo(4));
                Assert.That(attributes.LnNoteCount, Is.EqualTo(1));
                Assert.That(attributes.Mods, Is.Empty);
            });
        }

        [Test]
        public void TestAnalyzerCalculatesWeightedDensityWindows()
        {
            var beatmap = createPlayableBeatmap(
                BmsKeymode.Key7K,
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 0, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 500, EndTime = 1500, LaneIndex = 2 },
                new BmsHitObject { StartTime = 1500, LaneIndex = 3 });

            var analysis = new BmsNoteDensityAnalyzer().Analyze(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(analysis.TotalNoteCount, Is.EqualTo(4));
                Assert.That(analysis.ScratchNoteCount, Is.EqualTo(1));
                Assert.That(analysis.LnNoteCount, Is.EqualTo(1));
                Assert.That(analysis.Windows, Has.Count.EqualTo(4));
                Assert.That(analysis.Windows[0].WeightedNoteCount, Is.EqualTo(4.8).Within(0.001));
                Assert.That(analysis.Windows[0].NormalCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[0].ScratchCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[0].LnCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[1].WeightedNoteCount, Is.EqualTo(2.0).Within(0.001));
                Assert.That(analysis.Windows[1].NormalCount, Is.EqualTo(0));
                Assert.That(analysis.Windows[1].ScratchCount, Is.EqualTo(0));
                Assert.That(analysis.Windows[1].LnCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[2].WeightedNoteCount, Is.EqualTo(1.0).Within(0.001));
                Assert.That(analysis.Windows[2].NormalCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[2].ScratchCount, Is.EqualTo(0));
                Assert.That(analysis.Windows[2].LnCount, Is.EqualTo(0));
                Assert.That(analysis.Windows[3].WeightedNoteCount, Is.EqualTo(1.0).Within(0.001));
                Assert.That(analysis.Windows[3].NormalCount, Is.EqualTo(1));
                Assert.That(analysis.Windows[3].ScratchCount, Is.EqualTo(0));
                Assert.That(analysis.Windows[3].LnCount, Is.EqualTo(0));
                Assert.That(analysis.PeakDensityNps, Is.EqualTo(4.8).Within(0.001));
                Assert.That(analysis.PeakDensityMs, Is.EqualTo(0).Within(0.001));
                Assert.That(analysis.Percentile95DensityNps, Is.EqualTo(4.38).Within(0.001));
            });
        }

        private static BmsBeatmap createPlayableBeatmap(BmsKeymode keymode, params BmsHitObject[] hitObjects)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo
                {
                    Keymode = keymode,
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
