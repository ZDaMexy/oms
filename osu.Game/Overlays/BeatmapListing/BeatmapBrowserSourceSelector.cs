// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.BeatmapListing
{
    public enum BeatmapBrowserSource { Ginger, Alvorna, Sayobot }

    public partial class BeatmapBrowserSourceSelector : BeatmapSearchFilterRow<BeatmapBrowserSource>
    {
        private readonly BeatmapBrowserMode mode;
        private SourceFilter filter = null!;

        public BeatmapBrowserSourceSelector(BeatmapBrowserMode mode) : base("来源")
        {
            this.mode = mode;
            filter.Clear();
            if (mode == BeatmapBrowserMode.Bms)
            {
                filter.AddItem(BeatmapBrowserSource.Ginger);
                filter.AddItem(BeatmapBrowserSource.Alvorna);
                Current.Value = BeatmapBrowserSource.Ginger;
            }
            else
            {
                filter.AddItem(BeatmapBrowserSource.Sayobot);
                Current.Value = BeatmapBrowserSource.Sayobot;
            }
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame? game)
        {
            if (game == null) return;
            game.BeatmapBrowserSource.BindValueChanged(change =>
            {
                if ((change.NewValue == BeatmapBrowserSource.Sayobot) == (mode == BeatmapBrowserMode.Mania))
                    Current.Value = change.NewValue;
            }, true);
            Current.BindValueChanged(change =>
            {
                if ((game.BeatmapBrowserSource.Value == BeatmapBrowserSource.Sayobot) == (mode == BeatmapBrowserMode.Mania))
                    game.BeatmapBrowserSource.Value = change.NewValue;
            });
        }

        protected override Drawable CreateFilter() => filter = new SourceFilter();

        private partial class SourceFilter : BeatmapSearchFilter
        {
            protected override TabItem<BeatmapBrowserSource> CreateTabItem(BeatmapBrowserSource value) => new SourceTab(value);
        }

        private partial class SourceTab : FilterTabItem<BeatmapBrowserSource>
        {
            public SourceTab(BeatmapBrowserSource value) : base(value)
            {
            }

            protected override LocalisableString LabelFor(BeatmapBrowserSource value) => value switch
            {
                BeatmapBrowserSource.Ginger => "Ginger Rush",
                BeatmapBrowserSource.Alvorna => "616 / Alvorna",
                _ => "Sayobot",
            };
        }
    }
}
