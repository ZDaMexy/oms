// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsNativeCharts : OsuManualInputManagerTestScene
    {
        private const string bms_md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string mania_md5 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string local_title = "本地原谱曲名";
        private const string local_artist = "本地原谱艺术家";
        private const string late_title = "迟到的旧谱面";
        private readonly ConcurrentQueue<RequestSnapshot> requests = new ConcurrentQueue<RequestSnapshot>();
        private readonly ConcurrentQueue<string> legacyRequests = new ConcurrentQueue<string>();
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private BeatmapListingOverlay listing = null!;
        private BeatmapSetOverlay detail = null!;
        private volatile bool holdNextRead;
        private volatile bool omitMetadata;
        private volatile bool missingChart;
        private volatile bool wrongIdentity;
        private volatile TaskCompletionSource<JObject>? delayedRead;
        private CancellationToken delayedToken;
        private int completedReads;

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUp()
        {
            AddStep("create isolated native chart pages", () =>
            {
                delayedRead?.TrySetResult(chart(bms_md5, late_title));
                Clear();
                service?.Dispose();
                http?.Dispose();
                requests.Clear();
                legacyRequests.Clear();
                holdNextRead = omitMetadata = missingChart = wrongIdentity = false;
                delayedRead = null;
                delayedToken = default;
                completedReads = 0;
                Beatmap.SetDefault();
                API.Logout();
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    legacyRequests.Enqueue(request.GetType().Name);
                    return true;
                };
                http = new HttpClient(new Handler(response));
                service = new OmsIrService(LocalStorage.GetStorageForDirectory("oms-native-charts-synthetic-" + Guid.NewGuid()), http, new Credentials());
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(OmsIrService), service),
                        (typeof(IBindable<WorkingBeatmap>), Beatmap),
                    },
                    Children = new Drawable[]
                    {
                        listing = new BeatmapListingOverlay(true),
                        detail = new BeatmapSetOverlay(true),
                    },
                };
            });
            AddUntilStep("native chart pages loaded and hidden", () => listing.IsLoaded && detail.IsLoaded
                && listing.State.Value == Visibility.Hidden && detail.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestListingSearchExplicitPaginationAndCachedRestore()
        {
            var firstPage = new OmsPageRoute { Kind = OmsPageKind.Charts };
            float readingPosition = 0;
            AddWaitStep("leave chart pages idle", 2);
            AddAssert("hidden startup performs no request", () => requests.IsEmpty && legacyRequests.IsEmpty);
            AddStep("open the recorded chart directory", () => listing.ShowOmsPage(firstPage));
            AddUntilStep("twenty native chart cards loaded", () => !isLoading(listing) && listing.ChildrenOfType<OmsChartCard>().Count() == 20
                && hasText(listing, "合成谱面 1"));
            AddAssert("directory keeps its native anonymous page and search", () => listing.Width == 0.85f
                && listing.Header is OmsBeatmapListingHeader && listing.Header.Title.Title.ToString() == "谱面列表"
                && listing.Header.FilterControl == null && !listing.ChildrenOfType<OnlineViewContainer>().Any()
                && listing.ChildrenOfType<BasicSearchTextBox>().Count() == 1 && service.State.Account == null);
            AddAssert("cards show real metadata beside their icon", () =>
            {
                OmsChartCard card = listing.ChildrenOfType<OmsChartCard>().First();
                SpriteIcon icon = card.ChildrenOfType<SpriteIcon>().First();
                return hasText(card, "合成艺术家") && hasText(card, "难度标注：☆12") && hasText(card, "公开谱面目录")
                    && flow(card, "合成谱面 1").ScreenSpaceDrawQuad.Centre.X > icon.ScreenSpaceDrawQuad.Centre.X
                    && card.DrawWidth < listing.DrawWidth;
            });
            AddAssert("one bounded anonymous OMS directory read", () => requests.Count == 1
                && requests.Single().Path == "/api/ir/v2/charts" && requests.Single().Query == "?q=&page=1&limit=20"
                && requests.Single().Bearer == null && legacyRequests.IsEmpty);
            AddStep("scroll to the last card", () => listing.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddWaitStep("scrolling and idle do not preload another page", 2);
            AddAssert("next page is still an explicit action", () => requests.Count == 1 && buttonExists(listing, "下一页"));
            AddStep("read the next directory page", () => button(listing, "下一页").TriggerClick());
            AddUntilStep("only the next page card is displayed", () => !isLoading(listing) && listing.CurrentOmsRoute?.Page == 2
                && listing.ChildrenOfType<OmsChartCard>().Count() == 1 && hasText(listing, "合成谱面 21"));
            AddAssert("next page reads exactly its requested scope", () => requests.Count == 2
                && requests.Last().Query == "?q=&page=2&limit=20" && buttonExists(listing, "上一页"));
            AddStep("restore the previously read first page", () => listing.RestoreOmsPage(firstPage, 0));
            AddUntilStep("first page restored without a read", () => !isLoading(listing) && listing.CurrentOmsRoute == firstPage
                && listing.ChildrenOfType<OmsChartCard>().Count() == 20 && requests.Count == 2);
            AddStep("move to a reading position", () => listing.OmsScroll = 120);
            AddUntilStep("actual directory scroll moved", () => Math.Abs(listing.OmsScroll - 120) < 1);
            AddStep("close the directory while retaining reading position", () => { readingPosition = listing.OmsScroll; listing.Hide(); });
            AddStep("return to the same cached directory", () => listing.RestoreOmsPage(firstPage, readingPosition));
            AddUntilStep("native reading position restored", () => !isLoading(listing) && Math.Abs(listing.OmsScroll - readingPosition) < 1);
            AddAssert("restoring does not duplicate network work", () => readingPosition > 0 && requests.Count == 2 && legacyRequests.IsEmpty);
            AddStep("type a native Unicode search", () => listing.ChildrenOfType<BasicSearchTextBox>().Single().Text = "中文 & 曲名");
            AddWaitStep("typing alone does not search", 2);
            AddAssert("search is sent on commit only", () => requests.Count == 2);
            AddStep("commit the native search", () => listing.ChildrenOfType<IconButton>().Single(item => item.TooltipText.ToString() == "搜索已收录谱面").TriggerClick());
            AddUntilStep("searched cards replace the directory", () => !isLoading(listing) && hasText(listing, "中文 & 曲名 搜索结果"));
            AddAssert("search retains the actual text and resets to page one", () => requests.Count == 3
                && query(requests.Last().Query)["q"] == "中文 & 曲名" && query(requests.Last().Query)["page"] == "1"
                && listing.CurrentOmsRoute is { Query: "中文 & 曲名", Page: 1 } && legacyRequests.IsEmpty);
        }

        [Test]
        public void TestBmsDetailUsesMd5AndAuthorPlaylevelInNativeColumns()
        {
            AddStep("select the actual local BMS", () => setCurrentChart(bms_md5, "bms"));
            AddStep("read that source chart", () => detail.ShowOmsPage(chartRoute(bms_md5)));
            AddUntilStep("public identity and matching local metadata appear", () => !isLoading(detail)
                && hasText(detail, bms_md5) && hasText(detail, "公开 BMS 原谱") && hasText(detail, "作者 PLAYLEVEL：★12")
                && hasText(detail, "BPM：180"));
            AddAssert("native header uses no fabricated ppy identity or selector", () => detail.Header is OmsBeatmapSetHeader
                && detail.Header.Title.Title.ToString() == "谱面信息" && detail.Header.BeatmapSet.Value == null
                && detail.Header.HeaderContent == null && detail.Header.RulesetSelector == null
                && !detail.ChildrenOfType<OnlineViewContainer>().Any() && legacyRequests.IsEmpty);
            AddAssert("public labels and original source statistics stay distinct", () => hasText(detail, "公开艺术家")
                && hasText(detail, "难度标注：☆12（公开目录）") && hasText(detail, "曲名来源：公开谱面目录")
                && hasText(detail, "作者难度标注，不是计算星级。") && !hasText(detail, "本地计算星级")
                && hasText(detail, "键型：BMS 7k") && hasText(detail, "时长：2:03") && hasText(detail, "物件数：321"));
            AddAssert("the native detail places metadata on the left and local statistics on the right", () =>
            {
                OmsChartInfo info = detail.ChildrenOfType<OmsChartInfo>().Single();
                return flow(info, "公开 BMS 原谱").ScreenSpaceDrawQuad.Centre.X < flow(info, "本地谱面数据").ScreenSpaceDrawQuad.Centre.X
                    && buttonExists(info, "查看成绩榜") && buttonExists(info, "查找 BMS 原包");
            });
            AddAssert("detail reads the actual original MD5 anonymously", () => requests.Count == 1
                && requests.Single().Path == "/api/ir/v2/charts/" + bms_md5 && requests.Single().Bearer == null);
            AddStep("omit unavailable public metadata", () => { omitMetadata = true; detail.ShowOmsPage(chartRoute(bms_md5)); });
            AddUntilStep("matching local metadata supplies its explicit source", () => !isLoading(detail) && hasText(detail, local_title)
                && hasText(detail, local_artist) && hasText(detail, "曲名来源：本地索引") && hasText(detail, "艺术家来源：本地索引"));
        }

        [Test]
        public void TestMissingChartUsesOnlyMatchingLocalOriginal()
        {
            AddStep("select a different local chart", () => { setCurrentChart(new string('c', 32), "bms"); omitMetadata = true; });
            AddStep("read the public chart with missing metadata", () => detail.ShowOmsPage(chartRoute(bms_md5)));
            AddUntilStep("only the actual requested MD5 is shown", () => !isLoading(detail) && hasText(detail, bms_md5)
                && detail.ChildrenOfType<OmsChartInfo>().Any());
            AddAssert("a different selected chart cannot supply title artist or statistics", () => !hasText(detail, local_title)
                && !hasText(detail, local_artist) && !hasText(detail, "本地谱面数据") && !hasText(detail, "难度标注")
                && !hasText(detail, "BPM：") && buttonExists(detail, "查看成绩榜"));
            AddStep("read a matching original absent from the public directory", () =>
            {
                setCurrentChart(bms_md5, "bms", false);
                missingChart = true;
                detail.ShowOmsPage(chartRoute(bms_md5));
            });
            AddUntilStep("unlisted local chart remains useful", () => !isLoading(detail) && hasText(detail, local_title)
                && hasText(detail, "尚未收录公开成绩") && hasText(detail, "BPM：180"));
            AddAssert("an absent public board is not manufactured", () => !buttonExists(detail, "查看成绩榜")
                && buttonExists(detail, "查找 BMS 原包") && !hasText(detail, "作者 PLAYLEVEL") && !hasText(detail, "本地计算星级"));
            AddStep("same MD5 with a different original ruleset", () => { setCurrentChart(bms_md5, "mania"); detail.ShowOmsPage(chartRoute(bms_md5)); });
            AddUntilStep("ruleset mismatch cannot lend local metadata", () => !isLoading(detail) && hasText(detail, "尚未收录公开成绩")
                && !hasText(detail, local_title) && !hasText(detail, "本地谱面数据"));
            AddAssert("only the three requested MD5 reads occurred", () => requests.Count == 3 && legacyRequests.IsEmpty);
        }

        [Test]
        public void TestManiaShowsItsActualLocalStarsAndDownloadSearchLimit()
        {
            AddStep("select a real local mania chart", () => setCurrentChart(mania_md5, "mania"));
            AddStep("read its original source page", () => detail.ShowOmsPage(chartRoute(mania_md5, "mania")));
            AddUntilStep("mania local calculation and mode are rendered", () => !isLoading(detail)
                && hasText(detail, "公开 mania 原谱") && hasText(detail, "键型：mania 4k") && hasText(detail, "本地计算星级：★"));
            AddAssert("mania shows the exact local star value with its source", () => hasText(detail, "本地计算星级：★" + 2.75.ToString("0.##", CultureInfo.CurrentCulture))
                && !hasText(detail, "作者 PLAYLEVEL") && buttonExists(detail, "按歌名查找 mania 原包")
                && hasText(detail, "未提供原谱 MD5 反查") && hasText(detail, "歌名搜索只用于寻找候选"));
            AddStep("remove unavailable names and select another local chart", () =>
            {
                omitMetadata = true;
                setCurrentChart(new string('c', 32), "mania");
                detail.ShowOmsPage(chartRoute(mania_md5, "mania"));
            });
            AddUntilStep("unknown mania title stays the source MD5", () => !isLoading(detail) && hasText(detail, mania_md5)
                && !hasText(detail, "本地计算星级"));
            AddAssert("missing song title cannot become an MD5 download search", () => !buttonExists(detail, "按歌名查找 mania 原包")
                && !hasText(detail, local_title) && requests.Count == 2 && legacyRequests.IsEmpty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHideCancelsLateReadAndRestoreDoesNotReread(bool useListing)
        {
            IOmsPageOverlay page = null!;
            Drawable view = null!;
            OmsPageRoute initial = useListing ? new OmsPageRoute { Kind = OmsPageKind.Charts } : chartRoute(bms_md5);
            AddStep("hold an actual chart read", () =>
            {
                holdNextRead = true;
                page = useListing ? listing : detail;
                view = useListing ? listing : detail;
                page.ShowOmsPage(initial);
            });
            AddUntilStep("chart read is held at HTTP", () => delayedRead != null && isLoading(view));
            AddStep("hide the native page while reading", () => { if (useListing) listing.Hide(); else detail.Hide(); });
            AddUntilStep("hide cancels the transport", () => (useListing ? listing.State.Value : detail.State.Value) == Visibility.Hidden
                && delayedToken.IsCancellationRequested);
            AddStep("finish the obsolete response late", () => delayedRead!.TrySetResult(useListing ? charts(1, late_title) : chart(bms_md5, late_title)));
            AddUntilStep("obsolete HTTP read has finished", () => Volatile.Read(ref completedReads) == 1);
            AddWaitStep("allow obsolete rendering callbacks", 2);
            AddAssert("late read cannot populate or reopen a hidden page", () => !hasText(view, late_title)
                && !view.ChildrenOfType<OmsChartCard>().Any() && !view.ChildrenOfType<OmsChartInfo>().Any()
                && (useListing ? listing.State.Value : detail.State.Value) == Visibility.Hidden && requests.Count == 1);
            AddStep("open the requested page again", () => page.ShowOmsPage(initial));
            AddUntilStep("fresh explicit read renders native content", () => !isLoading(view)
                && (useListing ? view.ChildrenOfType<OmsChartCard>().Count() == 20 : hasText(view, "公开 BMS 原谱")));
            AddStep("hide the completed page", () => { if (useListing) listing.Hide(); else detail.Hide(); });
            AddStep("restore the successfully read page", () => page.RestoreOmsPage(initial, 0));
            AddUntilStep("cached content restored", () => !isLoading(view) && page.CurrentOmsRoute == initial
                && (useListing ? view.ChildrenOfType<OmsChartCard>().Count() == 20 : hasText(view, "公开 BMS 原谱")));
            AddAssert("cancelled read is not cached and completed restore uses no extra request", () => requests.Count == 2
                && !hasText(view, late_title) && legacyRequests.IsEmpty);
        }

        [Test]
        public void TestMismatchedMd5CannotBecomeTheRequestedDetail()
        {
            AddStep("return a different source identity", () => { wrongIdentity = true; detail.ShowOmsPage(chartRoute(bms_md5)); });
            AddUntilStep("wrong source chart is rejected visibly", () => !isLoading(detail) && hasText(detail, "页面数据不符合约定"));
            AddAssert("wrong identity creates no detail or board action", () => !detail.ChildrenOfType<OmsChartInfo>().Any()
                && !buttonExists(detail, "查看成绩榜") && detail.Header.BeatmapSet.Value == null && requests.Count == 1 && legacyRequests.IsEmpty);
        }

        private void setCurrentChart(string md5, string ruleset, bool hasPlaylevel = true)
        {
            var info = new BeatmapInfo(ruleset == "bms" ? new BmsRuleset().RulesetInfo : new ManiaRuleset().RulesetInfo)
            {
                MD5Hash = md5,
                Hash = new string('f', 64),
                LocalFilePath = ruleset == "bms" ? "synthetic-native.bms" : "synthetic-native.osu",
                DifficultyName = "本地 ANOTHER",
                Difficulty = new BeatmapDifficulty { CircleSize = ruleset == "bms" ? 7 : 4 },
                BPM = 180,
                Length = 123000,
                TotalObjectCount = 321,
                StarRating = ruleset == "bms" ? 9.25 : 2.75,
                Metadata = new BeatmapMetadata
                {
                    Title = local_title,
                    Artist = local_artist,
                    RulesetDataJson = ruleset == "bms" && hasPlaylevel ? "{\"chart_metadata\":{\"play_level\":\"★12\"}}" : string.Empty,
                },
            };
            Ruleset.Value = info.Ruleset;
            Beatmap.Value = CreateWorkingBeatmap(new Beatmap { BeatmapInfo = info });
        }

        private static OmsPageRoute chartRoute(string md5, string ruleset = "bms") => new OmsPageRoute { Kind = OmsPageKind.Chart, Md5 = md5, Ruleset = ruleset };
        private static bool isLoading(Drawable view) => view.ChildrenOfType<OmsChartPageContent>().Single().IsLoading;
        private static bool hasText(Drawable view, string text) => string.Concat(view.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Contains(text, StringComparison.Ordinal);
        private static OsuTextFlowContainer flow(Drawable view, string text) => view.ChildrenOfType<OsuTextFlowContainer>().Single(item => hasText(item, text));
        private static bool buttonExists(Drawable view, string label) => view.ChildrenOfType<OsuButton>().Any(item => item.Text.ToString() == label);
        private static OsuButton button(Drawable view, string label) => view.ChildrenOfType<OsuButton>().Single(item => item.Text.ToString() == label);

        private async Task<HttpResponseMessage> response(HttpRequestMessage request, CancellationToken token)
        {
            Uri uri = request.RequestUri!;
            requests.Enqueue(new RequestSnapshot(uri.AbsolutePath, uri.Query, request.Headers.Authorization?.Parameter));
            try
            {
                if (request.Method != HttpMethod.Get || (uri.AbsolutePath != "/api/ir/v2/charts" && !uri.AbsolutePath.StartsWith("/api/ir/v2/charts/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Unexpected synthetic native-chart request: " + request.Method + " " + uri.AbsolutePath);
                if (holdNextRead)
                {
                    holdNextRead = false;
                    delayedToken = token;
                    var pending = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                    delayedRead = pending;
                    // Deliberately finish after cancellation to exercise obsolete HTTP completion.
                    return json(await pending.Task.ConfigureAwait(false));
                }
                if (uri.AbsolutePath == "/api/ir/v2/charts")
                {
                    Dictionary<string, string> parameters = query(uri.Query);
                    return json(charts(int.Parse(parameters["page"], CultureInfo.InvariantCulture), parameters["q"]));
                }
                if (missingChart)
                    return json(new JObject { ["error"] = new JObject { ["code"] = "chart_not_found" } }, HttpStatusCode.NotFound);
                string md5 = wrongIdentity ? new string('e', 32) : uri.AbsolutePath[(uri.AbsolutePath.LastIndexOf('/') + 1)..];
                return json(chart(md5, omit: omitMetadata));
            }
            finally
            {
                Interlocked.Increment(ref completedReads);
            }
        }

        private static Dictionary<string, string> query(string value) => value.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]), StringComparer.Ordinal);

        private static JObject charts(int page, string search)
        {
            var items = new JArray();
            int first = page == 1 ? 1 : 21;
            int last = page == 1 ? 20 : 21;
            for (int index = first; index <= last; index++)
            {
                string title = index == 1 && search.Length > 0 ? search + " 搜索结果" : "合成谱面 " + index;
                // Real long metadata should wrap inside a horizontal card and leave a scrollable directory.
                items.Add(chart(index.ToString("x32", CultureInfo.InvariantCulture), title + " · " + new string('曲', 180)));
            }
            return new JObject { ["page"] = page, ["limit"] = 20, ["total"] = 21, ["items"] = items };
        }

        private static JObject chart(string md5, string? title = null, bool omit = false)
        {
            bool mania = md5 == mania_md5;
            return new JObject
            {
                ["chart"] = new JObject
                {
                    ["md5"] = md5,
                    ["title"] = omit ? JValue.CreateNull() : new JValue(title ?? (mania ? "公开 mania 原谱" : "公开 BMS 原谱")),
                    ["artist"] = omit ? JValue.CreateNull() : new JValue(title == null ? "公开艺术家" : "合成艺术家"),
                    ["difficulty"] = omit ? JValue.CreateNull() : new JValue(mania ? "4K Another" : "☆12"),
                },
                ["ruleset"] = mania ? "mania" : "bms",
                ["groups"] = new JArray(),
                ["available_sources"] = new JArray("oms", "lr2ir.v3.lr2"),
            };
        }

        private static HttpResponseMessage json(JObject document, HttpStatusCode status = HttpStatusCode.OK) => new HttpResponseMessage(status)
        {
            Content = new StringContent(document.ToString(), Encoding.UTF8, "application/json"),
        };

        protected override void Dispose(bool isDisposing)
        {
            delayedRead?.TrySetResult(chart(bms_md5, late_title));
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed record RequestSnapshot(string Path, string Query, string? Bearer);

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => this.respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            public OmsIrSession? Read(string target) => null;
            public void Write(string target, OmsIrSession value) => throw new InvalidOperationException("Anonymous chart pages must not create a session.");
            public void Delete(string target) { }
        }
    }
}
