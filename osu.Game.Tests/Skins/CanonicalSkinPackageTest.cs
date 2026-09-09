// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    public class CanonicalSkinPackageTest
    {
        private string root = null!;
        private string installation = null!;
        private string data = null!;
        private string original = null!;
        private string working => Path.Combine(data, CanonicalSkinPackage.WORKING_DIRECTORY, CanonicalSkinPackage.PACKAGE_FILENAME);
        private static readonly byte[] package_bytes = "ordinary author package bytes"u8.ToArray();

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), $"oms-canonical-{Guid.NewGuid():N}");
            installation = Path.Combine(root, "installation");
            data = Path.Combine(root, "custom data");
            Directory.CreateDirectory(installation);
            Directory.CreateDirectory(data);
            original = Path.Combine(installation, CanonicalSkinPackage.PACKAGE_FILENAME);
            File.WriteAllBytes(original, package_bytes);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, recursive: true);

        [Test]
        public void TestFirstInstallAndRestartUseCompleteWorkingCopyWithoutWritingOriginal()
        {
            DateTime originalTime = File.GetLastWriteTimeUtc(original);
            var first = prepare();
            DateTime workingTime = File.GetLastWriteTimeUtc(working);
            var restarted = prepare();
            Assert.Multiple(() =>
            {
                Assert.That(first.IsSuccess, Is.True);
                Assert.That(restarted.IsSuccess, Is.True);
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(package_bytes));
                Assert.That(File.ReadAllBytes(original), Is.EqualTo(package_bytes));
                Assert.That(File.GetLastWriteTimeUtc(original), Is.EqualTo(originalTime));
                Assert.That(File.GetLastWriteTimeUtc(working), Is.EqualTo(workingTime));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(working)!), Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestPortableDataRootBesideInstallationKeepsOriginalAndWorkingCopySeparate()
        {
            var result = CanonicalSkinPackage.PrepareArchive(installation, original, hash(package_bytes));
            string portableCopy = Path.Combine(installation, CanonicalSkinPackage.WORKING_DIRECTORY, CanonicalSkinPackage.PACKAGE_FILENAME);
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(File.ReadAllBytes(portableCopy), Is.EqualTo(package_bytes));
                Assert.That(File.ReadAllBytes(original), Is.EqualTo(package_bytes));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCorruptOrEmptyWorkingCopyRestoresAndPreservesPreviousContents(bool empty)
        {
            Assert.That(prepare().IsSuccess, Is.True);
            byte[] bad = empty ? Array.Empty<byte>() : "unrecognised previous data"u8.ToArray();
            File.WriteAllBytes(working, bad);
            var restored = prepare();
            string[] preserved = Directory.GetFiles(Path.GetDirectoryName(working)!, "*.preserved-*");
            Assert.Multiple(() =>
            {
                Assert.That(restored.IsSuccess, Is.True);
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(package_bytes));
                Assert.That(preserved, Has.Length.EqualTo(1));
                Assert.That(File.ReadAllBytes(preserved.Single()), Is.EqualTo(bad));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMissingOrCorruptInstallationCannotUseEvenAnIntactWorkingCopy(bool missing)
        {
            Assert.That(prepare().IsSuccess, Is.True);
            if (missing)
                File.Delete(original);
            else
                File.WriteAllText(original, "broken installation");
            var result = prepare();
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Failure, Is.EqualTo(missing
                    ? CanonicalSkinInstallationFailure.InstallationPackageUnavailable
                    : CanonicalSkinInstallationFailure.InstallationPackageInvalid));
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(package_bytes));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(working)!), Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestUpgradePublishesNewPackageAndRetainsPreviousPackageAndInterruptedFiles()
        {
            Assert.That(prepare().IsSuccess, Is.True);
            string interrupted = working + ".interrupted.tmp";
            File.WriteAllText(interrupted, "unowned interrupted write");
            byte[] next = "new ordinary author package"u8.ToArray();
            File.WriteAllBytes(original, next);
            var result = CanonicalSkinPackage.PrepareArchive(data, original, hash(next));
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(next));
                Assert.That(File.ReadAllText(interrupted), Is.EqualTo("unowned interrupted write"));
                Assert.That(File.ReadAllBytes(Directory.GetFiles(Path.GetDirectoryName(working)!, "*.preserved-*").Single()), Is.EqualTo(package_bytes));
            });
        }

        [Test]
        public void TestInterruptedAfterPreservingOldCopyRecoversWithoutGuessingOwnership()
        {
            Assert.That(prepare().IsSuccess, Is.True);
            string preserved = working + ".preserved-evidence";
            File.Move(working, preserved);
            string interrupted = working + ".complete-but-unpublished.tmp";
            File.WriteAllBytes(interrupted, package_bytes);
            Assert.That(prepare().IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(package_bytes));
                Assert.That(File.ReadAllBytes(preserved), Is.EqualTo(package_bytes));
                Assert.That(File.ReadAllBytes(interrupted), Is.EqualTo(package_bytes));
            });
        }

        [Test]
        public void TestDirectoryInWorkingFileSlotIsPreservedAndBlocksLoading()
        {
            Directory.CreateDirectory(working);
            string evidence = Path.Combine(working, "author-data.txt");
            File.WriteAllText(evidence, "keep me");
            Assert.Multiple(() =>
            {
                Assert.That(prepare().Failure, Is.EqualTo(CanonicalSkinInstallationFailure.WorkingCopyUnavailable));
                Assert.That(File.ReadAllText(evidence), Is.EqualTo("keep me"));
            });
        }

        [Test]
        public void TestLockedWorkingCopyIsPreservedAndCanBeRestoredAfterUnlock()
        {
            Assert.That(prepare().IsSuccess, Is.True);
            File.WriteAllText(working, "bad copy");
            using (File.Open(working, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.That(prepare().Failure, Is.EqualTo(CanonicalSkinInstallationFailure.WorkingCopyUnavailable));
            Assert.That(File.ReadAllText(working), Is.EqualTo("bad copy"));
            Assert.That(prepare().IsSuccess, Is.True);
        }

        [Test]
        public void TestWorkingDirectoryJunctionCannotModifyAuthorDirectory()
        {
            string external = Path.Combine(root, "external author");
            Directory.CreateDirectory(external);
            string authorFile = Path.Combine(external, "author.txt");
            File.WriteAllText(authorFile, "external author data");
            DateTime before = File.GetLastWriteTimeUtc(authorFile);
            string link = Path.GetDirectoryName(working)!;
            string script = Path.Combine(root, "create-junction.ps1");
            File.WriteAllText(script, "param([string]$linkPath, [string]$targetPath)\n$null = New-Item -ItemType Junction -Path $linkPath -Target $targetPath -ErrorAction Stop\n");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            foreach (string argument in new[]
                     {
                         "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, link, external,
                     })
                process.StartInfo.ArgumentList.Add(argument);
            Assert.That(process.Start(), Is.True);
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(prepare().Failure, Is.EqualTo(CanonicalSkinInstallationFailure.WorkingCopyUnavailable));
                    Assert.That(Directory.GetFiles(external), Has.Length.EqualTo(1));
                    Assert.That(File.ReadAllText(authorFile), Is.EqualTo("external author data"));
                    Assert.That(File.GetLastWriteTimeUtc(authorFile), Is.EqualTo(before));
                });
            }
            finally
            {
                Directory.Delete(link);
            }
        }

        [Test]
        public void TestRepairNeverChangesOtherHardLinkToWorkingCopyContents()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(working)!);
            string authorFile = Path.Combine(root, "author-file.txt");
            File.WriteAllText(authorFile, "author content");
            Assert.That(HardLinkHelper.TryCreateHardLink(working, authorFile), Is.True);
            Assert.That(prepare().IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(authorFile), Is.EqualTo("author content"));
                Assert.That(File.ReadAllBytes(working), Is.EqualTo(package_bytes));
                Assert.That(File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(working)!, "*.preserved-*").Single()), Is.EqualTo("author content"));
            });
        }

        [TestCase("")]
        [TestCase("bad")]
        public void TestInvalidInstallationManifestCannotCreateWorkingCopy(string manifest)
        {
            Assert.That(CanonicalSkinPackage.PrepareArchive(data, original, manifest).Failure,
                Is.EqualTo(CanonicalSkinInstallationFailure.InstallationManifestInvalid));
            Assert.That(Directory.GetFileSystemEntries(data), Is.Empty);
        }

        [Test]
        public void TestProtectedFallbackRecordsAcceptExactSupportedLegacyAndCanonicalOnly()
        {
            SkinInfo legacy = OmsSkin.CreateInfo();
            SkinInfo canonical = CanonicalSkinPackage.CreateInfo();
            Assert.That(SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(legacy), Is.True);
            Assert.That(SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(canonical), Is.True);
            canonical.Hash = "unknown old package";
            Assert.That(SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(canonical), Is.False);
            legacy.FilesystemStoragePath = "chartskin/user-data";
            Assert.That(SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(legacy), Is.False);
        }

        private CanonicalSkinArchiveResult prepare() => CanonicalSkinPackage.PrepareArchive(data, original, hash(package_bytes));
        private static string hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    }
}
