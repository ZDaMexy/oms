// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.SongSelect
{
    public partial class BmsNoteDistributionGraph : CompositeDrawable
    {
        private readonly IBindable<WorkingBeatmap> beatmap;
        private readonly BmsRuleset ruleset = new BmsRuleset();
        private readonly BmsNoteDistributionAnalyzer distributionAnalyzer = new BmsNoteDistributionAnalyzer();
        private readonly Dictionary<Guid, BmsNoteDistributionData> cachedData = new Dictionary<Guid, BmsNoteDistributionData>();

        private CancellationTokenSource? updateCancellationSource;
        private SkinnableNoteDistributionPanelDisplay panel = null!;

        public BmsNoteDistributionGraph(IBindable<WorkingBeatmap> beatmap)
        {
            this.beatmap = beatmap.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = panel = new SkinnableNoteDistributionPanelDisplay
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            panel.SetState(new BmsNoteDistributionPanelState(null, Array.Empty<string>(), "Loading note distribution..."));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(_ => updateBeatmap(), true);
        }

        private void updateBeatmap()
        {
            updateCancellationSource?.Cancel();
            updateCancellationSource?.Dispose();
            updateCancellationSource = null;

            var workingBeatmap = beatmap.Value;

            if (workingBeatmap?.BeatmapInfo?.Ruleset?.ShortName != BmsRuleset.SHORT_NAME)
            {
                panel.SetState(new BmsNoteDistributionPanelState(null, Array.Empty<string>(), "No BMS chart selected."));
                return;
            }

            var summaryLines = BuildSummaryLines(workingBeatmap.BeatmapInfo.Metadata);

            Guid cacheKey = workingBeatmap.BeatmapInfo.ID;

            if (cachedData.TryGetValue(cacheKey, out var cached))
            {
                applyData(cached, workingBeatmap.BeatmapInfo);
                return;
            }

            panel.SetState(new BmsNoteDistributionPanelState(null, summaryLines, "Loading note distribution..."));

            var cancellationSource = updateCancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;

            Task.Run(() => computeData(workingBeatmap, token), token)
                .ContinueWith(task => Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        panel.SetState(new BmsNoteDistributionPanelState(null, summaryLines, "Note distribution unavailable."));
                        return;
                    }

                    var result = task.GetResultSafely();

                    if (result == null)
                    {
                        panel.SetState(new BmsNoteDistributionPanelState(null, summaryLines, "Note distribution unavailable."));
                        return;
                    }

                    cachedData[cacheKey] = result;
                    applyData(result, workingBeatmap.BeatmapInfo);
                }), token, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        private BmsNoteDistributionData? computeData(WorkingBeatmap workingBeatmap, CancellationToken cancellationToken)
        {
            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>(), cancellationToken);
            var analysis = distributionAnalyzer.Analyze(playableBeatmap, 1000, 1000);
            var chartMetadata = playableBeatmap is BmsBeatmap bmsBeatmap
                ? BmsChartMetadata.FromBeatmapInfo(bmsBeatmap.BmsInfo)
                : null;

            return new BmsNoteDistributionData(
                analysis.Windows.Select(window => new BmsNoteDistributionBucket(window.StartTime, window.WeightedNoteCount, window.NormalCount, window.ScratchCount, window.LnCount)).ToArray(),
                analysis.TotalNoteCount,
                analysis.ScratchNoteCount,
                analysis.LnNoteCount,
                analysis.PeakDensityNps,
                analysis.PeakDensityMs,
                chartMetadata);
        }

        private void applyData(BmsNoteDistributionData data, IBeatmapInfo beatmapInfo)
            => panel.SetState(new BmsNoteDistributionPanelState(data, BuildSummaryLines(beatmapInfo.Metadata, data.ChartMetadata), string.Empty));

        internal static IReadOnlyList<string> BuildSummaryLines(IBeatmapMetadataInfo metadata, BmsChartMetadata? fallbackChartMetadata = null)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return BuildSummaryLines(metadata.GetChartMetadata() ?? fallbackChartMetadata, metadata.Author.Username, metadata.GetDifficultyTableEntries());
        }

        internal static IReadOnlyList<string> BuildSummaryLines(BmsChartMetadata? chartMetadata, string? creator, IReadOnlyList<BmsDifficultyTableEntry> entries)
        {
            var lines = new List<string>();

            lines.AddRange(BuildChartSummaryLines(chartMetadata, creator));
            lines.AddRange(BuildDifficultyTableSummaryLines(entries));

            return lines;
        }

        internal static IReadOnlyList<string> BuildChartSummaryLines(BmsChartMetadata? chartMetadata, string? creator = null)
        {
            if (chartMetadata == null)
            {
                if (string.IsNullOrWhiteSpace(creator))
                    return Array.Empty<string>();

                return new[] { $"Chart by: {creator}" };
            }

            var lines = new List<string>();

            string? chartCreator = !string.IsNullOrWhiteSpace(creator)
                ? creator
                : chartMetadata.TryGetChartCreator();

            if (!string.IsNullOrWhiteSpace(chartCreator))
                lines.Add($"Chart by: {chartCreator}");
            else if (!string.IsNullOrWhiteSpace(chartMetadata.SubArtist))
                lines.Add($"Credit: {chartMetadata.SubArtist}");

            string internalLevel = chartMetadata.GetInternalLevelDisplay();

            if (!string.IsNullOrWhiteSpace(internalLevel))
                lines.Add($"Internal level: {internalLevel}");

            if (!string.IsNullOrWhiteSpace(chartMetadata.Subtitle))
                lines.Add($"Subtitle: {chartMetadata.Subtitle}");

            return lines;
        }

        internal static IReadOnlyList<string> BuildDifficultyTableSummaryLines(IReadOnlyList<BmsDifficultyTableEntry> entries)
        {
            if (entries.Count == 0)
                return new[] { "Table: Unrated" };

            return entries.GroupBy(entry => (entry.TableSortOrder, entry.TableName))
                         .OrderBy(group => group.Key.TableSortOrder)
                         .ThenBy(group => group.Key.TableName, StringComparer.Ordinal)
                         .Select(group =>
                         {
                             string levels = string.Join(", ", group.OrderBy(entry => entry.Level)
                                                              .ThenBy(entry => entry.LevelLabel, StringComparer.Ordinal)
                                                              .Select(entry => entry.LevelLabel)
                                                              .Distinct(StringComparer.Ordinal));
                             return $"Table: {group.Key.TableName} ({levels})";
                         })
                         .ToArray();
        }

        protected override void Dispose(bool isDisposing)
        {
            updateCancellationSource?.Cancel();
            updateCancellationSource?.Dispose();
            updateCancellationSource = null;
            base.Dispose(isDisposing);
        }

        private partial class SkinnableNoteDistributionPanelDisplay : SkinnableDrawable
        {
            private BmsNoteDistributionPanelState? state;

            public SkinnableNoteDistributionPanelDisplay()
                : base(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistributionPanel), _ => new DefaultBmsNoteDistributionPanelDisplay())
            {
            }

            public void SetState(BmsNoteDistributionPanelState? state)
            {
                this.state = state;
                applyState();
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                applyState();
            }

            private void applyState()
            {
                if (Drawable is IBmsNoteDistributionPanelDisplay display)
                    display.SetState(state);
            }
        }

        internal partial class SkinnableNoteDistributionDisplay : SkinnableDrawable
        {
            private BmsNoteDistributionData? data;

            public SkinnableNoteDistributionDisplay()
                : base(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistribution), _ => new DefaultBmsNoteDistributionDisplay())
            {
            }

            public void SetData(BmsNoteDistributionData? data)
            {
                this.data = data;
                applyData();
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                applyData();
            }

            private void applyData()
            {
                if (Drawable is IBmsNoteDistributionDisplay display)
                    display.SetData(data);
            }
        }
    }

    public class BmsNoteDistributionPanelState
    {
        public BmsNoteDistributionData? Distribution { get; }

        public IReadOnlyList<string> SummaryLines { get; }

        public string StatusText { get; }

        public bool HasDistribution => Distribution != null;

        public bool HasSummary => SummaryLines.Count > 0;

        public BmsNoteDistributionPanelState(BmsNoteDistributionData? distribution, IReadOnlyList<string> summaryLines, string statusText)
        {
            Distribution = distribution;
            SummaryLines = summaryLines;
            StatusText = statusText;
        }
    }

    public interface IBmsNoteDistributionPanelDisplay
    {
        void SetState(BmsNoteDistributionPanelState? state);
    }

    public partial class DefaultBmsNoteDistributionPanelDisplay : DefaultResultsPanelDisplay<BmsNoteDistributionPanelState>, IBmsNoteDistributionPanelDisplay
    {
        private FillFlowContainer difficultyTableContainer = null!;
        private OsuSpriteText distributionStatusText = null!;
        private BmsNoteDistributionGraph.SkinnableNoteDistributionDisplay graph = null!;
        private FillFlowContainer summaryContainer = null!;
        private OsuSpriteText totalNotesText = null!;
        private OsuSpriteText scratchText = null!;
        private OsuSpriteText lnText = null!;
        private OsuSpriteText peakText = null!;

        public DefaultBmsNoteDistributionPanelDisplay()
            : base("NOTE DISTRIBUTION", "Loading note distribution...")
        {
        }

        protected override Color4 TitleColour => BmsDefaultResultsPalette.PanelTitle;

        protected override Color4 StatusColour => BmsDefaultResultsPalette.PanelStatus;

        protected override Color4 PanelBackgroundColour => BmsDefaultResultsPalette.PanelBackground;

        protected override void LoadContent(FillFlowContainer content)
        {
            content.AddRange(new Drawable[]
            {
                difficultyTableContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                },
                distributionStatusText = createSummaryText(),
                graph = new BmsNoteDistributionGraph.SkinnableNoteDistributionDisplay
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                summaryContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        totalNotesText = createSummaryText(),
                        scratchText = createSummaryText(),
                        lnText = createSummaryText(),
                        peakText = createSummaryText(),
                    }
                },
            });
        }

        public void SetState(BmsNoteDistributionPanelState? state)
            => SetPanelState(state);

        protected override void UpdateContent(BmsNoteDistributionPanelState? state)
        {
            difficultyTableContainer.Clear();

            foreach (string line in state?.SummaryLines ?? Array.Empty<string>())
                difficultyTableContainer.Add(createSummaryText(line));

            var distribution = state?.Distribution;

            graph.SetData(distribution);

            if (distribution == null)
            {
                graph.Hide();
                summaryContainer.Hide();
                distributionStatusText.Text = state?.StatusText ?? "Loading note distribution...";
                distributionStatusText.Show();
                return;
            }

            graph.Show();
            summaryContainer.Show();
            distributionStatusText.Hide();

            totalNotesText.Text = $"Total notes: {distribution.TotalNoteCount}";
            scratchText.Text = $"Scratch: {distribution.ScratchNoteCount} ({formatPercentage(distribution.ScratchNoteCount, distribution.TotalNoteCount)})";
            lnText.Text = $"LN: {distribution.LnNoteCount} ({formatPercentage(distribution.LnNoteCount, distribution.TotalNoteCount)})";
            peakText.Text = $"Peak density: {distribution.PeakDensityNps:0.0} notes/sec @ {TimeSpan.FromMilliseconds(distribution.PeakDensityMs):mm\\:ss}";
        }

        protected override bool HasContent(BmsNoteDistributionPanelState? state)
            => state?.Distribution != null || state?.HasSummary == true;

        protected override LocalisableString GetStatusText(BmsNoteDistributionPanelState? state)
            => state?.StatusText ?? "Loading note distribution...";

        protected override Color4 GetAccentColour(BmsNoteDistributionPanelState? state)
            => getPanelAccentColour(state?.Distribution);

        private static string formatPercentage(int value, int total)
            => total == 0 ? "0.0%" : $"{value * 100d / total:0.0}%";

        private static Color4 getPanelAccentColour(BmsNoteDistributionData? distribution)
        {
            if (distribution == null)
                return BmsDefaultResultsPalette.PanelStatus;

            if (distribution.ScratchNoteCount >= distribution.LnNoteCount && distribution.ScratchNoteCount > 0)
                return DefaultBmsNoteDistributionDisplay.DistributionScratchAccent;

            if (distribution.LnNoteCount > 0)
                return DefaultBmsNoteDistributionDisplay.DistributionLongNoteAccent;

            return DefaultBmsNoteDistributionDisplay.DistributionNormalAccent;
        }

        private static OsuSpriteText createSummaryText(string? text = null) => new OsuSpriteText
        {
            Text = text ?? string.Empty,
            Colour = BmsDefaultResultsPalette.PanelStatus,
            Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
        };
    }

    public interface IBmsNoteDistributionDisplay
    {
        void SetData(BmsNoteDistributionData? data);
    }

    public partial class DefaultBmsNoteDistributionDisplay : CompositeDrawable, IBmsNoteDistributionDisplay
    {
        internal static readonly Color4 DistributionNormalColour = BmsDefaultResultsPalette.PanelTitle;
        internal static readonly Color4 DistributionNormalAccent = BmsDefaultHudPalette.ComboActiveAccent;
        internal static readonly Color4 DistributionScratchColour = BmsDefaultHudPalette.GaugeNormalBar;
        internal static readonly Color4 DistributionScratchAccent = BmsDefaultHudPalette.GaugeNormalAccent;
        internal static readonly Color4 DistributionLongNoteColour = BmsDefaultHudPalette.ComboMilestoneAccent;
        internal static readonly Color4 DistributionLongNoteAccent = BmsDefaultHudPalette.ComboMilestoneAccent;

        private DistributionPlot plot = null!;

        public DefaultBmsNoteDistributionDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(12, 0),
                        Children = new Drawable[]
                        {
                            createLegend("NORMAL", DistributionNormalColour),
                            createLegend("SCRATCH", DistributionScratchColour),
                            createLegend("LN", DistributionLongNoteColour),
                        }
                    },
                    plot = new DistributionPlot(),
                }
            };
        }

        public void SetData(BmsNoteDistributionData? data) => plot.SetData(data?.Buckets ?? Array.Empty<BmsNoteDistributionBucket>());

        private static Drawable createLegend(string text, Color4 colour)
            => new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Box
                    {
                        Size = new Vector2(10),
                        Colour = colour,
                    },
                    new OsuSpriteText
                    {
                        Text = text,
                        Colour = BmsDefaultResultsPalette.PanelStatus,
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    }
                }
            };

        private partial class DistributionPlot : CompositeDrawable
        {
            private readonly Container barContainer;
            private IReadOnlyList<BmsNoteDistributionBucket> buckets = Array.Empty<BmsNoteDistributionBucket>();

            public DistributionPlot()
            {
                RelativeSizeAxes = Axes.X;
                Height = 84;
                Masking = true;
                CornerRadius = 6;
                BorderThickness = 1;
                BorderColour = BmsDefaultResultsPalette.StatisticBorder;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(8),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(BmsDefaultResultsPalette.StatisticBackground, BmsDefaultResultsPalette.StatisticBackgroundAccent),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BmsDefaultHudPalette.TrackShade.Opacity(0.18f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Colour = BmsDefaultHudPalette.ThresholdMarker.Opacity(0.18f),
                        },
                        barContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        }
                    }
                };
            }

            public void SetData(IReadOnlyList<BmsNoteDistributionBucket> buckets)
            {
                this.buckets = buckets;
                Scheduler.AddOnce(updateBars);
            }

            private void updateBars()
            {
                barContainer.Clear();

                if (buckets.Count == 0)
                    return;

                double maxWeightedNoteCount = Math.Max(1, buckets.Max(bucket => bucket.WeightedNoteCount));

                var grid = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = buckets.Select(_ => new Dimension()).ToArray(),
                    Content = new[]
                    {
                        buckets.Select(bucket => (Drawable)new DistributionBar(bucket, maxWeightedNoteCount)).ToArray()
                    }
                };

                barContainer.Add(grid);
            }

            private partial class DistributionBar : CompositeDrawable
            {
                private readonly BmsNoteDistributionBucket bucket;
                private readonly double maxWeightedNoteCount;

                public DistributionBar(BmsNoteDistributionBucket bucket, double maxWeightedNoteCount)
                {
                    this.bucket = bucket;
                    this.maxWeightedNoteCount = maxWeightedNoteCount;

                    RelativeSizeAxes = Axes.Both;
                    Margin = new MarginPadding { Horizontal = 0.5f };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    float totalHeight = maxWeightedNoteCount <= 0 ? 0 : (float)(bucket.WeightedNoteCount / maxWeightedNoteCount);
                    int totalCount = bucket.NormalCount + bucket.ScratchCount + bucket.LnCount;

                    float normalHeight = totalCount == 0 ? 0 : totalHeight * bucket.NormalCount / totalCount;
                    float scratchHeight = totalCount == 0 ? 0 : totalHeight * bucket.ScratchCount / totalCount;
                    float lnHeight = totalCount == 0 ? 0 : totalHeight * bucket.LnCount / totalCount;

                    InternalChildren = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BmsDefaultResultsPalette.PanelTitle.Opacity(0.02f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = totalHeight,
                            Y = 1 - totalHeight,
                            Colour = BmsDefaultResultsPalette.PanelTitle.Opacity(0.06f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = lnHeight,
                            Y = 1 - lnHeight,
                            Colour = DistributionLongNoteColour,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = scratchHeight,
                            Y = 1 - lnHeight - scratchHeight,
                            Colour = DistributionScratchColour,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = normalHeight,
                            Y = 1 - totalHeight,
                            Colour = DistributionNormalColour,
                        }
                    };
                }
            }
        }
    }

    public class BmsNoteDistributionData
    {
        public IReadOnlyList<BmsNoteDistributionBucket> Buckets { get; }

        public int TotalNoteCount { get; }

        public int ScratchNoteCount { get; }

        public int LnNoteCount { get; }

        public double PeakDensityNps { get; }

        public double PeakDensityMs { get; }

        public BmsChartMetadata? ChartMetadata { get; }

        public BmsNoteDistributionData(IReadOnlyList<BmsNoteDistributionBucket> buckets, int totalNoteCount, int scratchNoteCount, int lnNoteCount, double peakDensityNps, double peakDensityMs, BmsChartMetadata? chartMetadata = null)
        {
            Buckets = buckets;
            TotalNoteCount = totalNoteCount;
            ScratchNoteCount = scratchNoteCount;
            LnNoteCount = lnNoteCount;
            PeakDensityNps = peakDensityNps;
            PeakDensityMs = peakDensityMs;
            ChartMetadata = chartMetadata;
        }
    }

    public readonly record struct BmsNoteDistributionBucket(double StartTime, double WeightedNoteCount, int NormalCount, int ScratchCount, int LnCount);
}
