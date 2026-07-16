// Copyright (c) OMS contributors. Licensed under the MIT Licence.

#pragma warning disable CS0436 // The test project intentionally compiles the shared entry point as a linked source file.

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Tests.Skinning;
using osu.Game.Tests;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class VisualTestRunnerArgumentTest
    {
        [TestCase]
        [TestCase("--unknown-ide-argument")]
        [TestCase("--unknown-ide-argument", "value")]
        [TestCase("--storage-name", "oms")]
        [TestCase("--test", "Some.Legacy.Scene")]
        public void TestNonExactArgumentsPreserveLegacyMode(params string[] args)
        {
            bool parsed = VisualTestRunner.TryParseExactTestArguments(args, out string? testName, out string? error);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(testName, Is.Null);
                Assert.That(error, Is.Null);
            });
        }

        [Test]
        public void TestExactArgumentsAndIsolationPolicy()
        {
            bool parsed = VisualTestRunner.TryParseExactTestArguments(
                new[] { VisualTestRunner.EXACT_TEST_ARGUMENT, "Some.Namespace.TestScene" },
                out string? testName,
                out string? error);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(testName, Is.EqualTo("Some.Namespace.TestScene"));
                Assert.That(error, Is.Null);
                Assert.That(TestSceneBmsManagedPackageNoteVisualGate.IsExecutionIsolated(isHeadlessHost: false, hasExactIsolationMarker: false), Is.False);
                Assert.That(TestSceneBmsManagedPackageNoteVisualGate.IsExecutionIsolated(isHeadlessHost: false, hasExactIsolationMarker: true), Is.True);
                Assert.That(TestSceneBmsManagedPackageNoteVisualGate.IsExecutionIsolated(isHeadlessHost: true, hasExactIsolationMarker: false), Is.True);
            });
        }

        [TestCase("--exact-test")]
        [TestCase("--exact-test", "")]
        [TestCase("--exact-test", "   ")]
        [TestCase("--unknown", "--exact-test", "Some.Namespace.TestScene")]
        [TestCase("--exact-test", "Some.Namespace.TestScene", "--unknown")]
        [TestCase("--exact-test", "Some.Namespace.TestScene", "--exact-test", "Another.Scene")]
        public void TestMalformedExactArgumentsFailClosed(params string[] args)
        {
            bool parsed = VisualTestRunner.TryParseExactTestArguments(args, out string? testName, out string? error);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.False);
                Assert.That(testName, Is.Null);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public void TestExactHostStorageResolvesToUniqueDirectChild()
        {
            string applicationDataRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"oms-appdata-root-{Guid.NewGuid():N}"));
            string storageName = $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid():N}";

            bool resolved = VisualTestRunner.TryResolveExactHostStoragePath(applicationDataRoot, storageName, out string? storagePath);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.True);
                Assert.That(storagePath, Is.EqualTo(Path.Combine(applicationDataRoot, storageName)));
                Assert.That(Path.GetDirectoryName(storagePath), Is.EqualTo(applicationDataRoot));
                Assert.That(Path.GetFileName(storagePath), Is.EqualTo(storageName));
            });
        }

        [Test]
        public void TestExactHostStorageRejectsTraversalAndNameAliases()
        {
            string applicationDataRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"oms-appdata-root-{Guid.NewGuid():N}"));
            string validName = $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid():N}";
            string[] invalidNames =
            {
                "oms",
                VisualTestRunner.EXACT_HOST_STORAGE_PREFIX,
                $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}not-a-guid",
                $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid():D}",
                $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX.ToUpperInvariant()}{Guid.NewGuid():N}",
                $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid().ToString("N").ToUpperInvariant()}",
                $"..{Path.DirectorySeparatorChar}{validName}",
                $"{validName}{Path.DirectorySeparatorChar}..",
                Path.Combine(applicationDataRoot, validName),
                $"{validName}."
            };

            foreach (string invalidName in invalidNames)
            {
                Assert.That(
                    VisualTestRunner.TryResolveExactHostStoragePath(applicationDataRoot, invalidName, out string? storagePath),
                    Is.False,
                    invalidName);
                Assert.That(storagePath, Is.Null, invalidName);
            }
        }

        [Test]
        public void TestExactHostStorageRejectsRootAliases()
        {
            string applicationDataRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"oms-appdata-root-{Guid.NewGuid():N}"));
            string storageName = $"{VisualTestRunner.EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid():N}";
            string aliasedRoot = Path.Combine(applicationDataRoot, "child", "..");

            Assert.Multiple(() =>
            {
                Assert.That(VisualTestRunner.TryResolveExactHostStoragePath(aliasedRoot, storageName, out string? aliasedPath), Is.False);
                Assert.That(aliasedPath, Is.Null);
                Assert.That(VisualTestRunner.TryResolveExactHostStoragePath("relative-appdata", storageName, out string? relativePath), Is.False);
                Assert.That(relativePath, Is.Null);
            });
        }

        [Test]
        public void TestExactHostStorageCleanupDeletesNormalTree()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"oms-exact-host-cleanup-{Guid.NewGuid():N}");
            string hostStorage = Path.Combine(tempRoot, "host");
            string nestedDirectory = Path.Combine(hostStorage, "one", "two");
            string readOnlyFile = Path.Combine(nestedDirectory, "framework.ini");

            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(readOnlyFile, "test");
            File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

            try
            {
                VisualTestRunner.DeleteExactHostStorage(hostStorage);
                Assert.That(Directory.Exists(hostStorage), Is.False);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void TestExactHostStorageCleanupRefusesJunctionAndPreservesTarget()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"oms-exact-host-reparse-{Guid.NewGuid():N}");
            string hostStorage = Path.Combine(tempRoot, "host");
            string externalTarget = Path.Combine(tempRoot, "external");
            string junction = Path.Combine(hostStorage, "linked");
            string marker = Path.Combine(externalTarget, "marker.txt");

            Directory.CreateDirectory(hostStorage);
            Directory.CreateDirectory(externalTarget);
            File.WriteAllText(marker, "must survive");

            try
            {
                createDirectoryJunctionOrIgnore(junction, externalTarget);

                Assert.Throws<IOException>(() => VisualTestRunner.DeleteExactHostStorage(hostStorage));
                Assert.That(File.ReadAllText(marker), Is.EqualTo("must survive"));
            }
            finally
            {
                deleteReparsePointIfPresent(junction);

                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
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
                Assert.Ignore($"Directory junctions are unavailable in this Windows environment: {standardOutput}{Environment.NewLine}{standardError}");

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
    }
}

#pragma warning restore CS0436
