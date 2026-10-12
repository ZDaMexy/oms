// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.Sayobot;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadHeader : BeatmapBrowserHeader
    {
        private BeatmapBrowserOptionFilterRow<int> keys = null!;
        private BeatmapBrowserOptionFilterRow<StarRange> stars = null!;
        private BeatmapBrowserOptionFilterRow<SayobotCategory> category = null!;
        public Bindable<int> Keys => keys.Current;
        public Bindable<StarRange> Stars => stars.Current;
        public Bindable<SayobotCategory> Category => category.Current;

        public SayobotSearchQuery SearchQuery
        {
            get
            {
                int range = (int)Stars.Value;
                return new SayobotSearchQuery(Query.Value, Keys.Value == 0 ? null : Keys.Value,
                    range == 0 ? null : range - 1, range is 0 or 11 ? null : range, Category.Value);
            }
        }

        public enum StarRange
        {
            All,
            UpToOne,
            OneToTwo,
            TwoToThree,
            ThreeToFour,
            FourToFive,
            FiveToSix,
            SixToSeven,
            SevenToEight,
            EightToNine,
            NineToTen,
            TenAndAbove,
        }

        protected override LocalisableString SearchPlaceholder => BeatmapDownloadStrings.SearchMania;

        protected override IEnumerable<Drawable> CreateBrowserFilters() => new Drawable[]
        {
            keys = new BeatmapBrowserOptionFilterRow<int>(BeatmapDownloadStrings.Keys, Enumerable.Range(0, 19),
                value => value == 0 ? BeatmapDownloadStrings.AllKeys : $"{value}K"),
            stars = new BeatmapBrowserOptionFilterRow<StarRange>(BeatmapDownloadStrings.Stars, Enum.GetValues<StarRange>(),
                value => value == StarRange.All ? BeatmapDownloadStrings.AllStars
                    : value == StarRange.TenAndAbove ? "★10+" : $"★{(int)value - 1}–{(int)value}"),
            category = new BeatmapBrowserOptionFilterRow<SayobotCategory>(BeatmapDownloadStrings.Category, Enum.GetValues<SayobotCategory>(),
                value => value switch
                {
                    SayobotCategory.Ranked => BeatmapDownloadStrings.Ranked,
                    SayobotCategory.Qualified => BeatmapDownloadStrings.Qualified,
                    SayobotCategory.Loved => BeatmapDownloadStrings.Loved,
                    SayobotCategory.Pending => BeatmapDownloadStrings.Pending,
                    SayobotCategory.Graveyard => BeatmapDownloadStrings.Graveyard,
                    _ => BeatmapDownloadStrings.AllCategories,
                }),
        };
    }
}
