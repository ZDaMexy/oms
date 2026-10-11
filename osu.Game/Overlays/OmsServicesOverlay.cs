// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Online.IR;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    /// <summary>Native OMS pages with a bounded, account-owned navigation history.</summary>
    public partial class OmsServicesOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent
    {
        public IconUsage Icon => FontAwesome.Solid.Globe;
        public LocalisableString Title => "OMS 在线服务";
        public LocalisableString Description => "玩家、谱面、社区、新闻与帮助";
        public OmsPageRoute Route => route;
        public int HistoryDepth => history.Count;
        public bool IsLoading { get; private set; }

        [Cached]
        private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private OmsIrService service { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        private FillFlowContainer rows = null!;
        private OsuScrollContainer scroll = null!;
        private OsuTextFlowContainer status = null!;
        private OsuButton back = null!;
        private OsuButton refresh = null!;
        private readonly List<PageFrame> history = new List<PageFrame>();
        private PageFrame frame = new PageFrame(new OmsPageRoute());
        private OmsPageRoute route = new OmsPageRoute();
        private CancellationTokenSource? operation;
        private int revision;
        private long? owner;
        private bool loginRequired;
        private DateTimeOffset retryAfter;

        private sealed class PageFrame
        {
            public OmsPageRoute Route;
            public Action? Render;
            public double Scroll;
            public PageFrame(OmsPageRoute route) => Route = route;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.7f },
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Size = new Vector2(0.9f, 0.88f), Anchor = Anchor.Centre, Origin = Anchor.Centre,
                    Masking = true, CornerRadius = 8,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both, Padding = new MarginPadding(20),
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Relative, 1) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical, Spacing = new Vector2(0, 8), Padding = new MarginPadding { Bottom = 12 },
                                        Children = new Drawable[]
                                        {
                                            Text("OMS 在线服务", 26),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                                Content = new[] { new Drawable[]
                                                {
                                                    back = Button("返回", Back),
                                                    Button("服务首页", () => Navigate(new OmsPageRoute())),
                                                    refresh = Button("刷新", () => Reload()),
                                                    Button("在网页查看", () => game?.OpenUrlExternally(route.WebsiteUrl(service.State.ServiceAddress))),
                                                    Button("关闭", Hide),
                                                } },
                                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                            },
                                            status = Text(string.Empty, 15),
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    scroll = new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Horizontal = 4 },
                                        Child = rows = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 10),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            owner = service.State.Account?.Id;
            loginRequired = service.State.RequiresLogin;
            service.StateChanged += onStateChanged;
        }

        public void Open(OmsPageRoute requested)
        {
            CancelOperation();
            history.Clear();
            frame = new PageFrame(requested);
            route = requested;
            Show();
            Reload();
        }

        public void OpenRelated(OmsPageRoute requested)
        {
            if (State.Value == Visibility.Visible) Navigate(requested);
            else Open(requested);
        }

        private void Navigate(OmsPageRoute requested)
        {
            SaveFrame();
            history.Add(frame);
            if (history.Count > 16) history.RemoveAt(0);
            frame = new PageFrame(requested);
            route = requested;
            Reload();
        }

        public void NavigateFromBoard(OmsPageRoute next, OmsPageRoute previous)
        {
            frame.Route = previous;
            route = previous;
            Show();
            Navigate(next);
        }

        public void ReturnFromBoard()
        {
            if (history.Count == 0) return;
            Show();
            Back();
        }

        public void Resume()
        {
            Show();
            if (frame.Render == null) Reload();
            else Render(frame.Render, frame.Scroll);
        }

        private void ReplaceRoute(OmsPageRoute requested)
        {
            frame = new PageFrame(requested);
            route = requested;
            Reload();
        }

        private void SaveFrame() => frame.Scroll = scroll.Current;

        public void Back()
        {
            if (history.Count == 0) { Hide(); return; }
            CancelOperation();
            frame = history[^1];
            history.RemoveAt(history.Count - 1);
            route = frame.Route;
            if (frame.Render == null) Reload();
            else Render(frame.Render, frame.Scroll);
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!e.Repeat && e.Action == GlobalAction.Back) { Back(); return true; }
            return base.OnPressed(e);
        }

        private async void Reload()
        {
            CancelOperation();
            rows.Clear();
            back.Enabled.Value = history.Count > 0;
            if (route.Kind == OmsPageKind.Board)
            {
                game?.ShowOmsBoardFromServices(route);
                return;
            }
            if (DateTimeOffset.UtcNow < retryAfter) { status.Text = "服务要求稍后重试，请等待后刷新。"; return; }
            frame.Render = null;
            frame.Scroll = 0;
            int requestedRevision = revision;
            PageFrame requestedFrame = frame;
            long? requestedOwner = service.State.Account?.Id;
            var cancellation = operation = new CancellationTokenSource();
            IsLoading = true;
            status.Text = "正在加载…";
            try
            {
                Action render = route.Kind switch
                {
                    OmsPageKind.Rankings => await LoadRankingsAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.Profile => await LoadProfileAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.Charts => await LoadChartsAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.Chart => await LoadChartAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.Library => () => RenderLibrary(requestedFrame.Route),
                    OmsPageKind.Account => RenderAccount,
                    OmsPageKind.Community => await LoadCommunityAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.Topic => await LoadTopicAsync(route, cancellation.Token).ConfigureAwait(false),
                    OmsPageKind.News or OmsPageKind.Article or OmsPageKind.Help => await LoadContentAsync(route, cancellation.Token).ConfigureAwait(false),
                    _ => RenderHome,
                };
                Schedule(() =>
                {
                    if (!Current(requestedRevision, requestedFrame, requestedOwner, cancellation)) return;
                    requestedFrame.Render = render;
                    Render(render, 0);
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() =>
                {
                    if (!Current(requestedRevision, requestedFrame, requestedOwner, cancellation)) return;
                    if (exception.RetryAfter is TimeSpan wait) retryAfter = DateTimeOffset.UtcNow + wait;
                    status.Text = exception.Message;
                    if (exception.Code == "login_required") AddAction("登录 OMS 账号", () => game?.ShowOmsAccount());
                });
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException or OverflowException)
            {
                Schedule(() => { if (Current(requestedRevision, requestedFrame, requestedOwner, cancellation)) status.Text = "服务返回的页面数据不符合约定，请稍后刷新。"; });
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

        private bool Current(int requestedRevision, PageFrame requestedFrame, long? requestedOwner, CancellationTokenSource cancellation)
            => !IsDisposed && requestedRevision == revision && ReferenceEquals(frame, requestedFrame) && !cancellation.IsCancellationRequested
                && State.Value == Visibility.Visible && service.State.Account?.Id == requestedOwner;

        private void Render(Action render, double position)
        {
            rows.Clear();
            status.Text = string.Empty;
            back.Enabled.Value = history.Count > 0;
            try { render(); }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException or OverflowException)
            {
                rows.Clear();
                frame.Render = null;
                status.Text = "服务返回的页面数据不符合约定，请稍后刷新。";
            }
            Schedule(() => scroll.ScrollTo((float)position, false));
        }

        private void RenderHome()
        {
            AddText("在游戏内查看玩家、交流和了解 OMS", 24);
            AddAction("玩家排行", () => Navigate(game?.CurrentOmsRoute(OmsPageKind.Rankings) ?? new OmsPageRoute { Kind = OmsPageKind.Rankings }));
            AddAction("谱面榜", () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Charts }));
            AddAction("查找与下载谱面", () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Library }));
            if (service.State.Account is OmsIrAccount account)
            {
                AddAction("我的公开资料", () => Navigate((game?.CurrentOmsRoute(OmsPageKind.Profile) ?? new OmsPageRoute { Kind = OmsPageKind.Profile }) with { UserId = account.Id }));
                AddAction("我的完整游玩记录", () => game?.ShowOwnOmsProfile());
            }
            else AddAction("注册 / 登录 OMS 账号", () => game?.ShowOmsAccount());
            AddAction("社区", () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Community }));
            AddAction("新闻", () => Navigate(new OmsPageRoute { Kind = OmsPageKind.News }));
            AddAction("帮助", () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Help }));
            AddText("页面在打开、翻页或刷新时读取。离线游玩不需要账号。", 15);
        }

        private void onStateChanged(OmsIrState state) => Schedule(() =>
        {
            OmsIrState current = service.State;
            if (owner == current.Account?.Id && loginRequired == current.RequiresLogin) return;
            owner = current.Account?.Id;
            loginRequired = current.RequiresLogin;
            CancelOperation();
            history.Clear();
            frame.Render = null;
            rows.Clear();
            status.Text = "账号已变化，请刷新当前页面。草稿仍绑定原账号。";
            back.Enabled.Value = false;
        });

        protected override void Update()
        {
            base.Update();
            refresh.Enabled.Value = !IsLoading && DateTimeOffset.UtcNow >= retryAfter;
        }

        private void CancelOperation()
        {
            operation?.Cancel();
            operation = null;
            IsLoading = false;
            revision++;
        }

        protected override void PopIn() => this.FadeIn(150);

        protected override void PopOut()
        {
            SaveFrame();
            CancelOperation();
            this.FadeOut(150);
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            CancelOperation();
            if (service != null) service.StateChanged -= onStateChanged;
            base.Dispose(isDisposing);
        }

        private static OsuTextFlowContainer Text(string value, int size = 18)
            => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = value };

        private void AddText(string value, int size = 18) => rows.Add(Text(value, size));

        private static OsuButton Button(string label, Action action)
            => new RoundedButton { Text = label, RelativeSizeAxes = Axes.X, Height = 40, Action = action };

        private void AddAction(string label, Action action) => rows.Add(Button(label, action));

        private void AddPagination(OmsPageRoute requested, long total, long limit)
        {
            if (total < 0 || limit is < 1 or > 50) throw new JsonSerializationException("Invalid OMS pagination.");
            long pages = Math.Max(1, total / limit + (total % limit == 0 ? 0 : 1));
            AddText($"第 {requested.Page} / {pages} 页 · 共 {total} 项", 15);
            if (requested.Page > 1) AddAction("上一页", () => ReplaceRoute(requested with { Page = requested.Page - 1 }));
            if (requested.Page < pages) AddAction("下一页", () => ReplaceRoute(requested with { Page = requested.Page + 1 }));
        }

        private static JObject Object(JObject value, string key) => value[key] as JObject ?? throw new JsonSerializationException("Missing " + key);
        private static JArray Array(JObject value, string key) => value[key] as JArray ?? throw new JsonSerializationException("Missing " + key);
        private static string String(JObject value, string key) => value[key]?.Type == JTokenType.String ? value[key]!.Value<string>()! : throw new JsonSerializationException("Missing " + key);
        private static long Integer(JObject value, string key) => value[key]?.Type == JTokenType.Integer ? value[key]!.Value<long>() : throw new JsonSerializationException("Missing " + key);
        private static bool Boolean(JObject value, string key) => value[key]?.Type == JTokenType.Boolean ? value[key]!.Value<bool>() : throw new JsonSerializationException("Missing " + key);
    }
}
