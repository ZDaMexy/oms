// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.IR;
using osuTK;

namespace osu.Game.Overlays
{
    /// <summary>Account-aware data loading inside the existing native page containers.</summary>
    public abstract partial class OmsPageContent : Container
    {
        [Resolved]
        protected OmsIrService service { get; private set; } = null!;

        [Resolved]
        protected OsuGame? game { get; private set; }

        protected readonly FillFlowContainer rows;
        protected readonly OsuTextFlowContainer status;
        protected OmsPageRoute route = new OmsPageRoute();
        protected CancellationTokenSource? operation;
        protected int revision;
        protected DateTimeOffset retryAfter;
        private readonly Dictionary<OmsPageRoute, Action> cachedPages = new Dictionary<OmsPageRoute, Action>();
        private long? owner;
        private bool loginRequired;
        public OmsPageRoute Route => route;
        public bool IsLoading { get; protected set; }
        public Action? Rendered { get; set; }

        protected OmsPageContent()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    status = Text(string.Empty, 16),
                    rows = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 12),
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            owner = service.State.Account?.Id;
            loginRequired = service.State.RequiresLogin;
            service.StateChanged += onStateChanged;
        }

        public void Open(OmsPageRoute requested)
        {
            synchronizeAccount();
            route = requested;
            Reload();
        }

        public void Resume(OmsPageRoute requested)
        {
            synchronizeAccount();
            CancelOperation();
            route = requested;
            if (cachedPages.TryGetValue(requested, out Action? render)) Render(render);
            else Reload();
        }

        protected abstract Task<Action> LoadPageAsync(OmsPageRoute requested, CancellationToken token);

        protected void UpdateRoute(OmsPageRoute requested) => route = requested;
        protected void Navigate(OmsPageRoute requested) => game?.OpenRelatedOmsPage(requested);
        protected void ReplaceRoute(OmsPageRoute requested) => Open(requested);

        protected async void Reload()
        {
            CancelOperation();
            rows.Clear();
            if (DateTimeOffset.UtcNow < retryAfter) { status.Text = "服务要求稍后重试，请等待后刷新。"; return; }
            int requestedRevision = revision;
            long? requestedOwner = service.State.Account?.Id;
            var cancellation = operation = new CancellationTokenSource();
            OmsPageRoute requestedRoute = route;
            IsLoading = true;
            status.Text = "正在加载…";
            try
            {
                Action render = await LoadPageAsync(requestedRoute, cancellation.Token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (!Current(requestedRevision, requestedOwner, cancellation)) return;
                    if (!Render(render)) return;
                    if (cachedPages.Count >= 16) cachedPages.Clear();
                    cachedPages[route] = render;
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() =>
                {
                    if (!Current(requestedRevision, requestedOwner, cancellation)) return;
                    if (exception.RetryAfter is TimeSpan wait) retryAfter = DateTimeOffset.UtcNow + wait;
                    status.Text = exception.Message;
                    if (exception.Code == "login_required") AddAction("登录 OMS 账号", () => game?.ShowOmsAccount());
                });
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException or OverflowException)
            {
                Schedule(() => { if (Current(requestedRevision, requestedOwner, cancellation)) status.Text = "服务返回的页面数据不符合约定，请稍后刷新。"; });
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(operation, cancellation)) { operation = null; IsLoading = false; }
                    cancellation.Dispose();
                });
            }
        }

        protected bool Current(int requestedRevision, long? requestedOwner, CancellationTokenSource cancellation)
            => !IsDisposed && requestedRevision == revision && !cancellation.IsCancellationRequested
               && ReferenceEquals(operation, cancellation) && service.State.Account?.Id == requestedOwner;

        private bool Render(Action render)
        {
            rows.Clear();
            status.Text = string.Empty;
            try { render(); }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException or OverflowException)
            {
                rows.Clear();
                cachedPages.Remove(route);
                status.Text = "服务返回的页面数据不符合约定，请稍后刷新。";
                return false;
            }
            Rendered?.Invoke();
            return true;
        }

        private void onStateChanged(OmsIrState state) => Schedule(synchronizeAccount);

        private void synchronizeAccount()
        {
            OmsIrState current = service.State;
            if (owner == current.Account?.Id && loginRequired == current.RequiresLogin) return;
            owner = current.Account?.Id;
            loginRequired = current.RequiresLogin;
            CancelOperation();
            cachedPages.Clear();
            rows.Clear();
            status.Text = "账号已变化，请刷新当前页面。草稿仍绑定原账号。";
        }

        public void CancelOperation()
        {
            operation?.Cancel();
            operation = null;
            IsLoading = false;
            revision++;
        }

        protected void InvalidatePages() => cachedPages.Clear();

        protected override void Dispose(bool isDisposing)
        {
            CancelOperation();
            if (service != null) service.StateChanged -= onStateChanged;
            base.Dispose(isDisposing);
        }

        protected static OsuTextFlowContainer Text(string value, int size = 18)
            => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = value };

        protected void AddText(string value, int size = 18) => rows.Add(Text(value, size));

        protected static OsuButton Button(string label, Action action)
            => new RoundedButton { Text = label, Width = 280, Height = 34, Action = action };

        protected void AddAction(string label, Action action) => rows.Add(Button(label, action));

        protected void AddPagination(OmsPageRoute requested, long total, long limit)
        {
            if (total < 0 || limit is < 1 or > 50) throw new JsonSerializationException("Invalid OMS pagination.");
            long pages = Math.Max(1, total / limit + (total % limit == 0 ? 0 : 1));
            var pagination = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Horizontal, Spacing = new Vector2(12, 0) };
            if (requested.Page > 1) pagination.Add(new RoundedButton { Text = "上一页", Width = 96, Height = 32, Action = () => ReplaceRoute(requested with { Page = requested.Page - 1 }) });
            pagination.Add(new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 15)) { Width = 230, AutoSizeAxes = Axes.Y, Text = $"第 {requested.Page} / {pages} 页 · 共 {total} 项", Margin = new MarginPadding { Top = 8 } });
            if (requested.Page < pages) pagination.Add(new RoundedButton { Text = "下一页", Width = 96, Height = 32, Action = () => ReplaceRoute(requested with { Page = requested.Page + 1 }) });
            rows.Add(pagination);
        }

        protected static JObject Object(JObject value, string key) => value[key] as JObject ?? throw new JsonSerializationException("Missing " + key);
        protected static JArray Array(JObject value, string key) => value[key] as JArray ?? throw new JsonSerializationException("Missing " + key);
        protected static string String(JObject value, string key) => value[key]?.Type == JTokenType.String ? value[key]!.Value<string>()! : throw new JsonSerializationException("Missing " + key);
        protected static long Integer(JObject value, string key) => value[key]?.Type == JTokenType.Integer ? value[key]!.Value<long>() : throw new JsonSerializationException("Missing " + key);
        protected static bool Boolean(JObject value, string key) => value[key]?.Type == JTokenType.Boolean ? value[key]!.Value<bool>() : throw new JsonSerializationException("Missing " + key);
    }
}
