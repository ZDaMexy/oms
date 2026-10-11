// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.Bms;
using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadOverlay : IOmsPageOverlay
    {
        private OmsPageRoute? browserResultsRoute;
        private OmsPageRoute? pendingBrowserFilters;
        private float browserRestoreScroll;

        public OmsPageRoute CurrentOmsRoute => pendingBrowserFilters ?? new OmsPageRoute
        {
            Kind = OmsPageKind.Library,
            Ruleset = "bms",
            Sources = Header.Source.Value == BmsDownloadSource.Konmai ? "alvorna" : "ginger",
            Query = Header.Query.Value,
            BrowserTable = Header.Table.Value.Length == 0 ? null : Header.Table.Value,
            BrowserLevel = Header.Level.Value.Value,
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
            if (route.Kind != OmsPageKind.Library || route.Ruleset != "bms")
                throw new ArgumentException("The BMS browser requires a BMS library route.", nameof(route));
            route = route with { Sources = route.Sources == "alvorna" ? "alvorna" : "ginger" };
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
            pendingBrowserFilters = route.BrowserTable != null ? route : null;
            Header.Source.Value = route.Sources == "alvorna" ? BmsDownloadSource.Konmai : BmsDownloadSource.Ginger;
            Header.Query.Value = route.Query;
            Header.Table.Value = string.Empty;
            Header.ClearLevels();
            Show();
            openingSearch = false;
            if (pendingBrowserFilters == null) search(true);
            // The source's table catalogue is loaded by Show; its levels must exist before restoring a level.
            else if (Header.HasTable(route.BrowserTable!)) restoreBrowserTable();
        }

        private void restoreBrowserTable()
        {
            if (pendingBrowserFilters is not { } requested) return;
            openingSearch = true;
            bool selected = Header.SelectTable(requested.BrowserTable!);
            openingSearch = false;
            if (!selected)
            {
                status.Text = "所选难度表已不在该来源中，请重新选择。";
                return;
            }
            if (requested.BrowserLevel == null)
            {
                pendingBrowserFilters = null;
                search(true);
            }
            else if (tableData != null) restoreBrowserLevel();
        }

        private void restoreBrowserLevel()
        {
            if (pendingBrowserFilters is not { } requested || tableData == null) return;
            if (!tableData.Levels.Contains(requested.BrowserLevel!))
            {
                status.Text = "所选等级已不在该难度表中，请重新选择。";
                return;
            }
            openingSearch = true;
            Header.Level.Value = new BmsDownloadHeader.TableLevel(requested.BrowserLevel);
            openingSearch = false;
            pendingBrowserFilters = null;
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
