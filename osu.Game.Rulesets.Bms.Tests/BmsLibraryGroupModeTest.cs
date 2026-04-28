// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Graphics.Carousel;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsLibraryGroupModeTest
    {
        private readonly RulesetInfo rulesetInfo = new BmsRuleset().RulesetInfo;

        [Test]
        public async Task TestInternalLibraryGroupingUsesManagedParentsAndRootFallback()
        {
            var nested = createManagedBeatmap("Nested", "chartbms/packs/stage-a/set-nested");
            var rootLevel = createManagedBeatmap("Root Level", "chartbms/root-set");

            var results = await runGrouping(GroupMode.InternalLibrary, rootLevel, nested).ConfigureAwait(false);
            var groups = results.Select(item => item.Model).OfType<GroupDefinition>().ToArray();
            var groupedBeatmaps = results.Select(item => item.Model).OfType<GroupedBeatmap>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(groups.Select(group => group.Title.ToString()), Is.EqualTo(new[] { "packs", "stage-a", "Internal Library" }));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == nested).Group!.Title.ToString(), Is.EqualTo("stage-a"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == nested).Group!.Parent!.Title.ToString(), Is.EqualTo("packs"));
                Assert.That(groupedBeatmaps.Single(grouped => grouped.Beatmap == rootLevel).Group!.Title.ToString(), Is.EqualTo("Internal Library"));
            });
        }

        [Test]
        public async Task TestExternalLibraryGroupingUsesRootAndRelativeParents()
        {
            string rootPath = Path.Combine(Path.GetTempPath(), "RootA");
            string setPath = Path.Combine(rootPath, "packs", "folder", "set-a");
            var beatmap = createExternalBeatmap("External Nested", setPath, rootPath);

            var results = await runGrouping(GroupMode.ExternalLibrary, beatmap).ConfigureAwait(false);
            var groups = results.Select(item => item.Model).OfType<GroupDefinition>().ToArray();
            var groupedBeatmap = results.Select(item => item.Model).OfType<GroupedBeatmap>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(groups.Select(group => group.Title.ToString()), Is.EqualTo(new[] { "RootA", "packs", "folder" }));
                Assert.That(groupedBeatmap.Group!.Title.ToString(), Is.EqualTo("folder"));
                Assert.That(groupedBeatmap.Group!.Parent!.Title.ToString(), Is.EqualTo("packs"));
                Assert.That(groupedBeatmap.Group!.Parent!.Parent!.Title.ToString(), Is.EqualTo("RootA"));
            });
        }

        [Test]
        public async Task TestExternalLibraryGroupingUsesRootGroupForRootLevelSet()
        {
            string rootPath = Path.Combine(Path.GetTempPath(), "RootLevelExternal");
            string setPath = Path.Combine(rootPath, "set-a");
            var beatmap = createExternalBeatmap("External Root Level", setPath, rootPath);

            var results = await runGrouping(GroupMode.ExternalLibrary, beatmap).ConfigureAwait(false);
            var group = results.Select(item => item.Model).OfType<GroupDefinition>().Single();
            var groupedBeatmap = results.Select(item => item.Model).OfType<GroupedBeatmap>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(group.Title.ToString(), Is.EqualTo("RootLevelExternal"));
                Assert.That(groupedBeatmap.Group, Is.SameAs(group));
            });
        }

        [Test]
        public async Task TestExternalLibraryGroupingKeepsDuplicateRootDisplayNamesDistinct()
        {
            var first = createExternalBeatmap("First", @"C:\Songs\set-a", @"C:\Songs");
            var second = createExternalBeatmap("Second", @"D:\Songs\set-b", @"D:\Songs");

            var results = await runGrouping(GroupMode.ExternalLibrary, first, second).ConfigureAwait(false);
            var rootGroups = results.Select(item => item.Model).OfType<GroupDefinition>().Where(group => group.Depth == 0).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(rootGroups.Length, Is.EqualTo(2));
                Assert.That(rootGroups.All(group => group.Title.ToString() == "Songs"), Is.True);
                Assert.That(rootGroups.Distinct().Count(), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task TestExternalLibraryGroupingFallsBackWhenRootSnapshotMissing()
        {
            var beatmap = createExternalBeatmap("Legacy External", @"C:\LegacyRoot\set-a", null);

            var results = await runGrouping(GroupMode.ExternalLibrary, beatmap).ConfigureAwait(false);
            var group = results.Select(item => item.Model).OfType<GroupDefinition>().Single();

            Assert.That(group.Title.ToString(), Is.EqualTo("Unmapped External Library"));
        }

        [Test]
        public async Task TestExternalLibraryGroupingFallsBackWhenRootSnapshotDoesNotContainSet()
        {
            var beatmap = createExternalBeatmap("Moved External", @"C:\LibraryA\set-a", @"C:\LibraryB");

            var results = await runGrouping(GroupMode.ExternalLibrary, beatmap).ConfigureAwait(false);
            var group = results.Select(item => item.Model).OfType<GroupDefinition>().Single();

            Assert.That(group.Title.ToString(), Is.EqualTo("Unmapped External Library"));
        }

        private async Task<List<CarouselItem>> runGrouping(GroupMode mode, params BeatmapInfo[] beatmaps)
        {
            var criteria = new FilterCriteria
            {
                Group = mode,
                Sort = SortMode.Difficulty,
                Ruleset = rulesetInfo,
            };

            var sortingFilter = new BeatmapCarouselFilterSorting(() => criteria);
            var sortedItems = await sortingFilter.Run(beatmaps.Select(beatmap => new CarouselItem(beatmap)).ToList(), CancellationToken.None).ConfigureAwait(false);

            var groupingFilter = new BeatmapCarouselFilterGrouping
            {
                GetCriteria = () => criteria,
                GetCollections = () => new List<BeatmapCollection>(),
                GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                GetFavouriteBeatmapSets = () => new HashSet<int>(),
            };

            return await groupingFilter.Run(sortedItems, CancellationToken.None).ConfigureAwait(false);
        }

        private BeatmapInfo createManagedBeatmap(string title, string filesystemStoragePath)
            => createBeatmap(title, filesystemStoragePath, false, null);

        private BeatmapInfo createExternalBeatmap(string title, string filesystemStoragePath, string? externalRootPath)
            => createBeatmap(title, filesystemStoragePath, true, externalRootPath);

        private BeatmapInfo createBeatmap(string title, string filesystemStoragePath, bool isExternal, string? externalRootPath)
        {
            var beatmap = new BeatmapInfo(rulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata
            {
                Title = title,
                Artist = title,
            })
            {
                DifficultyName = title,
                StarRating = 1.5,
                MD5Hash = $"{title}-{Guid.NewGuid():N}".ToLowerInvariant(),
            };

            var beatmapSet = new BeatmapSetInfo
            {
                FilesystemStoragePath = filesystemStoragePath,
                IsExternalFilesystemStorage = isExternal,
                ExternalLibraryRootPath = externalRootPath,
            };

            beatmapSet.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = beatmapSet;

            return beatmap;
        }
    }
}
