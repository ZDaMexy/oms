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
            TooltipMain = "OMS IR";
            TooltipSub = "主动连接、交分与试验榜";
            SetIcon(FontAwesome.Solid.Trophy);
            Action = () => game.Ir?.ToggleVisibility();
        }
    }
}
