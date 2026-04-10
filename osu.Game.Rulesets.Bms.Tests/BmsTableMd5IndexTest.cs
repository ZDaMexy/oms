// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsTableMd5IndexTest
    {
        [Test]
        public async Task TestIndexRebuildsWhenManagerDataChanges()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-index-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "satellite-index", "Satellite",
                new TableEntry("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "2"));

            var manager = new BmsDifficultyTableManager(storage);
            var index = new BmsTableMd5Index(manager);

            int rebuildCount = 0;
            index.IndexChanged += () => rebuildCount++;

            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(rebuildCount, Is.EqualTo(1));
                Assert.That(index.GetEntries("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★2" }));
            });

            manager.SetSourceEnabled(imported.ID, false);

            Assert.Multiple(() =>
            {
                Assert.That(rebuildCount, Is.EqualTo(2));
                Assert.That(index.GetEntries("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), Is.Empty);
            });
        }

        [Test]
        public async Task TestApplyToBeatmapPersistsMetadataPayload()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-index-metadata-{Guid.NewGuid():N}");

            const string matching_md5 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            string tableRoot = createTableMirror(storage, "satellite-metadata", "Satellite",
                new TableEntry(matching_md5, "7"));

            var manager = new BmsDifficultyTableManager(storage);
            var index = new BmsTableMd5Index(manager);

            await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata())
            {
                MD5Hash = matching_md5,
            };

            beatmap.Metadata.SetChartMetadata(new BmsChartMetadata
            {
                Subtitle = "Extra Stage",
                PlayLevel = "12",
                HeaderDifficulty = 4,
            });

            bool changed = index.ApplyTo(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.True);
                Assert.That(beatmap.Metadata.RulesetDataJson, Is.Not.Empty);
                Assert.That(beatmap.Metadata.GetDifficultyTableEntries().Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★7" }));
                Assert.That(beatmap.Metadata.GetChartMetadata(), Is.Not.Null);
                Assert.That(beatmap.Metadata.GetChartMetadata()!.Subtitle, Is.EqualTo("Extra Stage"));
                Assert.That(beatmap.Metadata.GetChartMetadata()!.GetInternalLevelDisplay(), Is.EqualTo("Another 12"));
            });
        }

        private static string createTableMirror(Storage storage, string directoryName, string displayName, params TableEntry[] entries)
        {
            string tableRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tableRoot);

            File.WriteAllText(Path.Combine(tableRoot, "index.html"), "<html><head><meta name=\"bmstable\" content=\"header.json\"></head><body></body></html>");
            File.WriteAllText(Path.Combine(tableRoot, "header.json"),
                $"{{\"name\":\"{displayName}\",\"symbol\":\"★\",\"data_url\":\"score.json\"}}");
            File.WriteAllText(Path.Combine(tableRoot, "score.json"),
                "[" + string.Join(",", entries.Select(entry => $"{{\"md5\":\"{entry.Md5}\",\"level\":\"{entry.Level}\"}}")) + "]");

            return tableRoot;
        }

        private readonly record struct TableEntry(string Md5, string Level);
    }
}
