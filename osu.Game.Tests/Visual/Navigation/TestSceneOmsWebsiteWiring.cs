// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
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
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.News;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Overlays.Rankings.Tables;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.Bms;
using osu.Game.Screens.Select;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneOmsWebsiteWiring : OsuGameTestScene
    {
        private const string chart_md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly ConcurrentQueue<string> requests = new ConcurrentQueue<string>();
        private HttpClient? http;

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddUntilStep("native pages and navigation have loaded", () => Game.OmsPages.IsLoaded
                && Game.ChildrenOfType<NewsOverlay>().SingleOrDefault()?.IsLoaded == true
                && Game.ChildrenOfType<WikiOverlay>().SingleOrDefault()?.IsLoaded == true
                && Game.ChildrenOfType<BeatmapListingOverlay>().SingleOrDefault()?.IsLoaded == true
                && Game.ChildrenOfType<BeatmapSetOverlay>().SingleOrDefault()?.IsLoaded == true
                && Game.ChildrenOfType<UserProfileOverlay>().SingleOrDefault()?.IsLoaded == true
                && Game.ChildrenOfType<RankingsOverlay>().SingleOrDefault()?.IsLoaded == true);
            showToolbar();
        }

        protected override TestOsuGame CreateTestGame()
        {
            http?.Dispose();
            requests.Clear();
            http = new HttpClient(new Handler(request =>
            {
                requests.Enqueue(request.RequestUri!.AbsolutePath);
                return json(response(request));
            }));
            return new NativePageTestGame(LocalStorage, API, http);
        }

        [Test]
        public void TestSongSelectAndToolbarUseOmsWithLegacyOnlineDisabled()
        {
            SoloSongSelect select = null!;
            var chart = new BeatmapInfo(new BmsRuleset().RulesetInfo)
            {
                MD5Hash = new string('a', 32),
                Hash = new string('b', 64),
                OnlineID = -1,
            };
            AddAssert("legacy API stays disabled", () => Game.OnlineFeaturesEnabled, () => Is.False);
            AddAssert("all existing services have independent native toolbar entries", () => Game.ChildrenOfType<ToolbarRankingsButton>().Any()
                && Game.ChildrenOfType<ToolbarNewsButton>().Any() && Game.ChildrenOfType<ToolbarWikiButton>().Any()
                && Game.ChildrenOfType<ToolbarBeatmapListingButton>().Any() && Game.ChildrenOfType<ToolbarOmsCommunityButton>().Any());
            AddAssert("hidden native startup sends no OMS read and social is deferred", () => requests.IsEmpty
                && !Game.ChildrenOfType<ToolbarChatButton>().Any() && !Game.ChildrenOfType<DashboardOverlay>().Any());
            AddAssert("toolbar has no generic chart leaderboard toggle", () => !Game.ChildrenOfType<ToolbarOverlayToggleButton>().Any(button => button.TooltipMain.ToString() == "谱面排行榜"));
            AddStep("open native solo selection", () => Game.ScreenStack.Push(select = new SoloSongSelect()));
            AddUntilStep("selection loaded", () => select.IsLoaded);
            AddAssert("local MD5 has details and a board without a ppy ID", () =>
            {
                return select.GetForwardActions(chart).Any(item => item.Text.Value == CommonStrings.Details)
                       && select.GetForwardActions(chart).Any(item => item.Text.Value.ToString() == "谱面排行榜")
                       && Game.GetOmsBeatmapActions(chart).Any(item => item.Text.Value == CommonStrings.CopyLink);
            });
            AddAssert("guest can copy the actual OMS chart link", () => select.GetForwardActions(chart).Any(item => item.Text.Value == CommonStrings.CopyLink));
            AddAssert("public links do not sign in or enable submission", () => Game.OmsIr.State.Enabled, () => Is.False);
            AddStep("remove the chart identity", () => chart.MD5Hash = string.Empty);
            AddAssert("no link is fabricated for an unidentified chart", () => Game.GetOmsBeatmapActions(chart).Any(), () => Is.False);
            AddStep("follow an actual OMS home link", () => Game.OpenOmsLink("/"));
            AddUntilStep("home routes to the native news page", () => Game.OmsPages.Route.Kind == OmsPageKind.News
                && Game.ChildrenOfType<NewsOverlay>().Single().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestActualNewsAndHelpLinksReturnThroughNativePages()
        {
            NewsOverlay news = null!;
            WikiOverlay wiki = null!;
            float scroll = 0;
            AddStep("click the real news toolbar entry", () => click(Game.ChildrenOfType<ToolbarNewsButton>().Single()));
            AddUntilStep("native news cards are visible", () => (news = Game.ChildrenOfType<NewsOverlay>().Single()).State.Value == Visibility.Visible
                && news.ChildrenOfType<OmsNewsCard>().Any(card => card.IsLoaded));
            AddStep("click an actual visible news card", () => click(news.ChildrenOfType<OmsNewsCard>().OrderBy(card => card.ScreenSpaceDrawQuad.TopLeft.Y).First()));
            AddUntilStep("article is rendered by the same native page", () => Game.OmsPages.Route.Kind == OmsPageKind.Article
                && news.ChildrenOfType<OsuMarkdownLinkText>().Any(link => link.IsLoaded));
            AddAssert("card navigation retains its previous news page", () => Game.OmsPages.HistoryDepth, () => Is.EqualTo(1));
            AddStep("scroll to the original body link", () => news.OmsScroll = 100000);
            AddUntilStep("body link is within the native viewport", () => news.OmsScroll > 50
                && news.ChildrenOfType<OsuMarkdownLinkText>().Single().ScreenSpaceDrawQuad.Centre.Y < Game.ScreenSpaceDrawQuad.BottomLeft.Y - 30);
            AddStep("follow the original help link from the body", () => click(news.ChildrenOfType<OsuMarkdownLinkText>().Single()));
            AddUntilStep("link opens the native wiki chapter", () => (wiki = Game.ChildrenOfType<WikiOverlay>().Single()).State.Value == Visibility.Visible
                && wiki.CurrentOmsRoute.HelpAnchor == "ir" && hasText(wiki, "固定 OMSIR，直接登录。"));
            AddAssert("exact route and original window are preserved", () => news.State.Value == Visibility.Hidden
                && Game.OmsPages.HistoryDepth == 2 && requests.Count(path => path == "/client-content") == 1);
            AddStep("use the native header back action", () => wiki.Header.ChildrenOfType<IconButton>().Single(button => button.TooltipText.ToString() == "返回").Action!());
            AddUntilStep("original article is restored", () => news.State.Value == Visibility.Visible && Game.OmsPages.Route.Kind == OmsPageKind.Article);
            AddStep("move the native article scroll", () => news.OmsScroll = 150);
            AddUntilStep("scroll is applied", () => news.OmsScroll > 50);
            AddStep("record scroll and reopen the linked chapter", () =>
            {
                scroll = news.OmsScroll;
                Game.OpenOmsLink("/help#ir");
            });
            AddUntilStep("chapter is visible again", () => wiki.State.Value == Visibility.Visible && hasText(wiki, "固定 OMSIR，直接登录。"));
            AddStep("return using the native Back binding", () => Assert.That(wiki.OnPressed(new KeyBindingPressEvent<GlobalAction>(InputManager.CurrentState, GlobalAction.Back)), Is.True));
            AddUntilStep("original article scroll is restored", () => news.State.Value == Visibility.Visible && Math.Abs(news.OmsScroll - scroll) < 2);
            AddStep("return to the news listing", () => Game.OmsPages.Back());
            AddUntilStep("listing and empty history restored", () => news.CurrentOmsRoute.Kind == OmsPageKind.News
                && news.ChildrenOfType<OmsNewsCard>().Any(card => card.IsLoaded) && Game.OmsPages.HistoryDepth == 0);
            AddAssert("all native content navigation used one anonymous read", () => requests.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNativeChartAndPlayerEntriesKeepIdentitiesAndFilters()
        {
            BeatmapListingOverlay listing = null!;
            BeatmapSetOverlay details = null!;
            UserProfileOverlay profile = null!;
            var route = new OmsPageRoute { Kind = OmsPageKind.Charts, Query = "原谱 & #", Page = 2 };
            int beforeBack = 0;
            AddStep("open a specific chart-directory scope", () => Game.OpenOmsPage(route));
            AddUntilStep("real native chart cards loaded", () => (listing = Game.ChildrenOfType<BeatmapListingOverlay>().Single()).State.Value == Visibility.Visible
                && listing.ChildrenOfType<OmsChartCard>().Any(card => card.IsLoaded));
            AddStep("click a real chart card", () => click(listing.ChildrenOfType<OmsChartCard>().Single()));
            AddUntilStep("MD5 detail opens its native information page", () => (details = Game.ChildrenOfType<BeatmapSetOverlay>().Single()).State.Value == Visibility.Visible
                && hasText(details, chart_md5) && Game.OmsPages.Route.Kind == OmsPageKind.Chart);
            AddAssert("no ppy online set identity is fabricated", () => Game.OmsPages.Route.Md5 == chart_md5 && Game.OmsPages.Route.Ruleset == "bms"
                && details.Header.BeatmapSet.Value == null && listing.State.Value == Visibility.Hidden);
            AddStep("return to cached directory", () => { beforeBack = requests.Count; Game.OmsPages.Back(); });
            AddUntilStep("actual search and page are restored", () => listing.State.Value == Visibility.Visible && Game.OmsPages.Route == route
                && listing.ChildrenOfType<BasicSearchTextBox>().Single().Text == route.Query);
            AddAssert("back does not read the directory again", () => requests.Count, () => Is.EqualTo(beforeBack));
            showToolbar();
            AddStep("open the actual ranking toolbar entry", () => click(Game.ChildrenOfType<ToolbarRankingsButton>().Single()));
            AddUntilStep("original native ranking header is visible", () => Game.ChildrenOfType<RankingsOverlay>().Single().State.Value == Visibility.Visible
                && Game.OmsPages.Route.Kind == OmsPageKind.Rankings && hasText(Game.ChildrenOfType<RankingsOverlay>().Single(), "请选择实际键型"));
            AddStep("open a public OMS player", () => Game.OpenRelatedOmsPage(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 31, Sources = "oms" }));
            AddUntilStep("native public header uses the actual OMS identity", () => (profile = Game.ChildrenOfType<UserProfileOverlay>().Single()).State.Value == Visibility.Visible
                && ((OmsIrProfileHeader)profile.Header).PublicPlayer.Value?.Id == 31 && hasText(profile, "真实公开玩家"));
            AddAssert("public viewing never reads private UUID history", () => requests.All(path => !path.StartsWith("/api/ir/v1/user/", StringComparison.Ordinal))
                && !hasText(profile, "本局 ID："));
            AddStep("switch to the existing own-profile entry", () => Game.ShowOwnOmsProfile());
            AddUntilStep("private profile is detached from public navigation", () => profile.CurrentOmsRoute == null
                && Game.OmsPages.HistoryDepth == 0 && ((OmsIrProfileHeader)profile.Header).PublicRoute.Value == null);
            AddAssert("private mode has no old public header callbacks", () => !profile.Header.ChildrenOfType<IconButton>().Any(button => button.TooltipText.ToString() == "返回"));
        }

        [Test]
        public void TestBoundedHistoryRestoresPagesAndLaterNavigationWins()
        {
            int navigation = 0;
            AddStep("two native destinations requested before mounting", () =>
            {
                Game.OpenOmsPage(new OmsPageRoute { Kind = OmsPageKind.News });
                Game.OpenOmsPage(new OmsPageRoute { Kind = OmsPageKind.Help, HelpAnchor = "ir" });
            });
            AddUntilStep("later destination alone is visible", () => Game.ChildrenOfType<WikiOverlay>().Single().State.Value == Visibility.Visible
                && hasText(Game.ChildrenOfType<WikiOverlay>().Single(), "固定 OMSIR，直接登录。")
                && Game.ChildrenOfType<NewsOverlay>().Single().State.Value == Visibility.Hidden);
            AddRepeatStep("open distinct real articles", () => Game.OpenRelatedOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.Article, Slug = "article-" + ++navigation }), 18);
            AddUntilStep("last article is visible", () => Game.OmsPages.Route.Slug == "article-18" && hasText(Game.ChildrenOfType<NewsOverlay>().Single(), "公告 18"));
            AddAssert("history retains sixteen prior destinations", () => Game.OmsPages.HistoryDepth, () => Is.EqualTo(16));
            AddStep("back once", () => Game.OmsPages.Back());
            AddUntilStep("previous article restored", () => Game.OmsPages.Route.Slug == "article-17" && hasText(Game.ChildrenOfType<NewsOverlay>().Single(), "公告 17"));
            AddRepeatStep("walk the retained history", () => Game.OmsPages.Back(), 15);
            AddUntilStep("oldest retained article restored", () => Game.OmsPages.HistoryDepth == 0 && Game.OmsPages.Route.Slug == "article-2"
                && hasText(Game.ChildrenOfType<NewsOverlay>().Single(), "公告 2"));
            AddStep("back beyond retained history closes the native page", () => Game.OmsPages.Back());
            AddUntilStep("native content page is hidden", () => Game.ChildrenOfType<NewsOverlay>().Single().State.Value == Visibility.Hidden);
            AddAssert("history reuse does not prefetch or poll", () => requests.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNativeRankingRowAndPublicBestCardOpenTheirRealDestinations()
        {
            RankingsOverlay rankings = null!;
            UserProfileOverlay profile = null!;
            float readingPosition = 0;
            int readsBeforeBack = 0;
            AddStep("open actual BMS coverage rankings", () => Game.OpenOmsPage(new OmsPageRoute
            { Kind = OmsPageKind.Rankings, Ruleset = "bms", Keymode = "bms_7k", Sources = "oms", Metric = "coverage" }));
            AddUntilStep("real native table row has loaded", () => (rankings = Game.ChildrenOfType<RankingsOverlay>().Single()).State.Value == Visibility.Visible
                && rankings.ChildrenOfType<OmsPlayerRankingsTable>().SingleOrDefault()?.IsLoaded == true && hasText(rankings, "真实公开玩家"));
            AddAssert("native table uses public ranks and counts", () => rankings.ChildrenOfType<OmsPlayerRankingsTable>().Single().Columns.Length == 5
                && hasText(rankings, "#1") && !hasText(rankings, "表现分"));
            AddStep("bring the actual ranking row into view", () => rankings.OmsScroll = 100000);
            AddUntilStep("ranking row is inside the native viewport", () => rankings.ChildrenOfType<OmsPlayerRankingsTable>().Single()
                .ChildrenOfType<LinkFlowContainer>().Single(flow => hasText(flow, "真实公开玩家")).ChildrenOfType<SpriteText>().First().ScreenSpaceDrawQuad.Centre.Y < Game.ScreenSpaceDrawQuad.BottomLeft.Y - 20);
            AddStep("click the native player row", () => click(rankings.ChildrenOfType<OmsPlayerRankingsTable>().Single()
                .ChildrenOfType<LinkFlowContainer>().Single(flow => hasText(flow, "真实公开玩家")).ChildrenOfType<SpriteText>().First()));
            AddUntilStep("native profile statistics and best card have loaded", () => (profile = Game.ChildrenOfType<UserProfileOverlay>().Single()).State.Value == Visibility.Visible
                && Game.OmsPages.Route.UserId == 31 && profile.ChildrenOfType<ProfileItemContainer>().Count(item => item.IsPresent) == 2
                && hasText(profile, "EX：175/200"));
            AddAssert("player navigation preserves its actual source and keys", () => Game.OmsPages.Route.Keymode == "bms_7k"
                && Game.OmsPages.Route.Sources == "oms" && !hasText(profile, "本局 ID："));
            AddStep("scroll the real public-best card into view", () => profile.OmsScroll = (float)profile.ChildrenOfType<OverlayScrollContainer>().Single().ScrollableExtent);
            AddUntilStep("original chart link is inside the native profile viewport", () => profile.ChildrenOfType<LinkFlowContainer>()
                .Single(flow => hasText(flow, "真实原谱")).ChildrenOfType<SpriteText>().First().ScreenSpaceDrawQuad.Centre.Y < Game.ScreenSpaceDrawQuad.BottomLeft.Y - 20);
            AddStep("click the actual original chart link", () =>
            {
                readingPosition = profile.OmsScroll;
                click(profile.ChildrenOfType<LinkFlowContainer>().Single(flow => hasText(flow, "真实原谱")).ChildrenOfType<SpriteText>().First());
            });
            AddUntilStep("record opens its native original-MD5 detail", () => Game.ChildrenOfType<BeatmapSetOverlay>().Single().State.Value == Visibility.Visible
                && Game.OmsPages.Route.Kind == OmsPageKind.Chart && Game.OmsPages.Route.Md5 == chart_md5
                && hasText(Game.ChildrenOfType<BeatmapSetOverlay>().Single(), chart_md5));
            AddAssert("cross-chart conditions are cleared", () => Game.OmsPages.Route.Keymode == "bms_7k" && Game.OmsPages.Route.Sources == "oms"
                && Game.OmsPages.Route.Condition == null);
            AddStep("go back to the existing profile", () => { readsBeforeBack = requests.Count; Game.OmsPages.Back(); });
            AddWaitStep("allow native profile layout to settle", 2);
            AddUntilStep("native record page and reading position restored", () => profile.State.Value == Visibility.Visible
                && Game.OmsPages.Route.Kind == OmsPageKind.Profile && Math.Abs(profile.OmsScroll - readingPosition) < 2 && hasText(profile, "EX：175/200"));
            AddAssert("return performs no duplicate profile request", () => requests.Count, () => Is.EqualTo(readsBeforeBack));
        }

        private void click(Drawable target)
        {
            InputManager.MoveMouseTo(target);
            InputManager.Click(MouseButton.Left);
        }

        private void showToolbar()
        {
            AddStep("show the real toolbar", () => Game.Toolbar.Show());
            AddUntilStep("toolbar is fully within the input viewport", () => Game.Toolbar.State.Value == Visibility.Visible && Game.Toolbar.Y >= -1);
        }

        private static bool hasText(Drawable target, string text) => string.Concat(target.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString()))
            .Replace(" ", string.Empty, StringComparison.Ordinal).Contains(text.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        private static JObject response(HttpRequestMessage request)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/client-content") return new JObject
            {
                ["version"] = 1,
                ["news"] = new JArray(Enumerable.Range(1, 20).Select(index => new JObject
                {
                    ["slug"] = "article-" + index,
                    ["title"] = "公告 " + index,
                    ["published_at"] = "2026-10-12",
                    ["excerpt"] = "真实正文",
                    ["image"] = JValue.CreateNull(),
                    ["body"] = new JArray(new JObject
                    {
                        ["heading"] = "完整公告",
                        ["text"] = string.Join('\n', Enumerable.Range(1, 45).Select(line => "原始公告段落 " + line)),
                        ["links"] = new JArray(new JObject { ["label"] = "登录帮助", ["href"] = "/help#ir" })
                    }),
                })),
                ["help"] = new JArray(new JObject { ["id"] = "ir", ["heading"] = "登录帮助", ["text"] = "固定 OMSIR，直接登录。" }),
            };
            if (path == "/api/ir/v2/sources") return new JObject
            {
                ["items"] = new JArray(new JObject
                { ["code"] = "oms", ["label"] = "OMS", ["record_kind"] = "play", ["available"] = true })
            };
            if (path == "/api/ir/v1/users/31/profile") return new JObject { ["user"] = new JObject { ["id"] = 31, ["username"] = "真实公开玩家" } };
            if (path is "/api/ir/v1/rankings/scopes" or "/api/ir/v1/rankings/players" or "/api/ir/v1/users/31/performance" or "/api/ir/v1/users/31/public-bests")
                return publicPlayerResponse(request);
            JObject chart = new JObject
            {
                ["ruleset"] = "bms",
                ["chart"] = new JObject
                { ["md5"] = chart_md5, ["title"] = "真实原谱", ["artist"] = "原作者", ["difficulty"] = "原难度" }
            };
            if (path == "/api/ir/v2/charts/" + chart_md5) return chart;
            if (path == "/api/ir/v2/charts") return new JObject { ["items"] = new JArray(chart), ["page"] = 2, ["limit"] = 20, ["total"] = 21 };
            throw new AssertionException("Unexpected native-page HTTP request: " + path);
        }

        private static JObject publicPlayerResponse(HttpRequestMessage request)
        {
            string path = request.RequestUri!.AbsolutePath;
            Assert.That(Uri.UnescapeDataString(request.RequestUri.Query), Does.Contain("ruleset=bms").And.Contain("keymode=bms_7k"));
            var user = new JObject { ["id"] = 31, ["username"] = "真实公开玩家" };
            var condition = new JObject
            {
                ["id"] = new string('c', 64),
                ["label"] = "OMS 实际计分条件",
                ["comparison"] = "same-oms-rules",
                ["conditions"] = new JObject(),
                ["unknown_fields"] = new JArray(),
            };
            var scope = new JObject { ["ruleset"] = "bms", ["keymode"] = "bms_7k" };
            var result = new JObject
            {
                ["scope"] = scope,
                ["page"] = 1,
                ["limit"] = 20,
                ["total"] = 1,
                ["trust"] = "unverified-reference",
                ["snapshot"] = new JObject { ["read_at"] = "2026-10-12T00:00:00Z", ["dataset"] = "live-public-bests" },
            };
            if (path.EndsWith("/rankings/scopes", StringComparison.Ordinal))
            {
                scope["source"] = "oms";
                result["items"] = new JArray(condition);
            }
            else if (path.EndsWith("/rankings/players", StringComparison.Ordinal))
            {
                scope["source"] = "oms"; scope["metric"] = "coverage"; scope["condition_scope"] = JValue.CreateNull();
                scope["basis"] = "current-public-bests"; scope["rating"] = false;
                result["me"] = JValue.CreateNull();
                result["items"] = new JArray(new JObject { ["rank"] = 1, ["user"] = user, ["value"] = "1", ["public_chart_count"] = 1, ["public_best_count"] = 1 });
            }
            else
            {
                scope["selected_sources"] = new JArray("oms");
                result["user"] = user;
                if (path.EndsWith("/performance", StringComparison.Ordinal))
                {
                    result["totals"] = new JObject { ["public_chart_count"] = 1, ["public_best_count"] = 1 };
                    result["lanes"] = new JArray(new JObject
                    {
                        ["source"] = "oms",
                        ["condition_scope"] = condition,
                        ["metrics"] = new JObject { ["public_chart_count"] = 1, ["public_best_count"] = 1, ["cleared_chart_count"] = 1, ["best_total_score"] = JValue.CreateNull() },
                        ["rankings"] = new JArray(new JObject { ["metric"] = "cleared_charts", ["rank"] = 1, ["total_players"] = 1 }),
                    });
                }
                else
                {
                    scope["condition_id"] = JValue.CreateNull(); scope["record_basis"] = "current-public-bests";
                    result["items"] = new JArray(new JObject
                    {
                        ["chart"] = new JObject { ["md5"] = chart_md5, ["title"] = "真实原谱", ["artist"] = "原作者", ["difficulty"] = "原难度", ["sha256"] = JValue.CreateNull() },
                        ["ruleset"] = "bms",
                        ["condition_scope"] = condition,
                        ["score"] = new JObject
                        {
                            ["source"] = "oms",
                            ["record_kind"] = "play",
                            ["record_id"] = "oms:99",
                            ["ex_score"] = 175,
                            ["max_ex_score"] = 200,
                            ["total_score"] = JValue.CreateNull(),
                            ["total_score_version"] = JValue.CreateNull(),
                            ["accuracy"] = 0.875,
                            ["passed"] = true,
                            ["played_at"] = "2026-10-11T00:00:00Z",
                            ["received_at"] = "2026-10-12T00:00:00Z",
                            ["conditions"] = new JObject(),
                            ["unknown_fields"] = new JArray(),
                            ["lamp"] = new JObject { ["family"] = "original:200", ["value"] = 5, ["label"] = "HARD CLEAR" },
                        },
                        ["best_lamps"] = new JArray(),
                    });
                }
            }
            return result;
        }

        private static HttpResponseMessage json(JObject value)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(value.ToString(Formatting.None), Encoding.UTF8, "application/json") };
            if (value["news"] is JArray)
                response.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromSeconds(300) };
            return response;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            http?.Dispose();
        }

        private partial class NativePageTestGame : TestOsuGame
        {
            private readonly HttpClient http;

            public NativePageTestGame(Storage storage, IAPIProvider api, HttpClient http)
                : base(storage, api) => this.http = http;

            protected override OmsIrService CreateOmsIrService(Storage storage) => new OmsIrService(storage, http, new Credentials());
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
            public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            public OmsIrSession? Read(string target) => null;
            public void Write(string target, OmsIrSession value) => throw new AssertionException("Anonymous navigation must not create credentials.");
            public void Delete(string target) => throw new AssertionException("Anonymous navigation must not alter credentials.");
        }
    }
}
