// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics.Colour;
using osuTK.Graphics;

namespace osu.Game.Rulesets
{
    public readonly record struct SongSelectPanelAccent(ColourInfo AccentColour, Color4 ForegroundColour)
    {
        public SongSelectPanelAccent(Color4 accentColour, Color4 foregroundColour)
            : this(ColourInfo.GradientVertical(accentColour, accentColour), foregroundColour)
        {
        }
    }
}
