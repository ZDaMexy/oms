// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Online.IR;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarOmsServicesButton : ToolbarButton
    {
        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            TooltipMain = "OMS 在线服务";
            TooltipSub = "社区、新闻、帮助与玩家资料";
            SetIcon(FontAwesome.Solid.Globe);
            Action = () => game.OpenOmsPage(new OmsPageRoute());
        }
    }
}
