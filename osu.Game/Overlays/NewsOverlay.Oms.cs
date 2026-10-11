// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.IR;
using osu.Game.Overlays.News;
using osu.Game.Overlays.Wiki.Markdown;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class NewsOverlay
    {
        private readonly bool omsOnly;

        [Resolved(canBeNull: true)]
        private OmsIrService? omsService { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? omsGame { get; set; }

        private CancellationTokenSource? omsOperation;
        private int omsRevision;
        private OmsNewsPost[]? omsPosts;
        private float omsRestoreScroll;

        public OmsPageRoute CurrentOmsRoute { get; private set; } = new OmsPageRoute { Kind = OmsPageKind.News };

        public float OmsScroll
        {
            get => (float)ScrollFlow.Current;
            set
            {
                omsRestoreScroll = value;
                if (IsLoaded)
                    ScrollFlow.ScrollTo(value, false);
            }
        }

        public void ShowOmsPage(OmsPageRoute route)
        {
            if (route.Kind is not (OmsPageKind.News or OmsPageKind.Article))
                throw new ArgumentException("The news overlay requires a news or article route.", nameof(route));

            CurrentOmsRoute = route;
            omsRestoreScroll = 0;
            cancelOmsLoading();
            displayUpdateRequired = true;
            if (IsLoaded && State.Value == Visibility.Visible)
                beginOmsLoading();
            Show();
        }

        public void RestoreOmsPage(OmsPageRoute route, float scroll)
        {
            if (route.Kind is not (OmsPageKind.News or OmsPageKind.Article))
                throw new ArgumentException("The news overlay requires a news or article route.", nameof(route));

            CurrentOmsRoute = route;
            omsRestoreScroll = scroll;
            cancelOmsLoading();
            displayUpdateRequired = true;
            if (IsLoaded && omsPosts != null)
            {
                displayUpdateRequired = false;
                Loading.Show();
                content.Clear();
                renderOmsContent(omsPosts, route, omsRevision, scroll);
            }
            Show();
        }

        private void openOmsYear(int? year) => omsGame?.OpenRelatedOmsPage(new OmsPageRoute
        {
            Kind = OmsPageKind.News,
            Query = year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        });

        private void openOmsArticle(OmsNewsPost post) => omsGame?.OpenRelatedOmsPage(new OmsPageRoute
        {
            Kind = OmsPageKind.Article,
            Slug = post.Slug,
        });

        private void beginOmsLoading()
        {
            displayUpdateRequired = false;
            cancelOmsLoading();
            Loading.Show();
            content.Clear();
            omsSidebar.ClearPosts();
            Header.SetOmsPage();
            if (omsService == null)
            {
                showOmsError("OMS 新闻服务尚未准备好，请稍后刷新。", omsRevision);
                return;
            }
            var cancellation = omsOperation = new CancellationTokenSource();
            _ = loadOmsContentAsync(CurrentOmsRoute, omsRevision, omsRestoreScroll, cancellation.Token);
        }

        private async Task loadOmsContentAsync(OmsPageRoute route, int revision, float scroll, CancellationToken token)
        {
            try
            {
                JObject document = await omsService!.GetClientContentAsync(token).ConfigureAwait(false);
                OmsNewsPost[] posts = document["news"]!.Cast<JObject>().Select(OmsNewsPost.FromJson)
                                              .OrderByDescending(post => post.PublishedAt).ToArray();
                Schedule(() =>
                {
                    if (!isCurrentOmsLoad(revision, token)) return;
                    omsPosts = posts;
                    renderOmsContent(posts, route, revision, scroll);
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (OmsIrException exception)
            {
                Schedule(() => { if (isCurrentOmsLoad(revision, token)) showOmsError(exception.Message, revision); });
            }
            catch (JsonException)
            {
                Schedule(() => { if (isCurrentOmsLoad(revision, token)) showOmsError("网站新闻格式不符合约定，请稍后刷新。", revision); });
            }
        }

        private void renderOmsContent(OmsNewsPost[] posts, OmsPageRoute route, int revision, float scroll)
        {
            int? year = null;
            if (route.Kind == OmsPageKind.News && route.Query.Length != 0)
            {
                if (!int.TryParse(route.Query, NumberStyles.None, CultureInfo.InvariantCulture, out int selectedYear)
                    || selectedYear is < 1 or > 9999)
                {
                    showOmsError("这个新闻年份不符合约定，请返回新闻首页。", revision);
                    return;
                }
                year = selectedYear;
            }

            omsSidebar.SetPosts(posts, year);
            Drawable display;
            if (route.Kind == OmsPageKind.Article)
            {
                OmsNewsPost? post = posts.SingleOrDefault(candidate => candidate.Slug == route.Slug);
                if (post == null)
                {
                    showOmsError("这篇新闻不存在，请返回新闻列表。", revision);
                    return;
                }
                Header.SetOmsPage(post.Title);
                display = new OmsMarkdownContainer(url => omsGame?.OpenOmsLink(url))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    DocumentMargin = new MarginPadding(0),
                    DocumentPadding = new MarginPadding { Vertical = 25, Left = 30, Right = HORIZONTAL_PADDING },
                    Text = "# " + OmsContentMarkdown.Escape(post.Title) + "\n\n" + post.PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                           + "\n\n" + OmsContentMarkdown.Paragraphs(post.Body),
                };
            }
            else
            {
                Header.SetOmsPage(year?.ToString(CultureInfo.InvariantCulture));
                OmsNewsPost[] selected = posts.Where(post => year == null || post.PublishedAt.Year == year).ToArray();
                var listing = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Padding = new MarginPadding { Vertical = 20, Left = 30, Right = HORIZONTAL_PADDING },
                };
                if (selected.Length == 0)
                    listing.Add(new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
                    { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = "目前没有新闻。" });
                foreach (OmsNewsPost post in selected)
                    listing.Add(new OmsNewsCard(post, () => openOmsArticle(post)));
                display = listing;
            }

            ScrollFlow.ScrollToStart();
            var cancellation = omsOperation ??= new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            LoadComponentAsync(display, loaded =>
            {
                if (!isCurrentOmsLoad(revision, token))
                {
                    loaded.Dispose();
                    return;
                }
                content.Child = loaded;
                Loading.Hide();
                Schedule(() => { if (isCurrentOmsLoad(revision, token)) ScrollFlow.ScrollTo(scroll, false); });
            }, token);
        }

        private void showOmsError(string message, int revision)
        {
            if (revision != omsRevision || IsDisposed) return;
            content.Child = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Vertical = 25, Left = 30, Right = HORIZONTAL_PADDING },
                Text = message,
            };
            Loading.Hide();
        }

        private bool isCurrentOmsLoad(int revision, CancellationToken token)
            => !IsDisposed && revision == omsRevision && !token.IsCancellationRequested && State.Value == Visibility.Visible;

        private void cancelOmsLoading()
        {
            omsRevision++;
            omsOperation?.Cancel();
            omsOperation?.Dispose();
            omsOperation = null;
        }

        protected override void PopOut()
        {
            if (omsOnly)
            {
                cancelOmsLoading();
                displayUpdateRequired = true;
            }
            base.PopOut();
        }
    }
}
