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
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI.Scrolling;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class BmsPlayfield : ScrollingPlayfield
    {
        private readonly BindableDouble scrollLengthRatio = new BindableDouble(1);
        private readonly Bindable<BmsGimmickScrollMode> gimmickScrollMode = new Bindable<BmsGimmickScrollMode>();
        private IBindable<double>? laneScrollLengthRatio;

        // BMS-side scrolling info re-cached to lanes so the stop-motion bypass can be injected without touching shared
        // core types (P1-L Phase 2). Follows the base algorithm exactly until engaged for a gimmick chart.
        private BmsScrollingInfo bmsScrollingInfo = null!;

        public BmsLaneLayout LaneLayout { get; private set; } = null!;

        public BmsGameplayLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        public BmsBackgroundLayer BackgroundLayer { get; }

        internal BmsPlayfieldLayoutProfile LayoutProfile => LayoutSnapshot.Profile;

#pragma warning disable IDE0032 // [Cached] must remain on a field for the dependency source generator.
        [Cached]
        private readonly BmsKeysoundStore keysoundStore = new BmsKeysoundStore();

        public BmsKeysoundStore KeysoundStore => keysoundStore;
#pragma warning restore IDE0032

        public IBindable<double> ScrollLengthRatio => scrollLengthRatio;

        public BindableFloat LiftUnits { get; } = new BindableFloat();

        public Container CoverContainer { get; } = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };

        public IEnumerable<BmsLaneCover> LaneCovers => CoverContainer.Children.OfType<BmsLaneCover>();

        public IReadOnlyList<BmsLane> Lanes => lanes;

        public IReadOnlyList<BmsGameplayLayoutGroupContainer> GroupContainers => groupContainers;

        public int DisplayColumnCount => LaneLayout.Lanes.Count;

        private BmsLane[] lanes = Array.Empty<BmsLane>();
        private BmsGameplayLayoutGroupContainer[] groupContainers = Array.Empty<BmsGameplayLayoutGroupContainer>();
        private readonly BmsBeatmap beatmap;
        private bool layoutGraphInitialised;

        internal BmsGameplayLayoutProvider LayoutProvider { get; }
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
        private GameplaySkinLayoutRevisionOwner? sharedLayoutOwner { get; set; }

        public BmsPlayfield(IBeatmap beatmap)
            : this(beatmap, new BmsGameplayLayoutProvider(
                beatmap as BmsBeatmap
                ?? throw new ArgumentException("BMS gameplay layout requires parser-owned BmsBeatmapInfo keymode authority.", nameof(beatmap))))
        {
        }

        private BmsPlayfield(IBeatmap beatmap, BmsGameplayLayoutProvider layoutProvider)
        {
            this.beatmap = beatmap as BmsBeatmap
                           ?? throw new ArgumentException("BMS gameplay layout requires parser-owned BmsBeatmapInfo keymode authority.", nameof(beatmap));
            LayoutProvider = layoutProvider ?? throw new ArgumentNullException(nameof(layoutProvider));
            BackgroundLayer = new BmsBackgroundLayer(this.beatmap.BmsInfo);
        }

        /// <summary>
        /// Explicit isolated-test entry. Production gameplay obtains its publication from the enclosing exact skin root.
        /// </summary>
        internal static BmsPlayfield CreateCompatibility(BmsBeatmap beatmap, BmsPlayfieldStyle style = BmsPlayfieldStyle.Center)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            var provider = new BmsGameplayLayoutProvider(beatmap);
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(style, new BmsGameplayLayoutConfiguration());
            var playfield = new BmsPlayfield(beatmap, provider);
            playfield.initialiseLayoutGraph(snapshot);
            return playfield;
        }

        internal void InitialiseCompatibilityForTesting(
            BmsPlayfieldStyle style = BmsPlayfieldStyle.Center,
            ISkin? skin = null,
            BmsGameplayLayoutEnvironment? environment = null)
        {
            if (layoutGraphInitialised)
                throw new InvalidOperationException("The BMS playfield layout graph has already been constructed.");

            BmsGameplayLayoutSnapshot snapshot = LayoutProvider.PublishForTesting(
                style,
                BmsGameplayLayoutConfiguration.FromSkin(skin, beatmap.BmsInfo.Keymode),
                environment);
            initialiseLayoutGraph(snapshot);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            // Shadow the ruleset's shared IScrollingInfo for our lanes with a BMS-side wrapper. Direction/TimeRange pass
            // through; only the scroll algorithm can later diverge (and only for gimmick charts under the gate). Guarded
            // so an isolated (parent-less) playfield keeps the base behaviour instead of throwing.
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(LayoutProvider);
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
            if (!layoutGraphInitialised)
            {
                if (sharedLayoutOwner == null)
                    throw new InvalidOperationException("bms.layout.explicit-compatibility-required");

                LayoutProvider.AttachCommittedPublication(sharedLayoutOwner);
                initialiseLayoutGraph(LayoutProvider.Current);
            }
            else
            {
                if (LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("A preconstructed BMS playfield is restricted to explicit compatibility tests.");

                if (sharedLayoutOwner != null && sharedLayoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("A compatibility BMS playfield cannot enter an exact production root.");
            }

            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    KeysoundStore,
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
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativePositionAxes = Axes.Both,
                        RelativeSizeAxes = Axes.Both,
                        Position = new osuTK.Vector2(LayoutSnapshot.PlayfieldRect.X, LayoutSnapshot.PlayfieldRect.Y),
                        Size = new osuTK.Vector2(LayoutSnapshot.PlayfieldRect.Width, LayoutSnapshot.PlayfieldRect.Height),
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
                                Children = groupContainers,
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
            BmsGameplayLayoutLane snapshotLane = LayoutSnapshot.GetLaneByLogicalIndex(lane.LaneIndex);
            BmsLane drawableLane = lane.IsScratch
                ? new BmsScratchLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, LiftUnits, snapshotLane, LayoutSnapshot)
                : new BmsLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, LiftUnits, snapshotLane, LayoutSnapshot);

            drawableLane.SetKeysoundTimeline(beatmap.GetLaneKeysoundTimeline(lane.LaneIndex));

            GameplaySkinLayoutGroup group = LayoutSnapshot.Neutral.GetGroup(snapshotLane.NeutralLane.TopologyEntry.Identity.Group.Id);
            applyLaneBounds(drawableLane, snapshotLane, group.Rect);

            return drawableLane;
        }

        private BmsGameplayLayoutGroupContainer createGroupContainer(GameplaySkinLayoutGroup group)
        {
            var groupLanes = lanes.Where(lane => lane.LayoutSnapshotLane?.NeutralLane.TopologyEntry.Identity.Group.Id == group.GroupId).ToArray();
            var container = new BmsGameplayLayoutGroupContainer(group.GroupId, LayoutSnapshot)
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.X,
                RelativeSizeAxes = Axes.Both,
                Height = 1,
                Children = groupLanes,
            };

            applyGroupBounds(container, group.Rect, LayoutSnapshot.PlayfieldRect);

            foreach (BmsLane lane in groupLanes)
                applyLaneBounds(lane, lane.LayoutSnapshotLane!, group.Rect);

            return container;
        }

        private void initialiseLayoutGraph(BmsGameplayLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (layoutGraphInitialised)
                throw new InvalidOperationException("The immutable BMS playfield layout graph has already been constructed.");

            if (snapshot.Keymode != beatmap.BmsInfo.Keymode)
                throw new InvalidOperationException("The BMS playfield snapshot does not match the parser-owned keymode.");

            LayoutSnapshot = snapshot;
            ResolvedMaterialSet = LayoutProvider.CurrentMaterialSet;

            if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, snapshot.Neutral))
                throw new InvalidOperationException("The BMS playfield material set does not retain its exact layout publication.");

            LaneLayout = snapshot.LaneLayout;
            lanes = LaneLayout.Lanes.Select(createLane).ToArray();
            groupContainers = LayoutSnapshot.Neutral.GroupsInLogicalOrder.Select(createGroupContainer).ToArray();

            if (lanes.Length > 0)
            {
                laneScrollLengthRatio = lanes[0].ScrollLengthRatio.GetBoundCopy();
                laneScrollLengthRatio.BindValueChanged(ratio => scrollLengthRatio.Value = ratio.NewValue, true);
            }

            foreach (BmsLane lane in lanes)
                AddNested(lane);

            addMeasureBarLines(beatmap);
            addMines(beatmap);
            layoutGraphInitialised = true;
        }

        private static void applyLaneBounds(BmsLane drawableLane, BmsGameplayLayoutLane lane, GameplaySkinLayoutRect group)
        {
            drawableLane.RelativePositionAxes = Axes.X;
            drawableLane.X = (lane.NeutralLane.Rect.X - group.X) / group.Width;
            drawableLane.Width = lane.NeutralLane.Rect.Width / group.Width;
            drawableLane.Height = 1;
        }

        private static void applyGroupBounds(BmsGameplayLayoutGroupContainer container, GameplaySkinLayoutRect group, GameplaySkinLayoutRect playfield)
        {
            container.X = (group.X - playfield.X) / playfield.Width;
            container.Width = group.Width / playfield.Width;
            container.Height = 1;
        }

        private BmsLane getLane(BmsHitObject hitObject)
            => lanes[LayoutProvider.GetLaneForObject(hitObject).LogicalIndex];

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
                    }, lane.LayoutLane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, lane.LayoutSnapshotLane, LayoutSnapshot));
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
                int laneIndex = LayoutSnapshot.GetLaneByLogicalIndex(mine.LaneIndex).LogicalIndex;
                lanes[laneIndex].Add(new DrawableBmsMine(mine));
            }
        }
    }
}
