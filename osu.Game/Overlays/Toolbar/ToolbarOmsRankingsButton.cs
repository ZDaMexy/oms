// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarOmsRankingsButton : ToolbarButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            TooltipMain = "玩家排行";
            TooltipSub = "在 OMS 网站查看当前玩法与键型";
            SetIcon(FontAwesome.Solid.ChartBar);
            Action = game.OpenOmsRankings;
        }
    }
}
