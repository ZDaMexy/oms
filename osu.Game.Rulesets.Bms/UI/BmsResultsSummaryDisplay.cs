// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public class BmsResultsSummaryData
    {
        public BmsGaugeType GaugeType { get; }

        public string GaugeDisplayName { get; }

        public BmsJudgeMode JudgeMode { get; }

        public BmsLongNoteMode LongNoteMode { get; }

        public long ExScore { get; }

        public long MaxExScore { get; }

        public int EmptyPoorCount { get; }

        public double Accuracy { get; }

        public BmsDjLevel DjLevel { get; }

        public BmsClearLampData? ClearLamp { get; }

        public string FinalGaugeDisplay => ClearLamp == null ? "N/A" : $"{ClearLamp.FinalGauge:P2}";

        public BmsResultsSummaryData(BmsGaugeType gaugeType, string gaugeDisplayName, BmsJudgeMode judgeMode, BmsLongNoteMode longNoteMode, long exScore, long maxExScore, int emptyPoorCount, double accuracy, BmsDjLevel djLevel, BmsClearLampData? clearLamp)
        {
            GaugeType = gaugeType;
            GaugeDisplayName = gaugeDisplayName;
            JudgeMode = judgeMode;
            LongNoteMode = longNoteMode;
            ExScore = exScore;
            MaxExScore = maxExScore;
            EmptyPoorCount = emptyPoorCount;
            Accuracy = accuracy;
            DjLevel = djLevel;
            ClearLamp = clearLamp;
        }
    }

    public class BmsClearLampData
    {
        public BmsClearLamp Lamp { get; }

        public string DisplayName { get; }

        public double FinalGauge { get; }

        public BmsClearLampData(BmsClearLamp lamp, string displayName, double finalGauge)
        {
            Lamp = lamp;
            DisplayName = displayName;
            FinalGauge = finalGauge;
        }
    }

    public interface IBmsResultsSummaryDisplay
    {
        void SetSummary(BmsResultsSummaryData? summary);
    }

    public interface IBmsResultsSummaryPanelDisplay
    {
        void SetSummary(BmsResultsSummaryData? summary);
    }

    public interface IBmsClearLampDisplay
    {
        void SetClearLamp(BmsClearLampData? clearLamp);
    }

    public partial class DefaultBmsResultsSummaryPanelDisplay : DefaultResultsPanelDisplay<BmsResultsSummaryData>, IBmsResultsSummaryPanelDisplay
    {
        private SkinnableBmsResultsSummaryDisplay summaryDisplay = null!;

        public DefaultBmsResultsSummaryPanelDisplay()
            : base("BMS STATISTICS", "Results summary unavailable.")
        {
        }

        protected override Color4 TitleColour => BmsDefaultResultsPalette.PanelTitle;

        protected override Color4 StatusColour => BmsDefaultResultsPalette.PanelStatus;

        protected override Color4 PanelBackgroundColour => BmsDefaultResultsPalette.PanelBackground;

        protected override float FilledBackgroundAccentOpacity => 0.2f;

        protected override float FilledBorderAccentOpacity => 0.36f;

        protected override void LoadContent(FillFlowContainer content)
        {
            content.Add(summaryDisplay = new SkinnableBmsResultsSummaryDisplay
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            });
        }

        public void SetSummary(BmsResultsSummaryData? summary)
            => SetPanelState(summary);

        protected override void UpdateContent(BmsResultsSummaryData? state)
            => summaryDisplay.SetSummary(state);

        protected override bool HasContent(BmsResultsSummaryData? state)
            => state != null;

        protected override LocalisableString GetStatusText(BmsResultsSummaryData? state)
            => "Results summary unavailable.";

        protected override Color4 GetAccentColour(BmsResultsSummaryData? state)
            => state?.ClearLamp == null
                ? BmsDefaultResultsPalette.PanelStatus
                : BmsDefaultResultsPalette.GetClearLampAccent(state.ClearLamp.Lamp);
    }

    public partial class DefaultBmsResultsSummaryDisplay : CompositeDrawable, IBmsResultsSummaryDisplay
    {
        private const float summary_section_spacing = 8;
        private const float statistic_row_spacing = 6;
        private const float statistic_tile_height = 54;

        private FillFlowContainer content = null!;
        private BmsResultsSummaryData? summary;

        public DefaultBmsResultsSummaryDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, summary_section_spacing),
            };

            updateContent();
        }

        public void SetSummary(BmsResultsSummaryData? summary)
        {
            this.summary = summary;
            updateContent();
        }

        private void updateContent()
        {
            if (content == null)
                return;

            content.Clear();

            if (summary == null)
                return;

            content.Add(new SkinnableBmsClearLampDisplay(summary.ClearLamp)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            });

            content.Add(new ResultsStatisticsGrid(new[]
            {
                new StatisticMetric("GAUGE TYPE", summary.GaugeDisplayName),
                new StatisticMetric("JUDGE MODE", summary.JudgeMode.GetDisplayName()),
                new StatisticMetric("LONG NOTE MODE", summary.LongNoteMode.GetDisplayName()),
                new StatisticMetric("EX-SCORE", $"{summary.ExScore}"),
                new StatisticMetric("MAX EX-SCORE", $"{summary.MaxExScore}"),
                new StatisticMetric(BmsHitResultDisplayNames.GetDisplayName(HitResult.ComboBreak).ToString(), $"{summary.EmptyPoorCount}"),
                new StatisticMetric("EX %", $"{summary.Accuracy:P2}"),
                new StatisticMetric("DJ LEVEL", summary.DjLevel.ToString()),
                new StatisticMetric("FINAL GAUGE", summary.FinalGaugeDisplay),
            }));
        }

        private readonly record struct StatisticMetric(string Label, string Value);

        private partial class ResultsStatisticsGrid : CompositeDrawable
        {
            private readonly StatisticMetric[] metrics;

            public ResultsStatisticsGrid(IEnumerable<StatisticMetric> metrics)
            {
                this.metrics = metrics.ToArray();

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
                    Spacing = new Vector2(0, statistic_row_spacing),
                    Children = createRows().ToArray(),
                };
            }

            private IEnumerable<Drawable> createRows()
            {
                for (int i = 0; i < metrics.Length; i += 2)
                {
                    yield return new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, statistic_tile_height),
                        },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Relative, 1),
                            new Dimension(GridSizeMode.Absolute, 10),
                            new Dimension(GridSizeMode.Relative, 1),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new StatisticTile(metrics[i]),
                                new Container(),
                                i + 1 < metrics.Length ? new StatisticTile(metrics[i + 1]) : new Container(),
                            }
                        }
                    };
                }
            }
        }

        private partial class StatisticTile : CompositeDrawable
        {
            public StatisticTile(StatisticMetric metric)
            {
                RelativeSizeAxes = Axes.Both;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 9,
                    BorderThickness = 1,
                    BorderColour = BmsDefaultResultsPalette.StatisticBorder,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(BmsDefaultResultsPalette.StatisticBackground, BmsDefaultResultsPalette.StatisticBackgroundAccent),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Padding = new MarginPadding
                            {
                                Top = 9,
                                Right = 12,
                                Bottom = 9,
                                Left = 12,
                            },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = metric.Label,
                                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                    Colour = BmsDefaultResultsPalette.StatisticLabel,
                                },
                                new OsuSpriteText
                                {
                                    Text = metric.Value,
                                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                                    Colour = BmsDefaultResultsPalette.StatisticValue,
                                }
                            }
                        }
                    }
                };
            }
        }
    }

    public partial class DefaultBmsClearLampDisplay : CompositeDrawable, IBmsClearLampDisplay
    {
        private Container panel = null!;
        private Box background = null!;
        private Box accentStrip = null!;
        private OsuSpriteText lampValue = null!;
        private OsuSpriteText gaugeValue = null!;
        private OsuSpriteText label = null!;
        private BmsClearLampData? clearLamp;

        public DefaultBmsClearLampDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = panel = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 1,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    accentStrip = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 8,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Padding = new MarginPadding
                        {
                            Top = 10,
                            Right = 14,
                            Bottom = 10,
                            Left = 18,
                        },
                        Children = new Drawable[]
                        {
                            label = new OsuSpriteText
                            {
                                Text = "CLEAR LAMP",
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                            },
                            lampValue = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            gaugeValue = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            },
                        }
                    }
                }
            };

            applyClearLamp();
        }

        public void SetClearLamp(BmsClearLampData? clearLamp)
        {
            this.clearLamp = clearLamp;
            applyClearLamp();
        }

        private void applyClearLamp()
        {
            if (panel == null)
                return;

            Color4 accentColour = clearLamp == null ? BmsDefaultResultsPalette.PanelStatus : BmsDefaultResultsPalette.GetClearLampAccent(clearLamp.Lamp);

            background.Colour = ColourInfo.GradientVertical(BmsDefaultResultsPalette.PanelBackground, accentColour.Opacity(0.2f));
            accentStrip.Colour = accentColour;
            panel.BorderColour = accentColour.Opacity(0.42f);

            label.Colour = accentColour.Opacity(0.9f);
            lampValue.Colour = BmsDefaultResultsPalette.PanelTitle;
            gaugeValue.Colour = BmsDefaultResultsPalette.PanelStatus;

            lampValue.Text = clearLamp?.DisplayName ?? "UNAVAILABLE";
            gaugeValue.Text = clearLamp == null ? "Final gauge: N/A" : $"Final gauge: {clearLamp.FinalGauge:P2}";
        }
    }

    internal partial class SkinnableBmsResultsSummaryPanelDisplay : SkinnableDrawable
    {
        private readonly BmsResultsSummaryData? summary;

        public SkinnableBmsResultsSummaryPanelDisplay(BmsResultsSummaryData? summary)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummaryPanel), _ => new DefaultBmsResultsSummaryPanelDisplay())
        {
            this.summary = summary;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            applySummary();
        }

        private void applySummary()
        {
            if (Drawable is IBmsResultsSummaryPanelDisplay display)
                display.SetSummary(summary);
        }
    }

    internal partial class SkinnableBmsResultsSummaryDisplay : SkinnableDrawable
    {
        private BmsResultsSummaryData? summary;

        public SkinnableBmsResultsSummaryDisplay()
            : base(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummary), _ => new DefaultBmsResultsSummaryDisplay())
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CentreComponent = false;
        }

        public void SetSummary(BmsResultsSummaryData? summary)
        {
            this.summary = summary;
            applySummary();
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            applySummary();
        }

        private void applySummary()
        {
            if (Drawable is IBmsResultsSummaryDisplay display)
                display.SetSummary(summary);
        }
    }

    internal partial class SkinnableBmsClearLampDisplay : SkinnableDrawable
    {
        private readonly BmsClearLampData? clearLamp;

        public SkinnableBmsClearLampDisplay(BmsClearLampData? clearLamp)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.ClearLamp), _ => new DefaultBmsClearLampDisplay())
        {
            this.clearLamp = clearLamp;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            applyClearLamp();
        }

        private void applyClearLamp()
        {
            if (Drawable is IBmsClearLampDisplay display)
                display.SetClearLamp(clearLamp);
        }
    }
}
