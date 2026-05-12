// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Graphics.Textures;
using osu.Framework.Audio.Track;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsChartFilterStatsBackfillTest
    {
        [Test]
        public void TestComputeForWorkingBeatmapFallsBackToPlayableBeatmap()
        {
            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata());

            var sourceBeatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            var playableBeatmap = new BmsBeatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            playableBeatmap.ControlPointInfo.Add(0, new TimingControlPoint());
            playableBeatmap.HitObjects.AddRange(new BmsHitObject[]
            {
                new BmsHitObject { StartTime = 0, Column = 0 },
                new BmsHoldNote { StartTime = 100, Duration = 200, Column = 1 },
                new BmsHitObject { StartTime = 200, Column = 7, IsScratch = true },
            });

            var stats = BmsChartFilterStatsBackfill.ComputeForWorkingBeatmap(new TestWorkingBeatmap(sourceBeatmap, playableBeatmap));

            Assert.That(stats, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(stats!.TotalPlayableObjectCount, Is.EqualTo(3));
                Assert.That(stats.RegularNoteCount, Is.EqualTo(1));
                Assert.That(stats.LongNoteCount, Is.EqualTo(1));
                Assert.That(stats.ScratchNoteCount, Is.EqualTo(1));
            });
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
