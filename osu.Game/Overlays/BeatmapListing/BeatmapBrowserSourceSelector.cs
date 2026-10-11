// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.BeatmapListing
{
    public enum BeatmapBrowserSource { Oms, Ginger, Alvorna, Sayobot }

    public partial class BeatmapBrowserSourceSelector : BeatmapSearchFilterRow<BeatmapBrowserSource>
    {
        public BeatmapBrowserSourceSelector() : base("来源")
        {
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame? game)
        {
            if (game != null) Current.BindTo(game.BeatmapBrowserSource);
        }

        protected override Drawable CreateFilter() => new SourceFilter();

        private partial class SourceFilter : BeatmapSearchFilter
        {
            [BackgroundDependencyLoader(true)]
            private void load(OsuGame? game)
            {
                if (game == null) return;
                if (!game.BmsDownloadsEnabled)
                {
                    RemoveItem(BeatmapBrowserSource.Ginger);
                    RemoveItem(BeatmapBrowserSource.Alvorna);
                }
                if (!game.ManiaDownloadsEnabled) RemoveItem(BeatmapBrowserSource.Sayobot);
            }

            protected override TabItem<BeatmapBrowserSource> CreateTabItem(BeatmapBrowserSource value) => new SourceTab(value);
        }

        private partial class SourceTab : FilterTabItem<BeatmapBrowserSource>
        {
            public SourceTab(BeatmapBrowserSource value) : base(value)
            {
            }

            protected override LocalisableString LabelFor(BeatmapBrowserSource value) => value switch
            {
                BeatmapBrowserSource.Oms => "OMS",
                BeatmapBrowserSource.Ginger => "Ginger Rush",
                BeatmapBrowserSource.Alvorna => "616 / Alvorna",
                _ => "Sayobot",
            };
        }
    }
}
