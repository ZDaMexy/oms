// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.News;
using osu.Game.Overlays.News.Sidebar;
using osu.Game.Overlays.Wiki;
using osu.Game.Overlays.Wiki.Markdown;
using osu.Game.Utils;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsNativeContent : OsuManualInputManagerTestScene
    {
        private const string latest_title = "合成公告";
        private const string latest_excerpt = "这是一条真实来源的公告摘要。";
        private const string article_body = "第一段完整公告正文。";
        private const string long_title = "长篇公告";
        private const string long_body = "长篇公告开头。";
        private const string player_body = "播放器专用密钥帮助。";
        private const string ir_body = "固定 OMSIR，直接登录。";
        private const string late_title = "迟到的旧公告";
        private const string late_body = "迟到的旧章节正文。";

        private readonly ConcurrentQueue<RequestSnapshot> requests = new ConcurrentQueue<RequestSnapshot>();
        private readonly ConcurrentQueue<string> legacyRequests = new ConcurrentQueue<string>();
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private DependencyProvidingContainer scope = null!;
        private NewsOverlay news = null!;
        private WikiOverlay wiki = null!;
        private RecordingLinkHandler fallbackLinks = null!;
        private volatile bool holdNextRead;
        private volatile TaskCompletionSource<JObject>? delayedRead;
        private CancellationToken delayedToken;

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUp()
        {
            AddStep("create isolated native news and help", () =>
            {
                delayedRead?.TrySetResult(content(true));
                service?.Dispose();
                http?.Dispose();
                requests.Clear();
                legacyRequests.Clear();
                holdNextRead = false;
                delayedRead = null;
                delayedToken = default;
                fallbackLinks = new RecordingLinkHandler();
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    legacyRequests.Enqueue(request.GetType().Name);
                    return true;
                };
                http = new HttpClient(new Handler(response));
                service = new OmsIrService(LocalStorage.GetStorageForDirectory("oms-native-content-synthetic-" + Guid.NewGuid()), http, new Credentials());
                Child = scope = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(OmsIrService), service),
                        (typeof(ILinkHandler), fallbackLinks),
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Blue)),
                    },
                    Children = new Drawable[]
                    {
                        news = new NewsOverlay(true),
                        wiki = new WikiOverlay(true),
                    },
                };
            });
            AddUntilStep("both native pages loaded and hidden", () => news.IsLoaded && wiki.IsLoaded
                && news.State.Value == Visibility.Hidden && wiki.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestNewsUsesActualNativeCardsMonthsAndArticle()
        {
            AddWaitStep("leave both pages idle", 2);
            AddAssert("hidden startup performs no OMS or legacy request", () => requests.IsEmpty && legacyRequests.IsEmpty);
            AddStep("open the native news list", () => news.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.News }));
            AddUntilStep("actual news cards are ready", () => !isLoading(news) && news.ChildrenOfType<OmsNewsCard>().Count() == 3);
            AddAssert("news retains its fullscreen header and banner", () => news.Width == 0.85f && news.RelativeSizeAxes == Axes.Both
                && news.Header.Title.Title.ToString() == "新闻" && news.Header.ChildrenOfType<OverlayHeaderBackground>().Single().Height == 80);
            AddAssert("actual title excerpt and publication year appear on the newest card", () =>
            {
                OmsNewsCard card = news.ChildrenOfType<OmsNewsCard>().Single(candidate => hasText(candidate, latest_title));
                return hasText(card, latest_title) && hasText(card, latest_excerpt)
                    && card.ChildrenOfType<SpriteText>().Any(sprite => sprite.Text.Equals(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero).ToLocalisedMediumDate()));
            });
            AddAssert("sidebar months come from all three publication dates", () => news.ChildrenOfType<MonthSection>()
                .Select(month => (month.Year, month.Month)).SequenceEqual(new[] { (2026, 10), (2026, 9), (2025, 11) }));
            AddAssert("null image does not manufacture a remote cover", () => !news.ChildrenOfType<NewsPostBackground>().Any());
            AddAssert("native cards replace the generic services button shell", () => !hasText(news, "OMS 在线服务")
                && !news.ChildrenOfType<OsuButton>().Any());
            AddAssert("one anonymous website-content request", () => requests.Count == 1
                && requests.Single().Host == OmsIrService.ServiceOrigin.Host && requests.Single().Path == "/client-content"
                && requests.Single().Method == "GET" && requests.Single().Bearer == null);

            AddStep("read the actual article inside the client", () => news.ShowOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.Article, Slug = "latest-announcement" }));
            AddUntilStep("article body rendered as native markdown", () => ready(news, article_body)
                && news.ChildrenOfType<OmsMarkdownContainer>().Count() == 1);
            AddAssert("article has its title complete paragraphs and actual links", () => news.Header.Current.Value?.ToString() == latest_title
                && hasText(news, "第二段保留原文。") && news.ChildrenOfType<MarkdownParagraph>().Count() >= 4
                && news.ChildrenOfType<OsuMarkdownLinkText>().Count() == 2);
            AddAssert("plain-text markdown-like content stays literal", () => hasText(news, "*保留星号*")
                && hasText(news, "![字面图片]") && !news.ChildrenOfType<OsuMarkdownImage>().Any());
            AddAssert("article identity and website path agree", () => news.CurrentOmsRoute.Slug == "latest-announcement"
                && news.CurrentOmsRoute.WebsiteUrl(service.State.ServiceAddress).EndsWith("/news/latest-announcement", StringComparison.Ordinal));
            AddStep("select the actual older news year", () => news.ShowOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.News, Query = "2025" }));
            AddUntilStep("year selects only its own native card", () => !isLoading(news)
                && news.ChildrenOfType<OmsNewsCard>().Count() == 1 && hasText(news, long_title));
            AddAssert("year also selects the matching sidebar month", () => news.ChildrenOfType<MonthSection>().Single().Year == 2025
                && news.ChildrenOfType<MonthSection>().Single().Month == 11);
            AddAssert("all news navigation shares one HTTP result", () => requests.Count == 1 && legacyRequests.IsEmpty);
        }

        [Test]
        public void TestHelpUsesEightActualWikiPanelsAndStableSectionIdentity()
        {
            AddStep("read news once", () => news.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.News }));
            AddUntilStep("news content cached", () => !isLoading(news) && news.ChildrenOfType<OmsNewsCard>().Count() == 3);
            AddStep("open the actual help home", () =>
            {
                news.Hide();
                wiki.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Help });
            });
            AddUntilStep("eight native wiki panels rendered", () => !isLoading(wiki) && wiki.ChildrenOfType<WikiMainPage>().Count() == 1
                && wiki.ChildrenOfType<WikiPanelContainer>().Count() == 8);
            AddAssert("help retains the native fullscreen banner and compact panels", () => wiki.Width == 0.85f
                && wiki.Header.Title.Title.ToString() == "帮助" && wiki.Header.ChildrenOfType<OverlayHeaderBackground>().Single().Height == 80
                && wiki.ChildrenOfType<WikiPanelContainer>().All(panel => panel.DrawWidth < wiki.DrawWidth));
            AddAssert("all actual section headings appear without a generic services shell", () => helpHeadings.All(heading => hasText(wiki, heading))
                && !hasText(wiki, "OMS 在线服务"));
            AddStep("read the renamed IR section by its stable id", () => wiki.ShowOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.Help, HelpAnchor = "ir" }));
            AddUntilStep("native help body and chapter sidebar rendered", () => ready(wiki, ir_body)
                && wiki.ChildrenOfType<WikiArticlePage>().Count() == 1 && wiki.ChildrenOfType<OmsWikiSidebar>().Count() == 1);
            AddAssert("section identity is independent of title and position", () => wiki.CurrentOmsRoute.HelpAnchor == "ir"
                && wiki.Header.Current.Value?.ToString() == "新版登录说明"
                && wiki.CurrentOmsRoute.WebsiteUrl(service.State.ServiceAddress).EndsWith("/help#ir", StringComparison.Ordinal)
                && !hasText(wiki.ChildrenOfType<WikiArticlePage>().Single(), player_body));
            AddAssert("the native sidebar includes every real chapter", () => wiki.ChildrenOfType<OmsWikiSidebar>().Single().Width == 250
                && helpHeadings.All(heading => hasText(wiki.ChildrenOfType<OmsWikiSidebar>().Single(), heading)));
            AddStep("read another real section", () => wiki.ShowOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.Help, HelpAnchor = "players" }));
            AddUntilStep("the requested player help body appears", () => ready(wiki, player_body)
                && wiki.Header.Current.Value?.ToString() == "其他播放器说明");
            AddAssert("news help home and both chapters use one anonymous HTTP result", () => requests.Count == 1
                && requests.Single().Bearer == null && legacyRequests.IsEmpty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRestoreKeepsArticleAndScrollWithoutNetwork(bool useWiki)
        {
            IOmsPageOverlay page = null!;
            Drawable view = null!;
            float previousScroll = 0;
            OmsPageRoute original = useWiki
                ? new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "ir" }
                : new OmsPageRoute { Kind = OmsPageKind.Article, Slug = "long-announcement" };
            OmsPageRoute next = useWiki
                ? new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "players" }
                : new OmsPageRoute { Kind = OmsPageKind.Article, Slug = "latest-announcement" };
            AddStep("read a real long page", () =>
            {
                page = useWiki ? wiki : news;
                view = useWiki ? wiki : news;
                page.ShowOmsPage(original);
            });
            AddUntilStep("long page is scrollable", () => ready(view, useWiki ? ir_body : long_body)
                && view.ChildrenOfType<OverlayScrollContainer>().Single().ScrollContent.DrawHeight > view.DrawHeight + 300);
            AddStep("scroll while reading", () => page.OmsScroll = 240);
            AddUntilStep("actual scroll has moved", () => Math.Abs(page.OmsScroll - 240) < 1);
            AddStep("navigate to another page", () =>
            {
                previousScroll = page.OmsScroll;
                page.ShowOmsPage(next);
            });
            AddUntilStep("the next real page rendered", () => ready(view, useWiki ? player_body : article_body));
            AddStep("return to the already-read page", () => page.RestoreOmsPage(original, previousScroll));
            AddUntilStep("original body and actual reading position restored", () => ready(view, useWiki ? ir_body : long_body)
                && Math.Abs(page.OmsScroll - previousScroll) < 1);
            AddAssert("return preserves route and performs no additional HTTP request", () => page.CurrentOmsRoute == original
                && previousScroll > 0 && requests.Count == 1 && legacyRequests.IsEmpty);
        }

        [Test]
        public void TestNativeParagraphAndPanelHeadingLinksUseTheSuppliedCallback()
        {
            var opened = new List<string>();
            OmsMarkdownContainer markdown = null!;
            WikiPanelContainer panel = null!;
            AddStep("render the actual announcement paragraphs", () => scope.Child = markdown = new OmsMarkdownContainer(opened.Add)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CurrentPath = "https://unrelated.invalid/wiki/base/",
                Text = OmsContentMarkdown.Paragraphs((JArray)content()["news"]![0]!["body"]!),
            });
            AddUntilStep("real paragraphs and two native links loaded", () => markdown.IsLoaded && hasText(markdown, article_body)
                && markdown.ChildrenOfType<OsuMarkdownLinkText>().Count() == 2);
            AddAssert("plaintext keeps its punctuation and cannot load an image", () => hasText(markdown, "*保留星号*")
                && hasText(markdown, "![字面图片]") && !markdown.ChildrenOfType<OsuMarkdownImage>().Any());
            AddStep("click the article's actual IR help link", () => clickLink(markdown, "登录帮助"));
            AddUntilStep("callback receives the original website href", () => opened.Count == 1);
            AddAssert("unrelated document base cannot redirect OMS navigation", () => opened.Single() == new Uri(OmsIrService.ServiceOrigin, "help#ir").AbsoluteUri
                && OmsPageRoute.TryParse(service.State.ServiceAddress, opened.Single(), out var route)
                && route.Kind == OmsPageKind.Help && route.HelpAnchor == "ir");
            AddStep("click the second actual help fragment", () => clickLink(markdown, "播放器帮助"));
            AddUntilStep("second callback receives its own fragment", () => opened.Count == 2);
            AddAssert("each link preserves its actual section id", () => opened.SequenceEqual(new[]
                { new Uri(OmsIrService.ServiceOrigin, "help#ir").AbsoluteUri, new Uri(OmsIrService.ServiceOrigin, "help#players").AbsoluteUri }));
            AddStep("render an actual help panel with a linked heading", () => scope.Child = panel = new WikiPanelContainer(
                "## [新版登录说明](/help#ir)\n\n固定 OMSIR，直接登录。\n\n[阅读此章节](/help#ir)", openLink: opened.Add));
            AddUntilStep("native panel heading and paragraph links loaded", () => panel.IsLoaded
                && panel.ChildrenOfType<OsuMarkdownLinkText>().Count() == 2);
            AddStep("click the help panel heading", () => clickLink(panel, "新版登录说明"));
            AddUntilStep("heading invokes the native callback", () => opened.Count == 3);
            AddAssert("heading stays in the same native routing path", () => opened[^1] == new Uri(OmsIrService.ServiceOrigin, "help#ir").AbsoluteUri
                && fallbackLinks.Count == 0 && requests.IsEmpty && legacyRequests.IsEmpty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHideCancelsAndLateResponseCannotPopulateHiddenPage(bool useWiki)
        {
            Drawable view = null!;
            AddStep("hold an actual website-content read", () =>
            {
                holdNextRead = true;
                view = useWiki ? wiki : news;
                (useWiki ? (IOmsPageOverlay)wiki : news).ShowOmsPage(useWiki
                    ? new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "ir" }
                    : new OmsPageRoute { Kind = OmsPageKind.News });
            });
            AddUntilStep("read is held at HTTP", () => delayedRead != null && isLoading(view));
            AddStep("close the page during its read", () => hide(useWiki));
            AddUntilStep("page is hidden and cancellation reached HTTP", () => hidden(useWiki) && delayedToken.IsCancellationRequested);
            AddStep("finish the obsolete HTTP response", () => delayedRead!.TrySetResult(content(true)));
            AddUntilStep("held service read has settled", () => !service.State.Busy);
            AddWaitStep("allow delayed UI callbacks", 2);
            AddAssert("late content cannot reopen or populate the hidden page", () => hidden(useWiki)
                && !hasText(view, late_title) && !hasText(view, late_body)
                && !view.ChildrenOfType<OmsNewsCard>().Any() && !view.ChildrenOfType<WikiArticlePage>().Any());
            AddAssert("cancellation did not retry or use the legacy API", () => requests.Count == 1 && legacyRequests.IsEmpty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLateResponseCannotReplaceTheLaterRequestedPage(bool useWiki)
        {
            IOmsPageOverlay page = null!;
            Drawable view = null!;
            OmsPageRoute next = useWiki
                ? new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "players" }
                : new OmsPageRoute { Kind = OmsPageKind.Article, Slug = "latest-announcement" };
            AddStep("hold the earlier page at HTTP", () =>
            {
                holdNextRead = true;
                page = useWiki ? wiki : news;
                view = useWiki ? wiki : news;
                page.ShowOmsPage(useWiki ? new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "ir" }
                    : new OmsPageRoute { Kind = OmsPageKind.News });
            });
            AddUntilStep("earlier read is held", () => delayedRead != null && isLoading(view));
            AddStep("request another real page", () => page.ShowOmsPage(next));
            AddUntilStep("obsolete request is cancelled", () => delayedToken.IsCancellationRequested);
            AddStep("release the obsolete response late", () => delayedRead!.TrySetResult(content(true)));
            AddUntilStep("later requested page is complete", () => ready(view, useWiki ? player_body : article_body)
                && page.CurrentOmsRoute == next);
            AddWaitStep("allow any late rendering to run", 2);
            AddAssert("later content stays selected after the old response", () => page.CurrentOmsRoute == next
                && hasText(view, useWiki ? player_body : article_body) && !hasText(view, late_title) && !hasText(view, late_body));
            AddAssert("only the two explicit anonymous reads were made", () => requests.Count == 2
                && requests.All(request => request.Path == "/client-content" && request.Bearer == null) && legacyRequests.IsEmpty);
        }

        private void hide(bool useWiki)
        {
            if (useWiki) wiki.Hide();
            else news.Hide();
        }

        private bool hidden(bool useWiki) => (useWiki ? wiki.State.Value : news.State.Value) == Visibility.Hidden;
        private static bool isLoading(Drawable view) => view.ChildrenOfType<LoadingLayer>().Any(layer => layer.State.Value == Visibility.Visible);
        private static bool ready(Drawable view, string text) => !isLoading(view) && hasText(view, text);
        private static bool hasText(Drawable view, string text)
            => string.Concat(view.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(text.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        private void clickLink(Drawable view, string label)
        {
            InputManager.MoveMouseTo(view.ChildrenOfType<OsuMarkdownLinkText>().Single(link => hasText(link, label)));
            InputManager.Click(MouseButton.Left);
        }

        private async Task<HttpResponseMessage> response(HttpRequestMessage request, CancellationToken token)
        {
            Uri uri = request.RequestUri!;
            requests.Enqueue(new RequestSnapshot(uri.Host, uri.AbsolutePath, request.Method.Method, request.Headers.Authorization?.Parameter));
            if (uri.AbsolutePath != "/client-content" || request.Method != HttpMethod.Get)
                throw new InvalidOperationException("Unexpected synthetic native-content request: " + request.Method + " " + uri.AbsolutePath);
            JObject document;
            if (holdNextRead)
            {
                holdNextRead = false;
                delayedToken = token;
                var pending = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                delayedRead = pending;
                // The transport deliberately finishes after cancellation to exercise obsolete completion handling.
                document = await pending.Task.ConfigureAwait(false);
            }
            else document = content();
            var result = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(document.ToString(Formatting.None), Encoding.UTF8, "application/json") };
            result.Headers.ETag = new EntityTagHeaderValue("\"synthetic-native-content-v1\"");
            result.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromSeconds(300) };
            return result;
        }

        private static readonly string[] helpHeadings =
        {
            "其他播放器说明", "新版登录说明", "谱库与索引", "BMS 游玩", "mania 游玩", "成绩保存", "隐私说明", "排查问题",
        };

        private static JObject content(bool late = false) => new JObject
        {
            ["version"] = 1,
            ["news"] = new JArray(
                post("latest-announcement", late ? late_title : latest_title, "2026-10-11", latest_excerpt, new JArray(
                    section("全文", article_body + "\n\n第二段保留原文。\n*保留星号*\n![字面图片](https://image.invalid/unused.png)", new JArray(
                        link("登录帮助", "/help#ir"), link("播放器帮助", "/help#players"))),
                    section("结束语", "公告的最后一段正文。"))),
                post("september-announcement", "九月公告", "2026-09-02", "九月的真实摘要。", new JArray(section("全文", "九月公告的正文。"))),
                post("long-announcement", long_title, "2025-11-03", "较早的长篇公告摘要。", new JArray(section("全文", longText(long_body))))),
            // The established IR id is deliberately second and has a renamed heading.
            ["help"] = new JArray(
                help("players", helpHeadings[0], late ? late_body : player_body, new JArray(link("账号页面", "/account"))),
                help("ir", helpHeadings[1], late ? late_body : longText(ir_body), new JArray(link("播放器帮助", "/help#players"))),
                help("library", helpHeadings[2], "重建索引后按需计算转谱星级。"),
                help("bms", helpHeadings[3], "BMS 原包直接读取。"),
                help("mania", helpHeadings[4], "mania 谱面直接读取。"),
                help("scores", helpHeadings[5], "本地保存与在线成绩分别查看。"),
                help("privacy", helpHeadings[6], "公开页面无需登录。"),
                help("troubleshooting", helpHeadings[7], "查看实际日志以排查问题。")),
        };

        private static string longText(string firstLine) => firstLine + "\n" + string.Join('\n',
            Enumerable.Range(1, 90).Select(index => "完整正文的第 " + index + " 行，保留玩家原来的阅读位置。"));
        private static JObject link(string label, string href) => new JObject { ["label"] = label, ["href"] = href };
        private static JObject section(string heading, string text, JArray? links = null)
        {
            var value = new JObject { ["heading"] = heading, ["text"] = text };
            if (links != null) value["links"] = links;
            return value;
        }

        private static JObject help(string id, string heading, string text, JArray? links = null)
        {
            JObject value = section(heading, text, links);
            value["id"] = id;
            return value;
        }

        private static JObject post(string slug, string title, string publishedAt, string excerpt, JArray body) => new JObject
        {
            ["slug"] = slug,
            ["title"] = title,
            ["published_at"] = publishedAt,
            ["image"] = JValue.CreateNull(),
            ["excerpt"] = excerpt,
            ["body"] = body,
        };

        protected override void Dispose(bool isDisposing)
        {
            delayedRead?.TrySetResult(content(true));
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed record RequestSnapshot(string Host, string Path, string Method, string? Bearer);
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => this.respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            public OmsIrSession? Read(string target) => null;
            public void Write(string target, OmsIrSession value) => throw new InvalidOperationException("Anonymous content must not write credentials.");
            public void Delete(string target) => throw new InvalidOperationException("Anonymous content must not delete credentials.");
        }

        private sealed class RecordingLinkHandler : ILinkHandler
        {
            public int Count { get; private set; }
            public void HandleLink(string url) => Count++;
            public void HandleLink(LinkDetails link) => Count++;
        }
    }
}
