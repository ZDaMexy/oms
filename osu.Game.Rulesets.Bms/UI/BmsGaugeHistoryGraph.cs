// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsGaugeHistoryPanelDisplay
    {
        void SetHistory(BmsGaugeHistory? history);
    }

    public interface IBmsGaugeHistoryDisplay
    {
        void SetHistory(BmsGaugeHistory? history);
    }

    public partial class DefaultBmsGaugeHistoryPanelDisplay : DefaultResultsPanelDisplay<BmsGaugeHistory>, IBmsGaugeHistoryPanelDisplay
    {
        private SkinnableBmsGaugeHistoryDisplay historyDisplay = null!;

        public DefaultBmsGaugeHistoryPanelDisplay()
            : base("GAUGE HISTORY", "Gauge history unavailable.")
        {
        }

        protected override Color4 TitleColour => BmsDefaultResultsPalette.PanelTitle;

        protected override Color4 StatusColour => BmsDefaultResultsPalette.PanelStatus;

        protected override Color4 PanelBackgroundColour => BmsDefaultResultsPalette.PanelBackground;

        protected override void LoadContent(FillFlowContainer content)
        {
            content.Add(historyDisplay = new SkinnableBmsGaugeHistoryDisplay
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            });
        }

        public void SetHistory(BmsGaugeHistory? history)
            => SetPanelState(history);

        protected override void UpdateContent(BmsGaugeHistory? state)
            => historyDisplay.SetHistory(state);

        protected override bool HasContent(BmsGaugeHistory? state)
            => state != null;

        protected override LocalisableString GetStatusText(BmsGaugeHistory? state)
            => "Gauge history unavailable.";

        protected override Color4 GetAccentColour(BmsGaugeHistory? state)
            => getPanelAccentColour(state);

        private static Color4 getPanelAccentColour(BmsGaugeHistory? history)
        {
            var finalTimeline = history?.Timelines.LastOrDefault();

            return finalTimeline == null
                ? BmsDefaultResultsPalette.PanelStatus
                : BmsGaugeColours.Get(finalTimeline.GaugeType).Accent;
        }
    }

    public partial class DefaultBmsGaugeHistoryDisplay : CompositeDrawable, IBmsGaugeHistoryDisplay
    {
        private FillFlowContainer timelineContainer = null!;
        private BmsGaugeHistory? history;

        public DefaultBmsGaugeHistoryDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = timelineContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
            };

            updateTimelines();
        }

        public void SetHistory(BmsGaugeHistory? history)
        {
            this.history = history;
            updateTimelines();
        }

        private void updateTimelines()
        {
            if (timelineContainer == null)
                return;

            timelineContainer.Clear();

            if (history == null)
                return;

            timelineContainer.AddRange(history.Timelines.Select(timeline => (Drawable)new GaugeTimelineRow(history, timeline)));
        }

        private partial class GaugeTimelineRow : CompositeDrawable
        {
            private readonly BmsGaugeHistory history;
            private readonly BmsGaugeHistoryTimeline timeline;

            public GaugeTimelineRow(BmsGaugeHistory history, BmsGaugeHistoryTimeline timeline)
            {
                this.history = history;
                this.timeline = timeline;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                var (_, accentColour) = BmsGaugeColours.Get(timeline.GaugeType);

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 1,
                    BorderColour = accentColour.Opacity(0.28f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(BmsDefaultResultsPalette.StatisticBackground, BmsDefaultResultsPalette.StatisticBackgroundAccent),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 7,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Colour = accentColour,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Padding = new MarginPadding
                            {
                                Top = 14,
                                Right = 14,
                                Bottom = 14,
                                Left = 18,
                            },
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopLeft,
                                            Origin = Anchor.TopLeft,
                                            Text = timeline.GaugeType.GetDisplayName(),
                                            Colour = accentColour,
                                            Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE + 1, weight: FontWeight.Bold),
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            Text = $"{timeline.Samples.LastOrDefault().Value:P0}",
                                            Colour = BmsDefaultResultsPalette.PanelTitle,
                                            Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
                                        }
                                    }
                                },
                                new GaugeTimelinePlot(history, timeline)
                            }
                        }
                    }
                };
            }
        }

        private partial class GaugeTimelinePlot : CompositeDrawable
        {
            private readonly BmsGaugeHistory history;
            private readonly BmsGaugeHistoryTimeline timeline;
            private readonly SmoothPath gaugePath;
            private readonly Container plotArea;

            public GaugeTimelinePlot(BmsGaugeHistory history, BmsGaugeHistoryTimeline timeline)
            {
                this.history = history;
                this.timeline = timeline;

                var (barColour, accentColour) = BmsGaugeColours.Get(timeline.GaugeType);
                bool survivalGauge = BmsGaugeProcessor.UsesSurvivalClear(timeline.GaugeType);
                float clearBandHeight = 1f - (float)BmsGaugeProcessor.CLEAR_THRESHOLD;
                float floorMarkerPosition = 1f - (float)BmsGaugeProcessor.GetFloorGauge(timeline.GaugeType);

                RelativeSizeAxes = Axes.X;
                Height = 68;
                Masking = true;
                CornerRadius = 8;
                BorderThickness = 1;
                BorderColour = accentColour.Opacity(0.26f);

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(BmsDefaultResultsPalette.PanelBackgroundAccent, BmsDefaultResultsPalette.StatisticBackground),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Colour = BmsDefaultResultsPalette.PanelBorder.Opacity(0.6f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = 1,
                            Y = 0.5f,
                            Colour = BmsDefaultResultsPalette.PanelBorder.Opacity(0.35f),
                        },
                        new Box
                        {
                            RelativePositionAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            Y = floorMarkerPosition,
                            Height = 1,
                            Colour = accentColour.Opacity(0.28f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = 1,
                            Y = 1f - (float)BmsGaugeProcessor.CLEAR_THRESHOLD,
                            Alpha = survivalGauge ? 0 : 1,
                            Colour = BmsDefaultHudPalette.ThresholdMarker.Opacity(0.8f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            RelativePositionAxes = Axes.Y,
                            Height = clearBandHeight,
                            Alpha = survivalGauge ? 0 : 1,
                            Colour = accentColour.Opacity(0.08f),
                        },
                        plotArea = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Top = 8,
                                Right = 10,
                                Bottom = 10,
                                Left = 10,
                            },
                            Children = new Drawable[]
                            {
                                gaugePath = new SmoothPath
                                {
                                    PathRadius = 2.8f,
                                    Colour = barColour,
                                }
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Scheduler.AddOnce(updatePath, true);
            }

            protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
            {
                if (invalidation.HasFlag(Invalidation.DrawSize))
                    Scheduler.AddOnce(updatePath, false);

                return base.OnInvalidate(invalidation, source);
            }

            private void updatePath(bool animate = false)
            {
                if (plotArea.DrawWidth <= 0 || plotArea.DrawHeight <= 0 || timeline.Samples.Count == 0)
                    return;

                double duration = history.EndTime - history.StartTime;

                if (duration <= 0)
                    duration = 1;

                gaugePath.Vertices = timeline.Samples.Select(sample =>
                {
                    float x = (float)((sample.Time - history.StartTime) / duration) * plotArea.DrawWidth;
                    float y = (1f - (float)sample.Value) * plotArea.DrawHeight;
                    return new Vector2(x, y);
                }).ToList();
            }
        }
    }

    public partial class BmsGaugeHistoryGraph : DefaultBmsGaugeHistoryDisplay
    {
        public BmsGaugeHistoryGraph(BmsGaugeHistory history)
        {
            SetHistory(history);
        }
    }

    internal partial class SkinnableBmsGaugeHistoryPanelDisplay : SkinnableDrawable
    {
        private readonly BmsGaugeHistory? history;

        public SkinnableBmsGaugeHistoryPanelDisplay(BmsGaugeHistory? history)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistoryPanel), _ => new DefaultBmsGaugeHistoryPanelDisplay())
        {
            this.history = history;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            applyHistory();
        }

        private void applyHistory()
        {
            if (Drawable is IBmsGaugeHistoryPanelDisplay display)
                display.SetHistory(history);
        }
    }

    internal partial class SkinnableBmsGaugeHistoryDisplay : SkinnableDrawable
    {
        private BmsGaugeHistory? history;

        public SkinnableBmsGaugeHistoryDisplay()
            : base(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistory), _ => new DefaultBmsGaugeHistoryDisplay())
        {
        }

        public void SetHistory(BmsGaugeHistory? history)
        {
            this.history = history;
            applyHistory();
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            applyHistory();
        }

        private void applyHistory()
        {
            if (Drawable is IBmsGaugeHistoryDisplay display)
                display.SetHistory(history);
        }
    }
}
