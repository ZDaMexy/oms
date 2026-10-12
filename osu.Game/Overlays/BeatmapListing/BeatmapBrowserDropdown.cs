// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>The compact dropdown used for unbounded source table and level lists.</summary>
    public partial class BeatmapBrowserDropdown<T> : OsuDropdown<T>
    {
        protected override DropdownHeader CreateHeader() => new BrowserDropdownHeader();

        private partial class BrowserDropdownHeader : OsuDropdownHeader
        {
            public BrowserDropdownHeader()
            {
                Height = 22;
                Margin = new MarginPadding();
                Foreground.Padding = new MarginPadding { Horizontal = 6, Vertical = 3 };
                Text.Font = OsuFont.GetFont(size: 13);
                Chevron.Size = new Vector2(8);
            }
        }
    }
}
