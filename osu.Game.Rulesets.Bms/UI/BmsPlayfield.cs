// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class BmsPlayfield : ScrollingPlayfield
    {
        private const float layout_epsilon = 0.0001f;

        private readonly BindableDouble scrollLengthRatio = new BindableDouble(1);
        private readonly IBindable<double>? laneScrollLengthRatio;

        [Cached]
        private readonly BmsKeysoundStore keysoundStore = new BmsKeysoundStore();

        public BmsLaneLayout LaneLayout { get; private set; }

        public BmsBackgroundLayer BackgroundLayer { get; }

        public BmsPlayfieldLayoutProfile LayoutProfile => LaneLayout.Profile;

        public BmsKeysoundStore KeysoundStore => keysoundStore;

        public IBindable<double> ScrollLengthRatio => scrollLengthRatio;

        public Container CoverContainer { get; } = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };

        public IEnumerable<BmsLaneCover> LaneCovers => CoverContainer.Children.OfType<BmsLaneCover>();

        public IReadOnlyList<BmsLane> Lanes => lanes;

        public int DisplayColumnCount => LaneLayout.Lanes.Count;

        private readonly BmsLane[] lanes;
        private readonly IBeatmap beatmap;
        private readonly HitResult[] gameplay_judgements =
        {
            HitResult.Perfect,
            HitResult.Great,
            HitResult.Good,
            HitResult.Meh,
            HitResult.Miss,
            HitResult.Ok,
        };

        private JudgementContainer<DrawableBmsJudgement> judgements = null!;
        private JudgementPooler<DrawableBmsJudgement> judgementPooler = null!;

        public BmsPlayfield(IBeatmap beatmap, BmsPlayfieldLayoutProfile? layoutProfile = null)
        {
            this.beatmap = beatmap;
            var bmsBeatmap = beatmap as BmsBeatmap;

            LaneLayout = BmsLaneLayout.CreateFor(beatmap, layoutProfile);
            BackgroundLayer = new BmsBackgroundLayer(bmsBeatmap?.BmsInfo);
            lanes = LaneLayout.Lanes.Select(createLane).ToArray();

            if (lanes.Length > 0)
            {
                laneScrollLengthRatio = lanes[0].ScrollLengthRatio.GetBoundCopy();
                laneScrollLengthRatio.BindValueChanged(ratio => scrollLengthRatio.Value = ratio.NewValue, true);
            }

            foreach (var lane in lanes)
                AddNested(lane);

            if (bmsBeatmap != null)
                addMeasureBarLines(bmsBeatmap);
        }

        [BackgroundDependencyLoader]
        private void load(BmsRulesetConfigManager? config = null)
        {
            if (config != null)
                applyConfiguredLayoutProfile(config);

            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    keysoundStore,
                    new SkinnableDrawable(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Backdrop, LaneLayout.Keymode, DisplayColumnCount))
                    {
                        RelativeSizeAxes = Axes.Both,
                        CentreComponent = false,
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Width = LayoutProfile.PlayfieldWidth,
                        Height = LayoutProfile.PlayfieldHeight,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new SkinnableDrawable(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Baseplate, LaneLayout.Keymode, DisplayColumnCount))
                            {
                                RelativeSizeAxes = Axes.Both,
                                CentreComponent = false,
                            },
                            BackgroundLayer,
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = lanes,
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Child = HitObjectContainer,
                            },
                            CoverContainer,
                            judgements = new JudgementContainer<DrawableBmsJudgement>
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            judgementPooler = new JudgementPooler<DrawableBmsJudgement>(gameplay_judgements),
                        }
                    }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += onNewResult;
        }

        protected override void Dispose(bool isDisposing)
        {
            NewResult -= onNewResult;
            base.Dispose(isDisposing);
        }

        public override void Add(HitObject hitObject)
        {
            if (hitObject is BmsHitObject bmsHitObject)
            {
                getLane(bmsHitObject).Add(hitObject);
                return;
            }

            base.Add(hitObject);
        }

        public override bool Remove(HitObject hitObject)
        {
            if (hitObject is BmsHitObject bmsHitObject)
                return getLane(bmsHitObject).Remove(hitObject);

            return base.Remove(hitObject);
        }

        public override void Add(DrawableHitObject h)
        {
            if (h.HitObject is BmsHitObject bmsHitObject)
            {
                getLane(bmsHitObject).Add(h);
                return;
            }

            base.Add(h);
        }

        public override bool Remove(DrawableHitObject h)
        {
            if (h.HitObject is BmsHitObject bmsHitObject)
                return getLane(bmsHitObject).Remove(h);

            return base.Remove(h);
        }

        private void onNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            judgements.Clear(false);

            var judgement = judgementPooler.Get(result.Type, j => j.Apply(result, judgedObject));

            if (judgement != null)
                judgements.Add(judgement);
        }

        private BmsLane createLane(BmsLaneLayout.Lane lane)
        {
            BmsLane drawableLane = lane.IsScratch
                ? new BmsScratchLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile)
                : new BmsLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile);

            applyLaneBounds(drawableLane, lane, LaneLayout.TotalRelativeWidth);

            return drawableLane;
        }

        private void applyConfiguredLayoutProfile(BmsRulesetConfigManager config)
        {
            var playfieldWidth = config.GetBindable<double>(BmsRulesetSetting.PlayfieldWidth);
            var playfieldHeight = config.GetBindable<double>(BmsRulesetSetting.PlayfieldHeight);
            float configuredLaneWidth = (float)config.GetBindable<double>(BmsRulesetSetting.LaneWidth).Value;
            float configuredLaneSpacing = (float)config.GetBindable<double>(BmsRulesetSetting.LaneSpacing).Value;
            float configuredScratchWidthRatio = (float)config.GetBindable<double>(BmsRulesetSetting.ScratchLaneWidthRatio).Value;
            float configuredScratchSpacing = (float)config.GetBindable<double>(BmsRulesetSetting.ScratchLaneSpacing).Value;
            float configuredPlayfieldWidth = getConfiguredSizeOverride(playfieldWidth, LayoutProfile.PlayfieldWidth);
            float configuredPlayfieldHeight = getConfiguredSizeOverride(playfieldHeight, LayoutProfile.PlayfieldHeight);
            float configuredHitTargetHeight = (float)config.GetBindable<double>(BmsRulesetSetting.HitTargetHeight).Value;
            float configuredHitTargetBarHeight = (float)config.GetBindable<double>(BmsRulesetSetting.HitTargetBarHeight).Value;
            float configuredHitTargetLineHeight = (float)config.GetBindable<double>(BmsRulesetSetting.HitTargetLineHeight).Value;
            float configuredHitTargetGlowRadius = (float)config.GetBindable<double>(BmsRulesetSetting.HitTargetGlowRadius).Value;
            float configuredHitTargetVerticalOffset = (float)config.GetBindable<double>(BmsRulesetSetting.HitTargetVerticalOffset).Value;
            float configuredBarLineHeight = (float)config.GetBindable<double>(BmsRulesetSetting.BarLineHeight).Value;

            if (Math.Abs(LayoutProfile.PlayfieldWidth - configuredPlayfieldWidth) <= layout_epsilon
                && Math.Abs(LayoutProfile.PlayfieldHeight - configuredPlayfieldHeight) <= layout_epsilon
                && Math.Abs(LayoutProfile.NormalLaneRelativeWidth - configuredLaneWidth) <= layout_epsilon
                && Math.Abs(LayoutProfile.NormalLaneRelativeSpacing - configuredLaneSpacing) <= layout_epsilon
                && Math.Abs(LayoutProfile.ScratchLaneRelativeWidth - configuredScratchWidthRatio) <= layout_epsilon
                && Math.Abs(LayoutProfile.ScratchLaneRelativeSpacing - configuredScratchSpacing) <= layout_epsilon
                && Math.Abs(LayoutProfile.HitTargetHeight - configuredHitTargetHeight) <= layout_epsilon
                && Math.Abs(LayoutProfile.HitTargetBarHeight - configuredHitTargetBarHeight) <= layout_epsilon
                && Math.Abs(LayoutProfile.HitTargetLineHeight - configuredHitTargetLineHeight) <= layout_epsilon
                && Math.Abs(LayoutProfile.HitTargetGlowRadius - configuredHitTargetGlowRadius) <= layout_epsilon
                && Math.Abs(LayoutProfile.HitTargetVerticalOffset - configuredHitTargetVerticalOffset) <= layout_epsilon
                && Math.Abs(LayoutProfile.BarLineHeight - configuredBarLineHeight) <= layout_epsilon)
                return;

            var configuredProfile = BmsPlayfieldLayoutProfile.CreateDefault(
                LaneLayout.Keymode,
                LaneLayout.Lanes.Count,
                normalLaneRelativeWidth: configuredLaneWidth,
                scratchLaneRelativeWidth: configuredScratchWidthRatio,
                normalLaneRelativeSpacing: configuredLaneSpacing,
                scratchLaneRelativeSpacing: configuredScratchSpacing,
                playfieldWidth: configuredPlayfieldWidth,
                playfieldHeight: configuredPlayfieldHeight,
                hitTargetHeight: configuredHitTargetHeight,
                hitTargetBarHeight: configuredHitTargetBarHeight,
                hitTargetLineHeight: configuredHitTargetLineHeight,
                hitTargetGlowRadius: configuredHitTargetGlowRadius,
                hitTargetVerticalOffset: configuredHitTargetVerticalOffset,
                barLineHeight: configuredBarLineHeight);

            applyLaneLayout(BmsLaneLayout.CreateFor(beatmap, configuredProfile));
        }

        private static float getConfiguredSizeOverride(IBindable<double> bindable, float fallback)
            => bindable.IsDefault || bindable.Value <= 0 ? fallback : (float)bindable.Value;

        private void applyLaneLayout(BmsLaneLayout laneLayout)
        {
            if (laneLayout.Lanes.Count != lanes.Length)
                throw new InvalidOperationException("Configured lane layout must match the existing lane count.");

            LaneLayout = laneLayout;

            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i].ApplyLayoutProfile(laneLayout.Lanes[i], laneLayout.Profile);
                applyLaneBounds(lanes[i], laneLayout.Lanes[i], laneLayout.TotalRelativeWidth);
            }
        }

        private static void applyLaneBounds(BmsLane drawableLane, BmsLaneLayout.Lane lane, float totalRelativeWidth)
        {
            drawableLane.RelativePositionAxes = Axes.X;
            drawableLane.X = lane.RelativeStart / totalRelativeWidth;
            drawableLane.Width = lane.RelativeWidth / totalRelativeWidth;
            drawableLane.Height = 1;
        }

        private BmsLane getLane(BmsHitObject hitObject)
            => lanes[Math.Clamp(hitObject.LaneIndex, 0, lanes.Length - 1)];

        private void addMeasureBarLines(BmsBeatmap beatmap)
        {
            foreach (double startTime in beatmap.MeasureStartTimes)
            {
                foreach (var lane in lanes)
                {
                    lane.Add(new DrawableBmsBarLine(new BmsBarLine
                    {
                        StartTime = startTime,
                        Major = true,
                    }, lane.LayoutLane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile));
                }
            }
        }
    }
}
