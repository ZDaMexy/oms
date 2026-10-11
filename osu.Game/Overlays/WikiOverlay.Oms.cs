// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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
using osu.Game.Overlays.Wiki;
using osu.Game.Overlays.Wiki.Markdown;

namespace osu.Game.Overlays
{
    public partial class WikiOverlay
    {
        private readonly bool omsOnly;

        [Resolved(canBeNull: true)]
        private OmsIrService? omsService { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? omsGame { get; set; }

        private CancellationTokenSource? omsOperation;
        private int omsRevision;
        private JObject? omsDocument;
        private float omsRestoreScroll;

        public OmsPageRoute CurrentOmsRoute { get; private set; } = new OmsPageRoute { Kind = OmsPageKind.Help };

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
            if (route.Kind != OmsPageKind.Help)
                throw new ArgumentException("The wiki overlay requires a help route.", nameof(route));

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
            if (route.Kind != OmsPageKind.Help)
                throw new ArgumentException("The help overlay requires a help route.", nameof(route));

            CurrentOmsRoute = route;
            omsRestoreScroll = scroll;
            cancelOmsLoading();
            displayUpdateRequired = true;
            if (IsLoaded && omsDocument != null)
            {
                displayUpdateRequired = false;
                Loading.Show();
                Child = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
                renderOmsContent(omsDocument, route, omsRevision, scroll);
            }
            Show();
        }

        private void beginOmsLoading()
        {
            displayUpdateRequired = false;
            cancelOmsLoading();
            Header.SetOmsPage();
            Loading.Show();
            Child = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            articlePage = null;
            if (omsService == null)
            {
                showOmsError("OMS 帮助服务尚未准备好，请稍后刷新。", omsRevision);
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
                Schedule(() =>
                {
                    if (!isCurrentOmsLoad(revision, token)) return;
                    omsDocument = document;
                    renderOmsContent(document, route, revision, scroll);
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
                Schedule(() => { if (isCurrentOmsLoad(revision, token)) showOmsError("网站帮助格式不符合约定，请稍后刷新。", revision); });
            }
        }

        private void renderOmsContent(JObject document, OmsPageRoute route, int revision, float scroll)
        {
            JObject[] sections = document["help"]!.Cast<JObject>().ToArray();
            Drawable display;
            if (route.HelpAnchor == null)
            {
                Header.SetOmsPage();
                articlePage = null;
                display = new WikiMainPage(sections, url => omsGame?.OpenOmsLink(url))
                {
                    Padding = new MarginPadding { Vertical = 20, Horizontal = HORIZONTAL_PADDING },
                };
            }
            else
            {
                JObject? section = sections.SingleOrDefault(candidate => candidate["id"]!.Value<string>() == route.HelpAnchor);
                if (section == null)
                {
                    showOmsError("这节帮助不存在，请返回帮助首页选择章节。", revision);
                    return;
                }
                Header.SetOmsPage(section["heading"]!.Value<string>());
                display = articlePage = new WikiArticlePage(route.WebsiteUrl(omsService!.State.ServiceAddress), OmsContentMarkdown.Section(section),
                    url => omsGame?.OpenOmsLink(url), new OmsWikiSidebar(sections, route.HelpAnchor, url => omsGame?.OpenOmsLink(url)));
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
                Child = loaded;
                Loading.Hide();
                Schedule(() => { if (isCurrentOmsLoad(revision, token)) ScrollFlow.ScrollTo(scroll, false); });
            }, token);
        }

        private void showOmsError(string message, int revision)
        {
            if (revision != omsRevision || IsDisposed) return;
            articlePage = null;
            Child = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Vertical = 25, Horizontal = HORIZONTAL_PADDING },
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
