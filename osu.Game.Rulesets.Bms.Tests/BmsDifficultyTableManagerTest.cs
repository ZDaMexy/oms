// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsDifficultyTableManagerTest
    {
        [Test]
        public void TestBundledPresetsAreSeeded()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-presets-{Guid.NewGuid():N}");
            var manager = new BmsDifficultyTableManager(storage);

            var sources = manager.GetSources();

            Assert.Multiple(() =>
            {
                Assert.That(sources.Count(source => source.IsPreset), Is.EqualTo(7));
                Assert.That(sources.Any(source => source.IsPreset && source.SourceName == "satellite" && source.DisplayName == "Satellite"), Is.True);
                Assert.That(sources.Any(source => source.IsPreset && source.SourceName == "stella" && source.DisplayName == "Stella"), Is.True);
            });
        }

        [Test]
        public async Task TestImportDirectoryViaHtmlWrapperPersistsCachedEntriesAcrossRestart()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-import-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "satellite-local", "Satellite Local",
                new TableEntry("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "1"),
                new TableEntry("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "12"));

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.IsPreset, Is.False);
                Assert.That(imported.Enabled, Is.True);
                Assert.That(imported.DisplayName, Is.EqualTo("Satellite Local"));
                Assert.That(imported.Symbol, Is.EqualTo("★"));
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★1", "★12" }));
            });

            var restartedManager = new BmsDifficultyTableManager(storage);
            var restored = restartedManager.GetSources().Single(source => source.ID == imported.ID);

            Assert.Multiple(() =>
            {
                Assert.That(restored.DisplayName, Is.EqualTo(imported.DisplayName));
                Assert.That(restored.Entries.Select(entry => entry.Md5), Is.EqualTo(imported.Entries.Select(entry => entry.Md5)));
                Assert.That(restored.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(imported.Entries.Select(entry => entry.LevelLabel)));
            });
        }

        [Test]
        public async Task TestImportIntoPresetUpdatesExistingPresetSource()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-preset-import-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "satellite-preset", "Satellite",
                new TableEntry("cccccccccccccccccccccccccccccccc", "3"));

            var manager = new BmsDifficultyTableManager(storage);
            var preset = manager.GetSources().Single(source => source.IsPreset && source.SourceName == "satellite");

            var imported = await manager.ImportFromPath(tableRoot, preset.ID).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.ID, Is.EqualTo(preset.ID));
                Assert.That(imported.IsPreset, Is.True);
                Assert.That(imported.Enabled, Is.True);
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★3" }));
                Assert.That(manager.GetSources().Count(source => !source.IsPreset), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestImportMatchingBundledPresetUsesSeededSource()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-preset-match-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "custom-local-path", "Satellite",
                new TableEntry("ffffffffffffffffffffffffffffffff", "5"));

            var manager = new BmsDifficultyTableManager(storage);
            var preset = manager.GetSources().Single(source => source.IsPreset && source.SourceName == "satellite");

            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.ID, Is.EqualTo(preset.ID));
                Assert.That(imported.IsPreset, Is.True);
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★5" }));
                Assert.That(manager.GetSources().Count(source => !source.IsPreset), Is.EqualTo(0));
            });
        }

        [Test]
        public void TestSharedManagerReturnsSameInstanceForSameStorage()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-shared-{Guid.NewGuid():N}");

            var first = BmsDifficultyTableManager.GetShared(storage);
            var second = BmsDifficultyTableManager.GetShared(storage);

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public async Task TestRefreshTableUpdatesEntriesAndEnabledLookupRespectsToggle()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-refresh-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "stella-local", "Stella Local",
                new TableEntry("dddddddddddddddddddddddddddddddd", "2"));

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            int eventCount = 0;
            manager.TableDataChanged += () => eventCount++;

            File.WriteAllText(Path.Combine(tableRoot, "score.json"),
                """
                [
                  { "md5": "dddddddddddddddddddddddddddddddd", "level": "2" },
                  { "md5": "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "level": "9" }
                ]
                """);

            var refreshed = await manager.RefreshTable(imported.ID).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(refreshed.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★2", "★9" }));
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee").Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
            });

            manager.SetSourceEnabled(imported.ID, false);

            Assert.Multiple(() =>
            {
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"), Is.Empty);
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", onlyEnabled: false).Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
                Assert.That(eventCount, Is.EqualTo(2));
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
