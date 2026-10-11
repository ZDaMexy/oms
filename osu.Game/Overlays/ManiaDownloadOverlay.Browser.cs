// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadOverlay : IOmsPageOverlay
    {
        private OmsPageRoute? browserResultsRoute;
        private float browserRestoreScroll;

        public OmsPageRoute CurrentOmsRoute => new OmsPageRoute
        {
            Kind = OmsPageKind.Library,
            Ruleset = "mania",
            Query = Header.Query.Value,
            BrowserKeys = Header.Keys.Value,
            BrowserStars = (int)Header.Stars.Value,
            BrowserCategory = Header.Category.Value,
        };

        public float OmsScroll
        {
            get => browserRestoreScroll != 0 ? browserRestoreScroll : (float)ScrollFlow.Current;
            set => ScrollFlow.ScrollTo(value, false);
        }

        public void ShowOmsPage(OmsPageRoute route) => showBrowserRoute(route, false, 0);
        public void RestoreOmsPage(OmsPageRoute route, float scroll) => showBrowserRoute(route, true, scroll);

        private void showBrowserRoute(OmsPageRoute route, bool restore, float scroll)
        {
            if (route.Kind != OmsPageKind.Library || route.Ruleset != "mania")
                throw new ArgumentException("The mania browser requires a mania library route.", nameof(route));
            browserRestoreScroll = scroll;
            if (restore && route == browserResultsRoute && route == CurrentOmsRoute && cards.Count > 0)
            {
                Show();
                restoreBrowserScroll();
                return;
            }
            debounce?.Cancel();
            queryCancellation?.Cancel();
            revision++;
            results.Clear();
            cards.Clear();
            browserResultsRoute = null;
            searching = true;
            more.Alpha = 0;
            status.Text = string.Empty;
            openingSearch = true;
            Header.Query.Value = route.Query;
            Header.Keys.Value = route.BrowserKeys;
            Header.Stars.Value = (ManiaDownloadHeader.StarRange)route.BrowserStars;
            Header.Category.Value = route.BrowserCategory;
            Show();
            openingSearch = false;
            search(true);
        }

        private void restoreBrowserScroll()
        {
            float scroll = browserRestoreScroll;
            OmsPageRoute route = CurrentOmsRoute;
            browserRestoreScroll = 0;
            Schedule(() =>
            {
                if (State.Value == Visibility.Visible && CurrentOmsRoute == route) ScrollFlow.ScrollTo(scroll, false);
            });
        }
    }
}
