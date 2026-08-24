// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;

namespace osu.Game.Tests.Skins
{
    public partial class WindowsSkinManagedPackageCaptureContractTest
    {
        [Test]
        public void TestManagedHeldCaptureTransfersCapsuleButRetainsValidatableProof()
        {
            const string secret = "private-held-package";
            FakePackage package = createPackage(secret);
            package.Package.AddFile("skin.ini", new byte[] { 1, 2, 3 });
            FakeNode nested = package.Package.AddDirectory("nested");
            nested.AddFile("note.png", new byte[] { 4, 5 });

            SkinManagedPackageHeldCaptureResult result = new WindowsSkinManagedPackageCapture(package.FileSystem)
                                                            .CaptureManagedHeld(package.Request);

            Assert.That(result.IsSuccess, Is.True);
            ISkinManagedPackageCaptureSession session = result.Session!;

            Assert.Multiple(() =>
            {
                Assert.That(session.HeldHandleCount, Is.EqualTo(package.FileSystem.ActiveHandleCount));
                Assert.That(session.HeldHandleCount, Is.GreaterThan(0));
                Assert.That(session.PhysicalTreeFingerprint, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(result.ToString(), Does.Not.Contain(secret));
                Assert.That(session.ToString(), Does.Not.Contain(secret));
            });

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => session.Validate(cancelled.Token));

            using SkinPackageRevisionCapsule capsule = session.TakeCapsule();
            InvalidOperationException? secondTake = Assert.Throws<InvalidOperationException>(() => session.TakeCapsule());
            session.Validate();

            session.Dispose();
            session.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(secondTake!.ToString(), Does.Not.Contain(secret));
                Assert.That(session.HeldHandleCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.HandleDisposeCallCount, Is.EqualTo(package.FileSystem.HandleOpenCount));
                Assert.Throws<ObjectDisposedException>(() => session.Validate());
                Assert.Throws<ObjectDisposedException>(() => session.TakeCapsule());
            });

            // Session disposal must not reclaim a capsule whose ownership was already transferred.
            using var resources = capsule.CreateResourceView();
            Assert.That(resources.Get("nested/note.png"), Is.EqualTo(new byte[] { 4, 5 }));
        }

        [TestCase("authority-link", (int)SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged)]
        [TestCase("package-link", (int)SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged)]
        [TestCase("final-inventory", (int)SkinManagedPackageCaptureRejectionReason.InventoryChanged)]
        [TestCase("tree-metadata", (int)SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture)]
        public void TestManagedHeldValidationCoversAuthorityPackagePathAndCompleteTree(
            string drift,
            int expectedReasonValue)
        {
            var expectedReason = (SkinManagedPackageCaptureRejectionReason)expectedReasonValue;
            FakePackage package = createPackage();
            FakeNode heldFile = package.Package.AddFile("skin.ini", new byte[] { 1 });
            SkinManagedPackageHeldCaptureResult result = new WindowsSkinManagedPackageCapture(package.FileSystem)
                                                            .CaptureManagedHeld(package.Request);

            Assert.That(result.IsSuccess, Is.True);

            using (ISkinManagedPackageCaptureSession session = result.Session!)
            {
                switch (drift)
                {
                    case "authority-link":
                        package.FileSystem.MoveDirectChild(package.VolumeRoot, package.DataRoot, "data-drift");
                        break;

                    case "package-link":
                        package.FileSystem.MoveDirectChild(package.ManagedRoot, package.Package, "package-drift");
                        break;

                    case "final-inventory":
                        package.Package.AddFile("late.ini", new byte[] { 2 });
                        break;

                    case "tree-metadata":
                        heldFile.ChangeTime++;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(drift));
                }

                WindowsSkinPackageCaptureFileSystemException? exception = Assert.Throws<WindowsSkinPackageCaptureFileSystemException>(
                    () => session.Validate());
                Assert.That(exception!.RejectionReason, Is.EqualTo(expectedReason));
            }

            Assert.Multiple(() =>
            {
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.HandleDisposeCallCount, Is.EqualTo(package.FileSystem.HandleOpenCount));
            });
        }

        [Test]
        public void TestManagedHeldCaptureBudgetsFailBeforeUnboundedHandlesAndReleaseEveryOwner()
        {
            FakePackage depthPackage = createPackage();
            depthPackage.Package.AddFile("skin.ini", new byte[] { 1 });
            var depthLimits = new SkinManagedPackageHeldCaptureLimits(
                SkinPackageRevisionCapsuleLimits.Default,
                maxAuthorityDepth: 2,
                maxHeldHandleCount: 20);

            SkinManagedPackageHeldCaptureResult depth = new WindowsSkinManagedPackageCapture(depthPackage.FileSystem)
                                                        .CaptureManagedHeld(depthPackage.Request, depthLimits);

            FakePackage handlePackage = createPackage();
            FakeNode unopenedFile = handlePackage.Package.AddFile("skin.ini", new byte[] { 1 });
            var handleLimits = new SkinManagedPackageHeldCaptureLimits(
                SkinPackageRevisionCapsuleLimits.Default,
                maxAuthorityDepth: 3,
                maxHeldHandleCount: 4);

            SkinManagedPackageHeldCaptureResult handles = new WindowsSkinManagedPackageCapture(handlePackage.FileSystem)
                                                          .CaptureManagedHeld(handlePackage.Request, handleLimits);

            Assert.Multiple(() =>
            {
                Assert.That(depth.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.AuthorityDepthBudgetExceeded));
                Assert.That(depthPackage.FileSystem.OperationCount, Is.Zero);
                Assert.That(depthPackage.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(handles.RejectionReason, Is.EqualTo(SkinManagedPackageCaptureRejectionReason.HeldHandleBudgetExceeded));
                Assert.That(handlePackage.FileSystem.OpenCount(unopenedFile), Is.Zero);
                Assert.That(handlePackage.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(handlePackage.FileSystem.HandleDisposeCallCount, Is.EqualTo(handlePackage.FileSystem.HandleOpenCount));
            });
        }

        [Test]
        public void TestManagedHeldSessionConcurrentDisposeClaimsEveryOwnerExactlyOnce()
        {
            FakePackage package = createPackage();
            package.Package.AddFile("skin.ini", new byte[] { 1 });
            SkinManagedPackageHeldCaptureResult result = new WindowsSkinManagedPackageCapture(package.FileSystem)
                                                            .CaptureManagedHeld(package.Request);
            ISkinManagedPackageCaptureSession session = result.Session!;

            Parallel.For(0, 32, _ => session.Dispose());

            Assert.Multiple(() =>
            {
                Assert.That(session.HeldHandleCount, Is.Zero);
                Assert.That(package.FileSystem.ActiveHandleCount, Is.Zero);
                Assert.That(package.FileSystem.HandleDisposeCallCount, Is.EqualTo(package.FileSystem.HandleOpenCount));
                Assert.Throws<ObjectDisposedException>(() => session.TakeCapsule());
                Assert.Throws<ObjectDisposedException>(() => session.Validate());
            });
        }
    }

    public partial class WindowsSkinManagedPackageCaptureTest
    {
        [Test]
        public void TestManagedHeldNativeCapturePinsTreeAfterCapsuleTransferUntilSessionDispose()
        {
            string ini = System.IO.Path.Combine(packageRoot, "skin.ini");
            string moved = packageRoot + "-moved";
            System.IO.File.WriteAllBytes(ini, new byte[] { 1, 2, 3 });

            SkinManagedPackageHeldCaptureResult result = SkinManagedPackageCapture.CaptureHeld(resolveRequest());

            Assert.That(result.IsSuccess, Is.True);
            using ISkinManagedPackageCaptureSession session = result.Session!;
            using SkinPackageRevisionCapsule capsule = session.TakeCapsule();

            session.Validate();
            Assert.Multiple(() =>
            {
                Assert.That(session.HeldHandleCount, Is.GreaterThan(0));
                Assert.Throws<System.IO.IOException>(() => System.IO.Directory.Move(packageRoot, moved));
                Assert.Throws<System.IO.IOException>(() => System.IO.File.WriteAllText(ini, "blocked"));
            });

            session.Dispose();
            Assert.That(session.HeldHandleCount, Is.Zero);
            Assert.DoesNotThrow(() => System.IO.Directory.Move(packageRoot, moved));
            Assert.DoesNotThrow(() => System.IO.Directory.Move(moved, packageRoot));
            Assert.DoesNotThrow(() => System.IO.File.WriteAllText(ini, "released"));

            using var resources = capsule.CreateResourceView();
            Assert.That(resources.Get("skin.ini"), Is.EqualTo(new byte[] { 1, 2, 3 }));
        }
    }
}
