// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarOmsIrButton : ToolbarOverlayToggleButton
    {
        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            StateContainer = game.Ir;
            TooltipMain = "谱面排行榜";
            TooltipSub = "选择来源，查看同谱面成绩";
            SetIcon(FontAwesome.Solid.Trophy);
            Action = () => game.Ir?.ToggleVisibility();
        }
    }
}
