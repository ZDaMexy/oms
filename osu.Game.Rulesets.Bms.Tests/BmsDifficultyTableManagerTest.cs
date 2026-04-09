// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsDifficultyTableManagerTest
    {
        [Test]
        public void TestBundledPresetsAreSeeded()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-presets-{Guid.NewGuid():N}");
            var manager = new BmsDifficultyTableManager(storage);

            var sources = manager.GetSources();

            Assert.Multiple(() =>
            {
                Assert.That(sources.Count(source => source.IsPreset), Is.EqualTo(7));
                Assert.That(sources.Any(source => source.IsPreset && source.SourceName == "satellite" && source.DisplayName == "Satellite"), Is.True);
                Assert.That(sources.Any(source => source.IsPreset && source.SourceName == "stella" && source.DisplayName == "Stella"), Is.True);
            });
        }

        [Test]
        public async Task TestImportDirectoryViaHtmlWrapperPersistsCachedEntriesAcrossRestart()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-import-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "satellite-local", "Satellite Local",
                new TableEntry("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "1"),
                new TableEntry("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "12"));

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.IsPreset, Is.False);
                Assert.That(imported.Enabled, Is.True);
                Assert.That(imported.DisplayName, Is.EqualTo("Satellite Local"));
                Assert.That(imported.Symbol, Is.EqualTo("★"));
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★1", "★12" }));
            });

            var restartedManager = new BmsDifficultyTableManager(storage);
            var restored = restartedManager.GetSources().Single(source => source.ID == imported.ID);

            Assert.Multiple(() =>
            {
                Assert.That(restored.DisplayName, Is.EqualTo(imported.DisplayName));
                Assert.That(restored.Entries.Select(entry => entry.Md5), Is.EqualTo(imported.Entries.Select(entry => entry.Md5)));
                Assert.That(restored.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(imported.Entries.Select(entry => entry.LevelLabel)));
            });
        }

        [Test]
        public async Task TestImportIntoPresetUpdatesExistingPresetSource()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-preset-import-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "satellite-preset", "Satellite",
                new TableEntry("cccccccccccccccccccccccccccccccc", "3"));

            var manager = new BmsDifficultyTableManager(storage);
            var preset = manager.GetSources().Single(source => source.IsPreset && source.SourceName == "satellite");

            var imported = await manager.ImportFromPath(tableRoot, preset.ID).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.ID, Is.EqualTo(preset.ID));
                Assert.That(imported.IsPreset, Is.True);
                Assert.That(imported.Enabled, Is.True);
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★3" }));
                Assert.That(manager.GetSources().Count(source => !source.IsPreset), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestImportMatchingBundledPresetUsesSeededSource()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-preset-match-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "custom-local-path", "Satellite",
                new TableEntry("ffffffffffffffffffffffffffffffff", "5"));

            var manager = new BmsDifficultyTableManager(storage);
            var preset = manager.GetSources().Single(source => source.IsPreset && source.SourceName == "satellite");

            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.ID, Is.EqualTo(preset.ID));
                Assert.That(imported.IsPreset, Is.True);
                Assert.That(imported.LocalPath, Is.EqualTo(Path.GetFullPath(tableRoot)));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★5" }));
                Assert.That(manager.GetSources().Count(source => !source.IsPreset), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestImportRemoteHeaderUrlPersistsCachedEntriesAcrossRestart()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-remote-header-{Guid.NewGuid():N}");
            using var server = new TestHttpServer();

            string headerPath = "/normal/normal_header.json";
            string bodyPath = "/normal/normal_body.json";
            string headerUrl = server.GetUrl(headerPath);

            server.SetResponse(headerPath,
                $"{{\n  \"data_url\": \"{server.GetUrl(bodyPath)}\",\n  \"last_update\": \"2026/04/09\",\n  \"name\": \"Online Normal\",\n  \"symbol\": \"☆\"\n}}",
                "application/json; charset=utf-8");
            server.SetResponse(bodyPath, createTableBodyJson(
                new TableEntry("11111111111111111111111111111111", "1"),
                new TableEntry("22222222222222222222222222222222", "4")),
                "application/json; charset=utf-8");

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(headerUrl).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(imported.IsPreset, Is.False);
                Assert.That(imported.DisplayName, Is.EqualTo("Online Normal"));
                Assert.That(imported.Symbol, Is.EqualTo("☆"));
                Assert.That(imported.LocalPath, Is.EqualTo(headerUrl));
                Assert.That(imported.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "☆1", "☆4" }));
            });

            var restartedManager = new BmsDifficultyTableManager(storage);
            var restored = restartedManager.GetSources().Single(source => source.ID == imported.ID);

            Assert.Multiple(() =>
            {
                Assert.That(restored.DisplayName, Is.EqualTo(imported.DisplayName));
                Assert.That(restored.LocalPath, Is.EqualTo(headerUrl));
                Assert.That(restored.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "☆1", "☆4" }));
            });
        }

        [Test]
        public async Task TestImportRemoteHtmlWrapperRefreshesRelativeSources()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-remote-html-{Guid.NewGuid():N}");
            using var server = new TestHttpServer();

            string indexPath = "/satellite/index.html";
            string headerPath = "/satellite/header.json";
            string bodyPath = "/satellite/body.json";

            server.SetResponse(indexPath,
                "<html><head><meta name=\"bmstable\" content=\"header.json\"></head><body></body></html>",
                "text/html; charset=utf-8");
            server.SetResponse(headerPath,
                "{\n  \"name\": \"Online Satellite\",\n  \"symbol\": \"★\",\n  \"data_url\": \"body.json\"\n}",
                "application/json; charset=utf-8");
            server.SetResponse(bodyPath, createTableBodyJson(
                new TableEntry("33333333333333333333333333333333", "2")),
                "application/json; charset=utf-8");

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(server.GetUrl(indexPath)).ConfigureAwait(false);

            int eventCount = 0;
            manager.TableDataChanged += () => eventCount++;

            server.SetResponse(bodyPath, createTableBodyJson(
                new TableEntry("33333333333333333333333333333333", "2"),
                new TableEntry("44444444444444444444444444444444", "9")),
                "application/json; charset=utf-8");

            var refreshed = await manager.RefreshTable(imported.ID).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(refreshed.DisplayName, Is.EqualTo("Online Satellite"));
                Assert.That(refreshed.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★2", "★9" }));
                Assert.That(manager.GetEntriesForMd5("44444444444444444444444444444444").Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
            });
        }

        [Test]
        public void TestSharedManagerReturnsSameInstanceForSameStorage()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-shared-{Guid.NewGuid():N}");

            var first = BmsDifficultyTableManager.GetShared(storage);
            var second = BmsDifficultyTableManager.GetShared(storage);

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public async Task TestSharedManagerAsyncReturnsSameInstanceForSameStorage()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-shared-async-{Guid.NewGuid():N}");

            var first = await BmsDifficultyTableManager.GetSharedAsync(storage).ConfigureAwait(false);
            var second = await BmsDifficultyTableManager.GetSharedAsync(storage).ConfigureAwait(false);

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public async Task TestGetSourcesAsyncMatchesSynchronousSnapshot()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-get-sources-async-{Guid.NewGuid():N}");

            var manager = new BmsDifficultyTableManager(storage);
            var syncSources = manager.GetSources();
            var asyncSources = await manager.GetSourcesAsync().ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(asyncSources.Select(source => source.ID), Is.EqualTo(syncSources.Select(source => source.ID)));
                Assert.That(asyncSources.Select(source => source.SourceName), Is.EqualTo(syncSources.Select(source => source.SourceName)));
                Assert.That(asyncSources.Select(source => source.DisplayName), Is.EqualTo(syncSources.Select(source => source.DisplayName)));
            });
        }

        [Test]
        public async Task TestRefreshTableUpdatesEntriesAndEnabledLookupRespectsToggle()
        {
            using var storage = new TemporaryNativeStorage($"bms-table-refresh-{Guid.NewGuid():N}");

            string tableRoot = createTableMirror(storage, "stella-local", "Stella Local",
                new TableEntry("dddddddddddddddddddddddddddddddd", "2"));

            var manager = new BmsDifficultyTableManager(storage);
            var imported = await manager.ImportFromPath(tableRoot).ConfigureAwait(false);

            int eventCount = 0;
            manager.TableDataChanged += () => eventCount++;

            File.WriteAllText(Path.Combine(tableRoot, "score.json"),
                """
                [
                  { "md5": "dddddddddddddddddddddddddddddddd", "level": "2" },
                  { "md5": "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "level": "9" }
                ]
                """);

            var refreshed = await manager.RefreshTable(imported.ID).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(refreshed.Entries.Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★2", "★9" }));
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee").Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
            });

            manager.SetSourceEnabled(imported.ID, false);

            Assert.Multiple(() =>
            {
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"), Is.Empty);
                Assert.That(manager.GetEntriesForMd5("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", onlyEnabled: false).Select(entry => entry.LevelLabel), Is.EqualTo(new[] { "★9" }));
                Assert.That(eventCount, Is.EqualTo(2));
            });
        }

        private static string createTableMirror(Storage storage, string directoryName, string displayName, params TableEntry[] entries)
        {
            string tableRoot = Path.Combine(storage.GetFullPath("."), directoryName, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tableRoot);

            File.WriteAllText(Path.Combine(tableRoot, "index.html"), "<html><head><meta name=\"bmstable\" content=\"header.json\"></head><body></body></html>");
            File.WriteAllText(Path.Combine(tableRoot, "header.json"),
                $"{{\"name\":\"{displayName}\",\"symbol\":\"★\",\"data_url\":\"score.json\"}}");
            File.WriteAllText(Path.Combine(tableRoot, "score.json"), createTableBodyJson(entries));

            return tableRoot;
        }

        private static string createTableBodyJson(params TableEntry[] entries)
            => "[" + string.Join(",", entries.Select(entry => $"{{\"md5\":\"{entry.Md5}\",\"level\":\"{entry.Level}\"}}")) + "]";

        private sealed class TestHttpServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private readonly Dictionary<string, TestHttpResponse> responses = new Dictionary<string, TestHttpResponse>(StringComparer.Ordinal);
            private readonly Task serverTask;

            public string BaseUrl { get; }

            public TestHttpServer()
            {
                listener.Start();
                BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
                serverTask = Task.Run(serveAsync);
            }

            public string GetUrl(string path)
                => new Uri(new Uri(BaseUrl), path.TrimStart('/')).AbsoluteUri;

            public void SetResponse(string path, string body, string contentType)
                => responses[path] = new TestHttpResponse(HttpStatusCode.OK, contentType, body);

            private async Task serveAsync()
            {
                try
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                        _ = Task.Run(() => handleClientAsync(client), cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            private async Task handleClientAsync(TcpClient client)
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
                {
                    string? requestLine = await reader.ReadLineAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(requestLine))
                        return;

                    string[] parts = requestLine.Split(' ');
                    string requestPath = parts.Length > 1 ? parts[1] : "/";

                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false)))
                    {
                    }

                    TestHttpResponse response = responses.GetValueOrDefault(requestPath)
                                                ?? new TestHttpResponse(HttpStatusCode.NotFound, "text/plain; charset=utf-8", "Not found");

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(response.Body);
                    string headers = $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\nContent-Type: {response.ContentType}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers)).ConfigureAwait(false);
                    await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
                }
            }

            public void Dispose()
            {
                cancellationTokenSource.Cancel();
                listener.Stop();

                try
                {
                    serverTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
            }

            private sealed record TestHttpResponse(HttpStatusCode StatusCode, string ContentType, string Body);
        }

        private readonly record struct TableEntry(string Md5, string Level);
    }
}
