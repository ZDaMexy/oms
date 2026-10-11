// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.IR;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarWikiButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        [BackgroundDependencyLoader(true)]
        private void load(WikiOverlay wiki, OsuGame? game)
        {
            StateContainer = wiki;
            if (game?.OnlineFeaturesEnabled == false)
                Action = () =>
                {
                    if (wiki.State.Value == Visibility.Visible) wiki.Hide();
                    else game.OpenOmsPage(new OmsPageRoute { Kind = OmsPageKind.Help });
                };
        }
    }
}
