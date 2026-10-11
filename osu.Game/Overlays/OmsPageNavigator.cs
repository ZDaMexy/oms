// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    /// <summary>Navigation between the native pages; this component draws no portal or browser shell.</summary>
    public partial class OmsPageNavigator : Component
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private OmsIrService service { get; set; } = null!;

        private readonly List<(OmsPageRoute Route, float Scroll)> history = new List<(OmsPageRoute, float)>();
        private IOmsPageOverlay? activeView;
        private OmsPageRoute route = new OmsPageRoute { Kind = OmsPageKind.News };
        private long? owner;
        private bool requiresLogin;
        private int navigationRevision;
        public OmsPageRoute Route => activeView?.CurrentOmsRoute ?? (route.Kind == OmsPageKind.Board ? game.Ir?.CurrentRoute : null) ?? route;
        public int HistoryDepth => history.Count;

        [BackgroundDependencyLoader]
        private void load()
        {
            owner = service.State.Account?.Id;
            requiresLogin = service.State.RequiresLogin;
            service.StateChanged += onAccountChanged;
        }

        public void Open(OmsPageRoute requested)
        {
            if (requested.Kind == OmsPageKind.Home) requested = requested with { Kind = OmsPageKind.News };
            history.Clear();
            show(requested, false, 0);
        }

        public void OpenRelated(OmsPageRoute requested)
        {
            if (requested.Kind == OmsPageKind.Home) requested = requested with { Kind = OmsPageKind.News };
            if (Route != requested)
            {
                history.Add((Route, activeView?.OmsScroll ?? Route.BoardScroll));
                if (history.Count > 16) history.RemoveAt(0);
            }
            show(requested, false, 0);
        }

        public void Back()
        {
            if (history.Count == 0) { navigationRevision++; game.HideOmsPages(); return; }
            var previous = history[^1];
            history.RemoveAt(history.Count - 1);
            show(previous.Route, true, previous.Scroll);
        }

        public void Resume() => show(Route, true, activeView?.OmsScroll ?? Route.BoardScroll);

        public void ReplaceBrowserSource(OmsPageRoute requested, float scroll)
            => show(requested, true, scroll);

        public void OpenBrowser(OmsPageRoute requested, float scroll)
        {
            history.Clear();
            show(requested, true, scroll);
        }

        public Func<bool> BeginTemporaryPage()
        {
            int requestedRevision = ++navigationRevision;
            return () => requestedRevision == navigationRevision;
        }

        public Func<bool> ShowPrivateAccount()
        {
            history.Clear();
            activeView = null;
            route = new OmsPageRoute { Kind = OmsPageKind.Account };
            return BeginTemporaryPage();
        }

        public void NavigateFromBoard(OmsPageRoute next, OmsPageRoute previous)
        {
            activeView = null;
            route = previous;
            OpenRelated(next);
        }

        private void show(OmsPageRoute requested, bool restore, float scroll)
        {
            route = requested;
            activeView = null;
            int requestedRevision = ++navigationRevision;
            game.DisplayOmsPage(requested, restore, scroll,
                () => requestedRevision == navigationRevision,
                view => activeView = view);
        }

        private void onAccountChanged(OmsIrState state) => Schedule(() =>
        {
            OmsIrState current = service.State;
            if (owner == current.Account?.Id && requiresLogin == current.RequiresLogin) return;
            owner = current.Account?.Id;
            requiresLogin = current.RequiresLogin;
            history.Clear();
            navigationRevision++;
        });

        protected override void Dispose(bool isDisposing)
        {
            if (service != null) service.StateChanged -= onAccountChanged;
            base.Dispose(isDisposing);
        }
    }
}
