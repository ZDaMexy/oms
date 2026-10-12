// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
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
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>The common native search and source controls for the OMS beatmap browser.</summary>
    public abstract partial class BeatmapBrowserHeader : BeatmapListingHeader
    {
        private BrowserSearchTextBox search = null!;
        private BeatmapListingCardSizeTabControl cardSize = null!;
        private Box searchBackground = null!;
        private Box layoutBackground = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        public Bindable<string> Query => search.Current;
        public Action? Retry { get; set; }

        protected virtual LocalisableString SearchPlaceholder => "搜索曲名、艺术家或谱面…";

        protected abstract IEnumerable<Drawable> CreateBrowserFilters();
        protected abstract BeatmapBrowserMode BrowserMode { get; }

        protected virtual void CommitSearch() => Retry?.Invoke();

        protected override OverlayTitle CreateTitle() => new BrowserTitle();

        protected override Drawable CreateContent()
        {
            var filters = new ReverseChildIDFillFlowContainer<Drawable>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 10 },
                Spacing = new Vector2(0, 5),
            };
            filters.Add(new BeatmapBrowserModeSelector());
            filters.Add(new BeatmapBrowserSourceSelector(BrowserMode));
            filters.AddRange(CreateBrowserFilters());

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            searchBackground = new Box { RelativeSizeAxes = Axes.Both },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 20 },
                                Spacing = new Vector2(0, 16),
                                Children = new Drawable[]
                                {
                                    search = new BrowserSearchTextBox(CommitSearch)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        LengthLimit = 200,
                                        PlaceholderText = SearchPlaceholder,
                                    },
                                    filters,
                                },
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Children = new Drawable[]
                        {
                            layoutBackground = new Box { RelativeSizeAxes = Axes.Both },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Padding = new MarginPadding { Left = 20, Right = 110 },
                                Text = BmsDownloadStrings.SourceOrder,
                                Font = OsuFont.GetFont(size: 13),
                            },
                            cardSize = new BeatmapListingCardSizeTabControl
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Margin = new MarginPadding { Right = 20 },
                            },
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            searchBackground.Colour = colour.Dark6;
            layoutBackground.Colour = colour.Background4;
            search.OnCommit += (_, _) => CommitSearch();
            if (game != null)
                cardSize.Current.BindTo(game.BeatmapBrowserCardSize);
        }

        public void SetQuery(string query) => search.Text = query;

        public void FocusSearch() => search.TakeFocus();

        protected static Drawable FilterField(LocalisableString label, Drawable dropdown) => new BrowserFilterField(label, dropdown);

        private partial class BrowserFilterField : CompositeDrawable
        {
            private readonly Container field;

            public BrowserFilterField(LocalisableString label, Drawable dropdown)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                dropdown.RelativeSizeAxes = Axes.X;
                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 100), new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                            {
                                RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Text = label,
                            },
                            field = new Container { AutoSizeAxes = Axes.Y, Child = dropdown },
                        },
                    },
                };
            }

            protected override void Update()
            {
                base.Update();
                field.Width = Math.Min(400, Math.Max(0, DrawWidth - 100));
            }
        }

        private partial class BrowserSearchTextBox : BasicSearchTextBox
        {
            protected override bool AllowCommit => true;

            public BrowserSearchTextBox(Action search)
            {
                RemoveAll(child => child is SpriteIcon, true);
                Add(new IconButton
                {
                    Icon = FontAwesome.Solid.Search,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Margin = new MarginPadding { Right = 5 },
                    TooltipText = "搜索",
                    Action = search,
                });
            }
        }

        private partial class BrowserTitle : OverlayTitle
        {
            public BrowserTitle()
            {
                Title = "谱面列表";
                Description = "查找、下载和查看 BMS / osu!mania 谱面";
                Icon = OsuIcon.Beatmap;
            }
        }
    }
}
