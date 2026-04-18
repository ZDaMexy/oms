// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaImportIntegrationTest
    {
        [Test]
        public async Task TestRegisterExternalDirectoryIgnoresNonManiaBeatmapsInMixedFolder()
        {
            using var storage = new TemporaryNativeStorage($"mania-external-mixed-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "mixed-folder",
                ("standard.osu", standardOsuFile),
                ("mania.osu", maniaOsuFile));

            var importer = new ManiaFolderImporter(storage, realm);
            var result = await importer.RegisterExternalDirectory(importRoot).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
            Assert.That(result.SkippedBeatmapFiles, Is.EqualTo(new[] { "standard.osu" }));

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.IsExternalFilesystemStorage, Is.True);
                    Assert.That(set.FilesystemStoragePath, Is.EqualTo(Path.GetFullPath(importRoot)));
                    Assert.That(set.Beatmaps, Has.Count.EqualTo(1));
                    Assert.That(set.Beatmaps.Single().Ruleset.ShortName, Is.EqualTo(ManiaRuleset.SHORT_NAME));
                    Assert.That(set.Beatmaps.Single().Path, Is.EqualTo("mania.osu"));
                });
            });

            Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(set => !set.DeletePending)), Is.EqualTo(1));
        }

        [Test]
        public async Task TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull()
        {
            using var storage = new TemporaryNativeStorage($"mania-external-standard-only-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string importRoot = createImportSource(storage, "standard-only", ("standard.osu", standardOsuFile));

            var importer = new ManiaFolderImporter(storage, realm);
            var result = await importer.RegisterExternalDirectory(importRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result.ImportedBeatmapSet, Is.Null);
                Assert.That(result.SkippedBeatmapFiles, Is.EqualTo(new[] { "standard.osu" }));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(set => !set.DeletePending)), Is.Zero);
            });
        }

        [Test]
        public async Task TestRegisterManagedDirectoryPreservesRelativeManagedPath()
        {
            using var storage = new TemporaryNativeStorage($"mania-managed-readonly-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            string managedRoot = Path.Combine(storage.GetFullPath(ManiaFolderImporter.MANIA_STORAGE_PATH), "packs", "managed-set");
            Directory.CreateDirectory(managedRoot);
            File.WriteAllText(Path.Combine(managedRoot, "chart.osu"), maniaOsuFile);

            var importer = new ManiaFolderImporter(storage, realm);
            var result = await importer.RegisterManagedDirectory(managedRoot).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            result.ImportedBeatmapSet!.PerformRead(set =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(set.Files.Count, Is.EqualTo(0));
                    Assert.That(set.IsExternalFilesystemStorage, Is.False);
                    Assert.That(set.FilesystemStoragePath, Is.EqualTo("chartmania/packs/managed-set"));
                    Assert.That(set.Beatmaps, Has.Count.EqualTo(1));
                    Assert.That(set.Beatmaps.Single().Path, Is.EqualTo("chart.osu"));
                });
            });

            Assert.That(File.Exists(Path.Combine(managedRoot, "chart.osu")), Is.True);
        }

        private static string createImportSource(Storage storage, string directoryName, params (string fileName, string contents)[] files)
        {
            string sourceRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(sourceRoot);

            foreach (var (fileName, contents) in files)
                File.WriteAllText(Path.Combine(sourceRoot, fileName), contents);

            return sourceRoot;
        }

        private const string standardOsuFile = @"osu file format v14

[General]
Mode: 0
";

        private const string maniaOsuFile = @"osu file format v14

[General]
AudioFilename: audio.mp3
Mode: 3

[Metadata]
Title:Filesystem Test
Artist:OMS
Creator:Tester
Version:4K

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
";
    }
}
