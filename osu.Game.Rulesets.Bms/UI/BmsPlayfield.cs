// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI.Scrolling;
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
        /// <summary>
        /// Horizontal screen-edge inset kept when the playfield is side-anchored (P1/P2). Exposed so the gauge bar can
        /// mirror the exact same side anchoring directly under the lanes (see <see cref="DefaultBmsHudLayoutDisplay"/>).
        /// </summary>
        public const float SIDE_ANCHORED_HORIZONTAL_INSET = 0.05f;

        private readonly BindableDouble scrollLengthRatio = new BindableDouble(1);
        private readonly BindableFloat liftUnits = new BindableFloat();
        private readonly Bindable<BmsPlayfieldStyle> playfieldStyle = new Bindable<BmsPlayfieldStyle>();
        private readonly Bindable<BmsGimmickScrollMode> gimmickScrollMode = new Bindable<BmsGimmickScrollMode>();
        private readonly IBindable<double>? laneScrollLengthRatio;

        // BMS-side scrolling info re-cached to lanes so the stop-motion bypass can be injected without touching shared
        // core types (P1-L Phase 2). Follows the base algorithm exactly until engaged for a gimmick chart.
        private BmsScrollingInfo bmsScrollingInfo = null!;

        [Cached]
        private readonly BmsKeysoundStore keysoundStore = new BmsKeysoundStore();

        public BmsLaneLayout LaneLayout { get; private set; }

        public BmsBackgroundLayer BackgroundLayer { get; }

        public BmsPlayfieldLayoutProfile LayoutProfile => LaneLayout.Profile;

        public BmsKeysoundStore KeysoundStore => keysoundStore;

        public IBindable<double> ScrollLengthRatio => scrollLengthRatio;

        public BindableFloat LiftUnits => liftUnits;

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
        private readonly HashSet<BmsKeysoundSampleInfo> prewarmedKeysounds = new HashSet<BmsKeysoundSampleInfo>();

        private JudgementContainer<DrawableBmsJudgement> judgements = null!;
        private JudgementPooler<DrawableBmsJudgement> judgementPooler = null!;
        private Container playfieldContainer = null!;

        [Resolved(CanBeNull = true)]
        private ISkinSource? skinSource { get; set; }

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
            {
                addMeasureBarLines(bmsBeatmap);
                addMines(bmsBeatmap);
            }
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            // Shadow the ruleset's shared IScrollingInfo for our lanes with a BMS-side wrapper. Direction/TimeRange pass
            // through; only the scroll algorithm can later diverge (and only for gimmick charts under the gate). Guarded
            // so an isolated (parent-less) playfield keeps the base behaviour instead of throwing.
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            var baseScrollingInfo = parent.Get<IScrollingInfo>();

            if (baseScrollingInfo != null)
            {
                bmsScrollingInfo = new BmsScrollingInfo(baseScrollingInfo);
                dependencies.CacheAs<IScrollingInfo>(bmsScrollingInfo);
            }

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(BmsRulesetConfigManager config)
        {
            // Apply any per-keymode skin geometry before mounting, so the playfield strip + lanes pick up the overridden
            // profile. Done here (not the constructor) because the skin is only resolvable once the drawable tree exists.
            if (skinSource != null)
                applySkinGeometry(skinSource);

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
                    playfieldContainer = new Container
                    {
                        // Top-anchored at the screen edge so the first visible notes appear at the very top (matching the
                        // green-number "full visible field" semantics). Vertical extent is controlled by PlayfieldHeight;
                        // applyPlayfieldStyle keeps the top anchor and only varies the horizontal side anchoring. The gauge
                        // (DefaultBmsHudLayoutDisplay) sits just below the judgement line at PlayfieldHeight.
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        RelativePositionAxes = Axes.X,
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
                            // NOTE: the in-playfield BackgroundLayer is intentionally NOT mounted here — inside the
                            // masked playfield strip it sat under the opaque lane backgrounds and was fully occluded.
                            // The visible BGA now renders in the skinnable BmsBgaPanel mounted in DrawableBmsRuleset.Overlays
                            // (above the playfield). BackgroundLayer is kept as a property for skin/metadata compatibility.
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

            config.BindWith(BmsRulesetSetting.PlayfieldStyle, playfieldStyle);
            playfieldStyle.BindValueChanged(_ => applyPlayfieldStyle(), true);

            config.BindWith(BmsRulesetSetting.GimmickScrollMode, gimmickScrollMode);
            gimmickScrollMode.BindValueChanged(_ => updateGimmickScroll(), true);
        }

        // Engages or reverts the stop-motion bypass per the gate. Reverting is byte-for-byte the normal forward-scroll
        // path. Auto-detection is deferred to P1-L Phase 2 Step D, so Auto behaves as Off here.
        private void updateGimmickScroll()
        {
            if (bmsScrollingInfo == null)
                return;

            var profile = (beatmap as BmsBeatmap)?.ScrollProfile;

            bool engage = profile != null && gimmickScrollMode.Value switch
            {
                BmsGimmickScrollMode.On => true,
                BmsGimmickScrollMode.Auto => profile.IsStopMotionGimmick,
                _ => false,
            };

            if (engage)
                bmsScrollingInfo.EngageStopMotion(new BmsStopMotionScrollAlgorithm(profile!));
            else
                bmsScrollingInfo.Disengage();
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

        public void PrewarmKeysounds(IEnumerable<BmsKeysoundSampleInfo> sampleInfos)
        {
            foreach (var sampleInfo in sampleInfos)
            {
                if (!prewarmedKeysounds.Add(sampleInfo))
                    continue;

                PrepareSamplePool(sampleInfo);
            }
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
                ? new BmsScratchLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, liftUnits)
                : new BmsLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, liftUnits);

            drawableLane.SetKeysoundTimeline((beatmap as BmsBeatmap)?.GetLaneKeysoundTimeline(lane.LaneIndex));

            applyLaneBounds(drawableLane, lane, LaneLayout.TotalRelativeWidth);

            return drawableLane;
        }

        // Rebuilds the layout profile with the active skin's per-keymode geometry overrides. With no override present the
        // default profile is left untouched, so non-skin (and non-OMS) play stays byte-identical. HitTargetVerticalOffset
        // is deliberately not skinnable (it must stay 0 to keep scrollLengthRatio == 1, preserving GN / judgement timing).
        // The replaced profile flows to the already-built lanes via the playfield-style bind that fires right after load.
        private void applySkinGeometry(ISkin skin)
        {
            var keymode = LaneLayout.Keymode;

            float? normalLaneWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.NormalLaneWidth, keymode)?.Value;
            float? scratchLaneWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.ScratchLaneWidth, keymode)?.Value;
            float? normalLaneSpacing = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.NormalLaneSpacing, keymode)?.Value;
            float? scratchLaneSpacing = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.ScratchLaneSpacing, keymode)?.Value;
            float? playfieldWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, keymode)?.Value;
            float? playfieldHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldHeight, keymode)?.Value;
            float? hitTargetHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetHeight, keymode)?.Value;
            float? hitTargetBarHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetBarHeight, keymode)?.Value;
            float? hitTargetLineHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetLineHeight, keymode)?.Value;
            float? hitTargetGlowRadius = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetGlowRadius, keymode)?.Value;
            float? barLineHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.BarLineHeight, keymode)?.Value;

            bool anyOverride = normalLaneWidth != null || scratchLaneWidth != null || normalLaneSpacing != null || scratchLaneSpacing != null
                               || playfieldWidth != null || playfieldHeight != null || hitTargetHeight != null || hitTargetBarHeight != null
                               || hitTargetLineHeight != null || hitTargetGlowRadius != null || barLineHeight != null;

            if (!anyOverride)
                return;

            var profile = BmsPlayfieldLayoutProfile.CreateDefault(
                keymode,
                LaneLayout.Profile.LaneCount,
                normalLaneRelativeWidth: normalLaneWidth,
                scratchLaneRelativeWidth: scratchLaneWidth,
                normalLaneRelativeSpacing: normalLaneSpacing,
                scratchLaneRelativeSpacing: scratchLaneSpacing,
                playfieldWidth: playfieldWidth,
                playfieldHeight: playfieldHeight,
                hitTargetHeight: hitTargetHeight,
                hitTargetBarHeight: hitTargetBarHeight,
                hitTargetLineHeight: hitTargetLineHeight,
                hitTargetGlowRadius: hitTargetGlowRadius,
                barLineHeight: barLineHeight);

            LaneLayout = BmsLaneLayout.CreateFor(beatmap, profile);
        }

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

        private void applyPlayfieldStyle()
        {
            var updatedLayout = BmsLaneLayout.CreateFor(beatmap, LayoutProfile, playfieldStyle.Value);

            applyLaneLayout(updatedLayout);

            switch (updatedLayout.Style)
            {
                case BmsPlayfieldStyle.P1:
                    playfieldContainer.Anchor = Anchor.TopLeft;
                    playfieldContainer.Origin = Anchor.TopLeft;
                    playfieldContainer.X = SIDE_ANCHORED_HORIZONTAL_INSET;
                    break;

                case BmsPlayfieldStyle.P2:
                    playfieldContainer.Anchor = Anchor.TopRight;
                    playfieldContainer.Origin = Anchor.TopRight;
                    playfieldContainer.X = -SIDE_ANCHORED_HORIZONTAL_INSET;
                    break;

                default:
                    playfieldContainer.Anchor = Anchor.TopCentre;
                    playfieldContainer.Origin = Anchor.TopCentre;
                    playfieldContainer.X = 0;
                    break;
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

        // Mines are visual-only and live outside beatmap.HitObjects; they are added straight to their lane like bar
        // lines, so they never enter the scoring / statistics / judged-note path.
        private void addMines(BmsBeatmap beatmap)
        {
            if (lanes.Length == 0)
                return;

            foreach (var mine in beatmap.Mines)
            {
                int laneIndex = Math.Clamp(mine.LaneIndex, 0, lanes.Length - 1);
                lanes[laneIndex].Add(new DrawableBmsMine(mine));
            }
        }
    }
}
