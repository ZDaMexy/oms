// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Input.Bindings;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarBmsDownloadButton : ToolbarOverlayToggleButton
    {
        private readonly Bindable<BeatmapDownloadMode> mode = new Bindable<BeatmapDownloadMode>();

        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarBmsDownloadButton()
        {
            Hotkey = GlobalAction.ToggleBeatmapListing;
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            mode.BindTo(game.DownloadMode);
            mode.BindValueChanged(_ =>
            {
                StateContainer = mode.Value == BeatmapDownloadMode.Bms ? game.BmsDownloads : game.ManiaDownloads;
                Action = game.ToggleBeatmapDownloadBrowser;
            }, true);
        }
    }
}
