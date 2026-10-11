// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    public interface IOmsPageOverlay
    {
        OmsPageRoute? CurrentOmsRoute { get; }
        float OmsScroll { get; set; }
        void ShowOmsPage(OmsPageRoute route);
        void RestoreOmsPage(OmsPageRoute route, float scroll);
    }
}
