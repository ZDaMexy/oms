// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class FilesystemBeatmapLocationTest
    {
        [Test]
        public void TestHashBackedSetIsNotFilesystemBacked()
        {
            using var storage = new TemporaryNativeStorage("bms-location-hash");
            var set = new BeatmapSetInfo();
            var beatmap = new BeatmapInfo { BeatmapSet = set };

            Assert.Multiple(() =>
            {
                Assert.That(FilesystemBeatmapLocation.IsFilesystemBacked(set), Is.False);
                Assert.That(FilesystemBeatmapLocation.TryGetSetDirectory(set, storage, out _), Is.False);
                Assert.That(FilesystemBeatmapLocation.TryGetBeatmapFile(beatmap, storage, out _), Is.False);
                Assert.That(FilesystemBeatmapLocation.CreateOpenSongFolderItem(set, storage, null!), Is.Null);
                Assert.That(FilesystemBeatmapLocation.CreateOpenChartFileItem(beatmap, storage, null!), Is.Null);
            });
        }

        [Test]
        public void TestManagedSetResolvesRelativeToStorageRoot()
        {
            using var storage = new TemporaryNativeStorage("bms-location-managed");
            var set = new BeatmapSetInfo { FilesystemStoragePath = "chartbms/song-abc12345", IsExternalFilesystemStorage = false };

            Assert.Multiple(() =>
            {
                Assert.That(FilesystemBeatmapLocation.IsFilesystemBacked(set), Is.True);
                Assert.That(FilesystemBeatmapLocation.TryGetSetDirectory(set, storage, out string dir), Is.True);
                Assert.That(dir, Is.EqualTo(storage.GetFullPath("chartbms/song-abc12345")));
                // Managed paths are resolved through the data-storage root, never used verbatim.
                Assert.That(dir, Is.Not.EqualTo("chartbms/song-abc12345"));
                Assert.That(FilesystemBeatmapLocation.CreateOpenSongFolderItem(set, storage, null!), Is.Not.Null);
            });
        }

        [Test]
        public void TestExternalSetUsesAbsolutePathVerbatim()
        {
            using var storage = new TemporaryNativeStorage("bms-location-external");
            string external = Path.Combine(Path.GetTempPath(), "ExternalLib", "Song");
            var set = new BeatmapSetInfo { FilesystemStoragePath = external, IsExternalFilesystemStorage = true };

            Assert.Multiple(() =>
            {
                Assert.That(FilesystemBeatmapLocation.IsFilesystemBacked(set), Is.True);
                Assert.That(FilesystemBeatmapLocation.TryGetSetDirectory(set, storage, out string dir), Is.True);
                // External libraries store an absolute path; it must be used as-is (NOT joined to the data root).
                Assert.That(dir, Is.EqualTo(external));
            });
        }

        [Test]
        public void TestManagedBeatmapFileJoinsSetDirectoryWithLocalPath()
        {
            using var storage = new TemporaryNativeStorage("bms-location-managed-file");
            var set = new BeatmapSetInfo { FilesystemStoragePath = "chartbms/song-abc12345", IsExternalFilesystemStorage = false };
            var beatmap = new BeatmapInfo { LocalFilePath = "insane.bms", BeatmapSet = set };

            Assert.Multiple(() =>
            {
                Assert.That(FilesystemBeatmapLocation.TryGetBeatmapFile(beatmap, storage, out string file), Is.True);
                Assert.That(file, Is.EqualTo(Path.Combine(storage.GetFullPath("chartbms/song-abc12345"), "insane.bms")));
                Assert.That(FilesystemBeatmapLocation.CreateOpenChartFileItem(beatmap, storage, null!), Is.Not.Null);
            });
        }

        [Test]
        public void TestExternalBeatmapFileJoinsAbsoluteAndNormalisesSeparators()
        {
            using var storage = new TemporaryNativeStorage("bms-location-external-file");
            string external = Path.Combine(Path.GetTempPath(), "ExternalLib", "Song");
            var set = new BeatmapSetInfo { FilesystemStoragePath = external, IsExternalFilesystemStorage = true };
            // LocalFilePath is stored standardised (forward slashes) and must be nativised before joining.
            var beatmap = new BeatmapInfo { LocalFilePath = "sub/another.bme", BeatmapSet = set };

            Assert.That(FilesystemBeatmapLocation.TryGetBeatmapFile(beatmap, storage, out string file), Is.True);
            Assert.That(file, Is.EqualTo(Path.Combine(external, "sub", "another.bme")));
        }

        [Test]
        public void TestBeatmapWithoutLocalFilePathReturnsFalse()
        {
            using var storage = new TemporaryNativeStorage("bms-location-no-localpath");
            var set = new BeatmapSetInfo { FilesystemStoragePath = "chartbms/song-abc12345", IsExternalFilesystemStorage = false };
            var beatmap = new BeatmapInfo { BeatmapSet = set };

            Assert.That(FilesystemBeatmapLocation.TryGetBeatmapFile(beatmap, storage, out _), Is.False);
            Assert.That(FilesystemBeatmapLocation.CreateOpenChartFileItem(beatmap, storage, null!), Is.Null);
        }
    }
}
