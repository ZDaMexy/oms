// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Globalization;
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
                return Task.FromResult(jsonResponse("{\"result\":\"success\",\"tables\":[{\"diff_table_name\":\"Normal\",\"diff_table_url\":\"https://table.example/a\",\"diff_table_local_url\":\"/proxied/a\",\"diff_table_full_local_url\":\"https://bms.alvorna.com/tables/a/header.json\"},{\"diff_table_name\":\"Normal\",\"diff_table_url\":\"https://table.example/b\"}]}"));
            }));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTablesAsync(BmsDownloadSource.Konmai, CancellationToken.None);

            Assert.That(result.Select(table => table.Id), Is.EqualTo(new[] { "https://table.example/a", "https://table.example/b" }));
            Assert.That(result.Select(table => table.OriginalUrl), Is.EqualTo(result.Select(table => table.Id)));
            Assert.That(result[0].HeaderUrl, Is.EqualTo(new Uri("https://bms.alvorna.com/tables/a/header.json")));
            Assert.That(result[1].HeaderUrl, Is.Null);
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

        [Test]
        public async Task TestGingerReadsEveryTablePageAndPreservesOriginalGradeOrderAndStrings()
        {
            string[] entries = Enumerable.Range(0, 101).Select(index => tableEntry(hashFor(index), index switch
            {
                1 => " 1 ",
                100 => "EX",
                _ => "01"
            }, headerId: 7)).ToArray();
            var requestedPages = new ConcurrentQueue<int>();
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://gingerrush.com/api/v1/table/selectOneHeader/7"));
                    // External header/data URLs are catalogue metadata, not a request destination.
                    return jsonResponse("{\"id\":7,\"symbol\":\"sl\",\"levelOrders\":\"EX,01,, , 1 ,unknown\",\"dataURL\":\"http://external.example/table.json\"}");
                }

                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://gingerrush.com/api/v1/table/selectDataList"));
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                JsonElement root = body.RootElement;
                int page = root.GetProperty("pageRequest").GetProperty("page").GetInt32();
                requestedPages.Enqueue(page);
                Assert.That(root.GetProperty("pageRequest").GetProperty("pageSize").GetInt32(), Is.EqualTo(100));
                Assert.That(root.GetProperty("headerID").GetInt32(), Is.EqualTo(7));
                Assert.That(root.GetProperty("fuzzyKeyword").ValueKind, Is.EqualTo(JsonValueKind.Null));
                return jsonResponse(gingerTablePage(string.Join(",", entries.Skip((page - 1) * 100).Take(100)), page, 101));
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadTableData result = await client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("7", "Satellite", ""), CancellationToken.None);

            Assert.That(requestedPages, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(result.Symbol, Is.EqualTo("sl"));
            Assert.That(result.Levels, Is.EqualTo(new[] { "EX", "01", " 1 " }));
            Assert.That(result.Entries, Has.Count.EqualTo(101));
            Assert.That(result.Entries[100], Is.EqualTo(new BmsDownloadTableEntry(hashFor(100), "EX", "Table title", "Table artist")));
        }

        [Test]
        public async Task TestGingerWithoutDeclaredOrderUsesCompleteTableFirstOccurrenceOrder()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Get
                ? "{\"id\":1,\"symbol\":\"sl\",\"levelOrders\":\" , , , \"}"
                : gingerTablePage(tableEntry(md5_a, "10") + "," + tableEntry(md5_b, "2") + "," + tableEntry(md5_c, "0"), total: 3)))));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("1", "Satellite", ""), CancellationToken.None);

            Assert.That(result.Levels, Is.EqualTo(new[] { "10", "2", "0" }));
        }

        [TestCase("[\"EX\",\"01\",\"1\",\"missing\"]", "\"1\"")]
        [TestCase("[\"EX\",\"01\",1,999]", "1")]
        public async Task TestKonmaiReadsSelectedMirrorAndUsesStringAndNumericLevelOrders(string orderedLevels, string numericLevel)
        {
            const string header = "https://bms.alvorna.com/tables/selected/header.json";
            var requestedUrls = new ConcurrentQueue<string>();
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                requestedUrls.Enqueue(request.RequestUri!.AbsoluteUri);
                if (request.RequestUri.AbsoluteUri == header)
                    return Task.FromResult(jsonResponse("{\"symbol\":\"◆\",\"level_order\":" + orderedLevels + ",\"data_url\":\"table.json\"}"));

                Assert.That(request.RequestUri.AbsoluteUri, Is.EqualTo("https://bms.alvorna.com/tables/selected/table.json"));
                return Task.FromResult(jsonResponse("[" + tableEntry(md5_a, "01") + "," + tableEntry(md5_b, "1").Replace("\"level\":\"1\"", "\"level\":" + numericLevel, StringComparison.Ordinal)
                    + "," + tableEntry(md5_c, "EX") + "," + tableEntry(hashFor(1), "") + "]"));
            }));
            using var client = new BmsDownloadClient(http);

            var table = new BmsDownloadTable("https://external.example/table", "Selected", "https://external.example/table", new Uri(header));
            var result = await client.GetTableDataAsync(BmsDownloadSource.Konmai, table, CancellationToken.None);

            Assert.That(requestedUrls, Is.EqualTo(new[] { header, "https://bms.alvorna.com/tables/selected/table.json" }));
            Assert.That(result.Symbol, Is.EqualTo("◆"));
            Assert.That(result.Levels, Is.EqualTo(new[] { "EX", "01", "1", "" }));
            Assert.That(result.Entries.Select(entry => entry.Md5), Is.EqualTo(new[] { md5_a, md5_b, md5_c, hashFor(1) }));
            Assert.That(result.Entries[0].Level, Is.EqualTo("01"));
        }

        [Test]
        public async Task TestKonmaiWithoutLevelOrderKeepsBodyOrderAndMissingAuthorMetadata()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.RequestUri!.AbsolutePath.EndsWith("header.json", StringComparison.Ordinal)
                ? "{\"data_url\":\"table.json\"}"
                : "[{\"md5\":\"" + md5_a + "\",\"level\":\"◆23\",\"title\":null,\"artist\":null}," + tableEntry(md5_b, "入門") + "]"))));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTableDataAsync(BmsDownloadSource.Konmai, konmaiTable(), CancellationToken.None);

            Assert.That(result.Symbol, Is.Empty);
            Assert.That(result.Levels, Is.EqualTo(new[] { "◆23", "入門" }));
            Assert.That(result.Entries[0].Title, Is.Empty);
            Assert.That(result.Entries[0].Artist, Is.Empty);
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestAnEmptyFullTableIsAValidEmptyGradeList(BmsDownloadSource source)
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(source == BmsDownloadSource.Ginger
                ? request.Method == HttpMethod.Get ? "{\"id\":1,\"symbol\":\"sl\",\"levelOrders\":\"0,1\"}" : gingerTablePage("", total: 0)
                : request.RequestUri!.AbsolutePath.EndsWith("header.json", StringComparison.Ordinal) ? "{\"data_url\":\"table.json\",\"level_order\":[\"0\",\"1\"]}" : "[]"))));
            using var client = new BmsDownloadClient(http);

            var result = await client.GetTableDataAsync(source, source == BmsDownloadSource.Ginger ? new BmsDownloadTable("1", "Empty", "") : konmaiTable(), CancellationToken.None);

            Assert.That(result.Levels, Is.Empty);
            Assert.That(result.Entries, Is.Empty);
        }

        [TestCase("[]", 101)]
        [TestCase("entry", 102)]
        public void TestGingerDoesNotPublishAnIncompleteOrChangedTable(string lastPageEntries, int lastPageTotal)
        {
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get)
                    return jsonResponse("{\"id\":1}");

                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                int page = body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32();
                return jsonResponse(page == 1
                    ? gingerTablePage(string.Join(",", Enumerable.Range(0, 100).Select(index => tableEntry(hashFor(index), "0"))), total: 101)
                    : gingerTablePage(lastPageEntries == "entry" ? tableEntry(md5_a, "1") : "", 2, lastPageTotal));
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("1", "Satellite", ""), CancellationToken.None));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestGingerRejectsHeadersAndEntriesOfAnotherTable(bool wrongHeader)
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Get
                ? wrongHeader ? "{\"id\":2}" : "{\"id\":1}"
                : gingerTablePage(tableEntry(md5_a, "1", headerId: 2))))));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("1", "Satellite", ""), CancellationToken.None));
        }

        [TestCase(null)]
        [TestCase("http://bms.alvorna.com/tables/a/header.json")]
        [TestCase("https://external.example/header.json")]
        [TestCase("https://bms.alvorna.com.external.example/header.json")]
        [TestCase("https://bms.alvorna.com:444/header.json")]
        [TestCase("https://user:password@bms.alvorna.com/header.json")]
        public void TestKonmaiDoesNotVisitAnUntrustedHeaderOrFallBackToTheOriginalSite(string? headerUrl)
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse("{}"));
            }));
            using var client = new BmsDownloadClient(http);
            var table = new BmsDownloadTable("https://external.example/original", "Table", "https://external.example/original", headerUrl == null ? null : new Uri(headerUrl));

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Konmai, table, CancellationToken.None));
            Assert.That(requests, Is.Zero);
        }

        [TestCase("https://external.example/table.json")]
        [TestCase("//external.example/table.json")]
        [TestCase("http://bms.alvorna.com/table.json")]
        [TestCase("https://bms.alvorna.com:444/table.json")]
        [TestCase("https://user@bms.alvorna.com/table.json")]
        [TestCase(" data.json")]
        [TestCase("data\n.json")]
        public void TestKonmaiDoesNotLetTheMirrorHeaderEscapeToAnotherBodyHost(string dataUrl)
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse(JsonSerializer.Serialize(new { data_url = dataUrl })));
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Konmai, konmaiTable(), CancellationToken.None));
            Assert.That(requests, Is.EqualTo(1));
        }

        [TestCase(BmsDownloadSource.Ginger, "md5", "\"invalid\"")]
        [TestCase(BmsDownloadSource.Konmai, "md5", "\"invalid\"")]
        [TestCase(BmsDownloadSource.Ginger, "level", "null")]
        [TestCase(BmsDownloadSource.Konmai, "level", "true")]
        [TestCase(BmsDownloadSource.Ginger, "title", "123")]
        [TestCase(BmsDownloadSource.Konmai, "artist", "[]")]
        public void TestMalformedFullTableEntryIsNotSilentlyRemoved(BmsDownloadSource source, string field, string value)
        {
            JsonNode entry = JsonNode.Parse(tableEntry(md5_a, "1"))!;
            entry[field] = JsonNode.Parse(value);
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(source == BmsDownloadSource.Ginger
                ? request.Method == HttpMethod.Get ? "{\"id\":1}" : gingerTablePage(entry.ToJsonString())
                : request.RequestUri!.AbsolutePath.EndsWith("header.json", StringComparison.Ordinal) ? "{\"data_url\":\"table.json\"}" : "[" + entry.ToJsonString() + "]"))));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(source, source == BmsDownloadSource.Ginger ? new BmsDownloadTable("1", "Table", "") : konmaiTable(), CancellationToken.None));
        }

        [TestCase("[null]")]
        [TestCase("[{}]")]
        [TestCase("\"1,2\"")]
        public void TestKonmaiMalformedLevelOrderIsAnExplicitFailure(string levelOrder)
        {
            using var http = httpClientFor("{\"data_url\":\"table.json\",\"level_order\":" + levelOrder + "}");
            using var client = new BmsDownloadClient(http);
            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Konmai, konmaiTable(), CancellationToken.None));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestCompleteTableBudgetIncludesEveryPageAndTheHeader(BmsDownloadSource source)
        {
            string padding = new string(' ', 4 * 1024 * 1024);
            int requests = 0;
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                requests++;
                if (source == BmsDownloadSource.Konmai)
                    return jsonResponse((request.RequestUri!.AbsolutePath.EndsWith("header.json", StringComparison.Ordinal)
                        ? "{\"data_url\":\"table.json\"}" : "[" + tableEntry(md5_a, "1") + "]") + padding);

                if (request.Method == HttpMethod.Get)
                    return jsonResponse("{\"id\":1}");

                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                int page = body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32();
                string data = page == 1 ? string.Join(",", Enumerable.Repeat(tableEntry(md5_a, "1"), 100)) : tableEntry(md5_b, "2");
                return jsonResponse(gingerTablePage(data, page, 101) + padding);
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(source, source == BmsDownloadSource.Ginger ? new BmsDownloadTable("1", "Table", "") : konmaiTable(), CancellationToken.None));
            Assert.That(requests, Is.EqualTo(source == BmsDownloadSource.Ginger ? 3 : 2));
        }

        [Test]
        public async Task TestCancellingTheNextGingerPageDoesNotPublishTheFirstPageAsAFullTable()
        {
            var lastPageStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get)
                    return jsonResponse("{\"id\":1}");

                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                if (body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32() == 1)
                    return jsonResponse(gingerTablePage(string.Join(",", Enumerable.Range(0, 100).Select(index => tableEntry(hashFor(index), "1"))), total: 101));

                lastPageStarted.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return jsonResponse(gingerTablePage(tableEntry(md5_c, "2"), 2, 101));
            }));
            using var client = new BmsDownloadClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<BmsDownloadTableData> pending = client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("1", "Satellite", ""), cancellation.Token);
            await lastPageStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
        }

        [Test]
        public async Task TestCancellingTheKonmaiBodyReleasesTheIncompleteMirror()
        {
            var stream = new HangingResponseStream();
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("header.json", StringComparison.Ordinal)
                ? jsonResponse("{\"data_url\":\"table.json\"}") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })));
            using var client = new BmsDownloadClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<BmsDownloadTableData> pending = client.GetTableDataAsync(BmsDownloadSource.Konmai, konmaiTable(), cancellation.Token);
            await stream.Started.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
            Assert.That(stream.Released, Is.True);
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestGradeSearchKeepsOnlyCurrentTargetsAndUsesTheRealPackageIdentity(BmsDownloadSource source)
        {
            var data = new BmsDownloadTableData("★", new[] { "01", "1" }, new[]
            {
                new BmsDownloadTableEntry(md5_a, "01", "Same song", "Author"),
                new BmsDownloadTableEntry(md5_b, "1", "Same song", "Author"),
                new BmsDownloadTableEntry(md5_c, "01", "Same song", "Author"),
                new BmsDownloadTableEntry(md5_a, "01", "Duplicate entry", "Author")
            });
            var requestedHashes = new ConcurrentBag<string>();
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                string md5 = request.RequestUri!.AbsoluteUri[^32..];
                requestedHashes.Add(md5);
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri.AbsoluteUri, Is.EqualTo(source == BmsDownloadSource.Ginger
                    ? "https://gingerrush.com/api/v1/files/package/" + md5 : "https://bms.alvorna.com/api/hash?md5=" + md5));
                if (source == BmsDownloadSource.Konmai)
                    return Task.FromResult(jsonResponse("{\"result\":\"success\",\"data\":" + konmaiChart(md5, song_url) + "}"));

                JsonNode package = JsonNode.Parse(ginger_package_json)!;
                JsonNode normal = package["songs"]![0]!.DeepClone();
                JsonNode otherLevel = normal.DeepClone();
                otherLevel["md5"] = md5_b;
                JsonNode sameLevel = normal.DeepClone();
                sameLevel["md5"] = md5_c;
                package["songs"] = new JsonArray(normal, otherLevel, sameLevel);
                return Task.FromResult(jsonResponse(package.ToJsonString()));
            }));
            using var client = new BmsDownloadClient(http);

            BmsDownloadSearchResult result = await client.SearchTableLevelAsync(source, data, "01", "", 1, CancellationToken.None);

            Assert.That(requestedHashes, Is.EquivalentTo(new[] { md5_a, md5_c }));
            Assert.That(result.Total, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(1));
            Assert.That(result.Packages, Has.Count.EqualTo(1));
            Assert.That(result.Packages[0].Id, Is.EqualTo(source == BmsDownloadSource.Ginger ? "17" : song_url));
            Assert.That(result.Packages[0].Charts.Select(chart => chart.Md5), Is.EqualTo(new[] { md5_a, md5_c }));
            Assert.That(result.Packages[0].Charts[0].Level, Is.EqualTo(source == BmsDownloadSource.Ginger ? "12" : null));
        }

        [TestCase("月光 & #%?", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [TestCase(" COMPOSER ", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
        [TestCase("CCCCCCCC", "cccccccccccccccccccccccccccccccc")]
        public async Task TestGradeKeywordUsesFullTableTitleArtistAndHash(string query, string expectedHash)
        {
            var data = new BmsDownloadTableData("★", new[] { "1" }, new[]
            {
                new BmsDownloadTableEntry(md5_a, "1", "月光 & #%?", "Writer"),
                new BmsDownloadTableEntry(md5_b, "1", "Different title", "Composer"),
                new BmsDownloadTableEntry(md5_c, "1", "Third title", "Writer"),
                new BmsDownloadTableEntry(hashFor(1), "2", "月光 & #%? Composer", "Writer")
            });
            var requestedHashes = new ConcurrentBag<string>();
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                requestedHashes.Add(request.RequestUri!.AbsoluteUri[^32..]);
                return Task.FromResult(jsonResponse("Not found", HttpStatusCode.NotFound));
            }));
            using var client = new BmsDownloadClient(http);

            var result = await client.SearchTableLevelAsync(BmsDownloadSource.Konmai, data, "1", query, 1, CancellationToken.None);

            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(requestedHashes, Is.EqualTo(new[] { expectedHash }));
            Assert.That(result.Packages.Single().Charts.Single().Md5, Is.EqualTo(expectedHash));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public async Task TestGradePaginationCountsUniqueTargetsAndKeeps404TableMetadata(BmsDownloadSource source)
        {
            BmsDownloadTableEntry[] entries = Enumerable.Range(1, 21).Select(index => new BmsDownloadTableEntry(hashFor(index), "01", "Song " + index, "Table author"))
                .Append(new BmsDownloadTableEntry(hashFor(1), "01", "Repeated song", "Table author"))
                .Append(new BmsDownloadTableEntry(hashFor(99), "1", "Song 99", "Table author")).ToArray();
            var data = new BmsDownloadTableData("★", new[] { "01", "1" }, entries);
            var requestedHashes = new ConcurrentBag<string>();
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                requestedHashes.Add(request.RequestUri!.AbsoluteUri[^32..]);
                return Task.FromResult(jsonResponse("Not found", HttpStatusCode.NotFound));
            }));
            using var client = new BmsDownloadClient(http);

            var first = await client.SearchTableLevelAsync(source, data, "01", "", 1, CancellationToken.None);
            Assert.That(first.Packages, Has.Count.EqualTo(20));
            Assert.That(requestedHashes, Is.EquivalentTo(entries.Take(20).Select(entry => entry.Md5)));
            requestedHashes.Clear();
            var second = await client.SearchTableLevelAsync(source, data, "01", "", 2, CancellationToken.None);

            Assert.That(second.Page, Is.EqualTo(2));
            Assert.That(second.Total, Is.EqualTo(21));
            Assert.That(second.TotalPages, Is.EqualTo(2));
            Assert.That(requestedHashes, Is.EqualTo(new[] { hashFor(21) }));
            BmsDownloadPackage noPackage = second.Packages.Single();
            Assert.That(noPackage.Source, Is.EqualTo(source));
            Assert.That(noPackage.Id, Is.EqualTo("chart:" + hashFor(21)));
            Assert.That(noPackage.Name, Is.EqualTo("Song 21"));
            Assert.That(noPackage.CanDownload, Is.False);
            Assert.That(noPackage.Charts.Single(), Is.EqualTo(new BmsDownloadChart(hashFor(21), "Song 21", "Table author")));
            requestedHashes.Clear();

            var beyondLastPage = await client.SearchTableLevelAsync(source, data, "01", "", 3, CancellationToken.None);
            var noMatch = await client.SearchTableLevelAsync(source, data, "01", "unknown keyword", 1, CancellationToken.None);

            Assert.That(beyondLastPage.Packages, Is.Empty);
            Assert.That(beyondLastPage.Total, Is.EqualTo(21));
            Assert.That(noMatch.Packages, Is.Empty);
            Assert.That(noMatch.Total, Is.Zero);
            Assert.That(requestedHashes, Is.Empty);
        }

        [Test]
        public async Task TestUnclassifiedGradeIsDistinctFromAllGradesAnd404WithoutTitleStillIdentifiesTheChart()
        {
            var data = new BmsDownloadTableData("", new[] { "", "1" }, new[]
            {
                new BmsDownloadTableEntry(md5_a, "", "", ""),
                new BmsDownloadTableEntry(md5_b, "1", "Other grade", "Artist")
            });
            var requestedHashes = new ConcurrentBag<string>();
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                requestedHashes.Add(request.RequestUri!.AbsoluteUri[^32..]);
                return Task.FromResult(jsonResponse("Not found", HttpStatusCode.NotFound));
            }));
            using var client = new BmsDownloadClient(http);

            var result = await client.SearchTableLevelAsync(BmsDownloadSource.Konmai, data, "", "", 1, CancellationToken.None);

            Assert.That(requestedHashes, Is.EqualTo(new[] { md5_a }));
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Packages.Single().Name, Is.EqualTo(md5_a));
            Assert.That(result.Packages.Single().Charts.Single().Artist, Is.Empty);
        }

        [Test]
        public async Task TestGradeResolutionIsLimitedToFourConcurrentRequestsAndCancellationWithdrawsThem()
        {
            var data = new BmsDownloadTableData("★", new[] { "1" }, Enumerable.Range(0, 20)
                .Select(index => new BmsDownloadTableEntry(hashFor(index), "1", "Song", "Author")).ToArray());
            var fourStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int requests = 0;
            int active = 0;
            using var http = new HttpClient(new StubHandler(async (_, token) =>
            {
                Interlocked.Increment(ref active);
                if (Interlocked.Increment(ref requests) == 4)
                    fourStarted.TrySetResult(true);

                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return jsonResponse("Not found", HttpStatusCode.NotFound);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }));
            using var client = new BmsDownloadClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<BmsDownloadSearchResult> pending = client.SearchTableLevelAsync(BmsDownloadSource.Konmai, data, "1", "", 1, cancellation.Token);
            await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(Volatile.Read(ref requests), Is.EqualTo(4));
            Assert.That(Volatile.Read(ref active), Is.EqualTo(4));
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
            Assert.That(Volatile.Read(ref active), Is.Zero);
            Assert.That(Volatile.Read(ref requests), Is.EqualTo(4));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestGradePackageNetworkFailureIsNotConvertedTo404(BmsDownloadSource source)
        {
            var data = new BmsDownloadTableData("★", new[] { "1" }, new[] { new BmsDownloadTableEntry(md5_a, "1", "Song", "Author") });
            using var http = httpClientFor("{}", HttpStatusCode.ServiceUnavailable);
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<HttpRequestException>(() => client.SearchTableLevelAsync(source, data, "1", "", 1, CancellationToken.None));
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestSourceRedirectIsAnHttpFailureForSearchResolveAndTableMetadata(BmsDownloadSource source)
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                var response = jsonResponse("Redirect", HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://external.example/catalogue");
                return Task.FromResult(response);
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(source, "", 1, null, CancellationToken.None));
            Assert.ThrowsAsync<HttpRequestException>(() => client.ResolveAsync(source, md5_a, CancellationToken.None));
            Assert.ThrowsAsync<HttpRequestException>(() => client.GetTableDataAsync(source, source == BmsDownloadSource.Ginger ? new BmsDownloadTable("1", "Table", "") : konmaiTable(), CancellationToken.None));
        }

        [Test]
        public void TestInjectedClientCannotPublishRedirectedTableMetadata()
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                var response = jsonResponse("{\"data_url\":\"table.json\"}");
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://external.example/header.json");
                return Task.FromResult(response);
            }));
            using var client = new BmsDownloadClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetTableDataAsync(BmsDownloadSource.Konmai, konmaiTable(), CancellationToken.None));
        }

        [Test]
        public void TestTableAndGradeBadInputsAndPreCancellationDoNotStartRequests()
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse("{}"));
            }));
            using var client = new BmsDownloadClient(http);
            var data = new BmsDownloadTableData("", Array.Empty<string>(), Array.Empty<BmsDownloadTableEntry>());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<ArgumentException>(() => client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("Name", "Table", ""), CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchTableLevelAsync(BmsDownloadSource.Ginger, data, "1", "", 0, CancellationToken.None));
            Assert.CatchAsync<OperationCanceledException>(() => client.GetTableDataAsync(BmsDownloadSource.Ginger, new BmsDownloadTable("1", "Table", ""), cancellation.Token));
            Assert.CatchAsync<OperationCanceledException>(() => client.SearchTableLevelAsync(BmsDownloadSource.Konmai, data, "1", "", 1, cancellation.Token));
            Assert.That(requests, Is.Zero);
        }

        private static string hashFor(int index) => index.ToString("x32", CultureInfo.InvariantCulture);

        private static string tableEntry(string md5, string level, string title = "Table title", string artist = "Table artist", int headerId = 1) => JsonSerializer.Serialize(new
        {
            md5,
            level,
            title,
            artist,
            headerID = headerId
        });

        private static string gingerTablePage(string entries, int page = 1, int total = 1) => "{\"data\":[" + entries + "],\"page\":" + page
            + ",\"pageSize\":100,\"pageCount\":" + ((total + 99L) / 100) + ",\"total\":" + total + "}";

        private static BmsDownloadTable konmaiTable() => new BmsDownloadTable("https://external.example/table", "Table", "https://external.example/table",
            new Uri("https://bms.alvorna.com/tables/selected/header.json"));

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
