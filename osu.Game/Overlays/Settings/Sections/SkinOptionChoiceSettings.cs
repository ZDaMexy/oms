// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
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
        private readonly Bindable<string> selection;
        private readonly Container selectedImage;
        private readonly FillFlowContainer gallery;
        private readonly FormTextBox search;
        private readonly GridContainer tiles;
        private readonly OsuTextFlowContainer pageLabel;
        private readonly RoundedButton previous;
        private readonly RoundedButton next;
        private int page;
        private bool expanded;

        public ClickableContainer BrowseButton { get; }

        public SkinOptionChoiceSettings(GameplaySkinOptionDefinition definition, GameplaySkinOptionPreviews previews, Bindable<string> selection, string hint)
        {
            this.definition = definition;
            this.previews = previews;
            this.selection = selection;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);

            var dropdown = new OptionDropdown(definition.Choices)
            {
                Caption = definition.Label,
                HintText = hint,
                AlwaysShowSearchBar = definition.IsContentCatalog,
                Current = selection,
                Items = definition.Choices.Select(choice => choice.Value),
            };
            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 82 },
                        Child = new SettingsItemV2(dropdown),
                    },
                    new Container
                    {
                        Width = 76,
                        Height = 66,
                        Margin = new MarginPadding { Left = SettingsPanel.CONTENT_PADDING.Left },
                        Child = BrowseButton = new ClickableContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Action = () => { expanded = !expanded; updateGallery(); },
                            Children = new Drawable[]
                            {
                                selectedImage = new Container { RelativeSizeAxes = Axes.Both },
                                new OsuTextFlowContainer
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = "浏览样式",
                                    TextAnchor = Anchor.BottomCentre,
                                },
                            },
                        },
                    },
                },
            });

            Add(gallery = new FillFlowContainer
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
            selection.BindValueChanged(_ => updateSelectedImage(), true);
            search.Current.BindValueChanged(_ => { page = 0; updateGallery(); });
        }

        private void updateSelectedImage()
        {
            selectedImage.Clear();
            if (previews.TryGet(definition.Id, selection.Value, out GameplaySkinOptionPreview preview))
                selectedImage.Add(thumbnail(preview));
        }

        private void updateGallery()
        {
            gallery.Alpha = expanded ? 1 : 0;
            if (!expanded)
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
            return new ClickableContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(3),
                Action = () => { expanded = false; selection.Value = choice.Value; updateGallery(); },
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

        private partial class OptionDropdown : FormDropdown<string>
        {
            private readonly IReadOnlyDictionary<string, string> labels;
            public OptionDropdown(IReadOnlyList<GameplaySkinOptionChoice> choices)
                => labels = choices.ToDictionary(choice => choice.Value, choice => choice.Label, StringComparer.Ordinal);
            protected override LocalisableString GenerateItemText(string item) => labels[item];
        }
    }
}
