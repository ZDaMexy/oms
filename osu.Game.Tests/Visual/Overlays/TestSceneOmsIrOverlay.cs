// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsIrOverlay : OsuManualInputManagerTestScene
    {
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private OmsIrOverlay overlay = null!;
        private int requests;
        private bool malformed;
        private bool wrongManiaChart;
        private bool holdBoard;
        private TaskCompletionSource<JObject>? delayedBoard;
        private string lastBoardMd5 = string.Empty;
        private string lastBoardQuery = string.Empty;

        [BackgroundDependencyLoader]
        private void load()
        {
            http = new HttpClient(new Handler(async (request, token) =>
            {
                Interlocked.Increment(ref requests);
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                    return new JObject
                    {
                        ["user"] = new JObject { ["id"] = 1, ["username"] = "ir_ui_synthetic" },
                        ["access_token"] = "synthetic-access",
                        ["refresh_token"] = "synthetic-refresh",
                        ["expires_in"] = 3600,
                    };
                if (path.EndsWith("/auth/logout", StringComparison.Ordinal))
                    return new JObject();
                if (path == "/api/ir/v2/sources")
                    return new JObject { ["items"] = new JArray(source("oms", "OMS"), source("lr2oraja_ed", "ED"), source("lr2ir.v3.lr2", "LR2IR 历史")) };
                if (path.StartsWith("/api/ir/v2/charts/", StringComparison.Ordinal))
                {
                    JObject maniaChart = chart();
                    maniaChart["md5"] = wrongManiaChart ? new string('f', 32) : path.Split('/').Last();
                    return new JObject
                    {
                        ["chart"] = maniaChart,
                        ["ruleset"] = "mania",
                        ["groups"] = new JArray(new JObject { ["id"] = new string('c', 64), ["label"] = "mania · 原计分条件" }),
                    };
                }
                if (malformed)
                    return new JObject { ["items"] = new JArray("incorrect server record"), ["total"] = 1, ["limit"] = 20 };
                if (path.StartsWith("/api/ir/v2/scores/chart/", StringComparison.Ordinal))
                {
                    lastBoardMd5 = path[(path.LastIndexOf('/') + 1)..];
                    lastBoardQuery = request.RequestUri.Query;
                    JObject result = referenceBoard(request);
                    if (holdBoard)
                    {
                        holdBoard = false;
                        delayedBoard = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                        // Deliberately ignore cancellation here to exercise a genuinely late HTTP completion.
                        return await delayedBoard.Task;
                    }
                    return result;
                }
                JObject response = new JObject { ["page"] = 1, ["limit"] = 20, ["total"] = 1 };
                if (path.EndsWith("/charts", StringComparison.Ordinal))
                {
                    JObject archivedChart = chart();
                    archivedChart["title"] = JValue.CreateNull();
                    JObject maniaChart = chart();
                    maniaChart["md5"] = new string('1', 32);
                    response["total"] = 2;
                    response["items"] = new JArray(new JObject { ["chart"] = archivedChart, ["ruleset"] = "bms", ["groups"] = new JArray(), ["available_sources"] = new JArray("oms", "lr2ir.v3.lr2") }, new JObject
                    {
                        ["chart"] = maniaChart,
                        ["ruleset"] = "mania",
                        ["groups"] = new JArray(new JObject { ["id"] = new string('c', 64), ["label"] = "mania · 原计分条件" }),
                    });
                }
                else if (path.Contains("/scores/chart/", StringComparison.Ordinal))
                {
                    JObject mania = score();
                    mania["ruleset"] = "mania";
                    mania["keymode"] = "mania_7k";
                    mania["total_score"] = 900000;
                    mania["ruleset_data"] = JValue.CreateNull();
                    response["items"] = new JArray(new JObject
                    {
                        ["rank"] = 1,
                        ["user"] = new JObject { ["id"] = 1, ["username"] = "ir_ui_synthetic" },
                        ["score"] = mania,
                    });
                }
                else
                    response["items"] = new JArray(score());
                return response;
            }));
            Dependencies.Cache(service = new OmsIrService(LocalStorage.GetStorageForDirectory("ir-ui-synthetic"), http, new Credentials()));
        }

        [Test]
        public void TestSourceBoardsAndMalformedResponse()
        {
            AddStep("show IR", () =>
            {
                Child = overlay = new OmsIrOverlay();
                overlay.Show();
            });
            AddUntilStep("IR controls loaded", () => overlay.IsLoaded);
            AddAssert("default service is OMSIR", () => service.State.ServiceAddress, () => Is.EqualTo(OmsIrService.ServiceOrigin.AbsoluteUri));
            AddAssert("offline startup has no website fallback", () => overlay.WebsiteUrl, () => Is.Null);
            AddAssert("default view makes no request", () => requests, () => Is.Zero);
            AddAssert("IR has no duplicate account form", () => !overlay.ChildrenOfType<OsuPasswordTextBox>().Any() && !buttonExists("本人记录"));
            AddAssert("no generic search or connection step", () => !buttonExists("查找谱面") && !buttonExists("账号与连接"));
            AddStep("guest opens the chosen chart", () => overlay.ShowChartBoard(new string('a', 32), "合成 IR 谱面"));
            AddUntilStep("guest can read public scores", () => hasText("共 3 位玩家") && hasText("EX 175 / 200") && button("刷新").Enabled.Value);
            AddAssert("public board does not log in or capture submissions", () => service.State.Account == null && service.CaptureSubmissionTarget() == null);
            AddStep("login existing account", () => service.LoginAsync("ir_ui_synthetic", "synthetic-password-123").GetAwaiter().GetResult());
            AddUntilStep("existing account visible", () => service.State.Account != null && button("刷新").Enabled.Value);
            AddStep("refresh chosen chart for this account", () => click("刷新"));
            AddUntilStep("all sources and independent lamps appear", () => hasText("共 3 位玩家") && hasText("EX 175 / 200") && hasText("FULL COMBO") && hasText("分数与该灯来自不同记录"));
            AddAssert("historical same name keeps its identity and original lamp", () => hasText("LR2IR 旧身份 #1 · 未关联 OMS") && hasText("★FULLCOMBO · 灯规则未知") && hasText("EX 199 / 未收录"));
            AddAssert("own full-range rank is shown independently", () => hasText("我的名次（所选来源）") && hasText("#3 · ir_ui_synthetic"));
            AddUntilStep("board request completed", () => button("刷新").Enabled.Value);
            AddStep("exclude historical source", () => sourceCheckbox("LR2IR 历史").Current.Value = false);
            AddUntilStep("selected sources determine count and global rank", () => hasText("共 2 位玩家") && hasText("#2 · ir_ui_synthetic") && !hasText("★FULLCOMBO") && button("刷新").Enabled.Value);
            AddStep("narrow to proven conditions", () => click("同条件 · 7K · 普通 · 合成条件"));
            AddUntilStep("explicit same-condition range excludes other conditions", () => hasText("共 1 位玩家") && hasText("同条件榜") && !hasText("EX 190") && button("刷新").Enabled.Value);
            AddAssert("chosen condition is sent explicitly", () => lastBoardQuery.Contains("mode=comparable", StringComparison.Ordinal)
                && Uri.UnescapeDataString(lastBoardQuery).Contains("condition=" + new string('c', 64) + ":200", StringComparison.Ordinal));
            AddAssert("website keeps the selected condition and sources", () => overlay.WebsiteUrl,
                () => Is.EqualTo(OmsWebsite.ChartBoard(service.State.ServiceAddress, new string('a', 32), "bms", "comparable", new string('c', 64) + ":200", new[] { "oms", "lr2oraja_ed" })));
            AddStep("remove OMS while a same-condition board is chosen", () => sourceCheckbox("OMS").Current.Value = false);
            AddUntilStep("new source scope reads its actual best and count", () => hasText("共 1 位玩家") && hasText("EX 190") && !hasText("EX 175")
                && hasText("参考混榜 · 按原 EX") && button("刷新").Enabled.Value);
            AddAssert("source change clears the old condition and resets the page", () => lastBoardQuery.Contains("mode=reference", StringComparison.Ordinal)
                && lastBoardQuery.Contains("page=1", StringComparison.Ordinal) && !lastBoardQuery.Contains("condition=", StringComparison.Ordinal)
                && Uri.UnescapeDataString(lastBoardQuery).Contains("sources=lr2oraja_ed", StringComparison.Ordinal));
            AddStep("add OMS back to the source choice", () => sourceCheckbox("OMS").Current.Value = true);
            AddUntilStep("reference scope returns with OMS", () => hasText("共 2 位玩家") && hasText("EX 175") && button("刷新").Enabled.Value);
            AddStep("choose the proven condition again", () => click("同条件 · 7K · 普通 · 合成条件"));
            AddUntilStep("same-condition board returns", () => hasText("共 1 位玩家") && hasText("同条件榜") && !hasText("EX 190") && button("刷新").Enabled.Value);
            AddStep("return to reference", () => click("返回参考混榜"));
            AddUntilStep("reference restored", () => hasText("共 2 位玩家") && button("刷新").Enabled.Value);
            AddStep("keep an empty source choice", () => click("清空来源"));
            AddUntilStep("empty choice is not silently all", () => hasText("请选择来源") && hasText("共 0 位玩家") && !hasText("EX 175") && button("刷新").Enabled.Value);
            AddAssert("website keeps the empty source selection", () => overlay.WebsiteUrl, () => Does.EndWith("&sources="));
            AddStep("historical source only", () => sourceCheckbox("LR2IR 历史").Current.Value = true);
            AddUntilStep("old same name is not highlighted as self", () => hasText("共 1 位玩家") && hasText("你在所选来源中还没有公开成绩") && hasText("LR2IR 旧身份") && button("刷新").Enabled.Value);
            AddAssert("old LR2IR identity never becomes an OMS profile", () => overlay.ChildrenOfType<ExternalLinkButton>().Any(), () => Is.False);
            AddStep("scroll to the historical record", () => overlay.ChildrenOfType<OsuScrollContainer>().Single().ScrollToEnd(false));
            FillFlowContainer historicalDetails = null!;
            AddStep("read historical summary details", () =>
            {
                historicalDetails = (FillFlowContainer)button("查看记录详情").Parent!;
                click("查看记录详情");
            });
            AddUntilStep("missing time and unknown conditions stay honest beside the row", () => hasText("游玩时间：未收录") && hasText("血条规则") && !hasText("最终血条 0")
                && historicalDetails.ChildrenOfType<OsuTextFlowContainer>().Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Contains("游玩时间：未收录", StringComparison.Ordinal)));
            AddStep("collapse historical details", () => click("收起详情"));
            AddAssert("collapsing removes details without removing the original identity or lamp", () => !hasText("游玩时间：未收录") && hasText("LR2IR 旧身份 #1") && hasText("★FULLCOMBO · 灯规则未知"));
            AddStep("select all sources again", () => click("全部来源"));
            AddUntilStep("full range returns", () => hasText("共 3 位玩家") && button("刷新").Enabled.Value);
            AddStep("hold the old range response", () => { holdBoard = true; delayedBoard = null; click("刷新"); });
            AddUntilStep("old response is in flight", () => delayedBoard != null);
            AddStep("change sources during the old request", () => sourceCheckbox("LR2IR 历史").Current.Value = false);
            AddStep("release late response", () => delayedBoard!.TrySetResult(referenceBoardForSources(new[] { "oms", "lr2oraja_ed", "lr2ir.v3.lr2" }, true, false)));
            AddUntilStep("late old scope cannot overwrite the selection", () => hasText("共 2 位玩家") && !hasText("EX 199") && button("刷新").Enabled.Value);
            AddStep("return malformed server response", () => { malformed = true; click("刷新"); });
            AddUntilStep("bad response reports failure without breaking the overlay", () => hasText("列表格式不正确") && button("刷新").Enabled.Value);
            AddStep("open the chosen mania chart", () => { malformed = false; overlay.ShowChartBoard(new string('f', 32), "合成 mania 原谱", ruleset: "mania"); });
            AddUntilStep("mania original group available", () => buttonExists("mania · 原计分条件") && button("刷新").Enabled.Value);
            AddStep("open original mania board", () => click("mania · 原计分条件"));
            AddUntilStep("mania scoring remains v1", () => hasText("分数 900,000") && button("刷新").Enabled.Value);
            AddAssert("mania board does not claim a BMS best lamp", () => !hasText("点灯最佳") && !hasText("独立最佳灯"));
            AddStep("select a local original BMS", () => setCurrentBms(new string('d', 32)));
            AddStep("open current original chart", () => overlay.ShowChartBoard(new string('d', 32), "当前 BMS 原谱"));
            AddUntilStep("current chart uses the actual original MD5", () => lastBoardMd5 == new string('d', 32) && hasText("共 3 位玩家") && button("刷新").Enabled.Value);
            AddStep("hold current chart read", () => { holdBoard = true; delayedBoard = null; click("刷新"); });
            AddUntilStep("chart response held", () => delayedBoard != null);
            AddStep("change the selected chart", () => setCurrentBms(new string('e', 32)));
            AddUntilStep("chart change closes and clears the previous read", () => overlay.State.Value == Visibility.Hidden && !hasText("EX 175") && overlay.WebsiteUrl == null);
            AddStep("release previous chart response", () => delayedBoard!.TrySetResult(referenceBoardForSources(new[] { "oms", "lr2oraja_ed", "lr2ir.v3.lr2" }, true, false)));
            AddUntilStep("late chart request has stopped", () => !service.State.Busy && !button("刷新").Enabled.Value);
            AddAssert("late previous chart does not reappear", () => !hasText("EX 175") && !hasText("EX 199"));
            AddStep("logout existing account", () => service.LogoutAsync().GetAwaiter().GetResult());
            AddUntilStep("account scope is cleared", () => service.State.Account == null && !hasText("EX 175") && !hasText("EX 199"));
            AddStep("reopen a specific chart", () => overlay.ShowChartBoard(new string('a', 32), "合成 IR 谱面"));
            AddAssert("overlay remains usable after completed operations", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddUntilStep("reopened chart loaded", () => button("刷新").Enabled.Value && hasText("共 3 位玩家"));
            AddStep("hold a read before closing", () => { holdBoard = true; delayedBoard = null; click("刷新"); });
            AddUntilStep("read is held", () => delayedBoard != null);
            AddAssert("close remains available during loading", () => button("关闭").Enabled.Value);
            AddStep("close the loading board", () => click("关闭"));
            AddUntilStep("board closed", () => overlay.State.Value == Visibility.Hidden);
            AddStep("release the closed board response", () => delayedBoard!.TrySetResult(referenceBoardForSources(new[] { "oms", "lr2oraja_ed", "lr2ir.v3.lr2" }, true, false)));
            AddUntilStep("closed read settles", () => !service.State.Busy);
            AddAssert("closed response cannot restore scores", () => !hasText("EX 175") && !hasText("EX 199"));
            testSelectedManiaChartChoosesItsActualConditions();
        }

        private void testSelectedManiaChartChoosesItsActualConditions()
        {
            AddStep("show IR", () => { Child = overlay = new OmsIrOverlay(); overlay.Show(); });
            AddUntilStep("controls loaded", () => overlay.IsLoaded);
            AddStep("select native mania", () => setCurrentChart("mania", new string('1', 32)));
            AddStep("open selected mania conditions", () => overlay.ShowChartBoard(new string('1', 32), "原生 mania", ruleset: "mania"));
            AddUntilStep("actual conditions appear", () => hasText("选择计分条件") && buttonExists("mania · 原计分条件") && button("刷新").Enabled.Value);
            AddAssert("unselected mania group stays out of the URL", () => overlay.WebsiteUrl,
                () => Is.EqualTo(OmsWebsite.ChartBoard(service.State.ServiceAddress, new string('1', 32), "mania")));
            AddStep("select the actual group", () => click("mania · 原计分条件"));
            AddUntilStep("selected mania board loads", () => hasText("分数 900,000") && button("刷新").Enabled.Value);
            AddAssert("website follows the exact mania group", () => overlay.WebsiteUrl,
                () => Is.EqualTo(OmsWebsite.ChartBoard(service.State.ServiceAddress, new string('1', 32), "mania", group: new string('c', 64))));
            AddStep("change the original chart", () => setCurrentChart("mania", new string('2', 32)));
            AddUntilStep("old mania board is cleared", () => overlay.State.Value == Visibility.Hidden && !hasText("分数 900,000") && overlay.WebsiteUrl == null);
            AddStep("open the second mania chart", () => overlay.ShowChartBoard(new string('2', 32), "第二张 mania", ruleset: "mania"));
            AddUntilStep("second chart conditions appear", () => buttonExists("mania · 原计分条件") && button("刷新").Enabled.Value);
            AddStep("select its actual group", () => click("mania · 原计分条件"));
            AddUntilStep("second mania board loads", () => hasText("分数 900,000") && button("刷新").Enabled.Value);
            AddStep("select a third mania chart", () => setCurrentChart("mania", new string('3', 32)));
            AddUntilStep("previous group is cleared", () => overlay.WebsiteUrl == null && !hasText("分数 900,000"));
            AddStep("return a different chart for the next read", () => { wrongManiaChart = true; overlay.ShowChartBoard(new string('3', 32), "第三张 mania", ruleset: "mania"); });
            AddUntilStep("wrong chart cannot provide a selectable condition", () => hasText("列表格式不正确") && !buttonExists("mania · 原计分条件") && button("刷新").Enabled.Value);
            AddStep("clear malformed fixture", () => wrongManiaChart = false);
        }

        private bool hasText(string part) => overlay.ChildrenOfType<OsuTextFlowContainer>().Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Contains(part, StringComparison.Ordinal))
                                             || overlay.ChildrenOfType<SpriteText>().Any(sprite => sprite.Text.ToString().Contains(part, StringComparison.Ordinal));

        private bool buttonExists(string label) => overlay.ChildrenOfType<OsuButton>().Any(item => item.Text.ToString() == label);
        private OsuButton button(string label) => overlay.ChildrenOfType<OsuButton>().Single(item => item.Text.ToString() == label);
        private void click(string label) => button(label).TriggerClick();

        private FormCheckBox sourceCheckbox(string label) => overlay.ChildrenOfType<FormCheckBox>().Single(item => item.Caption.ToString().StartsWith(label + " ·", StringComparison.Ordinal));

        private void setCurrentBms(string md5)
            => setCurrentChart("bms", md5);

        private void setCurrentChart(string ruleset, string md5)
        {
            var beatmap = new Beatmap
            {
                BeatmapInfo = new BeatmapInfo(new RulesetInfo { ShortName = ruleset })
                {
                    MD5Hash = md5,
                    Hash = new string('f', 64),
                    Metadata = new BeatmapMetadata { Title = "本地原谱", Artist = "synthetic" },
                },
            };
            Beatmap.Value = CreateWorkingBeatmap(beatmap);
        }

        private static JObject source(string code, string label) => new JObject
        {
            ["code"] = code,
            ["label"] = label,
            ["available"] = true,
            ["record_kind"] = code == "oms" ? "play" : code.StartsWith("lr2ir", StringComparison.Ordinal) ? "archive_best" : "best_state",
            ["versions"] = new JArray(),
            ["verification"] = "未核验参考",
            ["unknown_fields"] = new JArray(),
        };

        private static JObject referenceBoard(HttpRequestMessage request)
        {
            Dictionary<string, string> query = request.RequestUri!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2)).ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]), StringComparer.Ordinal);
            string[] sources = query.TryGetValue("sources", out string? value) ? value.Split(',', StringSplitOptions.RemoveEmptyEntries) : new[] { "oms", "lr2oraja_ed", "lr2ir.v3.lr2" };
            return referenceBoardForSources(sources, request.Headers.Authorization != null, query["mode"] == "comparable");
        }

        private static JObject referenceBoardForSources(string[] sources, bool loggedIn, bool comparable)
        {
            var items = new JArray();
            if (!comparable && sources.Contains("lr2ir.v3.lr2"))
                items.Add(referenceRow("lr2ir", "1", "ir_ui_synthetic", "archive:1:1", "archive_best", "lr2ir.v3.lr2", 199, null,
                    new JObject { ["family"] = "unknown:lr2ir", ["value"] = "★FULLCOMBO", ["label"] = "★FULLCOMBO" }));
            if (!comparable && sources.Contains("lr2oraja_ed"))
                items.Add(referenceRow("oms", "2", "ed_synthetic", "external:1", "best_state", "lr2oraja_ed", 190, 200,
                    new JObject { ["family"] = "unknown:lr2oraja_ed:1", ["value"] = 5, ["label"] = "Normal" }));
            JObject? mine = null;
            if (sources.Contains("oms"))
            {
                mine = referenceRow("oms", "1", "ir_ui_synthetic", "oms:1", "play", "oms", 175, 200,
                    new JObject { ["family"] = "oms:" + new string('c', 64) + ":200", ["value"] = 4, ["label"] = "CLEAR" });
                mine["best_lamps"] = new JArray(new JObject
                {
                    ["family"] = "oms:" + new string('c', 64) + ":200",
                    ["value"] = 8,
                    ["label"] = "FULL COMBO",
                    ["source"] = "oms",
                    ["record_id"] = "oms:2",
                    ["rule_label"] = "OMS · 7K · Normal · LN · 200 最大 EX",
                });
                items.Add(mine);
            }
            for (int index = 0; index < items.Count; index++)
                items[index]["rank"] = index + 1;
            return new JObject
            {
                ["chart"] = chart(),
                ["ruleset"] = "bms",
                ["groups"] = new JArray(new JObject { ["id"] = new string('c', 64), ["label"] = "OMS · 7K · Normal · LN" }),
                ["available_sources"] = new JArray(sources),
                ["content_identity"] = "sha256-reported",
                ["archive_suspended"] = false,
                ["items"] = items,
                ["page"] = 1,
                ["limit"] = 20,
                ["total"] = items.Count,
                ["selected_sources"] = new JArray(sources),
                ["mode"] = comparable ? "comparable" : "reference",
                ["conditions"] = sources.Contains("oms") ? new JArray(new JObject { ["id"] = new string('c', 64) + ":200", ["label"] = "7K · 普通 · 合成条件", ["source_scope"] = new JArray("oms"), ["unknown_fields"] = new JArray() }) : new JArray(),
                ["trust"] = "unverified-reference",
                ["me"] = loggedIn && mine != null ? mine.DeepClone() : JValue.CreateNull(),
                ["snapshot"] = new JObject { ["archive_version"] = "synthetic-archive-v1", ["read_at"] = "2026-10-04T00:00:00Z" },
                ["notice"] = sources.Length == 0 ? "请选择来源" : "原 EX 参考，不代表跨播放器公平性",
            };
        }

        private static JObject referenceRow(string identityNamespace, string identityId, string name, string record, string kind, string sourceCode, int ex, int? maximum, JObject lamp) => new JObject
        {
            ["rank"] = 1,
            ["identity"] = new JObject { ["namespace"] = identityNamespace, ["id"] = identityId, ["username"] = name },
            ["score"] = new JObject
            {
                ["record_id"] = record,
                ["record_kind"] = kind,
                ["source"] = sourceCode,
                ["ex_score"] = ex,
                ["max_ex_score"] = maximum.HasValue ? new JValue(maximum.Value) : JValue.CreateNull(),
                ["played_at"] = kind == "play" ? new JValue("2026-10-02T12:00:00Z") : JValue.CreateNull(),
                ["conditions"] = new JObject { ["keymode"] = "bms_7k" },
                ["unknown_fields"] = kind == "archive_best" ? new JArray("gauge_rules", "played_at", "assist") : new JArray("cross_player_parity"),
                ["lamp"] = lamp,
            },
            ["best_lamps"] = new JArray(),
        };

        private static JObject chart() => new JObject { ["title"] = "合成 IR 谱面", ["md5"] = new string('a', 32), ["sha256"] = new string('b', 64), ["artist"] = JValue.CreateNull(), ["difficulty"] = JValue.CreateNull() };
        private static JObject score() => new JObject
        {
            ["id"] = 1,
            ["ruleset"] = "bms",
            ["chart"] = chart(),
            ["ex_score"] = 175,
            ["max_ex_score"] = 200,
            ["accuracy"] = 0.875,
            ["max_combo"] = 100,
            ["passed"] = true,
            ["public_board"] = true,
            ["group_id"] = new string('c', 64),
            ["group_label"] = "7K · 普通 · 合成条件",
            ["played_at"] = "2026-10-02T12:00:00Z",
            ["statistics"] = new JObject { ["perfect"] = 75, ["great"] = 25 },
            ["ruleset_data"] = new JObject { ["clear_lamp"] = 4, ["final_gauge"] = 0.85 },
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<JObject>> response;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<JObject>> response) => this.response = response;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent((await response(request, cancellationToken)).ToString(), Encoding.UTF8, "application/json"),
            };
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            private OmsIrSession? session;
            public OmsIrSession? Read(string target) => session;
            public void Write(string target, OmsIrSession value) => session = value;
            public void Delete(string target) => session = null;
        }
    }
}
