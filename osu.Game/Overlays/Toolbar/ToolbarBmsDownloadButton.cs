// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Input.Bindings;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarBmsDownloadButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarBmsDownloadButton()
        {
            Hotkey = GlobalAction.ToggleBeatmapListing;
        }

        [BackgroundDependencyLoader]
        private void load(BmsDownloadOverlay downloads, OsuGame game)
        {
            StateContainer = downloads;
            Action = game.ToggleBmsDownloadBrowser;
        }
    }
}
