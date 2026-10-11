// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.IR;
using osu.Game.Overlays.BeatmapSet;

namespace osu.Game.Overlays
{
    public partial class BeatmapSetOverlay
    {
        private OmsChartPageContent? omsContent;
        private OmsPageRoute? omsRoute;
        private bool omsLoadPending;
        private bool omsResumePending;
        private bool omsLoading;
        private float omsRestoreScroll;

        public OmsPageRoute? CurrentOmsRoute => omsContent?.Route.Kind == OmsPageKind.Chart ? omsContent.Route : omsRoute;

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
            if (IsLoaded && State.Value == Visibility.Visible) openOmsChart();
            Show();
        }

        public void RestoreOmsPage(OmsPageRoute route, float scroll)
        {
            requireOmsChartRoute(route);
            omsRoute = route;
            omsRestoreScroll = scroll;
            omsResumePending = true;
            omsLoadPending = true;
            if (IsLoaded && State.Value == Visibility.Visible) openOmsChart();
            Show();
        }

        private void requireOmsChartRoute(OmsPageRoute route)
        {
            if (!omsOnly) throw new InvalidOperationException("This overlay is not using OMS chart data.");
            if (route.Kind != OmsPageKind.Chart) throw new ArgumentException("The chart detail overlay requires a chart route.", nameof(route));
        }

        private void initialiseOmsContent()
        {
            Child = omsContent = new OmsChartPageContent
            {
                Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING, Vertical = Y_PADDING },
                Rendered = () =>
                {
                    float scroll = omsRestoreScroll;
                    OmsPageRoute renderedRoute = omsContent!.Route;
                    omsRestoreScroll = 0;
                    Schedule(() =>
                    {
                        if (State.Value == Visibility.Visible && CurrentOmsRoute == renderedRoute)
                            ScrollFlow.ScrollTo(scroll, false);
                    });
                },
            };
        }

        private void openOmsChart()
        {
            if (!omsLoadPending || omsRoute == null) return;
            omsLoadPending = false;
            ((OmsBeatmapSetHeader)Header).SetRuleset(omsRoute.Ruleset);
            ScrollFlow.ScrollToStart();
            if (omsResumePending) omsContent!.Resume(omsRoute);
            else omsContent!.Open(omsRoute);
            omsResumePending = false;
        }

        protected override void PopIn()
        {
            base.PopIn();
            if (omsOnly) openOmsChart();
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

        protected override void Update()
        {
            base.Update();
            if (!omsOnly || omsContent == null || omsLoading == omsContent.IsLoading) return;
            omsLoading = omsContent.IsLoading;
            if (omsLoading) Loading.Show();
            else Loading.Hide();
        }

        protected override void Dispose(bool isDisposing)
        {
            omsContent?.CancelOperation();
            base.Dispose(isDisposing);
        }
    }
}
