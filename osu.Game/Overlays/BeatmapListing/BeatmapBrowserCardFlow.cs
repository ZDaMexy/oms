// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>A responsive native card grid shared by every beatmap source.</summary>
    public partial class BeatmapBrowserCardFlow<T> : ReverseChildIDFillFlowContainer<T> where T : Drawable
    {
        public BeatmapBrowserCardFlow()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);
            Margin = new MarginPadding { Top = 15, Bottom = ExpandedContentScrollContainer.HEIGHT + 20 };
        }

        protected override void Update()
        {
            base.Update();
            float width = Math.Min(BeatmapCard.WIDTH, Math.Max(0, DrawWidth));
            int columns = Math.Max(1, (int)((DrawWidth + Spacing.X) / (width + Spacing.X)));
            float sidePadding = Math.Max(0, (DrawWidth - columns * width - (columns - 1) * Spacing.X) / 2);
            Padding = new MarginPadding { Horizontal = sidePadding };
            foreach (T card in this)
            {
                card.Width = width;
                card.Scale = Vector2.One;
            }
        }
    }
}
