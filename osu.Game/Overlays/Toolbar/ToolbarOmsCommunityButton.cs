// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.IR;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarOmsCommunityButton : ToolbarOverlayToggleButton
    {
        [BackgroundDependencyLoader]
        private void load(OmsCommunityOverlay community, OsuGame game)
        {
            StateContainer = community;
            Action = () =>
            {
                if (community.State.Value == Visibility.Visible) community.Hide();
                else game.OpenOmsPage(new OmsPageRoute { Kind = OmsPageKind.Community });
            };
        }
    }
}
