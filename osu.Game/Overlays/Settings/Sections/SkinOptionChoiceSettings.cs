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
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Sections
{
    internal partial class SkinOptionChoiceSettings : FillFlowContainer, IFilterable
    {
        public IEnumerable<LocalisableString> FilterTerms => new LocalisableString[] { definition.Label }
            .Concat(definition.Choices.Select(choice => (LocalisableString)choice.Label));
        public bool MatchingFilter { set => this.FadeTo(value ? 1 : 0); }
        public bool FilteringActive { get; set; }
        private const int page_size = 12;
        private readonly GameplaySkinOptionDefinition definition;
        private readonly GameplaySkinOptionPreviews previews;
        private readonly ArtworkChoiceControl choiceControl;
        private readonly ArtworkGallery gallery;
        private readonly FormTextBox search;
        private readonly GridContainer tiles;
        private readonly OsuTextFlowContainer pageLabel;
        private readonly RoundedButton previous;
        private readonly RoundedButton next;
        private int page;

        public ClickableContainer SelectionButton => choiceControl;
        public SpriteIcon SelectionArrow => choiceControl.Arrow;
        public Bindable<string> Current { get; }
        public string OptionId => definition.Id;
        public bool IsExpanded { get; private set; }

        public SkinOptionChoiceSettings(GameplaySkinOptionDefinition definition, GameplaySkinOptionPreviews previews, Bindable<string> selection, string hint)
        {
            this.definition = definition;
            this.previews = previews;
            Current = selection;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);

            Add(new SettingsItemV2(choiceControl = new ArtworkChoiceControl(definition, previews, selection, hint,
                () => { IsExpanded = !IsExpanded; updateGallery(); })));

            Add(gallery = new ArtworkGallery
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Alpha = 0,
                Children = new Drawable[]
                {
                    search = new FormTextBox { Caption = "搜索样式", RelativeSizeAxes = Axes.X },
                    tiles = new GridContainer { RelativeSizeAxes = Axes.X },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 32,
                        ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 72), new Dimension(), new Dimension(GridSizeMode.Absolute, 72) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                previous = new RoundedButton { RelativeSizeAxes = Axes.Both, Size = Vector2.One, Text = "上一页", Action = () => { page--; updateGallery(); } },
                                pageLabel = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, TextAnchor = Anchor.TopCentre },
                                next = new RoundedButton { RelativeSizeAxes = Axes.Both, Size = Vector2.One, Text = "下一页", Action = () => { page++; updateGallery(); } },
                            },
                        },
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            search.Current.BindValueChanged(_ => { page = 0; updateGallery(); });
        }

        private void updateGallery()
        {
            choiceControl.Expanded = IsExpanded;
            gallery.CanBeShown.Value = IsExpanded;
            gallery.Alpha = IsExpanded ? 1 : 0;

            // GridContainer retains replaced content for reuse; gallery pages own their cards instead.
            if (tiles.Content != null)
            {
                foreach (Drawable drawable in tiles.Content.SelectMany(row => row))
                {
                    if (drawable.Parent is Container parent)
                        parent.Remove(drawable, true);
                    else
                        drawable.Dispose();
                }
            }

            if (!IsExpanded)
            {
                tiles.Content = Array.Empty<Drawable[]>();
                return;
            }

            string term = search.Current.Value?.Trim() ?? string.Empty;
            GameplaySkinOptionChoice[] matches = definition.Choices.Where(choice => choice.Label.Contains(term, StringComparison.OrdinalIgnoreCase)
                || choice.Value.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
            int pages = Math.Max(1, (matches.Length + page_size - 1) / page_size);
            page = Math.Clamp(page, 0, pages - 1);
            GameplaySkinOptionChoice[] visible = matches.Skip(page * page_size).Take(page_size).ToArray();
            int rows = (visible.Length + 2) / 3;
            tiles.Height = rows * 108;
            tiles.Content = Enumerable.Range(0, rows).Select(row => Enumerable.Range(0, 3).Select(column =>
            {
                int index = row * 3 + column;
                return index < visible.Length ? tile(visible[index]) : new Container();
            }).ToArray()).ToArray();
            pageLabel.Text = matches.Length == 0 ? "没有匹配样式" : $"{page + 1} / {pages} · {matches.Length} 款";
            previous.Enabled.Value = page > 0;
            next.Enabled.Value = page + 1 < pages;
        }

        private Drawable tile(GameplaySkinOptionChoice choice)
        {
            var image = new Container { RelativeSizeAxes = Axes.X, Height = 72 };
            if (previews.TryGet(definition.Id, choice.Value, out GameplaySkinOptionPreview preview))
                image.Child = thumbnail(preview);
            return new ChoiceCard(choice.Value)
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(3),
                Action = () => { IsExpanded = false; Current.Value = choice.Value; updateGallery(); },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 5,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(32, 33, 44, 255) },
                        image,
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Horizontal = 4 },
                            Y = 75,
                            Text = choice.Label,
                            TextAnchor = Anchor.TopCentre,
                        },
                    },
                },
            };
        }

        private static Drawable thumbnail(GameplaySkinOptionPreview preview) => new DelayedLoadUnloadWrapper(() => new SkinOptionPreviewImage(preview))
        {
            RelativeSizeAxes = Axes.Both,
        };

        private partial class ArtworkGallery : FillFlowContainer, IConditionalFilterable
        {
            public readonly BindableBool CanBeShown = new BindableBool();
            IBindable<bool> IConditionalFilterable.CanBeShown => CanBeShown;
            public IEnumerable<LocalisableString> FilterTerms => Array.Empty<LocalisableString>();
            public bool MatchingFilter { set => this.FadeTo(value ? 1 : 0); }
            public bool FilteringActive { get; set; }

            public ArtworkGallery() => CanBeShown.BindValueChanged(_ => Invalidate(Invalidation.Presence));

            public override bool IsPresent => base.IsPresent && CanBeShown.Value;
        }

        internal partial class ChoiceCard : ClickableContainer
        {
            public string Value { get; }
            public ChoiceCard(string value) => Value = value;
        }

        private partial class ArtworkChoiceControl : ClickableContainer, IFormControl
        {
            private readonly GameplaySkinOptionDefinition definition;
            private readonly GameplaySkinOptionPreviews previews;
            private readonly Bindable<string> current;
            private readonly FormControlBackground background;
            private readonly FormFieldCaption caption;
            private readonly TruncatingSpriteText label;
            private readonly Container selectedImage;
            private bool expanded;

            public SpriteIcon Arrow { get; }
            public event Action? ValueChanged;
            public bool IsDefault => current.IsDefault;
            public bool IsDisabled => current.Disabled;
            public float MainDrawHeight => DrawHeight;
            public IEnumerable<LocalisableString> FilterTerms => new LocalisableString[] { definition.Label }
                .Concat(definition.Choices.Select(choice => (LocalisableString)choice.Label));

            public bool Expanded
            {
                set
                {
                    expanded = value;
                    if (IsLoaded)
                        updateState();
                }
            }

            [Resolved] private OverlayColourProvider colourProvider { get; set; } = null!;

            public ArtworkChoiceControl(GameplaySkinOptionDefinition definition, GameplaySkinOptionPreviews previews,
                                        Bindable<string> selection, string hint, Action toggle)
            {
                this.definition = definition;
                this.previews = previews;
                current = selection.GetBoundCopy();
                RelativeSizeAxes = Axes.X;
                Height = 76;
                Masking = true;
                CornerRadius = 5;
                Children = new Drawable[]
                {
                    background = new FormControlBackground(),
                    selectedImage = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(9, 0),
                        Size = new Vector2(66, 56),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 85, Right = 32 },
                        Child = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Children = new Drawable[]
                            {
                                caption = new FormFieldCaption { Caption = definition.Label, TooltipText = hint },
                                label = new TruncatingSpriteText { RelativeSizeAxes = Axes.X },
                            },
                        },
                    },
                    Arrow = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Icon = FontAwesome.Solid.ChevronDown,
                        Size = new Vector2(16),
                        Margin = new MarginPadding { Right = 10 },
                    },
                };
                Action = () =>
                {
                    if (!current.Disabled)
                    {
                        background.Flash();
                        toggle();
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(_ =>
                {
                    label.Text = definition.Choices.Single(choice => choice.Value == current.Value).Label;
                    selectedImage.Clear();
                    if (previews.TryGet(definition.Id, current.Value, out GameplaySkinOptionPreview preview))
                        selectedImage.Add(thumbnail(preview));
                    ValueChanged?.Invoke();
                }, true);
                current.BindDisabledChanged(_ => updateState(), true);
            }

            public void SetDefault() => current.SetDefault();

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                updateState();
            }

            private void updateState()
            {
                caption.Colour = current.Disabled ? colourProvider.Background1 : colourProvider.Content2;
                label.Colour = Arrow.Colour = current.Disabled ? colourProvider.Background1 : colourProvider.Content1;
                Arrow.Scale = new Vector2(1, expanded ? -1 : 1);
                background.VisualStyle = current.Disabled ? VisualStyle.Disabled
                    : expanded ? VisualStyle.Focused : IsHovered ? VisualStyle.Hovered : VisualStyle.Normal;
            }

            protected override void Dispose(bool isDisposing)
            {
                current.UnbindAll();
                base.Dispose(isDisposing);
            }
        }
    }
}
