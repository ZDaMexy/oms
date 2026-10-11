// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.IR;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Overlays.Rankings.Tables;
using osuTK;

namespace osu.Game.Overlays
{
    internal sealed record OmsPublicPlayer(long Id, string Name);
    internal sealed record OmsPlayerRank(long Rank, OmsPublicPlayer User, string Value, long Charts, long Bests);

    /// <summary>Strict OMS public data rendered in the existing ranking and profile page layouts.</summary>
    public partial class OmsPlayerPageContent : OmsPageContent
    {
        public event Action<OmsPageRoute>? RouteLoaded;
        public event Action<long, string>? PlayerLoaded;

        private sealed record PlayerSource(string Code, string Label);
        private sealed record PlayerCondition(string Id, string Label, string Comparison, string[] Unknown);
        private sealed record PlayerLane(string Source, PlayerCondition Condition, string[] Details);
        private sealed record PlayerBest(string Md5, string Title, PlayerCondition Condition, string[] Details);
        private sealed record PlayerPagination(long Total, int Limit);

        protected override Task<Action> LoadPageAsync(OmsPageRoute requested, CancellationToken token) => requested.Kind switch
        {
            OmsPageKind.Rankings => LoadRankingsAsync(requested, token),
            OmsPageKind.Profile => LoadProfileAsync(requested, token),
            _ => throw new ArgumentException("This content requires an OMS player page.", nameof(requested)),
        };

        public void SelectMetric(string metric) => ReplaceRoute(route with { Metric = metric, Page = 1 });

        public void SelectRuleset(string ruleset) => ReplaceRoute(route with
        { Ruleset = ruleset, Keymode = null, Sources = "oms", Condition = null, Metric = null, Page = 1, ScopePage = 1 });

        private async Task<Action> LoadRankingsAsync(OmsPageRoute requested, CancellationToken token)
        {
            OmsPageRoute current = normalisePlayerRoute(requested, true);
            long? requestedOwner = service.State.Account?.Id;
            PlayerSource[] sources = playerSources(await service.GetSourcesAsync(token).ConfigureAwait(false), current.Ruleset);
            current = selectPlayerSources(current, sources, true);
            PlayerCondition[] conditions = System.Array.Empty<PlayerCondition>();
            PlayerPagination? scopesPage = null;
            PlayerPagination? playersPage = null;
            OmsPlayerRank[] players = System.Array.Empty<OmsPlayerRank>();
            OmsPlayerRank? me = null;
            PlayerCondition? selected = null;
            string? snapshot = null;

            if (current.Keymode != null && current.Sources!.Length > 0)
            {
                string scopeQuery = playerQuery(current, false) + "&source=" + Uri.EscapeDataString(current.Sources);
                JObject scopes = await service.GetPlayerPageAsync("rankings/scopes?" + scopeQuery
                    + "&page=" + current.ScopePage.ToString(CultureInfo.InvariantCulture) + "&limit=20", token).ConfigureAwait(false);
                validatePlayerScope(Object(scopes, "scope"), current, false);
                conditions = playerObjects(Array(scopes, "items")).Select(playerCondition).ToArray();
                validatePlayerConditions(conditions);
                scopesPage = playerPagination(scopes, current.ScopePage, 20, conditions.Length);
                playerSnapshot(scopes);

                // Conditions come from actual public records, never a fabricated default group.
                if (current.Condition != null || current.Ruleset == "bms" && current.Metric == "coverage")
                {
                    JObject ranking = await service.GetPlayerPageAsync("rankings/players?" + scopeQuery
                        + "&metric=" + Uri.EscapeDataString(current.Metric!) + playerConditionQuery(current)
                        + "&page=" + current.Page.ToString(CultureInfo.InvariantCulture) + "&limit=20", token).ConfigureAwait(false);
                    JObject scope = Object(ranking, "scope");
                    validatePlayerScope(scope, current, false);
                    if (String(scope, "metric") != current.Metric || String(scope, "basis") != "current-public-bests" || Boolean(scope, "rating"))
                        throw new JsonSerializationException("Unexpected OMS player ranking basis.");
                    selected = nullablePlayerCondition(scope, "condition_scope");
                    if (selected?.Id != current.Condition)
                        throw new JsonSerializationException("Unexpected OMS player condition.");
                    players = playerObjects(Array(ranking, "items")).Select(playerRank).ToArray();
                    playersPage = playerPagination(ranking, current.Page, 20, players.Length);
                    if (ranking["me"] is JObject own)
                    {
                        me = playerRank(own);
                        if (me.User.Id != requestedOwner)
                            throw new JsonSerializationException("Unexpected OMS ranking owner.");
                    }
                    else requirePlayerNull(ranking, "me");
                    validatePlayerTrust(ranking, current.Ruleset);
                    snapshot = playerSnapshot(ranking);
                }
            }

            token.ThrowIfCancellationRequested();
            return () =>
            {
                UpdateRoute(current);
                RouteLoaded?.Invoke(current);
                addPlayerFilters(current, sources, true);
                if (current.Keymode == null) { AddText("请选择实际键型后查看排行。", 16); return; }
                if (current.Sources!.Length == 0) { AddText("请选择一个可用来源。", 16); return; }
                addPlayerConditions(current, conditions, selected, current.Ruleset == "bms" && current.Metric == "coverage");
                addPlayerScopePagination(current, scopesPage!.Total);
                if (playersPage == null)
                {
                    AddText(scopesPage.Total == 0 ? "当前范围还没有已公布的公开条件。" : "请选择已公布的实际条件后查看排行。", 16);
                    return;
                }
                AddText(playerMetricDescription(current.Ruleset, current.Metric!), 14);
                addPlayerTrust(current.Ruleset);
                if (selected != null) addPlayerConditionDetails(selected);
                if (requestedOwner != null)
                {
                    if (me == null) AddText("你在所选范围中还没有参榜记录。", 14);
                    else
                    {
                        AddText("我的全体名次", 16);
                        addRankingsTable(current, new OmsPlayerRank[] { me });
                    }
                }
                if (players.Length == 0) AddText("当前页没有公开参榜玩家。", 16);
                else addRankingsTable(current, players);
                AddPagination(current, playersPage.Total, playersPage.Limit);
                AddText("统计读取时间：" + snapshot, 12);
            };
        }

        private async Task<Action> LoadProfileAsync(OmsPageRoute requested, CancellationToken token)
        {
            OmsPageRoute current = normalisePlayerRoute(requested, false);
            if (current.UserId <= 0) throw new OmsIrException("invalid_player", "请选择真实的 OMS 玩家身份。");
            PlayerSource[] sources = playerSources(await service.GetSourcesAsync(token).ConfigureAwait(false), current.Ruleset);
            current = selectPlayerSources(current, sources, false);
            string userPath = "users/" + current.UserId.ToString(CultureInfo.InvariantCulture) + "/";
            OmsPublicPlayer user = playerUser(Object(await service.GetPlayerPageAsync(userPath + "profile", token).ConfigureAwait(false), "user"));
            if (user.Id != current.UserId) throw new JsonSerializationException("Unexpected OMS profile owner.");
            PlayerLane[] lanes = System.Array.Empty<PlayerLane>();
            PlayerBest[] bests = System.Array.Empty<PlayerBest>();
            PlayerPagination? page = null;
            long charts = 0;
            long states = 0;
            string? snapshot = null;

            if (current.Keymode != null)
            {
                string scopeQuery = playerQuery(current, true);
                JObject performance = await service.GetPlayerPageAsync(userPath + "performance?" + scopeQuery, token).ConfigureAwait(false);
                validatePlayerUser(performance, current.UserId);
                validatePlayerScope(Object(performance, "scope"), current, true);
                JObject totals = Object(performance, "totals");
                charts = playerCount(totals, "public_chart_count");
                states = playerCount(totals, "public_best_count");
                lanes = playerObjects(Array(performance, "lanes")).Select(lane => playerLane(lane, current)).ToArray();
                validatePlayerConditions(lanes.Select(lane => lane.Condition));
                validatePlayerTrust(performance, current.Ruleset);
                playerSnapshot(performance);

                JObject records = await service.GetPlayerPageAsync(userPath + (current.Recent ? "public-recent?" : "public-bests?")
                    + scopeQuery + playerConditionQuery(current)
                    + "&page=" + current.Page.ToString(CultureInfo.InvariantCulture) + "&limit=20", token).ConfigureAwait(false);
                validatePlayerUser(records, current.UserId);
                JObject scope = Object(records, "scope");
                validatePlayerScope(scope, current, true);
                if (String(scope, "record_basis") != "current-public-bests" || nullablePlayerString(scope, "condition_id") != current.Condition)
                    throw new JsonSerializationException("Unexpected OMS public best basis.");
                bests = playerObjects(Array(records, "items")).Select(item => playerBest(item, current)).ToArray();
                page = playerPagination(records, current.Page, 20, bests.Length);
                validatePlayerTrust(records, current.Ruleset);
                snapshot = playerSnapshot(records);
            }

            PlayerCondition[] allConditions = lanes.Select(lane => lane.Condition).DistinctBy(condition => condition.Id).ToArray();
            PlayerCondition[] options = allConditions.Skip((current.ScopePage - 1) * 50).Take(50).ToArray();
            PlayerCondition? selected = allConditions.Concat(bests.Select(best => best.Condition)).FirstOrDefault(condition => condition.Id == current.Condition);
            token.ThrowIfCancellationRequested();
            return () =>
            {
                UpdateRoute(current);
                PlayerLoaded?.Invoke(user.Id, user.Name);
                RouteLoaded?.Invoke(current);
                addPlayerFilters(current, sources, false);
                AddText("公开资料与当前公开最佳；完整游玩历史仅本人可读。", 14);
                if (service.State.Account?.Id == user.Id)
                    AddAction("我的完整游玩记录", () => game?.ShowOwnOmsProfile());
                if (current.Keymode == null) { AddText("请选择实际键型后查看公开统计和记录。", 16); return; }
                addPlayerTrust(current.Ruleset);
                rows.Add(statisticsRow(charts, states));
                AddText("统计按来源和键型拆分，条件筛选只影响下方记录。", 13);
                if (current.Ruleset == "mania") AddText("通关数取各谱当前公开最高分的通过状态；累计分包含未通过局。", 13);
                addPlayerConditions(current, options, selected, true);
                addPlayerScopePagination(current, allConditions.Length);
                IEnumerable<PlayerLane> visibleLanes = current.Condition == null
                    ? lanes.Skip((current.ScopePage - 1) * 50).Take(50)
                    : lanes.Where(lane => lane.Condition.Id == current.Condition);
                foreach (PlayerLane lane in visibleLanes)
                    addProfileLane(current, lane, sourceLabel(sources, lane.Source));
                if (lanes.Length == 0) AddText("所选范围还没有公开统计或参榜名次。", 16);
                var recordTabs = new TabControlOverlayHeader<LocalisableString>.OverlayHeaderTabControl
                { Anchor = Anchor.TopLeft, Origin = Anchor.TopLeft };
                recordTabs.AddItem("公开最佳");
                recordTabs.AddItem("最近公开最佳 / 状态更新");
                recordTabs.Current.Value = current.Recent ? "最近公开最佳 / 状态更新" : "公开最佳";
                recordTabs.Current.BindValueChanged(change => ReplaceRoute(current with
                { Recent = change.NewValue == "最近公开最佳 / 状态更新", Page = 1 }));
                rows.Add(recordTabs);
                if (current.Recent) AddText("按服务器接收最佳或改善状态的时间排序；游玩时间另行显示。", 13);
                if (bests.Length == 0) AddText(current.Sources!.Length == 0 ? "未选择来源；没有公开记录。" : "当前页没有符合筛选的公开记录。", 16);
                foreach (PlayerBest best in bests) addProfileBest(current, best);
                AddPagination(current, page!.Total, page.Limit);
                AddText("统计读取时间：" + snapshot, 12);
            };
        }

        private void addPlayerFilters(OmsPageRoute current, PlayerSource[] sources, bool ranking)
        {
            Drawable sourceControl = ranking
                ? choice("来源", sources.Select(source => (source.Code, source.Label)), current.Sources!, value =>
                    ReplaceRoute(current with { Sources = value, Condition = null, Page = 1, ScopePage = 1 }))
                : sourceChoices(current, sources);
            rows.Add(new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[] { new Dimension(), new Dimension(), new Dimension() },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[] { new Drawable[]
                {
                    choice("玩法", new[] { ("bms", "BMS"), ("mania", "osu!mania") }, current.Ruleset, SelectRuleset),
                    choice("键型", playerKeymodes(current.Ruleset).Prepend((string.Empty, "请选择实际键型")), current.Keymode ?? string.Empty,
                        value => ReplaceRoute(current with { Keymode = value.Length == 0 ? null : value, Condition = null, Page = 1, ScopePage = 1 })),
                    sourceControl,
                } },
            });
        }

        private Drawable sourceChoices(OmsPageRoute current, PlayerSource[] sources)
        {
            string[] selected = current.Sources!.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var choices = new FillFlowContainer
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 6) };
            choices.Add(Text("来源 · 分别统计", 13));
            foreach (PlayerSource source in sources)
            {
                var toggle = new OsuCheckbox(false) { LabelText = source.Label };
                toggle.Current.Value = selected.Contains(source.Code, StringComparer.Ordinal);
                toggle.Current.BindValueChanged(change =>
                {
                    string[] next = sources.Where(candidate => candidate.Code == source.Code ? change.NewValue : selected.Contains(candidate.Code, StringComparer.Ordinal))
                                           .Select(candidate => candidate.Code).ToArray();
                    ReplaceRoute(current with { Sources = string.Join(',', next), Condition = null, Page = 1, ScopePage = 1 });
                });
                choices.Add(toggle);
            }
            return new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Padding = new MarginPadding { Horizontal = 8 }, Child = choices };
        }

        private static Drawable choice(string label, IEnumerable<(string Code, string Label)> choices, string selected, Action<string> changed)
        {
            var dropdown = new PlayerChoiceDropdown(choices) { RelativeSizeAxes = Axes.X };
            dropdown.Current.Value = selected;
            dropdown.Current.BindValueChanged(change => changed(change.NewValue));
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 8 },
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[] { Text(label, 13), dropdown },
            };
        }

        private void addPlayerConditions(OmsPageRoute current, PlayerCondition[] available, PlayerCondition? selected, bool allowAll)
        {
            var choices = available.ToDictionary(condition => condition.Id, condition => condition.Label + " · " + condition.Id[..8], StringComparer.Ordinal);
            choices.Add(string.Empty, allowAll ? "全部公开条件" : "请选择实际公开条件");
            if (selected != null) choices.TryAdd(selected.Id, selected.Label + " · " + selected.Id[..8]);
            if (current.Condition != null) choices.TryAdd(current.Condition, "已选公开条件 · " + current.Condition);
            rows.Add(choice("记录 / 比较条件", choices.Select(option => (option.Key, option.Value)), current.Condition ?? string.Empty,
                value => ReplaceRoute(current with { Condition = value.Length == 0 ? null : value, Page = 1 })));
        }

        private void addPlayerScopePagination(OmsPageRoute current, long total)
        {
            int size = current.Kind == OmsPageKind.Rankings ? 20 : 50;
            long pages = Math.Max(1, total == 0 ? 1 : 1 + (total - 1) / size);
            var line = new LinkFlowContainer(text => text.Font = OsuFont.GetFont(size: 12))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            line.AddText($"条件：共 {total} 项 · 第 {current.ScopePage} / {pages} 页");
            if (current.ScopePage > 1)
            {
                line.AddText("   ");
                line.AddLink("‹ 上一页条件", () => ReplaceRoute(current with { ScopePage = current.ScopePage - 1 }));
            }
            if (current.ScopePage < pages)
            {
                line.AddText("   ");
                line.AddLink("下一页条件 ›", () => ReplaceRoute(current with { ScopePage = current.ScopePage + 1 }));
            }
            rows.Add(line);
        }

        private void addRankingsTable(OmsPageRoute current, IReadOnlyList<OmsPlayerRank> players)
            => rows.Add(new OmsPlayerRankingsTable(players, PlayerMetrics(current.Ruleset).Single(metric => metric.Code == current.Metric).Label,
                player => Navigate(current with { Kind = OmsPageKind.Profile, UserId = player.User.Id, Page = 1, ScopePage = 1, Recent = false })));

        private static Drawable statisticsRow(long charts, long bests) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
            Content = new[] { new Drawable[] { statistic("公开谱面", charts), statistic("当前最佳条件条目", bests) } },
        };

        private static Drawable statistic(string label, long count) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding(10),
            Children = new Drawable[] { Text(label, 13), Text(count.ToString("N0", CultureInfo.CurrentCulture), 24) },
        };

        private void addProfileLane(OmsPageRoute current, PlayerLane lane, string source)
        {
            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding(12),
            };
            var title = new LinkFlowContainer(text => text.Font = OsuFont.GetFont(size: 16))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            title.AddLink(source + " · " + lane.Condition.Label, () => Navigate(current with
            {
                Kind = OmsPageKind.Rankings,
                Sources = lane.Source,
                Condition = lane.Condition.Id,
                Metric = current.Ruleset == "mania" ? "best_total_score" : "cleared_charts",
                Page = 1,
                ScopePage = 1,
            }), "查看该条件的玩家排行");
            content.Add(title);
            foreach (string detail in lane.Details) content.Add(Text(detail, 14));
            content.Add(Text(conditionDescription(lane.Condition), 12));
            rows.Add(new OmsPublicProfileItem { Child = content });
        }

        private void addProfileBest(OmsPageRoute current, PlayerBest best)
        {
            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Padding = new MarginPadding(12),
            };
            var title = new LinkFlowContainer(text => text.Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            title.AddLink(best.Title, () => Navigate(playerChartRoute(current, best.Md5, best.Title)), "在客户端查看真实谱面");
            content.Add(title);
            content.Add(Text(best.Condition.Label, 14));
            foreach (string detail in best.Details) content.Add(Text(detail, 13));
            content.Add(Text(conditionDescription(best.Condition), 12));
            rows.Add(new OmsPublicProfileItem { Child = content });
        }

        private partial class OmsPublicProfileItem : ProfileItemContainer
        {
            public OmsPublicProfileItem()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Content.RelativeSizeAxes = Axes.X;
                Content.AutoSizeAxes = Axes.Y;
            }
        }

        private void addPlayerConditionDetails(PlayerCondition condition) => AddText(conditionDescription(condition), 13);

        private static string conditionDescription(PlayerCondition condition)
            => (condition.Comparison == "same-oms-rules" ? "OMS 实际规则范围；跨玩家一致性仍未证明。" : "来源原条件参考；精确 TOTAL 与血条历程未证明。")
               + (condition.Unknown.Length == 0 ? string.Empty : " 未证明字段：" + string.Join("、", condition.Unknown));

        private void addPlayerTrust(string ruleset)
            => AddText(ruleset == "mania" ? "客户端上报 · 按真实公开最高分统计" : "公开参考 · 各来源原条件和独立灯分别保留", 13);

        private partial class PlayerChoiceDropdown : OsuDropdown<string>
        {
            private readonly IReadOnlyDictionary<string, string> labels;

            public PlayerChoiceDropdown(IEnumerable<(string Code, string Label)> choices)
            {
                labels = choices.ToDictionary(choice => choice.Code, choice => choice.Label, StringComparer.Ordinal);
                Items = labels.Keys;
            }

            protected override LocalisableString GenerateItemText(string item) => labels[item];
        }

        private static OmsPageRoute playerChartRoute(OmsPageRoute current, string md5, string title) => current with
        {
            Kind = OmsPageKind.Chart,
            Md5 = md5,
            Title = title,
            Page = 1,
            ScopePage = 1,
            Mode = "reference",
            Condition = null,
            Group = null,
        };

        private static OmsPageRoute normalisePlayerRoute(OmsPageRoute requested, bool ranking)
        {
            if (requested.Ruleset is not ("bms" or "mania") || requested.Page is < 1 or > 1000000 || requested.ScopePage is < 1 or > 1000000
                || requested.Keymode != null && !playerKeymodes(requested.Ruleset).Any(mode => mode.Code == requested.Keymode)
                || requested.Condition != null && !playerDigest(requested.Condition, 64))
                throw new OmsIrException("invalid_scope", "请选择实际玩法、键型和已公布的公开条件。");
            string metric = requested.Metric ?? (requested.Ruleset == "mania" ? "best_total_score" : "coverage");
            if (ranking && !PlayerMetrics(requested.Ruleset).Any(option => option.Code == metric))
                throw new OmsIrException("invalid_metric", "请选择当前玩法提供的排行指标。");
            return requested with { Sources = requested.Sources ?? "oms", Metric = metric };
        }

        private static IEnumerable<(string Code, string Label)> playerKeymodes(string ruleset) => ruleset == "mania"
            ? Enumerable.Range(1, 18).Select(keys => ($"mania_{keys}k", $"{keys}K"))
            : new[] { ("bms_5k", "BMS 5K"), ("bms_7k", "BMS 7K"), ("bms_9k", "BMS 9K"), ("pms_9k", "PMS 9K"), ("bms_14k", "BMS 14K") };

        internal static IEnumerable<(string Code, string Label)> PlayerMetrics(string ruleset) => ruleset == "mania"
            ? new[] { ("best_total_score", "累计公开最佳分"), ("coverage", "公开谱面覆盖") }
            : new[] { ("coverage", "公开谱面覆盖"), ("cleared_charts", "同条件通关谱面") };

        private static string playerMetricDescription(string ruleset, string metric) => metric switch
        {
            "best_total_score" => "各谱当前公开最高总分的精确累加，含未通过局；不是 PP 或固定难度能力值。",
            "cleared_charts" => "同一来源与实际规则条件下，具有公开独立通关灯的谱面数量。",
            _ => ruleset == "mania" ? "所选条件已有公开最佳的不同谱面数量。" : "所选来源和键型的不同公开谱面数量；跨条件覆盖不代表竞技能力。",
        };

        private static PlayerSource[] playerSources(JObject registry, string ruleset)
        {
            var sources = new List<PlayerSource>();
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in playerObjects(Array(registry, "items")))
            {
                string code = String(item, "code");
                string kind = String(item, "record_kind");
                string label = String(item, "label");
                if (!codes.Add(code) || kind is not ("play" or "best_state" or "archive_best"))
                    throw new JsonSerializationException("Invalid OMS source registry.");
                if (Boolean(item, "available") && kind != "archive_best" && (ruleset == "bms" || code == "oms"))
                    sources.Add(new PlayerSource(code, label));
            }
            return sources.ToArray();
        }

        private static OmsPageRoute selectPlayerSources(OmsPageRoute current, PlayerSource[] available, bool ranking)
        {
            string[] selected = current.Sources!.Length == 0 ? System.Array.Empty<string>() : current.Sources.Split(',');
            if (current.Sources.Length > 200 || selected.Length > 16
                || selected.Any(code => !available.Any(source => source.Code == code)) || ranking && selected.Distinct(StringComparer.Ordinal).Count() != 1)
                throw new OmsIrException("invalid_sources", "请选择可用的新来源；玩家排行每次只能比较一个来源。");
            return current with { Sources = string.Join(',', available.Where(source => selected.Contains(source.Code, StringComparer.Ordinal)).Select(source => source.Code)) };
        }

        private static string playerQuery(OmsPageRoute current, bool sources) => "ruleset=" + Uri.EscapeDataString(current.Ruleset)
            + "&keymode=" + Uri.EscapeDataString(current.Keymode!) + (sources ? "&sources=" + Uri.EscapeDataString(current.Sources!) : string.Empty);

        private static string playerConditionQuery(OmsPageRoute current) => current.Condition == null ? string.Empty : "&condition=" + Uri.EscapeDataString(current.Condition);

        private static void validatePlayerScope(JObject scope, OmsPageRoute requested, bool multiple)
        {
            if (String(scope, "ruleset") != requested.Ruleset || String(scope, "keymode") != requested.Keymode
                || (multiple ? !playerStrings(Array(scope, "selected_sources")).SequenceEqual(requested.Sources!.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal)
                             : String(scope, "source") != requested.Sources))
                throw new JsonSerializationException("Unexpected OMS player scope.");
        }

        private static void validatePlayerUser(JObject response, long owner)
        {
            if (playerUser(Object(response, "user")).Id != owner) throw new JsonSerializationException("Unexpected OMS public record owner.");
        }

        private static OmsPublicPlayer playerUser(JObject user)
        {
            long id = Integer(user, "id");
            if (id <= 0) throw new JsonSerializationException("Invalid OMS player ID.");
            return new OmsPublicPlayer(id, String(user, "username"));
        }

        private static PlayerCondition playerCondition(JObject condition)
        {
            string id = String(condition, "id");
            string comparison = String(condition, "comparison");
            if (!playerDigest(id, 64) || comparison is not ("same-oms-rules" or "unverified-source-condition"))
                throw new JsonSerializationException("Invalid OMS public condition.");
            Object(condition, "conditions");
            return new PlayerCondition(id, String(condition, "label"), comparison, playerStrings(Array(condition, "unknown_fields")));
        }

        private static PlayerCondition? nullablePlayerCondition(JObject value, string key)
        {
            if (value[key] is JObject condition) return playerCondition(condition);
            requirePlayerNull(value, key);
            return null;
        }

        private static void validatePlayerConditions(IEnumerable<PlayerCondition> conditions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (conditions.Any(condition => !ids.Add(condition.Id)))
                throw new JsonSerializationException("Duplicate OMS public condition.");
        }

        private static OmsPlayerRank playerRank(JObject item)
        {
            long rank = Integer(item, "rank");
            if (rank <= 0) throw new JsonSerializationException("Invalid OMS player rank.");
            return new OmsPlayerRank(rank, playerUser(Object(item, "user")), playerDecimal(String(item, "value")),
                playerCount(item, "public_chart_count"), playerCount(item, "public_best_count"));
        }

        private static PlayerLane playerLane(JObject lane, OmsPageRoute current)
        {
            string source = String(lane, "source");
            if (!current.Sources!.Split(',').Contains(source, StringComparer.Ordinal)) throw new JsonSerializationException("Unexpected OMS performance source.");
            PlayerCondition condition = playerCondition(Object(lane, "condition_scope"));
            JObject metrics = Object(lane, "metrics");
            var details = new List<string>
            {
                $"公开谱面：{playerCount(metrics, "public_chart_count")} · 当前最佳条件条目：{playerCount(metrics, "public_best_count")} · 通关谱面：{playerCount(metrics, "cleared_chart_count")}",
            };
            if (current.Ruleset == "mania") details.Add("累计公开最佳分：" + playerDecimal(String(metrics, "best_total_score")));
            else requirePlayerNull(metrics, "best_total_score");
            foreach (JObject ranking in playerObjects(Array(lane, "rankings")))
            {
                string metric = String(ranking, "metric");
                if (metric != (current.Ruleset == "mania" ? "best_total_score" : "cleared_charts")) throw new JsonSerializationException("Unexpected OMS performance metric.");
                long population = playerCount(ranking, "total_players");
                long? position = nullablePlayerInteger(ranking, "rank");
                if (position is <= 0 || position > population) throw new JsonSerializationException("Invalid OMS performance rank.");
                details.Add((current.Ruleset == "mania" ? "累计公开最佳分" : "同条件通关") + (position == null ? "：未参榜" : $"：#{position} / {population} 位玩家"));
            }
            return new PlayerLane(source, condition, details.ToArray());
        }

        private static PlayerBest playerBest(JObject item, OmsPageRoute current)
        {
            if (String(item, "ruleset") != current.Ruleset) throw new JsonSerializationException("Unexpected OMS record ruleset.");
            JObject chart = Object(item, "chart");
            string md5 = String(chart, "md5");
            if (!playerDigest(md5, 32)) throw new JsonSerializationException("Invalid OMS chart identity.");
            string title = nullablePlayerString(chart, "title") ?? "未收录谱名 · " + md5;
            PlayerCondition condition = playerCondition(Object(item, "condition_scope"));
            if (current.Condition != null && condition.Id != current.Condition) throw new JsonSerializationException("Unexpected OMS record condition.");
            JObject score = Object(item, "score");
            string source = String(score, "source");
            string kind = String(score, "record_kind");
            string record = String(score, "record_id");
            if (!current.Sources!.Split(',').Contains(source, StringComparer.Ordinal) || kind != (source == "oms" ? "play" : "best_state"))
                throw new JsonSerializationException("Unexpected OMS public record source.");
            var details = new List<string>
            {
                (nullablePlayerString(chart, "artist") ?? "作者未收录") + " · " + (nullablePlayerString(chart, "difficulty") ?? "难度名未收录"),
                "来源：" + source + " · " + (kind == "play" ? "游玩记录 " : "最佳状态 ") + record,
            };
            if (current.Ruleset == "mania")
            {
                long? total = nullablePlayerInteger(score, "total_score");
                if (total is null or < 0 or > 9007199254740991 || nullablePlayerInteger(score, "total_score_version") != 30000016)
                    throw new JsonSerializationException("Invalid OMS mania score.");
                details.Add("总分：" + total.Value.ToString(CultureInfo.InvariantCulture) + " · 计分版本：30000016");
                requirePlayerNull(score, "ex_score");
                requirePlayerNull(score, "max_ex_score");
                requirePlayerNull(score, "lamp");
            }
            else
            {
                long? ex = nullablePlayerInteger(score, "ex_score");
                long? maximum = nullablePlayerInteger(score, "max_ex_score");
                if (ex is null or < 0 || maximum is <= 0 || maximum != null && ex > maximum) throw new JsonSerializationException("Invalid OMS EX score.");
                details.Add($"EX：{ex} / {(maximum == null ? "未收录" : maximum.Value.ToString(CultureInfo.InvariantCulture))}");
                requirePlayerNull(score, "total_score");
                requirePlayerNull(score, "total_score_version");
                JObject lamp = Object(score, "lamp");
                String(lamp, "family");
                playerCount(lamp, "value");
                details.Add("本条最高分记录灯：" + String(lamp, "label"));
            }
            JToken? accuracy = score["accuracy"];
            if (accuracy?.Type is JTokenType.Float or JTokenType.Integer)
            {
                double value = accuracy.Value<double>();
                if (!double.IsFinite(value) || value is < 0 or > 1) throw new JsonSerializationException("Invalid OMS accuracy.");
                details.Add("准确率：" + value.ToString("P2", CultureInfo.CurrentCulture));
            }
            else { requirePlayerNull(score, "accuracy"); details.Add("准确率：未开放"); }
            if (score["passed"]?.Type == JTokenType.Boolean) details.Add(Boolean(score, "passed") ? "本条记录：通过" : "本条记录：未通过");
            else { requirePlayerNull(score, "passed"); details.Add("本条通过状态：未开放（参考来源原灯）"); }
            string? played = nullablePlayerString(score, "played_at");
            if (kind == "best_state" && played != null) throw new JsonSerializationException("External best state has no play time.");
            details.Add("游玩时间：" + (played == null ? "未收录" : playerTime(played)));
            string received = String(score, "received_at");
            details.Add("服务器接收时间：" + playerTime(received));
            Object(score, "conditions");
            string[] unknown = playerStrings(Array(score, "unknown_fields"));
            if (unknown.Length > 0) details.Add("记录未证明字段：" + string.Join("、", unknown));
            JObject[] lamps = playerObjects(Array(item, "best_lamps")).ToArray();
            if (current.Ruleset == "mania" && lamps.Length != 0) throw new JsonSerializationException("Mania has no BMS independent lamps.");
            foreach (JObject lamp in lamps)
            {
                String(lamp, "family");
                playerCount(lamp, "value");
                details.Add("独立最佳灯：" + String(lamp, "label") + " · " + String(lamp, "rule_label") + " · 来源 " + String(lamp, "source"));
                if (String(lamp, "record_id") != record) details.Add("分数与该灯独立保存，不合成为同一局成绩。");
            }
            if (current.Recent)
            {
                JObject activity = Object(item, "activity");
                if (String(activity, "kind") != (kind == "play" ? "public-best-received" : "best-state-updated")
                    || String(activity, "order_at") != received || nullablePlayerString(activity, "played_at") != played)
                    throw new JsonSerializationException("Unexpected OMS public activity time.");
                details.Add(kind == "play" ? "最近更新类型：公开最佳接收" : "最近更新类型：最佳状态改善");
            }
            return new PlayerBest(md5, title, condition, details.ToArray());
        }

        private static PlayerPagination playerPagination(JObject response, int page, int limit, int count)
        {
            long total = playerCount(response, "total");
            if (Integer(response, "page") != page || Integer(response, "limit") != limit || count > limit || count > total)
                throw new JsonSerializationException("Invalid OMS player pagination.");
            return new PlayerPagination(total, limit);
        }

        private static void validatePlayerTrust(JObject response, string ruleset)
        {
            if (String(response, "trust") != (ruleset == "mania" ? "client-reported" : "unverified-reference"))
                throw new JsonSerializationException("Unexpected OMS public trust.");
        }

        private static string playerSnapshot(JObject response)
        {
            JObject snapshot = Object(response, "snapshot");
            if (String(snapshot, "dataset") != "live-public-bests") throw new JsonSerializationException("Unexpected OMS public dataset.");
            return playerTime(String(snapshot, "read_at"));
        }

        private static string playerTime(string value)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset time))
                throw new JsonSerializationException("Invalid OMS record time.");
            return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.CurrentCulture);
        }

        private static string playerDecimal(string value)
        {
            if (value.Length == 0 || value.Any(character => character is < '0' or > '9') || value.Length > 1 && value[0] == '0')
                throw new JsonSerializationException("Invalid OMS exact decimal metric.");
            return value;
        }

        private static bool playerDigest(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static string sourceLabel(PlayerSource[] sources, string code) => sources.Single(source => source.Code == code).Label;

        private static long playerCount(JObject value, string key)
        {
            long result = Integer(value, key);
            if (result < 0) throw new JsonSerializationException("Invalid OMS public count.");
            return result;
        }

        private static long? nullablePlayerInteger(JObject value, string key)
        {
            if (value[key]?.Type == JTokenType.Integer) return Integer(value, key);
            requirePlayerNull(value, key);
            return null;
        }

        private static string? nullablePlayerString(JObject value, string key)
        {
            if (value[key]?.Type == JTokenType.String) return String(value, key);
            requirePlayerNull(value, key);
            return null;
        }

        private static void requirePlayerNull(JObject value, string key)
        {
            if (value[key]?.Type != JTokenType.Null) throw new JsonSerializationException("Expected nullable OMS field " + key);
        }

        private static IEnumerable<JObject> playerObjects(JArray values) => values.Select(value => value as JObject ?? throw new JsonSerializationException("Invalid OMS public item."));

        private static string[] playerStrings(JArray values) => values.Select(value => value.Type == JTokenType.String ? value.Value<string>()! : throw new JsonSerializationException("Invalid OMS public text.")).ToArray();
    }
}
