// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class ExternalLibraryScannerTest
    {
        [Test]
        public async Task TestProgressStaysBelowCompleteUntilLastRootFinishes()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestProgressStaysBelowCompleteUntilLastRootFinishes));

            string firstRoot = createBmsRoot(storage, "root-a", "set-a");
            string secondRoot = createBmsRoot(storage, "root-b", "set-b1", "set-b2");

            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(firstRoot, ExternalLibraryRootType.BMS);
            config.AddRoot(secondRoot, ExternalLibraryRootType.BMS);

            var scanner = new ExternalLibraryScanner(config)
            {
                BmsDirectoryImporter = (_, _) => Task.CompletedTask,
            };

            var progressEvents = new List<ExternalLibraryScanner.ScanProgress>();

            var result = await scanner.ScanAllRoots(new ImmediateProgress<ExternalLibraryScanner.ScanProgress>(p => progressEvents.Add(p)));

            Assert.That(result.Imported, Is.EqualTo(3));
            Assert.That(result.Skipped, Is.EqualTo(0));
            Assert.That(result.Errors, Is.EqualTo(0));

            var secondRootStart = progressEvents.Single(p => p.RootIndex == 1 && p.CurrentDirectory == null && p.ProcessedDirectories == 0);

            Assert.That(secondRootStart.TotalDirectories, Is.EqualTo(3));
            Assert.That(secondRootStart.OverallProgress, Is.EqualTo(0.5f).Within(0.0001f));

            var secondRootActiveDirectory = progressEvents.First(p => p.RootIndex == 1 && p.CurrentDirectory != null && p.ProcessedDirectories == 1);

            Assert.That(secondRootActiveDirectory.OverallProgress, Is.LessThan(1f));
            Assert.That(progressEvents.Last().OverallProgress, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public async Task TestProgressReportsImportedSkippedAndErroredCounts()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestProgressReportsImportedSkippedAndErroredCounts));

            string root = storage.GetFullPath("root");
            Directory.CreateDirectory(root);

            createOsuDirectory(root, "valid-a", mania_osu_file);
            createOsuDirectory(root, "skip-me", standard_osu_file);
            createOsuDirectory(root, "error-me", mania_osu_file);

            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(root, ExternalLibraryRootType.Mania);

            var scanner = new ExternalLibraryScanner(config)
            {
                ManiaDirectoryImporter = (directory, _) => Path.GetFileName(directory) == "error-me"
                    ? Task.FromException(new IOException("boom"))
                    : Task.CompletedTask,
            };

            var progressEvents = new List<ExternalLibraryScanner.ScanProgress>();

            var result = await scanner.ScanAllRoots(new ImmediateProgress<ExternalLibraryScanner.ScanProgress>(p => progressEvents.Add(p)), CancellationToken.None);

            Assert.That(result.Imported, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.Errors, Is.EqualTo(1));

            Assert.That(progressEvents.Any(p => p.ImportedSoFar == 1), Is.True);
            Assert.That(progressEvents.Any(p => p.SkippedSoFar == 1), Is.True);
            Assert.That(progressEvents.Any(p => p.ErrorsSoFar == 1), Is.True);
            Assert.That(progressEvents.Last().ProcessedDirectories, Is.EqualTo(4));
        }

        [Test]
        public async Task TestManiaScanSkipsDirectoriesWithoutModeThreeBeatmaps()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestManiaScanSkipsDirectoriesWithoutModeThreeBeatmaps));

            string root = storage.GetFullPath("mania-root");
            Directory.CreateDirectory(root);

            createOsuDirectory(root, "standard-set", standard_osu_file);
            createOsuDirectory(root, "mania-set", mania_osu_file);

            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(root, ExternalLibraryRootType.Mania);

            var scanner = new ExternalLibraryScanner(config)
            {
                ManiaDirectoryImporter = (_, _) => Task.CompletedTask,
            };

            var result = await scanner.ScanAllRoots(cancellationToken: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Imported, Is.EqualTo(1));
                Assert.That(result.Skipped, Is.EqualTo(1));
                Assert.That(result.Errors, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestRecursiveScanIncludesRootAndNestedBeatmapDirectories()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestRecursiveScanIncludesRootAndNestedBeatmapDirectories));

            string root = storage.GetFullPath("recursive-root");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "root-chart.bms"), string.Empty);

            string nestedCategory = Path.Combine(root, "packs");
            Directory.CreateDirectory(nestedCategory);
            createBeatmapDirectory(nestedCategory, "nested-set", ".bms");

            var config = new ExternalLibraryConfig(storage);
            config.AddRoot(root, ExternalLibraryRootType.BMS);

            var importedDirectories = new List<string>();
            var scanner = new ExternalLibraryScanner(config)
            {
                BmsDirectoryImporter = (directory, _) =>
                {
                    importedDirectories.Add(directory);
                    return Task.CompletedTask;
                },
            };

            var result = await scanner.ScanAllRoots(cancellationToken: CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Imported, Is.EqualTo(2));
                Assert.That(result.Skipped, Is.Zero);
                Assert.That(result.Errors, Is.Zero);
                Assert.That(importedDirectories, Is.EquivalentTo(new[]
                {
                    root,
                    Path.Combine(nestedCategory, "nested-set"),
                }));
            });
        }

        private static string createBmsRoot(TemporaryNativeStorage storage, string rootName, params string[] setNames)
        {
            string root = storage.GetFullPath(rootName);
            Directory.CreateDirectory(root);

            foreach (string setName in setNames)
                createBeatmapDirectory(root, setName, ".bms");

            return root;
        }

        private static void createBeatmapDirectory(string root, string directoryName, string extension)
        {
            string directory = Path.Combine(root, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"chart{extension}"), string.Empty);
        }

        private static void createOsuDirectory(string root, string directoryName, string contents)
        {
            string directory = Path.Combine(root, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "chart.osu"), contents);
        }

        private const string standard_osu_file = @"osu file format v14

[General]
Mode: 0
";

        private const string mania_osu_file = @"osu file format v14

[General]
Mode: 3
";

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> report;

            public ImmediateProgress(Action<T> report)
            {
                this.report = report;
            }

            public void Report(T value) => report(value);
        }
    }
}
