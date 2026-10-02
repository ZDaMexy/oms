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
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Bms;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadOverlay : OnlineOverlay<BmsDownloadHeader>
    {
        private BmsDownloadManager downloads = null!;
        private BeatmapManager beatmaps = null!;
        private OsuGame game = null!;
        private RealmAccess realm = null!;

        private readonly Dictionary<string, BmsDownloadCard> cards = new Dictionary<string, BmsDownloadCard>();
        private FillFlowContainer<BmsDownloadCard> results = null!;
        private OsuSpriteText status = null!;
        private OsuSpriteText tableStatus = null!;
        private ShowMoreButton more = null!;
        private BeatmapDownloadCoverStore covers = null!;
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
        private bool tablesFailed;

        public IReadOnlyCollection<BmsDownloadCard> Cards => cards.Values;

        public BmsDownloadOverlay()
            : base(OverlayColourScheme.Blue, requiresSignIn: false)
        {
        }

        protected override BmsDownloadHeader CreateHeader() => new BmsDownloadHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background6;

        [BackgroundDependencyLoader]
        private void load(GameHost host, BmsDownloadManager downloads, BeatmapManager beatmaps, OsuGame game, RealmAccess realm)
        {
            this.downloads = downloads;
            this.beatmaps = beatmaps;
            this.game = game;
            this.realm = realm;
            covers = new BeatmapDownloadCoverStore(host, new BeatmapDownloadCoverResourceStore());

            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = ColourProvider.Background5 },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 20, Top = 15, Bottom = 20 },
                        Spacing = new Vector2(0, 15),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer<OsuSpriteText>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new[]
                                {
                                    tableStatus = new OsuSpriteText { Font = OsuFont.Default.With(size: 18), Alpha = 0 },
                                    status = new OsuSpriteText { Font = OsuFont.Default.With(size: 18) },
                                },
                            },
                            results = new ReverseChildIDFillFlowContainer<BmsDownloadCard>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(10),
                                Margin = new MarginPadding { Bottom = ExpandedContentScrollContainer.HEIGHT + 20 },
                            },
                            more = new ShowMoreButton
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Action = () => search(reset: false),
                                Alpha = 0,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            more.Text = BmsDownloadStrings.BrowseMore;
            Header.Source.BindValueChanged(_ =>
            {
                tablesFailed = false;
                tableStatus.Hide();
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
                if (tablesFailed)
                    loadTables();
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
                more.IsLoading = false;
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
            tablesFailed = false;
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
                    {
                        tableStatus.Hide();
                        Header.SetTables(tables);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Log($"Could not load BMS download source tables.\n{ex}", LoggingTarget.Network);
                Schedule(() =>
                {
                    if (!IsDisposed && !cancellation.IsCancellationRequested && Header.Source.Value == source)
                    {
                        tablesFailed = true;
                        tableStatus.Text = BmsDownloadStrings.TableFailed;
                        tableStatus.Show();
                    }
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
            more.IsLoading = true;
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
                    more.Text = BmsDownloadStrings.BrowseMore;
                    more.IsLoading = false;
                    more.Action = () => search(reset: false);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Log($"Could not search the selected BMS download source.\n{ex}", LoggingTarget.Network);
                Schedule(() =>
                {
                    if (IsDisposed || requestRevision != revision || cancellation.IsCancellationRequested)
                        return;

                    searching = false;
                    paginationPaused = true;
                    Loading.Hide();
                    status.Text = BmsDownloadStrings.SearchFailed;
                    more.Alpha = 1;
                    more.Text = BmsDownloadStrings.Retry;
                    more.IsLoading = false;
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
                Logger.Log($"Could not read the selected BMS table's levels.\n{ex}", LoggingTarget.Network);
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
            {
                // Import commits on a worker thread, before Realm's update-thread notification may arrive.
                if (task.Progress.State == BmsDownloadState.Completed)
                    realm.Realm.Refresh();

                card.Refresh();
            }
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

}
