// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Shared shell for results-style statistic panels.
    /// </summary>
    public partial class DefaultResultsPanelContainer : CompositeDrawable
    {
        public readonly Container Panel;
        public readonly Box Background;
        public readonly Box AccentBar;
        public readonly FillFlowContainer Content;
        public readonly OsuSpriteText TitleText;
        public readonly OsuSpriteText StatusText;

        public DefaultResultsPanelContainer(LocalisableString title, LocalisableString defaultStatus)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = Panel = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 12,
                BorderThickness = 1,
                Children = new Drawable[]
                {
                    Background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    AccentBar = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 3,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding
                        {
                            Top = 18,
                            Right = 18,
                            Bottom = 18,
                            Left = 18,
                        },
                        Child = Content = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 10),
                            Children = new Drawable[]
                            {
                                TitleText = new OsuSpriteText
                                {
                                    Text = title,
                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                },
                                StatusText = new OsuSpriteText
                                {
                                    Text = defaultStatus,
                                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                },
                            }
                        }
                    }
                }
            };
        }
    }
}