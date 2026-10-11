// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Overlays.BeatmapSet
{
    internal sealed record OmsChartSummary(string Md5, string Ruleset, string? Title, string? Artist, string? Difficulty);

    /// <summary>The native information layout, with only actual public or matching local metadata.</summary>
    internal partial class OmsChartInfo : BeatmapSetLayoutSection
    {
        private readonly Box background;

        public OmsChartInfo(OmsChartSummary chart, bool hasPublicChart, IWorkingBeatmap? local, Action openBoard, Action download)
        {
            IBeatmapInfo? localInfo = local?.BeatmapInfo;
            string? localTitle = nonEmpty(localInfo?.Metadata.TitleUnicode) ?? nonEmpty(localInfo?.Metadata.Title);
            string? localArtist = nonEmpty(localInfo?.Metadata.ArtistUnicode) ?? nonEmpty(localInfo?.Metadata.Artist);
            string title = TitleFor(chart, localInfo);
            string? artist = chart.Artist ?? localArtist;
            string? difficulty = chart.Difficulty ?? nonEmpty(localInfo?.DifficultyName);

            var identity = column();
            identity.Add(text(chart.Ruleset == "bms" ? "BMS" : "osu!mania", 16));
            identity.Add(text(title, 30, FontWeight.SemiBold));
            if (chart.Title != null || localTitle != null)
                identity.Add(text(chart.Title != null ? "曲名来源：公开谱面目录" : "曲名来源：本地索引", 12));
            if (artist != null)
            {
                identity.Add(text(artist, 22));
                identity.Add(text(chart.Artist != null ? "艺术家来源：公开谱面目录" : "艺术家来源：本地索引", 12));
            }
            if (difficulty != null)
                identity.Add(text("难度标注：" + difficulty + (chart.Difficulty != null ? "（公开目录）" : "（本地索引）"), 16));
            identity.Add(text("原谱 MD5", 13));
            identity.Add(text(chart.Md5, 15));

            var actions = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(10),
                Margin = new MarginPadding { Top = 12 },
            };
            if (hasPublicChart)
                actions.Add(new RoundedButton { Text = "查看成绩榜", Width = 160, Height = 38, Action = openBoard });
            if (chart.Ruleset == "bms" || title != chart.Md5)
                actions.Add(new RoundedButton { Text = chart.Ruleset == "bms" ? "查找 BMS 原包" : "按歌名查找 mania 原包", Width = 215, Height = 38, Action = download });
            identity.Add(actions);
            if (!hasPublicChart)
                identity.Add(text("这张谱面尚未收录公开成绩或历史榜。已有本地谱面仍可游玩。", 15));
            if (chart.Ruleset == "mania")
                identity.Add(text("镜像未提供原谱 MD5 反查。歌名搜索只用于寻找候选，不能确认是同一张谱面。", 14));

            var stats = column();
            if (localInfo != null)
            {
                stats.Add(text("本地谱面数据", 18, FontWeight.SemiBold));
                stats.Add(text("来自本地索引中同一 MD5 的原谱。", 12));
                string? keymode = localInfo.Ruleset.CreateInstance().GetOmsWebsiteKeymode(localInfo);
                if (keymode != null) stats.Add(text("键型：" + keymode.Replace("bms_", "BMS ", StringComparison.Ordinal)
                    .Replace("pms_", "PMS ", StringComparison.Ordinal).Replace("mania_", "mania ", StringComparison.Ordinal), 16));
                if (double.IsFinite(localInfo.BPM) && localInfo.BPM > 0)
                    stats.Add(text("BPM：" + localInfo.BPM.ToString("0.##", CultureInfo.CurrentCulture), 16));
                if (double.IsFinite(localInfo.Length) && localInfo.Length > 0)
                {
                    TimeSpan duration = TimeSpan.FromMilliseconds(localInfo.Length);
                    stats.Add(text("时长：" + ((int)duration.TotalMinutes).ToString(CultureInfo.CurrentCulture)
                                   + ":" + duration.Seconds.ToString("00", CultureInfo.CurrentCulture), 16));
                }
                if (localInfo.TotalObjectCount >= 0)
                    stats.Add(text("物件数：" + localInfo.TotalObjectCount.ToString("N0", CultureInfo.CurrentCulture), 16));
                if (chart.Ruleset == "bms")
                {
                    string? playLevel = nonEmpty(BmsPersistedMetadataResolver.GetChartMetadata(localInfo.Metadata as BeatmapMetadata)?.PlayLevel);
                    if (playLevel != null)
                    {
                        stats.Add(text("作者 PLAYLEVEL：" + playLevel, 16));
                        stats.Add(text("作者难度标注，不是计算星级。", 12));
                    }
                }
                else if (double.IsFinite(localInfo.StarRating) && localInfo.StarRating >= 0)
                    stats.Add(text("本地计算星级：★" + localInfo.StarRating.ToString("0.##", CultureInfo.CurrentCulture), 16));
            }

            var panel = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, localInfo == null ? 0 : BeatmapSetOverlay.RIGHT_WIDTH) },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Padding = new MarginPadding(25),
                Content = new[] { new Drawable[] { identity, stats } },
            };
            identity.Padding = new MarginPadding { Right = localInfo == null ? 0 : 30 };

            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both, Depth = 1 },
                panel,
            };
            if (local != null && nonEmpty(localInfo!.Metadata.BackgroundFile) != null)
            {
                Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = 0.5f,
                    Masking = true,
                    Alpha = 0.16f,
                    Child = new BeatmapBackgroundSprite(local)
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                });
            }
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour) => background.Colour = colour.Background5;

        private static FillFlowContainer column() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 8),
        };

        private static OsuTextFlowContainer text(string value, int size, FontWeight weight = FontWeight.Regular)
            => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size, weight: weight))
            { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = value };

        private static string? nonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        internal static string TitleFor(OmsChartSummary chart, IBeatmapInfo? local)
            => chart.Title ?? nonEmpty(local?.Metadata.TitleUnicode) ?? nonEmpty(local?.Metadata.Title) ?? chart.Md5;
    }
}
