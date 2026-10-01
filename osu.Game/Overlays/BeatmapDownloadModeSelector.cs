// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays
{
    public enum BeatmapDownloadMode
    {
        Bms,
        Mania,
    }

    public partial class BeatmapDownloadModeSelector : BeatmapSearchFilterRow<BeatmapDownloadMode>
    {
        public BeatmapDownloadModeSelector()
            : base(BeatmapDownloadStrings.Mode)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            Current.BindTo(game.DownloadMode);
            Alpha = game.BmsDownloadsEnabled && game.ManiaDownloadsEnabled ? 1 : 0;
        }

        protected override Drawable CreateFilter() => new ModeFilter();

        private partial class ModeFilter : BeatmapSearchFilter
        {
            protected override TabItem<BeatmapDownloadMode> CreateTabItem(BeatmapDownloadMode value) => new ModeTab(value);
        }

        private partial class ModeTab : FilterTabItem<BeatmapDownloadMode>
        {
            public ModeTab(BeatmapDownloadMode value)
                : base(value)
            {
            }

            protected override LocalisableString LabelFor(BeatmapDownloadMode value) => value == BeatmapDownloadMode.Bms ? "BMS" : "osu!mania";
        }
    }
}
