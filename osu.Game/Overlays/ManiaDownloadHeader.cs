// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Sayobot;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadHeader : BeatmapBrowserHeader
    {
        private KeysDropdown keys = null!;
        private StarsDropdown stars = null!;
        private CategoryDropdown category = null!;
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
            new BeatmapDownloadModeSelector(),
            filterField(BeatmapDownloadStrings.Keys, keys = new KeysDropdown()),
            filterField(BeatmapDownloadStrings.Stars, stars = new StarsDropdown()),
            filterField(BeatmapDownloadStrings.Category, category = new CategoryDropdown()),
        };

        private static Drawable filterField(LocalisableString label, Drawable value)
        {
            value.RelativeSizeAxes = Axes.X;
            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 100), new Dimension() },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new[]
                    {
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = label,
                        },
                        value,
                    },
                },
            };
        }

        private partial class KeysDropdown : OsuDropdown<int>
        {
            public KeysDropdown() => Items = Enumerable.Range(0, 19);
            protected override LocalisableString GenerateItemText(int item) => item == 0 ? BeatmapDownloadStrings.AllKeys : $"{item}K";
        }

        private partial class StarsDropdown : OsuDropdown<StarRange>
        {
            public StarsDropdown() => Items = Enum.GetValues<StarRange>();
            protected override LocalisableString GenerateItemText(StarRange item) => item == StarRange.All ? BeatmapDownloadStrings.AllStars
                : item == StarRange.TenAndAbove ? "★10+" : $"★{(int)item - 1}–{(int)item}";
        }

        private partial class CategoryDropdown : OsuDropdown<SayobotCategory>
        {
            public CategoryDropdown() => Items = Enum.GetValues<SayobotCategory>();
            protected override LocalisableString GenerateItemText(SayobotCategory item) => item switch
            {
                SayobotCategory.Ranked => BeatmapDownloadStrings.Ranked,
                SayobotCategory.Qualified => BeatmapDownloadStrings.Qualified,
                SayobotCategory.Loved => BeatmapDownloadStrings.Loved,
                SayobotCategory.Pending => BeatmapDownloadStrings.Pending,
                SayobotCategory.Graveyard => BeatmapDownloadStrings.Graveyard,
                _ => BeatmapDownloadStrings.AllCategories,
            };
        }

    }
}
