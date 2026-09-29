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
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Collections;
using osu.Game.Scoring;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsImportIntegrationTest
    {
        private readonly BmsBeatmapLoader loader = new BmsBeatmapLoader();
        private readonly BmsArchiveReader archiveReader = new BmsArchiveReader();
        private static readonly BmsBeatmapDecoderOptions five_key_contract = new BmsBeatmapDecoderOptions(BmsKeymode.Key5K);

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

            var beatmap = loader.Load(stream, "chart.bms", placeholderBeatmap, five_key_contract);

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
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartFilterStats(), Is.Not.Null);
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartFilterStats()!.TotalPlayableObjectCount, Is.EqualTo(1));
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartFilterStats()!.RegularNoteCount, Is.EqualTo(1));
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartFilterStats()!.LongNoteCount, Is.EqualTo(0));
                Assert.That(beatmap.BeatmapInfo.Metadata.GetChartFilterStats()!.ScratchNoteCount, Is.EqualTo(0));
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

            var beatmap = loader.Load(stream, "chart.bms", new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()), five_key_contract);

            Assert.That(beatmap.BeatmapInfo.StarRating, Is.Zero);
        }

        [Test]
        public void TestLoaderPopulatesTimingDataForSongSelectDisplays()
        {
            const string text = @"
#TITLE Example Song
#ARTIST Test Artist
#BPM 150
#00111:AA00
";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var beatmap = loader.Load(stream, "chart.bms", new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()), five_key_contract);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.ControlPointInfo.TimingPoints, Has.Count.EqualTo(1));
                Assert.That(beatmap.ControlPointInfo.BPMMinimum, Is.EqualTo(150).Within(0.001));
                Assert.That(beatmap.ControlPointInfo.BPMMaximum, Is.EqualTo(150).Within(0.001));
                Assert.That(beatmap.GetMostCommonBeatLength(), Is.EqualTo(400).Within(0.001));
                Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            });
        }

        [TestCase("#00112:AA00\n#00116:BB00\n", "chart.bms", 5, BmsKeymode.Key5K)]
        [TestCase("#00111:AA00\n", "chart.bms", 5, BmsKeymode.Key5K)]
        [TestCase("#00119:AA00\n", "chart.bme", 7, null)]
        [TestCase("#00111:AA00\n#00112:BB00\n#00113:CC00\n#00114:DD00\n#00115:EE00\n#00116:FF00\n#00117:GG00\n#00118:HH00\n#00119:II00\n", "chart.bms", 9, null)]
        [TestCase("#00122:AA00\n", "chart.bme", 14, null)]
        [TestCase("#00111:AA00\n", "chart.pms", 9, null)]
        public void TestLoaderPersistsResolvedKeyCountForSongSelect(string text, string fileName, float expectedKeyCount, BmsKeymode? keymodeOverride)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var options = keymodeOverride.HasValue ? new BmsBeatmapDecoderOptions(keymodeOverride.Value) : null;
            var beatmap = loader.Load(stream, fileName, new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()), options);

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

                string[] importPaths = prepared.FolderTasks.Select(t => Path.GetFullPath(t.Path)).OrderBy(path => path).ToArray();

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

                string[] importPaths = prepared.FolderTasks.Select(t => Path.GetFileName(Path.GetFullPath(t.Path))).OrderBy(path => path).ToArray();

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
#00132:AA00
#00133:AA00
#00134:AA00
#00135:AA00
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
        public async Task TestImportNormalisesStagefileToExistingImageVariant()
        {
            using var storage = new TemporaryNativeStorage($"bms-background-normalise-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = Path.Combine(storage.GetFullPath("."), "background-normalise", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(importRoot);
            File.WriteAllText(Path.Combine(importRoot, "chart.bms"), @"
#TITLE Filesystem Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#STAGEFILE stage.bmp
#00111:AA00
#00132:AA00
#00133:AA00
#00134:AA00
#00135:AA00
");
            File.WriteAllText(Path.Combine(importRoot, "stage.png"), "placeholder");

            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.That(set.Beatmaps.Single().Metadata.BackgroundFile, Is.EqualTo("stage.png"));
            });
        }

        [Test]
        public async Task TestImportNormalisesProjectedBgaBitmapToExistingImageVariant()
        {
            using var storage = new TemporaryNativeStorage($"bms-projected-background-normalise-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = Path.Combine(storage.GetFullPath("."), "projected-background-normalise", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(importRoot);
            File.WriteAllText(Path.Combine(importRoot, "chart.bms"), @"
#TITLE Filesystem Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#BMP01 stage.bmp
#BGA01 01 0 0 255 255 0 0
#00111:AA00
#00132:AA00
#00133:AA00
#00134:AA00
#00135:AA00
");
            File.WriteAllText(Path.Combine(importRoot, "stage.png"), "placeholder");

            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.That(set.Beatmaps.Single().Metadata.BackgroundFile, Is.EqualTo("stage.png"));
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
                    Assert.That(set.ExternalLibraryRootPath, Is.EqualTo(Path.GetFullPath(importRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
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
        public async Task TestExternalDirectoryRegistrationStoresExplicitRootSnapshot()
        {
            using var storage = new TemporaryNativeStorage($"bms-external-root-snapshot-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string rootPath = storage.GetFullPath("external-root");
            string importRoot = Path.Combine(rootPath, "packs", "set-a");
            Directory.CreateDirectory(importRoot);
            File.WriteAllText(Path.Combine(importRoot, "chart.bms"), buildChartText());
            File.WriteAllBytes(Path.Combine(importRoot, "stage.png"), new byte[] { 1, 2, 3, 4 });

            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.RegisterExternalDirectory(importRoot, rootPath).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.IsExternalFilesystemStorage, Is.True);
                    Assert.That(set.FilesystemStoragePath, Is.EqualTo(Path.GetFullPath(importRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                    Assert.That(set.ExternalLibraryRootPath, Is.EqualTo(Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                });
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
        public void TestLightweightChartFilterStatsMatchFullConversion()
        {
            // The backfill fast path counts RC/LN/SCR straight off the decoded chart (no full playable conversion).
            // This locks that it is byte-for-byte equivalent to the authoritative BmsChartFilterStats.FromBeatmap over a
            // fully converted beatmap — the whole optimization is only safe because note classification depends solely
            // on AutoPlay (BGM exclusion) and BmsBeatmapConverter.IsScratchLane, which both paths share.
            const string text = @"
#TITLE Equivalence
#ARTIST OMS
#BPM 150
#LNOBJ ZZ
#00101:CC00
#00111:AA00ZZ00
#00112:DD00
#00116:FF00
";
            using var fullStream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var lightStream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var full = BmsChartFilterStats.FromBeatmap(BmsImportedBeatmapFactory.Create(fullStream, "equivalence.bms", five_key_contract));
            var light = BmsChartFilterStatsBackfill.ComputeFromDecodedChart(BmsImportedBeatmapFactory.DecodeChart(lightStream, "equivalence.bms", five_key_contract));

            Assert.Multiple(() =>
            {
                Assert.That(light, Is.EqualTo(full), "lightweight decode-only count must equal the full-conversion count");
                Assert.That(full.TotalPlayableObjectCount, Is.EqualTo(3), "3 playable objects; the channel-01 BGM must be excluded");
                Assert.That(full.LongNoteCount, Is.EqualTo(1), "the channel-11 LNOBJ pair is one long note");
            });
        }

        [Test]
        public async Task TestChartFilterStatsBackfillQueryDoesNotThrowAgainstRealm()
        {
            // Regression: Phase 1 of the composition-filter stats backfill used to run
            // `r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == "bms")` directly on Realm's IQueryable, which
            // Realm cannot translate ("The left-hand side of the Equal operator must be a direct access to a persisted
            // property"). The exception aborted Phase 1, left the cache empty AND skipped Phase 2 — so the composition
            // filter silently fail-opened across the entire library. This locks the query against a REAL realm.
            using var storage = new TemporaryNativeStorage($"bms-filter-backfill-query-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "filter-backfill-query");
            var importer = new BmsFolderImporter(storage, realm);

            await importer.Import(new ImportTask(importRoot)).ConfigureAwait(false);

            var bmsBeatmaps = realm.Run(r => BmsChartFilterStatsBackfill.EnumerateBmsBeatmaps(r)
                                                                        .Select(b => (b.ID, stats: b.Metadata.GetChartFilterStats()))
                                                                        .ToList());

            Assert.Multiple(() =>
            {
                Assert.That(bmsBeatmaps, Has.Count.EqualTo(1), "the imported BMS beatmap must be visible to the backfill query");
                Assert.That(bmsBeatmaps.Single().stats, Is.Not.Null, "import-time persistence must leave readable chart-filter-stats for Phase 1 to cache");
                Assert.That(bmsBeatmaps.Single().stats!.RegularNoteCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestManagedDirectoryReuseReappliesDifficultyTableMetadata()
        {
            using var storage = new TemporaryNativeStorage($"bms-managed-reuse-table-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string managedRoot = Path.Combine(storage.GetFullPath(BmsFolderImporter.SONGS_STORAGE_PATH), "packs", "managed-reuse-set");
            Directory.CreateDirectory(managedRoot);
            File.WriteAllText(Path.Combine(managedRoot, "chart.bms"), buildChartText());
            File.WriteAllBytes(Path.Combine(managedRoot, "stage.png"), new byte[] { 1, 2, 3, 4 });

            string chartMd5 = computeFileMd5(Path.Combine(managedRoot, "chart.bms"));
            var importer = new BmsFolderImporter(storage, realm);
            string tableRoot = createTableMirror(storage, "satellite-managed-reuse", "Satellite", new TableEntry(chartMd5, "4"));

            await importer.DifficultyTableManager.ImportFromPath(tableRoot).ConfigureAwait(false);

            var firstResult = await importer.RegisterManagedDirectory(managedRoot).ConfigureAwait(false);

            Assert.That(firstResult.ImportedBeatmapSet, Is.Not.Null);

            Guid reusedSetId = firstResult.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.Beatmaps.Single().Metadata.GetDifficultyTableEntries().Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★4" }));
                    Assert.That(set.Beatmaps.Single().Metadata.GetChartFilterStats(), Is.Not.Null);
                    Assert.That(set.Beatmaps.Single().Metadata.GetChartFilterStats()!.RegularNoteCount, Is.EqualTo(1));
                });

                return set.ID;
            });

            realm.Write(r =>
            {
                var beatmap = r.Find<BeatmapSetInfo>(reusedSetId)!.Beatmaps.Single();
                beatmap.Metadata.SetDifficultyTableEntries(Array.Empty<BmsDifficultyTableEntry>());
                beatmap.Metadata.SetChartFilterStats(null);
            });

            var secondResult = await importer.RegisterManagedDirectory(managedRoot).ConfigureAwait(false);

            Assert.That(secondResult.ImportedBeatmapSet, Is.Not.Null);

            secondResult.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.ID, Is.EqualTo(reusedSetId));
                    Assert.That(set.Beatmaps.Single().Metadata.GetDifficultyTableEntries().Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★4" }));
                    Assert.That(set.Beatmaps.Single().Metadata.GetChartFilterStats(), Is.Not.Null);
                    Assert.That(set.Beatmaps.Single().Metadata.GetChartFilterStats()!.TotalPlayableObjectCount, Is.EqualTo(1));
                    Assert.That(set.Beatmaps.Single().Metadata.GetChartFilterStats()!.RegularNoteCount, Is.EqualTo(1));
                });
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
            const string completeFiveKeyChart = "#TITLE A\n#00111:AA00\n#00132:AA00\n#00133:AA00\n#00134:AA00\n#00135:AA00\n";
            using var archiveStream = createArchiveStream(("Pack/song/a.bms", completeFiveKeyChart), ("Pack/song/b.bms", completeFiveKeyChart));

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
        public async Task TestBeatmapImporterPostsParserWarningSummaryForDegradedChart()
        {
            using var storage = new TemporaryNativeStorage($"bms-importer-parser-warning-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            using var archiveStream = createArchiveStream(("Pack/song/chart.bms", @"#TITLE Warning Chart
#RANDOM 2
#IF 2
#00112:AA00
#ENDIF
#ENDRANDOM
#00111:BB00
#00132:AA00
#00133:AA00
#00134:AA00
#00135:AA00
"));

            var importer = new BmsBeatmapImporter(storage, realm);
            var notifications = new List<Notification>();
            importer.PostNotification = notifications.Add;

            await importer.Import(new[] { new ImportTask(archiveStream, "warning.zip") }).ConfigureAwait(false);

            var progress = notifications.OfType<ProgressNotification>().Single();
            var warning = notifications.OfType<SimpleNotification>().Single(notification => notification is not SimpleErrorNotification);

            Assert.Multiple(() =>
            {
                Assert.That(progress.State, Is.EqualTo(ProgressNotificationState.Completed));
                Assert.That(progress.CompletionText.ToString(), Is.EqualTo("Imported 1 BMS set!"));
                Assert.That(warning.Text.ToString(), Does.StartWith("Imported with parser warnings. Charts affected:"));
                Assert.That(warning.Text.ToString(), Does.Contain("chart.bms"));
                Assert.That(warning.Text.ToString(), Does.Contain("(2)"));
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

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestSameContentAtDifferentPathsSurvivesRestart(bool secondIsExternal)
        {
            using var storage = new TemporaryNativeStorage($"bms-path-identity-{Guid.NewGuid():N}");
            string first = createImportSource(storage, "chartbms/first");
            string second = createImportSource(storage, secondIsExternal ? "external" : "chartbms/second");
            Guid firstId;
            Guid secondId;
            using (var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                using var rulesets = new RealmRulesetStore(realm, storage);
                var importer = new BmsFolderImporter(storage, realm);
                var firstResult = await importer.RegisterManagedDirectory(first).ConfigureAwait(false);
                var secondResult = secondIsExternal ? await importer.RegisterExternalDirectory(second).ConfigureAwait(false) : await importer.RegisterManagedDirectory(second).ConfigureAwait(false);
                firstId = firstResult.ImportedBeatmapSet!.PerformRead(s => s.ID);
                secondId = secondResult.ImportedBeatmapSet!.PerformRead(s => s.ID);
                Assert.That(secondId, Is.Not.EqualTo(firstId));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.DeletePending && !s.FilesystemUnavailable)), Is.EqualTo(2));
                var repeated = await importer.RegisterManagedDirectory(first).ConfigureAwait(false);
                Assert.That(repeated.ImportedBeatmapSet!.PerformRead(s => s.ID), Is.EqualTo(firstId));
            }

            using (var reopened = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                Assert.That(reopened.Run(r => r.Find<BeatmapSetInfo>(firstId) != null && r.Find<BeatmapSetInfo>(secondId) != null), Is.True);
                Assert.That(File.Exists(Path.Combine(first, "chart.bms")), Is.True);
                Assert.That(File.Exists(Path.Combine(second, "chart.bms")), Is.True);
            }
        }

        [Test]
        public async Task TestRebuildRetiresRemovedChartAndRestoresOriginalIdentityWithoutDeletingFiles()
        {
            using var storage = new TemporaryNativeStorage($"bms-rebuild-identity-{Guid.NewGuid():N}");
            string directory = createImportSource(storage, "chartbms/set");
            string secondChart = Path.Combine(directory, "second.bms");
            string secondText = buildChartText().Replace("Filesystem Test", "Second difficulty");
            File.WriteAllText(secondChart, secondText);
            Guid originalId;
            Guid originalBeatmapId;
            Guid scoreId;
            Guid collectionId;

            using (var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                using var rulesets = new RealmRulesetStore(realm, storage);
                var importer = new BmsFolderImporter(storage, realm);
                var result = await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
                originalId = result.ImportedBeatmapSet!.PerformRead(s => s.ID);
                originalBeatmapId = result.ImportedBeatmapSet.PerformRead(s => s.Beatmaps[0].ID);
                var ids = realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(originalBeatmapId)!;
                    var score = new ScoreInfo(beatmap, beatmap.Ruleset);
                    var collection = new BeatmapCollection("kept", new[] { beatmap.MD5Hash });
                    r.Add(score);
                    r.Add(collection);
                    return (score.ID, collection.ID);
                });
                scoreId = ids.Item1;
                collectionId = ids.Item2;

                File.Delete(secondChart);
                var changed = await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
                Assert.That(changed.ImportedBeatmapSet!.PerformRead(s => s.Beatmaps.Count), Is.EqualTo(1));
                Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(originalId)!.FilesystemUnavailable), Is.True);
                Assert.That(realm.Run(r => r.Find<ScoreInfo>(scoreId)!.BeatmapInfo!.ID), Is.EqualTo(originalBeatmapId));
                Assert.That(realm.Run(r => r.Find<BeatmapCollection>(collectionId)!.BeatmapMD5Hashes.Count), Is.EqualTo(1));

                File.WriteAllText(secondChart, secondText);
                var restored = await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
                Assert.That(restored.ImportedBeatmapSet!.PerformRead(s => s.ID), Is.EqualTo(originalId));
                Assert.That(restored.ImportedBeatmapSet.PerformRead(s => s.FilesystemUnavailable), Is.False);
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable && !s.DeletePending)), Is.EqualTo(1));
            }

            using (var reopened = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                Assert.That(File.Exists(secondChart), Is.True);
                Assert.That(reopened.Run(r => r.Find<ScoreInfo>(scoreId)!.BeatmapInfo!.ID), Is.EqualTo(originalBeatmapId));
            }
        }

        [Test]
        public async Task TestRebuildUpdatesRenamedChartFilename()
        {
            using var storage = new TemporaryNativeStorage($"bms-rename-{Guid.NewGuid():N}");
            string directory = createImportSource(storage, "chartbms/set");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new BmsFolderImporter(storage, realm);
            var first = await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
            Guid id = first.ImportedBeatmapSet!.PerformRead(s => s.ID);
            File.Move(Path.Combine(directory, "chart.bms"), Path.Combine(directory, "renamed.bms"));
            var second = await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
            Assert.That(second.ImportedBeatmapSet!.PerformRead(s => s.ID), Is.EqualTo(id));
            Assert.That(second.ImportedBeatmapSet.PerformRead(s => s.Beatmaps[0].LocalFilePath), Is.EqualTo("renamed.bms"));
        }

        [Test]
        public async Task TestExternalRebuildMissingRecoveryAndUnregisterPreserveSource()
        {
            using var storage = new TemporaryNativeStorage($"bms-scan-recovery-{Guid.NewGuid():N}");
            string directory = createImportSource(storage, "external");
            string rootPath = Path.GetDirectoryName(directory)!;
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new BmsFolderImporter(storage, realm);
            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(rootPath, ExternalLibraryRootType.BMS);
            var root = config.Roots.Single();
            var scanner = new ExternalLibraryScanner(config, new FilesystemBeatmapIndex(storage, realm))
            {
                BmsDirectoryImporter = (path, registeredRoot, ct) => importer.RegisterExternalDirectory(path, registeredRoot, ct),
                BmsDirectoryShouldImport = importer.ShouldImportExternalDirectory,
            };
            await scanner.ScanAllRoots().ConfigureAwait(false);
            Guid id = realm.Run(r => r.All<BeatmapSetInfo>().Single().ID);
            string chart = Path.Combine(directory, "chart.bms");
            File.Delete(chart);
            await scanner.ScanAllRoots(ExternalLibraryScanner.ScanMode.Incremental).ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);
            await scanner.ScanAllRoots().ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.True);
            File.WriteAllText(chart, buildChartText());
            await scanner.ScanAllRoots(ExternalLibraryScanner.ScanMode.Incremental).ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);

            string offline = rootPath + "-offline";
            Directory.Move(rootPath, offline);
            try
            {
                var failed = await scanner.ScanAllRoots().ConfigureAwait(false);
                Assert.That(failed.Errors, Is.EqualTo(1));
                Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);
            }
            finally
            {
                Directory.Move(offline, rootPath);
            }

            await scanner.RemoveRoot(root).ConfigureAwait(false);
            Assert.That(config.Roots, Is.Empty);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.True);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.DeletePending), Is.False);
            Assert.That(File.Exists(chart), Is.True);
            config.AddRoot(rootPath, ExternalLibraryRootType.BMS);
            await scanner.ScanAllRoots().ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestRemovingLegacyOverlappingRootKeepsOtherRootVisible(bool removeParent)
        {
            using var storage = new TemporaryNativeStorage($"bms-overlapping-roots-{Guid.NewGuid():N}");
            string directory = createImportSource(storage, "external");
            string parentPath = Path.GetDirectoryName(directory)!;
            var legacyRoots = new[]
            {
                new ExternalLibraryRoot { Path = parentPath, Type = ExternalLibraryRootType.BMS },
                new ExternalLibraryRoot { Path = directory, Type = ExternalLibraryRootType.BMS },
            };
            File.WriteAllText(storage.GetFullPath("library-roots.json"), Newtonsoft.Json.JsonConvert.SerializeObject(legacyRoots));
            var config = new ExternalLibraryConfig(storage);
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new BmsFolderImporter(storage, realm);
            var imported = await importer.RegisterExternalDirectory(directory, parentPath).ConfigureAwait(false);
            Guid id = imported.ImportedBeatmapSet!.PerformRead(s => s.ID);
            var scanner = new ExternalLibraryScanner(config, new FilesystemBeatmapIndex(storage, realm));
            await scanner.RemoveRoot(config.Roots[removeParent ? 0 : 1]).ConfigureAwait(false);
            Assert.That(config.Roots.Count, Is.EqualTo(1));
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);
            await scanner.RemoveRoot(config.Roots.Single()).ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "chart.bms")), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestInvalidDirectoryReportsFailureAndDoesNotRetireOtherMissingSources(bool external)
        {
            using var storage = new TemporaryNativeStorage($"bms-invalid-scan-{Guid.NewGuid():N}");
            string rootName = external ? "external" : "chartbms";
            string invalidDirectory = createImportSource(storage, rootName + "/invalid");
            string missingDirectory = createImportSource(storage, rootName + "/missing");
            string rootPath = storage.GetFullPath(rootName);
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new BmsBeatmapImporter(storage, realm);
            var config = new ExternalLibraryConfig(storage);
            var scanner = new ExternalLibraryScanner(config, new FilesystemBeatmapIndex(storage, realm));
            Func<Task<ExternalLibraryScanner.ScanResult>> scan;
            if (external)
            {
                config.AddRoot(rootPath, ExternalLibraryRootType.BMS);
                scanner.BmsDirectoryImporter = (path, registeredRoot, ct) => importer.RegisterExternalDirectory(path, registeredRoot, ct);
                scan = () => scanner.ScanAllRoots();
            }
            else
            {
                var managedScanner = new ManagedLibraryScanner(scanner,
                    new[] { new ExternalLibraryScanner.ScanRootDefinition(rootPath, ExternalLibraryRootType.BMS, external: false) })
                {
                    BmsDirectoryImporter = (path, _, ct) => importer.RegisterManagedDirectory(path, ct),
                };
                scan = () => managedScanner.ScanAllRoots();
            }

            await scan().ConfigureAwait(false);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));
            File.WriteAllText(Path.Combine(invalidDirectory, "chart.bms"), "#TITLE Broken chart\n");
            File.Delete(Path.Combine(missingDirectory, "chart.bms"));
            var failed = await scan().ConfigureAwait(false);
            Assert.That(failed.Errors, Is.EqualTo(1));
            Assert.That(failed.Imported, Is.Zero);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable && !s.DeletePending)), Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(invalidDirectory, "chart.bms")), Is.True);

            File.WriteAllText(Path.Combine(invalidDirectory, "chart.bms"), buildChartText());
            var recovered = await scan().ConfigureAwait(false);
            Assert.That(recovered.Errors, Is.Zero);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable && !s.DeletePending)), Is.EqualTo(1));
        }

        [Test]
        public async Task TestMixedRulesetsInSameExternalDirectoryDoNotRetireEachOther()
        {
            using var storage = new TemporaryNativeStorage($"bms-mixed-directory-{Guid.NewGuid():N}");
            string directory = createImportSource(storage, "external");
            File.WriteAllText(Path.Combine(directory, "chart.osu"), @"osu file format v14

[General]
Mode: 3

[Metadata]
Title: Mixed library
Artist: OMS
Creator: OMS
Version: 4K

[Difficulty]
CircleSize: 4
OverallDifficulty: 5
HPDrainRate: 5
SliderMultiplier: 1.4
SliderTickRate: 1

[TimingPoints]
0,500,4,2,1,100,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var bms = new BmsFolderImporter(storage, realm);
            var mania = new ManiaFolderImporter(storage, realm);
            var first = await bms.RegisterExternalDirectory(directory).ConfigureAwait(false);
            Guid originalBmsId = first.ImportedBeatmapSet!.PerformRead(s => s.ID);
            var other = await mania.RegisterExternalDirectory(directory).ConfigureAwait(false);
            Guid maniaId = other.ImportedBeatmapSet!.PerformRead(s => s.ID);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));

            File.WriteAllText(Path.Combine(directory, "chart.bms"), buildChartText().Replace("Filesystem Test", "Updated BMS"));
            await bms.RegisterExternalDirectory(directory).ConfigureAwait(false);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(originalBmsId)!.FilesystemUnavailable), Is.True);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(maniaId)!.FilesystemUnavailable), Is.False);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));
            await mania.RegisterExternalDirectory(directory).ConfigureAwait(false);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(directory, "chart.bms")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "chart.osu")), Is.True);
        }

        private static string createImportSource(Storage storage, string directoryName)
        {
            string sourceRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(sourceRoot);
            File.WriteAllText(Path.Combine(sourceRoot, "chart.bms"), buildChartText());
            File.WriteAllText(Path.Combine(sourceRoot, "stage.png"), "placeholder");

            return sourceRoot;
        }

        private static string buildChartText() => @"
#TITLE Filesystem Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#STAGEFILE stage.png
#00111:AA00
#00132:AA00
#00133:AA00
#00134:AA00
#00135:AA00
";

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
