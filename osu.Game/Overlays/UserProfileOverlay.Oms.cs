// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface.PageSelector;
using osu.Game.Online.IR;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class UserProfileOverlay
    {
        private readonly Container omsViewContainer = new Container { RelativeSizeAxes = Axes.Both };

        [Resolved]
        private OmsIrService? omsIr { get; set; }

        [Resolved]
        private OsuGame? omsGame { get; set; }

        private OmsRecentSection omsRecent = null!;
        private OmsAccountSection omsAccount = null!;
        private CancellationTokenSource? omsRead;
        private int omsRevision;
        private long? omsDisplayedAccount;
        private string omsDisplayedService = string.Empty;
        private bool omsDisplayedRequiresLogin;
        private bool omsDisplayedEnabled;
        private bool updatingOmsPagination;

        private void loadOmsProfile()
        {
            omsRecent = new OmsRecentSection
            {
                Refresh = () => readOmsHistory(1),
                PageChanged = index => { if (!updatingOmsPagination) readOmsHistory(index + 1); },
            };
            omsAccount = new OmsAccountSection
            {
                OpenAccount = () =>
                {
                    Hide();
                    omsGame?.ShowOmsAccount();
                },
            };
            sections = new ProfileSection[] { omsRecent, omsAccount };
            recreateBaseContent();
            foreach (var section in sections)
            {
                sectionsContainer!.Add(section);
                tabs!.AddItem(section);
            }
            omsIr!.StateChanged += omsProfileStateChanged;
            applyOmsProfileState(omsIr.State, true);
        }

        public void ShowOwnOmsUser()
        {
            if (!useOmsAccount)
                return;
            applyOmsProfileState(omsIr!.State, true);
            Show();
            readOmsHistory(1);
        }

        private void omsProfileStateChanged(OmsIrState state) => Schedule(() => applyOmsProfileState(omsIr!.State));

        private void applyOmsProfileState(OmsIrState state, bool force = false)
        {
            bool changed = force || omsDisplayedAccount != state.Account?.Id || omsDisplayedService != state.ServiceAddress
                || omsDisplayedRequiresLogin != state.RequiresLogin || omsDisplayedEnabled != state.Enabled;
            omsDisplayedAccount = state.Account?.Id;
            omsDisplayedService = state.ServiceAddress;
            omsDisplayedRequiresLogin = state.RequiresLogin;
            omsDisplayedEnabled = state.Enabled;
            var header = (OmsIrProfileHeader)Header;
            header.Account.Value = state.Account;
            header.ServiceAddress.Value = state.ServiceAddress;
            omsAccount!.UpdateState(state);
            if (!changed)
                return;
            cancelOmsRead();
            omsRecent!.ClearRecords();
            omsRecent.SetStatus(state.Account == null || state.RequiresLogin ? "请从右上角登录 OMS 账号。游玩记录仅本人可见。"
                : !state.Enabled ? "IR 已关闭。请在连接设置中启用后查看。"
                : "刷新后查看已上传的游玩成绩。", false);
        }

        private async void readOmsHistory(int page)
        {
            OmsIrState owner = omsIr!.State;
            cancelOmsRead();
            omsRecent!.ClearRecords();
            if (!owner.Enabled || owner.Account == null || owner.RequiresLogin)
            {
                applyOmsProfileState(owner, true);
                return;
            }

            int requestedRevision = omsRevision;
            var cancellation = omsRead = new CancellationTokenSource();
            omsRecent.SetStatus("正在加载游玩记录…", true);
            loadingLayer.Show();
            try
            {
                JObject response = await omsIr.GetMyScoresAsync(page, cancellation.Token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (requestedRevision != omsRevision || cancellation.IsCancellationRequested)
                        return;
                    OmsIrState current = omsIr.State;
                    if (current.ServiceAddress != owner.ServiceAddress || current.Account?.Id != owner.Account.Id || current.RequiresLogin || !current.Enabled)
                        return;
                    try
                    {
                        JArray items = response["items"] as JArray ?? throw new JsonSerializationException("Missing OMS history records.");
                        long total = requiredInteger(response, "total");
                        long limit = requiredInteger(response, "limit");
                        if (total < 0 || limit is < 1 or > 50 || items.Count > limit || requiredInteger(response, "page") != page)
                            throw new JsonSerializationException("Invalid OMS history pagination.");
                        var records = new List<JObject>();
                        foreach (var item in items)
                        {
                            if (item is not JObject score || requiredInteger(score, "user_id") != owner.Account.Id
                                || !Guid.TryParse(requiredString(score, "submission_id"), out Guid uuid) || uuid == Guid.Empty)
                                throw new JsonSerializationException("The OMS history record has a different owner or no saved UUID.");
                            records.Add(score);
                        }
                        omsRecent.ShowRecords(records, showOmsScoreBoard, showOmsScoreWebsite);
                        updatingOmsPagination = true;
                        omsRecent.SetPagination(page, checked((int)Math.Max(1, Math.Ceiling((double)total / limit))));
                        updatingOmsPagination = false;
                        omsRecent.SetStatus($"共 {total} 条成绩 · 仅本人可见", false);
                    }
                    catch (Exception exception) when (exception is JsonException or FormatException or OverflowException)
                    {
                        updatingOmsPagination = false;
                        omsRecent.ClearRecords();
                        omsRecent.SetStatus("本人记录格式不正确，无法显示。", false);
                    }
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() => { if (requestedRevision == omsRevision) omsRecent.SetStatus(exception.Message, false); });
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(omsRead, cancellation))
                        omsRead = null;
                    if (requestedRevision == omsRevision)
                    {
                        loadingLayer.Hide();
                        omsRecent.SetLoading(false);
                    }
                    cancellation.Dispose();
                });
            }
        }

        private void showOmsScoreBoard(JObject score, bool comparable)
        {
            Hide();
            string md5 = requiredString((JObject)score["chart"]!, "md5");
            string title = requiredString((JObject)score["chart"]!, "title");
            bool isBms = requiredString(score, "ruleset") == "bms";
            string group = requiredString(score, "group_id");
            omsGame?.ShowOmsScoreBoard(md5, title, comparable && isBms ? group + ":" + requiredInteger(score, "max_ex_score").ToString(CultureInfo.InvariantCulture) : null,
                isBms ? null : group);
        }

        private void showOmsScoreWebsite(JObject score)
        {
            if (omsIr!.State.ServiceAddress != omsDisplayedService)
                return;
            string ruleset = requiredString(score, "ruleset");
            string group = requiredString(score, "group_id");
            string? url = OmsWebsite.ChartBoard(omsDisplayedService, requiredString(requiredObject(score, "chart"), "md5"), ruleset,
                mode: ruleset == "bms" ? "comparable" : null,
                condition: ruleset == "bms" ? group + ":" + requiredInteger(score, "max_ex_score").ToString(CultureInfo.InvariantCulture) : null,
                group: ruleset == "mania" ? group : null);
            if (url != null)
                omsGame?.OpenUrlExternally(url);
        }

        private void cancelOmsRead()
        {
            omsRead?.Cancel();
            omsRead = null;
            omsRevision++;
            loadingLayer.Hide();
        }

        protected override void PopOut()
        {
            if (useOmsAccount)
                cancelOmsRead();
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (useOmsAccount && omsIr != null)
                omsIr.StateChanged -= omsProfileStateChanged;
            if (useOmsAccount)
                cancelOmsRead();
            base.Dispose(isDisposing);
        }

        private static string requiredString(JObject item, string key) => item[key]?.Type == JTokenType.String
            ? item[key]!.Value<string>()! : throw new JsonSerializationException("Invalid OMS record field: " + key);

        private static long requiredInteger(JObject item, string key) => item[key]?.Type == JTokenType.Integer
            ? item[key]!.Value<long>() : throw new JsonSerializationException("Invalid OMS record field: " + key);

        private static JObject requiredObject(JObject item, string key) => item[key] as JObject ?? throw new JsonSerializationException("Invalid OMS record field: " + key);

        private static OsuTextFlowContainer omsText(string value, float size = 15) => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = value,
        };

        private partial class OmsRecentSection : ProfileSection
        {
            public override LocalisableString Title => "最近游玩";
            public override string Identifier => "recent";
            public Action? Refresh;
            public Action<int>? PageChanged;

            private readonly FillFlowContainer records;
            private readonly OsuTextFlowContainer status;
            private readonly SettingsButton refresh;
            private readonly PageSelector pagination;

            public OmsRecentSection()
            {
                AddRange(new Drawable[]
                {
                    status = omsText(string.Empty),
                    refresh = new SettingsButton { Text = "刷新本人记录", Action = () => Refresh?.Invoke() },
                    records = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                    },
                    pagination = new PageSelector { Margin = new MarginPadding { Top = 10 } },
                });
                pagination.CurrentPage.BindValueChanged(page => PageChanged?.Invoke(page.NewValue));
            }

            public void ClearRecords() => records.Clear();

            public void SetLoading(bool loading) => refresh.Enabled.Value = !loading;

            public void SetStatus(string value, bool loading)
            {
                status.Text = value;
                SetLoading(loading);
            }

            public void SetPagination(int page, int totalPages)
            {
                pagination.AvailablePages.Value = totalPages;
                pagination.CurrentPage.Value = page - 1;
            }

            public void ShowRecords(IReadOnlyList<JObject> scores, Action<JObject, bool> openBoard, Action<JObject> openWebsite)
            {
                var items = new List<Drawable>();
                if (scores.Count == 0)
                    items.Add(omsText("还没有已上传的游玩成绩。"));
                foreach (JObject score in scores)
                {
                    bool isBms = requiredString(score, "ruleset") == "bms";
                    string title = requiredString(requiredObject(score, "chart"), "title");
                    string result = isBms ? $"EX {requiredInteger(score, "ex_score")} / {requiredInteger(score, "max_ex_score")}" : $"分数 {requiredInteger(score, "total_score"):N0}";
                    string uuid = requiredString(score, "submission_id");
                    var actions = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical };
                    if (score["public_board"]?.Type == JTokenType.Boolean && score["public_board"]!.Value<bool>())
                    {
                        actions.Add(new SettingsButton { Text = "查看同条件榜", Action = () => openBoard(score, true) });
                        actions.Add(new SettingsButton { Text = "在网页查看同条件榜", Action = () => openWebsite(score) });
                        if (isBms)
                            actions.Add(new SettingsButton { Text = "查看参考混榜", Action = () => openBoard(score, false) });
                    }
                    items.Add(new ProfileItemContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(12),
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                omsText(title, 18),
                                omsText(result),
                                omsText(OmsIrOverlay.ScoreDetails(score, false)),
                                omsText("本局 ID：" + uuid, 12),
                                actions,
                            },
                        },
                    });
                }
                records.Clear();
                records.AddRange(items);
            }
        }

        private partial class OmsAccountSection : ProfileSection
        {
            public override LocalisableString Title => "账号与连接";
            public override string Identifier => "account";
            public Action? OpenAccount;

            private readonly OsuTextFlowContainer status;

            public OmsAccountSection()
            {
                AddRange(new Drawable[]
                {
                    status = omsText(string.Empty),
                    new SettingsButton { Text = "打开账号菜单", Action = () => OpenAccount?.Invoke() },
                });
            }

            public void UpdateState(OmsIrState state)
            {
                var lines = new List<string> { $"{(state.Enabled ? "IR 已启用" : "IR 已关闭")} · {state.ServiceAddress}" };
                if (state.PendingCount > 0)
                    lines.Add($"待上传 {state.PendingCount} 条");
                if (state.BlockedCount > 0)
                    lines.Add($"暂不能上传 {state.BlockedCount} 条");
                if (state.WaitingOtherAccountCount > 0)
                    lines.Add($"其他账号有 {state.WaitingOtherAccountCount} 条待上传成绩，请登录原账号重试。");
                if (state.Message.Length > 0)
                    lines.Add(state.Message);
                status.Text = string.Join("\n", lines);
            }
        }
    }
}
