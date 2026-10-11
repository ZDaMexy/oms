// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
        private readonly Bindable<BeatmapCardSize> cardSize = new Bindable<BeatmapCardSize>(BeatmapCardSize.Normal);
        public bool ReserveExpandedSpace { get; init; } = true;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        public BeatmapBrowserCardFlow()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);
            Margin = new MarginPadding { Top = 15, Bottom = ExpandedContentScrollContainer.HEIGHT + 20 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (game != null)
                cardSize.BindTo(game.BeatmapBrowserCardSize);
        }

        protected override void Update()
        {
            base.Update();
            int maxColumns = cardSize.Value == BeatmapCardSize.Extra ? 2 : 3;
            int columns = Math.Clamp((int)((DrawWidth + Spacing.X) / (BeatmapCard.WIDTH + Spacing.X)), 1, maxColumns);
            float width = Math.Max(0, (DrawWidth - Spacing.X * (columns - 1)) / columns);
            float scale = width / BeatmapCard.WIDTH;
            foreach (T card in this)
                card.Scale = new Vector2(scale);

            // Expanded difficulties remain outside the fixed card height, as in lazer's native grid.
            Margin = new MarginPadding { Top = 15, Bottom = (ReserveExpandedSpace ? ExpandedContentScrollContainer.HEIGHT * scale : 0) + 20 };
        }
    }
}
