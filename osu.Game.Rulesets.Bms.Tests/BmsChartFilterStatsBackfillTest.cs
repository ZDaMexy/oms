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
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsChartFilterStatsBackfillTest
    {
        [Test]
        public void TestResolveEmptyStatsPersistsResolvedMarkerWithoutStats()
        {
            var metadata = new BeatmapMetadata();

            // An empty/unavailable computation result. ResolveChartFilterStats must still leave a durable trace so the
            // backfill does not reclassify this beatmap as "missing" and re-process it on the next launch.
            metadata.ResolveChartFilterStats(null);

            // Round-trip through the persisted JSON column to simulate a fresh launch reading from realm.
            var reloaded = new BeatmapMetadata { RulesetDataJson = metadata.RulesetDataJson };

            var (stats, resolved) = reloaded.GetChartFilterStatsState();

            Assert.Multiple(() =>
            {
                Assert.That(stats, Is.Null, "an empty computation must not fabricate stats");
                Assert.That(resolved, Is.True, "the beatmap must read back as already-processed so Phase 2 skips it");
                Assert.That(reloaded.RulesetDataJson, Does.Contain("chart_filter_stats_resolved"));
            });
        }

        [Test]
        public void TestUnprocessedMetadataReadsAsNotResolved()
        {
            var (stats, resolved) = new BeatmapMetadata().GetChartFilterStatsState();

            Assert.Multiple(() =>
            {
                Assert.That(stats, Is.Null);
                Assert.That(resolved, Is.False, "a never-processed beatmap must remain in the Phase 2 missing set");
            });
        }

        [Test]
        public void TestResolveStatsPersistsStatsAndResolvedMarker()
        {
            var metadata = new BeatmapMetadata();

            metadata.ResolveChartFilterStats(new BmsChartFilterStats
            {
                TotalPlayableObjectCount = 10,
                RegularNoteCount = 6,
                LongNoteCount = 2,
                ScratchNoteCount = 2,
            });

            var reloaded = new BeatmapMetadata { RulesetDataJson = metadata.RulesetDataJson };
            var (stats, resolved) = reloaded.GetChartFilterStatsState();

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.True);
                Assert.That(stats, Is.Not.Null);
                Assert.That(stats!.RegularNoteCount, Is.EqualTo(6));
            });
        }

        [Test]
        public void TestResolvingEmptyStatsPreservesSharedConvertedStarColumn()
        {
            // The resolved marker shares BeatmapMetadata.RulesetData with the converted-star payload. Writing it must
            // not wipe converted_star_ratings (and the round-trip back must keep the resolved marker).
            var metadata = new BeatmapMetadata
            {
                RulesetDataJson = "{\"converted_star_ratings\":{\"mania\":{\"star_rating\":5.5,\"difficulty_version\":1,\"conversion_version\":20260526,\"failed\":false}}}",
            };

            metadata.ResolveChartFilterStats(null);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.RulesetDataJson, Does.Contain("converted_star_ratings"), "converted star data must survive a resolved-marker write");
                Assert.That(metadata.RulesetDataJson, Does.Contain("chart_filter_stats_resolved"));
                Assert.That(metadata.GetChartFilterStatsState().Resolved, Is.True);
            });
        }

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
