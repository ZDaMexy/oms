// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Sayobot;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadHeader : OverlayHeader
    {
        private BasicSearchTextBox search = null!;
        private KeysDropdown keys = null!;
        private StarsDropdown stars = null!;
        private CategoryDropdown category = null!;
        private Box background = null!;

        public Bindable<string> Query => search.Current;
        public Bindable<int> Keys => keys.Current;
        public Bindable<StarRange> Stars => stars.Current;
        public Bindable<SayobotCategory> Category => category.Current;
        public Action? Retry { get; set; }

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

        protected override OverlayTitle CreateTitle() => new DownloadTitle();

        protected override Drawable CreateContent() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 20 },
                    Spacing = new Vector2(0, 20),
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 40) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    search = new DownloadSearchTextBox { RelativeSizeAxes = Axes.X, Height = 40, PlaceholderText = BeatmapDownloadStrings.SearchMania },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.RedoAlt,
                                        TooltipText = BmsDownloadStrings.SearchNow,
                                        Action = () => Retry?.Invoke(),
                                    },
                                },
                            },
                        },
                        new ReverseChildIDFillFlowContainer<Drawable>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Horizontal = 10 },
                            Spacing = new Vector2(0, 5),
                            Children = new Drawable[]
                            {
                                new BeatmapDownloadModeSelector(),
                                filterField(BmsDownloadStrings.Source, new OsuSpriteText { Text = BeatmapDownloadStrings.Sayobot, Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold) }),
                                filterField(BeatmapDownloadStrings.Keys, keys = new KeysDropdown()),
                                filterField(BeatmapDownloadStrings.Stars, stars = new StarsDropdown()),
                                filterField(BeatmapDownloadStrings.Category, category = new CategoryDropdown()),
                            },
                        },
                    },
                },
            },
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            background.Colour = colour.Dark6;
            search.OnCommit += (_, _) => Retry?.Invoke();
        }

        public void FocusSearch() => search.TakeFocus();

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

        private partial class DownloadSearchTextBox : BasicSearchTextBox
        {
            protected override bool AllowCommit => true;
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

        private partial class DownloadTitle : OverlayTitle
        {
            public DownloadTitle()
            {
                Title = BeatmapDownloadStrings.Title;
                Description = BeatmapDownloadStrings.Description;
                Icon = OsuIcon.Beatmap;
            }
        }
    }
}
