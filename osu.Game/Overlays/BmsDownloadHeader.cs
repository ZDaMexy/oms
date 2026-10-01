// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Bms;
using osu.Game.Overlays.BeatmapListing;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadHeader : OverlayHeader
    {
        private SourceFilterRow source = null!;
        private TableDropdown tables = null!;
        private LevelDropdown levels = null!;
        private BasicSearchTextBox search = null!;
        private Box background = null!;

        public Bindable<BmsDownloadSource> Source => source.Current;
        public Bindable<string> Table => tables.Current;
        public Bindable<TableLevel> Level => levels.Current;
        public BmsDownloadTable? SelectedTable => tables.SelectedTable;
        public Bindable<string> Query => search.Current;
        public Action? Retry { get; set; }

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
                                    search = new DownloadSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        PlaceholderText = BmsDownloadStrings.Search,
                                    },
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
                                source = new SourceFilterRow(),
                                filterField(BmsDownloadStrings.Table, tables = new TableDropdown()),
                                filterField(BmsDownloadStrings.TableLevel, levels = new LevelDropdown()),
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
        public void ClearTables() => tables.SetTables(Array.Empty<BmsDownloadTable>());
        public void SetTables(IReadOnlyList<BmsDownloadTable> values) => tables.SetTables(values);
        public void ClearLevels() => levels.Clear();
        public void SetLevelsLoading() => levels.SetStatus(BmsDownloadStrings.LevelsLoading);
        public void SetLevelsFailed() => levels.SetStatus(BmsDownloadStrings.LevelsFailed);
        public void SetLevels(BmsDownloadTableData data) => levels.SetLevels(data.Symbol, data.Levels);

        public readonly record struct TableLevel(string? Value);

        private static Drawable filterField(LocalisableString label, Drawable dropdown)
        {
            dropdown.RelativeSizeAxes = Axes.X;
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
                        dropdown,
                    },
                },
            };
        }

        private partial class DownloadTitle : OverlayTitle
        {
            public DownloadTitle()
            {
                Title = BmsDownloadStrings.Title;
                Description = BmsDownloadStrings.Description;
                Icon = OsuIcon.Beatmap;
            }
        }

        private partial class DownloadSearchTextBox : BasicSearchTextBox
        {
            protected override bool AllowCommit => true;
        }

        private partial class SourceFilterRow : BeatmapSearchFilterRow<BmsDownloadSource>
        {
            public SourceFilterRow()
                : base(BmsDownloadStrings.Source)
            {
            }

            protected override Drawable CreateFilter() => new SourceFilter();

            private partial class SourceFilter : BeatmapSearchFilter
            {
                protected override TabItem<BmsDownloadSource> CreateTabItem(BmsDownloadSource value) => new SourceTab(value);
            }

            private partial class SourceTab : FilterTabItem<BmsDownloadSource>
            {
                public SourceTab(BmsDownloadSource value)
                    : base(value)
                {
                }

                protected override LocalisableString LabelFor(BmsDownloadSource value) => value == BmsDownloadSource.Ginger ? "Ginger Rush" : "616 / Alvorna";
            }
        }

        private partial class TableDropdown : OsuDropdown<string>
        {
            private readonly Dictionary<string, BmsDownloadTable> values = new Dictionary<string, BmsDownloadTable>();

            public BmsDownloadTable? SelectedTable => values.GetValueOrDefault(Current.Value);

            public TableDropdown() => SetTables(Array.Empty<BmsDownloadTable>());

            public void SetTables(IReadOnlyList<BmsDownloadTable> values)
            {
                string previous = Current.Value ?? string.Empty;
                this.values.Clear();
                foreach (var value in values)
                    this.values[value.Id] = value;
                Items = new[] { string.Empty }.Concat(this.values.Keys);
                Current.Value = this.values.ContainsKey(previous) ? previous : string.Empty;
            }

            protected override LocalisableString GenerateItemText(string item) => item.Length == 0 ? BmsDownloadStrings.AllTables : values[item].Name;
        }

        private partial class LevelDropdown : OsuDropdown<TableLevel>
        {
            private string symbol = string.Empty;
            private LocalisableString emptyText = BmsDownloadStrings.SelectTable;

            public LevelDropdown() => Clear();

            public void Clear() => SetStatus(BmsDownloadStrings.SelectTable);

            public void SetStatus(LocalisableString text)
            {
                emptyText = text;
                Current.Disabled = false;
                Items = new[] { new TableLevel(null) };
                Current.Value = new TableLevel(null);
                Current.Disabled = true;
            }

            public void SetLevels(string tableSymbol, IReadOnlyList<string> values)
            {
                var previous = Current.Value;
                symbol = tableSymbol;
                emptyText = BmsDownloadStrings.AllLevels;
                Current.Disabled = false;
                Items = new[] { new TableLevel(null) }.Concat(values.Select(value => new TableLevel(value)));
                Current.Value = previous.Value != null && values.Contains(previous.Value) ? previous : new TableLevel(null);
                Current.Disabled = values.Count == 0;
            }

            protected override LocalisableString GenerateItemText(TableLevel item) => item.Value == null ? emptyText
                : item.Value.Length == 0 ? BmsDownloadStrings.Ungraded : symbol + item.Value;
        }
    }
}
