// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsImportIntegrationTest
    {
        private readonly BmsBeatmapLoader loader = new BmsBeatmapLoader();
        private readonly BmsArchiveReader archiveReader = new BmsArchiveReader();

        [Test]
        public void TestLoaderBuildsDecodedBeatmapWithPopulatedMetadata()
        {
            const string text = @"
#TITLE Example Song
#SUBARTIST obj: OMS Charter
#ARTIST Test Artist
#BPM 150
#PLAYLEVEL 12
#DIFFICULTY 4
#STAGEFILE stage.png
#00111:AA00
";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var placeholderBeatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone());

            Assert.That(loader.CanLoad(placeholderBeatmap, "chart.bms"), Is.True);

            var beatmap = loader.Load(stream, "chart.bms", placeholderBeatmap);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap, Is.TypeOf<BmsDecodedBeatmap>());
                Assert.That(beatmap.BeatmapInfo.Ruleset.ShortName, Is.EqualTo(BmsRuleset.SHORT_NAME));
                Assert.That(beatmap.BeatmapInfo.StarRating, Is.EqualTo(12));
                Assert.That(beatmap.BeatmapInfo.Metadata.Title, Is.EqualTo("Example Song"));
                Assert.That(beatmap.BeatmapInfo.Metadata.Artist, Is.EqualTo("Test Artist"));
                Assert.That(beatmap.BeatmapInfo.Metadata.Author.Username, Is.EqualTo("OMS Charter"));
                Assert.That(beatmap.BeatmapInfo.Metadata.BackgroundFile, Is.EqualTo("stage.png"));
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartMetadata(), Is.Not.Null);
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartMetadata()!.PlayLevel, Is.EqualTo("12"));
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartMetadata()!.HeaderDifficulty, Is.EqualTo(4));
                Assert.That(beatmap.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(5));
                Assert.That(beatmap.BeatmapInfo.DifficultyName, Is.EqualTo("Another 12"));
                Assert.That(beatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(1));
                Assert.That(beatmap.BeatmapInfo.EndTimeObjectCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestLoaderDefaultsStarRatingToZeroWhenPlayLevelMissing()
        {
            const string text = @"
#TITLE Example Song
#ARTIST Test Artist
#BPM 150
#00111:AA00
";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var beatmap = loader.Load(stream, "chart.bms", new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()));

            Assert.That(beatmap.BeatmapInfo.StarRating, Is.Zero);
        }

        [TestCase("#00112:AA00\n#00116:BB00\n", "chart.bms", 5)]
        [TestCase("#00111:AA00\n", "chart.bms", 5)]
        [TestCase("#00119:AA00\n", "chart.bme", 7)]
        [TestCase("#00111:AA00\n#00112:BB00\n#00113:CC00\n#00114:DD00\n#00115:EE00\n#00116:FF00\n#00117:GG00\n#00118:HH00\n#00119:II00\n", "chart.bms", 9)]
        [TestCase("#00122:AA00\n", "chart.bme", 14)]
        [TestCase("#00111:AA00\n", "chart.pms", 9)]
        public void TestLoaderPersistsDetectedKeyCountForSongSelect(string text, string fileName, float expectedKeyCount)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var beatmap = loader.Load(stream, fileName, new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()));

            Assert.That(beatmap.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(expectedKeyCount));
        }

        [Test]
        public void TestArchiveReaderGroupsBeatmapsByContainingFolder()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "oms-bms-reader", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(tempRoot, "Alpha"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "Beta"));

            try
            {
                File.WriteAllText(Path.Combine(tempRoot, "Alpha", "alpha.bms"), "#TITLE Alpha\n#00111:AA00\n");
                File.WriteAllText(Path.Combine(tempRoot, "Beta", "beta.bme"), "#TITLE Beta\n#00111:AA00\n");

                using var prepared = archiveReader.Prepare(new ImportTask(tempRoot));

                var importPaths = prepared.FolderTasks.Select(t => Path.GetFullPath(t.Path)).OrderBy(path => path).ToArray();

                Assert.That(importPaths, Is.EqualTo(new[]
                {
                    Path.GetFullPath(Path.Combine(tempRoot, "Alpha")),
                    Path.GetFullPath(Path.Combine(tempRoot, "Beta")),
                }));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void TestArchiveReaderExtractsZipIntoGroupedFolderTasks()
        {
            using var archiveStream = new MemoryStream();

            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                using (var entryStream = archive.CreateEntry("PackA/song1/chart1.bms").Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false))
                    writer.Write("#TITLE One\n#00111:AA00\n");

                using (var entryStream = archive.CreateEntry("PackB/song2/chart2.bms").Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false))
                    writer.Write("#TITLE Two\n#00111:AA00\n");
            }

            archiveStream.Position = 0;

            string? cleanupPath;

            using (var prepared = archiveReader.Prepare(new ImportTask(archiveStream, "charts.zip")))
            {
                cleanupPath = prepared.CleanupPath;

                var importPaths = prepared.FolderTasks.Select(t => Path.GetFileName(Path.GetFullPath(t.Path))).OrderBy(path => path).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(importPaths, Is.EqualTo(new[] { "song1", "song2" }));
                    Assert.That(cleanupPath, Is.Not.Null.And.Not.Empty);
                    Assert.That(Directory.Exists(cleanupPath!), Is.True);
                });
            }

            Assert.That(cleanupPath, Is.Not.Null);
            Assert.That(Directory.Exists(cleanupPath!), Is.False);
        }

        [Test]
        public async Task TestFolderImporterStoresBeatmapsInSongsDirectory()
        {
            using var storage = new TemporaryNativeStorage($"bms-folder-import-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "filesystem-storage");
            var importer = new BmsFolderImporter(storage, realm);

            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
            Assert.That(result.SkippedBeatmapFiles, Is.Empty);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.Files.Count, Is.EqualTo(0));
                    Assert.That(set.FilesystemStoragePath, Is.Not.Null.And.StartsWith($"{BmsFolderImporter.SONGS_STORAGE_PATH}/"));
                    Assert.That(storage.Exists(Path.Combine(set.FilesystemStoragePath!, "chart.bms")), Is.True);
                    Assert.That(storage.Exists(Path.Combine(set.FilesystemStoragePath!, "stage.png")), Is.True);
                    Assert.That(set.Beatmaps.Single().Difficulty.CircleSize, Is.EqualTo(5));
                    Assert.That(set.Beatmaps.Single().Path, Is.EqualTo("chart.bms"));
                });
            });
        }

        [Test]
        public async Task TestBeatmapImporterImportsBeatmapStreamTask()
        {
            using var storage = new TemporaryNativeStorage($"bms-beatmap-import-stream-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            using var beatmapStream = new MemoryStream(Encoding.UTF8.GetBytes(@"
#TITLE Stream Import
#ARTIST OMS
#BPM 150
#00111:AA00
"));

            var importer = new BmsBeatmapImporter(storage, realm);
            var notifications = new List<Notification>();
            importer.PostNotification = notifications.Add;

            await importer.Import(new[] { new ImportTask(beatmapStream, "stream-chart.bms") }).ConfigureAwait(false);

            var progress = notifications.OfType<ProgressNotification>().Single();

            BeatmapSetInfo? importedSet = realm.Run(r => r.All<BeatmapSetInfo>()
                                                     .Where(set => !set.DeletePending)
                                                     .SingleOrDefault()
                                                     ?.Detach());

            Assert.That(importedSet, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(progress.State, Is.EqualTo(ProgressNotificationState.Completed));
                Assert.That(progress.CompletionText.ToString(), Is.EqualTo("Imported 1 BMS set!"));
                Assert.That(notifications.OfType<SimpleErrorNotification>(), Is.Empty);
                Assert.That(notifications.OfType<SimpleNotification>().Any(notification => notification is not SimpleErrorNotification), Is.False);
                Assert.That(importedSet!.FilesystemStoragePath, Is.Not.Null.And.StartsWith($"{BmsFolderImporter.SONGS_STORAGE_PATH}/"));
                Assert.That(storage.Exists(Path.Combine(importedSet.FilesystemStoragePath!, "stream-chart.bms")), Is.True);
                Assert.That(importedSet.Beatmaps.Single().Path, Is.EqualTo("stream-chart.bms"));
            });
        }

        [Test]
        public async Task TestImportedBeatmapCanBeReloadedFromSongsDirectory()
        {
            using var storage = new TemporaryNativeStorage($"bms-working-beatmap-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "working-beatmap");
            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            string? filesystemStoragePath = null;
            BeatmapInfo? importedBeatmap = null;

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                filesystemStoragePath = set.FilesystemStoragePath;
                importedBeatmap = set.Beatmaps.Single().Detach();
            });

            Assert.That(filesystemStoragePath, Is.Not.Null.And.Not.Empty);
            Assert.That(importedBeatmap, Is.Not.Null);

            using var stream = storage.GetStream(Path.Combine(filesystemStoragePath!, importedBeatmap!.Path!));
            var reloadedBeatmap = loader.Load(stream, importedBeatmap.Path!, importedBeatmap);

            Assert.That(reloadedBeatmap, Is.TypeOf<BmsDecodedBeatmap>());

            Assert.Multiple(() =>
            {
                Assert.That(reloadedBeatmap.BeatmapInfo.Metadata.Title, Is.EqualTo("Filesystem Test"));
                Assert.That(reloadedBeatmap.BeatmapInfo.Metadata.BackgroundFile, Is.EqualTo("stage.png"));
                Assert.That(reloadedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestExternalDirectoryRegistrationUsesSourceDirectoryReadOnly()
        {
            using var storage = new TemporaryNativeStorage($"bms-external-readonly-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "external-readonly");
            var importer = new BmsFolderImporter(storage, realm);

            var result = await importer.RegisterExternalDirectory(importRoot).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.Files.Count, Is.EqualTo(0));
                    Assert.That(set.IsExternalFilesystemStorage, Is.True);
                    Assert.That(set.FilesystemStoragePath, Is.EqualTo(Path.GetFullPath(importRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                    Assert.That(set.Beatmaps.Single().Path, Is.EqualTo("chart.bms"));
                });
            });

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(importRoot), Is.True);
                Assert.That(File.Exists(Path.Combine(importRoot, "chart.bms")), Is.True);
                Assert.That(File.Exists(Path.Combine(importRoot, "stage.png")), Is.True);
            });
        }

        [Test]
        public async Task TestManagedDirectoryRegistrationPreservesRelativeManagedPath()
        {
            using var storage = new TemporaryNativeStorage($"bms-managed-readonly-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string managedRoot = Path.Combine(storage.GetFullPath(BmsFolderImporter.SONGS_STORAGE_PATH), "packs", "managed-set");
            Directory.CreateDirectory(managedRoot);
            File.WriteAllText(Path.Combine(managedRoot, "chart.bms"), buildChartText());
            File.WriteAllBytes(Path.Combine(managedRoot, "stage.png"), new byte[] { 1, 2, 3, 4 });

            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.RegisterManagedDirectory(managedRoot).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.Files.Count, Is.EqualTo(0));
                    Assert.That(set.IsExternalFilesystemStorage, Is.False);
                    Assert.That(set.FilesystemStoragePath, Is.EqualTo("chartbms/packs/managed-set"));
                    Assert.That(set.Beatmaps.Single().Path, Is.EqualTo("chart.bms"));
                });
            });

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(managedRoot), Is.True);
                Assert.That(File.Exists(Path.Combine(managedRoot, "chart.bms")), Is.True);
                Assert.That(File.Exists(Path.Combine(managedRoot, "stage.png")), Is.True);
            });
        }

        [Test]
        public async Task TestDeletingExternalRegistrationDoesNotDeleteSourceDirectory()
        {
            using var storage = new TemporaryNativeStorage($"bms-external-delete-{Guid.NewGuid():N}");

            string importRoot = createImportSource(storage, "external-delete");
            Guid setId;

            using (var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                using var rulesets = new RealmRulesetStore(realm, storage);

                var importer = new BmsFolderImporter(storage, realm);
                var result = await importer.RegisterExternalDirectory(importRoot).ConfigureAwait(false);

                Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
                setId = result.ImportedBeatmapSet!.PerformRead(set => set.ID);

                realm.Run(r =>
                {
                    using var transaction = r.BeginWrite();
                    r.Find<BeatmapSetInfo>(setId)!.DeletePending = true;
                    transaction.Commit();
                });
            }

            using (var reopenedRealm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                Assert.That(reopenedRealm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(0));
            }

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(importRoot), Is.True);
                Assert.That(File.Exists(Path.Combine(importRoot, "chart.bms")), Is.True);
                Assert.That(File.Exists(Path.Combine(importRoot, "stage.png")), Is.True);
            });
        }

        [Test]
        public async Task TestFolderImporterAppliesDifficultyTableMatchesDuringImport()
        {
            using var storage = new TemporaryNativeStorage($"bms-folder-import-table-match-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "table-match-import");
            string chartMd5 = computeFileMd5(Path.Combine(importRoot, "chart.bms"));

            var importer = new BmsFolderImporter(storage, realm);
            string tableRoot = createTableMirror(storage, "satellite-import", "Satellite", new TableEntry(chartMd5, "4"));

            await importer.DifficultyTableManager.ImportFromPath(tableRoot).ConfigureAwait(false);

            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                var entries = set.Beatmaps.Single().Metadata.GetDifficultyTableEntries();

                Assert.Multiple(() =>
                {
                    Assert.That(entries.Select(entry => entry.TableName), Is.EqualTo(new[] { "Satellite" }));
                    Assert.That(entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★4" }));
                });
            });
        }

        [Test]
        public async Task TestBeatmapImporterPostsSkippedFileWarningForDuplicateBeatmapsInArchive()
        {
            using var storage = new TemporaryNativeStorage($"bms-importer-duplicate-archive-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            using var archiveStream = createArchiveStream(("Pack/song/a.bms", "#TITLE A\n#00111:AA00\n"), ("Pack/song/b.bms", "#TITLE A\n#00111:AA00\n"));

            var importer = new BmsBeatmapImporter(storage, realm);
            var notifications = new List<Notification>();
            importer.PostNotification = notifications.Add;

            await importer.Import(new[] { new ImportTask(archiveStream, "duplicate.zip") }).ConfigureAwait(false);

            var progress = notifications.OfType<ProgressNotification>().Single();
            var warning = notifications.OfType<SimpleNotification>().Single(notification => notification is not SimpleErrorNotification);

            Assert.Multiple(() =>
            {
                Assert.That(progress.State, Is.EqualTo(ProgressNotificationState.Completed));
                Assert.That(progress.CompletionText.ToString(), Is.EqualTo("Imported 1 BMS set!"));
                Assert.That(warning.Text.ToString(), Does.StartWith("Imported with warnings. Skipped BMS files:"));
                Assert.That(warning.Text.ToString(), Does.Contain("b.bms"));
                Assert.That(notifications.OfType<SimpleErrorNotification>(), Is.Empty);
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(set => !set.DeletePending)), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestBeatmapImporterPostsErrorNotificationWhenArchiveHasNoValidBeatmaps()
        {
            using var storage = new TemporaryNativeStorage($"bms-importer-empty-archive-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            using var archiveStream = createArchiveStream(("Pack/readme.txt", "no beatmaps here"));

            var importer = new BmsBeatmapImporter(storage, realm);
            var notifications = new List<Notification>();
            importer.PostNotification = notifications.Add;

            await importer.Import(new[] { new ImportTask(archiveStream, "empty.zip") }).ConfigureAwait(false);

            var progress = notifications.OfType<ProgressNotification>().Single();
            var error = notifications.OfType<SimpleErrorNotification>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(progress.State, Is.EqualTo(ProgressNotificationState.Cancelled));
                Assert.That(progress.Text.ToString(), Is.EqualTo("BMS import failed! Check logs for more information."));
                Assert.That(error.Text.ToString(), Is.EqualTo("Import failed: no valid BMS files found in archive."));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(set => !set.DeletePending)), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestDifficultyTableRefreshUpdatesPersistedImportedBeatmaps()
        {
            using var storage = new TemporaryNativeStorage($"bms-folder-import-table-refresh-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "table-refresh-import");
            string chartMd5 = computeFileMd5(Path.Combine(importRoot, "chart.bms"));

            var importer = new BmsFolderImporter(storage, realm);
            string tableRoot = createTableMirror(storage, "satellite-refresh", "Satellite", new TableEntry("ffffffffffffffffffffffffffffffff", "2"));

            var source = await importer.DifficultyTableManager.ImportFromPath(tableRoot).ConfigureAwait(false);
            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set => Assert.That(set.Beatmaps.Single().Metadata.GetDifficultyTableEntries(), Is.Empty));

            overwriteTableEntries(tableRoot,
                new TableEntry("ffffffffffffffffffffffffffffffff", "2"),
                new TableEntry(chartMd5, "9"));

            await importer.DifficultyTableManager.RefreshTable(source.ID).ConfigureAwait(false);

            result.ImportedBeatmapSet.PerformRead(set =>
            {
                var entries = set.Beatmaps.Single().Metadata.GetDifficultyTableEntries();

                Assert.Multiple(() =>
                {
                    Assert.That(entries.Select(entry => entry.TableName), Is.EqualTo(new[] { "Satellite" }));
                    Assert.That(entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
                });
            });
        }

        private static string createImportSource(Storage storage, string directoryName)
        {
            string sourceRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(sourceRoot);
            File.WriteAllText(Path.Combine(sourceRoot, "chart.bms"), @"
#TITLE Filesystem Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#STAGEFILE stage.png
#00111:AA00
");
            File.WriteAllText(Path.Combine(sourceRoot, "stage.png"), "placeholder");

            return sourceRoot;
        }

        private static MemoryStream createArchiveStream(params (string path, string content)[] entries)
        {
            var archiveStream = new MemoryStream();

            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                foreach (var (path, content) in entries)
                {
                    using var entryStream = archive.CreateEntry(path).Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
                    writer.Write(content);
                }
            }

            archiveStream.Position = 0;
            return archiveStream;
        }

        private static string createTableMirror(Storage storage, string directoryName, string displayName, params TableEntry[] entries)
        {
            string tableRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tableRoot);

            File.WriteAllText(Path.Combine(tableRoot, "index.html"), "<html><head><meta name=\"bmstable\" content=\"header.json\"></head><body></body></html>");
            File.WriteAllText(Path.Combine(tableRoot, "header.json"),
                $"{{\"name\":\"{displayName}\",\"symbol\":\"★\",\"data_url\":\"score.json\"}}");

            overwriteTableEntries(tableRoot, entries);
            return tableRoot;
        }

        private static void overwriteTableEntries(string tableRoot, params TableEntry[] entries)
            => File.WriteAllText(Path.Combine(tableRoot, "score.json"),
                "[" + string.Join(",", entries.Select(entry => $"{{\"md5\":\"{entry.Md5}\",\"level\":\"{entry.Level}\"}}")) + "]");

        private static string computeFileMd5(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return stream.ComputeMD5Hash();
        }

        private readonly record struct TableEntry(string Md5, string Level);
    }
}
