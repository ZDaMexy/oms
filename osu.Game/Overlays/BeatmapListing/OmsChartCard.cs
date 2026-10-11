// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.BeatmapSet;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>A horizontal native chart card which never invents an online cover or ppy ID.</summary>
    internal partial class OmsChartCard : OsuClickableContainer
    {
        private readonly Box background;

        public OmsChartCard(OmsChartSummary chart, Action open)
        {
            Width = 420;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 6;
            Action = open;
            TooltipText = "查看原谱 " + chart.Md5;

            var metadata = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Padding = new MarginPadding { Horizontal = 10 },
            };
            metadata.Add(text(chart.Title ?? chart.Md5, 18, FontWeight.SemiBold));
            if (chart.Artist != null) metadata.Add(text(chart.Artist, 15));
            if (chart.Difficulty != null) metadata.Add(text("难度标注：" + chart.Difficulty, 13));
            metadata.Add(text((chart.Ruleset == "bms" ? "BMS" : "osu!mania") + " · 公开谱面目录", 12));

            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(12),
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 40), new Dimension(), new Dimension(GridSizeMode.Absolute, 20) },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 60,
                                Child = new SpriteIcon
                                {
                                    Icon = OsuIcon.Beatmap,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(30),
                                },
                            },
                            metadata,
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 60,
                                Child = new SpriteIcon
                                {
                                    Icon = FontAwesome.Solid.ChevronRight,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(14),
                                },
                            },
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour) => background.Colour = colour.Background4;

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

        private static OsuTextFlowContainer text(string value, int size, FontWeight weight = FontWeight.Regular)
            => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size, weight: weight))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = value };
    }
}
