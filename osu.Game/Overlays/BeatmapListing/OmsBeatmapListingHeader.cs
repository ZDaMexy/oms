// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>Native search for the actual OMS recorded-chart directory.</summary>
    public partial class OmsBeatmapListingHeader : BeatmapListingHeader
    {
        private ChartSearchTextBox search = null!;
        private Box background = null!;

        public Action<string>? SearchRequested { get; set; }

        protected override OverlayTitle CreateTitle() => new OmsListingTitle();

        protected override Drawable CreateContent() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 15),
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 25 },
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 45) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    search = new ChartSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        LengthLimit = 200,
                                        PlaceholderText = "搜索曲名、艺术家或原谱 MD5…",
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Search,
                                        TooltipText = "搜索已收录谱面",
                                        Action = () => SearchRequested?.Invoke(search.Text),
                                    },
                                },
                            },
                        },
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = "已收录成绩谱面 · OMS 与历史榜目录；原包查找与下载使用客户端现有下载页。",
                        },
                    },
                },
            },
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour)
        {
            background.Colour = colour.Dark6;
            search.OnCommit += (_, _) => SearchRequested?.Invoke(search.Text);
        }

        public void SetQuery(string query) => search.Text = query;

        public void FocusSearch() => search.TakeFocus();

        private partial class ChartSearchTextBox : BasicSearchTextBox
        {
            protected override bool AllowCommit => true;
        }

        private partial class OmsListingTitle : OverlayTitle
        {
            public OmsListingTitle()
            {
                Title = "谱面列表";
                Description = "OMS 已收录成绩谱面";
                Icon = OsuIcon.Beatmap;
            }
        }
    }
}
