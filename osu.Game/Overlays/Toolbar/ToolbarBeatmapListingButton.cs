// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Input.Bindings;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarBeatmapListingButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarBeatmapListingButton()
        {
            Hotkey = GlobalAction.ToggleBeatmapListing;
        }

        [BackgroundDependencyLoader(true)]
        private void load(BeatmapListingOverlay beatmapListing, OsuGame? game)
        {
            StateContainer = beatmapListing;
            if (game?.OnlineFeaturesEnabled == false)
                game.BeatmapBrowserSource.BindValueChanged(_ =>
                {
                    StateContainer = game.BeatmapBrowserOverlay;
                    Action = game.ToggleBeatmapBrowser;
                }, true);
        }
    }
}
