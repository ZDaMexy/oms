// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Interface for the ghost-TD (timing-offset ghost) F2 display. The playfield pushes the timing offset
    /// (in milliseconds, negative = early, positive = late) of the most recent judgement through this interface.
    /// Custom skins implement it to display timing feedback without concrete-type casting.
    /// </summary>
    public interface IBmsGhostTdDisplay
    {
        void SetTimingOffset(float offsetMs);
    }

    /// <summary>
    /// Default ghost-TD display: a thin vertical bar at the judgement line that moves left (early) or right (late)
    /// based on the timing offset, then fades out. Uses additive blending for visibility over any background.
    /// No texture support — this is a pure programmatic indicator. Skin authors wanting a custom ghost-TD
    /// should implement <see cref="IBmsGhostTdDisplay"/> via the code-type provider route.
    /// </summary>
    public partial class DefaultBmsGhostTdDisplay : CompositeDrawable, IBmsGhostTdDisplay
    {
        private const float max_offset_ms = 150f;
        private const float max_offset_x = 0.4f;

        private readonly Box indicator;

        public DefaultBmsGhostTdDisplay()
        {
            // Positioned at the judgement line (bottom of playfield), centred horizontally.
            RelativeSizeAxes = Axes.X;
            Height = 20;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Blending = BlendingParameters.Additive;
            Alpha = 0;

            InternalChild = indicator = new Box
            {
                Width = 4,
                Height = 20,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativePositionAxes = Axes.X,
                Colour = Color4.White,
            };
        }

        public void SetTimingOffset(float offsetMs)
        {
            // Map timing offset to horizontal position: ±max_offset_ms → ±max_offset_x (relative to playfield width).
            float clamped = Math.Clamp(offsetMs / max_offset_ms, -1f, 1f);
            indicator.X = clamped * max_offset_x;

            // Colour shifts from green (perfect) toward red (early/late).
            float severity = Math.Abs(clamped);
            indicator.Colour = new Color4(
                (byte)(128 + 127 * severity),
                (byte)(255 - 179 * severity),
                (byte)(77 * (1f - severity)),
                255);

            this.FadeIn(30, Easing.OutQuint)
                .Then()
                .FadeOut(800, Easing.OutQuint);
        }
    }
}
