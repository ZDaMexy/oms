// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using SixLabors.ImageSharp;

namespace osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate
{
    [TestFixture]
    public class BmsNoteAnimationManualGateGeneratorTest
    {
        [Test]
        public void TestGenerateAndValidateManualGateArtifacts()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"oms-bms-note-animation-gate-{Guid.NewGuid():N}");

            try
            {
                string first = Path.Combine(tempRoot, "first");
                string second = Path.Combine(tempRoot, "second");
                BmsNoteAnimationManualGateGenerator.Generate(first);
                BmsNoteAnimationManualGateGenerator.Generate(second);

                assertTreesEqual(first, second);
                assertGoodPackage(Path.Combine(first, BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME));
                assertBrokenPackage(Path.Combine(first, BmsNoteAnimationManualGateGenerator.BROKEN_PACKAGE_FILENAME));
                assertChart(Path.Combine(first, "chartbms", "bms-note-animation-manual-gate", BmsNoteAnimationManualGateGenerator.CHART_FILENAME));

                string? requestedOutput = Environment.GetEnvironmentVariable(BmsNoteAnimationManualGateGenerator.OUTPUT_ENVIRONMENT_VARIABLE);

                if (!string.IsNullOrWhiteSpace(requestedOutput))
                {
                    string exported = Path.GetFullPath(requestedOutput);
                    BmsNoteAnimationManualGateGenerator.Generate(exported);
                    TestContext.Progress.WriteLine($"Manual gate artifacts generated at: {exported}");
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void TestImportStagingRerunPreservesUnrelatedFilesAndSources()
        {
            string tempRoot = createTempRoot();

            try
            {
                string output = Path.Combine(tempRoot, "output");
                BmsNoteAnimationManualGateGenerator.Generate(output);

                string[] sourcePaths =
                {
                    Path.Combine(output, BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME),
                    Path.Combine(output, BmsNoteAnimationManualGateGenerator.BROKEN_PACKAGE_FILENAME),
                    Path.Combine(output, "chartbms", "bms-note-animation-manual-gate", BmsNoteAnimationManualGateGenerator.CHART_FILENAME),
                    Path.Combine(output, "SHA256SUMS.txt"),
                };
                string[] sourceHashes = sourcePaths.Select(hash).ToArray();
                string staging = Path.Combine(output, "import-staging");
                string stagedGood = Path.Combine(staging, BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME);
                string stagedBroken = Path.Combine(staging, BmsNoteAnimationManualGateGenerator.BROKEN_PACKAGE_FILENAME);
                string unrelated = Path.Combine(staging, "keep-me.txt");

                Directory.CreateDirectory(staging);
                File.WriteAllText(stagedGood, "stale good package");
                File.WriteAllText(stagedBroken, "stale broken package");
                File.WriteAllText(unrelated, "unrelated content");

                assertStageOnlySucceeds(output);
                assertStagingAndSources();

                File.WriteAllText(stagedGood, "stale good package again");
                File.WriteAllText(stagedBroken, "stale broken package again");

                assertStageOnlySucceeds(output);
                assertStagingAndSources();

                void assertStagingAndSources()
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(hash(stagedGood), Is.EqualTo(hash(sourcePaths[0])));
                        Assert.That(hash(stagedBroken), Is.EqualTo(hash(sourcePaths[1])));
                        Assert.That(File.ReadAllText(unrelated), Is.EqualTo("unrelated content"));
                        Assert.That(sourcePaths.Select(hash), Is.EqualTo(sourceHashes), "Deterministic source artifacts and SHA256SUMS must remain unchanged.");
                    });
                }
            }
            finally
            {
                deleteTempRoot(tempRoot);
            }
        }

        [Test]
        public void TestImportStagingRejectsNonDirectory()
        {
            string tempRoot = createTempRoot();

            try
            {
                string output = Path.Combine(tempRoot, "output");
                BmsNoteAnimationManualGateGenerator.Generate(output);
                File.WriteAllText(Path.Combine(output, "import-staging"), "not a directory");

                assertStageOnlyFails(output, "exists but is not a directory");
            }
            finally
            {
                deleteTempRoot(tempRoot);
            }
        }

        [Test]
        public void TestImportStagingRejectsKnownFilePathOccupiedByDirectory()
        {
            string tempRoot = createTempRoot();

            try
            {
                string output = Path.Combine(tempRoot, "output");
                BmsNoteAnimationManualGateGenerator.Generate(output);
                Directory.CreateDirectory(Path.Combine(output, "import-staging", BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME));

                assertStageOnlyFails(output, "file path is occupied by a directory");
            }
            finally
            {
                deleteTempRoot(tempRoot);
            }
        }

        [Test]
        public void TestImportStagingRejectsReparsePoint()
        {
            string tempRoot = createTempRoot();
            string output = Path.Combine(tempRoot, "output");
            string staging = Path.Combine(output, "import-staging");

            try
            {
                BmsNoteAnimationManualGateGenerator.Generate(output);
                string target = Path.Combine(tempRoot, "staging-target");
                Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(target, "marker.txt"), "target remains intact");

                createDirectoryJunctionOrIgnore(staging, target);
                assertStageOnlyFails(output, "must not be a reparse point");
                Assert.That(File.ReadAllText(Path.Combine(target, "marker.txt")), Is.EqualTo("target remains intact"));
            }
            finally
            {
                deleteReparsePointIfPresent(staging);
                deleteTempRoot(tempRoot);
            }
        }

        [Test]
        public void TestImportStagingRejectsKnownFileReparsePoint()
        {
            string tempRoot = createTempRoot();
            string output = Path.Combine(tempRoot, "output");
            string stagedGood = Path.Combine(output, "import-staging", BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME);

            try
            {
                BmsNoteAnimationManualGateGenerator.Generate(output);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedGood)!);
                string target = Path.Combine(tempRoot, "known-file-target");
                Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(target, "marker.txt"), "target remains intact");

                createDirectoryJunctionOrIgnore(stagedGood, target);
                assertStageOnlyFails(output, "file must not be a reparse point");
                Assert.That(File.ReadAllText(Path.Combine(target, "marker.txt")), Is.EqualTo("target remains intact"));
            }
            finally
            {
                deleteReparsePointIfPresent(stagedGood);
                deleteTempRoot(tempRoot);
            }
        }

        private static void assertTreesEqual(string first, string second)
        {
            string[] firstFiles = relativeFiles(first);
            string[] secondFiles = relativeFiles(second);
            Assert.That(secondFiles, Is.EqualTo(firstFiles));

            foreach (string relativePath in firstFiles)
            {
                Assert.That(hash(Path.Combine(second, relativePath)), Is.EqualTo(hash(Path.Combine(first, relativePath))), relativePath);
            }
        }

        private static void assertGoodPackage(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(names, Has.Length.EqualTo(BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT + 1));
                Assert.That(names, Does.Contain("skin.ini"));
                Assert.That(names, Does.Contain("notes/ordinary-0.png"));
                Assert.That(names, Does.Contain($"notes/ordinary-{BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT - 1}.png"));
                Assert.That(names, Does.Not.Contain("notes/ordinary.png"));
            });

            for (int i = 0; i < BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT; i++)
            {
                ZipArchiveEntry entry = archive.GetEntry($"notes/ordinary-{i}.png")!;
                using Stream stream = entry.Open();
                ImageInfo? info = Image.Identify(stream);

                Assert.Multiple(() =>
                {
                    Assert.That(info, Is.Not.Null, entry.FullName);
                    Assert.That(info!.Width, Is.EqualTo(BmsNoteAnimationManualGateGenerator.FRAME_WIDTH), entry.FullName);
                    Assert.That(info.Height, Is.EqualTo(BmsNoteAnimationManualGateGenerator.FRAME_HEIGHT), entry.FullName);
                });
            }
        }

        private static void assertBrokenPackage(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "notes/ordinary-1.png", "skin.ini" }));
            Assert.That(archive.GetEntry("notes/ordinary-0.png"), Is.Null);
            Assert.That(archive.GetEntry("notes/ordinary.png"), Is.Null);
        }

        private static void assertChart(string path)
        {
            string text = File.ReadAllText(path);
            BmsDecodedChart decoded = new BmsBeatmapDecoder().DecodeText(text, BmsNoteAnimationManualGateGenerator.CHART_FILENAME);
            int[] expectedChannels = { 0x11, 0x12, 0x13, 0x14, 0x15, 0x18, 0x19 };

            Assert.Multiple(() =>
            {
                Assert.That(decoded.BeatmapInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(decoded.BeatmapInfo.Title, Is.EqualTo("OMS BMS Note Animation Manual Gate"));
                Assert.That(decoded.ObjectEvents.Count, Is.GreaterThan(90));
                Assert.That(decoded.ObjectEvents.Select(note => note.Channel).Distinct(), Is.SupersetOf(expectedChannels));
                Assert.That(decoded.Warnings, Is.Empty);
            });
        }

        private static string[] relativeFiles(string root)
            => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(root, path))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();

        private static string hash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static string createTempRoot()
        {
            string path = Path.Combine(Path.GetTempPath(), $"oms-bms-note-animation-staging-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void assertStageOnlySucceeds(string output)
        {
            ScriptResult result = runStageOnly(output);
            Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
        }

        private static void assertStageOnlyFails(string output, string expectedError)
        {
            ScriptResult result = runStageOnly(output);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.Not.Zero, result.CombinedOutput);
                Assert.That(result.CombinedOutput, Does.Contain(expectedError));
            });
        }

        private static ScriptResult runStageOnly(string output)
        {
            string script = findRepositoryFile("GenerateBmsNoteAnimationManualGate.ps1");
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = powershell,
                    WorkingDirectory = Path.GetDirectoryName(script)!,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-File");
            process.StartInfo.ArgumentList.Add(script);
            process.StartInfo.ArgumentList.Add("-OutputDirectory");
            process.StartInfo.ArgumentList.Add(output);
            process.StartInfo.ArgumentList.Add("-StageOnly");

            Assert.That(process.Start(), Is.True);
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Timed out while running the manual-gate staging script.");
            }

            return new ScriptResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
        }

        private static string findRepositoryFile(string filename)
        {
            for (DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, filename);

                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException($"Could not locate repository file {filename} from the test output directory.");
        }

        private static void createDirectoryJunctionOrIgnore(string linkPath, string targetPath)
        {
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = powershell,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-Command");
            process.StartInfo.ArgumentList.Add("& { param($linkPath, $targetPath) $null = New-Item -ItemType Junction -Path $linkPath -Target $targetPath -ErrorAction Stop }");
            process.StartInfo.ArgumentList.Add(linkPath);
            process.StartInfo.ArgumentList.Add(targetPath);

            Assert.That(process.Start(), Is.True);
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Assert.Ignore($"Directory junctions are unavailable in this Windows environment: {standardOutput}{Environment.NewLine}{standardError}");
            }

            Assert.That(File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint), Is.True);
        }

        private static void deleteReparsePointIfPresent(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);

                if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                    return;

                if (attributes.HasFlag(FileAttributes.Directory))
                    Directory.Delete(path);
                else
                    File.Delete(path);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
            }
        }

        private static void deleteTempRoot(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError)
        {
            public string CombinedOutput => $"{StandardOutput}{Environment.NewLine}{StandardError}";
        }
    }
}
