// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Bms;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadOverlay : OnlineOverlay<BmsDownloadHeader>
    {
        private BmsDownloadManager downloads = null!;
        private BeatmapManager beatmaps = null!;
        private OsuGame game = null!;

        private readonly Dictionary<string, BmsDownloadCard> cards = new Dictionary<string, BmsDownloadCard>();
        private FillFlowContainer<BmsDownloadCard> results = null!;
        private OsuSpriteText status = null!;
        private SettingsButtonV2 more = null!;
        private TextureStore covers = null!;
        private CancellationTokenSource? queryCancellation;
        private CancellationTokenSource? tableCancellation;
        private CancellationTokenSource? levelCancellation;
        private BmsDownloadTableData? tableData;
        private ScheduledDelegate? debounce;
        private int revision;
        private int page;
        private int totalPages;
        private bool searching;
        private bool paginationPaused;

        public IReadOnlyCollection<BmsDownloadCard> Cards => cards.Values;

        public BmsDownloadOverlay()
            : base(OverlayColourScheme.Blue, requiresSignIn: false)
        {
        }

        protected override BmsDownloadHeader CreateHeader() => new BmsDownloadHeader();

        [BackgroundDependencyLoader]
        private void load(GameHost host, BmsDownloadManager downloads, BeatmapManager beatmaps, OsuGame game)
        {
            this.downloads = downloads;
            this.beatmaps = beatmaps;
            this.game = game;
            covers = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(new BmsCoverResourceStore()));

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(20),
                Spacing = new Vector2(0, 15),
                Children = new Drawable[]
                {
                    status = new OsuSpriteText { Font = OsuFont.Default.With(size: 18) },
                    results = new FillFlowContainer<BmsDownloadCard>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(12),
                    },
                    more = new SettingsButtonV2
                    {
                        Text = BmsDownloadStrings.BrowseMore,
                        RelativeSizeAxes = Axes.X,
                        Action = () => search(reset: false),
                        Alpha = 0,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Header.Source.BindValueChanged(_ =>
            {
                Header.ClearTables();
                if (State.Value == Visibility.Visible)
                    loadTables();
                queueSearch();
            });
            Header.Table.BindValueChanged(_ =>
            {
                levelCancellation?.Cancel();
                tableData = null;
                Header.ClearLevels();
                if (State.Value == Visibility.Visible && Header.SelectedTable != null)
                    loadLevels();
                queueSearch();
            });
            Header.Level.BindValueChanged(_ => queueSearch());
            Header.Query.BindValueChanged(_ => queueSearch());
            Header.Retry = () =>
            {
                if (Header.SelectedTable != null && tableData == null)
                    loadLevels();
                search(reset: true);
            };
            downloads.TaskChanged += onTaskChanged;
            State.BindValueChanged(visibilityChanged, true);
        }

        private void visibilityChanged(ValueChangedEvent<Visibility> state)
        {
            if (state.NewValue == Visibility.Hidden)
            {
                debounce?.Cancel();
                queryCancellation?.Cancel();
                tableCancellation?.Cancel();
                levelCancellation?.Cancel();
                revision++;
                searching = false;
                Loading.Hide();
                return;
            }

            Header.FocusSearch();
            loadTables();
            if (Header.SelectedTable != null && tableData == null)
                loadLevels();
            if (cards.Count == 0)
                search(reset: true);
            else
                refreshCards();
        }

        private void queueSearch()
        {
            if (State.Value != Visibility.Visible)
                return;

            queryCancellation?.Cancel();
            revision++;
            debounce?.Cancel();
            searching = true;
            page = 0;
            results.Clear();
            cards.Clear();
            more.Alpha = 0;
            debounce = Scheduler.AddDelayed(() => search(reset: true), 350);
        }

        private async void loadTables()
        {
            tableCancellation?.Cancel();
            tableCancellation?.Dispose();
            tableCancellation = new CancellationTokenSource();
            var cancellation = tableCancellation;
            var source = Header.Source.Value;

            try
            {
                var tables = await downloads.Client.GetTablesAsync(source, cancellation.Token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (!IsDisposed && !cancellation.IsCancellationRequested && Header.Source.Value == source)
                        Header.SetTables(tables);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Error(ex, "Could not load BMS download source tables.");
                Schedule(() =>
                {
                    if (!IsDisposed && !cancellation.IsCancellationRequested && Header.Source.Value == source)
                        status.Text = BmsDownloadStrings.TableFailed;
                });
            }
        }

        private async void search(bool reset)
        {
            if (State.Value != Visibility.Visible || (!reset && (searching || page >= totalPages)))
                return;

            debounce?.Cancel();
            queryCancellation?.Cancel();
            queryCancellation?.Dispose();
            queryCancellation = new CancellationTokenSource();
            var cancellation = queryCancellation;
            int requestRevision = ++revision;
            var source = Header.Source.Value;
            int requestedPage = reset ? 1 : page + 1;
            string query = Header.Query.Value;
            string table = Header.Table.Value;
            string? level = Header.Level.Value.Value;
            var currentTableData = tableData;
            if (level != null && currentTableData == null)
                return;
            searching = true;
            more.Enabled.Value = false;
            if (reset)
                Loading.Show();

            try
            {
                var response = level == null
                    ? await downloads.Client.SearchAsync(source, query, requestedPage,
                        table.Length == 0 ? null : table, cancellation.Token).ConfigureAwait(false)
                    : await downloads.Client.SearchTableLevelAsync(source, currentTableData!, level, query, requestedPage, cancellation.Token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || requestRevision != revision || cancellation.IsCancellationRequested)
                        return;

                    if (reset)
                    {
                        results.Clear();
                        cards.Clear();
                        ScrollFlow.ScrollToStart();
                    }

                    foreach (var package in response.Packages)
                    {
                        if (cards.TryGetValue(package.Key, out var existing))
                            existing.Merge(package);
                        else
                        {
                            var card = new BmsDownloadCard(package, downloads, covers, findLocal, id => game.PresentDownloadedBmsBeatmap(id));
                            cards.Add(package.Key, card);
                            results.Add(card);
                        }
                    }

                    page = response.Page;
                    totalPages = response.TotalPages;
                    searching = false;
                    paginationPaused = false;
                    Loading.Hide();
                    status.Text = cards.Count == 0 ? BmsDownloadStrings.NoResults : string.Empty;
                    more.Alpha = page < totalPages ? 1 : 0;
                    more.Enabled.Value = true;
                    more.Action = () => search(reset: false);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Error(ex, "Could not search the selected BMS download source.");
                Schedule(() =>
                {
                    if (IsDisposed || requestRevision != revision || cancellation.IsCancellationRequested)
                        return;

                    searching = false;
                    paginationPaused = true;
                    Loading.Hide();
                    status.Text = BmsDownloadStrings.SearchFailed;
                    more.Alpha = 1;
                    more.Enabled.Value = true;
                    more.Action = () => search(reset: reset);
                });
            }
        }

        private async void loadLevels()
        {
            levelCancellation?.Cancel();
            levelCancellation?.Dispose();
            levelCancellation = new CancellationTokenSource();
            var cancellation = levelCancellation;
            var source = Header.Source.Value;
            var table = Header.SelectedTable!;
            Header.SetLevelsLoading();

            try
            {
                var data = await downloads.Client.GetTableDataAsync(source, table, cancellation.Token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (IsDisposed || cancellation.IsCancellationRequested || Header.Source.Value != source || Header.Table.Value != table.Id)
                        return;

                    tableData = data;
                    Header.SetLevels(data);
                    if (Header.Level.Value.Value != null)
                        queueSearch();
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Error(ex, "Could not read the selected BMS table's levels.");
                Schedule(() =>
                {
                    if (!IsDisposed && !cancellation.IsCancellationRequested && Header.Source.Value == source && Header.Table.Value == table.Id)
                        Header.SetLevelsFailed();
                });
            }
        }

        private Guid? findLocal(string md5) => beatmaps.QueryBeatmap("Ruleset.ShortName == $0 AND MD5Hash == $1", "bms", md5)?.ID;

        private void onTaskChanged(BmsDownloadTask task) => Scheduler.AddOnce(refreshTask, task);

        private void refreshTask(BmsDownloadTask task)
        {
            if (!IsDisposed && cards.TryGetValue(task.Package.Key, out var card))
                card.Refresh();
        }

        private void refreshCards()
        {
            if (!IsDisposed)
                foreach (var card in cards.Values)
                    card.Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (State.Value == Visibility.Visible && !searching && !paginationPaused && page > 0 && page < totalPages
                && ScrollFlow.ScrollableExtent - ScrollFlow.Current < 200)
                search(reset: false);
        }

        protected override void Dispose(bool isDisposing)
        {
            downloads.TaskChanged -= onTaskChanged;
            queryCancellation?.Cancel();
            tableCancellation?.Cancel();
            levelCancellation?.Cancel();
            queryCancellation?.Dispose();
            tableCancellation?.Dispose();
            levelCancellation?.Dispose();
            covers.Dispose();
            base.Dispose(isDisposing);
        }
    }

    public partial class BmsDownloadHeader : OverlayHeader
    {
        private SourceDropdown source = null!;
        private TableDropdown tables = null!;
        private LevelDropdown levels = null!;
        private BasicSearchTextBox search = null!;

        public Bindable<BmsDownloadSource> Source => source.Current;
        public Bindable<string> Table => tables.Current;
        public Bindable<TableLevel> Level => levels.Current;
        public BmsDownloadTable? SelectedTable => tables.SelectedTable;
        public Bindable<string> Query => search.Current;
        public Action? Retry { get; set; }

        protected override OverlayTitle CreateTitle() => new DownloadTitle();

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding(20),
            Spacing = new Vector2(0, 12),
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(12),
                    Children = new Drawable[]
                    {
                        filterField(BmsDownloadStrings.Source, 200, source = new SourceDropdown()),
                        filterField(BmsDownloadStrings.Table, 330, tables = new TableDropdown()),
                        filterField(BmsDownloadStrings.TableLevel, 190, levels = new LevelDropdown()),
                    },
                },
                search = new BasicSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    PlaceholderText = BmsDownloadStrings.Search,
                },
                new SettingsButtonV2 { Text = BmsDownloadStrings.SearchNow, Action = () => Retry?.Invoke() },
            },
        };

        public void FocusSearch() => search.TakeFocus();
        public void ClearTables() => tables.SetTables(Array.Empty<BmsDownloadTable>());
        public void SetTables(IReadOnlyList<BmsDownloadTable> values) => tables.SetTables(values);
        public void ClearLevels() => levels.Clear();
        public void SetLevelsLoading() => levels.SetStatus(BmsDownloadStrings.LevelsLoading);
        public void SetLevelsFailed() => levels.SetStatus(BmsDownloadStrings.LevelsFailed);
        public void SetLevels(BmsDownloadTableData data) => levels.SetLevels(data.Symbol, data.Levels);

        public readonly record struct TableLevel(string? Value);

        private static Drawable filterField(LocalisableString label, float width, Drawable dropdown)
        {
            dropdown.RelativeSizeAxes = Axes.X;
            return new FillFlowContainer
            {
                Width = width,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = label, Font = OsuFont.Default.With(size: 14) },
                    dropdown,
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

        private partial class SourceDropdown : OsuDropdown<BmsDownloadSource>
        {
            public SourceDropdown()
            {
                Items = Enum.GetValues<BmsDownloadSource>();
                Current.Value = BmsDownloadSource.Ginger;
            }

            protected override LocalisableString GenerateItemText(BmsDownloadSource item) => item == BmsDownloadSource.Ginger ? "Ginger Rush" : "616 / Alvorna";
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

            public void Clear()
            {
                SetStatus(BmsDownloadStrings.SelectTable);
            }

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

    public partial class BmsDownloadCard : CompositeDrawable
    {
        private readonly BmsDownloadManager downloads;
        private readonly TextureStore covers;
        private readonly Func<string, Guid?> findLocal;
        private readonly Action<Guid> open;
        private FillFlowContainer chartList = null!;
        private TruncatingSpriteText selection = null!;
        private TruncatingSpriteText artist = null!;
        private OsuSpriteText status = null!;
        private SettingsButtonV2 action = null!;
        private SettingsButtonV2 preview = null!;
        private Sprite cover = null!;
        private BmsDownloadChart selected;
        private bool expanded;
        private readonly CancellationTokenSource coverCancellation = new CancellationTokenSource();

        [Resolved]
        private OsuGame game { get; set; } = null!;

        public BmsDownloadPackage Package { get; private set; }
        public string SelectedMd5 => selected.Md5;

        public BmsDownloadCard(BmsDownloadPackage package, BmsDownloadManager downloads, TextureStore covers,
                               Func<string, Guid?> findLocal, Action<Guid> open)
        {
            Package = package;
            selected = package.Charts[0];
            this.downloads = downloads;
            this.covers = covers;
            this.findLocal = findLocal;
            this.open = open;
            Width = 345;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            InternalChildren = new Drawable[]
            {
                new osu.Framework.Graphics.Shapes.Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background4 },
                cover = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill, Alpha = 0.3f },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(14),
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText { Text = Package.Name, RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold) },
                        artist = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 15) },
                        selection = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 16) },
                        new SettingsButtonV2 { Text = BmsDownloadStrings.Expand, Action = ToggleCharts },
                        chartList = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 5), Alpha = 0 },
                        status = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 14) },
                        action = new SettingsButtonV2 { RelativeSizeAxes = Axes.X },
                        preview = new SettingsButtonV2 { Text = BmsDownloadStrings.ExternalPreview, Action = () => game.OpenUrlExternally(selected.PreviewUrl!.AbsoluteUri) },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuildCharts();
            Refresh();
            if (Package.CoverUrl != null)
                loadCover(Package.CoverUrl);
        }

        private async void loadCover(Uri uri)
        {
            try
            {
                var texture = await covers.GetAsync(uri.AbsoluteUri, coverCancellation.Token).ConfigureAwait(false);
                if (coverCancellation.IsCancellationRequested)
                {
                    texture?.Dispose();
                    return;
                }
                Schedule(() =>
                {
                    if (!IsDisposed)
                        cover.Texture = texture;
                    else
                        texture?.Dispose();
                });
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void ToggleCharts()
        {
            expanded = !expanded;
            chartList.Alpha = expanded ? 1 : 0;
            chartList.AutoSizeAxes = expanded ? Axes.Y : Axes.None;
            if (!expanded)
                chartList.Height = 0;
            Refresh();
        }

        public void SelectChart(string md5)
        {
            selected = Package.Charts.Single(c => c.Md5 == md5);
            Refresh();
        }

        public void Merge(BmsDownloadPackage incoming)
        {
            Package = Package with { Charts = Package.Charts.Concat(incoming.Charts).DistinctBy(c => c.Md5).ToArray() };
            if (IsLoaded)
                rebuildCharts();
        }

        private void rebuildCharts()
        {
            chartList.Clear();
            foreach (var chart in Package.Charts)
            {
                var value = chart;
                string detail = chart.KeyCount.HasValue ? $" · {chart.KeyCount}K" : string.Empty;
                if (!string.IsNullOrEmpty(chart.Level))
                    detail += $" · {chart.Level}";
                chartList.Add(new SettingsButtonV2
                {
                    RelativeSizeAxes = Axes.X,
                    Text = chart.Title + " " + chart.DifficultyName + detail,
                    Action = () => SelectChart(value.Md5),
                });
            }
            chartList.AutoSizeAxes = expanded ? Axes.Y : Axes.None;
            if (!expanded)
                chartList.Height = 0;
        }

        public void Refresh()
        {
            if (!IsLoaded)
                return;

            var task = downloads.GetTask(Package.Key);
            var local = findLocal(selected.Md5);
            artist.Text = selected.Artist;
            selection.Text = selected.Title + " " + selected.DifficultyName;
            preview.Alpha = selected.PreviewUrl == null ? 0 : 1;

            if (local.HasValue)
            {
                status.Text = BmsDownloadStrings.Available;
                action.Text = BmsDownloadStrings.Open;
                action.Enabled.Value = true;
                action.Action = () => open(local.Value);
                return;
            }

            if (task != null && task.Progress.State is BmsDownloadState.Queued or BmsDownloadState.Downloading or BmsDownloadState.Importing)
            {
                status.Text = task.Progress.State switch
                {
                    BmsDownloadState.Queued => BmsDownloadStrings.Queued,
                    BmsDownloadState.Importing => BmsDownloadStrings.Importing,
                    _ => BmsDownloadStrings.Downloading,
                };
                if (task.Progress.TotalBytes > 0 && task.Progress.State == BmsDownloadState.Downloading)
                    status.Text = $"{Math.Clamp(100.0 * task.Progress.Bytes / task.Progress.TotalBytes.Value, 0, 100):0}%";
                action.Text = BmsDownloadStrings.Cancel;
                action.Enabled.Value = true;
                action.Action = task.Cancel;
                return;
            }

            bool unsupported = selected.KeyCount.HasValue && selected.KeyCount.Value is not (5 or 7 or 9 or 10 or 14)
                               || string.Equals(Path.GetExtension(selected.FileName), ".bmson", StringComparison.OrdinalIgnoreCase);
            status.Text = unsupported ? BmsDownloadStrings.UnsupportedChart : !Package.CanDownload ? BmsDownloadStrings.NoPackage : task?.Progress.State switch
            {
                BmsDownloadState.Failed => BmsDownloadStrings.Failed,
                BmsDownloadState.Cancelled => BmsDownloadStrings.Cancelled,
                _ => string.Empty,
            };
            action.Text = task == null ? BmsDownloadStrings.Download : BmsDownloadStrings.Retry;
            action.Action = StartDownload;
            action.Enabled.Value = Package.CanDownload && !unsupported;
        }

        public void StartDownload() => downloads.Download(Package, selected.Md5);

        protected override void Dispose(bool isDisposing)
        {
            coverCancellation.Cancel();
            coverCancellation.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
