// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using osu.Game.Online.Bms;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class BmsDownloadCard : BeatmapDownloadCard
    {
        private readonly BmsDownloadManager downloads;
        private readonly Func<string, Guid?> findLocal;
        private readonly Action<Guid> open;
        private BmsDownloadChart selected;

        public BmsDownloadPackage Package { get; private set; }
        public string SelectedMd5 => selected.Md5;

        public BmsDownloadCard(BmsDownloadPackage package, BmsDownloadManager downloads, BeatmapDownloadCoverStore covers,
                               Func<string, Guid?> findLocal, Action<Guid> open)
            : base(covers, package.CoverUrl)
        {
            Package = package;
            selected = package.Charts[0];
            this.downloads = downloads;
            this.findLocal = findLocal;
            this.open = open;
        }

        public void SelectChart(string md5)
        {
            selected = Package.Charts.Single(c => c.Md5 == md5);
            Refresh();
        }

        public void Merge(BmsDownloadPackage incoming)
        {
            Package = Package with { Charts = Package.Charts.Concat(incoming.Charts).DistinctBy(c => c.Md5).ToArray() };
            if (IsLoaded)
            {
                RebuildCharts();
                Refresh();
            }
        }

        protected override void RebuildCharts()
        {
            ChartList.Clear();
            foreach (var chart in Package.Charts)
                ChartList.Add(new BmsDownloadChartRow(chart) { Action = () => SelectChart(chart.Md5) });
        }

        public override void Refresh()
        {
            if (!IsLoaded)
                return;

            var task = downloads.GetTask(Package.Key);
            var local = findLocal(selected.Md5);
            TitleText.Text = selected.Title;
            ArtistText.Text = selected.Artist;
            DifficultyText.Text = GetChartDetails(selected);
            ExternalButton.Alpha = selected.PreviewUrl == null ? 0 : 1;
            ExternalButton.Action = () => Game.OpenUrlExternally(selected.PreviewUrl!.AbsoluteUri);
            foreach (var row in ChartList.OfType<BmsDownloadChartRow>())
                row.SetSelected(row.Chart.Md5 == selected.Md5);

            ProgressBar.Alpha = 0;
            if (local.HasValue)
            {
                StatusText.Text = BmsDownloadStrings.Available;
                ActionButton.ButtonIcon = FontAwesome.Solid.Play;
                ActionButton.TooltipText = BmsDownloadStrings.Open;
                ActionButton.Action = () => open(local.Value);
                ActionButton.Enabled.Value = true;
                return;
            }

            if (task != null && task.Progress.State is BmsDownloadState.Queued or BmsDownloadState.Downloading or BmsDownloadState.Importing)
            {
                var current = task.Progress;
                StatusText.Text = current.State switch
                {
                    BmsDownloadState.Queued => BmsDownloadStrings.Queued,
                    BmsDownloadState.Importing => BmsDownloadStrings.Importing,
                    _ => current.TotalBytes > 0
                        ? BmsDownloadStrings.DownloadProgress((int)Math.Clamp(100.0 * current.Bytes / current.TotalBytes.Value, 0, 100))
                        : BmsDownloadStrings.Downloading,
                };
                ProgressState.Value = current.State == BmsDownloadState.Importing ? DownloadState.Importing : DownloadState.Downloading;
                Progress.Value = current.State == BmsDownloadState.Importing ? 1
                    : current.TotalBytes > 0 ? Math.Clamp((double)current.Bytes / current.TotalBytes.Value, 0, 1) : 0;
                ProgressBar.Alpha = 1;
                ActionButton.ButtonIcon = FontAwesome.Solid.Times;
                ActionButton.TooltipText = BmsDownloadStrings.Cancel;
                ActionButton.Action = task.Cancel;
                ActionButton.Enabled.Value = true;
                return;
            }

            bool unsupported = selected.KeyCount.HasValue && selected.KeyCount.Value is not (5 or 7 or 9 or 10 or 14)
                               || string.Equals(Path.GetExtension(selected.FileName), ".bmson", StringComparison.OrdinalIgnoreCase);
            StatusText.Text = unsupported ? BmsDownloadStrings.UnsupportedChart : !Package.CanDownload ? BmsDownloadStrings.NoPackage : task?.Progress.State switch
            {
                BmsDownloadState.Failed => BmsDownloadStrings.Failed,
                BmsDownloadState.Cancelled => BmsDownloadStrings.Cancelled,
                _ => BmsDownloadStrings.ChartsCount(Package.Charts.Count),
            };
            ActionButton.ButtonIcon = task == null ? FontAwesome.Solid.Download : FontAwesome.Solid.RedoAlt;
            ActionButton.TooltipText = task == null ? BmsDownloadStrings.Download : BmsDownloadStrings.Retry;
            ActionButton.Action = StartDownload;
            ActionButton.Enabled.Value = Package.CanDownload && !unsupported;
        }

        internal static LocalisableString GetChartDetails(BmsDownloadChart chart)
        {
            var parts = new List<object>();
            if (chart.DifficultyName.Length > 0 && chart.DifficultyName != chart.Title)
                parts.Add(chart.DifficultyName);
            if (chart.KeyCount.HasValue)
                parts.Add($"{chart.KeyCount}K");
            if (!string.IsNullOrEmpty(chart.Level))
                parts.Add(BmsDownloadStrings.AuthorLevel(chart.Level));
            return LocalisableString.Interpolate(FormattableStringFactory.Create(string.Join(" · ", parts.Select((_, i) => $"{{{i}}}")), parts.ToArray()));
        }

        public void StartDownload() => downloads.Download(Package, selected.Md5);

    }

    public partial class BmsDownloadChartRow : OsuAnimatedButton
    {
        public readonly BmsDownloadChart Chart;
        private readonly Box selection;

        public BmsDownloadChartRow(BmsDownloadChart chart)
        {
            Chart = chart;
            RelativeSizeAxes = Axes.X;
            Height = 26;
            TooltipText = LocalisableString.Interpolate($"{chart.Title} {BmsDownloadCard.GetChartDetails(chart)}");
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
