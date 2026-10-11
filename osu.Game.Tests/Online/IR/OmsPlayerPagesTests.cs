// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.Rankings.Tables;

namespace osu.Game.Tests.Online.IR
{
    [TestFixture]
    [NonParallelizable]
    public class OmsPlayerPagesTests
    {
        private static readonly string condition_id = new string('c', 64);
        private static readonly string external_condition_id = new string('d', 64);
        private static readonly string chart_md5 = new string('a', 32);
        private const string exact_total = "9232379236109515775";
        private TemporaryNativeStorage storage = null!;
        private HttpClient http = null!;
        private OmsIrService service = null!;
        private OmsPlayerPageContent overlay = null!;
        private FillFlowContainer rows = null!;
        private long? displayedPlayer;
        private string? displayedName;
        private readonly ConcurrentQueue<(string Path, string Query, string? Bearer)> requests = new ConcurrentQueue<(string, string, string?)>();
        private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response = null!;

        [SetUp]
        public void SetUp()
        {
            requests.Clear();
            storage = new TemporaryNativeStorage("oms-public-player-pages-" + Guid.NewGuid().ToString("N"));
            response = (request, _) => Task.FromResult(json(defaultResponse(request)));
            http = new HttpClient(new Handler((request, token) =>
            {
                requests.Enqueue((request.RequestUri!.AbsolutePath, Uri.UnescapeDataString(request.RequestUri.Query), request.Headers.Authorization?.Parameter));
                return response(request, token);
            }));
            service = new OmsIrService(storage, http, new Credentials());
            overlay = new OmsPlayerPageContent();
            displayedPlayer = null;
            displayedName = null;
            overlay.PlayerLoaded += (id, name) => { displayedPlayer = id; displayedName = name; };
            typeof(OmsPageContent).GetProperty("service", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, service);
            rows = (FillFlowContainer)typeof(OmsPageContent).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        }

        [TearDown]
        public void TearDown()
        {
            overlay.Dispose();
            service.Dispose();
            http.Dispose();
            storage.Dispose();
        }

        [TestCase("bms")]
        [TestCase("mania")]
        public async Task OpeningWithoutAnActualKeymodeAsksForSelectionAndDoesNotInventOne(string ruleset)
        {
            Assert.That(requests, Is.Empty);
            (await load(new OmsPageRoute { Kind = OmsPageKind.Rankings, Ruleset = ruleset }))();
            Assert.That(requests.Select(request => request.Path), Is.EqualTo(new[] { "/api/ir/v2/sources" }));
            Assert.That(text(), Does.Contain("请选择实际键型"));
            Assert.That(overlay.Route.Keymode, Is.Null);
            Assert.That(overlay.Route.Sources, Is.EqualTo("oms"));
            Assert.That(choices().SelectMany(choice => choice.Items), Does.Not.Contain("lr2ir.v3.lr2"));
            if (ruleset == "bms") Assert.That(choices().SelectMany(choice => choice.Items), Does.Contain("pms_9k"));
            else Assert.That(choices().SelectMany(choice => choice.Items), Does.Contain("mania_18k"));
            Assert.That(requests.All(request => request.Bearer == null), Is.True);
        }

        [Test]
        public async Task RankingReadsOnlyItsPageAndKeepsTheRealFullPopulationSelfRankAndScopeOutsideTheOptionsPage()
        {
            await service.LoginAsync("synthetic_player", "synthetic-password-123");
            requests.Clear();
            (await load(new OmsPageRoute
            {
                Kind = OmsPageKind.Rankings,
                Keymode = "pms_9k",
                Page = 2,
                ScopePage = 2,
                Condition = condition_id,
            }))();
            Assert.That(requests.Count, Is.EqualTo(3));
            Assert.That(requests.Last().Path, Is.EqualTo("/api/ir/v1/rankings/players"));
            Assert.That(requests.Last().Query, Does.Contain("keymode=pms_9k").And.Contain("source=oms").And.Contain("metric=coverage")
                .And.Contain("condition=" + condition_id).And.Contain("page=2&limit=20"));
            Assert.That(requests.ElementAt(1).Query, Does.Contain("page=2&limit=20"));
            Assert.That(requests.Skip(1).All(request => request.Bearer == "synthetic-access-token"), Is.True);
            OmsPlayerRankingsTable[] tables = rows.Children.OfType<OmsPlayerRankingsTable>().ToArray();
            Assert.That(tables.Length, Is.EqualTo(2));
            Assert.That(tablePlayers(tables[0]).Single().Rank, Is.EqualTo(9));
            Assert.That(tablePlayers(tables[0]).Single().User.Id, Is.EqualTo(service.State.Account!.Id));
            Assert.That(tablePlayers(tables[1]).Select(player => player.Rank), Is.EqualTo(Enumerable.Range(21, 20).Select(rank => (long)rank)));
            Assert.That(tablePlayers(tables[1])[0].User.Id, Is.EqualTo(long.MaxValue));
            Assert.That(buttons().Any(button => button.Text.ToString().StartsWith('#')), Is.False);
            Assert.That(text(), Does.Contain("我的全体名次"));
            Assert.That(choices().Single(choice => choice.Current.Value == condition_id).Items, Does.Contain(condition_id));
            Assert.That(text(), Does.Contain("条件：共 21 项 · 第 2 / 2 页").And.Contain("第 2 / 3 页"));
            Assert.That(overlay.Route.Condition, Is.EqualTo(condition_id));
            Assert.That(requests.Any(request => request.Path.Contains("/users/", StringComparison.Ordinal)), Is.False,
                "Twenty ranking rows must not trigger twenty profile reads.");
        }

        [TestCase("mania_1k")]
        [TestCase("mania_10k")]
        [TestCase("mania_18k")]
        public async Task ManiaKeepsActualColumnsAndAnExactCumulativeValueBeyondInt64(string keymode)
        {
            (await load(new OmsPageRoute { Kind = OmsPageKind.Rankings, Ruleset = "mania", Keymode = keymode, Condition = condition_id }))();
            Assert.That(requests.Last().Query, Does.Contain("keymode=" + keymode).And.Contain("metric=best_total_score"));
            IReadOnlyList<OmsPlayerRank> players = tablePlayers(rows.Children.OfType<OmsPlayerRankingsTable>().Single());
            Assert.That(players.Select(player => player.Value), Is.All.EqualTo(exact_total));
            Assert.That(players.Select(player => player.Rank), Is.All.EqualTo(1), "Tied global ranks must not become row positions.");
            Assert.That(text(), Does.Contain("精确累加").And.Contain("含未通过局"));
            Assert.That(choices().SelectMany(choice => choice.Items), Does.Not.Contain("lr2oraja_ed"));
        }

        [TestCase("bms", "cleared_charts", "bms_7k")]
        [TestCase("mania", "best_total_score", "mania_4k")]
        [TestCase("mania", "coverage", "mania_4k")]
        public async Task AComparisonRequiringConditionsWaitsForAnActualPublishedChoice(string ruleset, string metric, string keymode)
        {
            (await load(new OmsPageRoute { Kind = OmsPageKind.Rankings, Ruleset = ruleset, Metric = metric, Keymode = keymode }))();
            Assert.That(requests.Select(request => request.Path), Is.EqualTo(new[] { "/api/ir/v2/sources", "/api/ir/v1/rankings/scopes" }));
            Assert.That(text(), Does.Contain("请选择已公布的实际条件"));
            Assert.That(text(), Does.Not.Contain("共 0 项"));
        }

        [TestCase("oms,")]
        [TestCase("lr2ir.v3.lr2")]
        [TestCase("oms,lr2oraja_ed")]
        public void RankingRejectsAnEmptyTokenHistoricalIdentityOrMultipleSources(string sources)
        {
            OmsIrException exception = Assert.ThrowsAsync<OmsIrException>(async () => await load(new OmsPageRoute
            {
                Kind = OmsPageKind.Rankings,
                Keymode = "bms_7k",
                Sources = sources,
            }))!;
            Assert.That(exception.Code, Is.EqualTo("invalid_sources"));
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [TestCase(OmsPageKind.Rankings)]
        [TestCase(OmsPageKind.Profile)]
        public async Task RepeatedValidSourcesUseTheSameCanonicalSelectionAsTheServer(OmsPageKind kind)
        {
            (await load(new OmsPageRoute { Kind = kind, UserId = 42, Keymode = "bms_7k", Sources = "oms,oms" }))();
            Assert.That(overlay.Route.Sources, Is.EqualTo("oms"));
            Assert.That(requests.Last().Query, Does.Not.Contain("oms,oms"));
            Assert.That(requests.Last().Query, Does.Contain(kind == OmsPageKind.Rankings ? "source=oms" : "sources=oms"));
        }

        [Test]
        public async Task AProfileWithUnknownKeysOnlyReadsItsStable64BitPublicIdentity()
        {
            (await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = long.MaxValue }))();
            Assert.That(requests.Select(request => request.Path), Is.EqualTo(new[] { "/api/ir/v2/sources", "/api/ir/v1/users/9223372036854775807/profile" }));
            Assert.That(displayedPlayer, Is.EqualTo(long.MaxValue));
            Assert.That(displayedName, Is.EqualTo("synthetic_player"));
            Assert.That(text(), Does.Contain("请选择实际键型"));
            Assert.That(requests.All(request => request.Bearer == null), Is.True);
        }

        [TestCase("id")]
        [TestCase("username")]
        public void AProfileWithoutItsRealPublicIdentityCannotPopulateTheHeader(string field)
        {
            response = (request, _) =>
            {
                JObject result = defaultResponse(request);
                if (request.RequestUri!.AbsolutePath.EndsWith("/profile", StringComparison.Ordinal))
                    ((JObject)result["user"]!).Remove(field);
                return Task.FromResult(json(result));
            };
            Assert.ThrowsAsync<JsonSerializationException>(async () => await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 42 }));
            Assert.That(displayedPlayer, Is.Null);
            Assert.That(displayedName, Is.Null);
            Assert.That(rows.Children, Is.Empty);
            Assert.That(requests.Count, Is.EqualTo(2));
        }

        [TestCase(1L, true)]
        [TestCase(42L, false)]
        public async Task OnlyTheActualAccountOffersItsPrivateHistoryWhileThePageReadsPublicData(long userId, bool ownAccount)
        {
            await service.LoginAsync("synthetic_player", "synthetic-password-123");
            requests.Clear();
            (await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = userId, Keymode = "bms_7k" }))();
            Assert.That(buttons().Any(button => button.Text.ToString() == "我的完整游玩记录"), Is.EqualTo(ownAccount));
            Assert.That(requests.Select(request => request.Path), Is.EqualTo(new[]
            {
                "/api/ir/v2/sources",
                $"/api/ir/v1/users/{userId}/profile",
                $"/api/ir/v1/users/{userId}/performance",
                $"/api/ir/v1/users/{userId}/public-bests",
            }));
            Assert.That(displayedPlayer, Is.EqualTo(userId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task PublicProfileReadsOnlyTheSelectedTabAndPreservesExternalLampsAndReceptionTimes(bool recent)
        {
            var requested = new OmsPageRoute
            {
                Kind = OmsPageKind.Profile,
                UserId = long.MaxValue,
                Keymode = "bms_7k",
                Sources = "lr2oraja_ed,oms",
                Condition = external_condition_id,
                Recent = recent,
                Page = 2,
            };
            (await load(requested))();
            Assert.That(requests.Count, Is.EqualTo(4));
            Assert.That(requests.Last().Path, Does.EndWith(recent ? "/public-recent" : "/public-bests"));
            Assert.That(requests.Last().Query, Does.Contain("sources=oms,lr2oraja_ed").And.Contain("condition=" + external_condition_id).And.Contain("page=2&limit=20"));
            Assert.That(requests.ElementAt(2).Path, Does.EndWith("/performance"));
            Assert.That(requests.ElementAt(2).Query, Does.Not.Contain("condition="), "Performance totals are not condition-filtered by this API.");
            Assert.That(statistics(), Is.EqualTo(new[] { ("公开谱面", "12"), ("当前最佳条件条目", "31") }));
            Assert.That(text(), Does.Contain("本条最高分记录灯：Normal").And.Contain("独立最佳灯：Hard")
                .And.Contain("不合成为同一局成绩").And.Contain("游玩时间：未收录").And.Contain("服务器接收时间："));
            Assert.That(text(), Does.Contain("准确率：未开放").And.Contain("未证明字段"));
            if (recent) Assert.That(text(), Does.Contain("最近更新类型：最佳状态改善"));
            Assert.That(requests.Any(request => request.Path.Contains("/scores/user/", StringComparison.Ordinal)), Is.False);
            Assert.That(overlay.Route.Sources, Is.EqualTo("oms,lr2oraja_ed"));
            var chartRoute = (OmsPageRoute)typeof(OmsPlayerPageContent).GetMethod("playerChartRoute", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { overlay.Route, chart_md5, "合成谱面" })!;
            Assert.That(chartRoute.Kind, Is.EqualTo(OmsPageKind.Chart));
            Assert.That(chartRoute.Md5, Is.EqualTo(chart_md5));
            Assert.That(chartRoute.Sources, Is.EqualTo(overlay.Route.Sources));
            Assert.That(chartRoute.Keymode, Is.EqualTo("bms_7k"));
            Assert.That(chartRoute.Condition, Is.Null, "A cross-chart player condition must not become a chart condition.");
            Assert.That(chartRoute.Group, Is.Null);
            Assert.That(chartRoute.Mode, Is.EqualTo("reference"));
        }

        [Test]
        public async Task ManiaProfilePreservesRealHighestScoreFailureAndIndependentPlayedAndReceivedDates()
        {
            (await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 42, Ruleset = "mania", Keymode = "mania_10k", Recent = true }))();
            Assert.That(text(), Does.Contain("累计公开最佳分：" + exact_total).And.Contain("总分：9007199254740991")
                .And.Contain("计分版本：30000016").And.Contain("本条记录：未通过").And.Contain("准确率：90"));
            Assert.That(text(), Does.Contain("游玩时间：").And.Contain("服务器接收时间：").And.Contain("累计分包含未通过局"));
            Assert.That(text(), Does.Not.Contain("独立最佳灯"));
            Assert.That(requests.Last().Query, Does.Contain("keymode=mania_10k").And.Contain("sources=oms"));
        }

        [Test]
        public async Task ExplicitEmptySourcesRemainEmptyAndProduceNoInventedPlayCountOrRank()
        {
            (await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 42, Keymode = "bms_7k", Sources = string.Empty }))();
            Assert.That(requests.Skip(2).All(request => request.Query.Contains("sources=", StringComparison.Ordinal)
                && !request.Query.Contains("sources=oms", StringComparison.Ordinal)), Is.True);
            Assert.That(overlay.Route.Sources, Is.Empty);
            Assert.That(text(), Does.Contain("未选择来源"));
            Assert.That(statistics(), Is.EqualTo(new[] { ("公开谱面", "0"), ("当前最佳条件条目", "0") }));
            Assert.That(text(), Does.Not.Contain("#0").And.Not.Contain("游玩次数"));
        }

        [Test]
        public void DuplicatePublishedConditionsAreRejectedBeforeRequestingAPlayerRanking()
        {
            response = (request, _) =>
            {
                JObject result = defaultResponse(request);
                if (request.RequestUri!.AbsolutePath.EndsWith("/rankings/scopes", StringComparison.Ordinal))
                {
                    ((JArray)result["items"]!).Add(((JArray)result["items"]!)[0].DeepClone());
                    result["total"] = 2;
                }
                return Task.FromResult(json(result));
            };
            Assert.ThrowsAsync<JsonSerializationException>(async () => await load(new OmsPageRoute { Kind = OmsPageKind.Rankings, Keymode = "bms_7k" }));
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(rows.Children, Is.Empty);
        }

        [TestCase("owner")]
        [TestCase("time")]
        [TestCase("items")]
        public void InvalidProfileResponsesFailBeforeAnyPartialPageCanBeRendered(string failure)
        {
            response = (request, _) =>
            {
                JObject result = defaultResponse(request);
                if (request.RequestUri!.AbsolutePath.EndsWith("/public-bests", StringComparison.Ordinal))
                {
                    if (failure == "owner") result["user"]!["id"] = 99;
                    else if (failure == "time") result["items"]![0]!["score"]!["received_at"] = "unknown time";
                    else result["items"] = new JArray("invalid public row");
                }
                return Task.FromResult(json(result));
            };
            Assert.ThrowsAsync<JsonSerializationException>(async () => await load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 42, Keymode = "bms_7k" }));
            Assert.That(rows.Children, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ADecimalMetricCannotBeRoundedFromAnExponentOrFloatingPointValue(bool numeric)
        {
            response = (request, _) =>
            {
                JObject result = defaultResponse(request);
                if (request.RequestUri!.AbsolutePath.EndsWith("/rankings/players", StringComparison.Ordinal))
                    result["items"]![0]!["value"] = numeric ? new JValue(9.2e18) : new JValue("9.2e18");
                return Task.FromResult(json(result));
            };
            Assert.ThrowsAsync<JsonSerializationException>(async () => await load(new OmsPageRoute
            {
                Kind = OmsPageKind.Rankings,
                Ruleset = "mania",
                Keymode = "mania_4k",
                Condition = condition_id,
            }));
        }

        [Test]
        public async Task CancellationDuringPerformanceDoesNotStartThePublicRecordsRequest()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/performance", StringComparison.Ordinal))
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(token);
                }
                return json(defaultResponse(request));
            };
            using var cancellation = new CancellationTokenSource();
            Task<Action> pending = load(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = 42, Keymode = "bms_7k" }, cancellation.Token);
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.That(requests.Any(request => request.Path.EndsWith("/public-bests", StringComparison.Ordinal)), Is.False);
                Assert.That(rows.Children, Is.Empty);
            }
            finally
            {
                cancellation.Cancel();
                release.TrySetResult();
            }
        }

        [Test]
        public void ServerWaitingIsPropagatedWithItsRealRetryAfterWithoutImmediatePageRetries()
        {
            response = (request, _) =>
            {
                if (!request.RequestUri!.AbsolutePath.EndsWith("/rankings/scopes", StringComparison.Ordinal)) return Task.FromResult(json(defaultResponse(request)));
                HttpResponseMessage waiting = json(new JObject { ["error"] = new JObject { ["code"] = "rate_limited", ["message"] = "稍后重试" } }, HttpStatusCode.TooManyRequests);
                waiting.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
                return Task.FromResult(waiting);
            };
            OmsIrException exception = Assert.ThrowsAsync<OmsIrException>(async () => await load(new OmsPageRoute { Kind = OmsPageKind.Rankings, Keymode = "bms_7k" }))!;
            Assert.That(exception.Code, Is.EqualTo("rate_limited"));
            Assert.That(exception.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(requests.Count, Is.EqualTo(2));
        }

        private Task<Action> load(OmsPageRoute requested, CancellationToken token = default)
            => (Task<Action>)typeof(OmsPlayerPageContent).GetMethod(requested.Kind == OmsPageKind.Rankings ? "LoadRankingsAsync" : "LoadProfileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(overlay, new object[] { requested, token })!;

        private string text()
        {
            // These unit pages have no host update loop; materialise the real framework text parts before reading their drawables.
            OsuTextFlowContainer[] flows = components().OfType<OsuTextFlowContainer>().Distinct().ToArray();
            foreach (OsuTextFlowContainer flow in flows)
                typeof(TextFlowContainer).GetMethod("RecreateAllParts", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(flow, null);
            return string.Join('\n', flows
                .Select(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString()))));
        }
        private IEnumerable<OsuButton> buttons() => components().OfType<OsuButton>().Distinct();
        private IEnumerable<OsuDropdown<string>> choices() => components().OfType<OsuDropdown<string>>().Distinct();

        private IEnumerable<Drawable> components()
        {
            foreach (Drawable child in rows.Children)
            {
                yield return child;
                foreach (Drawable descendant in child.ChildrenOfType<Drawable>()) yield return descendant;
                // Grid cells are created from their real Content during load, which these data tests do not run.
                if (child is not GridContainer grid) continue;
                foreach (Drawable cell in grid.Content.SelectMany(row => row))
                {
                    yield return cell;
                    foreach (Drawable descendant in cell.ChildrenOfType<Drawable>()) yield return descendant;
                }
            }
        }

        private IEnumerable<(string Label, string Count)> statistics()
            => rows.Children.OfType<GridContainer>().Single(grid => grid.Content[0].Count == 2).Content[0]
                   .Cast<FillFlowContainer>().Select(column =>
                   {
                       OsuTextFlowContainer[] texts = column.Children.OfType<OsuTextFlowContainer>().ToArray();
                       return (flowText(texts[0]), flowText(texts[1]));
                   });

        private static string flowText(OsuTextFlowContainer flow)
        {
            typeof(TextFlowContainer).GetMethod("RecreateAllParts", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(flow, null);
            return string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString()));
        }

        private static IReadOnlyList<OmsPlayerRank> tablePlayers(OmsPlayerRankingsTable table)
            => (IReadOnlyList<OmsPlayerRank>)typeof(OmsPlayerRankingsTable).GetField("players", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(table)!;

        private static JObject defaultResponse(HttpRequestMessage request)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/api/ir/v2/sources") return new JObject { ["items"] = new JArray(source("oms", "play"), source("lr2oraja_ed", "best_state"), source("lr2ir.v3.lr2", "archive_best")) };
            if (path.EndsWith("/auth/login", StringComparison.Ordinal)) return new JObject
            {
                ["user"] = user(1),
                ["access_token"] = "synthetic-access-token",
                ["refresh_token"] = "synthetic-refresh",
                ["expires_in"] = 3600,
            };
            Dictionary<string, string> query = parameters(request.RequestUri.Query);
            string ruleset = query.GetValueOrDefault("ruleset", "bms");
            string keymode = query.GetValueOrDefault("keymode", "bms_7k");
            string selected = query.GetValueOrDefault("sources", "oms");
            int page = int.Parse(query.GetValueOrDefault("page", "1"), CultureInfo.InvariantCulture);
            var scope = new JObject { ["ruleset"] = ruleset, ["keymode"] = keymode };
            if (path.EndsWith("/rankings/scopes", StringComparison.Ordinal))
            {
                scope["source"] = query["source"];
                return new JObject
                {
                    ["scope"] = scope,
                    ["items"] = new JArray(condition(page == 2 ? external_condition_id : condition_id, "oms")),
                    ["page"] = page,
                    ["limit"] = 20,
                    ["total"] = page == 2 ? 21 : 1,
                    ["snapshot"] = snapshot(),
                };
            }
            if (path.EndsWith("/rankings/players", StringComparison.Ordinal))
            {
                scope["source"] = query["source"];
                scope["metric"] = query["metric"];
                scope["condition_scope"] = query.TryGetValue("condition", out string? conditionValue) ? condition(conditionValue, "oms") : JValue.CreateNull();
                scope["basis"] = "current-public-bests";
                scope["rating"] = false;
                return new JObject
                {
                    ["scope"] = scope,
                    ["items"] = new JArray(Enumerable.Range(0, 20).Select(index => rank(index == 0 ? long.MaxValue : 1000 + index,
                        ruleset == "mania" ? 1 : (page - 1) * 20 + index + 1, ruleset == "mania" ? exact_total : (1000 - index).ToString(CultureInfo.InvariantCulture),
                        ruleset == "mania" ? 1025 : 1000 - index))),
                    ["me"] = request.Headers.Authorization == null ? JValue.CreateNull() : rank(1, 9, "1012", 1012),
                    ["page"] = page,
                    ["limit"] = 20,
                    ["total"] = 41,
                    ["trust"] = ruleset == "mania" ? "client-reported" : "unverified-reference",
                    ["snapshot"] = snapshot(),
                };
            }
            if (!path.StartsWith("/api/ir/v1/users/", StringComparison.Ordinal)) throw new AssertionException("Unexpected player HTTP request: " + path);
            long owner = long.Parse(path.Split('/')[5], CultureInfo.InvariantCulture);
            if (path.EndsWith("/profile", StringComparison.Ordinal)) return new JObject { ["user"] = user(owner) };
            scope["selected_sources"] = new JArray(selected.Split(',', StringSplitOptions.RemoveEmptyEntries));
            bool empty = selected.Length == 0;
            if (path.EndsWith("/performance", StringComparison.Ordinal)) return new JObject
            {
                ["user"] = user(owner),
                ["scope"] = scope,
                ["totals"] = new JObject
                {
                    ["public_chart_count"] = empty ? 0 : ruleset == "mania" ? 1025 : selected.Contains(',') ? 12 : selected == "oms" ? 8 : 10,
                    ["public_best_count"] = empty ? 0 : ruleset == "mania" ? 1025 : selected.Contains(',') ? 31 : selected == "oms" ? 10 : 21,
                },
                ["lanes"] = new JArray(selected.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(code => lane(code, ruleset))),
                ["trust"] = ruleset == "mania" ? "client-reported" : "unverified-reference",
                ["snapshot"] = snapshot(),
            };
            bool recent = path.EndsWith("/public-recent", StringComparison.Ordinal);
            if (!recent && !path.EndsWith("/public-bests", StringComparison.Ordinal)) throw new AssertionException("Unexpected private or unrelated read: " + path);
            scope["condition_id"] = query.TryGetValue("condition", out string? chosen) ? chosen : JValue.CreateNull();
            scope["record_basis"] = "current-public-bests";
            string recordSource = chosen == external_condition_id || selected == "lr2oraja_ed" ? "lr2oraja_ed" : "oms";
            return new JObject
            {
                ["user"] = user(owner),
                ["scope"] = scope,
                ["items"] = empty ? new JArray() : new JArray(best(recordSource, ruleset, recent)),
                ["page"] = page,
                ["limit"] = 20,
                ["total"] = empty ? 0 : 21,
                ["trust"] = ruleset == "mania" ? "client-reported" : "unverified-reference",
                ["snapshot"] = snapshot(),
            };
        }

        private static Dictionary<string, string> parameters(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length == 1 ? string.Empty : Uri.UnescapeDataString(pair[1]), StringComparer.Ordinal);

        private static JObject source(string code, string kind) => new JObject { ["code"] = code, ["label"] = code == "oms" ? "OMS" : code, ["record_kind"] = kind, ["available"] = true };
        private static JObject user(long id) => new JObject { ["id"] = id, ["username"] = "synthetic_player" };
        private static JObject snapshot() => new JObject { ["read_at"] = "2026-10-11T10:00:00Z", ["dataset"] = "live-public-bests" };

        private static JObject condition(string id, string source) => new JObject
        {
            ["id"] = id,
            ["label"] = source + " · 实际条件",
            ["comparison"] = source == "oms" ? "same-oms-rules" : "unverified-source-condition",
            ["conditions"] = new JObject(),
            ["unknown_fields"] = new JArray("cross_player_parity"),
        };

        private static JObject rank(long id, int position, string value, int charts) => new JObject
        {
            ["rank"] = position,
            ["user"] = user(id),
            ["value"] = value,
            ["public_chart_count"] = charts,
            ["public_best_count"] = charts,
        };

        private static JObject lane(string source, string ruleset) => new JObject
        {
            ["source"] = source,
            ["condition_scope"] = condition(source == "oms" ? condition_id : external_condition_id, source),
            ["metrics"] = new JObject
            {
                ["public_chart_count"] = ruleset == "mania" ? 1025 : source == "oms" ? 8 : 10,
                ["public_best_count"] = ruleset == "mania" ? 1025 : source == "oms" ? 10 : 21,
                ["cleared_chart_count"] = 6,
                ["best_total_score"] = ruleset == "mania" ? exact_total : JValue.CreateNull(),
            },
            ["rankings"] = new JArray(new JObject { ["metric"] = ruleset == "mania" ? "best_total_score" : "cleared_charts", ["rank"] = 3, ["total_players"] = 60 }),
        };

        private static JObject best(string source, string ruleset, bool recent)
        {
            bool external = source != "oms";
            var score = new JObject
            {
                ["source"] = source,
                ["record_kind"] = external ? "best_state" : "play",
                ["record_id"] = external ? "external:99" : "oms:99",
                ["ex_score"] = ruleset == "bms" ? 175 : JValue.CreateNull(),
                ["max_ex_score"] = ruleset == "bms" ? 200 : JValue.CreateNull(),
                ["total_score"] = ruleset == "mania" ? 9007199254740991L : JValue.CreateNull(),
                ["total_score_version"] = ruleset == "mania" ? 30000016 : JValue.CreateNull(),
                ["accuracy"] = external ? JValue.CreateNull() : 0.9,
                ["passed"] = external ? JValue.CreateNull() : false,
                ["played_at"] = external ? JValue.CreateNull() : "2026-10-01T01:00:00Z",
                ["received_at"] = "2026-10-11T09:00:00Z",
                ["conditions"] = new JObject(),
                ["unknown_fields"] = new JArray("cross_player_parity"),
                ["lamp"] = ruleset == "mania" ? JValue.CreateNull() : new JObject { ["family"] = "original:200", ["value"] = 5, ["label"] = external ? "Normal" : "HARD CLEAR" },
            };
            var item = new JObject
            {
                ["chart"] = new JObject { ["md5"] = chart_md5, ["title"] = "合成谱面", ["artist"] = "合成作者", ["difficulty"] = "合成难度", ["sha256"] = JValue.CreateNull() },
                ["ruleset"] = ruleset,
                ["condition_scope"] = condition(external ? external_condition_id : condition_id, source),
                ["score"] = score,
                ["best_lamps"] = ruleset == "mania" ? new JArray() : new JArray(new JObject
                {
                    ["family"] = "original:200",
                    ["value"] = 6,
                    ["label"] = external ? "Hard" : "EX HARD",
                    ["source"] = source,
                    ["record_id"] = external ? "external:99:lamp" : "oms:100",
                    ["rule_label"] = "原来源 · 200 最大 EX · 血条历程未知",
                }),
            };
            if (recent) item["activity"] = new JObject
            {
                ["kind"] = external ? "best-state-updated" : "public-best-received",
                ["order_at"] = score["received_at"]!.DeepClone(),
                ["played_at"] = score["played_at"]!.DeepClone(),
            };
            return item;
        }

        private static HttpResponseMessage json(JObject value, HttpStatusCode status = HttpStatusCode.OK)
            => new HttpResponseMessage(status) { Content = new StringContent(value.ToString(Formatting.None), Encoding.UTF8, "application/json") };

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => this.respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            private readonly Dictionary<string, OmsIrSession> values = new Dictionary<string, OmsIrSession>();
            public OmsIrSession? Read(string target) => values.GetValueOrDefault(target);
            public void Write(string target, OmsIrSession session) => values[target] = session;
            public void Delete(string target) => values.Remove(target);
        }
    }
}
