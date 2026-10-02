// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
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
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Sayobot;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadOverlay : OnlineOverlay<ManiaDownloadHeader>
    {
        private ManiaDownloadManager downloads = null!;
        private BeatmapManager beatmaps = null!;
        private OsuGame game = null!;
        private RealmAccess realm = null!;
        private readonly BeatmapDownloadCoverResourceStore coverSource;
        private readonly Dictionary<string, ManiaDownloadCard> cards = new Dictionary<string, ManiaDownloadCard>();
        private FillFlowContainer<ManiaDownloadCard> results = null!;
        private OsuSpriteText status = null!;
        private ShowMoreButton more = null!;
        private BeatmapDownloadCoverStore covers = null!;
        private CancellationTokenSource? queryCancellation;
        private ScheduledDelegate? debounce;
        private int revision;
        private int nextOffset;
        private bool hasMore;
        private bool searching;
        private bool paginationPaused;

        public IReadOnlyCollection<ManiaDownloadCard> Cards => cards.Values;

        public ManiaDownloadOverlay(BeatmapDownloadCoverResourceStore? coverSource = null)
            : base(OverlayColourScheme.Blue, requiresSignIn: false)
        {
            this.coverSource = coverSource ?? new BeatmapDownloadCoverResourceStore();
        }

        protected override ManiaDownloadHeader CreateHeader() => new ManiaDownloadHeader();
        protected override Color4 BackgroundColour => ColourProvider.Background6;

        [BackgroundDependencyLoader]
        private void load(GameHost host, ManiaDownloadManager downloads, BeatmapManager beatmaps, OsuGame game, RealmAccess realm)
        {
            this.downloads = downloads;
            this.beatmaps = beatmaps;
            this.game = game;
            this.realm = realm;
            covers = new BeatmapDownloadCoverStore(host, coverSource);
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
                            status = new OsuSpriteText(),
                            results = new ReverseChildIDFillFlowContainer<ManiaDownloadCard>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(10),
                                Margin = new MarginPadding { Bottom = ExpandedContentScrollContainer.HEIGHT + 20 },
                            },
                            more = new ShowMoreButton { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Action = () => search(false), Alpha = 0 },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            more.Text = BmsDownloadStrings.BrowseMore;
            Header.Query.BindValueChanged(_ => queueSearch());
            Header.Keys.BindValueChanged(_ => queueSearch());
            Header.Stars.BindValueChanged(_ => queueSearch());
            Header.Category.BindValueChanged(_ => queueSearch());
            Header.Retry = () => search(true);
            downloads.TaskChanged += onTaskChanged;
            State.BindValueChanged(visibilityChanged, true);
        }

        private void visibilityChanged(ValueChangedEvent<Visibility> state)
        {
            if (state.NewValue == Visibility.Hidden)
            {
                debounce?.Cancel();
                queryCancellation?.Cancel();
                revision++;
                searching = false;
                more.IsLoading = false;
                Loading.Hide();
                return;
            }

            Header.FocusSearch();
            if (cards.Count == 0)
                search(true);
            else
                foreach (var card in cards.Values)
                    card.Refresh();
        }

        private void queueSearch()
        {
            if (State.Value != Visibility.Visible)
                return;

            queryCancellation?.Cancel();
            revision++;
            debounce?.Cancel();
            searching = true;
            nextOffset = 0;
            hasMore = false;
            results.Clear();
            cards.Clear();
            more.Alpha = 0;
            debounce = Scheduler.AddDelayed(() => search(true), 350);
        }

        private async void search(bool reset)
        {
            if (State.Value != Visibility.Visible || (!reset && (searching || !hasMore)))
                return;

            debounce?.Cancel();
            queryCancellation?.Cancel();
            queryCancellation?.Dispose();
            queryCancellation = new CancellationTokenSource();
            var cancellation = queryCancellation;
            int requestRevision = ++revision;
            int requestedOffset = reset ? 0 : nextOffset;
            searching = true;
            more.IsLoading = true;
            if (reset)
                Loading.Show();

            try
            {
                var response = await downloads.Client.SearchAsync(Header.SearchQuery, requestedOffset, cancellation.Token).ConfigureAwait(false);
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

                    foreach (var set in response.Sets)
                    {
                        if (cards.TryGetValue(set.Key, out var existing))
                            existing.Merge(set);
                        else
                        {
                            var card = new ManiaDownloadCard(set, downloads, covers, findLocal, id => game.PresentDownloadedManiaBeatmap(id));
                            cards.Add(set.Key, card);
                            results.Add(card);
                        }
                    }

                    nextOffset = response.NextOffset;
                    hasMore = response.HasMore;
                    searching = false;
                    paginationPaused = false;
                    Loading.Hide();
                    status.Text = cards.Count == 0 ? BmsDownloadStrings.NoResults : string.Empty;
                    more.Alpha = hasMore ? 1 : 0;
                    more.IsLoading = false;
                    more.Text = BmsDownloadStrings.BrowseMore;
                    more.Action = () => search(false);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.Log($"Could not search the Sayobot mania catalogue.\n{ex}", LoggingTarget.Network);
                Schedule(() =>
                {
                    if (IsDisposed || requestRevision != revision || cancellation.IsCancellationRequested)
                        return;
                    searching = false;
                    paginationPaused = true;
                    Loading.Hide();
                    status.Text = BmsDownloadStrings.SearchFailed;
                    more.Alpha = 1;
                    more.IsLoading = false;
                    more.Text = BmsDownloadStrings.Retry;
                    more.Action = () => search(reset);
                });
            }
        }

        private Guid? findLocal(int id) => beatmaps.QueryBeatmap("Ruleset.ShortName == $0 AND OnlineID == $1", "mania", id)?.ID;
        private void onTaskChanged(ManiaDownloadTask task) => Scheduler.AddOnce(refreshTask, task);

        private void refreshTask(ManiaDownloadTask task)
        {
            if (!IsDisposed && cards.TryGetValue(task.Set.Key, out var card))
            {
                // Import commits on a worker thread, before Realm's update-thread notification may arrive.
                if (task.Progress.State == ManiaDownloadState.Completed)
                    realm.Realm.Refresh();

                card.Refresh();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (State.Value == Visibility.Visible && !searching && !paginationPaused && hasMore
                && ScrollFlow.ScrollableExtent - ScrollFlow.Current < 200)
                search(false);
        }

        protected override void Dispose(bool isDisposing)
        {
            downloads.TaskChanged -= onTaskChanged;
            debounce?.Cancel();
            queryCancellation?.Cancel();
            queryCancellation?.Dispose();
            covers.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
