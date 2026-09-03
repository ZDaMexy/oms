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
using osu.Game.Rulesets.Bms.Difficulty;
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

        internal IReadOnlyList<BmsBarLinePlayfield> BarLinePlayfields => barLinePlayfields;

        public int DisplayColumnCount => LaneLayout.Lanes.Count;

        private BmsLane[] lanes = Array.Empty<BmsLane>();
        private BmsBarLinePlayfield[] barLinePlayfields = Array.Empty<BmsBarLinePlayfield>();
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

        private Container playfieldContainer = null!;
        private BmsPlayfieldStageFallbackVisual[] gameplaySkinStageFallbackVisuals = Array.Empty<BmsPlayfieldStageFallbackVisual>();
        private readonly List<IDisposable> gameplaySkinSceneVisualRegistrations = new List<IDisposable>();

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime { get; set; }

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }

        internal IReadOnlyList<BmsPlayfieldStageFallbackVisual> GameplaySkinStageFallbackVisuals => gameplaySkinStageFallbackVisuals;

        internal Drawable PlayfieldBackdropVisual => gameplaySkinStageFallbackVisuals[0].BackdropVisual;

        internal Drawable PlayfieldBaseplateVisual => gameplaySkinStageFallbackVisuals[0].BaseplateVisual;

        internal Drawable JudgementVisual => gameplaySkinStageFallbackVisuals[0].JudgementVisual;

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

            gameplaySkinStageFallbackVisuals = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder
                                                        .Select(group => new BmsPlayfieldStageFallbackVisual(
                                                            group,
                                                            LayoutSnapshot,
                                                            LaneLayout.Keymode,
                                                            DisplayColumnCount,
                                                            gameplay_judgements))
                                                        .ToArray();

            var playfieldChildren = new List<Drawable>();
            playfieldChildren.AddRange(gameplaySkinStageFallbackVisuals.Select(stage => stage.BaseplateVisual));
            playfieldChildren.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = groupContainers,
            });
            playfieldChildren.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Child = HitObjectContainer,
            });
            playfieldChildren.Add(CoverContainer);
            playfieldChildren.AddRange(gameplaySkinStageFallbackVisuals.Select(stage => stage.JudgementVisual));
            playfieldChildren.AddRange(gameplaySkinStageFallbackVisuals.Select(stage => stage.JudgementPooler));

            var rootChildren = new List<Drawable> { KeysoundStore };
            rootChildren.AddRange(gameplaySkinStageFallbackVisuals.Select(stage => stage.StageBackgroundVisual));
            rootChildren.AddRange(gameplaySkinStageFallbackVisuals.Select(stage => stage.BackdropVisual));
            rootChildren.Add(playfieldContainer = new Container
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
                Children = playfieldChildren,
            });

            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = rootChildren,
            });

            registerGameplaySkinSceneVisuals();

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
            NewResult += OnNewResult;
        }

        protected override void Dispose(bool isDisposing)
        {
            NewResult -= OnNewResult;

            if (isDisposing)
            {
                foreach (IDisposable registration in gameplaySkinSceneVisualRegistrations)
                    registration.Dispose();

                gameplaySkinSceneVisualRegistrations.Clear();
            }

            base.Dispose(isDisposing);
        }

        private void registerGameplaySkinSceneVisuals()
        {
            foreach (IDisposable registration in gameplaySkinSceneVisualRegistrations)
                registration.Dispose();

            gameplaySkinSceneVisualRegistrations.Clear();

            if (gameplaySkinSceneRuntime == null)
                return;

            if (!ReferenceEquals(gameplaySkinSceneRuntime.Publication.Snapshot, LayoutSnapshot.Neutral)
                || !ReferenceEquals(gameplaySkinSceneRuntime.MaterialSet, ResolvedMaterialSet))
            {
                throw new InvalidOperationException("The BMS playfield scene gates require its exact committed layout/material publication.");
            }

            foreach (BmsPlayfieldStageFallbackVisual stage in gameplaySkinStageFallbackVisuals)
            {
                gameplaySkinSceneVisualRegistrations.Add(gameplaySkinSceneRuntime.RegisterProgrammaticVisual(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageBackground, stage.Target),
                    stage.StageBackgroundVisual));
                gameplaySkinSceneVisualRegistrations.Add(gameplaySkinSceneRuntime.RegisterProgrammaticVisual(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBackdrop, stage.Target),
                    stage.BackdropVisual));
                gameplaySkinSceneVisualRegistrations.Add(gameplaySkinSceneRuntime.RegisterProgrammaticVisual(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBaseplate, stage.Target),
                    stage.BaseplateVisual));
                gameplaySkinSceneVisualRegistrations.Add(gameplaySkinSceneRuntime.RegisterProgrammaticVisual(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, stage.Target),
                    stage.JudgementVisual));
            }
        }

        public override void Add(HitObject hitObject)
        {
            if (hitObject is BmsBarLine barLine)
            {
                getBarLinePlayfield(barLine).Add(hitObject);
                return;
            }

            if (hitObject is BmsHitObject bmsHitObject)
            {
                getLane(bmsHitObject).Add(hitObject);
                return;
            }

            base.Add(hitObject);
        }

        public override bool Remove(HitObject hitObject)
        {
            if (hitObject is BmsBarLine barLine)
                return getBarLinePlayfield(barLine).Remove(hitObject);

            if (hitObject is BmsHitObject bmsHitObject)
                return getLane(bmsHitObject).Remove(hitObject);

            return base.Remove(hitObject);
        }

        public override void Add(DrawableHitObject h)
        {
            if (h.HitObject is BmsBarLine barLine)
            {
                getBarLinePlayfield(barLine).Add(h);
                return;
            }

            if (h.HitObject is BmsHitObject bmsHitObject)
            {
                getLane(bmsHitObject).Add(h);
                return;
            }

            base.Add(h);
        }

        public override bool Remove(DrawableHitObject h)
        {
            if (h.HitObject is BmsBarLine barLine)
                return getBarLinePlayfield(barLine).Remove(h);

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

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            if (result.IsHit && result.HitObject is BmsHitObject hitExplosionObject)
            {
                if (drawableRuleset == null && gameplaySkinSceneRuntime != null)
                    throw new InvalidOperationException("A production BMS hit explosion requires the engine-owned object identity provider.");

                getLane(hitExplosionObject).ShowHitExplosion(
                    result,
                    drawableRuleset?.GetGameplaySkinObjectId(result.HitObject) ?? 0);
            }

            foreach (BmsPlayfieldStageFallbackVisual stage in gameplaySkinStageFallbackVisuals)
                stage.JudgementVisual.Clear(false);

            BmsPlayfieldStageFallbackVisual targetStage = gameplaySkinStageFallbackVisuals[0];

            if (judgedObject.HitObject is BmsHitObject bmsHitObject)
            {
                GameplaySkinLaneGroupId groupId = LayoutProvider.GetLaneForObject(bmsHitObject).NeutralLane.TopologyEntry.Identity.Group.Id;
                targetStage = gameplaySkinStageFallbackVisuals.Single(stage => stage.Target.GroupId!.Equals(groupId));
            }

            var judgement = targetStage.JudgementPooler.Get(result.Type, j => j.Apply(result, judgedObject));

            if (judgement != null)
                targetStage.JudgementVisual.Add(judgement);
        }

        private BmsLane createLane(BmsLaneLayout.Lane lane)
        {
            BmsGameplayLayoutLane snapshotLane = LayoutSnapshot.GetLaneByLogicalIndex(lane.LaneIndex);
            BmsLane drawableLane = lane.IsScratch
                ? new BmsScratchLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, LiftUnits, snapshotLane, LayoutSnapshot, ResolvedMaterialSet)
                : new BmsLane(lane, DisplayColumnCount, LaneLayout.Keymode, LayoutProfile, LiftUnits, snapshotLane, LayoutSnapshot, ResolvedMaterialSet);

            drawableLane.SetKeysoundTimeline(beatmap.GetLaneKeysoundTimeline(lane.LaneIndex));

            GameplaySkinLayoutGroup group = LayoutSnapshot.Neutral.GetGroup(snapshotLane.NeutralLane.TopologyEntry.Identity.Group.Id);
            applyLaneBounds(drawableLane, snapshotLane, group.Rect);

            return drawableLane;
        }

        private BmsGameplayLayoutGroupContainer createGroupContainer(GameplaySkinLayoutGroup group)
        {
            var groupLanes = lanes.Where(lane => lane.LayoutSnapshotLane?.NeutralLane.TopologyEntry.Identity.Group.Id == group.GroupId).ToArray();
            BmsBarLinePlayfield barLinePlayfield = barLinePlayfields.Single(owner => owner.GroupId.Equals(group.GroupId));
            var container = new BmsGameplayLayoutGroupContainer(group.GroupId, LayoutSnapshot)
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.X,
                RelativeSizeAxes = Axes.Both,
                Height = 1,
                Children = groupLanes.Cast<Drawable>().Append(barLinePlayfield).ToArray(),
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
            barLinePlayfields = LayoutSnapshot.Neutral.GroupsInLogicalOrder
                                                  .Select(group => new BmsBarLinePlayfield(group, LayoutSnapshot, ResolvedMaterialSet, LiftUnits))
                                                  .ToArray();
            groupContainers = LayoutSnapshot.Neutral.GroupsInLogicalOrder.Select(createGroupContainer).ToArray();

            if (lanes.Length > 0)
            {
                laneScrollLengthRatio = lanes[0].ScrollLengthRatio.GetBoundCopy();
                laneScrollLengthRatio.BindValueChanged(ratio => scrollLengthRatio.Value = ratio.NewValue, true);
            }

            foreach (BmsLane lane in lanes)
                AddNested(lane);

            foreach (BmsBarLinePlayfield barLinePlayfield in barLinePlayfields)
                AddNested(barLinePlayfield);

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

        private BmsBarLinePlayfield getBarLinePlayfield(BmsBarLine barLine)
        {
            if ((uint)barLine.GroupLogicalIndex >= (uint)barLinePlayfields.Length)
                throw new InvalidOperationException("A BMS bar line targets a group outside the exact C3 topology.");

            BmsBarLinePlayfield playfield = barLinePlayfields[barLine.GroupLogicalIndex];

            if (barLine.GroupId == null || !barLine.GroupId.Equals(playfield.GroupId))
                throw new InvalidOperationException("A BMS bar line's stable GroupId does not match its exact logical group.");

            return playfield;
        }

        private void addMeasureBarLines(BmsBeatmap beatmap)
        {
            foreach (double startTime in beatmap.MeasureStartTimes)
            {
                foreach (BmsBarLinePlayfield playfield in barLinePlayfields)
                    playfield.AddMeasureBarLine(startTime);
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
                lanes[laneIndex].Add(mine);
            }
        }
    }

    /// <summary>
    /// One deck-local owner for the legacy/programmatic visuals represented by stage-scoped public slots.
    /// The wrappers use only rectangles from the exact C3 snapshot; they do not solve geometry.
    /// </summary>
    internal sealed class BmsPlayfieldStageFallbackVisual
    {
        public GameplaySkinResolvedMaterialTarget Target { get; }

        public Container StageBackgroundVisual { get; }

        public Container BackdropVisual { get; }

        public Container BaseplateVisual { get; }

        public JudgementContainer<DrawableBmsJudgement> JudgementVisual { get; }

        public JudgementPooler<DrawableBmsJudgement> JudgementPooler { get; }

        public BmsPlayfieldStageFallbackVisual(
            GameplaySkinLaneTopologyGroup group,
            BmsGameplayLayoutSnapshot snapshot,
            BmsKeymode keymode,
            int laneCount,
            IEnumerable<HitResult> gameplayJudgements)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(gameplayJudgements);

            Target = GameplaySkinResolvedMaterialTarget.ForStage(group);
            GameplaySkinLayoutRect groupRect = snapshot.Neutral.GetGroup(group.Identity.Id).Rect;
            // The legacy BMS renderer has one textured playfield backdrop but no separate outer stage art. Keep a
            // real, independently gateable stage owner with an empty compatibility fallback so authoring one public
            // surface can never hide the other. This owner is geometry-only and performs no second skin lookup.
            StageBackgroundVisual = createStageOwner(
                groupRect,
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                new Container
                {
                    Name = "Stage background compatibility owner",
                    RelativeSizeAxes = Axes.Both,
                });
            BackdropVisual = createStageOwner(
                groupRect,
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                new SkinnableDrawable(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Backdrop, keymode, laneCount))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                });
            BaseplateVisual = createStageOwner(
                groupRect,
                snapshot.PlayfieldRect,
                new SkinnableDrawable(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Baseplate, keymode, laneCount))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                });
            JudgementVisual = new JudgementContainer<DrawableBmsJudgement>
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.X,
                RelativeSizeAxes = Axes.Both,
                X = (groupRect.X - snapshot.PlayfieldRect.X) / snapshot.PlayfieldRect.Width,
                Width = groupRect.Width / snapshot.PlayfieldRect.Width,
                Height = 1,
            };
            JudgementPooler = new JudgementPooler<DrawableBmsJudgement>(
                gameplayJudgements,
                judgement => judgement.InitialiseStage(group.Identity.Id));
        }

        private static Container createStageOwner(
            GameplaySkinLayoutRect rect,
            GameplaySkinLayoutRect parent,
            Drawable child)
            => new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Position = new osuTK.Vector2((rect.X - parent.X) / parent.Width, (rect.Y - parent.Y) / parent.Height),
                Size = new osuTK.Vector2(rect.Width / parent.Width, rect.Height / parent.Height),
                Masking = true,
                Child = child,
            };
    }
}
