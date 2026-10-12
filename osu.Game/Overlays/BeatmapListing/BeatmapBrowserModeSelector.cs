// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.BeatmapListing
{
    public enum BeatmapBrowserMode { Bms, Mania }

    public partial class BeatmapBrowserModeSelector : BeatmapSearchFilterRow<BeatmapBrowserMode>
    {
        public BeatmapBrowserModeSelector() : base(BeatmapDownloadStrings.Mode)
        {
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame? game)
        {
            if (game == null) return;
            game.BeatmapBrowserSource.BindValueChanged(change =>
                Current.Value = change.NewValue == BeatmapBrowserSource.Sayobot ? BeatmapBrowserMode.Mania : BeatmapBrowserMode.Bms, true);
            Current.BindValueChanged(change => game.SelectBeatmapBrowserMode(change.NewValue));
        }

        protected override Drawable CreateFilter() => new ModeFilter();

        private partial class ModeFilter : BeatmapSearchFilter
        {
            [BackgroundDependencyLoader(true)]
            private void load(OsuGame? game)
            {
                if (game == null) return;
                if (!game.BmsDownloadsEnabled) RemoveItem(BeatmapBrowserMode.Bms);
                if (!game.ManiaDownloadsEnabled) RemoveItem(BeatmapBrowserMode.Mania);
            }

            protected override TabItem<BeatmapBrowserMode> CreateTabItem(BeatmapBrowserMode value) => new ModeTab(value);
        }

        private partial class ModeTab : FilterTabItem<BeatmapBrowserMode>
        {
            public ModeTab(BeatmapBrowserMode value) : base(value)
            {
            }

            protected override LocalisableString LabelFor(BeatmapBrowserMode value) => value == BeatmapBrowserMode.Bms ? "BMS" : "osu!mania";
        }
    }
}
