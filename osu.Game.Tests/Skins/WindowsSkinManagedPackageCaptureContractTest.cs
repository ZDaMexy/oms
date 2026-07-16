// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using NUnit.Framework;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows10.0.16299")]
    public class WindowsSkinManagedPackageCaptureContractTest
    {
        [TestCase(@"\Device\HarddiskVolume3", true)]
        [TestCase(@"\device\harddiskvolume42", true)]
        [TestCase(@"\Device\HarddiskVolume", false)]
        [TestCase(@"\Device\HarddiskVolume3\folder", false)]
        [TestCase(@"\Device\HarddiskVolumeShadowCopy3", false)]
        [TestCase(@"\Device\HarddiskVolume3suffix", false)]
        [TestCase(@"\Device\Mup", false)]
        [TestCase(@"\??\C:\folder", false)]
        public void TestOnlyExactPhysicalVolumeTargetsAccepted(string target, bool expected)
            => Assert.That(NativeWindowsSkinPackageCaptureFileSystem.IsExactLocalVolumeTarget(target), Is.EqualTo(expected));

        [Test]
        public void TestDeterministicNestedCaptureAndOwnership()
        {
            FakePackage first = createPackage();
            FakeNode nested = first.Package.AddDirectory("nested");
            nested.AddDirectory("empty");
            nested.AddFile("b.bin", new byte[] { 2, 3 });
            first.Package.AddFile("a.bin", new byte[] { 1 });

            FakePackage second = createPackage();
            second.Package.AddFile("a.bin", new byte[] { 1 });
            FakeNode secondNested = second.Package.AddDirectory("nested");
            secondNested.AddFile("b.bin", new byte[] { 2, 3 });
            secondNested.AddDirectory("empty");

            SkinManagedPackageCaptureResult firstResult = first.Capture();
            SkinManagedPackageCaptureResult secondResult = second.Capture();

            Assert.Multiple(() =>
            {
                Assert.That(firstResult.IsSuccess, Is.True);
                Assert.That(secondResult.IsSuccess, Is.True);
                Assert.That(firstResult.Capsule!.ContentRevision, Is.EqualTo(secondResult.Capsule!.ContentRevision));
                Assert.That(first.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(second.FileSystem.ActiveHandleCount, Is.Zero);
            });

            using SkinPackageRevisionCapsule firstCapsule = firstResult.Capsule!;
            using SkinPackageRevisionCapsule secondCapsule = secondResult.Capsule!;
            using var resources = firstCapsule.CreateResourceView();
            Assert.That(resources.Get("nested/b.bin"), Is.EqualTo(new byte[] { 2, 3 }));
        }

        [Test]
        public void TestDecomposedPackageDirectoryRemainsStableAcrossFinalValidation()
        {
            FakePackage package = createPackage("e\u0301");
            package.Package.AddFile("skin.ini", new byte[] { 1 });

            SkinManagedPackageCaptureResult result = package.Capture();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            result.Capsule!.Dispose();
        }

        [Test]
        public void TestRequestCannotBeForgedOutsideResolver()
        {
            FakePackage package = createPackage();

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => new SkinManagedPackageCaptureRequest(@"C:\data", "package", new object()));
                Assert.That(package.FileSystem.OperationCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestTypedResultFactoriesRejectUndefinedReasons()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => SkinManagedPackageCaptureResult.Reject((SkinManagedPackageCaptureRejectionReason)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => SkinManagedPackageCaptureResult.RejectCapsule((SkinPackageRevisionCapsuleRejectionReason)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => new WindowsSkinPackageCaptureFileSystemException((SkinManagedPackageCaptureRejectionReason)999));
            });
        }

        [Test]
        public void TestAlternatePathAliasRejected()
        {
            FakePackage package = createPackage();
            package.FileSystem.AddAlias(package.VolumeRoot, "DATA~1", package.DataRoot);
            SkinManagedPackageCaptureRequest aliasRequest = issueRequest(@"C:\DATA~1", "package");

            SkinManagedPackageCaptureResult result = new WindowsSkinManagedPackageCapture(package.FileSystem).Capture(aliasRequest);

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
        }

        [Test]
        public void TestPackageRemovedAfterRequestRejectedBeforeRead()
        {
            FakePackage package = createPackage();
            Assert.That(package.ManagedRoot.Children.Remove(package.Package), Is.True);

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OpenCount(package.Package), Is.Zero);
                Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
            });
        }

        [TestCase("volume")]
        [TestCase("data")]
        [TestCase("managed")]
        [TestCase("package")]
        [TestCase("nested")]
        public void TestReparseAtEveryAuthorityAndCapturedLevelRejectedWithoutFollowing(string level)
        {
            FakePackage package = createPackage();
            FakeNode target = level switch
            {
                "volume" => package.VolumeRoot,
                "data" => package.DataRoot,
                "managed" => package.ManagedRoot,
                "package" => package.Package,
                "nested" => package.Package.AddDirectory("junction"),
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
            target.IsReparsePoint = true;

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (level != "volume")
                Assert.That(package.FileSystem.OpenCount(target), Is.Zero);
        }

        [Test]
        public void TestEnumerationToOpenIdentitySwapRejectedBeforeRead()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package && operation.Index == 1)
                    file.FileId++;
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestMetadataMutationDuringReadRejected()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream && operation.Node == file)
                    file.ChangeTime++;
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            Assert.That(package.FileSystem.ReadStreamCount, Is.EqualTo(1));
        }

        [Test]
        public void TestFinalInventoryMutationRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    package.Package.AddFile("late.ini", new byte[] { 9 });
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.InventoryChanged);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestFinalPackageRootIdentitySwapRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind != FakeOperationKind.CreateReadStream)
                    return;

                package.ManagedRoot.Children.Remove(package.Package);
                FakeNode replacement = package.ManagedRoot.AddDirectory("package");
                replacement.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
        }

        [Test]
        public void TestFinalAuthorityAliasRejected()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.CreateReadStream)
                    package.VolumeRoot.AddDirectory("DATA");
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
        }

        [Test]
        public void TestMutationAfterEarlierFinalEnumerationCaughtByLastMetadataPass()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("root.bin", new byte[] { 1 });
            FakeNode nested = package.Package.AddDirectory("nested");
            nested.AddFile("nested.bin", new byte[] { 2 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate
                    && operation.Node == nested
                    && operation.Index == 2)
                {
                    package.Package.ChangeTime++;
                }
            };

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
        }

        [Test]
        public void TestHardLinkAndDuplicateIdentityRejected()
        {
            FakePackage hardLinkPackage = createPackage();
            hardLinkPackage.Package.AddFile("skin.ini", new byte[] { 1 }).NumberOfLinks = 2;
            assertRejected(
                hardLinkPackage,
                hardLinkPackage.Capture(),
                SkinManagedPackageCaptureRejectionReason.HardLinkedFile);

            FakePackage duplicatePackage = createPackage();
            FakeNode first = duplicatePackage.Package.AddFile("a.ini", new byte[] { 1 });
            FakeNode second = duplicatePackage.Package.AddFile("b.ini", new byte[] { 2 });
            second.FileId = first.FileId;
            assertRejected(
                duplicatePackage,
                duplicatePackage.Capture(),
                SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity);
        }

        [Test]
        public void TestBusySourceMappedWithoutRead()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1 });
            file.IsBusy = true;

            SkinManagedPackageCaptureResult result = package.Capture();

            assertRejected(package, result, SkinManagedPackageCaptureRejectionReason.SourceBusy);
            Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestCapsuleNameCollisionAndBudgetReasonsPreserved()
        {
            FakePackage collisionPackage = createPackage();
            collisionPackage.Package.AddFile("é.ini", new byte[] { 1 });
            collisionPackage.Package.AddFile("e\u0301.ini", new byte[] { 2 });

            SkinManagedPackageCaptureResult collision = collisionPackage.Capture();
            assertCapsuleRejected(collisionPackage, collision, SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath);
            Assert.That(collisionPackage.FileSystem.ReadStreamCount, Is.Zero);

            FakePackage budgetPackage = createPackage();
            budgetPackage.Package.AddFile("large.bin", new byte[] { 1, 2, 3 });
            var limits = new SkinPackageRevisionCapsuleLimits(10, 10, 4, 100, 2, 10);

            SkinManagedPackageCaptureResult budget = budgetPackage.Capture(limits);
            assertCapsuleRejected(budgetPackage, budget, SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded);
            Assert.That(budgetPackage.FileSystem.ReadStreamCount, Is.Zero);
        }

        [Test]
        public void TestEnumerationBudgetStopsAtLimitPlusOneBeforeOpeningSources()
        {
            FakePackage package = createPackage();

            for (int i = 0; i < 100; i++)
                package.Package.AddFile($"{i:D3}.bin", new byte[] { 1 });

            var limits = new SkinPackageRevisionCapsuleLimits(2, 100, 4, 100, 10, 1000);
            SkinManagedPackageCaptureResult result = package.Capture(limits);

            assertCapsuleRejected(package, result, SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.EnumerateEntry, package.Package), Is.EqualTo(3));
                Assert.That(package.FileSystem.OpenedCapturedFileCount, Is.Zero);
                Assert.That(package.FileSystem.ReadStreamCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationBeforeAndDuringCaptureCleansHandles()
        {
            FakePackage before = createPackage();
            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => before.Capture(cancellationToken: alreadyCancelled.Token));
            Assert.That(before.FileSystem.ActiveHandleCount, Is.Zero);

            FakePackage during = createPackage();
            during.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            using var cancellation = new CancellationTokenSource();
            during.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == during.Package && operation.Index == 1)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => during.Capture(cancellationToken: cancellation.Token));
            Assert.That(during.FileSystem.ActiveHandleCount, Is.Zero);
        }

        [Test]
        public void TestCancellationInsideWideEnumerationStopsImmediately()
        {
            FakePackage package = createPackage();

            for (int i = 0; i < 100; i++)
                package.Package.AddFile($"{i:D3}.bin", new byte[] { 1 });

            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.EnumerateEntry
                    && operation.Node == package.Package
                    && operation.Index == 3)
                {
                    cancellation.Cancel();
                }
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.EnumerateEntry, package.Package), Is.EqualTo(3));
                Assert.That(package.FileSystem.OpenedCapturedFileCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationBetweenBoundedReadChunksCleansBacking()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("large.bin", new byte[] { 1, 2, 3, 4, 5 });
            package.FileSystem.ReadChunkSize = 1;
            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.ReadSource && operation.Index == 2)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.OperationIndex(FakeOperationKind.ReadSource, package.Package.Children[0]), Is.EqualTo(2));
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestCancellationAfterCapsuleBuildCleansHandles()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            using var cancellation = new CancellationTokenSource();
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package && operation.Index == 2)
                    cancellation.Cancel();
            };

            Assert.Throws<OperationCanceledException>(() => package.Capture(cancellationToken: cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.ReadStreamCount, Is.EqualTo(1));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestUnexpectedExceptionPropagatesAfterCleanup()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            package.FileSystem.OnOperation = operation =>
            {
                if (operation.Kind == FakeOperationKind.Enumerate && operation.Node == package.Package)
                    throw new InvalidOperationException("sentinel");
            };

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => package.Capture());
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo("sentinel"));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        [Test]
        public void TestHandleDisposeFailureStillClosesAllHandlesAndDisposesCapsule()
        {
            FakePackage package = createPackage();
            FakeNode file = package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            file.ThrowOnDispose = true;

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => package.Capture());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo("dispose-sentinel"));
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.LastReadStreamDisposed, Is.True);
                Assert.That(package.FileSystem.LastReadBuffer, Is.Not.Null);
                Assert.That(package.FileSystem.LastReadBuffer!, Is.All.Zero);
            });
        }

        [Test]
        public void TestSafeStringsDoNotExposeSensitiveValues()
        {
            const string secret = "private-package-name";
            SkinManagedPackageCaptureRequest request = issueRequest(@"C:\private-root", secret);
            var entry = new WindowsSkinPackageDirectoryEntry(secret, FakeNode.CreateFile(secret, new byte[] { 1 }).Snapshot(forEnumeration: true));
            var exception = new WindowsSkinPackageCaptureFileSystemException(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            Assert.Multiple(() =>
            {
                Assert.That(request.ToString(), Does.Not.Contain(secret));
                Assert.That(request.ToString(), Does.Not.Contain("private-root"));
                Assert.That(entry.ToString(), Does.Not.Contain(secret));
                Assert.That(entry.Metadata.ToString(), Does.Not.Contain(secret));
                Assert.That(entry.Metadata.Identity.ToString(), Does.Not.Contain(secret));
                Assert.That(exception.ToString(), Does.Not.Contain(secret));
            });
        }

        private static FakePackage createPackage(string packageName = "package")
        {
            FakeNode volumeRoot = FakeNode.CreateDirectory("C:");
            FakeNode dataRoot = volumeRoot.AddDirectory("data");
            FakeNode managedRoot = dataRoot.AddDirectory(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            FakeNode packageRoot = managedRoot.AddDirectory(packageName);
            var fileSystem = new FakeFileSystem(volumeRoot);
            SkinManagedPackageCaptureRequest request = issueRequest(@"C:\data", packageName);
            return new FakePackage(fileSystem, request, volumeRoot, dataRoot, managedRoot, packageRoot);
        }

        private static SkinManagedPackageCaptureRequest issueRequest(string dataRoot, string packageName)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo { FilesystemStoragePath = $"chartskin/{packageName}" },
                dataRoot,
                AllDirectoriesFilesystemInfoProvider.Instance);

            Assert.That(resolution.ManagedCaptureRequest, Is.Not.Null);
            return resolution.ManagedCaptureRequest!;
        }

        private static void assertRejected(
            FakePackage package,
            SkinManagedPackageCaptureResult result,
            SkinManagedPackageCaptureRejectionReason reason)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(reason));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.None));
                Assert.That(result.Capsule, Is.Null);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        private static void assertCapsuleRejected(
            FakePackage package,
            SkinManagedPackageCaptureResult result,
            SkinPackageRevisionCapsuleRejectionReason reason)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.CapsuleRejected));
                Assert.That(result.CapsuleRejectionReason, Is.EqualTo(reason));
                Assert.That(result.Capsule, Is.Null);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
            });
        }

        private sealed record FakePackage(
            FakeFileSystem FileSystem,
            SkinManagedPackageCaptureRequest Request,
            FakeNode VolumeRoot,
            FakeNode DataRoot,
            FakeNode ManagedRoot,
            FakeNode Package)
        {
            public SkinManagedPackageCaptureResult Capture(
                SkinPackageRevisionCapsuleLimits? limits = null,
                CancellationToken cancellationToken = default)
                => new WindowsSkinManagedPackageCapture(FileSystem).Capture(Request, limits, cancellationToken);
        }

        private enum FakeOperationKind
        {
            OpenVolumeRoot,
            Enumerate,
            EnumerateEntry,
            OpenChild,
            QueryMetadata,
            CreateReadStream,
            ReadSource,
        }

        private readonly record struct FakeOperation(FakeOperationKind Kind, FakeNode Node, int Index);

        private sealed class FakeFileSystem : IWindowsSkinPackageCaptureFileSystem
        {
            private readonly FakeNode volumeRoot;
            private readonly Dictionary<(FakeOperationKind Kind, FakeNode Node), int> operationIndexes = new Dictionary<(FakeOperationKind, FakeNode), int>();
            private readonly Dictionary<(FakeNode Parent, string Alias), FakeNode> aliases = new Dictionary<(FakeNode, string), FakeNode>();
            private readonly Dictionary<FakeNode, int> openCounts = new Dictionary<FakeNode, int>();

            public Action<FakeOperation>? OnOperation { get; set; }

            public int ActiveHandleCount { get; private set; }

            public int OperationCount { get; private set; }

            public int ReadStreamCount { get; private set; }

            public int OpenedCapturedFileCount { get; private set; }

            public byte[]? LastReadBuffer { get; private set; }

            public bool LastReadStreamDisposed { get; private set; }

            public int ReadChunkSize { get; set; } = int.MaxValue;

            public FakeFileSystem(FakeNode volumeRoot)
            {
                this.volumeRoot = volumeRoot;
            }

            public void AddAlias(FakeNode parent, string alias, FakeNode target) => aliases.Add((parent, alias), target);

            public int OpenCount(FakeNode node) => openCounts.GetValueOrDefault(node);

            public int OperationIndex(FakeOperationKind kind, FakeNode node) => operationIndexes.GetValueOrDefault((kind, node));

            public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
            {
                invoke(FakeOperationKind.OpenVolumeRoot, volumeRoot);

                if (driveLetter != 'C')
                    throw failure(SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

                return open(volumeRoot);
            }

            public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
                IWindowsSkinPackageCaptureHandle directory,
                int maxEntries,
                CancellationToken cancellationToken)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(maxEntries);
                cancellationToken.ThrowIfCancellationRequested();
                FakeNode node = getNode(directory);

                if (node.Kind != WindowsSkinPackageEntryKind.Directory)
                    throw failure(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

                var snapshot = new List<WindowsSkinPackageDirectoryEntry>();

                foreach (FakeNode child in node.Children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    invoke(FakeOperationKind.EnumerateEntry, node);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (snapshot.Count >= maxEntries)
                        throw failure(SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);

                    snapshot.Add(new WindowsSkinPackageDirectoryEntry(child.Name, child.Snapshot(forEnumeration: true)));
                }

                invoke(FakeOperationKind.Enumerate, node);
                cancellationToken.ThrowIfCancellationRequested();
                return snapshot;
            }

            public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
                IWindowsSkinPackageCaptureHandle parent,
                string name,
                WindowsSkinPackageOpenMode mode,
                SkinManagedPackageCaptureRejectionReason unavailableReason)
            {
                FakeNode parentNode = getNode(parent);
                invoke(FakeOperationKind.OpenChild, parentNode);
                FakeNode? child = parentNode.Children.SingleOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

                if (child == null)
                    aliases.TryGetValue((parentNode, name), out child);

                if (child == null)
                    throw failure(unavailableReason);

                WindowsSkinPackageEntryKind expectedKind = mode == WindowsSkinPackageOpenMode.CapturedFile
                    ? WindowsSkinPackageEntryKind.File
                    : WindowsSkinPackageEntryKind.Directory;

                if (child.Kind != expectedKind)
                    throw failure(unavailableReason);

                if (child.IsBusy)
                    throw failure(SkinManagedPackageCaptureRejectionReason.SourceBusy);

                if (mode == WindowsSkinPackageOpenMode.CapturedFile)
                    OpenedCapturedFileCount++;

                return open(child);
            }

            public WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle)
            {
                FakeNode node = getNode(handle);
                invoke(FakeOperationKind.QueryMetadata, node);
                return node.Snapshot(forEnumeration: false);
            }

            public Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file)
            {
                FakeNode node = getNode(file);
                invoke(FakeOperationKind.CreateReadStream, node);
                ReadStreamCount++;
                return new TrackingReadStream(
                    node.Content,
                    buffer => LastReadBuffer = buffer,
                    () => invoke(FakeOperationKind.ReadSource, node),
                    ReadChunkSize,
                    () => LastReadStreamDisposed = true);
            }

            private FakeHandle open(FakeNode node)
            {
                ActiveHandleCount++;
                openCounts[node] = openCounts.GetValueOrDefault(node) + 1;
                return new FakeHandle(this, node);
            }

            private FakeNode getNode(IWindowsSkinPackageCaptureHandle handle)
            {
                if (handle is not FakeHandle fake)
                    throw new ArgumentException(nameof(handle));

                ObjectDisposedException.ThrowIf(fake.IsDisposed, fake);

                return fake.Node;
            }

            private void invoke(FakeOperationKind kind, FakeNode node)
            {
                OperationCount++;
                int index = operationIndexes.GetValueOrDefault((kind, node)) + 1;
                operationIndexes[(kind, node)] = index;
                OnOperation?.Invoke(new FakeOperation(kind, node, index));
            }

            private void close()
            {
                ActiveHandleCount--;
                Assert.That(ActiveHandleCount, Is.GreaterThanOrEqualTo(0));
            }

            private static WindowsSkinPackageCaptureFileSystemException failure(SkinManagedPackageCaptureRejectionReason reason)
                => new WindowsSkinPackageCaptureFileSystemException(reason);

            private sealed class FakeHandle : IWindowsSkinPackageCaptureHandle
            {
                private readonly FakeFileSystem owner;

                public FakeNode Node { get; }

                public bool IsDisposed { get; private set; }

                public FakeHandle(FakeFileSystem owner, FakeNode node)
                {
                    this.owner = owner;
                    Node = node;
                }

                public void Dispose()
                {
                    if (IsDisposed)
                        return;

                    IsDisposed = true;
                    owner.close();

                    if (Node.ThrowOnDispose)
                        throw new InvalidOperationException("dispose-sentinel");
                }

                public override string ToString() => nameof(FakeHandle);
            }

            private sealed class TrackingReadStream : Stream
            {
                private readonly byte[] content;
                private readonly Action<byte[]> onReadBuffer;
                private readonly Action onRead;
                private readonly Action onDisposed;
                private readonly int maxReadSize;
                private int position;

                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => content.Length;

                public override long Position
                {
                    get => position;
                    set => throw new NotSupportedException();
                }

                public TrackingReadStream(
                    byte[] content,
                    Action<byte[]> onReadBuffer,
                    Action onRead,
                    int maxReadSize,
                    Action onDisposed)
                {
                    this.content = content;
                    this.onReadBuffer = onReadBuffer;
                    this.onRead = onRead;
                    this.maxReadSize = maxReadSize;
                    this.onDisposed = onDisposed;
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    onReadBuffer(buffer);
                    onRead();
                    int read = Math.Min(Math.Min(count, maxReadSize), content.Length - position);
                    content.AsSpan(position, read).CopyTo(buffer.AsSpan(offset, read));
                    position += read;
                    return read;
                }

                public override int ReadByte()
                {
                    if (position >= content.Length)
                        return -1;

                    return content[position++];
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                        onDisposed();

                    base.Dispose(disposing);
                }

                public override void Flush() => throw new NotSupportedException();
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            }
        }

        private sealed class AllDirectoriesFilesystemInfoProvider : SkinFilesystemStorageResolver.ISkinFilesystemInfoProvider
        {
            public static AllDirectoriesFilesystemInfoProvider Instance { get; } = new AllDirectoriesFilesystemInfoProvider();

            public FileAttributes GetAttributes(string path) => FileAttributes.Directory;
        }

        private sealed class FakeNode
        {
            private static ulong next_id = 10;

            public string Name { get; }

            public WindowsSkinPackageEntryKind Kind { get; }

            public List<FakeNode> Children { get; } = new List<FakeNode>();

            public byte[] Content { get; }

            public ulong FileId { get; set; }

            public long ChangeTime { get; set; }

            public uint NumberOfLinks { get; set; } = 1;

            public bool IsReparsePoint { get; set; }

            public bool IsBusy { get; set; }

            public bool ThrowOnDispose { get; set; }

            private FakeNode(string name, WindowsSkinPackageEntryKind kind, byte[] content)
            {
                Name = name;
                Kind = kind;
                Content = content;
                FileId = next_id++;
                ChangeTime = checked((long)FileId + 1000);
            }

            public static FakeNode CreateDirectory(string name) => new FakeNode(name, WindowsSkinPackageEntryKind.Directory, Array.Empty<byte>());

            public static FakeNode CreateFile(string name, byte[] content) => new FakeNode(name, WindowsSkinPackageEntryKind.File, (byte[])content.Clone());

            public FakeNode AddDirectory(string name)
            {
                FakeNode child = CreateDirectory(name);
                Children.Add(child);
                return child;
            }

            public FakeNode AddFile(string name, byte[] content)
            {
                FakeNode child = CreateFile(name, content);
                Children.Add(child);
                return child;
            }

            public WindowsSkinPackageEntryMetadata Snapshot(bool forEnumeration)
            {
                uint attributes = Kind == WindowsSkinPackageEntryKind.Directory ? 0x10u : 0x80u;
                uint reparseTag = 0;

                if (IsReparsePoint)
                {
                    attributes |= 0x400;
                    reparseTag = 0xA0000003;
                }

                return new WindowsSkinPackageEntryMetadata(
                    new WindowsSkinPackagePhysicalIdentity(1, FileId, FileId * 17),
                    Kind,
                    Content.LongLength,
                    checked((long)FileId + 100),
                    checked((long)FileId + 200),
                    ChangeTime,
                    attributes,
                    reparseTag,
                    forEnumeration ? 0 : NumberOfLinks,
                    false);
            }
        }
    }
}
