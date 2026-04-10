// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.SongSelect;

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
    }
}
