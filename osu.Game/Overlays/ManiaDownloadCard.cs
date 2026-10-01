// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Sayobot;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class ManiaDownloadCard : BeatmapDownloadCard
    {
        private readonly ManiaDownloadManager downloads;
        private readonly Func<int, Guid?> findLocal;
        private readonly Action<Guid> open;
        private SayobotBeatmap selected;

        public SayobotBeatmapSet Set { get; private set; }
        public int SelectedBeatmapId => selected.Id;

        public ManiaDownloadCard(SayobotBeatmapSet set, ManiaDownloadManager downloads, BeatmapDownloadCoverStore covers, Func<int, Guid?> findLocal, Action<Guid> open)
            : base(covers, set.CoverUrl)
        {
            Set = set;
            selected = set.Beatmaps[0];
            this.downloads = downloads;
            this.findLocal = findLocal;
            this.open = open;
        }

        public void SelectBeatmap(int id)
        {
            selected = Set.Beatmaps.Single(beatmap => beatmap.Id == id);
            Refresh();
        }

        public void Merge(SayobotBeatmapSet incoming)
        {
            Set = Set with { Beatmaps = Set.Beatmaps.Concat(incoming.Beatmaps).DistinctBy(beatmap => beatmap.Id).ToArray() };
            if (IsLoaded)
            {
                RebuildCharts();
                Refresh();
            }
        }

        protected override void RebuildCharts()
        {
            ChartList.Clear();
            foreach (var beatmap in Set.Beatmaps)
                ChartList.Add(new ManiaDownloadDifficultyRow(beatmap) { Action = () => SelectBeatmap(beatmap.Id) });
        }

        public override void Refresh()
        {
            if (!IsLoaded)
                return;

            var task = downloads.GetTask(Set.Key);
            var local = findLocal(selected.Id);
            TitleText.Text = Set.Title;
            ArtistText.Text = $"{Set.Artist} · {Set.Creator}";
            DifficultyText.Text = GetBeatmapDetails(selected);
            ExternalButton.Alpha = 0;
            foreach (var row in ChartList.OfType<ManiaDownloadDifficultyRow>())
                row.SetSelected(row.Beatmap.Id == selected.Id);

            ProgressBar.Alpha = 0;
            ActionButton.Enabled.Value = true;
            if (local.HasValue)
            {
                StatusText.Text = BmsDownloadStrings.Available;
                ActionButton.ButtonIcon = FontAwesome.Solid.Play;
                ActionButton.TooltipText = BmsDownloadStrings.Open;
                ActionButton.Action = () => open(local.Value);
                return;
            }

            if (task != null && task.Progress.State is ManiaDownloadState.Queued or ManiaDownloadState.Downloading or ManiaDownloadState.Importing)
            {
                var current = task.Progress;
                StatusText.Text = current.State switch
                {
                    ManiaDownloadState.Queued => BmsDownloadStrings.Queued,
                    ManiaDownloadState.Importing => BmsDownloadStrings.Importing,
                    _ => current.TotalBytes > 0
                        ? BmsDownloadStrings.DownloadProgress((int)Math.Clamp(100.0 * current.Bytes / current.TotalBytes.Value, 0, 100))
                        : BmsDownloadStrings.Downloading,
                };
                ProgressState.Value = current.State == ManiaDownloadState.Importing ? DownloadState.Importing : DownloadState.Downloading;
                Progress.Value = current.State == ManiaDownloadState.Importing ? 1
                    : current.TotalBytes > 0 ? Math.Clamp((double)current.Bytes / current.TotalBytes.Value, 0, 1) : 0;
                ProgressBar.Alpha = 1;
                ActionButton.ButtonIcon = FontAwesome.Solid.Times;
                ActionButton.TooltipText = BmsDownloadStrings.Cancel;
                ActionButton.Action = task.Cancel;
                return;
            }

            StatusText.Text = task?.Progress.State switch
            {
                ManiaDownloadState.Failed => BmsDownloadStrings.Failed,
                ManiaDownloadState.Cancelled => BmsDownloadStrings.Cancelled,
                _ => LocalisableString.Interpolate($"{statusLabel(Set.Status)} · {BmsDownloadStrings.ChartsCount(Set.Beatmaps.Count)}"),
            };
            ActionButton.ButtonIcon = task == null ? FontAwesome.Solid.Download : FontAwesome.Solid.RedoAlt;
            ActionButton.TooltipText = task == null ? BmsDownloadStrings.Download : BmsDownloadStrings.Retry;
            ActionButton.Action = () => downloads.Download(Set, selected.Id);
        }

        internal static string GetBeatmapDetails(SayobotBeatmap beatmap) => $"{beatmap.DifficultyName} · {beatmap.KeyCount}K · ★{beatmap.StarRating.ToString("0.##", CultureInfo.InvariantCulture)}";

        private static LocalisableString statusLabel(int status) => status switch
        {
            -2 => BeatmapDownloadStrings.Graveyard,
            -1 or 0 => BeatmapDownloadStrings.Pending,
            1 => BeatmapDownloadStrings.Ranked,
            2 => BeatmapDownloadStrings.Approved,
            3 => BeatmapDownloadStrings.Qualified,
            4 => BeatmapDownloadStrings.Loved,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    public partial class ManiaDownloadDifficultyRow : OsuAnimatedButton
    {
        public readonly SayobotBeatmap Beatmap;
        private readonly Box selection;

        public ManiaDownloadDifficultyRow(SayobotBeatmap beatmap)
        {
            Beatmap = beatmap;
            RelativeSizeAxes = Axes.X;
            Height = 26;
            TooltipText = ManiaDownloadCard.GetBeatmapDetails(beatmap);
            AddRange(new Drawable[]
            {
                selection = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                new SpriteIcon { Icon = OsuIcon.Beatmap, Size = new Vector2(16), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 4 },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = 26, Right = 6 },
                    Child = new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = TooltipText,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    },
                },
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colour) => selection.Colour = colour.Highlight1.Opacity(0.2f);

        public void SetSelected(bool selected) => selection.Alpha = selected ? 1 : 0;
    }
}
