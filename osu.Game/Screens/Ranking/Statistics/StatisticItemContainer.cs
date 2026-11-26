// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Wraps a <see cref="StatisticItem"/> to add a header and suitable layout for use in <see cref="ResultsScreen"/>.
    /// </summary>
    internal partial class StatisticItemContainer : CompositeDrawable
    {
        /// <summary>
        /// Creates a new <see cref="StatisticItemContainer"/>.
        /// </summary>
        /// <param name="item">The <see cref="StatisticItem"/> to display.</param>
        public StatisticItemContainer(StatisticItem item)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Padding = new MarginPadding { Vertical = 5 };

            if (LocalisableString.IsNullOrEmpty(item.Name))
            {
                // Unnamed statistic items are expected to provide their own panel shell.
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Child = item.CreateContent()
                };

                return;
            }

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 20,
                CornerExponent = 2.5f,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = OsuColour.Gray(0.18f),
                    Radius = 2,
                    Hollow = true,
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0.25f),
                            OsuColour.Gray(0.18f)
                        ),
                        Alpha = 0.95f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Padding = new MarginPadding { Horizontal = 15, Vertical = 10 },
                        Children = new Drawable[]
                        {
                            new StatisticItemHeader { Text = item.Name },
                            item.CreateContent(),
                        }
                    },
                }
            };
        }
    }
}
