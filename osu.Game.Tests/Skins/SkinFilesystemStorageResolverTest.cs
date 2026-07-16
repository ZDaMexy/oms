// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Models;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinFilesystemStorageResolverTest
    {
        private string dataRoot = null!;
        private NativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            dataRoot = Path.Combine(Path.GetTempPath(), $"oms-skin-storage-resolver-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
            storage = new NativeStorage(dataRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dataRoot))
                Directory.Delete(dataRoot, true);
        }

        [Test]
        public void TestRealmPackageAuthorityIsUnchanged()
        {
            var skinInfo = new SkinInfo();
            skinInfo.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "hash" }, "skin.ini"));

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.RealmPackage));
                Assert.That(resolution.IsValid, Is.True);
                Assert.That(resolution.IsFilesystemBacked, Is.False);
                Assert.That(resolution.NormalisedAbsolutePath, Is.Null);
                Assert.That(resolution.ManagedCaptureRequest, Is.Null);
                Assert.That(resolution.RejectionReason, Is.EqualTo(SkinFilesystemStorageRejectionReason.None));
            });
        }

        [Test]
        public void TestBuiltInWithoutFolderRemainsRealmAuthority()
        {
            var skinInfo = new SkinInfo
            {
                ID = SkinInfo.OMS_SKIN,
                Protected = true,
            };

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.RealmPackage));
                Assert.That(resolution.RejectionReason, Is.EqualTo(SkinFilesystemStorageRejectionReason.None));
                Assert.That(skinInfo.Protected, Is.True);
                Assert.That(skinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
            });
        }

        [Test]
        public void TestValidManagedFolder()
        {
            string packagePath = createManagedPackage("Managed Skin");
            string[] entriesBefore = snapshotEntries();

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/Managed Skin",
            }, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ManagedFolder));
                Assert.That(resolution.IsValid, Is.True);
                Assert.That(resolution.IsFilesystemBacked, Is.True);
                Assert.That(resolution.NormalisedAbsolutePath, Is.EqualTo(packagePath));
                Assert.That(resolution.NormalisedManagedRelativePath, Is.EqualTo("chartskin/Managed Skin"));
                Assert.That(resolution.ManagedCaptureRequest, Is.Not.Null);
                Assert.That(resolution.ManagedCaptureRequest!.NormalisedDataRootAbsolutePath, Is.EqualTo(dataRoot));
                Assert.That(resolution.ManagedCaptureRequest.PackageDirectoryName, Is.EqualTo("Managed Skin"));
                Assert.That(resolution.ManagedCaptureRequest.ToString(), Is.EqualTo(nameof(SkinManagedPackageCaptureRequest)));
                Assert.That(resolution.ManagedCaptureRequest.ToString(), Does.Not.Contain(dataRoot));
                Assert.That(resolution.ManagedCaptureRequest.ToString(), Does.Not.Contain("Managed Skin"));
                Assert.That(resolution.ToString(), Does.Not.Contain("Managed Skin"));
                Assert.That(resolution.ToString(), Does.Not.Contain(packagePath));
                Assert.That(snapshotEntries(), Is.EqualTo(entriesBefore));
            });
        }

        [Test]
        public void TestManagedPathNormalisesCaseSeparatorsAndTrailingSeparator()
        {
            string packagePath = createManagedPackage("MixedCase");

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(new SkinInfo
            {
                FilesystemStoragePath = @"CHARTSKIN\MixedCase\",
            }, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ManagedFolder));
                Assert.That(resolution.NormalisedAbsolutePath, Is.EqualTo(packagePath).IgnoreCase);
                Assert.That(resolution.NormalisedManagedRelativePath, Is.EqualTo("chartskin/MixedCase"));
            });
        }

        [Test]
        public void TestValidExternalFolderIsReadOnly()
        {
            string externalPath = Path.Combine(dataRoot, "external skin");
            Directory.CreateDirectory(externalPath);
            string[] entriesBefore = snapshotEntries();

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(new SkinInfo
            {
                FilesystemStoragePath = externalPath + Path.DirectorySeparatorChar,
                IsExternalFilesystemStorage = true,
            }, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ExternalFolder));
                Assert.That(resolution.IsValid, Is.True);
                Assert.That(resolution.NormalisedAbsolutePath, Is.EqualTo(externalPath));
                Assert.That(resolution.NormalisedManagedRelativePath, Is.Null);
                Assert.That(resolution.ManagedCaptureRequest, Is.Null);
                Assert.That(resolution.ToString(), Does.Not.Contain("external skin"));
                Assert.That(resolution.ToString(), Does.Not.Contain(externalPath));
                Assert.That(snapshotEntries(), Is.EqualTo(entriesBefore));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        public void TestExternalMarkerWithoutPathIsRejected(string? path)
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = true,
            }, SkinFilesystemStorageRejectionReason.ExternalMarkerWithoutPath);

        [Test]
        public void TestRelativeExternalPathIsRejected()
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/package",
                IsExternalFilesystemStorage = true,
            }, SkinFilesystemStorageRejectionReason.ExternalPathMustBeAbsolute);

        [Test]
        public void TestAbsoluteManagedPathIsRejected()
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = Path.Combine(dataRoot, "package"),
            }, SkinFilesystemStorageRejectionReason.ManagedPathMustBeRelative);

        [Test]
        public void TestFolderWithRealmFilesIsRejected()
        {
            var skinInfo = new SkinInfo { FilesystemStoragePath = "chartskin/package" };
            skinInfo.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "hash" }, "skin.ini"));

            assertRejected(skinInfo, SkinFilesystemStorageRejectionReason.MixedStorageAuthorities);
        }

        [Test]
        public void TestExternalFolderWithRealmFilesIsRejected()
        {
            var skinInfo = new SkinInfo
            {
                FilesystemStoragePath = Path.Combine(dataRoot, "external"),
                IsExternalFilesystemStorage = true,
            };
            skinInfo.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "hash" }, "skin.ini"));

            assertRejected(skinInfo, SkinFilesystemStorageRejectionReason.MixedStorageAuthorities);
        }

        [Test]
        public void TestPendingFolderIsRejected()
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/package",
                DeletePending = true,
            }, SkinFilesystemStorageRejectionReason.DeletePending);

        [Test]
        public void TestProtectedFolderIsRejected()
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/package",
                Protected = true,
            }, SkinFilesystemStorageRejectionReason.ProtectedRecord);

        [Test]
        public void TestFixedIdFolderIsRejected()
            => assertRejected(new SkinInfo
            {
                ID = SkinInfo.OMS_SKIN,
                FilesystemStoragePath = "chartskin/package",
            }, SkinFilesystemStorageRejectionReason.FixedIdRecord);

        [TestCase("chartskin", nameof(SkinFilesystemStorageRejectionReason.ManagedRootSelected))]
        [TestCase("chartskin/", nameof(SkinFilesystemStorageRejectionReason.ManagedRootSelected))]
        [TestCase("chartskin-evil/package", nameof(SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot))]
        [TestCase("other/package", nameof(SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot))]
        [TestCase("chartskin/package/child", nameof(SkinFilesystemStorageRejectionReason.ManagedPathOutsideRoot))]
        [TestCase("../chartskin/package", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/../package", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/package/..", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/./package", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin//package", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/package:stream", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/package.", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/package ", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/CON", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase("chartskin/lpt1.png", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        public void TestUnsafeManagedPathIsRejected(string path, string expectedReason)
            => assertRejected(new SkinInfo { FilesystemStoragePath = path }, Enum.Parse<SkinFilesystemStorageRejectionReason>(expectedReason));

        [Test]
        public void TestMissingManagedDirectoryIsRejected()
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/missing",
            }, SkinFilesystemStorageRejectionReason.DirectoryUnavailable);

        [Test]
        public void TestManagedFileIsRejectedAsPackageRoot()
        {
            File.WriteAllText(Path.Combine(dataRoot, "chartskin", "file"), "not a directory");

            assertRejected(new SkinInfo
            {
                FilesystemStoragePath = "chartskin/file",
            }, SkinFilesystemStorageRejectionReason.PathIsNotDirectory);
        }

        [Test]
        public void TestManagedRootReparsePointIsRejected()
        {
            string packagePath = createManagedPackage("package");
            var filesystem = new DecoratingFilesystemInfoProvider(Path.Combine(dataRoot, "chartskin"), FileAttributes.Directory | FileAttributes.ReparsePoint);

            assertRejected(
                new SkinInfo { FilesystemStoragePath = "chartskin/package" },
                SkinFilesystemStorageRejectionReason.ReparsePoint,
                filesystem);

            Assert.That(Directory.Exists(packagePath), Is.True);
        }

        [Test]
        public void TestDataRootReparsePointIsRejected()
        {
            createManagedPackage("package");
            var filesystem = new DecoratingFilesystemInfoProvider(dataRoot, FileAttributes.Directory | FileAttributes.ReparsePoint);

            assertRejected(
                new SkinInfo { FilesystemStoragePath = "chartskin/package" },
                SkinFilesystemStorageRejectionReason.ReparsePoint,
                filesystem);
        }

        [Test]
        public void TestManagedPackageReparsePointIsRejected()
        {
            string packagePath = createManagedPackage("package");
            var filesystem = new DecoratingFilesystemInfoProvider(packagePath, FileAttributes.Directory | FileAttributes.ReparsePoint);

            assertRejected(
                new SkinInfo { FilesystemStoragePath = "chartskin/package" },
                SkinFilesystemStorageRejectionReason.ReparsePoint,
                filesystem);
        }

        [Test]
        public void TestExternalAncestorReparsePointIsRejected()
        {
            string packagePath = Path.Combine(dataRoot, "external", "package");
            Directory.CreateDirectory(packagePath);
            var filesystem = new DecoratingFilesystemInfoProvider(Path.Combine(dataRoot, "external"), FileAttributes.Directory | FileAttributes.ReparsePoint);

            assertRejected(
                new SkinInfo
                {
                    FilesystemStoragePath = packagePath,
                    IsExternalFilesystemStorage = true,
                },
                SkinFilesystemStorageRejectionReason.ReparsePoint,
                filesystem);
        }

        [Test]
        public void TestInspectionFailureIsRejectedWithoutLeakingPath()
        {
            string secretPackagePath = createManagedPackage("secret-package-name");
            var filesystem = new ThrowingFilesystemInfoProvider(new UnauthorizedAccessException());
            var skinInfo = new SkinInfo { FilesystemStoragePath = "chartskin/secret-package-name" };

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, dataRoot, filesystem);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.Invalid));
                Assert.That(resolution.RejectionReason, Is.EqualTo(SkinFilesystemStorageRejectionReason.PathInspectionFailed));
                Assert.That(resolution.NormalisedAbsolutePath, Is.Null);
                Assert.That(resolution.NormalisedManagedRelativePath, Is.Null);
                Assert.That(resolution.ToString(), Does.Not.Contain(secretPackagePath));
                Assert.That(resolution.ToString(), Does.Not.Contain("secret-package-name"));
            });
        }

        [Test]
        public void TestPathTooLongInspectionIsTypedAsUnsupportedSyntax()
        {
            createManagedPackage("package");

            assertRejected(
                new SkinInfo { FilesystemStoragePath = "chartskin/package" },
                SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax,
                new ThrowingFilesystemInfoProvider(new PathTooLongException()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestResolutionNeverMutatesRecordOrExistingFile(bool rejectRecord)
        {
            string packagePath = createManagedPackage("immutable-source");
            string resourcePath = Path.Combine(packagePath, "skin.ini");
            byte[] expectedContent = { 0x01, 0x02, 0x03, 0x04 };
            File.WriteAllBytes(resourcePath, expectedContent);
            DateTime expectedLastWriteTime = new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(resourcePath, expectedLastWriteTime);
            expectedLastWriteTime = File.GetLastWriteTimeUtc(resourcePath);

            var skinInfo = new SkinInfo("original-name", "original-creator")
            {
                FilesystemStoragePath = "chartskin/immutable-source",
                DeletePending = rejectRecord,
            };
            Guid expectedId = skinInfo.ID;

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.IsValid, Is.EqualTo(!rejectRecord));
                Assert.That(File.ReadAllBytes(resourcePath), Is.EqualTo(expectedContent));
                Assert.That(File.GetLastWriteTimeUtc(resourcePath), Is.EqualTo(expectedLastWriteTime));
                Assert.That(skinInfo.ID, Is.EqualTo(expectedId));
                Assert.That(skinInfo.Name, Is.EqualTo("original-name"));
                Assert.That(skinInfo.Creator, Is.EqualTo("original-creator"));
                Assert.That(skinInfo.FilesystemStoragePath, Is.EqualTo("chartskin/immutable-source"));
                Assert.That(skinInfo.IsExternalFilesystemStorage, Is.False);
                Assert.That(skinInfo.DeletePending, Is.EqualTo(rejectRecord));
                Assert.That(skinInfo.Files, Is.Empty);
            });
        }

        [TestCase(@"C:relative", nameof(SkinFilesystemStorageRejectionReason.ExternalPathMustBeAbsolute))]
        [TestCase(@"\\server\share\skin", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        [TestCase(@"\\?\C:\skin", nameof(SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax))]
        public void TestUnsupportedExternalPathIsRejected(string path, string expectedReason)
        {
            assertRejected(new SkinInfo
            {
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = true,
            }, Enum.Parse<SkinFilesystemStorageRejectionReason>(expectedReason));
        }

        [Test]
        public void TestExternalVolumeRootIsRejected()
        {
            string volumeRoot = Path.GetPathRoot(dataRoot)!;

            assertRejected(new SkinInfo
            {
                FilesystemStoragePath = volumeRoot,
                IsExternalFilesystemStorage = true,
            }, SkinFilesystemStorageRejectionReason.ExternalVolumeRootSelected);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestExternalManagedNamespaceOverlapIsRejected(int relationship)
        {
            string managedRoot = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            string path = relationship switch
            {
                0 => managedRoot,
                1 => createManagedPackage("package"),
                _ => dataRoot,
            };

            assertRejected(new SkinInfo
            {
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = true,
            }, SkinFilesystemStorageRejectionReason.ExternalManagedAuthorityConflict);
        }

        [TestCase("external.")]
        [TestCase("external ")]
        [TestCase("NUL")]
        [TestCase("com1.png")]
        [TestCase("COM¹.txt")]
        [TestCase("LPT³")]
        [TestCase("external:stream")]
        public void TestAmbiguousExternalSegmentIsRejected(string segment)
            => assertRejected(new SkinInfo
            {
                FilesystemStoragePath = Path.Combine(dataRoot, segment),
                IsExternalFilesystemStorage = true,
            }, SkinFilesystemStorageRejectionReason.UnsupportedPathSyntax);

        [Test]
        public void TestExternalResolutionNeverMutatesRecordOrExistingFile()
        {
            string packagePath = Path.Combine(dataRoot, "external-immutable-source");
            Directory.CreateDirectory(packagePath);
            string resourcePath = Path.Combine(packagePath, "skin.ini");
            byte[] expectedContent = { 0x04, 0x03, 0x02, 0x01 };
            File.WriteAllBytes(resourcePath, expectedContent);
            DateTime expectedLastWriteTime = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(resourcePath, expectedLastWriteTime);
            expectedLastWriteTime = File.GetLastWriteTimeUtc(resourcePath);

            var skinInfo = new SkinInfo("external-name", "external-creator")
            {
                FilesystemStoragePath = packagePath,
                IsExternalFilesystemStorage = true,
            };
            Guid expectedId = skinInfo.ID;

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ExternalFolder));
                Assert.That(File.ReadAllBytes(resourcePath), Is.EqualTo(expectedContent));
                Assert.That(File.GetLastWriteTimeUtc(resourcePath), Is.EqualTo(expectedLastWriteTime));
                Assert.That(skinInfo.ID, Is.EqualTo(expectedId));
                Assert.That(skinInfo.Name, Is.EqualTo("external-name"));
                Assert.That(skinInfo.Creator, Is.EqualTo("external-creator"));
                Assert.That(skinInfo.FilesystemStoragePath, Is.EqualTo(packagePath));
                Assert.That(skinInfo.IsExternalFilesystemStorage, Is.True);
                Assert.That(skinInfo.DeletePending, Is.False);
                Assert.That(skinInfo.Files, Is.Empty);
            });
        }

        private string createManagedPackage(string name)
        {
            string path = Path.Combine(dataRoot, SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private string[] snapshotEntries()
            => Directory.EnumerateFileSystemEntries(dataRoot, "*", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(dataRoot, path))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();

        private void assertRejected(
            SkinInfo skinInfo,
            SkinFilesystemStorageRejectionReason expectedReason,
            SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider? filesystem = null)
        {
            SkinFilesystemStorageResolution resolution = filesystem == null
                ? SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage)
                : SkinFilesystemStorageResolver.ResolveExisting(skinInfo, dataRoot, filesystem);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.Invalid));
                Assert.That(resolution.IsValid, Is.False);
                Assert.That(resolution.IsFilesystemBacked, Is.False);
                Assert.That(resolution.RejectionReason, Is.EqualTo(expectedReason));
                Assert.That(resolution.NormalisedAbsolutePath, Is.Null);
                Assert.That(resolution.NormalisedManagedRelativePath, Is.Null);
                Assert.That(resolution.ManagedCaptureRequest, Is.Null);
            });
        }

        private sealed class DecoratingFilesystemInfoProvider : SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider
        {
            private readonly string decoratedPath;
            private readonly FileAttributes attributes;

            public DecoratingFilesystemInfoProvider(string decoratedPath, FileAttributes attributes)
            {
                this.decoratedPath = Path.GetFullPath(decoratedPath);
                this.attributes = attributes;
            }

            public FileAttributes GetAttributes(string path)
                => string.Equals(Path.GetFullPath(path), decoratedPath, StringComparison.OrdinalIgnoreCase)
                    ? attributes
                    : File.GetAttributes(path);
        }

        private sealed class ThrowingFilesystemInfoProvider : SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider
        {
            private readonly Exception exception;

            public ThrowingFilesystemInfoProvider(Exception exception)
            {
                this.exception = exception;
            }

            public FileAttributes GetAttributes(string path) => throw exception;
        }
    }
}
