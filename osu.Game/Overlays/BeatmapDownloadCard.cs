// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
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
using osuTK;

namespace osu.Game.Overlays
{
    public abstract partial class BeatmapDownloadCard : CompositeDrawable
    {
        private readonly BeatmapDownloadCoverStore covers;
        private readonly Uri? coverUrl;
        private readonly BeatmapCardContent content = new BeatmapCardContent(BeatmapCardNormal.HEIGHT);
        private readonly BindableBool expanded = new BindableBool();
        private readonly CancellationTokenSource coverCancellation = new CancellationTokenSource();
        private BeatmapDownloadCardButton expand = null!;
        private Sprite cover = null!;
        private SpriteIcon placeholder = null!;

        protected readonly Bindable<DownloadState> ProgressState = new Bindable<DownloadState>();
        protected readonly BindableDouble Progress = new BindableDouble();
        protected FillFlowContainer ChartList = null!;
        protected TruncatingSpriteText TitleText = null!;
        protected TruncatingSpriteText DifficultyText = null!;
        protected TruncatingSpriteText ArtistText = null!;
        protected TruncatingSpriteText StatusText = null!;
        protected BeatmapCardDownloadProgressBar ProgressBar = null!;
        protected BeatmapDownloadCardButton ActionButton = null!;
        protected BeatmapDownloadCardButton ExternalButton = null!;

        [Resolved]
        protected OsuGame Game { get; private set; } = null!;

        protected BeatmapDownloadCard(BeatmapDownloadCoverStore covers, Uri? coverUrl)
        {
            this.covers = covers;
            this.coverUrl = coverUrl;
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
                                    TitleText = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 18, weight: FontWeight.SemiBold) },
                                    ArtistText = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold), Colour = colour.Light2 },
                                    new DifficultySummary(content)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 16,
                                        TooltipText = BmsDownloadStrings.Expand,
                                        Action = ToggleCharts,
                                        Child = DifficultyText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.Default.With(size: 11),
                                            Colour = colour.Light1,
                                        },
                                    },
                                    StatusText = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.Default.With(size: 11), Colour = colour.Light2 },
                                },
                            },
                            ProgressBar = new BeatmapCardDownloadProgressBar
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 3,
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                State = { BindTarget = ProgressState },
                                Progress = { BindTarget = Progress },
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
                            buttonSlot(ActionButton = new BeatmapDownloadCardButton(FontAwesome.Solid.Download)),
                            buttonSlot(expand = new BeatmapDownloadCardButton(FontAwesome.Solid.ChevronDown)
                            {
                                TooltipText = BmsDownloadStrings.Expand,
                                Action = ToggleCharts,
                            }),
                            buttonSlot(ExternalButton = new BeatmapDownloadCardButton(FontAwesome.Solid.ExternalLinkAlt)
                            {
                                TooltipText = BmsDownloadStrings.ExternalPreview,
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
                Child = ChartList = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 3),
                },
            };
        }

        private static Drawable buttonSlot(BeatmapDownloadCardButton button) => new Container
        {
            Width = 28,
            Height = BeatmapCardNormal.HEIGHT / 3,
            Child = button,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            content.Expanded.BindValueChanged(state => expand.ButtonIcon = state.NewValue ? FontAwesome.Solid.ChevronUp : FontAwesome.Solid.ChevronDown);
            RebuildCharts();
            Refresh();
            if (coverUrl != null)
                loadCover(coverUrl);
        }

        private async void loadCover(Uri uri)
        {
            try
            {
                var texture = await covers.GetAsync(uri.AbsoluteUri, coverCancellation.Token).ConfigureAwait(false);
                if (coverCancellation.IsCancellationRequested)
                    return;

                Schedule(() =>
                {
                    if (!IsDisposed)
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

        protected abstract void RebuildCharts();
        public abstract void Refresh();

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

    public partial class BeatmapDownloadCardButton : BeatmapCardIconButton
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

        public BeatmapDownloadCardButton(IconUsage icon) => buttonIcon = icon;

        [BackgroundDependencyLoader]
        private void load() => Icon.Icon = buttonIcon;
    }

}
