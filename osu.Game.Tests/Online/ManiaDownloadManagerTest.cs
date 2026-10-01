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
using osu.Game.Online.Sayobot;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class ManiaDownloadManagerTest
    {
        private const int set_id = 123;
        private const int beatmap_id_a = 201;
        private const int beatmap_id_b = 202;
        private const string md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static readonly Guid local_beatmap_id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly TimeSpan wait_limit = TimeSpan.FromSeconds(10);
        private static readonly byte[] archive_bytes = Encoding.UTF8.GetBytes("archive bytes for the mania importer");

        private TemporaryNativeStorage storage = null!;
        private string playerFile = null!;
        private string retainedArchive = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"mania-download-manager-{Guid.NewGuid():N}");
            playerFile = storage.GetFullPath("chartmania/player-owned/song.osu");
            retainedArchive = storage.GetFullPath("mania-downloads/preexisting-recovery/archive.osz");
            Directory.CreateDirectory(Path.GetDirectoryName(playerFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(retainedArchive)!);
            File.WriteAllText(playerFile, "prior player data");
            File.WriteAllText(retainedArchive, "prior recovery archive");
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public async Task TestSingleStreamReachesOwnedOszStagingWithOriginalSetAndSelectedDifficulty()
        {
            var stream = new ProbeStream(archive_bytes, chunkSize: 2);
            var handler = new StubHandler((_, _) => Task.FromResult(responseFor(stream)));
            string? importedPath = null;
            var importer = new StubImporter((path, sid, bid, token) =>
            {
                importedPath = path;
                string relative = Path.GetRelativePath(storage.GetFullPath("mania-downloads"), path);
                Assert.That(relative, Does.Not.StartWith(".."));
                Assert.That(Path.IsPathRooted(relative), Is.False);
                Assert.That(Path.GetFileName(path), Is.EqualTo("package.osz"));
                Assert.That(Guid.TryParseExact(Path.GetFileName(Path.GetDirectoryName(path)), "N", out _), Is.True);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(archive_bytes));
                Assert.That(sid, Is.EqualTo(set_id));
                Assert.That(bid, Is.EqualTo(beatmap_id_b));
                Assert.That(stream.Released, Is.True);
                return importedResult(bid);
            });
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            var seen = new ConcurrentQueue<ManiaDownloadState>();
            manager.TaskChanged += task => seen.Enqueue(task.Progress.State);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Set.Id, Is.EqualTo(set_id));
            Assert.That(task.RequestedBeatmapId, Is.EqualTo(beatmap_id_b));
            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
            Assert.That(task.Progress.Bytes, Is.EqualTo(archive_bytes.Length));
            Assert.That(task.Progress.TotalBytes, Is.EqualTo(archive_bytes.Length));
            Assert.That(task.Progress.Imported, Is.EqualTo(new[] { new ManiaDownloadImportedBeatmap(local_beatmap_id, beatmap_id_b, md5) }));
            Assert.That(manager.GetTask(task.Set.Key), Is.SameAs(task));
            Assert.That(importer.Calls, Is.EqualTo(1));
            Assert.That(stream.ReadCalls, Is.GreaterThan(1));
            Assert.That(seen, Does.Contain(ManiaDownloadState.Downloading));
            Assert.That(seen, Does.Contain(ManiaDownloadState.Importing));
            Assert.That(seen, Does.Contain(ManiaDownloadState.Completed));
            Assert.That(File.Exists(importedPath), Is.False);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestQueuedSetDeduplicatesEvenForAnotherDifficultyAndCanCancelThenRetry()
        {
            var firstImportStarted = signal();
            var releaseFirstImport = signal();
            var paths = new ConcurrentQueue<string>();
            var importer = new StubImporter(async (path, _, bid, token) =>
            {
                paths.Enqueue(path);
                if (paths.Count == 1)
                {
                    firstImportStarted.TrySetResult(true);
                    await releaseFirstImport.Task.WaitAsync(wait_limit);
                }

                token.ThrowIfCancellationRequested();
                return await importedResult(bid);
            });
            var handler = new StubHandler((_, _) => Task.FromResult(responseFor(new ProbeStream(archive_bytes))));
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            try
            {
                ManiaDownloadTask first = manager.Download(createSet(id: 124), beatmap_id_b);
                await firstImportStarted.Task.WaitAsync(wait_limit);
                SayobotBeatmapSet queuedSet = createSet();
                ManiaDownloadTask queued = manager.Download(queuedSet, beatmap_id_b);

                Assert.That(queued.Progress.State, Is.EqualTo(ManiaDownloadState.Queued));
                Assert.That(manager.Download(queuedSet, beatmap_id_a), Is.SameAs(queued));
                Assert.That(queued.RequestedBeatmapId, Is.EqualTo(beatmap_id_b));
                queued.Cancel();
                await queued.Completion.WaitAsync(wait_limit);
                Assert.That(queued.Progress.State, Is.EqualTo(ManiaDownloadState.Cancelled));
                Assert.That(handler.Requests, Is.EqualTo(1));

                ManiaDownloadTask retry = manager.Download(queuedSet, beatmap_id_a);
                Assert.That(retry, Is.Not.SameAs(queued));
                Assert.That(retry.Progress.State, Is.EqualTo(ManiaDownloadState.Queued));
                queued.Cancel();
                releaseFirstImport.TrySetResult(true);
                await Task.WhenAll(first.Completion, retry.Completion).WaitAsync(wait_limit);

                Assert.That(retry.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
                Assert.That(retry.RequestedBeatmapId, Is.EqualTo(beatmap_id_a));
                Assert.That(manager.GetTask(queuedSet.Key), Is.SameAs(retry));
                Assert.That(handler.Requests, Is.EqualTo(2));
                Assert.That(paths.Select(Path.GetDirectoryName).Distinct().Count(), Is.EqualTo(2));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseFirstImport.TrySetResult(true);
            }
        }

        [Test]
        public async Task TestClosingBrowserObserverKeepsDownloadRunning()
        {
            var importStarted = signal();
            var releaseImport = signal();
            var importer = new StubImporter(async (_, _, bid, token) =>
            {
                importStarted.TrySetResult(true);
                await releaseImport.Task.WaitAsync(wait_limit);
                token.ThrowIfCancellationRequested();
                return await importedResult(bid);
            });
            using var http = successfulHttpClient();
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            int observations = 0;
            void observe(ManiaDownloadTask _) => Interlocked.Increment(ref observations);
            manager.TaskChanged += observe;

            try
            {
                ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
                await importStarted.Task.WaitAsync(wait_limit);
                manager.TaskChanged -= observe;
                int atClose = Volatile.Read(ref observations);
                releaseImport.TrySetResult(true);
                await task.Completion.WaitAsync(wait_limit);

                Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
                Assert.That(Volatile.Read(ref observations), Is.EqualTo(atClose));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseImport.TrySetResult(true);
                manager.TaskChanged -= observe;
            }
        }

        [TestCase("https://dl.sayobot.cn/beatmaps/download/full/123")]
        [TestCase("https://txy1.sayobot.cn/beatmaps/download/full/123")]
        [TestCase("https://dl.sayobot.cn:443/beatmaps/download/full/123")]
        public async Task TestApprovedEntryPointsHaveRequiredSoftwareIdentity(string url)
        {
            var handler = new StubHandler((request, _) =>
            {
                Assert.That(request.RequestUri, Is.EqualTo(new Uri(url)));
                assertSoftwareHeaders(request);
                return Task.FromResult(responseFor(new ProbeStream(archive_bytes)));
            });
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(url: url), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(importer.Calls, Is.EqualTo(1));
            assertStoragePreservedAndClean();
        }

        [TestCase("http://dl.sayobot.cn/archive.osz")]
        [TestCase("https://dl.sayobot.cn:8443/archive.osz")]
        [TestCase("https://txy1.sayobot.cn:25225/archive.osz")]
        [TestCase("https://user:password@dl.sayobot.cn/archive.osz")]
        [TestCase("https://dl.sayobot.cn.example.org/archive.osz")]
        [TestCase("https://tc1.sayobot.cn:25225/archive.osz")]
        [TestCase("https://tc2.sayobot.cn:25225/archive.osz")]
        [TestCase("https://tc1.sayobot.cn/archive.osz")]
        [TestCase("https://example.org/archive.osz")]
        [TestCase("https://localhost/archive.osz")]
        [TestCase("https://127.0.0.1/archive.osz")]
        public async Task TestUnapprovedInitialAddressNeverReachesNetwork(string url)
        {
            var handler = new StubHandler((_, _) => throw new InvalidOperationException("An unauthorised URI must not reach HTTP."));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(url: url), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(handler.Requests, Is.Zero);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase("tc1.sayobot.cn")]
        [TestCase("tc2.sayobot.cn")]
        public async Task TestActualMirrorPortsAndRelativeRedirectKeepHeadersForEveryHop(string host)
        {
            var requests = new ConcurrentQueue<Uri>();
            var handler = new StubHandler((request, _) =>
            {
                requests.Enqueue(request.RequestUri!);
                assertSoftwareHeaders(request);
                return Task.FromResult(requests.Count switch
                {
                    1 => redirectTo($"https://{host}:25225/123.osz"),
                    2 => redirectTo("/final/123.osz"),
                    _ => responseFor(new ProbeStream(archive_bytes)),
                });
            });
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, successfulImporter(), downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
            Assert.That(requests, Is.EqualTo(new[]
            {
                new Uri("https://dl.sayobot.cn/beatmaps/download/full/123"),
                new Uri($"https://{host}:25225/123.osz"),
                new Uri($"https://{host}:25225/final/123.osz"),
            }));
            assertStoragePreservedAndClean();
        }

        [TestCase("http://tc1.sayobot.cn:25225/archive.osz")]
        [TestCase("https://tc1.sayobot.cn/archive.osz")]
        [TestCase("https://tc1.sayobot.cn:8443/archive.osz")]
        [TestCase("https://dl.sayobot.cn:25225/archive.osz")]
        [TestCase("https://txy1.sayobot.cn:25225/archive.osz")]
        [TestCase("https://user:password@tc1.sayobot.cn:25225/archive.osz")]
        [TestCase("https://tc1.sayobot.cn.example.org:25225/archive.osz")]
        [TestCase("https://127.0.0.1:25225/archive.osz")]
        [TestCase("https://tc2.sayobot.cn/archive.osz")]
        [TestCase("https://tc2.sayobot.cn:8443/archive.osz")]
        [TestCase("https://tc2.sayobot.cn.example.org:25225/archive.osz")]
        [TestCase("https://tc3.sayobot.cn:25225/archive.osz")]
        public async Task TestRedirectCannotEscapeExactHostAndPort(string redirectUrl)
        {
            var handler = new StubHandler((_, _) => Task.FromResult(redirectTo(redirectUrl)));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(301)]
        [TestCase(302)]
        [TestCase(303)]
        [TestCase(307)]
        [TestCase(308)]
        public async Task TestApprovedRedirectStatusCanReachSecondaryEntry(int redirectStatus)
        {
            int requests = 0;
            var handler = new StubHandler((request, _) =>
            {
                assertSoftwareHeaders(request);
                return Task.FromResult(Interlocked.Increment(ref requests) == 1
                    ? new HttpResponseMessage((HttpStatusCode)redirectStatus) { Headers = { Location = new Uri("https://txy1.sayobot.cn/archive.osz") } }
                    : responseFor(new ProbeStream(archive_bytes)));
            });
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, successfulImporter(), downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
            Assert.That(handler.Requests, Is.EqualTo(2));
            assertStoragePreservedAndClean();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestFiveRedirectsCanCompleteButTheNextRedirectFails(bool exceedLimit)
        {
            int requests = 0;
            var handler = new StubHandler((_, _) => Task.FromResult(Interlocked.Increment(ref requests) <= 5 || exceedLimit
                ? redirectTo("/again.osz")
                : responseFor(new ProbeStream(archive_bytes))));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(exceedLimit ? ManiaDownloadState.Failed : ManiaDownloadState.Completed));
            Assert.That(handler.Requests, Is.EqualTo(6));
            Assert.That(importer.Calls, Is.EqualTo(exceedLimit ? 0 : 1));
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestRedirectWithoutDestinationFails()
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(0L)]
        [TestCase(ManiaDownloadManager.MaximumPackageBytes + 1)]
        public async Task TestInvalidDeclaredSizeFailsBeforeReading(long length)
        {
            var stream = new ProbeStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream, length))));
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
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
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(stream.Released, Is.True);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [Test]
        [Explicit("Writes the actual 2 GiB package boundary into isolated temporary storage.")]
        public async Task TestActualBodyBudgetIsEnforcedWithoutAContentLength()
        {
            var stream = new BudgetStream(ManiaDownloadManager.MaximumPackageBytes + 1);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream))));
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(TimeSpan.FromMinutes(5));

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(stream.Released, Is.True);
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestNetworkFailureBeforeOrDuringStreamFailsAndCleansOwnedStaging(bool duringRead)
        {
            var stream = new InterruptedStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => duringRead
                ? Task.FromResult(responseFor(stream))
                : Task.FromException<HttpResponseMessage>(new HttpRequestException("Expected connection failure."))));
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(importer.Calls, Is.Zero);
            if (duringRead)
                Assert.That(stream.Released, Is.True);
            assertStoragePreservedAndClean();
        }

        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        public async Task TestHttpFailureDoesNotRetryOrImport(HttpStatusCode status)
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
            var importer = successfulImporter();
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestRequestTimeoutIsFailureRatherThanPlayerCancellation()
        {
            var importer = successfulImporter();
            var handler = new StubHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("The timed request must not finish successfully.");
            });
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
            await task.Completion.WaitAsync(wait_limit);

            Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
            Assert.That(importer.Calls, Is.Zero);
            assertStoragePreservedAndClean();
        }

        [Test]
        public async Task TestImporterFailureCanRetryFromTerminalEventAfterPriorOwnerIsCleaned()
        {
            var firstImportStarted = signal();
            var releaseFailure = signal();
            var retryImportStarted = signal();
            var releaseRetry = signal();
            var retryCreated = signal();
            int imports = 0;
            string? firstPath = null;
            string? retryPath = null;
            ManiaDownloadTask? retry = null;
            var importer = new StubImporter(async (path, _, bid, token) =>
            {
                if (Interlocked.Increment(ref imports) == 1)
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
                return await importedResult(bid);
            });
            using var http = successfulHttpClient();
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            SayobotBeatmapSet set = createSet();
            bool cleanedBeforeTerminalEvent = false;

            try
            {
                ManiaDownloadTask first = manager.Download(set, beatmap_id_b);
                await firstImportStarted.Task.WaitAsync(wait_limit);
                manager.TaskChanged += changed =>
                {
                    if (!ReferenceEquals(changed, first) || changed.Progress.State != ManiaDownloadState.Failed)
                        return;

                    cleanedBeforeTerminalEvent = !Directory.Exists(Path.GetDirectoryName(firstPath!)!);
                    changed.Cancel();
                    retry = manager.Download(set, beatmap_id_b);
                    retryCreated.TrySetResult(true);
                };
                releaseFailure.TrySetResult(true);
                await retryCreated.Task.WaitAsync(wait_limit);
                await retryImportStarted.Task.WaitAsync(wait_limit);

                Assert.That(cleanedBeforeTerminalEvent, Is.True);
                Assert.That(first.Progress.State, Is.EqualTo(ManiaDownloadState.Failed));
                Assert.That(retry, Is.Not.SameAs(first));
                Assert.That(manager.GetTask(set.Key), Is.SameAs(retry));
                Assert.That(Path.GetDirectoryName(firstPath), Is.Not.EqualTo(Path.GetDirectoryName(retryPath)));
                releaseRetry.TrySetResult(true);
                await Task.WhenAll(first.Completion, retry!.Completion).WaitAsync(wait_limit);
                Assert.That(retry.Progress.State, Is.EqualTo(ManiaDownloadState.Completed));
                assertStoragePreservedAndClean();
            }
            finally
            {
                releaseFailure.TrySetResult(true);
                releaseRetry.TrySetResult(true);
            }
        }

        [Test]
        public async Task TestCancelDuringStreamingWithdrawsBeforePublishingCancelled()
        {
            var stream = new GatedStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream))));
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);

            try
            {
                ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
                await stream.Started.WaitAsync(wait_limit);
                Assert.That(manager.Download(createSet(), beatmap_id_a), Is.SameAs(task));
                task.Cancel();
                await stream.CancellationObserved.WaitAsync(wait_limit);

                Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Downloading));
                Assert.That(task.Completion.IsCompleted, Is.False);
                stream.ReleaseRead();
                await task.Completion.WaitAsync(wait_limit);

                Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Cancelled));
                Assert.That(stream.Released, Is.True);
                Assert.That(importer.Calls, Is.Zero);
                assertStoragePreservedAndClean();
            }
            finally
            {
                stream.ReleaseRead();
            }
        }

        [Test]
        public async Task TestDisposeWaitsForCancelledImporterBeforeRealmCanClose()
        {
            var importStarted = signal();
            var cancellationObserved = signal();
            var releaseImport = signal();
            string? activeArchive = null;
            var importer = new StubImporter(async (path, _, bid, token) =>
            {
                activeArchive = path;
                using var registration = token.Register(() => cancellationObserved.TrySetResult(true));
                importStarted.TrySetResult(true);
                await releaseImport.Task.WaitAsync(wait_limit);
                token.ThrowIfCancellationRequested();
                return await importedResult(bid);
            });
            using var http = successfulHttpClient();
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            Task? disposal = null;

            try
            {
                ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
                await importStarted.Task.WaitAsync(wait_limit);
                ManiaDownloadTask queued = manager.Download(createSet(id: 124), beatmap_id_a);
                disposal = Task.Run(manager.Dispose);
                await cancellationObserved.Task.WaitAsync(wait_limit);

                Assert.That(disposal.IsCompleted, Is.False);
                Assert.That(File.Exists(activeArchive), Is.True);
                releaseImport.TrySetResult(true);
                await disposal.WaitAsync(wait_limit);
                Assert.That(task.Completion.IsCompleted, Is.True);
                Assert.That(queued.Completion.IsCompleted, Is.True);
                Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Cancelled));
                Assert.That(queued.Progress.State, Is.EqualTo(ManiaDownloadState.Cancelled));
                Assert.Throws<ObjectDisposedException>(() => manager.Download(createSet(id: 125), beatmap_id_b));
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
        public async Task TestDisposeWaitsForCancelledStreamToRelease()
        {
            var stream = new GatedStream(archive_bytes);
            var importer = successfulImporter();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(stream))));
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            Task? disposal = null;

            try
            {
                ManiaDownloadTask task = manager.Download(createSet(), beatmap_id_b);
                await stream.Started.WaitAsync(wait_limit);
                disposal = Task.Run(manager.Dispose);
                await stream.CancellationObserved.WaitAsync(wait_limit);

                Assert.That(disposal.IsCompleted, Is.False);
                Assert.That(stream.Released, Is.False);
                stream.ReleaseRead();
                await disposal.WaitAsync(wait_limit);
                Assert.That(stream.Released, Is.True);
                Assert.That(task.Completion.IsCompleted, Is.True);
                Assert.That(task.Progress.State, Is.EqualTo(ManiaDownloadState.Cancelled));
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
        public void TestWrongSelectedDifficultyDoesNotCreateTaskOrRequest()
        {
            var importer = successfulImporter();
            var handler = new StubHandler((_, _) => throw new InvalidOperationException("A rejected selection must not create requests."));
            using var http = new HttpClient(handler);
            using var manager = new ManiaDownloadManager(storage, importer, downloads: http);
            SayobotBeatmapSet set = createSet();

            Assert.Throws<ArgumentException>(() => manager.Download(set, 999));
            Assert.That(manager.GetTask(set.Key), Is.Null);
            Assert.That(importer.Calls, Is.Zero);
            Assert.That(handler.Requests, Is.Zero);
            assertStoragePreservedAndClean();
        }

        private void assertStoragePreservedAndClean()
        {
            Assert.That(File.ReadAllText(playerFile), Is.EqualTo("prior player data"));
            Assert.That(File.ReadAllText(retainedArchive), Is.EqualTo("prior recovery archive"));
            Assert.That(Directory.GetDirectories(storage.GetFullPath("mania-downloads")).Select(Path.GetFileName), Is.EqualTo(new[] { "preexisting-recovery" }));
        }

        private static void assertSoftwareHeaders(HttpRequestMessage request)
        {
            Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo("OMS/1.0 (+https://github.com/ZDaMexy/oms)"));
            Assert.That(request.Headers.Referrer, Is.EqualTo(new Uri("https://github.com/ZDaMexy/oms/")));
            Assert.That(request.Headers.Authorization, Is.Null);
        }

        private static SayobotBeatmapSet createSet(int id = set_id, string url = "https://dl.sayobot.cn/beatmaps/download/full/123")
            => new SayobotBeatmapSet(id, "Song title", "Artist", "Creator", 1, new[]
            {
                new SayobotBeatmap(beatmap_id_a, "Normal", 4, 2.5),
                new SayobotBeatmap(beatmap_id_b, "Hyper", 7, 4.5),
            }, new Uri(url), new Uri($"https://cdn.sayobot.cn:25225/beatmaps/{id}/cover.jpg"));

        private static TaskCompletionSource<bool> signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static Task<IReadOnlyList<ManiaDownloadImportedBeatmap>> importedResult(int onlineId)
            => Task.FromResult<IReadOnlyList<ManiaDownloadImportedBeatmap>>(new[] { new ManiaDownloadImportedBeatmap(local_beatmap_id, onlineId, md5) });

        private static StubImporter successfulImporter() => new StubImporter((_, _, bid, _) => importedResult(bid));

        private static HttpClient successfulHttpClient() => new HttpClient(new StubHandler((_, _) => Task.FromResult(responseFor(new ProbeStream(archive_bytes)))));

        private static HttpResponseMessage responseFor(Stream stream, long? length = null)
        {
            var content = new StreamContent(stream);
            if (length.HasValue)
                content.Headers.ContentLength = length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static HttpResponseMessage redirectTo(string url) => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri(url, UriKind.RelativeOrAbsolute) },
        };

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

        private sealed class StubImporter : IManiaDownloadImporter
        {
            private readonly Func<string, int, int, CancellationToken, Task<IReadOnlyList<ManiaDownloadImportedBeatmap>>> import;
            private int calls;
            public int Calls => Volatile.Read(ref calls);

            public StubImporter(Func<string, int, int, CancellationToken, Task<IReadOnlyList<ManiaDownloadImportedBeatmap>>> import) => this.import = import;

            public Task<IReadOnlyList<ManiaDownloadImportedBeatmap>> ImportAsync(string archivePath, int expectedSetId, int requestedBeatmapId, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref calls);
                return import(archivePath, expectedSetId, requestedBeatmapId, cancellationToken);
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

        private sealed class BudgetStream : Stream
        {
            private long remaining;
            public bool Released { get; private set; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public BudgetStream(long length) => remaining = length;

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = (int)Math.Min(buffer.Length, remaining);
                buffer.Span[..read].Clear();
                remaining -= read;
                return ValueTask.FromResult(read);
            }

            protected override void Dispose(bool disposing)
            {
                Released = true;
                base.Dispose(disposing);
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
