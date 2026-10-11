// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Online.IR;

namespace osu.Game.Tests.Online.IR
{
    [TestFixture]
    public class OmsNativeServiceTests
    {
        private const string origin = "https://oms.zdamexy.work/";
        private const string password = "OMS-native-test-password!";
        private const string chart_md5 = "0123456789abcdef0123456789abcdef";
        private const int content_budget = 256 * 1024;
        private static readonly TimeSpan test_timeout = TimeSpan.FromSeconds(5);
        private TemporaryNativeStorage storage = null!;
        private Mock<IOmsIrCredentialStore> credentials = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("oms-native-service-" + Guid.NewGuid().ToString("N"));
            credentials = new Mock<IOmsIrCredentialStore>();
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [TestCase("/", OmsPageKind.Home)]
        [TestCase("/rankings", OmsPageKind.Rankings)]
        [TestCase("/users/9223372036854775807", OmsPageKind.Profile)]
        [TestCase("/community", OmsPageKind.Community)]
        [TestCase("/community/9223372036854775807", OmsPageKind.Topic)]
        [TestCase("/news", OmsPageKind.News)]
        [TestCase("/news/oms-ir_trial-1", OmsPageKind.Article)]
        [TestCase("/help#ir", OmsPageKind.Help)]
        [TestCase("/ir", OmsPageKind.Charts)]
        [TestCase("/beatmapsets", OmsPageKind.Library)]
        [TestCase("/account", OmsPageKind.Account)]
        public void ParsesNativePagesFromRelativeAndOfficialWebsiteLinks(string path, OmsPageKind kind)
        {
            Assert.That(OmsPageRoute.TryParse(origin, path, out OmsPageRoute relative), Is.True);
            Assert.That(OmsPageRoute.TryParse(origin, new Uri(new Uri(origin), path).AbsoluteUri, out OmsPageRoute absolute), Is.True);
            Assert.That(relative.Kind, Is.EqualTo(kind));
            Assert.That(absolute, Is.EqualTo(relative));
            if (kind == OmsPageKind.Profile) Assert.That(relative.UserId, Is.EqualTo(long.MaxValue));
            if (kind == OmsPageKind.Topic) Assert.That(relative.PostId, Is.EqualTo(long.MaxValue));
            if (kind == OmsPageKind.Article) Assert.That(relative.Slug, Is.EqualTo("oms-ir_trial-1"));
            if (kind == OmsPageKind.Help) Assert.That(relative.HelpAnchor, Is.EqualTo("ir"));
        }

        [TestCase("https://other.example.test/community")]
        [TestCase("https://oms.zdamexy.work.evil.example.test/help")]
        [TestCase("http://oms.zdamexy.work/help")]
        [TestCase("https://oms.zdamexy.work:8443/help")]
        [TestCase("https://user:password@oms.zdamexy.work/help")]
        [TestCase("//other.example.test/help")]
        [TestCase("mailto:player@example.test")]
        [TestCase("javascript:alert(1)")]
        [TestCase("/download")]
        [TestCase("/users/0")]
        [TestCase("/users/-1")]
        [TestCase("/users/9223372036854775808")]
        [TestCase("/community/0")]
        [TestCase("/community/17/edit")]
        [TestCase("/news/invalid.slug")]
        [TestCase("/rankings?ruleset=osu")]
        [TestCase("/rankings?page=0")]
        [TestCase("/rankings?page=1000001")]
        [TestCase("/rankings?page=2147483648")]
        [TestCase("/rankings?page=1&page=2")]
        [TestCase("/rankings?%70age=1&page=2")]
        [TestCase("/beatmaps?md5=not-a-chart")]
        public void LeavesExternalAndInvalidNativeLinksToTheCaller(string url) =>
            Assert.That(OmsPageRoute.TryParse(origin, url, out _), Is.False);

        [Test]
        public void RejectsOversizedNavigationInput() =>
            Assert.That(OmsPageRoute.TryParse(origin, "/community?q=" + new string('a', 4096), out _), Is.False);

        [TestCase(OmsPageKind.Profile)]
        [TestCase(OmsPageKind.Rankings)]
        public void PlayerShareLinksRetainTheSelectedPublicScope(OmsPageKind kind)
        {
            var original = new OmsPageRoute
            {
                Kind = kind,
                UserId = 42,
                Ruleset = "bms",
                Keymode = "bms_7k",
                Sources = "oms",
                Condition = "known-condition",
                Metric = "coverage",
                Page = 3,
                ScopePage = kind == OmsPageKind.Rankings ? 2 : 1,
                Recent = kind == OmsPageKind.Profile,
            };
            Assert.That(OmsPageRoute.TryParse(origin, original.WebsiteUrl(origin), out OmsPageRoute parsed), Is.True);
            Assert.That(parsed.Kind, Is.EqualTo(kind));
            Assert.That(parsed.UserId, Is.EqualTo(kind == OmsPageKind.Profile ? 42 : 0));
            Assert.That(parsed.Ruleset, Is.EqualTo(original.Ruleset));
            Assert.That(parsed.Keymode, Is.EqualTo(original.Keymode));
            Assert.That(parsed.Sources, Is.EqualTo(original.Sources));
            Assert.That(parsed.Condition, Is.EqualTo(original.Condition));
            Assert.That(parsed.Metric, Is.EqualTo(original.Metric));
            Assert.That(parsed.Page, Is.EqualTo(3));
            Assert.That(parsed.ScopePage, Is.EqualTo(original.ScopePage));
            Assert.That(parsed.Recent, Is.EqualTo(original.Recent));
            Assert.That(original.WebsiteUrl(origin), Does.Contain(kind == OmsPageKind.Rankings ? "source=oms" : "section=recent"));
        }

        [TestCase(OmsPageKind.Community)]
        [TestCase(OmsPageKind.Charts)]
        [TestCase(OmsPageKind.Library)]
        public void SearchShareLinksRetainLiteralQueryTextAndPagination(OmsPageKind kind)
        {
            var original = new OmsPageRoute { Kind = kind, Category = "development", Ruleset = "mania", Query = "中文 + 50% & samples", Page = 2 };
            Assert.That(OmsPageRoute.TryParse(origin, original.WebsiteUrl(origin), out OmsPageRoute parsed), Is.True);
            Assert.That(parsed.Kind, Is.EqualTo(kind));
            Assert.That(parsed.Query, Is.EqualTo(original.Query));
            Assert.That(parsed.Page, Is.EqualTo(2));
            if (kind == OmsPageKind.Community) Assert.That(parsed.Category, Is.EqualTo("development"));
            else Assert.That(parsed.Ruleset, Is.EqualTo("mania"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("table-b,table-a")]
        public void BmsBoardSharePreservesUnselectedAndExplicitlyEmptySources(string? sources)
        {
            var original = new OmsPageRoute
            {
                Kind = OmsPageKind.Board,
                Md5 = chart_md5.ToUpperInvariant(),
                Ruleset = "bms",
                Mode = "comparable",
                Condition = "known-condition",
                Sources = sources,
                Page = 4,
            };
            string url = original.WebsiteUrl(origin);
            Assert.That(OmsPageRoute.TryParse(origin, url, out OmsPageRoute parsed), Is.True);
            Assert.That(parsed.Kind, Is.EqualTo(OmsPageKind.Chart));
            Assert.That(parsed.Md5, Is.EqualTo(chart_md5));
            Assert.That(parsed.Mode, Is.EqualTo("comparable"));
            Assert.That(parsed.Condition, Is.EqualTo(original.Condition));
            Assert.That(parsed.Sources, Is.EqualTo(sources?.Length > 0 ? "table-a,table-b" : sources));
            Assert.That(parsed.Page, Is.EqualTo(4));
            Assert.That(url.Contains("sources=", StringComparison.Ordinal), Is.EqualTo(sources != null));
        }

        [Test]
        public void ManiaBoardShareRetainsOriginalGroupAndHelpRetainsItsSection()
        {
            var board = new OmsPageRoute { Kind = OmsPageKind.Board, Md5 = chart_md5, Ruleset = "mania", Group = "original+4k", Page = 2 };
            Assert.That(OmsPageRoute.TryParse(origin, board.WebsiteUrl(origin), out OmsPageRoute parsed), Is.True);
            Assert.That(parsed.Ruleset, Is.EqualTo("mania"));
            Assert.That(parsed.Group, Is.EqualTo(board.Group));
            Assert.That(parsed.Page, Is.EqualTo(2));
            var help = new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "ir" };
            Assert.That(OmsPageRoute.TryParse(origin, help.WebsiteUrl(origin), out OmsPageRoute parsedHelp), Is.True);
            Assert.That(parsedHelp.HelpAnchor, Is.EqualTo("ir"));
        }

        [Test]
        public void StructuredContentAcceptsPlainTextAndTheCurrentVersion() =>
            Assert.DoesNotThrow(() => OmsClientContent.Validate(content()));

        [TestCase("null")]
        [TestCase("0")]
        [TestCase("2")]
        [TestCase("\"1\"")]
        [TestCase("1.0")]
        [TestCase("2147483648")]
        [TestCase("9223372036854775808")]
        public void RejectsUnsupportedVersionWithoutLeakingNumericConversionErrors(string version)
        {
            JObject document = content();
            document["version"] = JToken.Parse(version);
            assertContentFailure(document);
        }

        [TestCase("/help#ir", true)]
        [TestCase("/beatmapsets?ruleset=mania", true)]
        [TestCase("https://github.com/zdamexy/oms/releases", true)]
        [TestCase("https://oms.zdamexy.work/help#ir", true)]
        [TestCase("http://other.example.test/", false)]
        [TestCase("//other.example.test/", false)]
        [TestCase("/\\other.example.test/", false)]
        [TestCase("https://user:password@other.example.test/", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("data:text/html,hello", false)]
        [TestCase("file:///F:/test.txt", false)]
        public void StructuredContentRestrictsLinksBeforeTheyReachTheNativePage(string href, bool valid)
        {
            JObject document = content();
            document.SelectToken("help[0].links[0].href")!.Replace(href);
            if (valid) Assert.DoesNotThrow(() => OmsClientContent.Validate(document));
            else assertContentFailure(document);
        }

        [TestCase("news[0].slug", 100)]
        [TestCase("news[0].title", 300)]
        [TestCase("news[0].published_at", 40)]
        [TestCase("news[0].excerpt", 2000)]
        [TestCase("help[0].id", 100)]
        [TestCase("help[0].heading", 300)]
        [TestCase("help[0].text", 12000)]
        [TestCase("help[0].links[0].label", 300)]
        public void StructuredTextAcceptsItsBoundaryAndRejectsOneMoreCharacter(string field, int limit)
        {
            JObject document = content();
            document.SelectToken(field)!.Replace(new string('a', limit));
            Assert.DoesNotThrow(() => OmsClientContent.Validate(document));
            document.SelectToken(field)!.Replace(new string('a', limit + 1));
            assertContentFailure(document);
        }

        [Test]
        public void StructuredLinkAcceptsItsLengthBoundaryAndRejectsOneMoreCharacter()
        {
            JObject document = content();
            document.SelectToken("help[0].links[0].href")!.Replace("/" + new string('a', 4095));
            Assert.DoesNotThrow(() => OmsClientContent.Validate(document));
            document.SelectToken("help[0].links[0].href")!.Replace("/" + new string('a', 4096));
            assertContentFailure(document);
        }

        [TestCase("news", 32)]
        [TestCase("news[0].body", 32)]
        [TestCase("help", 32)]
        [TestCase("help[0].links", 16)]
        public void StructuredCollectionsAreBounded(string field, int limit)
        {
            JObject document = content();
            var array = (JArray)document.SelectToken(field)!;
            JToken template = array[0].DeepClone();
            for (int i = 1; i < limit; i++)
            {
                JToken copy = template.DeepClone();
                if (field == "help") copy["id"] = "help-" + i;
                if (field == "news") copy["slug"] = "news-" + i;
                array.Add(copy);
            }
            Assert.DoesNotThrow(() => OmsClientContent.Validate(document));
            array.Add(template.DeepClone());
            assertContentFailure(document);
        }

        [TestCase("news[0].title", "")]
        [TestCase("news[0].slug", "has/slash")]
        [TestCase("help[0].id", "bad.id")]
        [TestCase("help[0].text", "<script>alert(1)</script>")]
        [TestCase("news[0].body[0].text", "Text <b>markup</b>")]
        public void StructuredContentRejectsUnusableIdentityOrMarkup(string field, string value)
        {
            JObject document = content();
            document.SelectToken(field)!.Replace(value);
            assertContentFailure(document);
        }

        [Test]
        public void HelpSectionsMustHaveDistinctIdentities()
        {
            JObject document = content();
            ((JArray)document["help"]!).Add(document["help"]![0]!.DeepClone());
            assertContentFailure(document);
        }

        [Test]
        public async Task LoggedInPublicContentAndCommunityReadsCarryNoAccountCredentials()
        {
            var publicPaths = new List<string>();
            using var http = loginHttp((request, _) =>
            {
                publicPaths.Add(request.RequestUri!.AbsolutePath);
                assertPublicRequest(request);
                return Task.FromResult(json(request.RequestUri.AbsolutePath == "/client-content" ? content() : new JObject { ["items"] = new JArray() }));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            await service.GetClientContentAsync();
            await service.GetCommunityAsync("posts?page=1&limit=20");
            Assert.That(publicPaths, Is.EqualTo(new[] { "/client-content", "/api/ir/v1/community/posts" }));
            Assert.That(service.State.Account?.Id, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions, Is.Empty);
        }

        [Test]
        public async Task ConditionalContentReadsKeepEtagsAndReturnIndependentDocuments()
        {
            int reads = 0;
            using var http = mockHttp((request, _) =>
            {
                assertPublicRequest(request);
                int attempt = ++reads;
                if (attempt == 1)
                {
                    Assert.That(request.Headers.IfNoneMatch, Is.Empty);
                    return Task.FromResult(contentResponse("\"content-one\""));
                }
                Assert.That(request.Headers.IfNoneMatch.Single().Tag, Is.EqualTo(attempt == 4 ? "\"content-two\"" : "\"content-one\""));
                if (attempt == 3)
                {
                    JObject updated = content();
                    updated.SelectToken("news[0].title")!.Replace("Updated title");
                    return Task.FromResult(contentResponse("\"content-two\"", updated));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            JObject first = await service.GetClientContentAsync();
            first.SelectToken("news[0].title")!.Replace("Caller mutation");
            JObject second = await service.GetClientContentAsync();
            Assert.That(second.SelectToken("news[0].title")!.Value<string>(), Is.EqualTo("OMS update"));
            second.SelectToken("help[0].text")!.Replace("Another caller mutation");
            JObject third = await service.GetClientContentAsync();
            JObject fourth = await service.GetClientContentAsync();
            Assert.That(third.SelectToken("news[0].title")!.Value<string>(), Is.EqualTo("Updated title"));
            Assert.That(fourth.SelectToken("help[0].text")!.Value<string>(), Is.EqualTo("打开 OMS\n阅读帮助"));
            Assert.That(reads, Is.EqualTo(4));
        }

        [Test]
        public async Task FreshPublicContentIsReusedWithoutAnotherHttpRequest()
        {
            int reads = 0;
            using var http = mockHttp((_, _) =>
            {
                reads++;
                HttpResponseMessage response = contentResponse("\"fresh-content\"");
                response.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromSeconds(300) };
                return Task.FromResult(response);
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            JObject first = await service.GetClientContentAsync();
            first["help"] = new JArray();
            JObject second = await service.GetClientContentAsync();
            Assert.That(((JArray)second["help"]!).Count, Is.EqualTo(1));
            Assert.That(reads, Is.EqualTo(1));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public async Task NoCacheNoStoreAndZeroAgeRequireConditionalHttpValidation(bool noCache, bool noStore)
        {
            int reads = 0;
            using var http = mockHttp((request, _) =>
            {
                if (++reads == 1)
                {
                    HttpResponseMessage response = contentResponse("\"must-validate\"");
                    response.Headers.CacheControl = new CacheControlHeaderValue
                    {
                        MaxAge = TimeSpan.FromSeconds(noCache || noStore ? 300 : 0),
                        NoCache = noCache,
                        NoStore = noStore,
                    };
                    return Task.FromResult(response);
                }
                Assert.That(request.Headers.IfNoneMatch.Single().Tag, Is.EqualTo("\"must-validate\""));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.GetClientContentAsync();
            await service.GetClientContentAsync();
            Assert.That(reads, Is.EqualTo(2));
        }

        [TestCase(content_budget, true)]
        [TestCase(content_budget, false)]
        [TestCase(content_budget + 1, true)]
        [TestCase(content_budget + 1, false)]
        public async Task ContentBudgetCountsUtf8BytesWithAndWithoutContentLength(int size, bool advertisedLength)
        {
            byte[] payload = paddedContent(size);
            using var http = mockHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = advertisedLength ? new ByteArrayContent(payload) : unadvertisedContent(payload),
            }));
            using var service = new OmsIrService(storage, http, credentials.Object);
            if (size <= content_budget)
                Assert.That((await service.GetClientContentAsync())["version"]!.Value<int>(), Is.EqualTo(1));
            else
                await expectFailure(service.GetClientContentAsync(), "invalid_response");
        }

        [TestCase("{\"version\":1,\"version\":1,\"news\":[],\"help\":[]}")]
        [TestCase("{\"version\":1,\"news\":[],\"help\":[]}{\"extra\":true}")]
        [TestCase("[]")]
        [TestCase("{broken-json")]
        public async Task InvalidContentJsonFailsBeforeItCanBeCached(string payload)
        {
            int reads = 0;
            using var http = mockHttp((request, _) =>
            {
                if (++reads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
                Assert.That(request.Headers.IfNoneMatch, Is.Empty);
                return Task.FromResult(contentResponse("\"valid-after-error\""));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await expectFailure(service.GetClientContentAsync(), "invalid_response");
            Assert.That((await service.GetClientContentAsync())["version"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(reads, Is.EqualTo(2));
        }

        [Test]
        public async Task ContentRejectsInvalidUtf8AndUnsupportedVersions()
        {
            int reads = 0;
            using var http = mockHttp((_, _) =>
            {
                if (++reads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 0xc3, 0x28 }) });
                JObject document = content();
                document["version"] = 2;
                return Task.FromResult(json(document));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await expectFailure(service.GetClientContentAsync(), "invalid_response");
            await expectFailure(service.GetClientContentAsync(), "invalid_content");
        }

        [TestCase(HttpStatusCode.NotModified)]
        [TestCase(HttpStatusCode.TemporaryRedirect)]
        public async Task Uncached304AndContentRedirectsCannotCreateAUsablePage(HttpStatusCode status)
        {
            int reads = 0;
            using var http = mockHttp((request, _) =>
            {
                reads++;
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo(origin + "client-content"));
                assertPublicRequest(request);
                var response = new HttpResponseMessage(status);
                response.Headers.Location = new Uri("https://other.example.test/client-content");
                return Task.FromResult(response);
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await expectFailure(service.GetClientContentAsync(), "redirect_rejected");
            Assert.That(reads, Is.EqualTo(1));
        }

        [TestCase("posts")]
        [TestCase("posts/17/edit")]
        [TestCase("posts/17/delete")]
        [TestCase("posts/17/replies")]
        [TestCase("replies/23/edit")]
        [TestCase("replies/23/delete")]
        public async Task CommunityWritesUseTheOriginalDesktopBearerAndAcceptMatchingReceipts(string path)
        {
            JObject body = communityBody(path);
            string expectedBody = body.ToString(Formatting.None);
            int writes = 0;
            using var http = loginHttp(async (request, token) =>
            {
                writes++;
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo(origin + "api/ir/v1/community/" + path));
                assertDesktopWrite(request, accessFor(1));
                Assert.That(await request.Content!.ReadAsStringAsync(token), Is.EqualTo(expectedBody));
                return json(communityReceipt(path));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            JObject receipt = await service.PostCommunityAsync(path, body, 1);
            Assert.That(receipt.SelectToken(path.StartsWith("replies", StringComparison.Ordinal) || path.EndsWith("/replies", StringComparison.Ordinal) ? "reply.author.id" : "post.author.id")!.Value<long>(), Is.EqualTo(1));
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions, Is.Empty);
        }

        [Test]
        public async Task Community401RefreshesOnceAndResendsExactlyTheSameBodyAndUuid()
        {
            const string rotated_access = "rotated-native-access-0000000000000000";
            var attempts = new List<(string Body, string? Bearer)>();
            int refreshes = 0;
            JObject body = communityBody("posts");
            using var http = loginHttp(async (request, token) =>
            {
                attempts.Add((await request.Content!.ReadAsStringAsync(token), request.Headers.Authorization?.Parameter));
                return attempts.Count == 1 ? error(HttpStatusCode.Unauthorized, "invalid_session") : json(communityReceipt("posts"));
            }, async (request, token) =>
            {
                refreshes++;
                Assert.That(request.Headers.Authorization, Is.Null);
                Assert.That(JObject.Parse(await request.Content!.ReadAsStringAsync(token))["refresh_token"]!.Value<string>(), Is.EqualTo(refreshFor(1)));
                JObject refreshed = loginResponse(1);
                refreshed["access_token"] = rotated_access;
                refreshed["refresh_token"] = "rotated-native-refresh-0000000000000000";
                return json(refreshed);
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            await service.PostCommunityAsync("posts", body, 1);
            Assert.That(refreshes, Is.EqualTo(1));
            Assert.That(attempts.Select(a => a.Bearer), Is.EqualTo(new[] { accessFor(1), rotated_access }));
            Assert.That(attempts.Select(a => a.Body), Is.EqualTo(new[] { body.ToString(Formatting.None), body.ToString(Formatting.None) }));
            credentials.Verify(c => c.Write(It.IsAny<string>(), It.Is<OmsIrSession>(s => s.UserId == 1 && s.AccessToken == rotated_access)), Times.Once());
            Assert.That(service.State.Account?.Id, Is.EqualTo(1));
        }

        [Test]
        public async Task CommunityRefreshToAnotherAccountStopsBeforeResendingTheDraft()
        {
            int writes = 0;
            using var http = loginHttp((_, _) =>
            {
                writes++;
                return Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_session"));
            }, (_, _) => Task.FromResult(json(loginResponse(2))));
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            await expectFailure(service.PostCommunityAsync("posts", communityBody("posts"), 1), "session_identity_changed");
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(service.State.RequiresLogin, Is.True);
            Assert.That(service.State.Account, Is.Null);
            credentials.Verify(c => c.Delete(It.IsAny<string>()), Times.Once());
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task MissingOrDifferentCommunityAccountFailsBeforeSendingTheDraft(bool loggedIn)
        {
            int writes = 0;
            using var http = loginHttp((_, _) =>
            {
                writes++;
                return Task.FromResult(json(communityReceipt("posts")));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            if (loggedIn) await service.LoginAsync("player_one", password);
            await expectFailure(service.PostCommunityAsync("posts", communityBody("posts"), loggedIn ? 2 : 1), "account_changed");
            Assert.That(writes, Is.Zero);
            Assert.That(service.PendingSubmissions, Is.Empty);
        }

        [Test]
        public async Task QueuedCommunityWriteRechecksTheCapturedAccountBeforeSending()
        {
            var loginStarted = signal();
            var releaseLogin = signal();
            int writes = 0;
            using var http = mockHttp(async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal))
                {
                    string name = JObject.Parse(await request.Content!.ReadAsStringAsync(token))["username"]!.Value<string>()!;
                    if (name == "player_two")
                    {
                        loginStarted.TrySetResult();
                        await releaseLogin.Task.WaitAsync(token);
                    }
                    return json(loginResponse(name == "player_two" ? 2 : 1));
                }
                writes++;
                return json(communityReceipt("posts"));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            Task login = service.LoginAsync("player_two", password);
            try
            {
                await loginStarted.Task.WaitAsync(test_timeout);
                Task<JObject> write = service.PostCommunityAsync("posts", communityBody("posts"), 1);
                Assert.That(writes, Is.Zero);
                releaseLogin.TrySetResult();
                await login.WaitAsync(test_timeout);
                await expectFailure(write, "account_changed");
                Assert.That(service.State.Account?.Id, Is.EqualTo(2));
                Assert.That(writes, Is.Zero);
            }
            finally
            {
                releaseLogin.TrySetResult();
                await login.WaitAsync(test_timeout);
            }
        }

        [Test]
        public async Task WaitingCommunityWriteKeepsTheBodyAndUuidCapturedBeforeItsNetworkTurn()
        {
            var readStarted = signal();
            var releaseRead = signal();
            JObject body = communityBody("posts");
            string originalBody = body.ToString(Formatting.None);
            string? sentBody = null;
            using var http = loginHttp(async (request, token) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    readStarted.TrySetResult();
                    await releaseRead.Task.WaitAsync(token);
                    return json(new JObject { ["items"] = new JArray() });
                }
                sentBody = await request.Content!.ReadAsStringAsync(token);
                return json(communityReceipt("posts"));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            Task<JObject> read = service.GetCommunityAsync("posts");
            try
            {
                await readStarted.Task.WaitAsync(test_timeout);
                Task<JObject> write = service.PostCommunityAsync("posts", body, 1);
                body["body"] = "Edited while waiting";
                body["submission_id"] = Guid.NewGuid().ToString();
                releaseRead.TrySetResult();
                await read.WaitAsync(test_timeout);
                await write.WaitAsync(test_timeout);
                Assert.That(sentBody, Is.EqualTo(originalBody));
            }
            finally
            {
                releaseRead.TrySetResult();
                await read.WaitAsync(test_timeout);
            }
        }

        [TestCase("posts", "post.author.id", "2")]
        [TestCase("posts", "post.author.id", "0")]
        [TestCase("posts", "post.author.id", "\"1\"")]
        [TestCase("posts", "post.id", "0")]
        [TestCase("posts", "post.id", "9223372036854775808")]
        [TestCase("posts", "post", "null")]
        [TestCase("posts/17/edit", "post.id", "18")]
        [TestCase("posts/17/delete", "post.id", "18")]
        [TestCase("posts/17/replies", "reply.author.id", "2")]
        [TestCase("posts/17/replies", "reply.id", "0")]
        [TestCase("posts/17/replies", "reply.post_id", "18")]
        [TestCase("posts/17/replies", "reply.post_id", "\"17\"")]
        [TestCase("replies/23/edit", "reply.id", "24")]
        [TestCase("replies/23/edit", "reply.post_id", "0")]
        [TestCase("replies/23/delete", "reply.id", "24")]
        [TestCase("replies/23/delete", "reply.author.id", "2")]
        public async Task WrongCommunityReceiptCannotReportSuccessOrChangeTheRetryBody(string path, string field, string value)
        {
            JObject badReceipt = communityReceipt(path);
            badReceipt.SelectToken(field)!.Replace(JToken.Parse(value));
            JObject body = communityBody(path);
            var sentBodies = new List<string>();
            using var http = loginHttp(async (request, token) =>
            {
                sentBodies.Add(await request.Content!.ReadAsStringAsync(token));
                return json(sentBodies.Count == 1 ? badReceipt : communityReceipt(path));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            await expectFailure(service.PostCommunityAsync(path, body, 1), "invalid_receipt");
            Assert.That(sentBodies.Count, Is.EqualTo(1));
            Assert.That(service.State.Account?.Id, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions, Is.Empty);
            await service.PostCommunityAsync(path, body, 1);
            Assert.That(sentBodies, Is.EqualTo(new[] { body.ToString(Formatting.None), body.ToString(Formatting.None) }));
            credentials.Verify(c => c.Delete(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public async Task CommunityRateLimitIsReportedWithoutBackgroundResubmission()
        {
            int writes = 0;
            using var http = loginHttp((_, _) =>
            {
                writes++;
                HttpResponseMessage response = error(HttpStatusCode.TooManyRequests, "rate_limited");
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
                return Task.FromResult(response);
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            OmsIrException failure = await expectFailure(service.PostCommunityAsync("posts", communityBody("posts"), 1), "rate_limited");
            Assert.That(failure.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(failure.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(120)));
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions, Is.Empty);
        }

        [Test]
        public async Task CancellingACommunityWriteReleasesTheNetworkTurnAndPreservesItsManualRetryUuid()
        {
            var writeStarted = signal();
            var sentBodies = new List<string>();
            using var cancellation = new CancellationTokenSource();
            using var http = loginHttp(async (request, token) =>
            {
                sentBodies.Add(await request.Content!.ReadAsStringAsync(token));
                if (sentBodies.Count == 1)
                {
                    writeStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                return json(communityReceipt("posts"));
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            await service.LoginAsync("player_one", password);
            JObject body = communityBody("posts");
            Task<JObject> write = service.PostCommunityAsync("posts", body, 1, cancellation.Token);
            try
            {
                await writeStarted.Task.WaitAsync(test_timeout);
                cancellation.Cancel();
                await expectCancellation(write);
                Assert.That(service.State.Busy, Is.False);
                Assert.That(service.State.Account?.Id, Is.EqualTo(1));
                await service.PostCommunityAsync("posts", body, 1).WaitAsync(test_timeout);
                Assert.That(sentBodies, Is.EqualTo(new[] { body.ToString(Formatting.None), body.ToString(Formatting.None) }));
            }
            finally
            {
                cancellation.Cancel();
                await expectCancellation(write);
            }
        }

        [Test]
        public async Task CancellingActiveContentDoesNotCacheAResponseOrPreventTheNextRead()
        {
            var readStarted = signal();
            int reads = 0;
            using var cancellation = new CancellationTokenSource();
            using var http = mockHttp(async (request, token) =>
            {
                if (++reads == 1)
                {
                    readStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                Assert.That(request.Headers.IfNoneMatch, Is.Empty);
                return contentResponse("\"after-cancellation\"");
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            Task<JObject> read = service.GetClientContentAsync(cancellation.Token);
            try
            {
                await readStarted.Task.WaitAsync(test_timeout);
                cancellation.Cancel();
                await expectCancellation(read);
                Assert.That(service.State.Busy, Is.False);
                Assert.That((await service.GetClientContentAsync().WaitAsync(test_timeout))["version"]!.Value<int>(), Is.EqualTo(1));
                Assert.That(reads, Is.EqualTo(2));
            }
            finally
            {
                cancellation.Cancel();
                await expectCancellation(read);
            }
        }

        [Test]
        public async Task CancellingQueuedContentDoesNotSendHttpOrReleaseAnotherRequestsTurn()
        {
            var readStarted = signal();
            var releaseRead = signal();
            int contentReads = 0;
            using var cancellation = new CancellationTokenSource();
            using var http = mockHttp(async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath != "/client-content")
                {
                    readStarted.TrySetResult();
                    await releaseRead.Task.WaitAsync(token);
                    return json(new JObject { ["items"] = new JArray() });
                }
                contentReads++;
                return contentResponse("\"after-queued-cancellation\"");
            });
            using var service = new OmsIrService(storage, http, credentials.Object);
            Task<JObject> heldRead = service.GetCommunityAsync("posts");
            try
            {
                await readStarted.Task.WaitAsync(test_timeout);
                Task<JObject> queuedRead = service.GetClientContentAsync(cancellation.Token);
                cancellation.Cancel();
                await expectCancellation(queuedRead);
                Assert.That(contentReads, Is.Zero);
                Assert.That(service.State.Busy, Is.True);
                releaseRead.TrySetResult();
                await heldRead.WaitAsync(test_timeout);
                await service.GetClientContentAsync().WaitAsync(test_timeout);
                Assert.That(contentReads, Is.EqualTo(1));
                Assert.That(service.State.Busy, Is.False);
            }
            finally
            {
                cancellation.Cancel();
                releaseRead.TrySetResult();
                await heldRead.WaitAsync(test_timeout);
            }
        }

        private static TaskCompletionSource signal() => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private static HttpClient mockHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                   .Returns((HttpRequestMessage request, CancellationToken token) => send(request, token));
            return new HttpClient(handler.Object);
        }

        private static HttpClient loginHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
                                            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? refresh = null) =>
            mockHttp(async (request, token) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                {
                    JObject payload = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
                    Assert.That(payload["transport"]!.Value<string>(), Is.EqualTo("desktop"));
                    return json(loginResponse(payload["username"]!.Value<string>() == "player_two" ? 2 : 1));
                }
                if (path.EndsWith("/auth/refresh", StringComparison.Ordinal))
                {
                    Assert.That(refresh, Is.Not.Null, "This scenario must not refresh unexpectedly.");
                    return await refresh!(request, token);
                }
                return await send(request, token);
            });

        private static void assertPublicRequest(HttpRequestMessage request)
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Headers.Authorization, Is.Null);
            Assert.That(request.Headers.Contains("Cookie"), Is.False);
            Assert.That(request.Headers.Contains("Origin"), Is.False);
            Assert.That(request.Headers.Contains("X-OMS-Actor"), Is.False);
            Assert.That(request.Headers.Accept.Select(value => value.MediaType), Contains.Item("application/json"));
        }

        private static void assertDesktopWrite(HttpRequestMessage request, string access)
        {
            Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo(access));
            Assert.That(request.Headers.Contains("Cookie"), Is.False);
            Assert.That(request.Headers.Contains("Origin"), Is.False);
            Assert.That(request.Headers.Contains("X-OMS-Actor"), Is.False);
            Assert.That(request.Content!.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        }

        private static JObject content() => new JObject
        {
            ["version"] = 1,
            ["news"] = new JArray(new JObject
            {
                ["slug"] = "ir-trial",
                ["title"] = "OMS update",
                ["published_at"] = "2026-10-11",
                ["excerpt"] = "Trial update",
                ["body"] = new JArray(new JObject { ["heading"] = "OMS IR", ["text"] = "First line\nSecond line" }),
            }),
            ["help"] = new JArray(new JObject
            {
                ["id"] = "ir",
                ["heading"] = "Getting started",
                ["text"] = "打开 OMS\n阅读帮助",
                ["links"] = new JArray(new JObject { ["label"] = "Help", ["href"] = "/help#ir" }),
            }),
        };

        private static void assertContentFailure(JObject document) =>
            Assert.That(Assert.Throws<OmsIrException>(() => OmsClientContent.Validate(document))!.Code, Is.EqualTo("invalid_content"));

        private static HttpResponseMessage contentResponse(string etag, JObject? document = null)
        {
            HttpResponseMessage response = json(document ?? content());
            response.Headers.ETag = new EntityTagHeaderValue(etag);
            return response;
        }

        private static byte[] paddedContent(int size)
        {
            byte[] document = Encoding.UTF8.GetBytes(content().ToString(Formatting.None));
            byte[] padded = new byte[size];
            Array.Fill(padded, (byte)' ');
            document.CopyTo(padded, 0);
            return padded;
        }

        private static HttpContent unadvertisedContent(byte[] payload)
        {
            int position = 0;
            var stream = new Mock<Stream>();
            stream.SetupGet(value => value.CanRead).Returns(true);
            stream.SetupGet(value => value.CanSeek).Returns(false);
            stream.SetupGet(value => value.CanWrite).Returns(false);
            stream.Setup(value => value.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                  .Returns((Memory<byte> buffer, CancellationToken token) =>
                  {
                      token.ThrowIfCancellationRequested();
                      int count = Math.Min(buffer.Length, payload.Length - position);
                      payload.AsMemory(position, count).CopyTo(buffer);
                      position += count;
                      return ValueTask.FromResult(count);
                  });
            var result = new StreamContent(stream.Object);
            Assert.That(result.Headers.ContentLength, Is.Null);
            return result;
        }

        private static string accessFor(long id) => "access-native-player-" + id + "-0000000000000000";
        private static string refreshFor(long id) => "refresh-native-player-" + id + "-0000000000000000";

        private static JObject loginResponse(long id) => new JObject
        {
            ["user"] = new JObject { ["id"] = id, ["username"] = id == 1 ? "player_one" : "player_two" },
            ["access_token"] = accessFor(id),
            ["refresh_token"] = refreshFor(id),
            ["expires_in"] = 3600,
        };

        private static JObject communityBody(string path)
        {
            var result = new JObject();
            if (path == "posts" || path.EndsWith("/replies", StringComparison.Ordinal)) result["submission_id"] = Guid.NewGuid().ToString();
            if (!path.EndsWith("/delete", StringComparison.Ordinal)) result["body"] = "Original native draft";
            if (path == "posts" || path.StartsWith("posts/", StringComparison.Ordinal) && path.EndsWith("/edit", StringComparison.Ordinal))
            {
                result["title"] = "Native community topic";
                result["category"] = "discussion";
            }
            return result;
        }

        private static JObject communityReceipt(string path)
        {
            bool reply = path.StartsWith("replies/", StringComparison.Ordinal) || path.EndsWith("/replies", StringComparison.Ordinal);
            var item = new JObject
            {
                ["id"] = reply ? 23 : 17,
                ["author"] = new JObject { ["id"] = 1, ["username"] = "player_one" },
                ["body"] = "Original native draft",
            };
            if (reply) item["post_id"] = 17;
            else
            {
                item["title"] = "Native community topic";
                item["category"] = "discussion";
            }
            return new JObject { [reply ? "reply" : "post"] = item, ["duplicate"] = false };
        }

        private static HttpResponseMessage error(HttpStatusCode status, string code) =>
            json(new JObject { ["error"] = new JObject { ["code"] = code, ["message"] = "Synthetic native service failure" } }, status);

        private static HttpResponseMessage json(JObject document, HttpStatusCode status = HttpStatusCode.OK) => new HttpResponseMessage(status)
        {
            Content = new StringContent(document.ToString(Formatting.None), Encoding.UTF8, "application/json"),
        };

        private static async Task<OmsIrException> expectFailure(Task operation, string code)
        {
            try
            {
                await operation.WaitAsync(test_timeout);
            }
            catch (OmsIrException exception)
            {
                Assert.That(exception.Code, Is.EqualTo(code));
                return exception;
            }
            Assert.Fail("Expected native service failure: " + code);
            return null!;
        }

        private static async Task expectCancellation(Task operation)
        {
            try
            {
                await operation.WaitAsync(test_timeout);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Assert.Fail("Caller cancellation must stay cancellation rather than becoming a service error.");
        }
    }
}
