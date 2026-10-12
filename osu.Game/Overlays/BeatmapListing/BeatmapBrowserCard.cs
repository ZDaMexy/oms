// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>The native normal and extra card layouts, populated with actual source metadata.</summary>
    public abstract partial class BeatmapBrowserCard : OsuClickableContainer
    {
        public const float EXTRA_HEIGHT = 112;

        private readonly Bindable<BeatmapCardSize> cardSize = new Bindable<BeatmapCardSize>();
        private readonly BindableBool expanded = new BindableBool();
        private BeatmapCardContent content = null!;
        private Container thumbnail = null!;
        private Container mainArea = null!;
        private Container textArea = null!;
        private Container buttons = null!;
        private Container summary = null!;
        private Sprite cover = null!;
        private Sprite backgroundCover = null!;
        private SpriteIcon placeholder = null!;
        private BeatmapDownloadCardButton expand = null!;
        private float lastWidth;

        protected readonly Bindable<DownloadState> ProgressState = new Bindable<DownloadState>();
        protected readonly BindableDouble Progress = new BindableDouble();
        protected FillFlowContainer ChartList = null!;
        protected TruncatingSpriteText TitleText = null!;
        protected TruncatingSpriteText ArtistText = null!;
        protected TruncatingSpriteText CreatorText = null!;
        protected TruncatingSpriteText SelectedText = null!;
        protected TruncatingSpriteText ExtraText = null!;
        protected TruncatingSpriteText DifficultyText = null!;
        protected TruncatingSpriteText StatusText = null!;
        protected BeatmapCardDownloadProgressBar ProgressBar = null!;
        protected BeatmapDownloadCardButton ActionButton = null!;
        protected BeatmapDownloadCardButton ExternalButton = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        protected BeatmapBrowserCard()
        {
            Width = BeatmapCard.WIDTH;
            Height = BeatmapCardNormal.HEIGHT;
            Action = ToggleCharts;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            Child = content = new BeatmapCardContent(Height);
            content.Expanded.BindTarget = expanded;
            content.MainContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    thumbnail = new Container
                    {
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background4 },
                            placeholder = new SpriteIcon
                            {
                                Anchor = Anchor.Centre, Origin = Anchor.Centre,
                                Size = new Vector2(28), Icon = OsuIcon.Beatmap, Colour = colour.Light1,
                            },
                            cover = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill },
                        },
                    },
                    mainArea = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background3 },
                            backgroundCover = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill, Alpha = 0.15f },
                            textArea = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Horizontal = 10, Vertical = 4 },
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Children = new Drawable[]
                                        {
                                            TitleText = text(18, FontWeight.SemiBold),
                                            ArtistText = text(14, FontWeight.SemiBold),
                                            CreatorText = text(11, FontWeight.SemiBold),
                                            SelectedText = text(11),
                                            ExtraText = text(11),
                                        },
                                    },
                                    summary = new DifficultySummary(content)
                                    {
                                        RelativeSizeAxes = Axes.X, Height = 16,
                                        Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                                        Action = ToggleCharts,
                                        TooltipText = BmsDownloadStrings.Expand,
                                        Child = DifficultyText = text(11, FontWeight.SemiBold),
                                    },
                                    StatusText = new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Font = OsuFont.GetFont(size: 10),
                                        Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                                    },
                                },
                            },
                            ProgressBar = new BeatmapCardDownloadProgressBar
                            {
                                RelativeSizeAxes = Axes.X, Height = 3,
                                Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                                State = { BindTarget = ProgressState }, Progress = { BindTarget = Progress }, Alpha = 0,
                            },
                        },
                    },
                    buttons = new Container
                    {
                        Anchor = Anchor.TopRight, Origin = Anchor.TopRight,
                        Width = 24, RelativeSizeAxes = Axes.Y, AlwaysPresent = true, Alpha = 0,
                        Children = new Drawable[]
                        {
                            buttonSlot(ActionButton = new BeatmapDownloadCardButton(FontAwesome.Solid.Download), 0),
                            buttonSlot(expand = new BeatmapDownloadCardButton(FontAwesome.Solid.ChevronDown)
                            {
                                TooltipText = BmsDownloadStrings.Expand, Action = ToggleCharts,
                            }, 1),
                            buttonSlot(ExternalButton = new BeatmapDownloadCardButton(FontAwesome.Solid.ExternalLinkAlt)
                            {
                                TooltipText = BmsDownloadStrings.ExternalPreview, Alpha = 0,
                            }, 2),
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
            ArtistText.Colour = colour.Light2;
            CreatorText.Colour = colour.Light2;
            SelectedText.Colour = colour.Light1;
            ExtraText.Colour = colour.Light2;
            DifficultyText.Colour = colour.Light1;
            StatusText.Colour = colour.Light2;
        }

        private static TruncatingSpriteText text(float size, FontWeight weight = FontWeight.Regular) => new TruncatingSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Font = OsuFont.GetFont(size: size, weight: weight),
        };

        private static Drawable buttonSlot(BeatmapDownloadCardButton button, int index) => new Container
        {
            RelativeSizeAxes = Axes.Y,
            RelativePositionAxes = Axes.Y,
            Width = 24,
            Height = 1f / 3,
            Y = index / 3f,
            Child = button,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (game != null) cardSize.BindTo(game.BeatmapBrowserCardSize);
            cardSize.BindValueChanged(_ => updateLayout(), true);
            expanded.BindValueChanged(state =>
            {
                expand.ButtonIcon = state.NewValue ? FontAwesome.Solid.ChevronUp : FontAwesome.Solid.ChevronDown;
                updateHover();
            });
        }

        protected override void Update()
        {
            base.Update();
            if (lastWidth != Width) updateLayout();
            bool extra = cardSize.Value == BeatmapCardSize.Extra;
            CreatorText.Alpha = CreatorText.Text == string.Empty ? 0 : 1;
            SelectedText.Alpha = SelectedText.Text == string.Empty || (!extra && CreatorText.Alpha > 0) ? 0 : 1;
            ExtraText.Alpha = extra && ExtraText.Text != string.Empty ? 1 : 0;
            summary.Margin = new MarginPadding { Bottom = StatusText.Text == string.Empty ? 0 : 12 };
        }

        private void updateLayout()
        {
            lastWidth = Width;
            Height = cardSize.Value == BeatmapCardSize.Extra ? EXTRA_HEIGHT : BeatmapCardNormal.HEIGHT;
            content.BodyHeight = Height;
            thumbnail.Size = new Vector2(Height);
            mainArea.X = Height - BeatmapCard.CORNER_RADIUS;
            mainArea.Width = Width - mainArea.X - (IsHovered || expanded.Value ? 24 : BeatmapCard.CORNER_RADIUS);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateHover();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateHover();
            base.OnHoverLost(e);
        }

        private void updateHover()
        {
            bool showDetails = IsHovered || expanded.Value;
            buttons.FadeTo(showDetails ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            backgroundCover.FadeTo(showDetails ? 0.08f : 0.15f, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            mainArea.ResizeWidthTo(Width - mainArea.X - (showDetails ? 24 : BeatmapCard.CORNER_RADIUS), BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
        }

        protected void SetCover(Texture? texture)
        {
            cover.Texture = backgroundCover.Texture = texture;
            placeholder.Alpha = texture == null ? 1 : 0;
        }

        public void ToggleCharts() => expanded.Toggle();

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
                if (!content.Expanded.Value) content.CancelExpand();
                base.OnHoverLost(e);
            }
        }
    }
}
