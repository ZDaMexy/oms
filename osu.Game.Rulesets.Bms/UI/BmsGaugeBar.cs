// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsGaugeBar : HealthDisplay, ISerialisableDrawable
    {
        // Single integrated band: the gauge IS the bottom of the playfield column, with the label/value overlaid on the
        // bar (IIDX groove-gauge read) rather than floating in a separate header row above it.
        private const float bar_height = 34;

        // Number of faint vertical dividers slicing the track into even cells (groove-gauge read; not IIDX per-cell detail).
        private const int segment_count = 10;

        private readonly Container track;
        private readonly List<GaugeStageVisual> stageVisuals = new List<GaugeStageVisual>();
        private IBindable<BmsGaugeType>? currentGaugeType;
        private IBindable<BmsGaugeRulesFamily>? currentGaugeRulesFamily;
        private double currentDisplayMaxGauge = 1;

        private Color4 barColour = BmsDefaultHudPalette.SurfaceText;
        private Color4 accentColour = BmsDefaultHudPalette.SurfaceText;
        public bool UsesFixedAnchor { get; set; }

        protected override bool PlayInitialIncreaseAnimation => false;

        [Resolved(CanBeNull = true)]
        private HUDOverlay? gaugeHudOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinLayoutRevisionOwner? layoutOwner { get; set; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        internal GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; private set; }

        internal Drawable GameplaySkinFallbackVisual => stageVisuals[0];

        internal IReadOnlyList<Drawable> GameplaySkinStageFallbackVisuals => stageVisuals;

        public BmsGaugeBar()
        {
            Height = bar_height;

            // The gauge is a single flush band (no surrounding border, no separate header row) so it merges into the
            // playfield strip above it instead of reading as a bolt-on widget.
            InternalChild = track = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = bar_height,
                Masking = true,
                CornerRadius = 0,
            };
        }

        // Faint, evenly-spaced vertical dividers overlaid on the track to give a segmented groove-gauge read.
        private static Container createSegmentTicks()
        {
            var ticks = new Drawable[segment_count - 1];

            for (int i = 1; i < segment_count; i++)
            {
                ticks[i - 1] = new Box
                {
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    X = (float)i / segment_count,
                    Colour = BmsDefaultHudPalette.SurfaceText.Opacity(0.08f),
                };
            }

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = ticks,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            LayoutSnapshot = BmsGameplayLayoutProvider.ResolveOwnerPublication(
                layoutOwner,
                layoutProvider,
                "bms.layout.missing-gauge-publication");
            ResolvedMaterialSet = BmsGameplayLayoutProvider.ResolveOwnerMaterialSet(
                layoutOwner,
                layoutProvider,
                "bms.material.missing-gauge-publication");

            if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, LayoutSnapshot.Neutral))
                throw new InvalidOperationException("The BMS gauge does not retain the material set from its exact publication.");

            createStageVisuals();
            updateGaugeStyling();
        }

        private void createStageVisuals()
        {
            if (stageVisuals.Count != 0)
                throw new InvalidOperationException("The BMS gauge stage-local visual graph is immutable after load.");

            GameplaySkinLayoutRect playfield = LayoutSnapshot!.PlayfieldRect;

            foreach (GameplaySkinLaneTopologyGroup group in LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder)
            {
                GameplaySkinLayoutRect groupRect = LayoutSnapshot.Neutral.GetGroup(group.Identity.Id).Rect;
                var visual = new GaugeStageVisual(
                    GameplaySkinResolvedMaterialTarget.ForStage(group),
                    (groupRect.X - playfield.X) / playfield.Width,
                    groupRect.Width / playfield.Width);
                stageVisuals.Add(visual);
                track.Add(visual);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The BMS groove gauge is core gameplay information and must stay visible even when the generic health bar
            // is hidden (ShowHealthBar=false via NoFail etc.). HealthDisplay fades `this` to 0 in that case; re-assert
            // full visibility. This binding fires AFTER the base's (bound-copy subscribers run before own subscribers),
            // so it wins. The HUD-wide ShowHud fade still applies via the parent and is unaffected.
            gaugeHudOverlay?.ShowHealthBar.BindValueChanged(_ =>
                {
                    FinishTransforms();
                    Alpha = 1;
                }, true);

            if (HealthProcessor is BmsGaugeProcessor gaugeProcessor)
            {
                currentGaugeType = gaugeProcessor.GaugeTypeBindable.GetBoundCopy();
                currentGaugeType.BindValueChanged(_ => updateGaugeStyling(), true);

                currentGaugeRulesFamily = gaugeProcessor.GaugeRulesFamilyBindable.GetBoundCopy();
                currentGaugeRulesFamily.BindValueChanged(_ => updateGaugeStyling(), true);
                return;
            }

            updateGaugeStyling();
        }

        protected override void Update()
        {
            base.Update();

            float fillWidth = (float)(currentDisplayMaxGauge <= 0 ? 0 : Current.Value / currentDisplayMaxGauge);

            foreach (GaugeStageVisual stage in stageVisuals)
                stage.UpdateValue(fillWidth, $"{Current.Value:P0}");
        }

        protected override void Flash()
        {
            foreach (GaugeStageVisual stage in stageVisuals)
                stage.Flash();
        }

        private void updateGaugeStyling()
        {
            var gaugeProcessor = HealthProcessor as BmsGaugeProcessor;
            var gaugeType = gaugeProcessor?.GaugeType ?? BmsGaugeType.Normal;
            var gaugeRulesFamily = gaugeProcessor?.GaugeRulesFamily ?? BmsGaugeRulesFamily.Legacy;
            (barColour, accentColour) = getGaugeColours(gaugeType);

            currentDisplayMaxGauge = gaugeProcessor?.CurrentMaximumGauge ?? 1;

            float floorGauge = (float)((gaugeProcessor?.CurrentFloorGauge ?? BmsGaugeProcessor.GetFloorGauge(gaugeType)) / currentDisplayMaxGauge);
            bool survivalGauge = BmsGaugeProcessor.UsesSurvivalClear(gaugeType);
            float clearGauge = (float)((gaugeProcessor?.CurrentClearThreshold ?? BmsGaugeProcessor.CLEAR_THRESHOLD) / currentDisplayMaxGauge);
            string label = getGaugeLabel(gaugeProcessor, gaugeType, gaugeRulesFamily);

            foreach (GaugeStageVisual stage in stageVisuals)
                stage.UpdateStyle(label, barColour, accentColour, floorGauge, clearGauge, survivalGauge);
        }

        private static string getGaugeLabel(BmsGaugeProcessor? gaugeProcessor, BmsGaugeType gaugeType, BmsGaugeRulesFamily gaugeRulesFamily)
        {
            string label = gaugeProcessor?.IsGaugeAutoShiftActive == true ? $"GAS / {gaugeType.GetDisplayName()}" : gaugeType.GetDisplayName();

            if (gaugeRulesFamily != BmsGaugeRulesFamily.Legacy)
                label += $" / {gaugeRulesFamily.GetDisplayName()}";

            return label;
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

        private sealed partial class GaugeStageVisual : Container
        {
            private readonly OsuSpriteText gaugeLabel;
            private readonly OsuSpriteText gaugeValue;
            private readonly Box trackBackground;
            private readonly Container fill;
            private readonly Box fillBox;
            private readonly Box floorBand;
            private readonly Box floorMarker;
            private readonly Box clearMarker;
            private readonly Box highlight;
            private readonly Box topAccent;

            public GameplaySkinResolvedMaterialTarget Target { get; }

            public GaugeStageVisual(GameplaySkinResolvedMaterialTarget target, float x, float width)
            {
                Target = target ?? throw new ArgumentNullException(nameof(target));
                RelativePositionAxes = Axes.X;
                RelativeSizeAxes = Axes.Both;
                X = x;
                Width = width;
                Masking = true;
                Children = new Drawable[]
                {
                    trackBackground = new Box { RelativeSizeAxes = Axes.Both },
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
                            fillBox = new Box { RelativeSizeAxes = Axes.Both },
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 2,
                                Colour = BmsDefaultHudPalette.SurfaceText.Opacity(0.12f),
                            },
                        },
                    },
                    createSegmentTicks(),
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
                    topAccent = new Box
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                    },
                    gaugeLabel = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Left = 8 },
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Colour = BmsDefaultHudPalette.SurfaceText,
                        Shadow = true,
                    },
                    gaugeValue = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 8 },
                        Font = OsuFont.Numeric.With(size: 20, fixedWidth: true),
                        Colour = BmsDefaultHudPalette.SurfaceText,
                        Shadow = true,
                    },
                };
            }

            public void UpdateValue(float width, string value)
            {
                fill.Width = width;
                gaugeValue.Text = value;
            }

            public void Flash()
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

            public void UpdateStyle(
                string label,
                Color4 barColour,
                Color4 accentColour,
                float floorGauge,
                float clearGauge,
                bool survivalGauge)
            {
                gaugeLabel.Text = label;
                trackBackground.Colour = ColourInfo.GradientVertical(BmsDefaultHudPalette.GaugeTrackTop, BmsDefaultHudPalette.GaugeTrackBottom);
                topAccent.Colour = accentColour.Opacity(0.55f);
                fillBox.Colour = barColour;
                highlight.Colour = accentColour;
                floorBand.Width = floorGauge;
                floorBand.Colour = accentColour.Opacity(0.16f);
                floorBand.Alpha = survivalGauge || floorGauge <= 0 ? 0 : 1;
                floorMarker.X = floorGauge;
                floorMarker.Colour = accentColour.Opacity(0.7f);
                floorMarker.Alpha = survivalGauge || floorGauge <= 0 ? 0 : 1;
                clearMarker.X = clearGauge;
                clearMarker.Colour = BmsDefaultHudPalette.ThresholdMarker;
                clearMarker.Alpha = survivalGauge ? 0 : 1;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            currentGaugeType?.UnbindAll();
            currentGaugeRulesFamily?.UnbindAll();
        }
    }
}
