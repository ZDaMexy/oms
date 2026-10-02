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
using CRC32 = SharpCompress.Compressors.Deflate.CRC32;

namespace osu.Game.Rulesets.Bms.Tests.Beatmaps
{
    [TestFixture]
    public class BmsDownloadImporterTest
    {
        private const string chart = "#TITLE Downloaded Song\n#ARTIST OMS\n#BPM 150\n#WAV01 sound.wav\n#00119:0100\n";
        private const string ambiguous_chart = "#TITLE Ambiguous Target\n#ARTIST OMS\n#BPM 150\n#00111:0100\n";
        private const uint chart_limit = 32 * 1024 * 1024;

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
        public async Task TestUnsupportedZipCompressionFailsBeforePublishingAndPreservesExistingLibrary()
        {
            string existingPath = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "player's audio") });
            var existing = await importer.ImportAsync(existingPath, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string existingSound = realm.Run(r => storage.GetFullPath(Path.Combine(r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!, "sound.wav")));
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "downloaded audio") });
            byte[] bytes = modifyZipEntry(path, "Song/sound.wav", (data, local, central) =>
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(local + 8), 77);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(central + 10), 77);
            });

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.InnerException, Is.TypeOf<NotSupportedException>());
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
                Assert.That(File.ReadAllText(existingSound), Is.EqualTo("player's audio"));
            });
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestArchivePathLengthOrDepthBudgetIsEnforced(bool tooDeep)
        {
            string unsafePath = tooDeep
                ? string.Join('/', Enumerable.Repeat("directory", 32)) + "/asset.wav"
                : $"{new string('a', 200)}/{new string('b', 200)}/{new string('c', 111)}";
            string path = createZip(new[] { ("Song/chart.bme", chart), (unsafePath, "untrusted data") });
            byte[] bytes = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public async Task TestResourceAtThirtyTwoPathLevelsIsPublished()
        {
            string resource = "Song/" + string.Join('/', Enumerable.Repeat("directory", 30)) + "/sound.wav";
            string path = createZip(new[] { ("Song/chart.bme", chart), (resource, "nested audio") });
            byte[] bytes = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string library = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);

            Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(library, resource["Song/".Length..]))), Is.EqualTo("nested audio"));
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase("bms")]
        [TestCase("bme")]
        [TestCase("bml")]
        [TestCase("pms")]
        public void TestOversizedChartDeclarationIsRejected(string extension)
        {
            string name = $"Song/chart.{extension}";
            string path = createZip(new[] { (name, chart) });
            byte[] bytes = setZipDeclaredSize(path, name, chart_limit + 1);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("chart text limit"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestTotalChartDeclarationBudgetIsCheckedBeforeExtraction()
        {
            var entries = Enumerable.Range(0, 5).Select(index => ($"Song/chart{index}.bme", chart)).ToArray();
            string path = createZip(entries);

            foreach (var entry in entries)
                setZipDeclaredSize(path, entry.Item1, chart_limit);

            byte[] bytes = File.ReadAllBytes(path);
            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("chart text limit"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestActualChartBytesCannotEscapeTheDeclaredBudget()
        {
            string path = createPaddedZip("Song/chart.bme", chart_limit + 1);
            byte[] bytes = setZipDeclaredSize(path, "Song/chart.bme", chart_limit);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("chart text limit"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestActualTotalChartBytesCannotEscapeTheDeclaredBudget()
        {
            string path = createPaddedZip("Song/chart0.bme", chart_limit, paddedChartCount: 4, extraChart: true);
            byte[] bytes = setZipDeclaredSize(path, "Song/extra.bme", 0);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("chart text limit"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public async Task TestOrdinaryResourceMayExceedTheChartTextBudget()
        {
            string path = createPaddedZip("Song/sound.wav", chart_limit + 1);
            byte[] bytes = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string library = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);

            Assert.That(new FileInfo(storage.GetFullPath(Path.Combine(library, "sound.wav"))).Length, Is.EqualTo(chart_limit + 1));
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestDamagedStoredResourceIsRejectedBeforePublishing()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "valid sound payload") }, compressionLevel: CompressionLevel.NoCompression);
            byte[] bytes = modifyZipEntry(path, "Song/sound.wav", (data, local, _) =>
            {
                int offset = local + 30 + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 26)) + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 28));
                data[offset] ^= 0x20;
            });

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public async Task TestMalformedDeflateResourceFailsBeforePublishingAndPreservesExistingLibrary()
        {
            string oldChart = chart.Replace("Downloaded Song", "Player's Library Song", StringComparison.Ordinal);
            string existingPath = createZip(new[] { ("Owned/chart.bme", oldChart), ("Owned/sound.wav", "player's audio") });
            var existing = await importer.ImportAsync(existingPath, new[] { md5(oldChart) }, CancellationToken.None).ConfigureAwait(false);
            string existingSound = realm.Run(r => storage.GetFullPath(Path.Combine(r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!, "sound.wav")));
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "valid sound payload") });
            byte[] bytes = modifyZipEntry(path, "Song/sound.wav", (data, local, _) =>
            {
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 8)), Is.EqualTo(8));
                int offset = local + 30 + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 26)) + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 28));
                // DEFLATE BTYPE=3 is reserved. Keep the chart, declared sizes and central CRC unchanged.
                data[offset] = (byte)((data[offset] & ~0x06) | 0x06);
            });

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.InnerException, Is.TypeOf<SharpCompress.Compressors.Deflate.ZlibException>());
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
                Assert.That(realm.Run(r => r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.DeletePending), Is.False);
                Assert.That(realm.Run(r => r.Find<BeatmapInfo>(existing.Single().BeatmapId)!.BeatmapSet!.FilesystemUnavailable), Is.False);
                Assert.That(File.ReadAllText(existingSound), Is.EqualTo("player's audio"));
            });
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase((ushort)14, "09040500E1000001000000000000", "SharpCompress.Compressors.LZMA.InvalidParamException")]
        [TestCase((ushort)93, "696E76616C6964207A737464", "ZstdSharp.ZstdException")]
        [TestCase((ushort)95, "FD377A585A00000AE1FB0CA1", "System.NotImplementedException")]
        [TestCase((ushort)14, "09040000", "System.IndexOutOfRangeException")]
        public void TestInvalidOrUnsupportedZipCodecResourceIsRejected(ushort method, string compressedBytes, string exceptionType)
        {
            byte[] payload = Convert.FromHexString(compressedBytes);
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", new string('x', payload.Length)) }, compressionLevel: CompressionLevel.NoCompression);
            byte[] bytes = modifyZipEntry(path, "Song/sound.wav", (data, local, central) =>
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(local + 8), method);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(central + 10), method);
                int offset = local + 30 + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 26)) + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 28));
                payload.CopyTo(data, offset);
            });

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.InnerException!.GetType().FullName, Is.EqualTo(exceptionType));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestMalformedSevenZipLzma2ChartIsRejected()
        {
            string path = createStoredArchive("7z", "Song/chart.bme", invalidLzma2: true);
            byte[] bytes = File.ReadAllBytes(path);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.InnerException!.GetType().FullName, Is.EqualTo("SharpCompress.Compressors.LZMA.DataErrorException"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestSevenZipStartHeaderCrcCorruptionIsRejected()
        {
            string path = createStoredArchive("7z", "Song/chart.bme");
            byte[] bytes = File.ReadAllBytes(path);
            bytes[8] ^= 1;
            File.WriteAllBytes(path, bytes);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestStoredRarLengthMismatchIsRejected()
        {
            string path = createStoredArchive("rar", "Song/chart.bme", (uint)Encoding.UTF8.GetByteCount(chart) + 1);
            byte[] bytes = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public void TestCorruptBzip2ZipResourceIsRejected()
        {
            string path = createBzip2Zip();
            byte[] bytes = modifyZipEntry(path, "Song/sound.wav", (data, local, _) =>
            {
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 8)), Is.EqualTo(12));
                int offset = local + 30 + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 26)) + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(local + 28));
                Assert.That(Encoding.ASCII.GetString(data, offset, 3), Is.EqualTo("BZh"));
                data[offset + 10] ^= 1;
            });

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase(CompressionLevel.NoCompression)]
        [TestCase(CompressionLevel.Optimal)]
        public void TestZipDirectoryWithFileDataIsRejected(CompressionLevel compressionLevel)
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/assets/", "invalid directory payload") }, compressionLevel: compressionLevel);
            byte[] bytes = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TestActualResourceLengthMustMatchDeclaration(int difference)
        {
            const string resource = "valid sound payload";
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", resource) });
            byte[] bytes = setZipDeclaredSize(path, "Song/sound.wav", (uint)(Encoding.UTF8.GetByteCount(resource) + difference));

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [Test]
        public async Task TestDataDescriptorZipRetainsCentralSizeAndCrc()
        {
            string path = createZip(new[] { ("Song/chart.bme", chart), ("Song/sound.wav", "descriptor audio") }, dataDescriptor: true);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)) & 8, Is.EqualTo(8));
            Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14)), Is.Zero);
            Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(22)), Is.Zero);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string library = realm.Run(r => r.Find<BeatmapInfo>(result.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);

            Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(library, "sound.wav"))), Is.EqualTo("descriptor audio"));
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase("rar")]
        [TestCase("7z")]
        public async Task TestStoredRarOrSevenZipChartIsPublished(string format)
        {
            string path = createStoredArchive(format, "Song/chart.bme");
            byte[] bytes = File.ReadAllBytes(path);

            var result = await importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None).ConfigureAwait(false);
            string localChart = realm.Run(r => storage.GetFullPath(Path.Combine(r.Find<BeatmapInfo>(result.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!, "chart.bme")));

            Assert.That(File.ReadAllText(localChart), Is.EqualTo(chart));
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase("rar", false)]
        [TestCase("rar", true)]
        [TestCase("7z", false)]
        [TestCase("7z", true)]
        public void TestRarAndSevenZipPathBudgetsAreEnforced(string format, bool tooDeep)
        {
            string name = tooDeep
                ? string.Join('/', Enumerable.Repeat("directory", 32)) + "/chart.bme"
                : $"{new string('a', 200)}/{new string('b', 200)}/{new string('c', 111)}.bme";
            string path = createStoredArchive(format, name);
            byte[] bytes = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, bytes);
        }

        [TestCase("rar")]
        [TestCase("7z")]
        public void TestRarAndSevenZipChartDeclarationBudgetIsEnforced(string format)
        {
            string path = createStoredArchive(format, "Song/chart.bme", chart_limit + 1);
            byte[] bytes = File.ReadAllBytes(path);

            var exception = Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, new[] { md5(chart) }, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("chart text limit"));
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

        private string createZip((string Name, string Content)[] entries, int? attributes = null, Encoding? entryNameEncoding = null,
                                 CompressionLevel compressionLevel = CompressionLevel.Optimal, bool dataDescriptor = false)
        {
            string taskDirectory = storage.GetFullPath($"bms-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(taskDirectory);
            string path = Path.Combine(taskDirectory, "package.zip");

            using var source = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var zip = new ZipArchive(dataDescriptor ? new NonSeekableWriteStream(source) : source, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: entryNameEncoding);

            foreach (var item in entries)
            {
                var entry = zip.CreateEntry(item.Name, compressionLevel);

                if (attributes.HasValue)
                    entry.ExternalAttributes = attributes.Value;

                using var content = entry.Open();
                content.Write(Encoding.UTF8.GetBytes(item.Content));
            }

            return path;
        }

        private string createPaddedZip(string name, long size, int paddedChartCount = 1, bool extraChart = false)
        {
            string taskDirectory = storage.GetFullPath($"bms-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(taskDirectory);
            string path = Path.Combine(taskDirectory, "package.zip");
            byte[] prefix = Encoding.UTF8.GetBytes(chart);
            byte[] buffer = new byte[64 * 1024];
            Array.Fill(buffer, (byte)'x');

            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            if (!BmsImportExtensions.IsBeatmapFile(name))
            {
                using var original = zip.CreateEntry("Song/chart.bme").Open();
                original.Write(prefix);
            }

            for (int i = 0; i < paddedChartCount; i++)
            {
                using var output = zip.CreateEntry(paddedChartCount == 1 ? name : $"Song/chart{i}.bme").Open();
                output.Write(prefix);

                for (long remaining = size - prefix.Length; remaining > 0;)
                {
                    int count = (int)Math.Min(remaining, buffer.Length);
                    output.Write(buffer, 0, count);
                    remaining -= count;
                }
            }

            if (extraChart)
            {
                using var original = zip.CreateEntry("Song/extra.bme").Open();
                original.Write(prefix);
            }

            return path;
        }

        private static byte[] setZipDeclaredSize(string path, string name, uint size) => modifyZipEntry(path, name, (data, local, central) =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(local + 22), size);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(central + 24), size);
        });

        private static byte[] modifyZipEntry(string path, string name, Action<byte[], int, int> mutate)
        {
            byte[] bytes = File.ReadAllBytes(path);

            for (int offset = 0; offset <= bytes.Length - 46; offset++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) != 0x02014b50)
                    continue;

                int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28));

                if (Encoding.UTF8.GetString(bytes, offset + 46, nameLength) != name)
                    continue;

                int localOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 42)));
                mutate(bytes, localOffset, offset);
                File.WriteAllBytes(path, bytes);
                return bytes;
            }

            throw new InvalidOperationException($"ZIP fixture does not contain '{name}'.");
        }

        private string createBzip2Zip()
        {
            string taskDirectory = storage.GetFullPath($"bms-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(taskDirectory);
            string path = Path.Combine(taskDirectory, "package.zip");
            using var output = File.Create(path);
            using var zip = SharpCompress.Archives.Zip.ZipArchive.Create();
            using var chartSource = new MemoryStream(Encoding.UTF8.GetBytes(chart));
            using var resourceSource = new MemoryStream(Encoding.UTF8.GetBytes("valid sound payload"));
            zip.AddEntry("Song/chart.bme", chartSource, chartSource.Length, null);
            zip.AddEntry("Song/sound.wav", resourceSource, resourceSource.Length, null);
            zip.SaveTo(output, new SharpCompress.Writers.WriterOptions(SharpCompress.Common.CompressionType.BZip2));
            return path;
        }

        private string createStoredArchive(string format, string name, uint? declaredSize = null, bool invalidLzma2 = false)
        {
            string taskDirectory = storage.GetFullPath($"bms-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(taskDirectory);
            string path = Path.Combine(taskDirectory, $"package.{format}");
            byte[] data = Encoding.UTF8.GetBytes(chart);

            if (invalidLzma2)
                data[0] = 0x03;

            uint size = declaredSize ?? (uint)data.Length;
            using var output = File.Create(path);
            using var writer = new BinaryWriter(output);

            // Minimal stored archives keep corruption and budget tests independent of installed packer tools.
            if (format == "rar")
            {
                writer.Write(Convert.FromHexString("526172211A0700"));
                writeRarHeader(writer, Convert.FromHexString("00007300000D00000000000000"));
                byte[] filename = Encoding.ASCII.GetBytes(name);
                byte[] header = new byte[32 + filename.Length];
                header[2] = 0x74;
                BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(3), 0x8000);
                BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(5), (ushort)header.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(7), (uint)data.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(11), size);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), crc32(data));
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), 0x00210000);
                header[24] = 20;
                header[25] = 0x30;
                BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26), (ushort)filename.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), 0x20);
                filename.CopyTo(header, 32);
                writeRarHeader(writer, header);
                writer.Write(data);
                writeRarHeader(writer, Convert.FromHexString("00007B00400700"));
            }
            else
            {
                byte[] filename = Encoding.Unicode.GetBytes(name);
                using var headerStream = new MemoryStream();
                using (var headerWriter = new BinaryWriter(headerStream, Encoding.UTF8, leaveOpen: true))
                {
                    headerWriter.Write(Convert.FromHexString("010406000109"));
                    writeSevenZipNumber(headerWriter, (ulong)data.Length);
                    headerWriter.Write(Convert.FromHexString(invalidLzma2 ? "00070B010001212101000C" : "00070B01000101000C"));
                    writeSevenZipNumber(headerWriter, size);
                    headerWriter.Write(Convert.FromHexString("00080A01"));
                    headerWriter.Write(crc32(data));
                    headerWriter.Write(Convert.FromHexString("0000050111"));
                    writeSevenZipNumber(headerWriter, (ulong)filename.Length + 3);
                    headerWriter.Write((byte)0);
                    headerWriter.Write(filename);
                    headerWriter.Write((ushort)0);
                    headerWriter.Write((ushort)0);
                }

                byte[] header = headerStream.ToArray();
                byte[] startHeader = new byte[20];
                BinaryPrimitives.WriteUInt64LittleEndian(startHeader, (ulong)data.Length);
                BinaryPrimitives.WriteUInt64LittleEndian(startHeader.AsSpan(8), (ulong)header.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(startHeader.AsSpan(16), crc32(header));
                writer.Write(Convert.FromHexString("377ABCAF271C0004"));
                writer.Write(crc32(startHeader));
                writer.Write(startHeader);
                writer.Write(data);
                writer.Write(header);
            }

            return path;
        }

        private static void writeRarHeader(BinaryWriter writer, byte[] header)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)crc32(header[2..]));
            writer.Write(header);
        }

        private static void writeSevenZipNumber(BinaryWriter writer, ulong value)
        {
            byte first = 0;

            for (int extraBytes = 0; extraBytes < 8; extraBytes++)
            {
                if (value < (1UL << (7 * (extraBytes + 1))))
                {
                    writer.Write((byte)(first | (byte)(value >> (8 * extraBytes))));
                    for (int i = 0; i < extraBytes; i++)
                        writer.Write((byte)(value >> (8 * i)));
                    return;
                }

                first |= (byte)(0x80 >> extraBytes);
            }

            writer.Write(byte.MaxValue);
            writer.Write(value);
        }

        private static uint crc32(byte[] data)
        {
            var crc = new CRC32();
            crc.SlurpBlock(data, 0, data.Length);
            return unchecked((uint)crc.Crc32Result);
        }

        private sealed class NonSeekableWriteStream : Stream
        {
            private readonly Stream inner;

            public NonSeekableWriteStream(Stream inner) => this.inner = inner;

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
        }

        private static string md5(string text) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private static void assertTaskPreservedAndCleaned(string path, byte[] originalArchive)
        {
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalArchive));
            Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!), Is.EquivalentTo(new[] { path }));
        }
    }
}
