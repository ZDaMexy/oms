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
using osu.Game.Online.Sayobot;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class SayobotClientTest
    {
        [Test]
        public async Task TestSearchSendsManiaFiltersAndIdentifiesTheSoftware()
        {
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo("OMS/1.0 (+https://github.com/ZDaMexy/oms)"));
                Assert.That(request.Headers.Referrer!.AbsoluteUri, Is.EqualTo("https://github.com/ZDaMexy/oms/"));

                if (request.Method == HttpMethod.Post)
                {
                    Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.sayobot.cn/?post"));
                    Assert.That(request.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
                    using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token));
                    JsonElement body = document.RootElement;
                    Assert.That(body.GetProperty("cmd").GetString(), Is.EqualTo("beatmaplist"));
                    Assert.That(body.GetProperty("type").GetString(), Is.EqualTo("search"));
                    Assert.That(body.GetProperty("mode").GetInt32(), Is.EqualTo(8));
                    Assert.That(body.GetProperty("keyword").GetString(), Is.EqualTo("月光 & #%?"));
                    Assert.That(body.GetProperty("offset").GetInt32(), Is.EqualTo(3903));
                    Assert.That(body.GetProperty("limit").GetInt32(), Is.EqualTo(20));
                    Assert.That(body.GetProperty("cs").EnumerateArray().Select(value => value.GetInt32()), Is.EqualTo(new[] { 4, 4 }));
                    Assert.That(body.GetProperty("stars").EnumerateArray().Select(value => value.GetDouble()), Is.EqualTo(new[] { 1.0, 3.0 }));
                    Assert.That(body.GetProperty("class").GetInt32(), Is.EqualTo(4));
                    return jsonResponse(searchResponse(0, 17));
                }

                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.sayobot.cn/v2/beatmapinfo?0=17"));
                return jsonResponse(detailResponse(17, chart(23, stars: 2.5), approval: 4));
            }));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery(" 月光 & #%? ", 4, 1, 3, SayobotCategory.Loved), 3903, CancellationToken.None);

            SayobotBeatmapSet set = result.Sets.Single();
            Assert.That(set.Key, Is.EqualTo("Sayobot:17"));
            Assert.That(set.Title, Is.EqualTo("Actual song title"));
            Assert.That(set.Artist, Is.EqualTo("Artist"));
            Assert.That(set.Creator, Is.EqualTo("Mapper"));
            Assert.That(set.Status, Is.EqualTo(4));
            Assert.That(set.Beatmaps.Single(), Is.EqualTo(new SayobotBeatmap(23, "Easy", 4, 2.5)));
            Assert.That(set.DownloadUrl.AbsoluteUri, Is.EqualTo("https://dl.sayobot.cn/beatmaps/download/novideo/17"));
            Assert.That(set.CoverUrl.AbsoluteUri, Is.EqualTo("https://a.sayobot.cn/beatmaps/17/covers/cover.webp?0"));
            Assert.That(result.HasMore, Is.False);
        }

        [Test]
        public async Task TestPaginationUsesTheReturnedCursorAndAllowsANonEmptyLastPage()
        {
            var offsets = new ConcurrentQueue<int>();
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                    JsonElement body = document.RootElement;
                    Assert.That(body.TryGetProperty("class", out _), Is.False);
                    Assert.That(body.TryGetProperty("cs", out _), Is.False);
                    Assert.That(body.TryGetProperty("stars", out _), Is.False);
                    int offset = body.GetProperty("offset").GetInt32();
                    offsets.Enqueue(offset);
                    return jsonResponse(offset == 0 ? searchResponse(3903, 11, 12) : searchResponse(0, 13));
                }

                int id = int.Parse(request.RequestUri!.Query[3..], CultureInfo.InvariantCulture);
                return jsonResponse(detailResponse(id, chart(id + 100)));
            }));
            using var client = new SayobotClient(http);

            SayobotSearchResult first = await client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None);
            SayobotSearchResult second = await client.SearchAsync(new SayobotSearchQuery(""), first.NextOffset, CancellationToken.None);

            Assert.That(first.Sets.Select(set => set.Id), Is.EqualTo(new[] { 11, 12 }));
            Assert.That(first.NextOffset, Is.EqualTo(3903));
            Assert.That(first.HasMore, Is.True);
            Assert.That(second.Sets.Select(set => set.Id), Is.EqualTo(new[] { 13 }));
            Assert.That(second.NextOffset, Is.Zero);
            Assert.That(second.HasMore, Is.False);
            Assert.That(offsets, Is.EqualTo(new[] { 0, 3903 }));
        }

        [Test]
        public async Task TestANumericKeywordLoadsThatSetDirectlyAndAppliesTheSameFilters()
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                requests++;
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.sayobot.cn/v2/beatmapinfo?0=2440353"));
                return Task.FromResult(jsonResponse(detailResponse(2440353, chart(5323273, stars: 1.481) + "," + chart(5323274, stars: 3.053))));
            }));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery(" 2440353 ", 4, 0, 2, SayobotCategory.Ranked), 0, CancellationToken.None);

            Assert.That(result.Sets.Single().Beatmaps.Single().Id, Is.EqualTo(5323273));
            Assert.That(result.HasMore, Is.False);
            Assert.That(result.NextOffset, Is.Zero);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void TestAMissingNumericSetDoesNotFallBackToANameSearch()
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((request, _) =>
            {
                requests++;
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                return Task.FromResult(jsonResponse("{\"status\":-1}"));
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery("999999999"), 0, CancellationToken.None));
            Assert.That(requests, Is.EqualTo(1));
        }

        [TestCase("0")]
        [TestCase("2147483648")]
        public void TestInvalidNumericSearchIsAReadFailureWithoutSendingARequest(string text)
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse("{\"status\":-1}"));
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(text), 0, CancellationToken.None));
            Assert.That(requests, Is.Zero);
        }

        [Test]
        public async Task TestOnlyNativeManiaDifficultiesArePublishedFromAMixedPackage()
        {
            using var http = httpClientFor(detailResponse(2506607,
                chart(5519555, name: "Finished", stars: 1.416)
                + "," + chart(5526234, keys: 4.8, mode: 0)
                + "," + chart(5530849, mode: 1)
                + "," + chart(5533399, mode: 2)
                + "," + chart(5519611, name: "Normal", stars: 0.9729)));
            using var client = new SayobotClient(http);

            SayobotBeatmapSet set = await client.GetBeatmapSetAsync(2506607, CancellationToken.None);

            Assert.That(set.Beatmaps, Is.EqualTo(new[]
            {
                new SayobotBeatmap(5519555, "Finished", 4, 1.416),
                new SayobotBeatmap(5519611, "Normal", 4, 0.9729)
            }));
        }

        [Test]
        public async Task TestKeyAndStarRangesMustMatchTheSameDifficulty()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Post
                ? searchResponse(0, 17)
                : detailResponse(17, chart(21, keys: 7, stars: 2) + "," + chart(22, keys: 4, stars: 4) + "," + chart(23, keys: 4, stars: 2))))));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery("", 4, 1, 3), 0, CancellationToken.None);

            Assert.That(result.Sets.Single().Beatmaps.Select(beatmap => beatmap.Id), Is.EqualTo(new[] { 23 }));
        }

        [Test]
        public async Task TestAnEntireFilteredPageCanBeEmptyWithoutEndingPagination()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Post
                ? searchResponse(3903, 17)
                : detailResponse(17, chart(21, keys: 7, stars: 2) + "," + chart(22, keys: 4, stars: 4))))));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery("", 4, 1, 3), 0, CancellationToken.None);

            Assert.That(result.Sets, Is.Empty);
            Assert.That(result.NextOffset, Is.EqualTo(3903));
            Assert.That(result.HasMore, Is.True);
        }

        [TestCase(SayobotCategory.Ranked, 1, true)]
        [TestCase(SayobotCategory.Ranked, 2, true)]
        [TestCase(SayobotCategory.Ranked, 3, false)]
        [TestCase(SayobotCategory.Qualified, 3, true)]
        [TestCase(SayobotCategory.Loved, 4, true)]
        [TestCase(SayobotCategory.Pending, -1, true)]
        [TestCase(SayobotCategory.Pending, 0, true)]
        [TestCase(SayobotCategory.Graveyard, -2, true)]
        public async Task TestCategoriesUseTheActualApprovalOfTheDetailedSet(SayobotCategory category, int approval, bool expected)
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Post
                ? searchResponse(0, 17)
                : detailResponse(17, chart(23), approval)))));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery("", Category: category), 0, CancellationToken.None);

            Assert.That(result.Sets.Count, Is.EqualTo(expected ? 1 : 0));
        }

        [TestCase("{\"status\":-1}")]
        [TestCase("{\"status\":-1,\"endid\":0,\"data\":[]}")]
        public async Task TestBothRealNoMatchShapesAreEmptyResults(string response)
        {
            using var http = httpClientFor(response);
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery("no matching song"), 0, CancellationToken.None);

            Assert.That(result.Sets, Is.Empty);
            Assert.That(result.HasMore, Is.False);
        }

        [Test]
        public async Task TestASetRemovedAfterSearchIsSkippedWhileTheCursorIsKept()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(jsonResponse(request.Method == HttpMethod.Post
                ? searchResponse(3903, 17, 18)
                : request.RequestUri!.Query == "?0=17" ? "{\"status\":-1}" : detailResponse(18, chart(23))))));
            using var client = new SayobotClient(http);

            SayobotSearchResult result = await client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None);

            Assert.That(result.Sets.Select(set => set.Id), Is.EqualTo(new[] { 18 }));
            Assert.That(result.NextOffset, Is.EqualTo(3903));
            Assert.That(result.HasMore, Is.True);
            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [TestCase("not JSON")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"status\":-2}")]
        [TestCase("{\"status\":0,\"endid\":0,\"data\":null}")]
        [TestCase("{\"status\":-1,\"endid\":12}")]
        [TestCase("{\"status\":-1,\"data\":[{\"sid\":17}]}")]
        public void TestMalformedSearchResponsesAreFailures(string response)
        {
            using var http = httpClientFor(response);
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
        }

        [TestCase(3903)]
        [TestCase(100)]
        [TestCase(-1)]
        public void TestAContinuationCursorMustAdvance(int nextOffset)
        {
            using var http = httpClientFor(searchResponse(nextOffset, 17));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(""), 3903, CancellationToken.None));
        }

        [Test]
        public void TestAnOversizedPageAndDuplicateSetIdsAreRejectedBeforeReadingDetails()
        {
            int requests = 0;
            string response = searchResponse(0, Enumerable.Range(1, 21).ToArray());
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse(response));
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
            response = searchResponse(0, 17, 17);
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
            Assert.That(requests, Is.EqualTo(2));
        }

        [TestCase("bid", "0")]
        [TestCase("mode", "4")]
        [TestCase("CS", "4.5")]
        [TestCase("CS", "19")]
        [TestCase("star", "-1")]
        [TestCase("star", "1e400")]
        [TestCase("version", "null")]
        public void TestMalformedNativeManiaDifficultiesAreNotSilentlySkipped(string field, string value)
        {
            JsonNode response = JsonNode.Parse(detailResponse(17, chart(23)))!;
            response["data"]!["bid_data"]![0]![field] = JsonNode.Parse(value);
            using var http = httpClientFor(response.ToJsonString());
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [TestCase("sid", "18")]
        [TestCase("approved", "5")]
        [TestCase("title", "42")]
        [TestCase("artist", "null")]
        [TestCase("creator", "\"\"")]
        public void TestMalformedOrMismatchedSetDetailsAreFailures(string field, string value)
        {
            JsonNode response = JsonNode.Parse(detailResponse(17, chart(23)))!;
            response["data"]![field] = JsonNode.Parse(value);
            using var http = httpClientFor(response.ToJsonString());
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [Test]
        public void TestDuplicateBeatmapIdsAreRejected()
        {
            using var http = httpClientFor(detailResponse(17, chart(23) + "," + chart(23, name: "Conflicting difficulty")));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [Test]
        public void TestADetailNetworkFailureDoesNotPublishAPartialPage()
        {
            using var http = new HttpClient(new StubHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Post
                ? jsonResponse(searchResponse(0, 17, 18))
                : request.RequestUri!.Query == "?0=17"
                    ? jsonResponse(detailResponse(17, chart(23)))
                    : jsonResponse("Unavailable", HttpStatusCode.ServiceUnavailable))));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
        }

        [Test]
        public async Task TestOnlyFourDetailsAreRequestedAtOnceAndCancellationStopsThePage()
        {
            int requests = 0;
            int active = 0;
            var fourStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var http = new HttpClient(new StubHandler(async (request, token) =>
            {
                if (request.Method == HttpMethod.Post)
                    return jsonResponse(searchResponse(0, Enumerable.Range(1, 20).ToArray()));

                Interlocked.Increment(ref requests);

                if (Interlocked.Increment(ref active) == 4)
                    fourStarted.TrySetResult(true);

                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return jsonResponse("{}");
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }));
            using var client = new SayobotClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<SayobotSearchResult> pending = client.SearchAsync(new SayobotSearchQuery(""), 0, cancellation.Token);
            await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(Volatile.Read(ref requests), Is.EqualTo(4));
            Assert.That(Volatile.Read(ref active), Is.EqualTo(4));
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
            Assert.That(Volatile.Read(ref requests), Is.EqualTo(4));
            Assert.That(Volatile.Read(ref active), Is.Zero);
        }

        [Test]
        public async Task TestCancellationDuringBodyReadReleasesTheResponse()
        {
            var stream = new HangingResponseStream();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            })));
            using var client = new SayobotClient(http);
            using var cancellation = new CancellationTokenSource();

            Task<SayobotBeatmapSet> pending = client.GetBeatmapSetAsync(17, cancellation.Token);
            await stream.Started.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => pending);
            Assert.That(stream.Released, Is.True);
        }

        [Test]
        public async Task TestTheRequestDeadlineAlsoBoundsBodyReads()
        {
            var stream = new HangingResponseStream();
            using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            }))) { Timeout = TimeSpan.FromMilliseconds(500) };
            using var client = new SayobotClient(http);

            Task<SayobotBeatmapSet> pending = client.GetBeatmapSetAsync(17, CancellationToken.None);
            await stream.Started.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.ThrowsAsync<HttpRequestException>(() => pending);
            Assert.That(stream.Released, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestTheJsonByteLimitIsEnforcedWithAndWithoutAContentLength(bool declaredLength)
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                HttpContent content;

                if (declaredLength)
                {
                    content = new StringContent("{}");
                    content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
                }
                else
                    content = new StreamContent(new NonSeekableMemoryStream(Encoding.UTF8.GetBytes(new string(' ', 8 * 1024 * 1024 + 1))));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [Test]
        public void TestOverDeepJsonIsRejectedBeforePublishingMetadata()
        {
            string response = "{\"status\":0,\"nested\":" + new string('[', 32) + "0" + new string(']', 32) + "}";
            using var http = httpClientFor(response);
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
        }

        [Test]
        public void TestMetadataRedirectsAreFailures()
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                HttpResponseMessage response = jsonResponse("Redirect", HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://external.example/catalogue");
                return Task.FromResult(response);
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, CancellationToken.None));
            Assert.ThrowsAsync<HttpRequestException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [Test]
        public void TestAnInjectedRedirectingClientCannotPublishAnotherSitesMetadata()
        {
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                HttpResponseMessage response = jsonResponse(detailResponse(17, chart(23)));
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://external.example/catalogue");
                return Task.FromResult(response);
            }));
            using var client = new SayobotClient(http);

            Assert.ThrowsAsync<InvalidDataException>(() => client.GetBeatmapSetAsync(17, CancellationToken.None));
        }

        [Test]
        public void TestInvalidQueriesAndPreCancellationDoNotStartRequests()
        {
            int requests = 0;
            using var http = new HttpClient(new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(jsonResponse("{}"));
            }));
            using var client = new SayobotClient(http);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery(""), -1, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery("", 0), 0, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery("", MinStars: double.NaN), 0, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery("", MaxStars: double.PositiveInfinity), 0, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery("", MinStars: 3, MaxStars: 2), 0, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SearchAsync(new SayobotSearchQuery("", Category: (SayobotCategory)3), 0, CancellationToken.None));
            Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync(new SayobotSearchQuery("9999999999"), 0, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetBeatmapSetAsync(0, CancellationToken.None));
            Assert.CatchAsync<OperationCanceledException>(() => client.SearchAsync(new SayobotSearchQuery(""), 0, cancellation.Token));
            Assert.That(requests, Is.Zero);
        }

        private static string searchResponse(int nextOffset, params int[] ids) => JsonSerializer.Serialize(new
        {
            status = 0,
            endid = nextOffset,
            data = ids.Select(id => new { sid = id }).ToArray()
        });

        private static string detailResponse(int id, string charts, int approval = 1) => "{\"status\":0,\"data\":{\"sid\":" + id.ToString(CultureInfo.InvariantCulture)
            + ",\"approved\":" + approval.ToString(CultureInfo.InvariantCulture)
            + ",\"title\":\"Actual song title\",\"artist\":\"Artist\",\"creator\":\"Mapper\",\"bid_data\":[" + charts + "]}}";

        private static string chart(int id, string name = "Easy", double keys = 4, double stars = 2, int mode = 3) => JsonSerializer.Serialize(new
        {
            bid = id,
            version = name,
            CS = keys,
            star = stars,
            mode
        });

        private static HttpClient httpClientFor(string response) => new HttpClient(new StubHandler((_, _) => Task.FromResult(jsonResponse(response))));

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
