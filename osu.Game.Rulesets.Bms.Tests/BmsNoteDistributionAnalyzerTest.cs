// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.SongSelect;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsNoteDistributionAnalyzerTest
    {
        [Test]
        public void TestAnalyzerCalculatesWeightedDistributionWindows()
        {
            var beatmap = createPlayableBeatmap(
                BmsKeymode.Key7K,
                new BmsHitObject { StartTime = 0, LaneIndex = 1 },
                new BmsHitObject { StartTime = 0, LaneIndex = 0, IsScratch = true },
                new BmsHoldNote { StartTime = 500, EndTime = 1500, LaneIndex = 2 },
                new BmsHitObject { StartTime = 1500, LaneIndex = 3 });

            var analysis = new BmsNoteDistributionAnalyzer().Analyze(beatmap);

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
    }
}