// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.IR;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class OmsIrOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent
    {
        public IconUsage Icon => FontAwesome.Solid.Trophy;
        public LocalisableString Title => "OMS IR";
        public LocalisableString Description => "谱面榜与来源筛选";

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private OmsIrService service { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

        private readonly Bindable<string> search = new Bindable<string>(string.Empty);
        private OsuTextFlowContainer connectionStatus = null!;
        private OsuTextFlowContainer actionStatus = null!;
        private OsuSpriteText pageText = null!;
        private FillFlowContainer rows = null!;
        private FillFlowContainer sourceOptions = null!;
        private FillFlowContainer conditionOptions = null!;
        private readonly List<FormButton> buttons = new List<FormButton>();
        private CancellationTokenSource? operationCancellation;
        private long? displayedAccount;
        private string displayedService = string.Empty;
        private bool requiresLogin;
        private bool displayedEnabled;
        private int revision;
        private int page = 1;
        private int totalPages;
        private bool running;
        private View view;
        private string? chartMd5;
        private string? group;
        private string? chartTitle;
        private JArray? sourceRegistry;
        private HashSet<string>? selectedSources;
        private string boardMode = "reference";
        private string? conditionId;

        private enum View { Charts, Board, ReferenceBoard }

        [Resolved]
        private OsuGame? game { get; set; }

        public OmsIrOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.7f },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.9f, 0.86f),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    CornerRadius = 8,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background4 },
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(24),
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 12),
                                Children = new Drawable[]
                                {
                                    text("OMS IR", 26),
                                    flow("新成绩与历史摘要均未核验。参考混榜按原 EX 排序；需要时可主动收窄到已证明的同条件。"),
                                    connectionStatus = flow(string.Empty),
                                    makeButtons(button("账号与连接", () => game?.ShowOmsAccount()), new FormButton { ButtonText = "关闭", Action = Hide }),
                                    makeButtons(button("读取谱面", () => navigate(View.Charts))),
                                    new FormTextBox { Caption = "谱名、作者或 MD5", PlaceholderText = "填写后点击读取谱面", Current = search },
                                    makeButtons(button("当前 BMS 谱面榜", openCurrentChart)),
                                    actionStatus = flow("本地成绩先保存，再交分。网络失败时待交保留；切换账号不会转交旧账号的记录。"),
                                    sourceOptions = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                    },
                                    conditionOptions = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                    },
                                    rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                    },
                                    pageText = text(string.Empty, 16),
                                    makeButtons(button("上一页", () => changePage(-1)), button("刷新", refresh), button("下一页", () => changePage(1))),
                                }
                            }
                        }
                    }
                }
            };
            service.StateChanged += onServiceChanged;
            currentBeatmap.ValueChanged += onBeatmapChanged;
            applyState(service.State);
        }

        private static OsuSpriteText text(string value, float size) => new OsuSpriteText { Text = value, Font = OsuFont.GetFont(size: size) };

        private static OsuTextFlowContainer flow(string value)
        {
            var result = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 17))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = value,
            };
            return result;
        }

        private FormButton button(string label, Action action)
        {
            var result = new FormButton { ButtonText = label, Action = action };
            buttons.Add(result);
            return result;
        }

        private static GridContainer makeButtons(params Drawable[] children) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Content = new[] { children },
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
        };

        private void onServiceChanged(OmsIrState state) => Schedule(() => applyState(service.State));

        private void applyState(OmsIrState state)
        {
            if (IsDisposed)
                return;

            bool changedOwner = displayedAccount != state.Account?.Id || displayedService != state.ServiceAddress || requiresLogin != state.RequiresLogin || displayedEnabled != state.Enabled;
            bool changedService = displayedService != state.ServiceAddress;
            displayedAccount = state.Account?.Id;
            displayedService = state.ServiceAddress;
            requiresLogin = state.RequiresLogin;
            displayedEnabled = state.Enabled;
            if (changedOwner)
            {
                cancelOperation();
                clearRead();
                sourceOptions.Clear();
                conditionOptions.Clear();
                page = 1;
                if (changedService)
                {
                    sourceRegistry = null;
                    selectedSources = null;
                }
                actionStatus.Text = "连接或账号已变化，请重新读取。";
            }
            string account = state.Account == null ? "未登录" : state.RequiresLogin ? $"{state.Account.Username} · 需要重新登录" : state.Account.Username;
            connectionStatus.Text = $"{(state.Enabled ? "IR 已启用" : "IR 已关闭")} · {account}\n待交 {state.PendingCount} · 暂不能提交 {state.BlockedCount} · 其他账号待交 {state.WaitingOtherAccountCount}\n{state.Message}";
            updateButtons();
        }

        private void updateButtons()
        {
            foreach (var item in buttons)
                item.Enabled.Value = !running;
        }

        private void navigate(View next)
        {
            if (running)
                return;
            view = next;
            page = 1;
            sourceOptions.Clear();
            conditionOptions.Clear();
            refresh();
        }

        private void changePage(int offset)
        {
            if (running || page + offset < 1 || page + offset > totalPages)
                return;
            page += offset;
            refresh();
        }

        private void refresh()
        {
            clearRead();
            actionStatus.Text = "正在按所选范围读取；本地游玩和成绩不受影响。";
            View requestedView = view;
            int requestedPage = page;
            string? requestedMd5 = chartMd5;
            string? requestedGroup = group;
            string? requestedCondition = conditionId;
            string requestedMode = boardMode;
            string[]? requestedSources = selectedSources?.ToArray();
            string requestedSearch = search.Value;
            run(async token =>
            {
                int requestRevision = revision;
                JArray? registry = sourceRegistry;
                if (requestedView == View.ReferenceBoard)
                    registry = required<JArray>(await service.GetSourcesAsync(token).ConfigureAwait(false), "items");
                JObject response = requestedView switch
                {
                    View.Board when requestedMd5 != null && requestedGroup != null => await service.GetChartScoresAsync(requestedMd5, requestedGroup, requestedPage, token).ConfigureAwait(false),
                    View.ReferenceBoard when requestedMd5 != null => await service.GetSourceChartScoresAsync(requestedMd5, requestedSources, requestedMode, requestedCondition, requestedPage, token).ConfigureAwait(false),
                    _ => await service.GetSourceChartsAsync(requestedSearch, requestedPage, token).ConfigureAwait(false),
                };
                Schedule(() =>
                {
                    if (requestRevision != revision)
                        return;
                    try
                    {
                        sourceRegistry = registry;
                        showResponse(response);
                    }
                    catch (JsonException)
                    {
                        actionStatus.Text = "IR 返回的列表格式不正确，请检查服务地址。";
                    }
                    catch (FormatException)
                    {
                        actionStatus.Text = "IR 返回的成绩字段格式不正确，请检查服务地址。";
                    }
                });
            });
        }

        private void showResponse(JObject response)
        {
            JArray items = required<JArray>(response, "items");
            long total = integer(response, "total");
            long limit = integer(response, "limit");
            if (limit is < 1 or > 50 || total < 0 || items.Count > limit)
                throw new JsonSerializationException("Invalid IR pagination.");
            if (view == View.ReferenceBoard)
            {
                showReferenceResponse(response, items, total, limit);
                return;
            }
            var content = new List<Drawable>();
            if (view == View.Board)
                content.Add(flow(chartTitle ?? "谱面榜单"));
            if (items.Count == 0)
                content.Add(flow("这里还没有记录。"));
            foreach (JToken entry in items)
            {
                var item = entry as JObject ?? throw new JsonSerializationException("Invalid IR record.");
                if (view == View.Charts)
                {
                    JObject chart = required<JObject>(item, "chart");
                    string title = nullableString(chart, "title") ?? "标题未收录";
                    string md5 = stringValue(chart, "md5");
                    content.Add(flow($"{title}\n作者：{nullableString(chart, "artist") ?? "未收录"} · 难度：{nullableString(chart, "difficulty") ?? "未收录"}\nMD5：{md5}"));
                    if (stringValue(item, "ruleset") == "bms")
                    {
                        content.Add(new FormButton { ButtonText = "查看参考混榜", Action = () => openReferenceBoard(md5, title) });
                        continue;
                    }
                    foreach (JToken conditionToken in required<JArray>(item, "groups"))
                    {
                        var condition = conditionToken as JObject ?? throw new JsonSerializationException("Invalid IR group.");
                        string id = stringValue(condition, "id");
                        string label = stringValue(condition, "label");
                        content.Add(new FormButton { ButtonText = label, Action = () => openBoard(md5, id, title) });
                    }
                }
                else
                {
                    JObject score = required<JObject>(item, "score");
                    bool isBms = stringValue(score, "ruleset") == "bms";
                    string value = isBms
                        ? $"EX {integer(score, "ex_score")} / {integer(score, "max_ex_score")}" : $"分数 {integer(score, "total_score"):N0}";
                    string heading = $"#{integer(item, "rank")} · {stringValue(required<JObject>(item, "user"), "username")}";
                    string lamp = isBms ? lampName(integer(item, "best_lamp")) : boolean(score, "passed") ? "通过" : "未通过";
                    string source = isBms && integer(item, "best_lamp_score_id") != integer(score, "id") ? " · 最佳灯来自另一局" : string.Empty;
                    string detail = ScoreDetails(score);
                    content.Add(flow($"{heading}\n{value} · {number(score, "accuracy"):P2} · {integer(score, "max_combo")} COMBO · {lamp}{source}"));
                    content.Add(new FormButton { ButtonText = "查看这局详情", Action = () => actionStatus.Text = detail });
                }
            }
            rows.Clear();
            rows.AddRange(content);
            totalPages = Math.Max(1, (int)Math.Ceiling((double)total / limit));
            pageText.Text = $"{page} / {totalPages} · 共 {total} {(view == View.Board ? "位玩家" : "条")}";
            actionStatus.Text = "客户端上报，未经回放核验。分数最佳与点灯最佳可能来自不同一局。";
        }

        private void openBoard(string md5, string id, string title)
        {
            if (running)
                return;
            chartMd5 = md5;
            group = id;
            chartTitle = title;
            navigate(View.Board);
        }

        public void ShowChartBoard(string md5, string title, string? condition = null, string? maniaGroup = null)
        {
            cancelOperation();
            Show();
            if (maniaGroup != null)
                openBoard(md5, maniaGroup, title);
            else
                openReferenceBoard(md5, title, condition);
        }

        private void openCurrentChart()
        {
            BeatmapInfo info = currentBeatmap.Value.BeatmapInfo;
            if (info.Ruleset.ShortName != "bms" || info.MD5Hash.Length != 32)
            {
                actionStatus.Text = "请先在选歌页选择具有原谱内容身份的 BMS 谱面。";
                return;
            }
            openReferenceBoard(info.MD5Hash.ToLowerInvariant(), info.Metadata.Title);
        }

        private void openReferenceBoard(string md5, string title, string? condition = null)
        {
            cancelOperation();
            chartMd5 = md5;
            chartTitle = title;
            selectedSources = null;
            boardMode = condition == null ? "reference" : "comparable";
            conditionId = condition;
            navigate(View.ReferenceBoard);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> change) => Schedule(() =>
        {
            if (view != View.ReferenceBoard)
                return;
            cancelOperation();
            clearRead();
            sourceOptions.Clear();
            conditionOptions.Clear();
            chartMd5 = null;
            chartTitle = null;
            conditionId = null;
            page = 1;
            view = View.Charts;
            actionStatus.Text = "选谱已变化，请重新读取当前 BMS 谱面榜。";
            updateButtons();
        });

        private void clearRead()
        {
            rows.Clear();
            pageText.Text = string.Empty;
            totalPages = 0;
        }

        private void changeSources(HashSet<string>? sources)
        {
            selectedSources = sources;
            cancelOperation();
            page = 1;
            conditionOptions.Clear();
            refresh();
        }

        private void chooseSource(string code, bool selected)
        {
            var chosen = selectedSources == null
                ? new HashSet<string>(sourceRegistry!.OfType<JObject>().Where(item => boolean(item, "available")).Select(item => stringValue(item, "code")), StringComparer.Ordinal)
                : new HashSet<string>(selectedSources, StringComparer.Ordinal);
            if (selected)
                chosen.Add(code);
            else
                chosen.Remove(code);
            changeSources(chosen);
        }

        private void chooseCondition(string? id)
        {
            boardMode = id == null ? "reference" : "comparable";
            conditionId = id;
            cancelOperation();
            page = 1;
            refresh();
        }

        private void showReferenceResponse(JObject response, JArray items, long total, long limit)
        {
            var controls = new List<Drawable>
            {
                flow("选择一个、多个或全部来源。取消全部来源会保留空选择。"),
                makeButtons(new FormButton { ButtonText = "全部来源", Action = () => changeSources(null) },
                    new FormButton { ButtonText = "清空来源", Action = () => changeSources(new HashSet<string>(StringComparer.Ordinal)) }),
            };
            foreach (JToken entry in sourceRegistry!)
            {
                var source = entry as JObject ?? throw new JsonSerializationException("Invalid IR source.");
                string code = stringValue(source, "code");
                bool available = boolean(source, "available");
                var selection = new Bindable<bool>(available && (selectedSources?.Contains(code) ?? true)) { Disabled = !available };
                controls.Add(new FormCheckBox { Caption = stringValue(source, "label") + " · " + (available ? stringValue(source, "verification") : "未开放"), Current = selection });
                selection.BindValueChanged(change => chooseSource(code, change.NewValue));
            }

            var conditions = required<JArray>(response, "conditions");
            var conditionControls = new List<Drawable>
            {
                flow(boardMode == "reference" ? "当前为参考混榜：按原 EX 排序，条件可能不同。" : "当前为主动选择的同条件榜。"),
                new FormButton { ButtonText = "返回参考混榜", Action = () => chooseCondition(null) },
            };
            foreach (JToken entry in conditions)
            {
                var condition = entry as JObject ?? throw new JsonSerializationException("Invalid IR condition.");
                string id = stringValue(condition, "id");
                conditionControls.Add(new FormButton { ButtonText = "同条件 · " + stringValue(condition, "label"), Action = () => chooseCondition(id) });
            }
            if (conditions.Count == 0)
                conditionControls.Add(flow("所选来源没有已证明的同条件组；可返回参考混榜。"));

            var content = new List<Drawable> { flow(chartTitle ?? nullableString(required<JObject>(response, "chart"), "title") ?? "标题未收录") };
            content.Add(flow("来源范围：" + string.Join("、", strings(response, "selected_sources").Select(sourceLabel))));
            if (stringValue(response, "content_identity") == "md5-only")
                content.Add(flow("目前只凭 MD5 关联，尚未确认相同内容与条件。"));
            if (boolean(response, "archive_suspended"))
                content.Add(flow("历史目录保留了停用标记，请结合原始来源判断。"));
            content.Add(flow(stringValue(response, "notice")));
            JObject snapshot = required<JObject>(response, "snapshot");
            string archiveVersion = nullableString(snapshot, "archive_version") ?? "本次未加载历史";
            string readAt = DateTimeOffset.Parse(stringValue(snapshot, "read_at"), CultureInfo.InvariantCulture).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            content.Add(flow($"历史快照：{archiveVersion}\n本次读取：{readAt}；该时间不代表游玩时间。"));
            JToken? me = response["me"];
            if (me is JObject mine)
            {
                JObject identity = required<JObject>(mine, "identity");
                if (service.State.Account is not OmsIrAccount owner || stringValue(identity, "namespace") != "oms"
                    || stringValue(identity, "id") != owner.Id.ToString(CultureInfo.InvariantCulture))
                    throw new JsonSerializationException("IR self rank has a different owner.");
                content.Add(flow("本人在当前完整范围内的名次"));
                addReferenceRow(content, mine);
            }
            else if (me?.Type == JTokenType.Null)
                content.Add(flow(service.State.Account == null ? "登录后可查看本人在相同来源范围内的名次。" : "本人在当前范围没有公开成绩。"));
            else
                throw new JsonSerializationException("Invalid IR self rank.");
            if (items.Count == 0)
                content.Add(flow("当前范围没有记录。"));
            foreach (JToken entry in items)
                addReferenceRow(content, entry as JObject ?? throw new JsonSerializationException("Invalid IR record."));

            sourceOptions.Clear();
            sourceOptions.AddRange(controls);
            conditionOptions.Clear();
            conditionOptions.AddRange(conditionControls);
            rows.Clear();
            rows.AddRange(content);
            totalPages = Math.Max(1, (int)Math.Ceiling((double)total / limit));
            pageText.Text = $"{page} / {totalPages} · 共 {total} 位玩家";
            actionStatus.Text = "成绩与历史摘要均未核验。分数和各规则最佳灯分别保留原记录；同名旧身份不会合并。";
        }

        private void addReferenceRow(List<Drawable> content, JObject item)
        {
            JObject identity = required<JObject>(item, "identity");
            JObject score = required<JObject>(item, "score");
            string name = stringValue(identity, "username");
            string origin = stringValue(identity, "namespace") == "lr2ir"
                ? "LR2IR 旧身份 #" + stringValue(identity, "id") + " · 未关联 OMS"
                : "OMS 账号 #" + stringValue(identity, "id");
            string maximum = nullableInteger(score, "max_ex_score")?.ToString(CultureInfo.InvariantCulture) ?? "未收录";
            string kind = recordKindLabel(stringValue(score, "record_kind"));
            string lamp = originalLamp(score);
            content.Add(flow($"#{integer(item, "rank")} · {name}\n{origin}\nEX {integer(score, "ex_score")} / {maximum} · {sourceLabel(stringValue(score, "source"))} · {kind}\n原灯：{lamp}"));
            foreach (JToken entry in required<JArray>(item, "best_lamps"))
            {
                var best = entry as JObject ?? throw new JsonSerializationException("Invalid IR lamp.");
                string evidence = stringValue(best, "record_id") == stringValue(score, "record_id") ? string.Empty : " · 分数与该灯来自不同记录";
                string ruleLabel = stringValue(best, "rule_label");
                content.Add(flow($"独立最佳灯：{stringValue(best, "label")} · {sourceLabel(stringValue(best, "source"))}{evidence}\n灯规则：{ruleLabel}"));
            }
            if (stringValue(score, "record_kind") == "archive_best")
                content.Add(flow("这是历史最佳摘要；分数和灯不一定来自同一次游玩。"));
            string detail = referenceDetails(item);
            content.Add(new FormButton { ButtonText = "查看记录详情", Action = () => actionStatus.Text = detail });
        }

        private string referenceDetails(JObject item)
        {
            JObject score = required<JObject>(item, "score");
            string? playedAt = nullableString(score, "played_at");
            string time = playedAt == null ? "未收录" : DateTimeOffset.Parse(playedAt, CultureInfo.InvariantCulture).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            string known = string.Join("\n", required<JObject>(score, "conditions").Properties().Select(property => conditionLabel(property.Name) + "：" + conditionValue(property.Value)));
            string unknown = string.Join("、", strings(score, "unknown_fields").Select(conditionLabel));
            string rawLamp = score["lamp"] is JObject original ? conditionValue(original["value"]!) : "未收录";
            return $"{recordKindLabel(stringValue(score, "record_kind"))} · {sourceLabel(stringValue(score, "source"))}\n原灯：{originalLamp(score)}\n原灯值：{rawLamp}\n游玩时间：{time}\n已知条件\n{known}\n未收录或未证明：{(unknown.Length == 0 ? "无声明缺项" : unknown)}";
        }

        private string sourceLabel(string code) => sourceRegistry!.OfType<JObject>().FirstOrDefault(source => stringValue(source, "code") == code) is JObject found ? stringValue(found, "label") : code;

        private static string recordKindLabel(string kind) => kind switch
        {
            "play" => "保存后的新局", "best_state" => "播放器最佳状态", "archive_best" => "历史最佳摘要", _ => kind,
        };

        private static string conditionLabel(string field) => field switch
        {
            "ruleset" => "玩法", "keymode" => "键型", "judge_mode" => "判定", "judge_rank" => "判定等级",
            "judge_algorithm" => "判定算法", "gauge_rules_family" or "gauge_rules" => "血条规则",
            "gauge_auto_shift" => "自动降档", "starting_gauge_type" => "起始血条", "floor_gauge_type" => "降档下限",
            "long_note_mode" => "长条规则", "branch_policy" => "谱面分支", "assist" => "辅助资格", "frequency" => "频率",
            "total" => "TOTAL", "max_ex_score" => "最大 EX", "played_at" => "游玩时间", "sha256" => "内容声明",
            "version" => "成绩规则版本", "mods" => "辅助选项", "cross_player_parity" => "跨播放器同规则证明",
            "option_1" => "历史选项 1", "option_2" => "历史选项 2", "option_3" => "历史选项 3", "option_4" => "历史选项 4", "input" => "输入方式", _ => field,
        };

        private static string conditionValue(JToken value) => value.Type switch
        {
            JTokenType.Null => "未收录", JTokenType.String => value.Value<string>()!,
            JTokenType.Boolean => value.Value<bool>() ? "是" : "否", _ => value.ToString(Formatting.None),
        };

        private static string originalLamp(JObject score)
        {
            JToken? lamp = score["lamp"];
            if (lamp?.Type == JTokenType.Null)
                return "未收录";
            if (lamp is not JObject original || original["value"]?.Type is not (JTokenType.String or JTokenType.Integer))
                throw new JsonSerializationException("Invalid IR original lamp.");
            string label = stringValue(original, "label");
            return stringValue(original, "family").StartsWith("unknown:", StringComparison.Ordinal) ? label + " · 灯规则未知" : label;
        }

        private static string[] strings(JObject source, string field) => required<JArray>(source, field).Select(value => value.Type == JTokenType.String
            ? value.Value<string>()! : throw new JsonSerializationException("Invalid IR string list.")).ToArray();

        private static string? nullableString(JObject source, string field) => source[field]?.Type == JTokenType.Null ? null : stringValue(source, field);
        private static long? nullableInteger(JObject source, string field) => source[field]?.Type == JTokenType.Null ? null : integer(source, field);

        internal static string ScoreDetails(JObject score)
        {
            JObject statistics = required<JObject>(score, "statistics");
            string counts = stringValue(score, "ruleset") switch
            {
                "bms" => $"PERFECT {statistics.Value<int>("perfect")} · GREAT {statistics.Value<int>("great")} · GOOD {statistics.Value<int>("good")} · MISS {statistics.Value<int>("miss")} · EMPTY POOR {statistics.Value<int>("ok")}",
                "mania" => $"Perfect {statistics.Value<int>("perfect")} · Great {statistics.Value<int>("great")} · Good {statistics.Value<int>("good")} · Ok {statistics.Value<int>("ok")} · Meh {statistics.Value<int>("meh")} · Miss {statistics.Value<int>("miss")}",
                _ => throw new JsonSerializationException("Invalid IR score ruleset."),
            };
            string gauge = score["ruleset_data"] is JObject data ? $"\n本局灯：{lampName(integer(data, "clear_lamp"))} · 最终血条 {number(data, "final_gauge"):P1}" : string.Empty;
            string time = DateTimeOffset.Parse(stringValue(score, "played_at"), CultureInfo.InvariantCulture).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return $"{stringValue(required<JObject>(score, "chart"), "title")}\n{stringValue(score, "group_label")}\n{counts}{gauge}\n游玩时间：{time}";
        }

        private static T required<T>(JObject source, string field) where T : JToken => source[field] as T ?? throw new JsonSerializationException($"Missing IR field: {field}");
        private static JToken scalar(JObject source, string field, JTokenType type)
        {
            JToken? token = source[field];
            if (token?.Type != type)
                throw new JsonSerializationException($"Invalid IR field: {field}");
            return token;
        }

        private static string stringValue(JObject source, string field) => scalar(source, field, JTokenType.String).Value<string>()!;
        private static long integer(JObject source, string field) => scalar(source, field, JTokenType.Integer).Value<long>();
        private static bool boolean(JObject source, string field) => scalar(source, field, JTokenType.Boolean).Value<bool>();
        private static double number(JObject source, string field)
        {
            JToken? token = source[field];
            if (token?.Type is not (JTokenType.Float or JTokenType.Integer))
                throw new JsonSerializationException($"Invalid IR field: {field}");
            return token.Value<double>();
        }

        private static string lampName(long? value) => value switch
        {
            1 => "FAILED", 2 => "ASSIST EASY", 3 => "EASY CLEAR", 4 => "CLEAR", 5 => "HARD CLEAR", 6 => "EX HARD CLEAR", 7 => "HAZARD CLEAR", 8 => "FULL COMBO", 9 => "PERFECT", _ => "—",
        };

        private async void run(Func<CancellationToken, Task> action)
        {
            if (running)
                return;
            cancelOperation();
            int requestRevision = revision;
            var cancellation = operationCancellation = new CancellationTokenSource();
            running = true;
            updateButtons();
            try
            {
                await action(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException error)
            {
                Schedule(() => { if (requestRevision == revision) actionStatus.Text = error.Message; });
            }
            catch (IOException error)
            {
                Schedule(() => { if (requestRevision == revision) actionStatus.Text = $"IR 数据无法保存或读取：{error.Message}"; });
            }
            catch (JsonException)
            {
                Schedule(() => { if (requestRevision == revision) actionStatus.Text = "IR 返回的列表格式不正确，请检查服务地址。"; });
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(operationCancellation, cancellation))
                        operationCancellation = null;
                    cancellation.Dispose();
                    if (requestRevision != revision)
                        return;
                    running = false;
                    updateButtons();
                });
            }
        }

        private void cancelOperation()
        {
            operationCancellation?.Cancel();
            operationCancellation = null;
            revision++;
            running = false;
        }

        protected override void PopIn() => this.FadeIn(150);

        protected override void PopOut()
        {
            cancelOperation();
            this.FadeOut(150);
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (service != null)
                service.StateChanged -= onServiceChanged;
            if (currentBeatmap != null)
                currentBeatmap.ValueChanged -= onBeatmapChanged;
            cancelOperation();
            base.Dispose(isDisposing);
        }
    }
}
