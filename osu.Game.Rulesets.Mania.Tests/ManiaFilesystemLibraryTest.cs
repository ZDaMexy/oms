// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaFilesystemLibraryTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task TestIncrementalChecksSkipOnlyAvailableDirectory(bool external)
        {
            using var storage = new TemporaryNativeStorage($"mania-incremental-{Guid.NewGuid():N}");
            string directory = storage.GetFullPath(external ? "external/set" : "chartmania/set");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "chart.osu"), chart);
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new ManiaFolderImporter(storage, realm);
            var registered = external
                ? await importer.RegisterExternalDirectory(directory).ConfigureAwait(false)
                : await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
            bool shouldImport() => external ? importer.ShouldImportExternalDirectory(directory.ToUpperInvariant()) : importer.ShouldImportManagedDirectory(directory.ToUpperInvariant());
            Assert.That(shouldImport(), Is.False);
            registered.ImportedBeatmapSet!.PerformWrite(set => set.FilesystemUnavailable = true);
            Assert.That(shouldImport(), Is.True);
            registered.ImportedBeatmapSet.PerformWrite(set =>
            {
                set.FilesystemUnavailable = false;
                set.DeletePending = true;
            });
            Assert.That(shouldImport(), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "chart.osu")), Is.True);
        }

        [Test]
        public async Task TestDuplicateSourcesAndUnavailableRecoveryPreserveManagedFiles()
        {
            using var storage = new TemporaryNativeStorage($"mania-library-{Guid.NewGuid():N}");
            string managed = storage.GetFullPath("chartmania/set");
            string external = storage.GetFullPath("external/set");
            Directory.CreateDirectory(managed);
            Directory.CreateDirectory(external);
            File.WriteAllText(Path.Combine(managed, "chart.osu"), chart);
            File.WriteAllText(Path.Combine(external, "chart.osu"), chart);
            Guid id;
            using (var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                using var rulesets = new RealmRulesetStore(realm, storage);
                var importer = new ManiaFolderImporter(storage, realm);
                var first = await importer.RegisterManagedDirectory(managed).ConfigureAwait(false);
                id = first.ImportedBeatmapSet!.PerformRead(s => s.ID);
                var second = await importer.RegisterExternalDirectory(external).ConfigureAwait(false);
                Assert.That(second.ImportedBeatmapSet!.PerformRead(s => s.ID), Is.Not.EqualTo(id));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.DeletePending && !s.FilesystemUnavailable)), Is.EqualTo(2));
                var index = new FilesystemBeatmapIndex(storage, realm);
                index.Reconcile(storage.GetFullPath("chartmania"), ExternalLibraryRootType.Mania, false, Array.Empty<string>());
                Assert.That(importer.ShouldImportManagedDirectory(managed), Is.True);
                File.Move(Path.Combine(managed, "chart.osu"), Path.Combine(managed, "renamed.osu"));
                var recovered = await importer.RegisterManagedDirectory(managed).ConfigureAwait(false);
                Assert.That(recovered.ImportedBeatmapSet!.PerformRead(s => s.ID), Is.EqualTo(id));
                Assert.That(recovered.ImportedBeatmapSet.PerformRead(s => s.Beatmaps[0].LocalFilePath), Is.EqualTo("renamed.osu"));
            }

            using (var reopened = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
            {
                Assert.That(reopened.Run(r => r.Find<BeatmapSetInfo>(id)!.FilesystemUnavailable), Is.False);
                Assert.That(File.Exists(Path.Combine(managed, "renamed.osu")), Is.True);
                Assert.That(File.Exists(Path.Combine(external, "chart.osu")), Is.True);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestRegistrationRejectsDirectoryWithoutValidManiaCharts(bool external)
        {
            using var storage = new TemporaryNativeStorage($"mania-invalid-{Guid.NewGuid():N}");
            string directory = storage.GetFullPath(external ? "external" : "chartmania/set");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "broken.osu"), "not a valid osu file");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new ManiaFolderImporter(storage, realm);
            Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                if (external)
                    await importer.RegisterExternalDirectory(directory).ConfigureAwait(false);
                else
                    await importer.RegisterManagedDirectory(directory).ConfigureAwait(false);
            });
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.Zero);
            Assert.That(File.Exists(Path.Combine(directory, "broken.osu")), Is.True);
        }

        [Test]
        public async Task TestLockedManiaFileReportsFailureAndPreservesMissingSourceIndex()
        {
            using var storage = new TemporaryNativeStorage($"mania-locked-scan-{Guid.NewGuid():N}");
            string root = storage.GetFullPath("external");
            string first = Path.Combine(root, "first");
            string second = Path.Combine(root, "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            string lockedChart = Path.Combine(first, "chart.osu");
            string missingChart = Path.Combine(second, "chart.osu");
            File.WriteAllText(lockedChart, chart);
            File.WriteAllText(missingChart, chart);
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);
            var importer = new ManiaFolderImporter(storage, realm);
            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(root, ExternalLibraryRootType.Mania);
            var scanner = new ExternalLibraryScanner(config, new FilesystemBeatmapIndex(storage, realm))
            {
                ManiaDirectoryImporter = (path, ct) => importer.RegisterExternalDirectory(path, ct),
            };
            await scanner.ScanAllRoots().ConfigureAwait(false);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));
            File.Delete(missingChart);
            using (File.Open(lockedChart, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var failed = await scanner.ScanAllRoots().ConfigureAwait(false);
                Assert.That(failed.Errors, Is.EqualTo(1));
                Assert.That(failed.Imported, Is.Zero);
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(2));
            }

            var recovered = await scanner.ScanAllRoots().ConfigureAwait(false);
            Assert.That(recovered.Errors, Is.Zero);
            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.FilesystemUnavailable)), Is.EqualTo(1));
            Assert.That(File.Exists(lockedChart), Is.True);
        }

        private const string chart = @"osu file format v14

[General]
Mode: 3

[Metadata]
Title: Library test
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
";
    }
}
