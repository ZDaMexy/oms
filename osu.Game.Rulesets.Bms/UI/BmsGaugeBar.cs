// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsGaugeBar : HealthDisplay, ISerialisableDrawable
    {
        private const float bar_height = 20;
        private const float header_spacing = 6;

        private readonly OsuSpriteText gaugeLabel;
        private readonly OsuSpriteText gaugeValue;
        private readonly Container track;
        private readonly Box trackBackground;
        private readonly Container fill;
        private readonly Box fillBox;
        private readonly Box floorBand;
        private readonly Box floorMarker;
        private readonly Box clearMarker;
        private readonly Box highlight;
        private IBindable<BmsGaugeType>? currentGaugeType;

        private Color4 barColour = BmsDefaultHudPalette.SurfaceText;
        private Color4 accentColour = BmsDefaultHudPalette.SurfaceText;

        public bool UsesFixedAnchor { get; set; }

        protected override bool PlayInitialIncreaseAnimation => false;

        public BmsGaugeBar()
        {
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, header_spacing),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            gaugeLabel = new OsuSpriteText
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            gaugeValue = new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = OsuFont.Numeric.With(size: 14, fixedWidth: true),
                                Colour = BmsDefaultHudPalette.SurfaceText,
                            },
                        }
                    },
                    track = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = bar_height,
                        Masking = true,
                        CornerRadius = 10,
                        BorderThickness = 1,
                        BorderColour = BmsDefaultHudPalette.SurfaceBorder,
                        Children = new Drawable[]
                        {
                            trackBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            floorBand = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                            },
                            fill = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = 0,
                                Children = new Drawable[]
                                {
                                    fillBox = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 2,
                                        Colour = BmsDefaultHudPalette.SurfaceText.Opacity(0.12f),
                                    }
                                }
                            },
                            highlight = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 2,
                                Alpha = 0.16f,
                            },
                            floorMarker = new Box
                            {
                                RelativePositionAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Width = 2,
                                Height = 1,
                                Alpha = 0,
                            },
                            clearMarker = new Box
                            {
                                RelativePositionAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Width = 2,
                                Height = 1,
                                Alpha = 0,
                            },
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
            => updateGaugeStyling();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (HealthProcessor is BmsGaugeProcessor gaugeProcessor)
            {
                currentGaugeType = gaugeProcessor.GaugeTypeBindable.GetBoundCopy();
                currentGaugeType.BindValueChanged(_ => updateGaugeStyling(), true);
                return;
            }

            updateGaugeStyling();
        }

        protected override void Update()
        {
            base.Update();

            fill.Width = (float)Current.Value;
            gaugeValue.Text = $"{Current.Value:P0}";
        }

        protected override void Flash()
        {
            fill.ClearTransforms();
            fill.ScaleTo(new Vector2(1.01f, 1), 60, Easing.OutQuint)
                .Then()
                .ScaleTo(1, 180, Easing.OutQuint);

            highlight.ClearTransforms();
            highlight.FadeTo(0.45f, 50, Easing.OutQuint)
                     .Then()
                     .FadeTo(0.2f, 220, Easing.OutQuint);
        }

        private void updateGaugeStyling()
        {
            var gaugeProcessor = HealthProcessor as BmsGaugeProcessor;
            var gaugeType = gaugeProcessor?.GaugeType ?? BmsGaugeType.Normal;
            (barColour, accentColour) = getGaugeColours(gaugeType);

            gaugeLabel.Text = gaugeProcessor?.IsGaugeAutoShiftActive == true ? $"GAS / {gaugeType.GetDisplayName()}" : gaugeType.GetDisplayName();
            gaugeLabel.Colour = accentColour;
            gaugeValue.Colour = BmsDefaultHudPalette.SurfaceText;

            track.BorderColour = accentColour.Opacity(0.32f);
            trackBackground.Colour = ColourInfo.GradientVertical(BmsDefaultHudPalette.TrackBackground, BmsDefaultHudPalette.TrackShade);

            fillBox.Colour = barColour;

            highlight.Colour = accentColour;

            float floorGauge = (float)BmsGaugeProcessor.GetFloorGauge(gaugeType);
            bool survivalGauge = BmsGaugeProcessor.UsesSurvivalClear(gaugeType);

            floorBand.Width = floorGauge;
            floorBand.Colour = accentColour.Opacity(0.16f);
            floorBand.Alpha = survivalGauge || floorGauge <= 0 ? 0 : 1;

            floorMarker.X = floorGauge;
            floorMarker.Colour = accentColour.Opacity(0.7f);
            floorMarker.Alpha = survivalGauge || floorGauge <= 0 ? 0 : 1;

            clearMarker.X = (float)BmsGaugeProcessor.CLEAR_THRESHOLD;
            clearMarker.Colour = BmsDefaultHudPalette.ThresholdMarker;
            clearMarker.Alpha = survivalGauge ? 0 : 1;
        }

        private static (Color4 BarColour, Color4 AccentColour) getGaugeColours(BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => (BmsDefaultHudPalette.GaugeAssistEasyBar, BmsDefaultHudPalette.GaugeAssistEasyAccent),
                BmsGaugeType.Easy => (BmsDefaultHudPalette.GaugeEasyBar, BmsDefaultHudPalette.GaugeEasyAccent),
                BmsGaugeType.Normal => (BmsDefaultHudPalette.GaugeNormalBar, BmsDefaultHudPalette.GaugeNormalAccent),
                BmsGaugeType.Hard => (BmsDefaultHudPalette.GaugeHardBar, BmsDefaultHudPalette.GaugeHardAccent),
                BmsGaugeType.ExHard => (BmsDefaultHudPalette.GaugeExHardBar, BmsDefaultHudPalette.GaugeExHardAccent),
                BmsGaugeType.Hazard => (BmsDefaultHudPalette.GaugeHazardBar, BmsDefaultHudPalette.GaugeHazardAccent),
                _ => (BmsDefaultHudPalette.SurfaceText, BmsDefaultHudPalette.SurfaceSubtext),
            };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            currentGaugeType?.UnbindAll();
        }
    }
}
