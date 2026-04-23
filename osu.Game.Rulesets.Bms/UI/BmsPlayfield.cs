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
        private const float side_anchored_horizontal_inset = 0.05f;

        private readonly BindableDouble scrollLengthRatio = new BindableDouble(1);
        private readonly BindableFloat liftUnits = new BindableFloat();
        private readonly Bindable<BmsPlayfieldStyle> playfieldStyle = new Bindable<BmsPlayfieldStyle>();
        private readonly IBindable<double>? laneScrollLengthRatio;

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

        private JudgementContainer<DrawableBmsJudgement> judgements = null!;
        private JudgementPooler<DrawableBmsJudgement> judgementPooler = null!;
        private Container playfieldContainer = null!;

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
        private void load(BmsRulesetConfigManager config)
        {
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
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
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

            config.BindWith(BmsRulesetSetting.PlayfieldStyle, playfieldStyle);
            playfieldStyle.BindValueChanged(_ => applyPlayfieldStyle(), true);
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
                ? new BmsScratchLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, liftUnits)
                : new BmsLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, liftUnits);

            applyLaneBounds(drawableLane, lane, LaneLayout.TotalRelativeWidth);

            return drawableLane;
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
                    playfieldContainer.Anchor = Anchor.CentreLeft;
                    playfieldContainer.Origin = Anchor.CentreLeft;
                    playfieldContainer.X = side_anchored_horizontal_inset;
                    break;

                case BmsPlayfieldStyle.P2:
                    playfieldContainer.Anchor = Anchor.CentreRight;
                    playfieldContainer.Origin = Anchor.CentreRight;
                    playfieldContainer.X = -side_anchored_horizontal_inset;
                    break;

                default:
                    playfieldContainer.Anchor = Anchor.Centre;
                    playfieldContainer.Origin = Anchor.Centre;
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
    }
}
