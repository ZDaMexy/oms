// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    public partial class OmsCommunityOverlay : OnlineOverlay<OmsCommunityHeader>, IOmsPageOverlay
    {
        private readonly OmsCommunityPageContent page;
        private float restoreScroll;
        public OmsPageRoute? CurrentOmsRoute => page.Route;
        public float OmsScroll { get => (float)ScrollFlow.Current; set => ScrollFlow.ScrollTo(value, false); }

        public OmsCommunityOverlay()
            : base(OverlayColourScheme.Pink, false)
        {
            Child = page = new OmsCommunityPageContent { Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING, Vertical = 24 } };
            page.Rendered = () =>
            {
                OmsPageRoute renderedRoute = page.Route;
                float scroll = restoreScroll;
                ScheduleAfterChildren(() =>
                {
                    if (State.Value == Visibility.Visible && page.Route == renderedRoute) OmsScroll = scroll;
                });
            };
            State.BindValueChanged(state =>
            {
                if (state.NewValue != Visibility.Hidden) return;
                restoreScroll = 0;
                page.CancelOperation();
            });
        }

        protected override OmsCommunityHeader CreateHeader() => new OmsCommunityHeader();

        public void ShowOmsPage(OmsPageRoute route)
        {
            Show();
            restoreScroll = 0;
            ScrollFlow.ScrollToStart();
            page.Open(route);
        }

        public void RestoreOmsPage(OmsPageRoute route, float scroll)
        {
            Show();
            restoreScroll = scroll;
            page.Resume(route);
        }
    }

    public partial class OmsCommunityHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new CommunityTitle();
        protected override Drawable CreateBackground() => new OverlayHeaderBackground("Headers/news");

        private partial class CommunityTitle : OverlayTitle
        {
            public CommunityTitle()
            {
                Title = "社区";
                Description = "讨论、求助、反馈与作品展示";
                Icon = FontAwesome.Solid.Comments;
            }
        }
    }
}
