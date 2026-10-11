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
using osu.Game.Online.Bms;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadHeader : BeatmapBrowserHeader
    {
        public Bindable<BmsDownloadSource> Source { get; } = new Bindable<BmsDownloadSource>();
        private TableDropdown tables = null!;
        private LevelDropdown levels = null!;
        public Bindable<string> Table => tables.Current;
        public Bindable<TableLevel> Level => levels.Current;
        public BmsDownloadTable? SelectedTable => tables.SelectedTable;

        protected override LocalisableString SearchPlaceholder => "搜索曲名、艺术家或原谱 MD5…";

        protected override IEnumerable<Drawable> CreateBrowserFilters() => new Drawable[]
        {
            new BeatmapDownloadModeSelector(),
            filterField(BmsDownloadStrings.Table, tables = new TableDropdown()),
            filterField(BmsDownloadStrings.TableLevel, levels = new LevelDropdown()),
        };

        public void ClearTables() => tables.SetTables(Array.Empty<BmsDownloadTable>());
        public void SetTables(IReadOnlyList<BmsDownloadTable> values) => tables.SetTables(values);
        public void ClearLevels() => levels.Clear();
        public void SetLevelsLoading() => levels.SetStatus(BmsDownloadStrings.LevelsLoading);
        public void SetLevelsFailed() => levels.SetStatus(BmsDownloadStrings.LevelsFailed);
        public void SetLevels(BmsDownloadTableData data) => levels.SetLevels(data.Symbol, data.Levels);
        public bool SelectTable(string id) => tables.SelectTable(id);
        public bool HasTable(string id) => tables.HasTable(id);

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

        private partial class TableDropdown : OsuDropdown<string>
        {
            private readonly Dictionary<string, BmsDownloadTable> values = new Dictionary<string, BmsDownloadTable>();

            public BmsDownloadTable? SelectedTable => values.GetValueOrDefault(Current.Value);
            public bool HasTable(string id) => values.ContainsKey(id);

            public bool SelectTable(string id)
            {
                if (!values.ContainsKey(id)) return false;
                Current.Value = id;
                return true;
            }

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
