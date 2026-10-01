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
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.Beatmaps
{
    [TestFixture]
    public class ManiaDownloadImporterTest
    {
        private const int set_id = 1234;
        private const int requested_id = 5678;
        private static readonly string requested_chart = createChart(set_id, requested_id);

        private TemporaryNativeStorage storage = null!;
        private RealmAccess realm = null!;
        private RealmRulesetStore rulesets = null!;
        private ManiaDownloadImporter importer = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"mania-download-import-{Guid.NewGuid():N}");
            realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            rulesets = new RealmRulesetStore(realm, storage);
            importer = new ManiaDownloadImporter(storage, realm);
        }

        [TearDown]
        public void TearDown()
        {
            rulesets.Dispose();
            realm.Dispose();
            storage.Dispose();
        }

        [Test]
        public async Task TestRequestedDifficultyAndManiaSiblingsArePublishedToDirectReadLibrary()
        {
            string path = createZip(("requested.osu", requested_chart), ("other.osu", createChart(set_id, requested_id + 1)),
                ("standard.osu", createChart(set_id, requested_id + 2, mode: 0)), ("audio.mp3", "downloaded audio"), ("assets/background.jpg", "background"));
            byte[] original = File.ReadAllBytes(path);

            var imported = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);
            var requested = imported.Single(beatmap => beatmap.OnlineId == requested_id);
            var local = realm.Run(r => r.Find<BeatmapInfo>(requested.BeatmapId)!.Detach());
            var set = local.BeatmapSet!;

            Assert.Multiple(() =>
            {
                Assert.That(imported.Select(beatmap => beatmap.OnlineId), Is.EquivalentTo(new[] { requested_id, requested_id + 1 }));
                Assert.That(requested.Md5, Is.EqualTo(md5(requested_chart)));
                Assert.That(local.MD5Hash, Is.EqualTo(requested.Md5));
                Assert.That(local.OnlineID, Is.EqualTo(requested_id));
                Assert.That(set.Beatmaps.All(beatmap => beatmap.Ruleset.ShortName == ManiaRuleset.SHORT_NAME), Is.True);
                Assert.That(set.FilesystemStoragePath, Does.StartWith("chartmania/"));
                Assert.That(set.FilesystemStoragePath, Does.Contain($"set-{set_id}"));
                Assert.That(set.IsExternalFilesystemStorage, Is.False);
                Assert.That(set.FilesystemUnavailable, Is.False);
                Assert.That(set.DeletePending, Is.False);
                Assert.That(set.Files, Is.Empty);
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, local.LocalFilePath!))), Is.EqualTo(requested_chart));
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, "audio.mp3"))), Is.EqualTo("downloaded audio"));
                Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(set.FilesystemStoragePath!, "assets/background.jpg"))), Is.EqualTo("background"));
                Assert.That(storage.ExistsDirectory("chartbms"), Is.False);
            });
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public async Task TestOnlyRequestedSongDirectoryIsPublished()
        {
            string path = createZip(("wrapper/Song/requested.osu", requested_chart), ("wrapper/Song/audio.mp3", "audio"),
                ("wrapper/Unrelated/chart.osu", createChart(set_id + 1, requested_id + 10)));
            byte[] original = File.ReadAllBytes(path);

            var imported = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);

            Assert.That(imported.Select(beatmap => beatmap.OnlineId), Is.EqualTo(new[] { requested_id }));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
            assertTaskPreservedAndCleaned(path, original);
        }

        [TestCase("missing")]
        [TestCase("wrong-set")]
        [TestCase("wrong-difficulty")]
        [TestCase("non-mania")]
        [TestCase("empty")]
        [TestCase("bad-version")]
        [TestCase("bad-header")]
        public void TestInvalidRequestedDifficultyFailsBeforePublishingOtherValidCharts(string failure)
        {
            string invalidTarget = failure switch
            {
                "missing" => "not a beatmap",
                "wrong-set" => createChart(set_id + 1, requested_id),
                "wrong-difficulty" => createChart(set_id, requested_id + 2),
                "non-mania" => createChart(set_id, requested_id, mode: 0),
                "empty" => requested_chart[..requested_chart.IndexOf("[HitObjects]", StringComparison.Ordinal)] + "[HitObjects]\n",
                "bad-version" => requested_chart.Replace("osu file format v14", "osu file format vbroken", StringComparison.Ordinal),
                "bad-header" => requested_chart.Replace("osu file format v14", "invalid .osu header", StringComparison.Ordinal),
                _ => throw new ArgumentOutOfRangeException(nameof(failure)),
            };
            string path = createZip(("Song/A-valid.osu", createChart(set_id, requested_id + 1)), ("Song/Z-target.osu", invalidTarget));
            byte[] original = File.ReadAllBytes(path);
            string userFile = createUserFile();

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, set_id, requested_id, CancellationToken.None));

            assertNoPublication(userFile);
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public void TestDuplicateOriginalDifficultyIdentityIsRejectedBeforePublication()
        {
            string path = createZip(("A/chart.osu", requested_chart), ("B/chart.osu", requested_chart));
            byte[] original = File.ReadAllBytes(path);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, set_id, requested_id, CancellationToken.None));

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public async Task TestRepeatedDownloadReusesIdentityAndPreservesPlayerAssets()
        {
            string firstPath = createZip(("Song/chart.osu", requested_chart), ("Song/audio.mp3", "first download"));
            var first = await importer.ImportAsync(firstPath, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);
            string libraryPath = realm.Run(r => r.Find<BeatmapInfo>(first.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);
            string audioPath = storage.GetFullPath(Path.Combine(libraryPath, "audio.mp3"));
            File.WriteAllText(audioPath, "player's edited audio");
            string secondPath = createZip(("Song/chart.osu", requested_chart), ("Song/audio.mp3", "replacement download"));
            byte[] original = File.ReadAllBytes(secondPath);

            var second = await importer.ImportAsync(secondPath, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(second.Single().BeatmapId, Is.EqualTo(first.Single().BeatmapId));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
                Assert.That(File.ReadAllText(audioPath), Is.EqualTo("player's edited audio"));
            });
            assertTaskPreservedAndCleaned(secondPath, original);
        }

        [Test]
        public async Task TestStaleReusedFileReportsFailureWithoutOverwritingPlayerChart()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            byte[] original = File.ReadAllBytes(path);
            var first = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);
            string chartPath = realm.Run(r =>
            {
                var beatmap = r.Find<BeatmapInfo>(first.Single().BeatmapId)!;
                return storage.GetFullPath(Path.Combine(beatmap.BeatmapSet!.FilesystemStoragePath!, beatmap.LocalFilePath!));
            });
            File.WriteAllText(chartPath, "player's modified chart");

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, set_id, requested_id, CancellationToken.None));

            Assert.That(File.ReadAllText(chartPath), Is.EqualTo("player's modified chart"));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public async Task TestUnindexedOccupiedDirectoryIsPreserved()
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requested_chart))).ToLowerInvariant();
            string occupied = storage.GetFullPath($"chartmania/Song-{hash[..8]}");
            Directory.CreateDirectory(occupied);
            File.WriteAllText(Path.Combine(occupied, "chart.osu"), requested_chart);
            File.WriteAllText(Path.Combine(occupied, "audio.mp3"), "player audio");
            string path = createZip(("Song/chart.osu", requested_chart), ("Song/audio.mp3", "downloaded audio"));

            var imported = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);
            string added = realm.Run(r => r.Find<BeatmapInfo>(imported.Single().BeatmapId)!.BeatmapSet!.FilesystemStoragePath!);

            Assert.That(added, Is.EqualTo($"chartmania/Song-{hash[..8]}-2"));
            Assert.That(File.ReadAllText(Path.Combine(occupied, "audio.mp3")), Is.EqualTo("player audio"));
            Assert.That(File.ReadAllText(storage.GetFullPath(Path.Combine(added, "audio.mp3"))), Is.EqualTo("downloaded audio"));
        }

        [Test]
        public async Task TestCancellationPreservesCommittedLibraryAndDownloadedArchive()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            byte[] original = File.ReadAllBytes(path);
            var first = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => importer.ImportAsync(path, set_id, requested_id, cancellation.Token));

            Assert.That(realm.Run(r => r.Find<BeatmapInfo>(first.Single().BeatmapId)!.BeatmapSet!.DeletePending), Is.False);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(1));
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public void TestPublicationFailureRemovesOnlyTheNewSongDirectory()
        {
            string path = createZip(("Song/chart.osu", requested_chart), ("Song/audio.mp3", "downloaded audio"));
            byte[] original = File.ReadAllBytes(path);
            string userFile = createUserFile();
            realm.Run(r =>
            {
                using var transaction = r.BeginWrite();
                r.All<osu.Game.Rulesets.RulesetInfo>().Single(ruleset => ruleset.ShortName == ManiaRuleset.SHORT_NAME).Available = false;
                transaction.Commit();
            });

            Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(path, set_id, requested_id, CancellationToken.None));

            assertNoPublication(userFile);
            Assert.That(Directory.GetDirectories(storage.GetFullPath("chartmania")), Has.Length.EqualTo(1));
            assertTaskPreservedAndCleaned(path, original);
        }

        [TestCase("../outside.txt")]
        [TestCase("..\\outside.txt")]
        [TestCase("Song/../../outside.txt")]
        [TestCase("/absolute.txt")]
        [TestCase("C:\\absolute.txt")]
        [TestCase("C:relative.txt")]
        [TestCase("Song/chart.osu:stream")]
        [TestCase("NUL.txt")]
        [TestCase("LPT\u00b9.txt")]
        [TestCase("Song /asset.mp3")]
        [TestCase("Song./asset.mp3")]
        [TestCase("Song//asset.mp3")]
        [TestCase("./asset.mp3")]
        [TestCase("Song/*.mp3")]
        public void TestUnsafeWindowsPathsRejectTheEntireDownload(string unsafePath)
        {
            assertRejectedZip(createZip(("Song/chart.osu", requested_chart), (unsafePath, "untrusted data")));
            Assert.That(File.Exists(storage.GetFullPath("outside.txt")), Is.False);
        }

        [TestCase("Song/chart.osu", "song/chart.osu")]
        [TestCase("Song/chart.osu", "song/other.osu")]
        [TestCase("Song/chart.osu", "Song/chart.osu")]
        [TestCase("Song", "Song/chart.osu")]
        [TestCase("Song/chart.osu", "Song")]
        [TestCase("Song/", "Song/")]
        public void TestDuplicateOrConflictingPathsRejectTheEntireDownload(string first, string second)
            => assertRejectedZip(createZip((first, requested_chart), (second, requested_chart)));

        [TestCase(unchecked((int)0xa1ff0000))]
        [TestCase((int)FileAttributes.ReparsePoint)]
        [TestCase(0x10000000)]
        public void TestLinksAndSpecialEntriesAreRejected(int attributes)
            => assertRejectedZip(createZip(new[] { ("Song/chart.osu", requested_chart) }, attributes: attributes));

        [Test]
        public void TestEncryptedEntryIsRejected()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            modifyZipHeaders(path, (bytes, offset, central) =>
            {
                int flagsOffset = offset + (central ? 8 : 6);
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(flagsOffset, 2));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(flagsOffset, 2), (ushort)(flags | 1));
            });
            assertRejectedZip(path);
        }

        [Test]
        public void TestDamagedFileCrcIsRejectedBeforePublication()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            modifyZipHeaders(path, (bytes, offset, central) =>
            {
                int crcOffset = offset + (central ? 16 : 14);
                uint crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(crcOffset, 4));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(crcOffset, 4), crc ^ 0x12345678);
            });
            assertRejectedZip(path);
        }

        [Test]
        public void TestActualExpandedSizeMustMatchTheDeclaration()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            modifyZipHeaders(path, (bytes, offset, central) =>
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + (central ? 24 : 22), 4), (uint)Encoding.UTF8.GetByteCount(requested_chart) - 1));
            assertRejectedZip(path);
        }

        [Test]
        public void TestOversizedChartDeclarationIsRejectedBeforeParsing()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            modifyZipHeaders(path, (bytes, offset, central) =>
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + (central ? 24 : 22), 4), 32 * 1024 * 1024 + 1));
            assertRejectedZip(path);
        }

        [Test]
        public void TestOversizedCentralDirectoryIsRejectedBeforeOpeningEntries()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            byte[] bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 22 + 12, 4), 32 * 1024 * 1024 + 1);
            File.WriteAllBytes(path, bytes);
            assertRejectedZip(path);
        }

        [Test]
        public void TestEntryCannotPointBeyondTheLocalDataRegion()
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            modifyZipHeaders(path, (bytes, offset, central) =>
            {
                if (central)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 42, 4), (uint)bytes.Length + 1);
            });
            assertRejectedZip(path);
        }

        [Test]
        public void TestEntryLimitIsAppliedBeforeOpeningZipEntries()
            => assertRejectedZip(createZip(Enumerable.Range(0, 50_001).Select(index => ($"entry-{index}.txt", string.Empty)).ToArray()));

        [TestCase(false)]
        [TestCase(true)]
        public void TestHtmlOrTruncatedResponseIsRejected(bool truncated)
        {
            string path = createZip(("Song/chart.osu", requested_chart));
            File.WriteAllBytes(path, truncated ? File.ReadAllBytes(path).Take(38).ToArray() : Encoding.UTF8.GetBytes("<!doctype html><html>download unavailable</html>"));
            assertRejectedZip(path);
        }

        [Test]
        public async Task TestShiftJisFilenameAndExplicitWrapperDirectoryAreSupported()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string path = createZip(new[] { ("楽曲/", string.Empty), ("楽曲/譜面.osu", requested_chart), ("楽曲/audio.mp3", "audio") }, entryNameEncoding: Encoding.GetEncoding(932));
            byte[] original = File.ReadAllBytes(path);

            var imported = await importer.ImportAsync(path, set_id, requested_id, CancellationToken.None).ConfigureAwait(false);

            Assert.That(realm.Run(r => r.Find<BeatmapInfo>(imported.Single().BeatmapId)!.LocalFilePath), Is.EqualTo("譜面.osu"));
            assertTaskPreservedAndCleaned(path, original);
        }

        [Test]
        public void TestArchiveOutsideExclusiveTaskDirectoryIsRejectedWithoutTouchingIt()
        {
            string source = createZip(("chart.osu", requested_chart));
            string outside = storage.GetFullPath("player-package.osz");
            File.Move(source, outside);
            byte[] original = File.ReadAllBytes(outside);

            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(outside, set_id, requested_id, CancellationToken.None));

            Assert.That(File.ReadAllBytes(outside), Is.EqualTo(original));
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
        }

        private string createUserFile()
        {
            string path = storage.GetFullPath("chartmania/player-owned/saved.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "player data");
            return path;
        }

        private void assertNoPublication(string userFile)
        {
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            Assert.That(File.ReadAllText(userFile), Is.EqualTo("player data"));
        }

        private void assertRejectedZip(string path)
        {
            byte[] original = File.ReadAllBytes(path);
            string userFile = createUserFile();
            Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(path, set_id, requested_id, CancellationToken.None));
            assertNoPublication(userFile);
            assertTaskPreservedAndCleaned(path, original);
        }

        private string createZip(params (string Name, string Content)[] entries) => createZip(entries, null, null);

        private string createZip((string Name, string Content)[] entries, int? attributes = null, Encoding? entryNameEncoding = null)
        {
            string task = storage.GetFullPath($"mania-downloads/{Guid.NewGuid():N}");
            Directory.CreateDirectory(task);
            string path = Path.Combine(task, "package.osz");
            using var source = File.Create(path);
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

        private static void modifyZipHeaders(string path, Action<byte[], int, bool> modify)
        {
            byte[] bytes = File.ReadAllBytes(path);
            for (int offset = 0; offset < bytes.Length - 46; offset++)
            {
                uint signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
                if (signature is 0x04034b50 or 0x02014b50)
                    modify(bytes, offset, signature == 0x02014b50);
            }
            File.WriteAllBytes(path, bytes);
        }

        private static string createChart(int setId, int beatmapId, int mode = 3) => $@"osu file format v14

[General]
AudioFilename: audio.mp3
Mode: {mode}

[Metadata]
Title: Downloaded mania song
Artist: OMS
Creator: Test author
Version: 4K
BeatmapID: {beatmapId}
BeatmapSetID: {setId}

[Difficulty]
HPDrainRate: 5
CircleSize: 4
OverallDifficulty: 5
ApproachRate: 5
SliderMultiplier: 1.4
SliderTickRate: 1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
";

        private static string md5(string text) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private static void assertTaskPreservedAndCleaned(string path, byte[] original)
        {
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!), Is.EquivalentTo(new[] { path }));
        }
    }
}
