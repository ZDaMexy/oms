// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.IR;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.BeatmapSet;

namespace osu.Game.Overlays
{
    public partial class BeatmapListingOverlay
    {
        private OmsChartPageContent? omsContent;
        private OmsPageRoute? omsRoute;
        private bool omsLoadPending;
        private bool omsResumePending;
        private bool omsLoading;
        private float omsRestoreScroll;

        public OmsPageRoute? CurrentOmsRoute => omsContent?.Route.Kind == OmsPageKind.Charts ? omsContent.Route : omsRoute;

        public float OmsScroll
        {
            get => (float)ScrollFlow.Current;
            set
            {
                omsRestoreScroll = value;
                if (IsLoaded) ScrollFlow.ScrollTo(value, false);
            }
        }

        public void ShowOmsPage(OmsPageRoute route)
        {
            requireOmsChartRoute(route);
            omsRoute = route;
            omsRestoreScroll = 0;
            omsResumePending = false;
            omsLoadPending = true;
            if (IsLoaded && State.Value == Visibility.Visible) openOmsCharts();
            Show();
        }

        public void RestoreOmsPage(OmsPageRoute route, float scroll)
        {
            requireOmsChartRoute(route);
            omsRoute = route;
            omsRestoreScroll = scroll;
            omsResumePending = true;
            omsLoadPending = true;
            if (IsLoaded && State.Value == Visibility.Visible) openOmsCharts();
            Show();
        }

        private void requireOmsChartRoute(OmsPageRoute route)
        {
            if (!omsOnly) throw new InvalidOperationException("This overlay is not using OMS chart data.");
            if (route.Kind != OmsPageKind.Charts) throw new ArgumentException("The chart listing overlay requires a charts route.", nameof(route));
        }

        private void initialiseOmsContent()
        {
            var header = (OmsBeatmapListingHeader)Header;
            header.SearchRequested = query => ShowOmsPage((CurrentOmsRoute ?? new OmsPageRoute { Kind = OmsPageKind.Charts }) with { Query = query, Page = 1 });
            Child = omsContent = new OmsChartPageContent
            {
                Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING, Vertical = 20 },
                Rendered = () =>
                {
                    header.SetQuery(omsContent!.Route.Query);
                    float scroll = omsRestoreScroll;
                    OmsPageRoute renderedRoute = omsContent.Route;
                    omsRestoreScroll = 0;
                    Schedule(() =>
                    {
                        if (State.Value == Visibility.Visible && CurrentOmsRoute == renderedRoute)
                            ScrollFlow.ScrollTo(scroll, false);
                    });
                },
            };
        }

        private void openOmsCharts()
        {
            if (!omsLoadPending || omsRoute == null) return;
            omsLoadPending = false;
            ((OmsBeatmapListingHeader)Header).SetQuery(omsRoute.Query);
            ScrollFlow.ScrollToStart();
            if (omsResumePending) omsContent!.Resume(omsRoute);
            else omsContent!.Open(omsRoute);
            omsResumePending = false;
        }

        protected override void PopIn()
        {
            base.PopIn();
            if (omsOnly) openOmsCharts();
        }

        protected override void PopOut()
        {
            if (omsOnly)
            {
                omsContent?.CancelOperation();
                omsLoadPending = false;
            }
            base.PopOut();
        }

        private void updateOmsLoading()
        {
            if (omsContent == null || omsLoading == omsContent.IsLoading) return;
            omsLoading = omsContent.IsLoading;
            if (omsLoading) Loading.Show();
            else Loading.Hide();
        }
    }
}
