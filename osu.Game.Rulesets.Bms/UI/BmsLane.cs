// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsLane : ScrollingPlayfield, IKeyBindingHandler<BmsAction>
    {
        // These are per-lane pools. Keeping the old 96+96 prewarm here multiplied into thousands of
        // SkinReloadableDrawable revision admissions before a 14K root could become ready. The small warm baseline
        // covers ordinary simultaneous lane usage; the bounded ceiling handles dense charts and every returned
        // drawable remains pooled after growth instead of being recreated for each object.
        // DrawablePool prepares its initial stack eagerly. Note/LN hosts carry asynchronous skin/material consumers,
        // so prewarming one stack per lane would perform hundreds of duplicate loads before the first object is
        // inside its lifetime window. A zero baseline keeps the pool bounded and ready; the first active usage creates
        // one host on demand, and subsequent usages reuse it without per-note/per-frame tokenisation or resolution.
        internal const int INITIAL_NOTE_POOL_SIZE = 0;
        internal const int INITIAL_HOLD_NOTE_POOL_SIZE = 0;
        internal const int INITIAL_HOLD_COMPONENT_POOL_SIZE = 0;
        internal const int INITIAL_BODY_TICK_POOL_SIZE = 0;
        internal const int INITIAL_MINE_POOL_SIZE = 0;
        internal const int MAXIMUM_NOTE_POOL_SIZE = 256;
        internal const int MAXIMUM_BODY_TICK_POOL_SIZE = 512;

        internal readonly Bindable<BmsAction> Action = new Bindable<BmsAction>();

        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsLaneLayout.Lane LayoutLane { get; }

        public BmsGameplayLayoutLane? LayoutSnapshotLane { get; }

        public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public BmsHitTarget HitTarget { get; }

        internal Drawable GameplaySkinLaneSurfaceFallbackVisual => laneSurfaceVisual;

        internal Drawable GameplaySkinLaneDividerFallbackVisual => laneDividerVisual;

        public Container PreviewContainer => hitObjectArea.PreviewContainer;

        public IBindable<double> ScrollLengthRatio => hitObjectArea.ScrollLengthRatio;

        protected BmsPlayfieldLayoutProfile LayoutProfile { get; }

        private readonly BmsOrderedHitPolicy hitPolicy;
        private readonly int laneCount;
        private readonly BmsKeymode keymode;
        private readonly BmsHitObjectArea hitObjectArea;
        private readonly SkinnableDrawable laneSurfaceVisual;
        private readonly SkinnableDrawable laneDividerVisual;
        private readonly DrawablePool<BmsPoolableHitExplosion> hitExplosionPool;
        private readonly DrawablePool<DrawableBmsHitObject>? notePool;
        private readonly DrawablePool<DrawableBmsHoldNote>? holdNotePool;
        private readonly DrawablePool<DrawableBmsHoldNoteHead>? holdNoteHeadPool;
        private readonly DrawablePool<DrawableBmsHoldNoteTail>? holdNoteTailPool;
        private readonly DrawablePool<DrawableBmsHoldNoteBodyTick>? holdNoteBodyTickPool;
        private readonly DrawablePool<DrawableBmsMine>? minePool;
        private readonly BindableFloat? liftUnits;
        private GameplaySkinSceneHostedSlot? laneSurfaceGate;
        private GameplaySkinSceneHostedSlot? laneDividerGate;
        private IDisposable? laneSurfaceVisualRegistration;
        private IDisposable? laneDividerVisualRegistration;

        [Resolved(canBeNull: true)]
        private BmsInputManager? inputManager { get; set; }

        [Resolved(CanBeNull = true)]
        private BmsKeysoundStore? keysoundStore { get; set; }

        private IReadOnlyList<BmsLaneKeysoundEntry> keysoundTimeline = Array.Empty<BmsLaneKeysoundEntry>();

        /// <summary>
        /// Supplies the time-ordered keysound assignments for this lane so empty (note-less) key presses can play
        /// the keysound currently armed on the lane (built at conversion time by BmsBeatmap.GetLaneKeysoundTimeline).
        /// </summary>
        internal void SetKeysoundTimeline(IReadOnlyList<BmsLaneKeysoundEntry>? timeline)
            => keysoundTimeline = timeline ?? Array.Empty<BmsLaneKeysoundEntry>();

        public BmsLane(
            BmsLaneLayout.Lane lane,
            int laneCount,
            BmsKeymode keymode,
            BmsPlayfieldLayoutProfile layoutProfile,
            BindableFloat? liftUnits = null,
            BmsGameplayLayoutLane? layoutSnapshotLane = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null,
            GameplaySkinResolvedMaterialSet? resolvedMaterialSet = null)
        {
            if (resolvedMaterialSet != null
                && (layoutSnapshot == null || !ReferenceEquals(resolvedMaterialSet.Snapshot, layoutSnapshot.Neutral)))
            {
                throw new ArgumentException("A BMS lane pool must retain its exact layout/material publication.", nameof(resolvedMaterialSet));
            }

            LayoutLane = lane;
            LayoutSnapshotLane = layoutSnapshotLane;
            LayoutSnapshot = layoutSnapshot;
            LaneIndex = lane.LaneIndex;
            IsScratch = lane.IsScratch;
            this.laneCount = laneCount;
            this.keymode = keymode;
            this.liftUnits = liftUnits;
            LayoutProfile = layoutProfile;
            Name = $"Lane {LaneIndex}";
            Action.Value = lane.Action;
            hitPolicy = new BmsOrderedHitPolicy(HitObjectContainer);

            RelativeSizeAxes = Axes.Both;
            Masking = true;

            laneSurfaceVisual = new SkinnableDrawable(createLookup(BmsLaneSkinElements.Background))
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            };
            laneDividerVisual = new SkinnableDrawable(createLookup(BmsLaneSkinElements.Divider))
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            };
            hitObjectArea = createHitObjectArea();

            HitExplosionPoolCapacity = GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY;

            if (layoutSnapshot != null && resolvedMaterialSet != null)
            {
                BmsGameplayLayoutLane exactLane = layoutSnapshotLane
                                                  ?? throw new InvalidOperationException("A production BMS hit-explosion pool requires its exact C3 lane target.");
                GameplaySkinLaneTopologyEntry topologyLane = exactLane.NeutralLane.TopologyEntry;
                GameplaySkinLaneTopologyGroup topologyGroup = layoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                    candidate.Identity.Id.Equals(topologyLane.Identity.Group.Id));
                var hitExplosionKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.HitExplosion,
                    GameplaySkinResolvedMaterialTarget.ForLane(topologyGroup, topologyLane));

                if (!resolvedMaterialSet.TryGet(hitExplosionKey, out GameplaySkinResolvedMaterialEntry? hitExplosionEntry)
                    && layoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("The exact BMS hit-explosion material entry is missing from its committed publication.");

                if (hitExplosionEntry?.State == GameplaySkinResolvedMaterialState.Suppress)
                    HitExplosionPoolCapacity = 0;

                hitExplosionPool = new ExactHitExplosionPool(
                    layoutSnapshot,
                    resolvedMaterialSet,
                    exactLane,
                    HitExplosionPoolCapacity);
            }
            else
                hitExplosionPool = new DrawablePool<BmsPoolableHitExplosion>(HitExplosionPoolCapacity, HitExplosionPoolCapacity);

            if (layoutSnapshot != null && resolvedMaterialSet != null)
            {
                BmsGameplayLayoutLane exactLane = layoutSnapshotLane
                                                  ?? throw new InvalidOperationException("A production BMS lane pool requires its exact C3 lane target.");
                notePool = new ExactNotePool(layoutSnapshot, resolvedMaterialSet, exactLane, INITIAL_NOTE_POOL_SIZE, MAXIMUM_NOTE_POOL_SIZE);
                holdNotePool = new ExactHoldNotePool(layoutSnapshot, resolvedMaterialSet, exactLane, INITIAL_HOLD_NOTE_POOL_SIZE, MAXIMUM_NOTE_POOL_SIZE);
                holdNoteHeadPool = new ExactHoldNoteHeadPool(layoutSnapshot, resolvedMaterialSet, exactLane, INITIAL_HOLD_COMPONENT_POOL_SIZE, MAXIMUM_NOTE_POOL_SIZE);
                holdNoteTailPool = new ExactHoldNoteTailPool(layoutSnapshot, resolvedMaterialSet, exactLane, INITIAL_HOLD_COMPONENT_POOL_SIZE, MAXIMUM_NOTE_POOL_SIZE);
                holdNoteBodyTickPool = new ExactBodyTickPool(INITIAL_BODY_TICK_POOL_SIZE, MAXIMUM_BODY_TICK_POOL_SIZE);
                minePool = new ExactMinePool(layoutSnapshot, resolvedMaterialSet, exactLane, INITIAL_MINE_POOL_SIZE, MAXIMUM_NOTE_POOL_SIZE);

                RegisterPool<BmsHitObject, DrawableBmsHitObject>(notePool);
                RegisterPool<BmsHoldNote, DrawableBmsHoldNote>(holdNotePool);
                RegisterPool<BmsHoldNoteHead, DrawableBmsHoldNoteHead>(holdNoteHeadPool);
                RegisterPool<BmsHoldNoteTailEvent, DrawableBmsHoldNoteTail>(holdNoteTailPool);
                RegisterPool<BmsHoldNoteBodyTick, DrawableBmsHoldNoteBodyTick>(holdNoteBodyTickPool);
                RegisterPool<BmsMine, DrawableBmsMine>(minePool);
            }

            // Pools must load before the hit-object container. Mine entries are installed while the immutable
            // layout graph is built, so the container may request an already-alive object in its own load completion.
            // Adding the visual hierarchy only after RegisterPool() keeps that dependency ordering deterministic.
            AddRangeInternal(new Drawable[]
            {
                hitExplosionPool,
                laneSurfaceVisual,
                laneDividerVisual,
                hitObjectArea,
            });

            HitTarget = hitObjectArea.HitTarget;
        }

        internal int HitExplosionPoolCapacity { get; }

        internal int HitExplosionPoolSize => hitExplosionPool.CurrentPoolSize;

        internal int HitExplosionsInUse => hitExplosionPool.CountInUse;

        internal int NotePoolSize => notePool?.CurrentPoolSize ?? 0;

        internal int HoldNotePoolSize => holdNotePool?.CurrentPoolSize ?? 0;

        internal int HoldNoteHeadPoolSize => holdNoteHeadPool?.CurrentPoolSize ?? 0;

        internal int HoldNoteTailPoolSize => holdNoteTailPool?.CurrentPoolSize ?? 0;

        internal int HoldNoteBodyTickPoolSize => holdNoteBodyTickPool?.CurrentPoolSize ?? 0;

        internal int MinePoolSize => minePool?.CurrentPoolSize ?? 0;

        internal long HitExplosionCapacityDropCount { get; private set; }

        internal IEnumerable<BmsPoolableHitExplosion> ActiveHitExplosions
            => HitTarget.HitExplosions.Children.OfType<BmsPoolableHitExplosion>();

        internal void ShowHitExplosion(JudgementResult result, long objectId)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);

            if (hitExplosionPool.CountInUse >= HitExplosionPoolCapacity)
            {
                HitExplosionCapacityDropCount++;
                return;
            }

            HitTarget.HitExplosions.Add(hitExplosionPool.Get(explosion => explosion.Apply(result, objectId)));
        }

        private sealed partial class ExactHitExplosionPool : DrawablePool<BmsPoolableHitExplosion>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactHitExplosionPool(
                BmsGameplayLayoutSnapshot layout,
                GameplaySkinResolvedMaterialSet materials,
                BmsGameplayLayoutLane lane,
                int capacity)
                : base(capacity, capacity)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override BmsPoolableHitExplosion CreateNewDrawable()
                => new BmsPoolableHitExplosion(layout, materials, lane);
        }

        [BackgroundDependencyLoader(true)]
        private void loadGameplaySkinScene(GameplaySkinSceneRuntimeHost? sceneRuntime)
        {
            if (sceneRuntime == null || LayoutSnapshotLane == null || LayoutSnapshot == null)
                return;

            GameplaySkinLaneTopologyEntry lane = LayoutSnapshotLane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(lane.Identity.Group.Id));
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);

            sceneRuntime.TryGetVisualGate(
                new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneSurface, target),
                out laneSurfaceGate);
            sceneRuntime.TryGetVisualGate(
                new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneDivider, target),
                out laneDividerGate);

            if (laneSurfaceGate == null || laneDividerGate == null)
                throw new InvalidOperationException("The exact BMS lane scene gates are missing from the committed publication.");

            laneSurfaceVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(laneSurfaceGate.Key, laneSurfaceVisual);
            laneDividerVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(laneDividerGate.Key, laneDividerVisual);
        }

        private sealed partial class ExactNotePool : DrawablePool<DrawableBmsHitObject>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactNotePool(BmsGameplayLayoutSnapshot layout, GameplaySkinResolvedMaterialSet materials, BmsGameplayLayoutLane lane, int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override DrawableBmsHitObject CreateNewDrawable()
                => new DrawableBmsHitObject(new BmsHitObject
                {
                    Keymode = layout.Keymode,
                    LaneIndex = lane.LogicalIndex,
                    IsScratch = lane.IsScratch,
                }, layout, materials);
        }

        private sealed partial class ExactHoldNotePool : DrawablePool<DrawableBmsHoldNote>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactHoldNotePool(BmsGameplayLayoutSnapshot layout, GameplaySkinResolvedMaterialSet materials, BmsGameplayLayoutLane lane, int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override DrawableBmsHoldNote CreateNewDrawable()
                => new DrawableBmsHoldNote(new BmsHoldNote
                {
                    Keymode = layout.Keymode,
                    LaneIndex = lane.LogicalIndex,
                    IsScratch = lane.IsScratch,
                }, layout, materials);
        }

        private sealed partial class ExactHoldNoteHeadPool : DrawablePool<DrawableBmsHoldNoteHead>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactHoldNoteHeadPool(BmsGameplayLayoutSnapshot layout, GameplaySkinResolvedMaterialSet materials, BmsGameplayLayoutLane lane, int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override DrawableBmsHoldNoteHead CreateNewDrawable()
                => new DrawableBmsHoldNoteHead(new BmsHoldNoteHead
                {
                    Keymode = layout.Keymode,
                    LaneIndex = lane.LogicalIndex,
                    IsScratch = lane.IsScratch,
                }, layout, materials);
        }

        private sealed partial class ExactHoldNoteTailPool : DrawablePool<DrawableBmsHoldNoteTail>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactHoldNoteTailPool(BmsGameplayLayoutSnapshot layout, GameplaySkinResolvedMaterialSet materials, BmsGameplayLayoutLane lane, int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override DrawableBmsHoldNoteTail CreateNewDrawable()
                => new DrawableBmsHoldNoteTail(new BmsHoldNoteTailEvent
                {
                    Keymode = layout.Keymode,
                    LaneIndex = lane.LogicalIndex,
                    IsScratch = lane.IsScratch,
                }, layout, materials);
        }

        private sealed partial class ExactBodyTickPool : DrawablePool<DrawableBmsHoldNoteBodyTick>
        {
            public ExactBodyTickPool(int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
            }

            protected override DrawableBmsHoldNoteBodyTick CreateNewDrawable()
                => new DrawableBmsHoldNoteBodyTick(new BmsHoldNoteBodyTick());
        }

        private sealed partial class ExactMinePool : DrawablePool<DrawableBmsMine>
        {
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;
            private readonly BmsGameplayLayoutLane lane;

            public ExactMinePool(BmsGameplayLayoutSnapshot layout, GameplaySkinResolvedMaterialSet materials, BmsGameplayLayoutLane lane, int initialSize, int maximumSize)
                : base(initialSize, maximumSize)
            {
                this.layout = layout;
                this.materials = materials;
                this.lane = lane;
            }

            protected override DrawableBmsMine CreateNewDrawable()
                => new DrawableBmsMine(new BmsMine { LaneIndex = lane.LogicalIndex }, layout, materials, lane);
        }

        protected BmsLaneSkinLookup createLookup(BmsLaneSkinElements element, bool isMajorBarLine = true)
            => new BmsLaneSkinLookup(
                element,
                LaneIndex,
                laneCount,
                IsScratch,
                keymode,
                isMajorBarLine,
                LayoutSnapshotLane?.LaneId,
                element == BmsLaneSkinElements.HitTarget ? LayoutProfile : null,
                element == BmsLaneSkinElements.HitTarget ? LayoutSnapshot : null);

        protected virtual BmsHitTarget createHitTarget() => new BmsHitTarget(createLookup(BmsLaneSkinElements.HitTarget), LayoutProfile, LayoutSnapshot);

        protected virtual BmsHitObjectArea createHitObjectArea()
            => new BmsHitObjectArea(createHitTarget(), LayoutProfile, HitObjectContainer, liftUnits, LayoutSnapshot)
            {
                RelativeSizeAxes = Axes.Both,
            };

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<BmsAction>>(Action);
            return dependencies;
        }

        protected override void Update()
        {
            base.Update();

            if (inputManager == null)
                return;

            bool isPressed = inputManager.KeyBindingContainer.PressedActions.Contains(Action.Value);

            if (HitTarget.IsPressed.Value != isPressed)
                HitTarget.IsPressed.Value = isPressed;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                laneSurfaceVisualRegistration?.Dispose();
                laneDividerVisualRegistration?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            if (drawableHitObject is not DrawableBmsHitObject bmsHitObject)
                return;

            bmsHitObject.CheckHittable = hitPolicy.IsHittable;
            bmsHitObject.OnUserPressedSuccessfully = hitPolicy.HandleHit;
        }

        public virtual bool OnPressed(KeyBindingPressEvent<BmsAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            playCurrentLaneKeysound();

            if (!shouldTriggerEmptyPoor())
                return false;

            triggerEmptyPoor();
            return true;
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
        {
        }

        private void triggerEmptyPoor()
        {
            var drawable = new DrawableBmsEmptyPoorHitObject(new BmsEmptyPoorHitObject
            {
                StartTime = Time.Current,
            })
            {
                Clock = Clock,
            };

            Add(drawable);
            drawable.ApplyEmptyPoor();
        }

        private void playCurrentLaneKeysound()
        {
            if (keysoundStore == null || keysoundTimeline.Count == 0)
                return;

            // Autoplay / auto-scratch / auto-note: the notes in this lane are auto-played and already sound their own
            // keysound through the note auto-apply path (DrawableBmsHitObject.PlaySamples). The autoplay replay still
            // synthesises a key press per note, and because an auto note does not accept input the press falls through
            // to this lane handler. Sounding the lane's armed keysound on top would play every note TWICE — diverging
            // from an actual 100%-perfect play, where the note consumes the press so the lane never sounds. Leave the
            // keysound to the notes when this lane is auto-driven; genuine empty presses only happen on a player lane
            // (a hit note there consumes the press before it can reach here).
            if (laneHasAutoPlayNote())
                return;

            var entry = resolveArmedKeysound(Time.Current);

            if (entry != null)
                keysoundStore.Play(entry.Value.Sample, 0, entry.Value.KeysoundId);
        }

        // True when this lane currently hosts an auto-played note (autoplay mod, or auto-scratch / auto-note). Such a
        // lane is driven entirely by the replay: every press lines up with an auto note that sounds itself, so the
        // lane's own armed-keysound playback would only duplicate it. Player lanes never match (their notes accept
        // input and consume the press on a hit), so the empty-press keysound there is unaffected.
        private bool laneHasAutoPlayNote()
        {
            foreach (var aliveObject in HitObjectContainer.AliveObjects)
            {
                if (aliveObject is DrawableBmsHitObject bmsObject && !bmsObject.AcceptsPlayerInput)
                    return true;
            }

            return false;
        }

        private BmsLaneKeysoundEntry? resolveArmedKeysound(double time)
        {
            // Binary search for the most recent assignment at-or-before `time`. Before the first entry, fall back to
            // the first so the lane is never silent and the opening press previews the first keysound.
            int low = 0;
            int high = keysoundTimeline.Count - 1;
            int resolved = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);

                if (keysoundTimeline[mid].Time <= time)
                {
                    resolved = mid;
                    low = mid + 1;
                }
                else
                    high = mid - 1;
            }

            return keysoundTimeline[resolved < 0 ? 0 : resolved];
        }

        private bool shouldTriggerEmptyPoor()
        {
            double currentTime = Time.Current;
            bool foundCandidate = false;
            bool supportsExcessivePoor = false;
            bool canTriggerSupportedEmptyPoor = false;
            bool hasFutureUnjudgedCandidate = false;

            void inspectCandidate(DrawableBmsHitObject hitObject)
            {
                if (!hitObject.AcceptsPlayerInput)
                    return;

                foundCandidate = true;

                if (hitObject.HitObject.HitWindows is BmsTimingWindows timingWindows && timingWindows.SupportsExcessivePoor)
                {
                    supportsExcessivePoor = true;
                    canTriggerSupportedEmptyPoor |= timingWindows.CanTriggerExcessivePoor(currentTime - hitObject.HitObject.StartTime);
                    return;
                }

                hasFutureUnjudgedCandidate |= !hitObject.Judged && hitObject.HitObject.StartTime > currentTime;
            }

            foreach (var hitObject in HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>())
            {
                inspectCandidate(hitObject);

                if (supportsExcessivePoor && canTriggerSupportedEmptyPoor)
                    return true;
            }

            foreach (var hitObject in HitObjectContainer.Objects.OfType<DrawableBmsHitObject>())
            {
                inspectCandidate(hitObject);

                if (supportsExcessivePoor && canTriggerSupportedEmptyPoor)
                    return true;
            }

            if (!foundCandidate)
                return false;

            return supportsExcessivePoor ? canTriggerSupportedEmptyPoor : hasFutureUnjudgedCandidate;
        }

        private sealed partial class DrawableBmsEmptyPoorHitObject : DrawableHitObject<BmsEmptyPoorHitObject>
        {
            public override bool DisplayResult => true;

            protected override double InitialLifetimeOffset => 0;

            public DrawableBmsEmptyPoorHitObject(BmsEmptyPoorHitObject hitObject)
                : base(hitObject)
            {
                Alpha = 0;
                HandleUserInput = false;
            }

            public void ApplyEmptyPoor() => ApplyResult(HitResult.Ok);

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }

            protected override void UpdateHitStateTransforms(ArmedState state)
            {
                base.UpdateHitStateTransforms(state);

                if (state != ArmedState.Idle)
                    Expire();
            }
        }
    }
}
