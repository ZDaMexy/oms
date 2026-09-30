// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.Bms;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BmsDownloadManagerTest
    {
        private const string md5_a = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string md5_b = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private static readonly Guid beatmap_id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly TimeSpan wait_limit = TimeSpan.FromSeconds(10);
        private static readonly byte[] archive_bytes = Encoding.UTF8.GetBytes("archive bytes for the importer");

        private TemporaryNativeStorage storage = null!;
        private string playerFile = null!;
        private string retainedArchive = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"bms-download-manager-{Guid.NewGuid():N}");
            playerFile = storage.GetFullPath("chartbms/player-owned/song.bms");
            retainedArchive = storage.GetFullPath("bms-downloads/preexisting-recovery/archive.7z");
            Directory.CreateDirectory(Path.GetDirectoryName(playerFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(retainedArchive)!);
            File.WriteAllText(playerFile, "prior player data");
            File.WriteAllText(retainedArchive, "prior recovery archive");
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public async Task TestStreamedArchiveReachesOwnedStagingAndReturnsTheSelectedChartIdentity()
        {
            var stream = new ProbeStream(archive_bytes, chunkSize: 2);
            var handler = new StubHandler((_, _) => Task.FromResult(responseFor(stream)));
            string? importedPath = null;
            var importer = new StubImporter((path, hashes, _) =>
            {
                importedPath = path;
                Assert.That(Path.GetRelativePath(storage.GetFullPath("bms-downloads"), path), Does.Not.StartWith(".."));
                Assert.That(Path.IsPathRooted(Path.GetRelativePath(storage.GetFullPath("bms-downloads"), path)), Is.False);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(archive_bytes));
                Assert.That(hashes, Is.EqualTo(new[] { md5_b }));
                Assert.That(stream.Released, Is.True);
                return importedResult(md5_b);
            });
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            var seen = new ConcurrentQueue<BmsDownloadState>();
            manager.TaskChanged += task => seen.Enqueue(task.Progress.State);

            // 616's display name has no archive extension; the downloaded contents belong to the importer.
            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
            Assert.That(task.Progress.Bytes, Is.EqualTo(archive_bytes.Length));
            Assert.That(task.Progress.TotalBytes, Is.EqualTo(archive_bytes.Length));
            Assert.That(task.Progress.Imported, Is.EqualTo(new[] { new BmsDownloadImportedBeatmap(beatmap_id, md5_b) }));
            Assert.That(manager.GetTask(task.Package.Key), Is.SameAs(task));
            Assert.That(importer.Calls, Is.EqualTo(1));
            Assert.That(stream.ReadCalls, Is.GreaterThan(1));
            Assert.That(seen, Does.Contain(BmsDownloadState.Downloading));
            Assert.That(seen, Does.Contain(BmsDownloadState.Importing));
            Assert.That(seen, Does.Contain(BmsDownloadState.Completed));
            Assert.That(File.Exists(importedPath), Is.False);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestQueuedDownloadIsDeduplicatedCanBeCancelledAndCanBeRetried()
        {
            var firstImportStarted = signal();
            var releaseFirstImport = signal();
            var paths = new ConcurrentQueue<string>();
            var importer = new StubImporter(async (path, hashes, token) =>
            {
                paths.Enqueue(path);

                if (paths.Count == 1)
                {
                    firstImportStarted.TrySetResult(true);
                    await releaseFirstImport.Task.WaitAsync(wait_limit);
                }

                token.ThrowIfCancellationRequested();
                return await importedResult(hashes.Single());
            });
            var handler = new StubHandler((_, _) => Task.FromResult(responseFor(new ProbeStream(archive_bytes))));
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            try
            {
                BmsDownloadTask first = manager.Download(createPackage("first"), md5_b);
                await firstImportStarted.Task.WaitAsync(wait_limit);
                BmsDownloadPackage queuedPackage = createPackage("queued");
                BmsDownloadTask queued = manager.Download(queuedPackage, md5_b);

                Assert.That(queued.Progress.State, Is.EqualTo(BmsDownloadState.Queued));
                Assert.That(manager.Download(queuedPackage, md5_b), Is.SameAs(queued));
                queued.Cancel();
                await queued.Completion.WaitAsync(wait_limit);
                Assert.That(queued.Progress.State, Is.EqualTo(BmsDownloadState.Cancelled));
                Assert.That(handler.Requests, Is.EqualTo(1));

                BmsDownloadTask retry = manager.Download(queuedPackage, md5_b);
                Assert.That(retry, Is.Not.SameAs(queued));
                Assert.That(retry.Progress.State, Is.EqualTo(BmsDownloadState.Queued));
                queued.Cancel();
                releaseFirstImport.TrySetResult(true);
                await Task.WhenAll(first.Completion, retry.Completion).WaitAsync(wait_limit);

                Assert.That(retry.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
                Assert.That(manager.GetTask(queuedPackage.Key), Is.SameAs(retry));
                Assert.That(handler.Requests, Is.EqualTo(2));
                Assert.That(paths.Select(path => Path.GetDirectoryName(path)).Distinct().Count(), Is.EqualTo(2));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseFirstImport.TrySetResult(true);
            }
        }

        [Test]
        public async Task TestClosingAnObserverKeepsTheGameOwnedDownloadRunning()
        {
            var importStarted = signal();
            var releaseImport = signal();
            var importer = new StubImporter(async (_, hashes, token) =>
            {
                importStarted.TrySetResult(true);
                await releaseImport.Task.WaitAsync(wait_limit);
                token.ThrowIfCancellationRequested();
                return await importedResult(hashes.Single());
            });
            using var http = successfulHttpClient();
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            int observations = 0;
            void observe(BmsDownloadTask _) => Interlocked.Increment(ref observations);
            manager.TaskChanged += observe;

            try
            {
                BmsDownloadTask task = manager.Download(createPackage(), md5_b);
                await importStarted.Task.WaitAsync(wait_limit);
                manager.TaskChanged -= observe;
                int atClose = Volatile.Read(ref observations);
                releaseImport.TrySetResult(true);
                await task.Completion.WaitAsync(wait_limit);

                Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
                Assert.That(Volatile.Read(ref observations), Is.EqualTo(atClose));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseImport.TrySetResult(true);
                manager.TaskChanged -= observe;
            }
        }

        [TestCase("https://gingerrush.com/archive/package.zip")]
        [TestCase("https://pixeldrain.net/archive/package.zip")]
        [TestCase("https://bms.alvorna.com:443/archive/package.zip")]
        public async Task TestExactApprovedHttpsHostsAreAccepted(string url)
        {
            var importer = successfulImporter();
            var handler = new StubHandler((request, _) =>
            {
                Assert.That(request.RequestUri, Is.EqualTo(new Uri(url)));
                return Task.FromResult(responseFor(new ProbeStream(archive_bytes)));
            });
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(url: url), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
            Assert.That(importer.Calls, Is.EqualTo(1));
            assertStoragePreservedAndClean();
        }

        [TestCase("http://gingerrush.com/archive.zip")]
        [TestCase("https://pixeldrain.net:8443/archive.zip")]
        [TestCase("https://user:password@gingerrush.com/archive.zip")]
        [TestCase("https://gingerrush.com.example.org/archive.zip")]
        [TestCase("https://example.org/archive.zip")]
        [TestCase("https://localhost/archive.zip")]
        [TestCase("https://127.0.0.1/archive.zip")]
        public async Task TestUnsupportedPackageAddressNeverReachesTheNetwork(string url)
        {
            var handler = new StubHandler((_, _) => throw new InvalidOperationException("An unauthorised package URI must not reach HTTP."));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(url: url), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(handler.Requests, Is.Zero);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase("http://pixeldrain.net/archive.zip")]
        [TestCase("https://bms.alvorna.com:8443/archive.zip")]
        [TestCase("https://pixeldrain.net.example.org/archive.zip")]
        [TestCase("https://127.0.0.1/archive.zip")]
        public async Task TestRedirectCannotEscapeTheApprovedDownloadAddresses(string redirectUrl)
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri(redirectUrl) }
            }));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestRelativeRedirectOnAnApprovedHostCanComplete()
        {
            var requests = new ConcurrentQueue<Uri>();
            var handler = new StubHandler((request, _) =>
            {
                requests.Enqueue(request.RequestUri!);
                return Task.FromResult(requests.Count == 1
                    ? new HttpResponseMessage(HttpStatusCode.TemporaryRedirect) { Headers = { Location = new Uri("/bms/zipped/final.7z", UriKind.Relative) } }
                    : responseFor(new ProbeStream(archive_bytes)));
            });
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, successfulImporter(), downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
            Assert.That(requests.Last(), Is.EqualTo(new Uri("https://bms.alvorna.com/bms/zipped/final.7z")));
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestOversizedCatalogueSizeIsRejectedBeforeMakingARequest()
        {
            var handler = new StubHandler((_, _) => throw new InvalidOperationException("An oversized catalogue package must not be requested."));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(size: BmsDownloadManager.MaximumPackageBytes + 1), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(handler.Requests, Is.Zero);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(0L)]
        [TestCase(BmsDownloadManager.MaximumPackageBytes + 1)]
        public async Task TestInvalidDeclaredSizeIsRejectedWithoutReadingTheBody(long length)
        {
            var stream = new ProbeStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream, length))));
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(stream.ReadCalls, Is.Zero);
            Assert.That(stream.Released, Is.True);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(0, null)]
        [TestCase(1, 2L)]
        [TestCase(5, 1L)]
        public async Task TestEmptyTruncatedAndFalseLengthBodiesAreNotImported(int bytes, long? declaredLength)
        {
            var stream = new ProbeStream(new byte[bytes]);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream, declaredLength))));
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(stream.Released, Is.True);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestNetworkFailureBeforeOrDuringBodyReadFailsAndCleansOnlyThisTask(bool duringRead)
        {
            var stream = new InterruptedStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => duringRead
                ? Task.FromResult(responseFor(stream))
                : Task.FromException<HttpResponseMessage>(new HttpRequestException("Expected connection failure."))));
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);

            BmsDownloadTask task = manager.Download(createPackage(), md5_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
            Assert.That(importer.Calls, Is.Zero);
            if (duringRead)
                Assert.That(stream.Released, Is.True);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestImporterIoFailureIsReportedAndARegisteredTaskCanBeRetriedFromItsTerminalEvent()
        {
            var firstImportStarted = signal();
            var releaseFailure = signal();
            var retryImportStarted = signal();
            var releaseRetry = signal();
            var retryCreated = signal();
            int importCalls = 0;
            string? firstPath = null;
            string? retryPath = null;
            BmsDownloadTask? retry = null;
            var importer = new StubImporter(async (path, hashes, token) =>
            {
                if (Interlocked.Increment(ref importCalls) == 1)
                {
                    firstPath = path;
                    firstImportStarted.TrySetResult(true);
                    await releaseFailure.Task.WaitAsync(wait_limit);
                    throw new IOException("Expected importer failure.");
                }

                retryPath = path;
                retryImportStarted.TrySetResult(true);
                await releaseRetry.Task.WaitAsync(wait_limit);
                token.ThrowIfCancellationRequested();
                return await importedResult(hashes.Single());
            });
            using var http = successfulHttpClient();
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            BmsDownloadPackage package = createPackage();
            bool cleanedBeforeTerminalEvent = false;

            try
            {
                BmsDownloadTask first = manager.Download(package, md5_b);
                await firstImportStarted.Task.WaitAsync(wait_limit);
                manager.TaskChanged += changed =>
                {
                    if (!ReferenceEquals(changed, first) || changed.Progress.State != BmsDownloadState.Failed)
                        return;

                    cleanedBeforeTerminalEvent = !Directory.Exists(Path.GetDirectoryName(firstPath!)!);
                    changed.Cancel();
                    retry = manager.Download(package, md5_b);
                    retryCreated.TrySetResult(true);
                };
                releaseFailure.TrySetResult(true);
                await retryCreated.Task.WaitAsync(wait_limit);
                await retryImportStarted.Task.WaitAsync(wait_limit);

                Assert.That(cleanedBeforeTerminalEvent, Is.True);
                Assert.That(first.Progress.State, Is.EqualTo(BmsDownloadState.Failed));
                Assert.That(retry, Is.Not.SameAs(first));
                Assert.That(manager.GetTask(package.Key), Is.SameAs(retry));
                Assert.That(Path.GetDirectoryName(firstPath), Is.Not.EqualTo(Path.GetDirectoryName(retryPath)));
                releaseRetry.TrySetResult(true);
                await Task.WhenAll(first.Completion, retry!.Completion).WaitAsync(wait_limit);
                Assert.That(retry.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseFailure.TrySetResult(true);
                releaseRetry.TrySetResult(true);
            }
        }

        [Test]
        public async Task TestDisposeWaitsForTheCancelledImporterToWithdraw()
        {
            var importStarted = signal();
            var cancellationObserved = signal();
            var releaseImport = signal();
            string? activeArchive = null;
            var importer = new StubImporter(async (path, hashes, token) =>
            {
                activeArchive = path;
                using var registration = token.Register(() => cancellationObserved.TrySetResult(true));
                importStarted.TrySetResult(true);
                await releaseImport.Task.WaitAsync(wait_limit);
                token.ThrowIfCancellationRequested();
                return await importedResult(hashes.Single());
            });
            using var http = successfulHttpClient();
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            Task? disposal = null;

            try
            {
                BmsDownloadTask task = manager.Download(createPackage(), md5_b);
                await importStarted.Task.WaitAsync(wait_limit);
                disposal = Task.Run(manager.Dispose);
                await cancellationObserved.Task.WaitAsync(wait_limit);

                Assert.That(disposal.IsCompleted, Is.False);
                Assert.That(File.Exists(activeArchive), Is.True);
                releaseImport.TrySetResult(true);
                await disposal.WaitAsync(wait_limit);
                Assert.That(task.Completion.IsCompleted, Is.True);
                Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Cancelled));
                Assert.Throws<ObjectDisposedException>(() => manager.Download(createPackage("after-dispose"), md5_b));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseImport.TrySetResult(true);
                if (disposal != null)
                    await disposal.WaitAsync(wait_limit);
            }
        }

        [Test]
        public async Task TestDisposeWaitsForTheCancelledStreamToReleaseBeforeReturning()
        {
            var stream = new GatedStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream))));
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            Task? disposal = null;

            try
            {
                BmsDownloadTask task = manager.Download(createPackage(), md5_b);
                await stream.Started.WaitAsync(wait_limit);
                disposal = Task.Run(manager.Dispose);
                await stream.CancellationObserved.WaitAsync(wait_limit);

                Assert.That(disposal.IsCompleted, Is.False);
                Assert.That(stream.Released, Is.False);
                stream.ReleaseRead();
                await disposal.WaitAsync(wait_limit);
                Assert.That(stream.Released, Is.True);
                Assert.That(task.Completion.IsCompleted, Is.True);
                Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Cancelled));
                Assert.That(importer.Calls, Is.Zero);
                assertStoragePreservedAndClean();
            }
            finally
            {
                stream.ReleaseRead();
                if (disposal != null)
                    await disposal.WaitAsync(wait_limit);
            }
        }

        [Test]
        public void TestUnavailablePackageAndWrongSelectedChartDoNotCreateTasks()
        {
            var importer = successfulImporter();
            var handler = new StubHandler((_, _) => throw new InvalidOperationException("Rejected selections must not create requests."));
            using var http = new HttpClient(handler);
            using var manager = new BmsDownloadManager(storage, importer, downloads: http);
            BmsDownloadPackage package = createPackage();

            Assert.Throws<InvalidOperationException>(() => manager.Download(package with { DownloadUrl = null }, md5_b));
            Assert.Throws<ArgumentException>(() => manager.Download(package, "cccccccccccccccccccccccccccccccc"));
            Assert.That(manager.GetTask(package.Key), Is.Null);
            Assert.That(importer.Calls, Is.Zero);
            Assert.That(handler.Requests, Is.Zero);
            assertStoragePreservedAndClean();
        }

        private void assertStoragePreservedAndClean()
        {
            Assert.That(File.ReadAllText(playerFile), Is.EqualTo("prior player data"));
            Assert.That(File.ReadAllText(retainedArchive), Is.EqualTo("prior recovery archive"));
            Assert.That(Directory.GetDirectories(storage.GetFullPath("bms-downloads")).Select(path => Path.GetFileName(path)), Is.EqualTo(new[] { "preexisting-recovery" }));
        }

        private static BmsDownloadPackage createPackage(string id = "song", string url = "https://bms.alvorna.com/bms/zipped/song.7z", long? size = null)
            => new BmsDownloadPackage(BmsDownloadSource.Konmai, id, "Song display name", new Uri(url), new[]
            {
                new BmsDownloadChart(md5_a, "Song", "Author", "NORMAL"),
                new BmsDownloadChart(md5_b, "Song", "Author", "HYPER")
            }, size);

        private static TaskCompletionSource<bool> signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static Task<IReadOnlyList<BmsDownloadImportedBeatmap>> importedResult(string md5)
            => Task.FromResult<IReadOnlyList<BmsDownloadImportedBeatmap>>(new[] { new BmsDownloadImportedBeatmap(beatmap_id, md5) });

        private static StubImporter successfulImporter() => new StubImporter((_, hashes, _) => importedResult(hashes.Single()));

        private static HttpClient successfulHttpClient() => new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(new ProbeStream(archive_bytes)))));

        private static HttpResponseMessage responseFor(Stream stream, long? length = null)
        {
            var content = new StreamContent(stream);
            if (length.HasValue)
                content.Headers.ContentLength = length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
            private int requests;
            public int Requests => Volatile.Read(ref requests);

            public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => this.send = send;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref requests);
                return send(request, cancellationToken);
            }
        }

        private sealed class StubImporter : IBmsDownloadImporter
        {
            private readonly Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyList<BmsDownloadImportedBeatmap>>> import;
            private int calls;
            public int Calls => Volatile.Read(ref calls);

            public StubImporter(Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyList<BmsDownloadImportedBeatmap>>> import) => this.import = import;

            public Task<IReadOnlyList<BmsDownloadImportedBeatmap>> ImportAsync(string archivePath, IReadOnlyCollection<string> expectedMd5s, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref calls);
                return import(archivePath, expectedMd5s, cancellationToken);
            }
        }

        private class ProbeStream : MemoryStream
        {
            private readonly int chunkSize;
            private int readCalls;
            public int ReadCalls => Volatile.Read(ref readCalls);
            public bool Released { get; private set; }
            public override bool CanSeek => false;

            public ProbeStream(byte[] bytes, int chunkSize = int.MaxValue)
                : base(bytes)
            {
                this.chunkSize = chunkSize;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref readCalls);
                return base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                Released = true;
                base.Dispose(disposing);
            }
        }

        private sealed class InterruptedStream : ProbeStream
        {
            public InterruptedStream(byte[] bytes)
                : base(bytes, chunkSize: 1)
            {
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (ReadCalls > 0)
                    throw new IOException("Expected interrupted response body.");
                return base.ReadAsync(buffer, cancellationToken);
            }
        }

        private sealed class GatedStream : ProbeStream
        {
            private readonly TaskCompletionSource<bool> started = signal();
            private readonly TaskCompletionSource<bool> cancelled = signal();
            private readonly TaskCompletionSource<bool> release = signal();
            public Task Started => started.Task;
            public Task CancellationObserved => cancelled.Task;

            public GatedStream(byte[] bytes)
                : base(bytes)
            {
            }

            public void ReleaseRead() => release.TrySetResult(true);

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                using var registration = cancellationToken.Register(() => cancelled.TrySetResult(true));
                started.TrySetResult(true);
                await release.Task.WaitAsync(wait_limit);
                cancellationToken.ThrowIfCancellationRequested();
                return await base.ReadAsync(buffer, cancellationToken);
            }
        }
    }
}
