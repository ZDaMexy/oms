// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Bms;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadCard : CompositeDrawable
    {
        private readonly BmsDownloadManager downloads;
        private readonly TextureStore covers;
        private readonly Func<string, Guid?> findLocal;
        private readonly Action<Guid> open;
        private readonly BeatmapCardContent content = new BeatmapCardContent(BeatmapCardNormal.HEIGHT);
        private readonly BindableBool expanded = new BindableBool();
        private readonly Bindable<DownloadState> progressState = new Bindable<DownloadState>();
        private readonly BindableDouble progress = new BindableDouble();
        private readonly CancellationTokenSource coverCancellation = new CancellationTokenSource();

        private FillFlowContainer<BmsDownloadChartRow> chartList = null!;
        private TruncatingSpriteText title = null!;
        private TruncatingSpriteText selection = null!;
        private TruncatingSpriteText artist = null!;
        private TruncatingSpriteText status = null!;
        private BeatmapCardDownloadProgressBar progressBar = null!;
        private BmsDownloadCardButton action = null!;
        private BmsDownloadCardButton expand = null!;
        private BmsDownloadCardButton preview = null!;
        private Sprite cover = null!;
        private SpriteIcon placeholder = null!;
        private BmsDownloadChart selected;

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
            content.Expanded.BindTarget = expanded;
            Width = BeatmapCard.WIDTH;
            Height = BeatmapCardNormal.HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            InternalChild = content;
            content.MainContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background3 },
                    new Container
                    {
                        Size = new Vector2(BeatmapCardNormal.HEIGHT),
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background4 },
                            placeholder = new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(28),
                                Icon = OsuIcon.Beatmap,
                                Colour = colour.Light1,
                            },
                            cover = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill },
                        },
                    },
                    new Container
                    {
                        X = BeatmapCardNormal.HEIGHT,
                        Width = BeatmapCard.WIDTH - BeatmapCardNormal.HEIGHT - 28,
                        RelativeSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 8, Right = 4, Top = 5, Bottom = 2 },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Children = new Drawable[]
                                {
                                    title = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 18, weight: FontWeight.SemiBold) },
                                    artist = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold), Colour = colour.Light2 },
                                    new DifficultySummary(content)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 16,
                                        TooltipText = BmsDownloadStrings.Expand,
                                        Action = ToggleCharts,
                                        Child = selection = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.Default.With(size: 11),
                                            Colour = colour.Light1,
                                        },
                                    },
                                    status = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 11), Colour = colour.Light2 },
                                },
                            },
                            progressBar = new BeatmapCardDownloadProgressBar
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 3,
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                State = { BindTarget = progressState },
                                Progress = { BindTarget = progress },
                                Alpha = 0,
                            },
                        },
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        AutoSizeAxes = Axes.Y,
                        Width = 28,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            buttonSlot(action = new BmsDownloadCardButton(FontAwesome.Solid.Download)),
                            buttonSlot(expand = new BmsDownloadCardButton(FontAwesome.Solid.ChevronDown)
                            {
                                TooltipText = BmsDownloadStrings.Expand,
                                Action = ToggleCharts,
                            }),
                            buttonSlot(preview = new BmsDownloadCardButton(FontAwesome.Solid.ExternalLinkAlt)
                            {
                                TooltipText = BmsDownloadStrings.ExternalPreview,
                                Action = () => game.OpenUrlExternally(selected.PreviewUrl!.AbsoluteUri),
                            }),
                        },
                    },
                },
            };
            content.ExpandedContent = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 8, Vertical = 10 },
                Child = chartList = new FillFlowContainer<BmsDownloadChartRow>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 3),
                },
            };
        }

        private static Drawable buttonSlot(BmsDownloadCardButton button) => new Container
        {
            Width = 28,
            Height = BeatmapCardNormal.HEIGHT / 3,
            Child = button,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            content.Expanded.BindValueChanged(state => expand.ButtonIcon = state.NewValue ? FontAwesome.Solid.ChevronUp : FontAwesome.Solid.ChevronDown);
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
                    if (IsDisposed)
                        texture?.Dispose();
                    else
                    {
                        cover.Texture = texture;
                        placeholder.Alpha = texture == null ? 1 : 0;
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void ToggleCharts() => expanded.Toggle();

        public void SelectChart(string md5)
        {
            selected = Package.Charts.Single(c => c.Md5 == md5);
            Refresh();
        }

        public void Merge(BmsDownloadPackage incoming)
        {
            Package = Package with { Charts = Package.Charts.Concat(incoming.Charts).DistinctBy(c => c.Md5).ToArray() };
            if (IsLoaded)
            {
                rebuildCharts();
                Refresh();
            }
        }

        private void rebuildCharts()
        {
            chartList.Clear();
            foreach (var chart in Package.Charts)
                chartList.Add(new BmsDownloadChartRow(chart) { Action = () => SelectChart(chart.Md5) });
        }

        public void Refresh()
        {
            if (!IsLoaded)
                return;

            var task = downloads.GetTask(Package.Key);
            var local = findLocal(selected.Md5);
            title.Text = selected.Title;
            artist.Text = selected.Artist;
            selection.Text = GetChartDetails(selected);
            preview.Alpha = selected.PreviewUrl == null ? 0 : 1;
            foreach (var row in chartList)
                row.SetSelected(row.Chart.Md5 == selected.Md5);

            progressBar.Alpha = 0;
            if (local.HasValue)
            {
                status.Text = BmsDownloadStrings.Available;
                action.ButtonIcon = FontAwesome.Solid.Play;
                action.TooltipText = BmsDownloadStrings.Open;
                action.Action = () => open(local.Value);
                action.Enabled.Value = true;
                return;
            }

            if (task != null && task.Progress.State is BmsDownloadState.Queued or BmsDownloadState.Downloading or BmsDownloadState.Importing)
            {
                var current = task.Progress;
                status.Text = current.State switch
                {
                    BmsDownloadState.Queued => BmsDownloadStrings.Queued,
                    BmsDownloadState.Importing => BmsDownloadStrings.Importing,
                    _ => current.TotalBytes > 0
                        ? BmsDownloadStrings.DownloadProgress((int)Math.Clamp(100.0 * current.Bytes / current.TotalBytes.Value, 0, 100))
                        : BmsDownloadStrings.Downloading,
                };
                progressState.Value = current.State == BmsDownloadState.Importing ? DownloadState.Importing : DownloadState.Downloading;
                progress.Value = current.State == BmsDownloadState.Importing ? 1
                    : current.TotalBytes > 0 ? Math.Clamp((double)current.Bytes / current.TotalBytes.Value, 0, 1) : 0;
                progressBar.Alpha = 1;
                action.ButtonIcon = FontAwesome.Solid.Times;
                action.TooltipText = BmsDownloadStrings.Cancel;
                action.Action = task.Cancel;
                action.Enabled.Value = true;
                return;
            }

            bool unsupported = selected.KeyCount.HasValue && selected.KeyCount.Value is not (5 or 7 or 9 or 10 or 14)
                               || string.Equals(Path.GetExtension(selected.FileName), ".bmson", StringComparison.OrdinalIgnoreCase);
            status.Text = unsupported ? BmsDownloadStrings.UnsupportedChart : !Package.CanDownload ? BmsDownloadStrings.NoPackage : task?.Progress.State switch
            {
                BmsDownloadState.Failed => BmsDownloadStrings.Failed,
                BmsDownloadState.Cancelled => BmsDownloadStrings.Cancelled,
                _ => BmsDownloadStrings.ChartsCount(Package.Charts.Count),
            };
            action.ButtonIcon = task == null ? FontAwesome.Solid.Download : FontAwesome.Solid.RedoAlt;
            action.TooltipText = task == null ? BmsDownloadStrings.Download : BmsDownloadStrings.Retry;
            action.Action = StartDownload;
            action.Enabled.Value = Package.CanDownload && !unsupported;
        }

        internal static LocalisableString GetChartDetails(BmsDownloadChart chart)
        {
            var parts = new List<object>();
            if (chart.DifficultyName.Length > 0 && chart.DifficultyName != chart.Title)
                parts.Add(chart.DifficultyName);
            if (chart.KeyCount.HasValue)
                parts.Add($"{chart.KeyCount}K");
            if (!string.IsNullOrEmpty(chart.Level))
                parts.Add(BmsDownloadStrings.AuthorLevel(chart.Level));
            return LocalisableString.Interpolate(FormattableStringFactory.Create(string.Join(" · ", parts.Select((_, i) => $"{{{i}}}")), parts.ToArray()));
        }

        public void StartDownload() => downloads.Download(Package, selected.Md5);

        protected override void Dispose(bool isDisposing)
        {
            coverCancellation.Cancel();
            coverCancellation.Dispose();
            base.Dispose(isDisposing);
        }

        private partial class DifficultySummary : OsuClickableContainer
        {
            private readonly BeatmapCardContent content;

            public DifficultySummary(BeatmapCardContent content) => this.content = content;

            protected override bool OnHover(HoverEvent e)
            {
                content.ExpandAfterDelay();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!content.Expanded.Value)
                    content.CancelExpand();
                base.OnHoverLost(e);
            }
        }
    }

    public partial class BmsDownloadCardButton : BeatmapCardIconButton
    {
        private IconUsage buttonIcon;

        public IconUsage ButtonIcon
        {
            get => buttonIcon;
            set
            {
                buttonIcon = value;
                if (IsLoaded)
                    Icon.Icon = value;
            }
        }

        public BmsDownloadCardButton(IconUsage icon) => buttonIcon = icon;

        [BackgroundDependencyLoader]
        private void load() => Icon.Icon = buttonIcon;
    }

    public partial class BmsDownloadChartRow : OsuAnimatedButton
    {
        public readonly BmsDownloadChart Chart;
        private readonly Box selection;

        public BmsDownloadChartRow(BmsDownloadChart chart)
        {
            Chart = chart;
            RelativeSizeAxes = Axes.X;
            Height = 26;
            TooltipText = LocalisableString.Interpolate($"{chart.Title} {BmsDownloadCard.GetChartDetails(chart)}");
            AddRange(new Drawable[]
            {
                selection = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                new SpriteIcon { Icon = OsuIcon.Beatmap, Size = new Vector2(16), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 4 },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = 26, Right = 6 },
                    Child = new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = TooltipText,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    },
                },
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour) => selection.Colour = colour.Highlight1.Opacity(0.2f);

        public void SetSelected(bool selected) => selection.Alpha = selected ? 1 : 0;
    }
}
