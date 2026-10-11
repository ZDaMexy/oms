// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.BeatmapSet;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>A compact native chart card containing only actual source-chart metadata.</summary>
    internal partial class OmsChartCard : OsuClickableContainer
    {
        private readonly OmsChartSummary chart;
        private Box background = null!;

        public OmsChartCard(OmsChartSummary chart, Action open)
        {
            this.chart = chart;
            Width = BeatmapCard.WIDTH;
            Height = BeatmapCardNormal.HEIGHT;
            Action = open;
            TooltipText = string.Join("\n", new[] { chart.Title, chart.Artist, chart.Difficulty, "原谱 MD5：" + chart.Md5 }.Where(value => value != null));
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            var content = new BeatmapCardContent(BeatmapCardNormal.HEIGHT);
            Child = content;
            content.MainContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background3 },
                    new Container
                    {
                        Size = new Vector2(BeatmapCardNormal.HEIGHT),
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Background4 },
                            new SpriteIcon
                            {
                                Icon = OsuIcon.Beatmap,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(28),
                                Colour = colour.Light1,
                            },
                        },
                    },
                    new Container
                    {
                        X = BeatmapCardNormal.HEIGHT,
                        Width = BeatmapCard.WIDTH - BeatmapCardNormal.HEIGHT - 28,
                        RelativeSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 8, Right = 4, Top = 5, Bottom = 2 },
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Children = new Drawable[]
                            {
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                    Text = chart.Title ?? chart.Md5,
                                },
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                    Colour = colour.Light2,
                                    Text = chart.Artist ?? string.Empty,
                                },
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 11),
                                    Colour = colour.Light1,
                                    Text = chart.Difficulty == null ? string.Empty : "难度标注：" + chart.Difficulty,
                                },
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 11),
                                    Colour = colour.Light2,
                                    Text = (chart.Ruleset == "bms" ? "BMS" : "osu!mania") + " · OMS",
                                },
                            },
                        },
                    },
                    new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.ChevronRight,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 8 },
                        Size = new Vector2(14),
                        Colour = colour.Light1,
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.75f, 120);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(1, 120);
            base.OnHoverLost(e);
        }
    }
}
