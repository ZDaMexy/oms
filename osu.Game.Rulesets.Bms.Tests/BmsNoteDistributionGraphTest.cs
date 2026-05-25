// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsNoteDistributionGraphTest
    {
        [Test]
        public void TestBuildSummaryLinesIncludesChartCreditAndInternalLevel()
        {
            var lines = BmsNoteDistributionGraph.BuildSummaryLines(
                new BmsChartMetadata
                {
                    Subtitle = "Extra Stage",
                    SubArtist = "obj: Test Charter",
                    PlayLevel = "12",
                    HeaderDifficulty = 4,
                },
                null,
                Array.Empty<BmsDifficultyTableEntry>());

            Assert.That(lines, Is.EqualTo(new[]
            {
                "Chart by: Test Charter",
                "Internal level: Another 12",
                "Subtitle: Extra Stage",
                "Table: Unrated",
            }));
        }

        [Test]
        public void TestBuildDifficultyTableSummaryLinesReturnsUnratedWhenNoEntries()
        {
            Assert.That(BmsNoteDistributionGraph.BuildDifficultyTableSummaryLines(Array.Empty<BmsDifficultyTableEntry>()),
                Is.EqualTo(new[] { "Table: Unrated" }));
        }

        [Test]
        public void TestBuildDifficultyTableSummaryLinesGroupsLevelsPerTable()
        {
            var lines = BmsNoteDistributionGraph.BuildDifficultyTableSummaryLines(new[]
            {
                new BmsDifficultyTableEntry("Stella", "☆", 3, "☆3", "c", 1),
                new BmsDifficultyTableEntry("Satellite", "★", 2, "★2", "b", 0),
                new BmsDifficultyTableEntry("Satellite", "★", 1, "★1", "a", 0),
                new BmsDifficultyTableEntry("Satellite", "★", 1, "★1", "d", 0),
            });

            Assert.That(lines, Is.EqualTo(new[]
            {
                "Table: Satellite (★1, ★2)",
                "Table: Stella (☆3)",
            }));
        }

        [Test]
        public void TestResolveBeatmapForAnalysisPrefersProjectedWorkingBeatmapSource()
        {
            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata());

            var sourceBeatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            sourceBeatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, Column = 0 });

            var playableBeatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            var workingBeatmap = new TestWorkingBeatmap(sourceBeatmap, playableBeatmap);
            var resolvedBeatmap = BmsNoteDistributionGraph.ResolveBeatmapForAnalysis(workingBeatmap, new BmsRuleset().RulesetInfo, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resolvedBeatmap, Is.SameAs(sourceBeatmap));
                Assert.That(workingBeatmap.PlayableBeatmapRequests, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestResolveBeatmapForAnalysisFallsBackToPlayableBeatmapWhenSourceIsNotProjected()
        {
            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata());

            var sourceBeatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            var playableBeatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
            };

            playableBeatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, Column = 0 });

            var workingBeatmap = new TestWorkingBeatmap(sourceBeatmap, playableBeatmap);
            var resolvedBeatmap = BmsNoteDistributionGraph.ResolveBeatmapForAnalysis(workingBeatmap, new BmsRuleset().RulesetInfo, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resolvedBeatmap, Is.SameAs(playableBeatmap));
                Assert.That(workingBeatmap.PlayableBeatmapRequests, Is.EqualTo(1));
            });
        }

        private sealed class TestWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap sourceBeatmap;
            private readonly IBeatmap playableBeatmap;

            public int PlayableBeatmapRequests { get; private set; }

            public TestWorkingBeatmap(IBeatmap sourceBeatmap, IBeatmap playableBeatmap)
                : base(sourceBeatmap.BeatmapInfo, null)
            {
                this.sourceBeatmap = sourceBeatmap;
                this.playableBeatmap = playableBeatmap;
            }

            protected override IBeatmap GetBeatmap() => sourceBeatmap;

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            {
                PlayableBeatmapRequests++;
                return playableBeatmap;
            }

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
