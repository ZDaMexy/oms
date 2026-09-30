// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests.Beatmaps
{
    [TestFixture]
    public class BmsDownloadImporterTest
    {
        private const string chart = "#TITLE Downloaded Song\n#ARTIST OMS\n#BPM 150\n#WAV01 sound.wav\n#00119:0100\n";
        private const string ambiguous_chart = "#TITLE Ambiguous Target\n#ARTIST OMS\n#BPM 150\n#00111:0100\n";

        private TemporaryNativeStorage storage = null!;
        private RealmAccess realm = null!;
        private RealmRulesetStore rulesets = null!;
        private BmsDownloadImporter importer = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"bms-download-import-{Guid.NewGuid():N}");
            realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            rulesets = new RealmRulesetStore(realm, storage);
            importer = new BmsDownloadImporter(storage, realm);
        }

        [TearDown]
        public void TearDown()
        {
            rulesets.Dispose();
            realm.Dispose();
            storage.Dispose();
        }

        [Test]
        public async Task TestRequestedChartIsPublishedToDirectReadLibrary()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "sound bytes"), ("Song/assets/stage.png", "stage bytes") });
            byte[] originalArchive = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart).ToUpperInvariant() }, CancellationToken.None).ConfigureAwait(false);
            BeatmapInfo beatmap = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.Detach());
            BeatmapSetInfo set = beatmap.BeatmapSet!;
            using var chartStream = File.OpenRead(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, beatmap.LocalFilePath!)));
            var reloaded = new BmsBeatmapLoader().Load(chartStream, beatmap.LocalFilePath!, beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(result.Single().Md5, Is.EqualTo(md5(chart)));
                Assert.That(beatmap.MD5Hash, Is.EqualTo(result.Single().Md5));
                Assert.That(set.FilesystemStoragePath, Does.StartWith("chartbms/"));
                Assert.That(set.IsExternalFilesystemStorage, Is.False);
                Assert.That(set.FilesystemUnavailable, Is.False);
                Assert.That(set.DeletePending, Is.False);
                Assert.That(set.Files, Is.Empty);
                Assert.That(set.OnlineID, Is.EqualTo(-1));
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, beatmap.LocalFilePath!))), Is.EqualTo(chart));
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, "sound.wav"))), Is.EqualTo("sound bytes"));
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, "assets/stage.png"))), Is.EqualTo("stage bytes"));
                Assert.That(reloaded.HitObjects, Has.Count.EqualTo(1));
            });

            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public void TestMissingRequestedMd5DoesNotImportOtherCharts()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/readme.txt", "missing target") });
            byte[] originalArchive = File.ReadAllBytes(path);
            string userFile = storage.GetFullPath("chartbms/user-owned/saved.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(userFile)!);
            File.WriteAllText(userFile, "player data");

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5("missing target") }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            Assert.That(File.ReadAllText(userFile), Is.EqualTo("player data"));
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public void TestMatchingButUnplayableTargetReportsFailure()
        {
            string path = createZip(new[] { ("Song/chart.bms", ambiguous_chart) });
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(ambiguous_chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public void TestHtmlDownloadResponseIsRejectedAsInvalidPackage()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] response = Encoding.UTF8.GetBytes("<!doctype html><html><body>Download unavailable</body></html>");
            File.WriteAllBytes(path, response);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, response);
        }

        [Test]
        public void TestTruncatedZipIsRejectedAsInvalidPackage()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] response = File.ReadAllBytes(path).Take(38).ToArray();
            File.WriteAllBytes(path, response);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, response);
        }

        [TestCase("377ABCAF271C000400000000")]
        [TestCase("526172211A07010000000000")]
        public void TestTruncatedSevenZipOrRarIsRejectedAsInvalidPackage(string truncatedHeader)
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] response = Convert.FromHexString(truncatedHeader);
            File.WriteAllBytes(path, response);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, response);
        }

        [TestCase("../outside.txt")]
        [TestCase("..\\outside.txt")]
        [TestCase("Song/../../outside.txt")]
        [TestCase("/absolute.txt")]
        [TestCase("\\absolute.txt")]
        [TestCase("C:\\absolute.txt")]
        [TestCase("C:relative-to-drive.txt")]
        [TestCase("Song/chart.bme:stream")]
        [TestCase("NUL.txt")]
        [TestCase("COM1.bme")]
        [TestCase("LPT\u00b9.txt")]
        [TestCase("Song /asset.wav")]
        [TestCase("Song./asset.wav")]
        [TestCase("Song//asset.wav")]
        [TestCase("./asset.wav")]
        [TestCase("Song/./asset.wav")]
        [TestCase("Song/*.wav")]
        public void TestUnsafeWindowsArchivePathIsRejected(string unsafePath)
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), (unsafePath, "untrusted data") });
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            Assert.That(File.Exists(storage.GetFullPath("outside.txt")), Is.False);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [TestCase("Song/chart.bme", "song/chart.bme")]
        [TestCase("Song/chart.bme", "song/other.bme")]
        [TestCase("Song/chart.bme", "Song/chart.bme")]
        [TestCase("Song", "Song/chart.bme")]
        [TestCase("Song/chart.bme", "Song")]
        [TestCase("Song/", "Song/")]
        public void TestDuplicateOrFileDirectoryConflictIsRejected(string first, string second)
        {
            string path = createZip(new[] { (first, chart), (second, chart) });
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [TestCase(unchecked((int)0xa1ff0000))]
        [TestCase((int)FileAttributes.ReparsePoint)]
        [TestCase(0x10000000)]
        public void TestZipLinksAndSpecialEntriesAreRejected(int attributes)
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) }, attributes);
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public void TestEncryptedZipEntryIsRejectedBeforeImport()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] bytes = File.ReadAllBytes(path);

            for (int i = 0; i < bytes.Length - 10; i++)
            {
                uint signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4));

                if (signature is 0x04034b50 or 0x02014b50)
                {
                    int offset = i + (signature == 0x04034b50 ? 6 : 8);
                    ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), (ushort)(flags | 1));
                }
            }

            File.WriteAllBytes(path, bytes);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestOversizedZipFileDeclarationIsRejected()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] bytes = File.ReadAllBytes(path);

            for (int i = 0; i < bytes.Length - 28; i++)
            {
                uint signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4));

                if (signature is 0x04034b50 or 0x02014b50)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + (signature == 0x04034b50 ? 22 : 24), 4), 0x80000001);
            }

            File.WriteAllBytes(path, bytes);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestEntryBudgetIsEnforced()
        {
            string path = createZip(Enumerable.Range(0, 50_001).Select(index => ($"entry-{index}.txt", string.Empty)).ToArray());
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public async Task TestExistingUnindexedDirectoryIsPreservedDespiteMatchingHash()
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chart))).ToLowerInvariant();
            string occupiedPath = $"chartbms/Song-{hash[..8]}";
            Directory.CreateDirectory(storage.GetFullPath(occupiedPath));
            File.WriteAllText(storage.GetFullPath($"{occupiedPath}/chart.bme"), chart);
            File.WriteAllText(storage.GetFullPath($"{occupiedPath}/sound.wav"), "user's original audio");

            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "downloaded audio") });
            byte[] originalArchive = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string importedPath = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);

            Assert.Multiple(() =>
            {
                Assert.That(importedPath, Is.EqualTo($"{occupiedPath}-2"));
                Assert.That(File.ReadAllText(storage.GetFullPath($"{occupiedPath}/chart.bme")), Is.EqualTo(chart));
                Assert.That(File.ReadAllText(storage.GetFullPath($"{occupiedPath}/sound.wav")), Is.EqualTo("user's original audio"));
                Assert.That(File.ReadAllText(storage.GetFullPath($"{importedPath}/sound.wav")), Is.EqualTo("downloaded audio"));
            });

            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public async Task TestCancelledImportPreservesExistingLibraryAndArchive()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            var existing = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            byte[] originalArchive = File.ReadAllBytes(path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => importer.ImportAsync(path, new[] { md5(chart) }, cancellation.Token));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
            Assert.That(realm.Run(r => r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.DeletePending), Is.False);
            Assert.That(realm.Run(r => r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.FilesystemUnavailable), Is.False);
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public async Task TestRepeatedDownloadReusesIdentityAndPreservesExistingAssets()
        {
            string firstPath = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "first download") });
            var original = await importer.ImportAsync(firstPath, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string libraryPath = realm.Run(r => r.Find<BeatmapInfo>(original.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);
            string soundPath = storage.GetFullPath(Path.Combine(libraryPath, "sound.wav"));
            File.WriteAllText(soundPath, "user's edited audio");
            string secondPath = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "second download") });
            byte[] secondArchive = File.ReadAllBytes(secondPath);

            var repeated = await importer.ImportAsync(secondPath, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(repeated.Single().BeatmapId, Is.EqualTo(original.Single().BeatmapId));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
                Assert.That(File.ReadAllText(soundPath), Is.EqualTo("user's edited audio"));
            });

            assertTaskPreservedAndCleaned(secondPath, secondArchive);
        }

        [Test]
        public void TestPartialFailureKeepsPreviouslyCommittedDirectoryAndReportsFailure()
        {
            string path = createZip(new[] { ("A/chart.bme", chart), ("B/invalid.bms", ambiguous_chart) });
            byte[] originalArchive = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart), md5(ambiguous_chart) }, CancellationToken.None));

            var committed = realm.Run(r => r.All<BeatmapSetInfo>().Single().Detach());

            Assert.Multiple(() =>
            {
                Assert.That(committed.Beatmaps.Single().MD5Hash, Is.EqualTo(md5(chart)));
                Assert.That(committed.DeletePending, Is.False);
                Assert.That(committed.FilesystemUnavailable, Is.False);
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(committed.FilesystemStoragePath!, "chart.bme"))), Is.EqualTo(chart));
            });

            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public async Task TestStaleReusedLibraryFileDoesNotReportInstalledTarget()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart) });
            byte[] originalArchive = File.ReadAllBytes(path);
            var original = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string chartPath = realm.Run(r =>
            {
                var beatmap = r.Find<BeatmapInfo>(original.Single().BeatmapId)!;
                return storage.GetFullPath(Path.Combine(beatmap.BeatmapSet!.FilesystemStoragePath!, beatmap.LocalFilePath!));
            });
            File.WriteAllText(chartPath, "user's modified chart");

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(File.ReadAllText(chartPath), Is.EqualTo("user's modified chart"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public async Task TestShiftJisZipFilenamesAndDirectoryEntriesAreSupported()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string path = createZip(new[] { ("楽曲/", string.Empty), ("楽曲/譜面.bme", chart), ("楽曲/sound.wav", "audio") }, entryNameEncoding: Encoding.GetEncoding(932));
            byte[] originalArchive = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string localFilename = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.LocalFilePath!);

            Assert.That(localFilename, Is.EqualTo("譜面.bme"));
            assertTaskPreservedAndCleaned(path, originalArchive);
        }

        [Test]
        public void TestPackageOutsideExclusiveDownloadDirectoryIsRejected()
        {
            string source = createZip(new[] { ("Song/chart.bme", chart) });
            string outside = storage.GetFullPath("user-package.zip");
            File.Move(source, outside);
            byte[] originalArchive = File.ReadAllBytes(outside);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(outside, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(File.ReadAllBytes(outside), Is.EqualTo(originalArchive));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
        }

        private string createZip((string Name, string Content)[] entries, int? attributes = null, Encoding? entryNameEncoding = null)
        {
            string taskDirectory = storage.GetFullPath($"bms-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(taskDirectory);
            string path = Path.Combine(taskDirectory, "package.zip");

            using var source = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var zip = new ZipArchive(source, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: entryNameEncoding);

            foreach (var item in entries)
            {
                var entry = zip.CreateEntry(item.Name);

                if (attributes.HasValue)
                    entry.ExternalAttributes = attributes.Value;

                using var content = entry.Open();
                content.Write(Encoding.UTF8.GetBytes(item.Content));
            }

            return path;
        }

        private static string md5(string text) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private static void assertTaskPreservedAndCleaned(string path, byte[] originalArchive)
        {
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalArchive));
            Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!), Is.EquivalentTo(new[] { path }));
        }
    }
}
