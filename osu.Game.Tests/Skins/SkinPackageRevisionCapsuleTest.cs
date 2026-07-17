// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinPackageRevisionCapsuleTest
    {
        [Test]
        public void TestCreatesExactReadOnlyResourceView()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateDirectory("textures"),
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }),
                SkinPackageCapturedEntry.CreateFile(@"textures\Note.PNG", new byte[] { 4, 5 }));
            using IResourceStore<byte[]> view = capsule.CreateResourceView();

            Assert.Multiple(() =>
            {
                Assert.That(capsule.FileCount, Is.EqualTo(2));
                Assert.That(capsule.TotalBytes, Is.EqualTo(5));
                Assert.That(capsule.ContentRevision, Has.Length.EqualTo(64));
                Assert.That(view.Get("SKIN.INI"), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(view.Get(@"TEXTURES\note.png"), Is.EqualTo(new byte[] { 4, 5 }));
                Assert.That(view.Get("missing.png"), Is.Null);
                Assert.That(view.Get("../skin.ini"), Is.Null);
                Assert.That(view.GetAvailableResources(), Is.EqualTo(new[] { "skin.ini", "textures/Note.PNG" }));
            });

            using Stream? stream = view.GetStream(@"textures\NOTE.png");
            using Stream? missing = view.GetStream("missing.png");

            Assert.Multiple(() =>
            {
                Assert.That(stream, Is.Not.Null);
                Assert.That(stream!.CanWrite, Is.False);
                Assert.That(readAll(stream!), Is.EqualTo(new byte[] { 4, 5 }));
                Assert.That(missing, Is.Null);
            });
        }

        [Test]
        public void TestShortReadsCompleteAndSuccessfulSourceIsDisposed()
        {
            var stream = new TrackingMemoryStream(new byte[] { 1, 2, 3 }, maxReadSize: 1);
            using SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateFile("skin.ini", 3, () => stream));
            using IResourceStore<byte[]> view = capsule.CreateResourceView();

            Assert.Multiple(() =>
            {
                Assert.That(view.Get("skin.ini"), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(stream.IsDisposed, Is.True);
            });
        }

        [Test]
        public void TestInputAndReturnedBytesAreDefensiveCopies()
        {
            byte[] source = { 10, 20, 30 };
            using SkinPackageRevisionCapsule capsule = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", source));
            using IResourceStore<byte[]> view = capsule.CreateResourceView();

            source[0] = 99;
            byte[] first = view.Get("skin.ini");
            first[1] = 88;

            using Stream stream = view.GetStream("skin.ini")!;
            byte[] fromStream = readAll(stream);
            fromStream[2] = 77;

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(new byte[] { 10, 88, 30 }));
                Assert.That(view.Get("skin.ini"), Is.EqualTo(new byte[] { 10, 20, 30 }));
                Assert.That(fromStream, Is.EqualTo(new byte[] { 10, 20, 77 }));
                Assert.That(view.Get("skin.ini"), Is.EqualTo(new byte[] { 10, 20, 30 }));
            });
        }

        [Test]
        public void TestFileMetadataAndResourceNamesAreReadOnlyCollections()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(SkinPackageCapturedEntry.CreateFile("secret.ini", new byte[] { 1, 2, 3 }));
            using IResourceStore<byte[]> view = capsule.CreateResourceView();
            var files = (IList<SkinPackageFileRevision>)capsule.Files;
            var available = (IList<string>)view.GetAvailableResources();

            Assert.Multiple(() =>
            {
                Assert.That(() => files[0] = files[0], Throws.TypeOf<NotSupportedException>());
                Assert.That(() => available[0] = "changed.ini", Throws.TypeOf<NotSupportedException>());
                Assert.That(view.GetAvailableResources(), Is.EqualTo(new[] { "secret.ini" }));
            });
        }

        [Test]
        public void TestViewDoesNotOwnCapsuleAndCapsuleDisposalIsIdempotent()
        {
            SkinPackageRevisionCapsule capsule = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }));
            IResourceStore<byte[]> disposedView = capsule.CreateResourceView();
            disposedView.Dispose();

            using IResourceStore<byte[]> activeView = capsule.CreateResourceView();
            Assert.That(activeView.Get("skin.ini"), Is.EqualTo(new byte[] { 1, 2, 3 }));

            Stream survivingStream = activeView.GetStream("skin.ini")!;
            capsule.Dispose();
            Assert.DoesNotThrow(capsule.Dispose);

            Assert.Multiple(() =>
            {
                Assert.That(readAll(survivingStream), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(() => activeView.Get("skin.ini"), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => activeView.Get("../invalid"), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => activeView.GetStream("skin.ini"), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => activeView.GetAvailableResources(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
            });

            survivingStream.Dispose();
        }

        [Test]
        public void TestOwningResourceStoreRetiresCapsuleExactlyOnce()
        {
            SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }));
            var store = new SkinPackageRevisionResourceStore(capsule);

            Assert.Multiple(() =>
            {
                Assert.That(store.ContentRevision, Is.EqualTo(capsule.ContentRevision));
                Assert.That(store.Files, Is.EqualTo(capsule.Files));
                Assert.That(store.Get("skin.ini"), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(store.ToString(), Is.EqualTo(nameof(SkinPackageRevisionResourceStore)));
            });

            store.Dispose();
            Assert.DoesNotThrow(store.Dispose);

            Assert.Multiple(() =>
            {
                Assert.That(() => store.Get("skin.ini"), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => store.GetStream("skin.ini"), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => store.GetAvailableResources(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => store.ContentRevision, Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
            });
        }

        [Test]
        public void TestGetAsyncHonoursCancellation()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }));
            using IResourceStore<byte[]> view = capsule.CreateResourceView();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Task<byte[]> task = view.GetAsync("skin.ini", cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(task.IsCanceled, Is.True);
                Assert.That(async () => await task, Throws.TypeOf<TaskCanceledException>());
            });
        }

        [Test]
        public void TestRevisionIsIndependentOfCaptureOrderAndSlashStyle()
        {
            using SkinPackageRevisionCapsule first = createSuccess(
                SkinPackageCapturedEntry.CreateDirectory("textures"),
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }),
                SkinPackageCapturedEntry.CreateFile("textures/note.png", new byte[] { 2 }));
            using SkinPackageRevisionCapsule second = createSuccess(
                SkinPackageCapturedEntry.CreateFile(@"textures\note.png", new byte[] { 2 }),
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }),
                SkinPackageCapturedEntry.CreateDirectory(@"textures"));

            Assert.That(second.ContentRevision, Is.EqualTo(first.ContentRevision));
        }

        [Test]
        public void TestCanonicalRevisionFixedVector()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }),
                SkinPackageCapturedEntry.CreateFile("textures/note.png", new byte[] { 4, 5 }));

            Assert.That(capsule.ContentRevision, Is.EqualTo("79D567860862E2D7A37B70669524994560591DA014508B41CC04FD0EE16B572A"));
        }

        [Test]
        public void TestUnicodeNormalisationProducesSameRevisionAndLookup()
        {
            const string composed = "Caf\u00e9.ini";
            const string decomposed = "Cafe\u0301.ini";
            using SkinPackageRevisionCapsule first = createSuccess(
                SkinPackageCapturedEntry.CreateFile(composed, new byte[] { 1 }));
            using SkinPackageRevisionCapsule second = createSuccess(
                SkinPackageCapturedEntry.CreateFile(decomposed, new byte[] { 1 }));
            using IResourceStore<byte[]> view = first.CreateResourceView();

            Assert.Multiple(() =>
            {
                Assert.That(second.ContentRevision, Is.EqualTo(first.ContentRevision));
                Assert.That(view.Get(decomposed), Is.EqualTo(new byte[] { 1 }));
            });
        }

        [Test]
        public void TestRevisionChangesWithNameCaseLengthOrContent()
        {
            using SkinPackageRevisionCapsule baseline = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }));
            using SkinPackageRevisionCapsule renamed = createSuccess(SkinPackageCapturedEntry.CreateFile("other.ini", new byte[] { 1 }));
            using SkinPackageRevisionCapsule recased = createSuccess(SkinPackageCapturedEntry.CreateFile("Skin.ini", new byte[] { 1 }));
            using SkinPackageRevisionCapsule resized = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 0 }));
            using SkinPackageRevisionCapsule changed = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 2 }));

            Assert.That(new[]
            {
                baseline.ContentRevision,
                renamed.ContentRevision,
                recased.ContentRevision,
                resized.ContentRevision,
                changed.ContentRevision,
            }.Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void TestEmptyDirectoriesDoNotAffectRevision()
        {
            using SkinPackageRevisionCapsule baseline = createSuccess(SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }));
            using SkinPackageRevisionCapsule withDirectories = createSuccess(
                SkinPackageCapturedEntry.CreateDirectory("empty"),
                SkinPackageCapturedEntry.CreateDirectory("empty/nested"),
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1 }));

            Assert.That(withDirectories.ContentRevision, Is.EqualTo(baseline.ContentRevision));
        }

        [TestCase("skin.ini", "SKIN.INI")]
        [TestCase("Caf\u00e9.ini", "Cafe\u0301.ini")]
        public void TestCaseAndUnicodeNormalisationDuplicatesAreRejected(string first, string second)
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath,
                SkinPackageCapturedEntry.CreateFile(first, Array.Empty<byte>()),
                SkinPackageCapturedEntry.CreateFile(second, Array.Empty<byte>()));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("/skin.ini")]
        [TestCase("skin.ini/")]
        [TestCase(@"\skin.ini")]
        [TestCase(@"C:\skin.ini")]
        [TestCase("../skin.ini")]
        [TestCase("folder/../skin.ini")]
        [TestCase("folder/./skin.ini")]
        [TestCase("folder//skin.ini")]
        [TestCase("skin.ini:stream")]
        [TestCase("skin.ini.")]
        [TestCase("skin.ini ")]
        [TestCase("CON")]
        [TestCase("nul.png")]
        [TestCase("COM1.wav")]
        [TestCase("LPT\u00b3.png")]
        [TestCase("folder/<skin>.ini")]
        [TestCase("folder/skin?.ini")]
        [TestCase("folder/\u001fskin.ini")]
        public void TestInvalidResourceNamesAreRejected(string? resourceName)
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName,
                SkinPackageCapturedEntry.CreateFile(resourceName, Array.Empty<byte>()));
        }

        [Test]
        public void TestUnpairedSurrogateResourceNameIsRejected()
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName,
                SkinPackageCapturedEntry.CreateFile(new string('\ud800', 1), Array.Empty<byte>()));
        }

        [Test]
        public void TestDuplicateKindAndFileDirectoryCollisionAreDistinguished()
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath,
                SkinPackageCapturedEntry.CreateDirectory("same"),
                SkinPackageCapturedEntry.CreateDirectory("SAME"));

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.PathTypeConflict,
                SkinPackageCapturedEntry.CreateDirectory("same"),
                SkinPackageCapturedEntry.CreateFile("SAME", Array.Empty<byte>()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestFileCannotBeAncestorOfAnotherEntry(bool fileComesFirst)
        {
            SkinPackageCapturedEntry file = SkinPackageCapturedEntry.CreateFile("parent", Array.Empty<byte>());
            SkinPackageCapturedEntry child = SkinPackageCapturedEntry.CreateFile("parent/child.ini", Array.Empty<byte>());

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.PathTypeConflict,
                fileComesFirst ? file : child,
                fileComesFirst ? child : file);
        }

        [Test]
        public void TestSimilarPrefixIsNotAPathConflict()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateFile("parent", new byte[] { 1 }),
                SkinPackageCapturedEntry.CreateFile("parent-other/child.ini", new byte[] { 2 }));

            Assert.That(capsule.FileCount, Is.EqualTo(2));
        }

        [Test]
        public void TestEmptyAndUnsupportedPackagesAreRejected()
        {
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.EmptyPackage);
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.EmptyPackage,
                SkinPackageCapturedEntry.CreateDirectory("empty"));
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.UnsupportedEntryKind,
                new SkinPackageCapturedEntry((SkinPackageCapturedEntryKind)99, "skin.ini"));
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.UnsupportedEntryKind,
                new SkinPackageCapturedEntry?[] { null });
        }

        [Test]
        public void TestEntryCountBudgetBoundaryIncludesSyntheticDirectories()
        {
            SkinPackageCapturedEntry[] entries =
            {
                SkinPackageCapturedEntry.CreateDirectory("empty"),
                SkinPackageCapturedEntry.CreateFile("nested/skin.ini", Array.Empty<byte>()),
            };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxEntryCount: 3), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded, limits(maxEntryCount: 2), entries);
        }

        [Test]
        public void TestFileCountBudgetBoundary()
        {
            SkinPackageCapturedEntry[] entries =
            {
                SkinPackageCapturedEntry.CreateFile("a", Array.Empty<byte>()),
                SkinPackageCapturedEntry.CreateFile("b", Array.Empty<byte>()),
            };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxFileCount: 2), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.FileCountBudgetExceeded, limits(maxFileCount: 1), entries);
        }

        [Test]
        public void TestDepthBudgetBoundary()
        {
            SkinPackageCapturedEntry[] entries = { SkinPackageCapturedEntry.CreateFile("nested/skin.ini", Array.Empty<byte>()) };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxDepth: 2), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.DepthBudgetExceeded, limits(maxDepth: 1), entries);
        }

        [Test]
        public void TestResourceNameBudgetBoundary()
        {
            SkinPackageCapturedEntry[] entries = { SkinPackageCapturedEntry.CreateFile("abcd", Array.Empty<byte>()) };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxResourceNameLength: 4), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.ResourceNameBudgetExceeded, limits(maxResourceNameLength: 3), entries);
        }

        [Test]
        public void TestFileByteBudgetBoundary()
        {
            SkinPackageCapturedEntry[] entries = { SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }) };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxFileBytes: 3), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded, limits(maxFileBytes: 2), entries);
        }

        [Test]
        public void TestPackageByteBudgetBoundary()
        {
            SkinPackageCapturedEntry[] entries =
            {
                SkinPackageCapturedEntry.CreateFile("a", new byte[] { 1, 2 }),
                SkinPackageCapturedEntry.CreateFile("b", new byte[] { 3, 4, 5 }),
            };

            using SkinPackageRevisionCapsule capsule = createSuccess(limits(maxPackageBytes: 5), entries);
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded, limits(maxPackageBytes: 4), entries);
        }

        [Test]
        public void TestInvalidLimitsAreRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => limits(maxEntryCount: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxFileCount: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxDepth: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxResourceNameLength: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxFileBytes: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxFileBytes: (long)int.MaxValue + 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => limits(maxPackageBytes: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestDefaultProductionBudgetsAreFixed()
        {
            SkinPackageRevisionCapsuleLimits defaults = SkinPackageRevisionCapsuleLimits.Default;

            Assert.Multiple(() =>
            {
                Assert.That(defaults.MaxEntryCount, Is.EqualTo(8192));
                Assert.That(defaults.MaxFileCount, Is.EqualTo(8192));
                Assert.That(defaults.MaxDepth, Is.EqualTo(32));
                Assert.That(defaults.MaxResourceNameLength, Is.EqualTo(512));
                Assert.That(defaults.MaxFileBytes, Is.EqualTo(64L * 1024 * 1024));
                Assert.That(defaults.MaxPackageBytes, Is.EqualTo(512L * 1024 * 1024));
                Assert.That(defaults.MaxFileCount, Is.LessThanOrEqualTo(defaults.MaxEntryCount));
                Assert.That(defaults.MaxFileBytes, Is.LessThanOrEqualTo(defaults.MaxPackageBytes));
            });
        }

        [Test]
        public void TestStructuralAndBudgetValidationNeverOpensSources()
        {
            int opened = 0;

            SkinPackageCapturedEntry tracked(string name, int length = 1)
                => SkinPackageCapturedEntry.CreateFile(name, length, () =>
                {
                    opened++;
                    return new MemoryStream(new byte[length]);
                });

            assertRejected(SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName, tracked("valid"), tracked("../invalid"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath, tracked("same"), tracked("SAME"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded, limits(maxEntryCount: 1), tracked("nested/file"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.FileCountBudgetExceeded, limits(maxFileCount: 1), tracked("a"), tracked("b"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.DepthBudgetExceeded, limits(maxDepth: 1), tracked("nested/file"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.ResourceNameBudgetExceeded, limits(maxResourceNameLength: 1), tracked("ab"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded, limits(maxFileBytes: 0), tracked("file"));
            assertRejected(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded, limits(maxPackageBytes: 1), tracked("a"), tracked("b"));

            Assert.That(opened, Is.Zero);
        }

        [Test]
        public void TestNegativeDeclaredLengthIsRejectedWithoutOpeningSource()
        {
            bool opened = false;

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.InvalidDeclaredLength,
                SkinPackageCapturedEntry.CreateFile("skin.ini", -1, () =>
                {
                    opened = true;
                    return new MemoryStream();
                }));

            Assert.That(opened, Is.False);
        }

        [TestCase(2, 3)]
        [TestCase(4, 3)]
        public void TestDeclaredLengthMismatchIsRejectedAndDisposesStream(int declaredLength, int actualLength)
        {
            var stream = new TrackingMemoryStream(Enumerable.Range(0, actualLength).Select(value => (byte)value).ToArray());

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceLengthMismatch,
                SkinPackageCapturedEntry.CreateFile("skin.ini", declaredLength, () => stream));

            Assert.Multiple(() =>
            {
                Assert.That(stream.IsDisposed, Is.True);
                Assert.That(stream.LastReadBuffer, Is.All.Zero);
            });
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TestMalformedReadCountIsRejected(int returnedCount)
        {
            var stream = new MalformedReadStream(returnedCount);

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceLengthMismatch,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () => stream));

            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void TestZeroLengthFileIsAccepted()
        {
            using SkinPackageRevisionCapsule capsule = createSuccess(
                limits(maxFileBytes: 0, maxPackageBytes: 0),
                SkinPackageCapturedEntry.CreateFile("empty", Array.Empty<byte>()));

            Assert.Multiple(() =>
            {
                Assert.That(capsule.FileCount, Is.EqualTo(1));
                Assert.That(capsule.TotalBytes, Is.Zero);
            });
        }

        [Test]
        public void TestNullSourceIsRejected()
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceUnavailable,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 0, () => null));
        }

        [Test]
        public void TestUnreadableSourceIsRejectedAndDisposed()
        {
            var stream = new NonReadableStream();

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceNotReadable,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 0, () => stream));

            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void TestOpenAndReadIoFailuresAreTyped()
        {
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () => throw new IOException("open failed")));

            var stream = new ThrowingReadStream();
            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () => stream));

            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void TestAllExpectedOpenFailuresAreTyped()
        {
            foreach (Exception exception in new Exception[]
                     {
                         new IOException("io"),
                         new UnauthorizedAccessException("unauthorised"),
                         new NotSupportedException("unsupported"),
                         new ObjectDisposedException("source"),
                         new SecurityException("security"),
                     })
            {
                assertRejected(
                    SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed,
                    SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () => throw exception));
            }
        }

        [Test]
        public void TestPartialReadFailureZeroesCurrentBuffer()
        {
            var stream = new PartialThenThrowStream(new IOException("read failed"));

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed,
                SkinPackageCapturedEntry.CreateFile("skin.ini", 2, () => stream));

            Assert.Multiple(() =>
            {
                Assert.That(stream.IsDisposed, Is.True);
                Assert.That(stream.LastReadBuffer, Is.All.Zero);
            });
        }

        [Test]
        public void TestUnexpectedFailurePropagatesAndZeroesAllBuffers()
        {
            var first = new TrackingMemoryStream(new byte[] { 1 });
            var second = new PartialThenThrowStream(new InvalidOperationException("unexpected"));

            Assert.That(() => SkinPackageRevisionCapsuleFactory.Create(new[]
            {
                SkinPackageCapturedEntry.CreateFile("a", 1, () => first),
                SkinPackageCapturedEntry.CreateFile("b", 2, () => second),
            }), Throws.TypeOf<InvalidOperationException>());

            Assert.Multiple(() =>
            {
                Assert.That(first.IsDisposed, Is.True);
                Assert.That(first.LastReadBuffer, Is.All.Zero);
                Assert.That(second.IsDisposed, Is.True);
                Assert.That(second.LastReadBuffer, Is.All.Zero);
            });
        }

        [Test]
        public void TestCancellationBeforeCaptureDoesNotOpenSource()
        {
            bool opened = false;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.That(() => SkinPackageRevisionCapsuleFactory.Create(
                new[]
                {
                    SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () =>
                    {
                        opened = true;
                        return new MemoryStream(new byte[] { 1 });
                    }),
                }, cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(opened, Is.False);
        }

        [Test]
        public void TestCancellationDuringReadDisposesOpenStream()
        {
            using var cancellation = new CancellationTokenSource();
            var stream = new TrackingMemoryStream(new byte[] { 1, 2, 3 }, maxReadSize: 1, afterRead: cancellation.Cancel);

            Assert.That(() => SkinPackageRevisionCapsuleFactory.Create(
                new[] { SkinPackageCapturedEntry.CreateFile("skin.ini", 3, () => stream) },
                cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.Multiple(() =>
            {
                Assert.That(stream.IsDisposed, Is.True);
                Assert.That(stream.LastReadBuffer, Is.All.Zero);
            });
        }

        [Test]
        public void TestCancellationBetweenFilesDisposesFirstAndDoesNotOpenSecond()
        {
            using var cancellation = new CancellationTokenSource();
            var first = new TrackingMemoryStream(new byte[] { 1 }, onDispose: cancellation.Cancel);
            bool openedSecond = false;

            Assert.That(() => SkinPackageRevisionCapsuleFactory.Create(
                new[]
                {
                    SkinPackageCapturedEntry.CreateFile("a", 1, () => first),
                    SkinPackageCapturedEntry.CreateFile("b", 1, () =>
                    {
                        openedSecond = true;
                        return new MemoryStream(new byte[] { 2 });
                    }),
                }, cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(first.IsDisposed, Is.True);
                Assert.That(openedSecond, Is.False);
            });
        }

        [Test]
        public void TestCancellationFromLastSourceDisposalCleansOwnedBacking()
        {
            using var cancellation = new CancellationTokenSource();
            var stream = new TrackingMemoryStream(new byte[] { 1 }, onDispose: cancellation.Cancel);

            Assert.That(() => SkinPackageRevisionCapsuleFactory.Create(
                new[] { SkinPackageCapturedEntry.CreateFile("skin.ini", 1, () => stream) },
                cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(stream.IsDisposed, Is.True);
                Assert.That(stream.LastReadBuffer, Is.All.Zero);
            });
        }

        [Test]
        public void TestFailureInLastFileDisposesEveryOpenedStream()
        {
            var first = new TrackingMemoryStream(new byte[] { 1 });
            var second = new ThrowingReadStream();

            assertRejected(
                SkinPackageRevisionCapsuleRejectionReason.SourceReadFailed,
                SkinPackageCapturedEntry.CreateFile("a", 1, () => first),
                SkinPackageCapturedEntry.CreateFile("b", 1, () => second));

            Assert.Multiple(() =>
            {
                Assert.That(first.IsDisposed, Is.True);
                Assert.That(second.IsDisposed, Is.True);
                Assert.That(first.LastReadBuffer, Is.All.Zero);
            });
        }

        [Test]
        public void TestCapsuleDisposalZeroesOwnedBacking()
        {
            var stream = new TrackingMemoryStream(new byte[] { 1, 2, 3 });
            SkinPackageRevisionCapsule capsule = createSuccess(
                SkinPackageCapturedEntry.CreateFile("skin.ini", 3, () => stream));

            Assert.That(stream.LastReadBuffer, Is.EqualTo(new byte[] { 1, 2, 3 }));

            capsule.Dispose();

            Assert.That(stream.LastReadBuffer, Is.All.Zero);
        }

        [Test]
        public void TestToStringDoesNotLeakNamesHashesOrRevision()
        {
            const string secret = "do-not-log-this-name.ini";
            SkinPackageCapturedEntry entry = SkinPackageCapturedEntry.CreateFile(secret, new byte[] { 1, 2, 3 });
            SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(new[] { entry });
            using SkinPackageRevisionCapsule capsule = result.Capsule!;
            SkinPackageFileRevision file = capsule.Files[0];

            Assert.Multiple(() =>
            {
                Assert.That(entry.ToString(), Does.Not.Contain(secret));
                Assert.That(result.ToString(), Does.Not.Contain(secret));
                Assert.That(result.ToString(), Does.Not.Contain(capsule.ContentRevision));
                Assert.That(capsule.ToString(), Does.Not.Contain(secret));
                Assert.That(capsule.ToString(), Does.Not.Contain(capsule.ContentRevision));
                Assert.That(file.ToString(), Does.Not.Contain(secret));
                Assert.That(file.ToString(), Does.Not.Contain(file.ContentHash));
                Assert.That(SkinPackageRevisionCapsuleLimits.Default.ToString(), Does.Not.Contain(secret));
            });
        }

        private static SkinPackageRevisionCapsule createSuccess(params SkinPackageCapturedEntry?[] entries)
            => createSuccess(null, entries);

        private static SkinPackageRevisionCapsule createSuccess(SkinPackageRevisionCapsuleLimits? capsuleLimits, params SkinPackageCapturedEntry?[] entries)
        {
            SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(entries, capsuleLimits);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinPackageRevisionCapsuleRejectionReason.None));
                Assert.That(result.Capsule, Is.Not.Null);
            });

            return result.Capsule!;
        }

        private static void assertRejected(SkinPackageRevisionCapsuleRejectionReason expectedReason, params SkinPackageCapturedEntry?[] entries)
            => assertRejected(expectedReason, null, entries);

        private static void assertRejected(
            SkinPackageRevisionCapsuleRejectionReason expectedReason,
            SkinPackageRevisionCapsuleLimits? capsuleLimits,
            params SkinPackageCapturedEntry?[] entries)
        {
            SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(entries, capsuleLimits);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(expectedReason));
                Assert.That(result.Capsule, Is.Null);
            });
        }

        private static SkinPackageRevisionCapsuleLimits limits(
            int maxEntryCount = 16,
            int maxFileCount = 16,
            int maxDepth = 8,
            int maxResourceNameLength = 128,
            long maxFileBytes = 128,
            long maxPackageBytes = 256)
            => new SkinPackageRevisionCapsuleLimits(
                maxEntryCount,
                maxFileCount,
                maxDepth,
                maxResourceNameLength,
                maxFileBytes,
                maxPackageBytes);

        private static byte[] readAll(Stream stream)
        {
            using var output = new MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
        }

        private sealed class TrackingMemoryStream : MemoryStream
        {
            private readonly int maxReadSize;
            private readonly Action? afterRead;
            private readonly Action? onDispose;

            public bool IsDisposed { get; private set; }

            public byte[]? LastReadBuffer { get; private set; }

            public TrackingMemoryStream(byte[] content, int maxReadSize = int.MaxValue, Action? afterRead = null, Action? onDispose = null)
                : base(content, writable: false)
            {
                this.maxReadSize = maxReadSize;
                this.afterRead = afterRead;
                this.onDispose = onDispose;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                LastReadBuffer = buffer;
                int read = base.Read(buffer, offset, Math.Min(count, maxReadSize));
                afterRead?.Invoke();
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                IsDisposed = true;
                onDispose?.Invoke();
            }
        }

        private sealed class MalformedReadStream : Stream
        {
            private readonly int returnedCount;

            public bool IsDisposed { get; private set; }

            public MalformedReadStream(int returnedCount)
            {
                this.returnedCount = returnedCount;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => returnedCount;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class PartialThenThrowStream : Stream
        {
            private readonly Exception failure;
            private bool returnedPartialContent;

            public bool IsDisposed { get; private set; }

            public byte[]? LastReadBuffer { get; private set; }

            public PartialThenThrowStream(Exception failure)
            {
                this.failure = failure;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                LastReadBuffer = buffer;

                if (returnedPartialContent)
                    throw failure;

                returnedPartialContent = true;
                buffer[offset] = 42;
                return 1;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class NonReadableStream : Stream
        {
            public bool IsDisposed { get; private set; }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class ThrowingReadStream : Stream
        {
            public bool IsDisposed { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new IOException("read failed");
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
