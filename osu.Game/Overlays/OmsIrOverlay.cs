// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        public LocalisableString Description => "主动连接、交分与试验榜";

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private OmsIrService service { get; set; } = null!;

        private readonly Bindable<string> address = new Bindable<string>();
        private readonly Bindable<bool> enabled = new Bindable<bool>();
        private FormTextBox username = null!;
        private OsuPasswordTextBox password = null!;
        private OsuTextFlowContainer connectionStatus = null!;
        private OsuTextFlowContainer actionStatus = null!;
        private OsuSpriteText pageText = null!;
        private FillFlowContainer rows = null!;
        private readonly List<FormButton> buttons = new List<FormButton>();
        private FormButton historyButton = null!;
        private CancellationTokenSource? operationCancellation;
        private long? displayedAccount;
        private string displayedService = string.Empty;
        private bool requiresLogin;
        private int revision;
        private int page = 1;
        private int totalPages;
        private bool running;
        private View view;
        private string? chartMd5;
        private string? group;
        private string? chartTitle;

        private enum View { Charts, Board, History }

        public OmsIrOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            address.Value = service.State.ServiceAddress;
            enabled.Value = service.State.Enabled;
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
                                    text("OMS IR · 记录每次进步", 26),
                                    flow("客户端上报，未经回放核验。自动游玩只留本人记录；辅助玩法单独分组。"),
                                    connectionStatus = flow(string.Empty),
                                    new FormTextBox { Caption = "服务地址", PlaceholderText = "填写 HTTPS 服务地址", Current = address },
                                    new FormCheckBox { Caption = "启用 IR；只提交启用并登录后开始的新局", Current = enabled },
                                    makeButtons(button("保存连接设置", () => run(async token =>
                                    {
                                        await service.ConfigureAsync(address.Value, enabled.Value, token).ConfigureAwait(false);
                                        Schedule(() => { rows.Clear(); page = 1; view = View.Charts; });
                                    })), new FormButton { ButtonText = "关闭", Action = Hide }),
                                    username = new FormTextBox { Caption = "账号名", PlaceholderText = "3–24 位字母、数字或下划线" },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 64,
                                        Children = new Drawable[]
                                        {
                                            text("密码", 14),
                                            password = new OsuPasswordTextBox { RelativeSizeAxes = Axes.X, Y = 22, Height = 40, PlaceholderText = "至少 10 个字符" },
                                        }
                                    },
                                    makeButtons(button("登录", () => login(false)), button("注册并登录", () => login(true)), button("退出账号", () => run(service.LogoutAsync))),
                                    makeButtons(button("重试待交", () => run(service.RetryPendingAsync)), button("读取谱面", () => navigate(View.Charts)), historyButton = button("本人记录", () => navigate(View.History))),
                                    actionStatus = flow("本地成绩先保存，再交分。网络失败时待交保留；切换账号不会转交旧账号的记录。"),
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

            bool changedOwner = displayedAccount != state.Account?.Id || displayedService != state.ServiceAddress || requiresLogin != state.RequiresLogin;
            displayedAccount = state.Account?.Id;
            displayedService = state.ServiceAddress;
            requiresLogin = state.RequiresLogin;
            if (changedOwner)
            {
                cancelOperation();
                rows.Clear();
                pageText.Text = string.Empty;
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
            historyButton.Enabled.Value = !running && service.State.Account != null && !service.State.RequiresLogin;
        }

        private void login(bool register)
        {
            string account = username.Current.Value;
            string secret = password.Text;
            password.Text = string.Empty;
            run(token => service.LoginAsync(account, secret, register, token));
        }

        private void navigate(View next)
        {
            if (running)
                return;
            view = next;
            page = 1;
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
            View requestedView = view;
            int requestedPage = page;
            string? requestedMd5 = chartMd5;
            string? requestedGroup = group;
            run(async token =>
            {
                int requestRevision = revision;
                JObject response = requestedView switch
                {
                    View.History => await service.GetMyScoresAsync(requestedPage, token).ConfigureAwait(false),
                    View.Board when requestedMd5 != null && requestedGroup != null => await service.GetChartScoresAsync(requestedMd5, requestedGroup, requestedPage, token).ConfigureAwait(false),
                    _ => await service.GetChartsAsync(requestedPage, token).ConfigureAwait(false),
                };
                Schedule(() =>
                {
                    if (requestRevision != revision)
                        return;
                    try
                    {
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
                    string title = stringValue(chart, "title");
                    string md5 = stringValue(chart, "md5");
                    content.Add(flow(title));
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
                    JObject score = view == View.Board ? required<JObject>(item, "score") : item;
                    bool isBms = stringValue(score, "ruleset") == "bms";
                    string value = isBms
                        ? $"EX {integer(score, "ex_score")} / {integer(score, "max_ex_score")}" : $"分数 {integer(score, "total_score"):N0}";
                    string heading = view == View.Board ? $"#{integer(item, "rank")} · {stringValue(required<JObject>(item, "user"), "username")}" : stringValue(required<JObject>(score, "chart"), "title");
                    string lamp = isBms ? lampName(view == View.Board ? integer(item, "best_lamp") : integer(required<JObject>(score, "ruleset_data"), "clear_lamp")) : boolean(score, "passed") ? "通过" : "未通过";
                    string source = isBms && view == View.Board && integer(item, "best_lamp_score_id") != integer(score, "id") ? " · 最佳灯来自另一局" : string.Empty;
                    string detail = scoreDetails(score);
                    content.Add(flow($"{heading}\n{value} · {number(score, "accuracy"):P2} · {integer(score, "max_combo")} COMBO · {lamp}{source}"));
                    content.Add(new FormButton { ButtonText = "查看这局详情", Action = () => actionStatus.Text = detail });
                    if (view == View.History && boolean(score, "public_board"))
                    {
                        string md5 = stringValue(required<JObject>(score, "chart"), "md5");
                        string id = stringValue(score, "group_id");
                        content.Add(new FormButton { ButtonText = "查看同条件榜", Action = () => openBoard(md5, id, heading) });
                    }
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

        private static string scoreDetails(JObject score)
        {
            JObject statistics = required<JObject>(score, "statistics");
            string counts = $"PERFECT {statistics.Value<int>("perfect")} · GREAT {statistics.Value<int>("great")} · GOOD {statistics.Value<int>("good")} · MISS {statistics.Value<int>("miss")} · EMPTY POOR {statistics.Value<int>("ok")}";
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
            password.Text = string.Empty;
            this.FadeOut(150);
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (service != null)
                service.StateChanged -= onServiceChanged;
            cancelOperation();
            base.Dispose(isDisposing);
        }
    }
}
