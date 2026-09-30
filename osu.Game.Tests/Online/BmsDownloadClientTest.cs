// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online.Bms;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BmsDownloadClientTest
    {
        private const string md5_a = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string md5_b = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string md5_c = "cccccccccccccccccccccccccccccccc";
        private const string song_url = "https://bms.alvorna.com/bms/zipped/%2300FFFF%20%25%3F.7z";

        private const string ginger_package_json = """
                                                  {"id":17,"fileName":"Music.7z","fileSize":100,
                                                   "shardMD5":"not-a-chart-or-archive-checksum",
                                                   "downloadURL":"https://pixeldrain.net/api/filesystem/abc/Music.7z",
                                                   "bannerURL":"https://pixeldrain.net/banner.png","stageFileURL":"https://pixeldrain.net/stage.png",
                                                   "songs":[{"md5":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","fileName":"normal.bms","title":"Track","artist":"Author","subTitle":"[NORMAL]","mode":"BEAT_7K","playLevel":"12"}]}
                                                  """;

        private const string ginger_empty_search_json = "{\"data\":[],\"page\":1,\"pageCount\":0,\"pageSize\":20,\"total\":0}";
        private const string konmai_empty_search_json = "{\"result\":\"success\",\"data\":[],\"page\":1,\"total_pages\":0,\"page_size\":20,\"total\":0}";

        [Test]
        public async Task TestGingerSearchSendsPageKeywordAndTableAndReadsRealMetadata()
        {
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://gingerrush.com/api/v1/files/selectList"));
                Assert.That(request.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
                using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token));
                JsonElement root = body.RootElement;
                Assert.That(root.GetProperty("pageRequest").GetProperty("page").GetInt32(), Is.EqualTo(2));
                Assert.That(root.GetProperty("pageRequest").GetProperty("pageSize").GetInt32(), Is.EqualTo(20));
                Assert.That(root.GetProperty("fuzzyKeyword").GetString(), Is.EqualTo("月光 & #%?"));
                Assert.That(root.GetProperty("md5").GetString(), Is.Empty);
                Assert.That(root.GetProperty("tableID").GetInt32(), Is.EqualTo(7));
                return jsonResponse(gingerSearch(ginger_package_json, page: 2, total: 21));
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchAsync(BmsDownloadSource.Ginger, " 月光 & #%? ", 2, "7", CancellationToken.None);

            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(2));
            Assert.That(result.Total, Is.EqualTo(21));
            BmsDownloadPackage package = result.Packages.Single();
            Assert.That(package.Key, Is.EqualTo("Ginger:17"));
            Assert.That(package.Name, Is.EqualTo("Music.7z"));
            Assert.That(package.CanDownload, Is.True);
            Assert.That(package.DownloadUrl!.OriginalString, Is.EqualTo("https://pixeldrain.net/api/filesystem/abc/Music.7z"));
            Assert.That(package.Size, Is.EqualTo(100));
            Assert.That(package.CoverUrl!.OriginalString, Is.EqualTo("https://pixeldrain.net/stage.png"));
            Assert.That(package.Charts.Single(), Is.EqualTo(new BmsDownloadChart(md5_a, "Track", "Author", "[NORMAL]", 7, "12", FileName: "normal.bms")));
        }

        [Test]
        public async Task TestGingerPackageRowsAndRepeatedHashesAreMerged()
        {
            string otherChart = ginger_package_json.Replace(md5_a, md5_b, StringComparison.Ordinal);
            using var http = httpClientFor(gingerSearch(ginger_package_json + "," + otherChart + "," + ginger_package_json, total: 3));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchAsync(BmsDownloadSource.Ginger, "Track", 1, null, CancellationToken.None);

            Assert.That(result.Packages, Has.Count.EqualTo(1));
            Assert.That(result.Packages.Single().Charts.Select(chart => chart.Md5), Is.EqualTo(new[] { md5_a, md5_b }));
            Assert.That(result.Total, Is.EqualTo(3));
        }

        [Test]
        public async Task TestKonmaiEncodesSearchAndOriginalTableUrlAndGroupsByPackageUrl()
        {
            const string query = "月光 & #%?";
            const string table = "https://table.example/list?name=★&x=1#anchor";
            string chartA = konmaiChart(md5_a, song_url);
            string chartB = konmaiChart(md5_b, song_url, chartName: "Track [HYPER]");
            string separatePackage = konmaiChart(md5_c, "https://bms.alvorna.com/bms/zipped/other.7z");
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri!.OriginalString,
                    Is.EqualTo("https://bms.alvorna.com/api/search?name=" + Uri.EscapeDataString(query) + "&page=2&page_size=20&table=" + Uri.EscapeDataString(table)));
                return Task.FromResult(jsonResponse(konmaiSearch(chartA + "," + chartB + "," + chartA + "," + separatePackage, page: 2, total: 41)));
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchAsync(BmsDownloadSource.Konmai, query, 2, table, CancellationToken.None);

            Assert.That(result.Packages, Has.Count.EqualTo(2));
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Total, Is.EqualTo(41));
            Assert.That(result.Packages[0].Id, Is.EqualTo(song_url));
            Assert.That(result.Packages[0].DownloadUrl!.OriginalString, Is.EqualTo(song_url));
            Assert.That(result.Packages[0].Charts.Select(chart => chart.Md5), Is.EqualTo(new[] { md5_a, md5_b }));
            Assert.That(result.Packages[0].Charts[0].PreviewUrl!.OriginalString, Is.EqualTo("https://bms.alvorna.com/bms/score/view?md5=" + md5_a));
            Assert.That(result.Packages[0].Name, Is.EqualTo(result.Packages[1].Name));
            Assert.That(result.Packages[0].Key, Is.Not.EqualTo(result.Packages[1].Key));
            Assert.That(result.Packages[0].Size, Is.Null);
            Assert.That(result.Packages[0].Charts[0].KeyCount, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task TestKonmaiRetainsChartsWithoutPackagesAndNullableMetadata(string? downloadUrl)
        {
            string chartA = konmaiChart(md5_a, downloadUrl, title: null, artist: null, songName: null, previewUrl: null);
            string chartB = konmaiChart(md5_b, downloadUrl, title: null, artist: null, songName: null, previewUrl: null);
            using var http = httpClientFor(konmaiSearch(chartA + "," + chartB, total: 2));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchAsync(BmsDownloadSource.Konmai, "Track", 1, null, CancellationToken.None);

            Assert.That(result.Packages, Has.Count.EqualTo(2));
            Assert.That(result.Packages.Select(package => package.Id), Is.EqualTo(new[] { "chart:" + md5_a, "chart:" + md5_b }));

            foreach (BmsDownloadPackage package in result.Packages)
            {
                Assert.That(package.CanDownload, Is.False);
                Assert.That(package.DownloadUrl, Is.Null);
                Assert.That(package.Name, Is.EqualTo("Track [NORMAL]"));
                Assert.That(package.Charts.Single().Artist, Is.Empty);
                Assert.That(package.Charts.Single().PreviewUrl, Is.Null);
            }
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestEmptyResultsAndNoTableFilter(BmsDownloadSource source)
        {
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (source == BmsDownloadSource.Ginger)
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                    Assert.That(body.RootElement.GetProperty("tableID").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    return jsonResponse(ginger_empty_search_json);
                }

                Assert.That(request.RequestUri!.Query, Is.EqualTo("?name=&page=1&page_size=20"));
                return jsonResponse(konmai_empty_search_json);
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchAsync(source, "", 1, null, CancellationToken.None);

            Assert.That(result.Packages, Is.Empty);
            Assert.That(result.Total, Is.Zero);
            Assert.That(result.TotalPages, Is.Zero);
        }

        [Test]
        public async Task TestGingerTableRequestAndMissingOriginalUrl()
        {
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://gingerrush.com/api/v1/table/selectHeaderListWithFullInfo"));
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Assert.That(body.RootElement.GetProperty("type").GetString(), Is.EqualTo("TABLE"));
                return jsonResponse("[{\"id\":1,\"name\":\"Satellite\",\"originalURL\":\"\",\"headerURL\":\"http://zris.work/header.json\"},{\"id\":2,\"name\":\"Normal\",\"originalURL\":\"http://table.example/original\"}]");
            }));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTablesAsync(BmsDownloadSource.Ginger, CancellationToken.None);

            Assert.That(result, Is.EqualTo(new[]
            {
                new BmsDownloadTable("1", "Satellite", ""),
                new BmsDownloadTable("2", "Normal", "http://table.example/original")
            }));
        }

        [Test]
        public async Task TestKonmaiTablesUseOriginalUrlsEvenWhenNamesMatch()
        {
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://bms.alvorna.com/api/tables"));
                return Task.FromResult(jsonResponse("{\"result\":\"success\",\"tables\":[{\"diff_table_name\":\"Normal\",\"diff_table_url\":\"https://table.example/a\",\"diff_table_local_url\":\"/proxied/a\"},{\"diff_table_name\":\"Normal\",\"diff_table_url\":\"https://table.example/b\"}]}"));
            }));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTablesAsync(BmsDownloadSource.Konmai, CancellationToken.None);

            Assert.That(result.Select(table => table.Id), Is.EqualTo(new[] { "https://table.example/a", "https://table.example/b" }));
            Assert.That(result.Select(table => table.OriginalUrl), Is.EqualTo(result.Select(table => table.Id)));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestResolveNormalisesHashAndUsesExactSourceRoute(BmsDownloadSource source)
        {
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo(source == BmsDownloadSource.Ginger
                    ? "https://gingerrush.com/api/v1/files/package/" + md5_a
                    : "https://bms.alvorna.com/api/hash?md5=" + md5_a));
                return Task.FromResult(jsonResponse(source == BmsDownloadSource.Ginger
                    ? ginger_package_json
                    : "{\"result\":\"success\",\"data\":" + konmaiChart(md5_a, song_url, previewUrl: null) + "}"));
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadPackage? result = await client.ResolveAsync(source, " " + md5_a.ToUpperInvariant() + " ", CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Charts.Single().Md5, Is.EqualTo(md5_a));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestResolveNotFoundReturnsNull(BmsDownloadSource source)
        {
            using var http = httpClientFor("this 404 body need not be JSON", HttpStatusCode.NotFound);
            using var client = new BmsDownloadClient(http);
            Assert.That(await client.ResolveAsync(source, md5_a, CancellationToken.None), Is.Null);
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestResolveRejectsAResponseForADifferentChart(BmsDownloadSource source)
        {
            using var http = httpClientFor(source == BmsDownloadSource.Ginger
                ? ginger_package_json
                : "{\"result\":\"success\",\"data\":" + konmaiChart(md5_a, song_url) + "}");
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.ResolveAsync(source, md5_b, CancellationToken.None));
        }

        [TestCase(BmsDownloadSource.Ginger, HttpStatusCode.NotFound)]
        [TestCase(BmsDownloadSource.Konmai, HttpStatusCode.NotFound)]
        [TestCase(BmsDownloadSource.Ginger, HttpStatusCode.TooManyRequests)]
        [TestCase(BmsDownloadSource.Konmai, HttpStatusCode.InternalServerError)]
        public void TestSearchHttpFailuresRemainFailures(BmsDownloadSource source, HttpStatusCode status)
        {
            using var http = httpClientFor("{}", status);
            using var client = new BmsDownloadClient(http);
            HttpRequestException? exception = Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(source, "", 1, null, CancellationToken.None));
            Assert.That(exception!.StatusCode, Is.EqualTo(status));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestResolveHttpServerFailureIsNotAnUnrecordedChart(BmsDownloadSource source)
        {
            using var http = httpClientFor("{}", HttpStatusCode.ServiceUnavailable);
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<HttpRequestException>(() => client.ResolveAsync(source, md5_a, CancellationToken.None));
        }

        [TestCase("id", "-1")]
        [TestCase("fileSize", "-1")]
        [TestCase("fileSize", "9223372036854775808")]
        [TestCase("downloadURL", "\"ftp://files.example/package.7z\"")]
        [TestCase("downloadURL", "\"/relative/package.7z\"")]
        [TestCase("downloadURL", "\"https://user:password@files.example/package.7z\"")]
        [TestCase("bannerURL", "\"file:///C:/secret.png\"")]
        [TestCase("songs", "[]")]
        [TestCase("songs", "null")]
        public void TestMalformedGingerPackageFieldsAreRejected(string field, string jsonValue)
        {
            JsonNode package = JsonNode.Parse(ginger_package_json)!;
            package[field] = JsonNode.Parse(jsonValue);
            using var http = httpClientFor(gingerSearch(package.ToJsonString()));
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestInvalidResponseHashIsNotSilentlySkipped(BmsDownloadSource source)
        {
            string response = source == BmsDownloadSource.Ginger
                ? gingerSearch(ginger_package_json.Replace(md5_a, "not-a-hash", StringComparison.Ordinal))
                : konmaiSearch(konmaiChart("not-a-hash", song_url));
            using var http = httpClientFor(response);
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(source, "", 1, null, CancellationToken.None));
        }

        [TestCase("song_url", "\"javascript:alert(1)\"")]
        [TestCase("song_preview_url", "\"data:text/plain,example\"")]
        [TestCase("md5", "null")]
        [TestCase("chart_name", "42")]
        public void TestMalformedKonmaiChartFieldsAreRejected(string field, string jsonValue)
        {
            JsonNode chart = JsonNode.Parse(konmaiChart(md5_a, song_url))!;
            chart[field] = JsonNode.Parse(jsonValue);
            using var http = httpClientFor(konmaiSearch(chart.ToJsonString()));
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Konmai, "", 1, null, CancellationToken.None));
        }

        [TestCase("page", "2")]
        [TestCase("pageSize", "100")]
        [TestCase("pageCount", "2")]
        [TestCase("total", "-1")]
        [TestCase("total", "2147483648")]
        [TestCase("data", "null")]
        public void TestInvalidPaginationIsRejected(string field, string jsonValue)
        {
            JsonNode root = JsonNode.Parse(gingerSearch(ginger_package_json))!;
            root[field] = JsonNode.Parse(jsonValue);
            using var http = httpClientFor(root.ToJsonString());
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        [TestCase("not JSON")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"data\":")]
        public void TestInvalidJsonShapeIsReportedAsInvalidData(string response)
        {
            using var http = httpClientFor(response);
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        [Test]
        public void TestKonmaiFailureEnvelopeIsNotAnEmptySuccess()
        {
            using var http = httpClientFor(konmai_empty_search_json.Replace("success", "fail", StringComparison.Ordinal));
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Konmai, "", 1, null, CancellationToken.None));
        }

        [Test]
        public void TestBadRequestInputsDoNotReachTheNetwork()
        {
            int requestCount = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requestCount++;
                return Task.FromResult(jsonResponse(ginger_empty_search_json));
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 0, null, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, "table-name", CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => client.SearchAsync(BmsDownloadSource.Konmai, "", 1, "ftp://table.example/", CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => client.ResolveAsync(BmsDownloadSource.Ginger, "bad hash", CancellationToken.None));
            Assert.That(requestCount, Is.Zero);
        }

        [Test]
        public void TestCancellationBeforeRequest()
        {
            int requestCount = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requestCount++;
                return Task.FromResult(jsonResponse(ginger_empty_search_json));
            }));
            using var client = new BmsDownloadClient(http);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, cancellation.Token));
            Assert.That(requestCount, Is.Zero);
        }

        [Test]
        public void TestHttpClientTimeoutIsANetworkFailure()
        {
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("request timeout", new TimeoutException()))));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        [Test]
        public async Task TestRequestDeadlineIncludesTheBodyAfterSuccessfulHeaders()
        {
            var stream = new HangingResponseStream();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            })))
            { Timeout = TimeSpan.FromMilliseconds(500) };
            using var client = new BmsDownloadClient(http);

            Task<BmsDownloadSearchResult> pending = client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None);
            await stream.Started.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.ThrowsAsync<HttpRequestException>(() => pending);
            Assert.That(stream.Released, Is.True);
        }

        [Test]
        public async Task TestCancellationDuringRequest()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var http = new HttpClient(new StubHandler(async (_, token) =>
            {
                started.SetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return jsonResponse(ginger_empty_search_json);
            }));
            using var client = new BmsDownloadClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<BmsDownloadSearchResult> pending = client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, cancellation.Token);
            await started.Task;
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
        }

        [Test]
        public void TestCancellationDuringResponseRead()
        {
            using var cancellation = new CancellationTokenSource();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CancellingMemoryStream(Encoding.UTF8.GetBytes(ginger_empty_search_json), cancellation))
            })));
            using var client = new BmsDownloadClient(http);

            Assert.CatchAsync<OperationCanceledException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, cancellation.Token));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestOversizedJsonIsRejectedWithAndWithoutDeclaredLength(bool declaredLength)
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                HttpContent content;

                if (declaredLength)
                {
                    content = new StringContent(ginger_empty_search_json);
                    content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
                }
                else
                    content = new StreamContent(new NonSeekableMemoryStream(Encoding.UTF8.GetBytes(ginger_empty_search_json + new string(' ', 8 * 1024 * 1024))));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        [Test]
        public void TestUnknownDeepJsonFieldsAreAlsoBounded()
        {
            string response = ginger_empty_search_json[..^1] + ",\"unused\":" + new string('[', 33) + "0" + new string(']', 33) + "}";
            using var http = httpClientFor(response);
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(BmsDownloadSource.Ginger, "", 1, null, CancellationToken.None));
        }

        private static string gingerSearch(string packages, int page = 1, int total = 1) => "{\"data\":[" + packages + "],\"page\":" + page
            + ",\"pageSize\":20,\"pageCount\":" + ((total + 19L) / 20) + ",\"total\":" + total + "}";

        private static string konmaiSearch(string charts, int page = 1, int total = 1) => "{\"result\":\"success\",\"data\":[" + charts + "],\"page\":" + page
            + ",\"page_size\":20,\"total_pages\":" + ((total + 19L) / 20) + ",\"total\":" + total + "}";

        private static string konmaiChart(string md5, string? downloadUrl, string chartName = "Track [NORMAL]", string? title = "Track", string? artist = "Author",
                                         string? songName = "Track", string? previewUrl = "preview") => JsonSerializer.Serialize(new
                                         {
                                             chart_name = chartName,
                                             title,
                                             artist,
                                             md5,
                                             song_name = songName,
                                             song_url = downloadUrl,
                                             song_preview_url = previewUrl == "preview" ? "https://bms.alvorna.com/bms/score/view?md5=" + md5 : previewUrl
                                         });

        private static HttpClient httpClientFor(string response, HttpStatusCode status = HttpStatusCode.OK) => new HttpClient(new StubHandler((_, _) => Task.FromResult(jsonResponse(response, status))));

        private static HttpResponseMessage jsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;

            public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                this.send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
        }

        private class NonSeekableMemoryStream : MemoryStream
        {
            public NonSeekableMemoryStream(byte[] bytes)
                : base(bytes)
            {
            }

            public override bool CanSeek => false;
        }

        private sealed class CancellingMemoryStream : NonSeekableMemoryStream
        {
            private readonly CancellationTokenSource cancellation;

            public CancellingMemoryStream(byte[] bytes, CancellationTokenSource cancellation)
                : base(bytes)
            {
                this.cancellation = cancellation;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return base.ReadAsync(buffer, cancellationToken);
            }
        }

        private sealed class HangingResponseStream : NonSeekableMemoryStream
        {
            private readonly TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task Started => started.Task;
            public bool Released { get; private set; }

            public HangingResponseStream()
                : base(Array.Empty<byte>())
            {
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

            protected override void Dispose(bool disposing)
            {
                Released = true;
                base.Dispose(disposing);
            }
        }
    }
}
