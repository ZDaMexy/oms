// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.Rankings.Tables
{
    /// <summary>The native ranking table layout with the service's actual ranks and public counts.</summary>
    internal partial class OmsPlayerRankingsTable : TableContainer
    {
        private const float row_height = 35;
        private readonly IReadOnlyList<OmsPlayerRank> players;
        private readonly string metric;
        private readonly Action<OmsPlayerRank> openPlayer;

        public OmsPlayerRankingsTable(IReadOnlyList<OmsPlayerRank> players, string metric, Action<OmsPlayerRank> openPlayer)
        {
            this.players = players;
            this.metric = metric;
            this.openPlayer = openPlayer;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            RowSize = new Dimension(GridSizeMode.Absolute, row_height);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var backgrounds = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Depth = 1,
                Margin = new MarginPadding { Top = row_height },
                Spacing = new Vector2(0, 3),
            };
            for (int index = 0; index < players.Count; index++)
                backgrounds.Add(new TableRowBackground { Height = row_height - 3 });
            AddInternal(backgrounds);
            Columns = new[]
            {
                new TableColumn("名次", Anchor.Centre, new Dimension(GridSizeMode.Absolute, 70)),
                new TableColumn("玩家", Anchor.CentreLeft, new Dimension()),
                new TableColumn(metric, Anchor.CentreRight, new Dimension(GridSizeMode.AutoSize)),
                new TableColumn("公开谱面", Anchor.CentreRight, new Dimension(GridSizeMode.AutoSize)),
                new TableColumn("最佳条件条目", Anchor.CentreRight, new Dimension(GridSizeMode.AutoSize)),
            };
            Content = players.Select(player => new Drawable[]
            {
                cell("#" + player.Rank.ToString(CultureInfo.InvariantCulture), true),
                playerLink(player),
                cell(player.Value, true),
                cell(player.Charts.ToString("N0", CultureInfo.CurrentCulture)),
                cell(player.Bests.ToString("N0", CultureInfo.CurrentCulture)),
            }).ToArray().ToRectangular();
        }

        protected override Drawable CreateHeader(int index, TableColumn column) => new OsuSpriteText
        {
            Text = column.Header,
            Font = OsuFont.GetFont(size: 12),
            Margin = new MarginPadding { Horizontal = 10, Vertical = 5 },
        };

        private Drawable playerLink(OmsPlayerRank player)
        {
            var link = new LinkFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Margin = new MarginPadding { Horizontal = 10, Bottom = 3 },
            };
            link.AddLink(player.User.Name, () => openPlayer(player), "在客户端查看 OMS 玩家资料");
            return link;
        }

        private static Drawable cell(string value, bool highlighted = false) => new OsuSpriteText
        {
            Text = value,
            Font = OsuFont.GetFont(size: 12, weight: highlighted ? FontWeight.SemiBold : FontWeight.Regular),
            Margin = new MarginPadding { Horizontal = 10, Bottom = 3 },
        };
    }
}
