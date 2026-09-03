// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Input.Handlers;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class DrawableBmsRuleset : DrawableScrollingRuleset<HitObject>, IGameplaySkinSceneRuntimeSource, IGameplaySkinEventObjectSnapshotSource
    {
        public const double MIN_TIME_RANGE = 290;

        // Derived from the official-cab reference HS 10 + WN 350 => GN 300.
        public const double MAX_TIME_RANGE = 100000d / 13d;

        public new BmsPlayfield Playfield => (BmsPlayfield)base.Playfield;

        public BmsLongNoteMode LongNoteMode => BmsScoreProcessor.GetLongNoteMode(Mods);

        public BmsJudgeMode JudgeMode => BmsJudgeModeExtensions.GetJudgeMode(Mods);

        public override int Variant => Beatmap is BmsBeatmap bmsBeatmap
            ? BmsRuleset.GetLaneCount(bmsBeatmap.BmsInfo.Keymode)
            : throw new InvalidOperationException("BMS variant requires parser-owned keymode authority.");

        protected override bool RelativeScaleBeatLengths => true;

        protected new BmsRulesetConfigManager Config => (BmsRulesetConfigManager)base.Config;

        private readonly Bindable<BmsHiSpeedMode> configHiSpeedMode = new Bindable<BmsHiSpeedMode>();
        private readonly BindableDouble configNormalHiSpeed = new BindableDouble();
        private readonly BindableDouble configFloatingHiSpeed = new BindableDouble();
        private readonly BindableDouble configClassicHiSpeed = new BindableDouble();
        private readonly BindableDouble selectedHiSpeed = new BindableDouble();
        private IBindable<double>? playfieldScrollLengthRatio;
        private readonly BindableBool laneCoverFocusPressed = new BindableBool();
        private readonly Bindable<BmsScrollSpeedMetrics> speedMetrics = new Bindable<BmsScrollSpeedMetrics>();
        private readonly Bindable<BmsGameplayAdjustmentTarget?> activeAdjustmentTarget = new Bindable<BmsGameplayAdjustmentTarget?>();
        private readonly BindableInt enabledAdjustmentTargetCount = new BindableInt();
        private readonly Bindable<int> activeAdjustmentTargetIndex = new Bindable<int>();
        private readonly BindableBool adjustmentTargetTemporarilyOverridden = new BindableBool();
        private readonly BindableBool allowAdjustmentWhilePaused = new BindableBool();
        private BmsGameplayAdjustmentTarget? currentGameplayAdjustmentTarget;
        private BmsPreStartSpeedPreview? preStartSpeedPreview;
        private BmsBgaPanel? bgaPanel;
        private BmsHudLayoutPanel? hudLayoutPanel;
        private readonly BindableBool showBga = new BindableBool(true);
        private GameplaySkinLayoutPublication? gameplaySkinPublication;
        private readonly Dictionary<HitObject, long> gameplaySkinObjectIds = new Dictionary<HitObject, long>();
        private readonly Dictionary<HitObject, HitObject> gameplaySkinObjectIdentityOwners = new Dictionary<HitObject, HitObject>();
        private readonly HashSet<long> gameplaySkinActiveObjectIds = new HashSet<long>();
        private readonly Dictionary<GameplaySkinLaneId, bool> gameplaySkinPressedInputs = new Dictionary<GameplaySkinLaneId, bool>();
        private BmsInputManager? gameplaySkinInputManager;
        // Direct lane-local objects (mines and bar lines) can be applied by the playfield while this ruleset is
        // still loading, before the beatmap-root identity table is installed. Keep that provisional namespace above
        // the deterministic [0, Beatmap.HitObjects.Count) root range so early allocations can never alias a later
        // root object ID. The value is stable for the lifetime of this production root and remains read-only to
        // consumers.
        private long nextGameplaySkinObjectId = 1L << 32;
        private readonly Dictionary<int, (GameplaySkinBgaContentState State, long Revision)> gameplaySkinBgaStates =
            new Dictionary<int, (GameplaySkinBgaContentState, long)>();
        private bool gameplaySkinLifecycleStarted;
        private double lastGameplaySkinSnapshotTime = double.NaN;

        public IBindable<BmsScrollSpeedMetrics> SpeedMetrics => speedMetrics;

        public IBindable<BmsHiSpeedMode> HiSpeedMode => configHiSpeedMode;

        public IBindable<double> SelectedHiSpeed => selectedHiSpeed;

        public IBindable<BmsGameplayAdjustmentTarget?> ActiveAdjustmentTarget => activeAdjustmentTarget;

        public IBindable<int> EnabledAdjustmentTargetCount => enabledAdjustmentTargetCount;

        public IBindable<int> ActiveAdjustmentTargetIndex => activeAdjustmentTargetIndex;

        public IBindable<bool> IsAdjustmentTargetTemporarilyOverridden => adjustmentTargetTemporarilyOverridden;

        public bool IsPreStartSpeedPreviewVisible => preStartSpeedPreview?.IsPreviewVisible == true;

        public bool IsPreStartSpeedPreviewPaused => preStartSpeedPreview?.IsPreviewPaused == true;

        public int? PreStartSpeedPreviewLaneIndex => preStartSpeedPreview?.LaneIndex;

        public float PreStartSpeedPreviewProgress => preStartSpeedPreview?.PrimaryNoteProgress ?? -1;

        internal ulong SpeedMetricsToastDisplayCount { get; private set; }

        public BmsInputManager? GameplayInputManager => KeyBindingInputManager as BmsInputManager;

        public BmsGameplayLayoutSnapshot LayoutSnapshot => LayoutProvider.Current;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => LayoutProvider.CurrentMaterialSet;

        /// <summary>
        /// The sole read-only C5 event stream for this exact production gameplay root.
        /// </summary>
        public GameplaySkinEventStream GameplaySkinEventStream
            => GameplaySkinEventRuntime?.EventStream
               ?? throw new InvalidOperationException("A compatibility BMS root does not own a production gameplay-skin event stream.");

        internal GameplaySkinEventRuntimeHost? GameplaySkinEventRuntime { get; private set; }

        /// <summary>
        /// The sole shared declarative scene controller for this exact production publication.
        /// </summary>
        internal GameplaySkinSceneRuntimeHost? GameplaySkinSceneRuntime { get; private set; }

        GameplaySkinSceneRuntimeHost? IGameplaySkinSceneRuntimeSource.GameplaySkinSceneRuntime => GameplaySkinSceneRuntime;

        internal BmsGameplayLayoutProvider LayoutProvider { get; }

        internal void InitialiseCompatibilityLayoutForTesting(
            BmsPlayfieldStyle style = BmsPlayfieldStyle.Center,
            ISkin? skin = null,
            BmsGameplayLayoutEnvironment? environment = null)
            => Playfield.InitialiseCompatibilityForTesting(style, skin, environment);

        internal BmsGameplayLayoutSnapshot? PreStartSpeedPreviewLayoutSnapshot => preStartSpeedPreview?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? PreStartSpeedPreviewMaterialSet => preStartSpeedPreview?.ResolvedMaterialSet;

        internal float PreStartSpeedPreviewNoteScreenSpaceHeight => preStartSpeedPreview?.PrimaryNoteScreenSpaceHeight ?? 0;

        internal BmsGameplayLayoutSnapshot? BgaLayoutSnapshot => bgaPanel?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? BgaMaterialSet => bgaPanel?.ResolvedMaterialSet;

        internal BmsGameplayLayoutSnapshot? HudLayoutSnapshot => hudLayoutPanel?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? HudMaterialSet => hudLayoutPanel?.Carrier?.ResolvedMaterialSet;

        internal GameplaySkinResolvedMaterialSet? GaugeMaterialSet => hudLayoutPanel?.Carrier?.ResolvedMaterialSet;

        internal GameplaySkinResolvedMaterialSet? ComboMaterialSet => hudLayoutPanel?.Carrier?.ResolvedMaterialSet;

        [Resolved(CanBeNull = true)]
        private OnScreenDisplay? bmsOnScreenDisplay { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        public DrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            if (beatmap is not BmsBeatmap)
                throw new ArgumentException("Drawable BMS gameplay requires a converted BmsBeatmap.", nameof(beatmap));

            BmsBeatmapModApplicator.ApplyToBeatmap(beatmap, mods);
            LayoutProvider = Playfield.LayoutProvider;
            Direction.Value = ScrollingDirection.Down;

            TimeRange.MinValue = MIN_TIME_RANGE;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(LayoutProvider);

            if (parent.TryGet(out GameplaySkinLayoutRevisionOwner owner)
                && owner.CurrentPublication is GameplaySkinLayoutPublication publication)
            {
                gameplaySkinPublication = publication;
                LayoutProvider.AttachRevisionOwner(owner);
                BmsGameplayLayoutSnapshot adapter = publication.GetAdapter<BmsGameplayLayoutSnapshot>();

                if (!ReferenceEquals(adapter.Neutral, publication.Snapshot)
                    || !ReferenceEquals(publication.MaterialSet.Snapshot, publication.Snapshot)
                    || !ReferenceEquals(publication.PreparedScene.Snapshot, publication.Snapshot)
                    || !ReferenceEquals(publication.PreparedScene.MaterialSet, publication.MaterialSet)
                    || !ReferenceEquals(publication.MaterialSet.PackageRevision, owner.PackageRevision)
                    || !ReferenceEquals(publication.Snapshot.Context.PackageRevision, owner.PackageRevision)
                    || publication.Snapshot.Context.RulesetId != "bms")
                {
                    throw new InvalidOperationException("The BMS gameplay root does not retain its exact layout/material publication.");
                }

                dependencies.Cache(publication.MaterialSet);
                dependencies.Cache(publication.PreparedScene);

                IGameplaySkinTimingProjection? timingProjection = Beatmap is BmsBeatmap { TimingProfile: not null, ScrollProfile: not null } bmsBeatmap
                    ? new BmsGameplaySkinTimingProjection(bmsBeatmap)
                    : null;
                GameplaySkinEventRuntime = new GameplaySkinEventRuntimeHost(publication, Beatmap, timingProjection, this);
                dependencies.Cache(GameplaySkinEventRuntime);
                dependencies.Cache(GameplaySkinEventRuntime.EventStream);

                GameplaySkinSceneRuntime = new GameplaySkinSceneRuntimeHost(publication, GameplaySkinEventRuntime.EventStream);
                dependencies.Cache(GameplaySkinSceneRuntime);
            }

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Subscribe before the playfield adds its direct lane-local bar-line/mine drawables. Their engine usage
            // begins during child loading, so LoadComplete is too late to construct the initial complete snapshot.
            if (GameplaySkinEventRuntime != null)
                initialiseGameplaySkinEventBridge();

            if (KeyBindingInputManager is BmsInputManager inputManager)
                laneCoverFocusPressed.BindTo(inputManager.LaneCoverFocusPressed);

            laneCoverFocusPressed.BindValueChanged(e =>
            {
                if (e.NewValue)
                    CycleGameplayAdjustmentTarget();
            });

            playfieldScrollLengthRatio = Playfield.ScrollLengthRatio.GetBoundCopy();
            playfieldScrollLengthRatio.BindValueChanged(_ => updateTimeRange(), true);

            Config.BindWith(BmsRulesetSetting.HiSpeedMode, configHiSpeedMode);
            Config.BindWith(BmsRulesetSetting.ScrollSpeed, configNormalHiSpeed);
            Config.BindWith(BmsRulesetSetting.FloatingHiSpeed, configFloatingHiSpeed);
            Config.BindWith(BmsRulesetSetting.ClassicHiSpeed, configClassicHiSpeed);

            configHiSpeedMode.BindValueChanged(_ => refreshHiSpeedConfiguration(), true);
            configNormalHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());
            configFloatingHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());
            configClassicHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());

            // The keysound channel count is no longer user-configurable: the shared pool (created with its default
            // baseline) auto-grows on demand, so the playfield store is left at its constructed default here.
            getSuddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getHiddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getLiftMod()?.LiftUnits.BindValueChanged(_ => refreshSpeedMetrics(), true);

            setupBgaPanel();
            setupHudLayout();

            if (GameplaySkinEventRuntime != null)
                FrameStableComponents.Add(GameplaySkinEventRuntime);

            if (GameplaySkinSceneRuntime != null)
            {
                mountGameplaySkinSceneLayers(GameplaySkinSceneRuntime);
                FrameStableComponents.Add(GameplaySkinSceneRuntime);
            }
        }

        private void mountGameplaySkinSceneLayers(GameplaySkinSceneRuntimeHost sceneRuntime)
        {
            GameplaySkinSceneRuntimeLayers layers = sceneRuntime.Layers;

            // Positive depth renders behind the native playfield; negative depth renders above it. The immutable C5
            // strata therefore preserve background/object/effect ordering without flattening everything into a HUD
            // overlay or changing the C3 playfield transform.
            layers.Background.Depth = 3;
            layers.Underlay.Depth = 2;
            layers.Object.Depth = -1;
            layers.GameplayEffects.Depth = -2;
            PlayfieldAdjustmentContainer.Add(layers.Background);
            PlayfieldAdjustmentContainer.Add(layers.Underlay);
            PlayfieldAdjustmentContainer.Add(layers.Object);
            PlayfieldAdjustmentContainer.Add(layers.GameplayEffects);

            // The BGA player and native HUD already live in Overlays at the default depth. Author foreground,
            // BGA-frame and decoration slots must render above that engine-owned content without gaining control of it.
            layers.Overlay.Depth = -1;
            layers.HudForeground.Depth = -2;
            Overlays.Add(layers.Overlay);
            Overlays.Add(layers.HudForeground);
            sceneRuntime.MarkLayersMounted();
        }

        private void setupHudLayout()
        {
            // Detached compatibility drawables do not own the Player health/score dependency graph. HUD layout is
            // exercised there through its explicit carrier fixture; the production root always has a non-compat exact
            // package and mounts the complete gauge/combo graph below that dependency scope.
            if (LayoutProvider.Current.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                return;

            hudLayoutPanel = new BmsHudLayoutPanel(LayoutProvider);
            Overlays.Add(hudLayoutPanel);
        }

        // Mounts the skinnable BGA panel above the playfield (in Overlays, so lanes never occlude it) and keeps its
        // default placement mirrored to the playfield style (P1-L Phase 5).
        private void setupBgaPanel()
        {
            if (Beatmap is not BmsBeatmap bmsBeatmap)
                return;

            bgaPanel = new BmsBgaPanel(
                bmsBeatmap.BgaTimeline,
                bmsBeatmap.PoorBgaMode,
                LayoutProvider,
                GameplaySkinSceneRuntime);
            Overlays.Add(bgaPanel);

            // Transcode legacy BGA videos during loading so the BGA plays from the first frame (P1-L Phase 5.2 R1).
            // Mounted directly here (not inside the skinnable panel) so its blocking background load is part of the
            // tree the player push awaits; it self-gates (no-op without legacy video / with transcoding disabled).
            Overlays.Add(new BmsBgaVideoPreloader(bmsBeatmap.BgaTimeline));

            updateBgaPlacement();

            Config.BindWith(BmsRulesetSetting.ShowBga, showBga);
            showBga.BindValueChanged(visible => bgaPanel.Alpha = visible.NewValue ? 1 : 0, true);
        }

        private void updateBgaPlacement()
        {
            if (bgaPanel == null)
                return;

            bgaPanel.SetLayout(BmsBgaPanel.ResolveDefaultPlacement(Playfield.LayoutSnapshot.Keymode, Playfield.LayoutSnapshot.Style));
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new BmsPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new BmsPlayfield(Beatmap);

        public override DrawableHitObject<HitObject>? CreateDrawableRepresentation(HitObject h)
        {
            if (Mods.OfType<BmsModAutoplay>().Any() && h is BmsHitObject bmsHitObject)
                bmsHitObject.AutoPlay = true;

            // An exact C5 publication gives playable Note/LN ownership to the lane-local pools. Compatibility roots
            // have no publication (and therefore no prepared scene or pool carrier), so retain the programmatic
            // migration chain rather than making their playable objects disappear. BGM events remain direct in both
            // modes because they are non-visual, short-lived scheduling carriers and have no lane target.
            if (gameplaySkinPublication == null)
            {
                return h is BmsHoldNote holdNote
                    ? new DrawableBmsHoldNote(holdNote, Playfield.LayoutSnapshot, LayoutProvider.CurrentMaterialSet)
                    : new DrawableBmsHitObject(h, Playfield.LayoutSnapshot, LayoutProvider.CurrentMaterialSet);
            }

            return h is BmsHitObject
                ? null
                : new DrawableBmsHitObject(h, Playfield.LayoutSnapshot, LayoutProvider.CurrentMaterialSet);
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay)
            => Mods.OfType<BmsModAutoplay>().Any()
                ? new BmsAutoplayReplayInputHandler(replay)
                : new BmsFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new BmsReplayRecorder(score);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Preload every distinct keysound at load (player mode included, not just autoplay): a fully-keysounded
            // chart otherwise cold-decodes hundreds of WAVs DURING gameplay, and on the converted-mania side the
            // resulting transient large buffers / promotion bursts were measured triggering blocking gen2 full GCs
            // (~220ms freezes) early in the play (P1-J, 2026-06-11). Preloading before gameplay matches LR2/beatoraja.
            Playfield.PrewarmKeysounds(getBeatmapKeysoundSamples());

            NewResult += HandleGameplayJudgementResult;

            // Lane construction can precede the parent bridge even though the bridge is attached in load(). Seed the
            // complete state from the real registered drawables once loading commits; later usage edges are de-duped.
            if (gameplaySkinLifecycleStarted)
            {
                synchroniseExistingGameplaySkinObjectUsages();
                updateGameplaySkinInputEvents();
                updateGameplaySkinBgaState();
            }

            RefreshLaneCoverFocus();
            refreshSpeedMetrics();
            initialisePreStartSpeedPreview();
        }

        private IEnumerable<BmsKeysoundSampleInfo> getBeatmapKeysoundSamples()
        {
            foreach (var hitObject in Beatmap.HitObjects)
            {
                switch (hitObject)
                {
                    case BmsHoldNote holdNote:
                        if (holdNote.HeadKeysoundSample != null)
                            yield return holdNote.HeadKeysoundSample;

                        if (holdNote.TailKeysoundSample != null)
                            yield return holdNote.TailKeysoundSample;

                        break;

                    case BmsHitObject { KeysoundSample: not null } bmsHitObject:
                        yield return bmsHitObject.KeysoundSample;
                        break;

                    case BmsBgmEvent { KeysoundSample: not null } bgmEvent:
                        yield return bgmEvent.KeysoundSample;
                        break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            NewResult -= HandleGameplayJudgementResult;

            if (gameplaySkinLifecycleStarted)
            {
                Playfield.HitObjectUsageBegan -= onGameplaySkinObjectBegan;
                Playfield.HitObjectUsageFinished -= onGameplaySkinObjectFinished;
                NewResult -= onGameplaySkinJudgement;
                if (gameplaySkinInputManager != null)
                {
                    gameplaySkinInputManager.Router.ActionPressed -= onGameplaySkinInputPressed;
                    gameplaySkinInputManager.Router.ActionReleased -= onGameplaySkinInputReleased;
                    gameplaySkinInputManager = null;
                }
                gameplaySkinLifecycleStarted = false;
            }

            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            if (!gameplaySkinLifecycleStarted)
                return;

            updateGameplaySkinInputEvents();
            updateGameplaySkinBgaState();
        }

        private void initialiseGameplaySkinEventBridge()
        {
            for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            {
                HitObject identityOwner = Beatmap.HitObjects[i];
                registerGameplaySkinObjectIdentityTree(identityOwner, identityOwner);
                gameplaySkinObjectIds.TryAdd(identityOwner, i);
            }

            nextGameplaySkinObjectId = Math.Max(nextGameplaySkinObjectId, (long)Beatmap.HitObjects.Count);
            Playfield.HitObjectUsageBegan += onGameplaySkinObjectBegan;
            Playfield.HitObjectUsageFinished += onGameplaySkinObjectFinished;
            NewResult += onGameplaySkinJudgement;

            // Polling remains the frame-stable reconciliation path, but a real input press/release can begin and
            // end between two frames (HID pulses and deterministic test inputs do this routinely). Subscribe to the
            // engine-owned router as well so the read-only event stream records every transition without granting
            // skin code any input authority. The polling pass then observes the same state and emits no duplicate.
            gameplaySkinInputManager = KeyBindingInputManager as BmsInputManager;
            if (gameplaySkinInputManager != null)
            {
                gameplaySkinInputManager.Router.ActionPressed += onGameplaySkinInputPressed;
                gameplaySkinInputManager.Router.ActionReleased += onGameplaySkinInputReleased;
            }

            gameplaySkinLifecycleStarted = true;
        }

        private void onGameplaySkinInputPressed(OmsAction action)
            => publishGameplaySkinInputTransition(action, pressed: true);

        private void onGameplaySkinInputReleased(OmsAction action)
            => publishGameplaySkinInputTransition(action, pressed: false);

        private void publishGameplaySkinInputTransition(OmsAction action, bool pressed)
        {
            if (GameplaySkinEventRuntime == null
                || !OmsBmsActionMap.TryMapToBmsAction(Variant, action, out BmsAction bmsAction)
                || !bmsAction.IsLaneAction())
                return;

            BmsLane? lane = Playfield.Lanes.FirstOrDefault(candidate => candidate.Action.Value == bmsAction);
            BmsGameplayLayoutLane exactLane = lane?.LayoutSnapshotLane
                                              ?? throw new InvalidOperationException("A production BMS input event requires its exact C3 lane target.");
            GameplaySkinLaneId laneId = exactLane.LaneId;

            if (gameplaySkinPressedInputs.TryGetValue(laneId, out bool previous) && previous == pressed)
                return;

            gameplaySkinPressedInputs[laneId] = pressed;
            GameplaySkinEventRuntime.PublishInput(exactLane.NeutralLane.TopologyEntry.Identity.Group.Id, laneId, pressed);
        }

        private void updateGameplaySkinInputEvents()
        {
            if (KeyBindingInputManager is not BmsInputManager inputManager)
                return;

            foreach (BmsLane lane in Playfield.Lanes)
            {
                BmsGameplayLayoutLane exactLane = lane.LayoutSnapshotLane
                                                  ?? throw new InvalidOperationException("A production BMS input event requires its exact C3 lane target.");
                GameplaySkinLaneTopologyEntry topologyLane = exactLane.NeutralLane.TopologyEntry;
                bool pressed = inputManager.KeyBindingContainer.PressedActions.Contains(lane.Action.Value);

                if (gameplaySkinPressedInputs.TryGetValue(exactLane.LaneId, out bool previous) && previous == pressed)
                    continue;

                gameplaySkinPressedInputs[exactLane.LaneId] = pressed;
                GameplaySkinEventRuntime!.PublishInput(topologyLane.Identity.Group.Id, exactLane.LaneId, pressed);
            }
        }

        private void updateGameplaySkinBgaState()
        {
            if (bgaPanel == null)
                return;

            int viewportCount = Math.Max(1, LayoutSnapshot.BgaViewports.Count);

            for (int viewportIndex = 0; viewportIndex < viewportCount; viewportIndex++)
            {
                // P1-L remains the only owner of BGA selection, playback, seek and POOR behaviour. C5 observes the
                // state produced by the mounted engine display; it never replays or interprets the timeline itself.
                if (!bgaPanel.TryGetContentStateAt(
                        viewportIndex,
                        GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                        out GameplaySkinBgaContentState state,
                        out long revision))
                    continue;

                if (gameplaySkinBgaStates.TryGetValue(viewportIndex, out var previous)
                    && previous.Revision == revision
                    && previous.State == state)
                    continue;

                gameplaySkinBgaStates[viewportIndex] = (state, revision);
                GameplaySkinEventRuntime.PublishBga(viewportIndex, state, revision);
            }
        }

        private void onGameplaySkinObjectBegan(HitObject hitObject)
        {
            hitObject = getGameplaySkinObjectIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(hitObject, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            long objectId = getGameplaySkinObjectId(hitObject);

            if (!gameplaySkinActiveObjectIds.Add(objectId))
                return;

            GameplaySkinEventRuntime!.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectSpawned,
                kind,
                GameplaySkinObjectState.Visible,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                getGameplaySkinObjectProgress(hitObject));
        }

        private void synchroniseExistingGameplaySkinObjectUsages()
        {
            foreach (BmsLane lane in Playfield.Lanes)
            {
                foreach (DrawableHitObject drawable in lane.AllHitObjects)
                    onGameplaySkinObjectBegan(drawable.HitObject);
            }
        }

        IEnumerable<GameplaySkinObjectStateSnapshot> IGameplaySkinEventObjectSnapshotSource.CreateGameplaySkinActiveObjectSnapshot(double gameplayTime)
        {
            var snapshots = new Dictionary<long, GameplaySkinObjectStateSnapshot>();
            var snapshotPriorities = new Dictionary<long, int>();
            bool rewound = double.IsFinite(lastGameplaySkinSnapshotTime) && gameplayTime < lastGameplaySkinSnapshotTime;
            lastGameplaySkinSnapshotTime = gameplayTime;

            // AllHitObjects is the engine-owned active usage graph, including the exact lane and group-scoped
            // nested playfields. It is intentionally sampled here rather than reconstructing activity from the
            // beatmap or retaining terminal event-stream state across a seek.
            foreach (DrawableHitObject drawable in Playfield.AllHitObjects)
            {
                HitObject hitObject = getGameplaySkinObjectIdentityOwner(drawable.HitObject);

                if (!tryGetGameplaySkinObjectTarget(hitObject, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                    continue;

                long objectId = getGameplaySkinObjectId(hitObject);
                GameplaySkinObjectState state = getGameplaySkinSnapshotState(drawable, gameplayTime, rewound);
                var snapshot = new GameplaySkinObjectStateSnapshot(
                    objectId,
                    kind,
                    state,
                    groupId!,
                    laneId,
                    hitObject.StartTime,
                    hitObject.GetEndTime(),
                    0);

                // A seek can briefly leave the retiring and rebuilt pooled drawables for one logical hit object in
                // the engine-owned usage graph together. They intentionally retain the same stable object ID; the
                // Reset contract represents that logical object once. Prefer the usage whose real state agrees with
                // the authoritative seek time (the rebuilt non-terminal usage before its end, the judged usage after
                // its end), while preserving deterministic graph order for equal candidates.
                int priority = getGameplaySkinSnapshotPriority(drawable, gameplayTime, state);

                if (!snapshotPriorities.TryGetValue(objectId, out int currentPriority) || priority > currentPriority)
                {
                    snapshots[objectId] = snapshot;
                    snapshotPriorities[objectId] = priority;
                }
            }

            // Keep the edge producer's admission set aligned with the same real usage graph which atomically
            // replaced the Reset snapshot. Subsequent drawable state changes can then perform their one allowed
            // post-reset resynchronisation without synthesising a second spawn authority.
            gameplaySkinActiveObjectIds.Clear();
            gameplaySkinActiveObjectIds.UnionWith(snapshots.Keys);
            return snapshots.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }

        private static int getGameplaySkinSnapshotPriority(
            DrawableHitObject drawable,
            double gameplayTime,
            GameplaySkinObjectState state)
        {
            if (gameplayTime < drawable.HitObject.GetEndTime())
            {
                return state switch
                {
                    GameplaySkinObjectState.Holding => 4,
                    GameplaySkinObjectState.Visible => 3,
                    GameplaySkinObjectState.Scheduled => 2,
                    _ => 1,
                };
            }

            return drawable.Judged ? 2 : 1;
        }

        private static GameplaySkinObjectState getGameplaySkinSnapshotState(DrawableHitObject drawable, double gameplayTime, bool rewound)
        {
            if (drawable is DrawableBmsHoldNote hold)
            {
                // A seek/rewind is an epoch barrier. The engine may deliver its result-revert callbacks one
                // traversal after the clock destination, so a pooled hold can briefly retain a pre-seek broken or
                // terminal result. Do not carry that history into the complete Reset snapshot; the next normal
                // frame will publish any real post-seek state transition through the existing drawable callback.
                if (rewound && gameplayTime < hold.HitObject.GetEndTime())
                    return hold.IsHoldingForTesting ? GameplaySkinObjectState.Holding : GameplaySkinObjectState.Visible;

                return hold.BodyState.Value switch
                {
                    BmsLongNoteBodyState.Holding => GameplaySkinObjectState.Holding,
                    BmsLongNoteBodyState.Broken => GameplaySkinObjectState.Missed,
                    _ when hold.Judged => hold.IsHit ? GameplaySkinObjectState.Hit : GameplaySkinObjectState.Missed,
                    _ => gameplayTime < hold.HitObject.StartTime ? GameplaySkinObjectState.Scheduled : GameplaySkinObjectState.Visible,
                };
            }

            if (drawable.Judged)
                return drawable.IsHit ? GameplaySkinObjectState.Hit : GameplaySkinObjectState.Missed;

            return gameplayTime < drawable.HitObject.StartTime
                ? GameplaySkinObjectState.Scheduled
                : GameplaySkinObjectState.Visible;
        }

        private void onGameplaySkinObjectFinished(HitObject hitObject)
        {
            hitObject = getGameplaySkinObjectIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(hitObject, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            long objectId = getGameplaySkinObjectId(hitObject);

            if (!gameplaySkinActiveObjectIds.Remove(objectId))
                return;

            GameplaySkinEventRuntime!.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectStateChanged,
                kind,
                GameplaySkinObjectState.Completed,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                1);

            GameplaySkinEventRuntime!.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectDespawned,
                kind,
                GameplaySkinObjectState.Despawned,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                1);
        }

        private void onGameplaySkinJudgement(JudgementResult result)
        {
            HitObject identityOwner = getGameplaySkinObjectIdentityOwner(result.HitObject);

            if (!tryGetGameplaySkinObjectTarget(identityOwner, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            long objectId = getGameplaySkinObjectId(identityOwner);

            // The BMS hold parent deliberately resolves to IgnoreHit because its head/tail own score judgement.
            // That still is a real terminal object-state edge, but must not invent a second public judgement grade.
            // A pre-roll/late-added or just-retired drawable can also report its real lane/group judgement after its
            // bounded usage left the event snapshot. Preserve that judgement without resurrecting a stale object ID;
            // object-targeted judgements remain valid only while that exact engine usage is active in this epoch.
            long? activeObjectId = gameplaySkinActiveObjectIds.Contains(objectId) ? objectId : null;

            if (tryMapJudgementGrade(result.Type, out GameplaySkinJudgementGrade grade))
                GameplaySkinEventRuntime!.PublishJudgement(activeObjectId, groupId, laneId, grade, result.TimeOffset, result.HealthIncrease);

            // Head, body-tick and tail judgements all retain the one long-note parent identity. The real pooled
            // hold owner publishes its continuous Visible/Holding/Missed state, so a nested judgement must not
            // overwrite that state with a terminal Hit/Missed edge. The parent hold's own IgnoreHit/IgnoreMiss
            // result is different: it is the authoritative terminal lifecycle edge even though it does not score.
            bool nestedLongNoteJudgement = identityOwner is BmsHoldNote
                                           && !ReferenceEquals(result.HitObject, identityOwner);

            if (nestedLongNoteJudgement || activeObjectId == null)
                return;

            GameplaySkinEventRuntime!.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectStateChanged,
                kind,
                result.IsHit ? GameplaySkinObjectState.Hit : GameplaySkinObjectState.Missed,
                groupId!,
                laneId,
                identityOwner.StartTime,
                identityOwner.GetEndTime(),
                1);
        }

        private bool tryGetGameplaySkinObjectTarget(
            HitObject hitObject,
            out GameplaySkinLaneGroupId? groupId,
            out GameplaySkinLaneId? laneId,
            out GameplaySkinObjectKind kind)
        {
            groupId = null;
            laneId = null;
            kind = GameplaySkinObjectKind.Unspecified;

            if (hitObject is BmsBarLine barLine)
            {
                if ((uint)barLine.GroupLogicalIndex >= (uint)LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Count)
                    return false;

                GameplaySkinLaneTopologyGroup group = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder[barLine.GroupLogicalIndex];

                if (barLine.GroupId == null || !barLine.GroupId.Equals(group.Identity.Id))
                    return false;

                groupId = group.Identity.Id;
                kind = GameplaySkinObjectKind.BarLine;
                return true;
            }

            int logicalIndex;

            switch (hitObject)
            {
                case BmsHoldNote hold:
                    logicalIndex = hold.LaneIndex;
                    kind = GameplaySkinObjectKind.LongNote;
                    break;

                case BmsHitObject note:
                    logicalIndex = note.LaneIndex;
                    kind = GameplaySkinObjectKind.Note;
                    break;

                case BmsMine mine:
                    logicalIndex = mine.LaneIndex;
                    kind = GameplaySkinObjectKind.Mine;
                    break;

                default:
                    return false;
            }

            if (logicalIndex < 0 || logicalIndex >= LayoutSnapshot.LanesInLogicalOrder.Count)
                return false;

            BmsGameplayLayoutLane lane = LayoutSnapshot.GetLaneByLogicalIndex(logicalIndex);
            groupId = lane.NeutralLane.TopologyEntry.Identity.Group.Id;
            laneId = lane.LaneId;

            return true;
        }

        private long getGameplaySkinObjectId(HitObject hitObject)
        {
            HitObject identityOwner = getGameplaySkinObjectIdentityOwner(hitObject);

            if (gameplaySkinObjectIds.TryGetValue(identityOwner, out long id))
                return id;

            id = nextGameplaySkinObjectId++;
            gameplaySkinObjectIds.Add(identityOwner, id);
            return id;
        }

        private HitObject getGameplaySkinObjectIdentityOwner(HitObject hitObject)
            => gameplaySkinObjectIdentityOwners.TryGetValue(hitObject, out HitObject? owner) ? owner : hitObject;

        private void registerGameplaySkinObjectIdentityTree(HitObject hitObject, HitObject identityOwner)
        {
            if (gameplaySkinObjectIdentityOwners.TryGetValue(hitObject, out HitObject? existingOwner))
            {
                if (!ReferenceEquals(existingOwner, identityOwner))
                    throw new InvalidOperationException("A BMS nested hit object cannot change its stable gameplay-skin identity owner.");

                return;
            }

            gameplaySkinObjectIdentityOwners.Add(hitObject, identityOwner);

            foreach (HitObject nested in hitObject.NestedHitObjects)
                registerGameplaySkinObjectIdentityTree(nested, identityOwner);
        }

        internal void RegisterGameplaySkinNestedObjectIdentity(HitObject nestedHitObject, HitObject identityOwner)
        {
            ArgumentNullException.ThrowIfNull(nestedHitObject);
            ArgumentNullException.ThrowIfNull(identityOwner);

            if (identityOwner is not BmsHoldNote || !identityOwner.NestedHitObjects.Contains(nestedHitObject))
                throw new InvalidOperationException("Only a real nested BMS long-note drawable may inherit its parent object's stable identity.");

            registerGameplaySkinObjectIdentityTree(nestedHitObject, identityOwner);
        }

        /// <summary>
        /// Returns the exact stable ID used by this root's sole read-only gameplay event producer.
        /// Native pooled scene owners may bind to it but cannot allocate or publish another identity.
        /// </summary>
        internal long GetGameplaySkinObjectId(HitObject hitObject)
        {
            ArgumentNullException.ThrowIfNull(hitObject);
            return getGameplaySkinObjectId(hitObject);
        }

        internal void PublishGameplaySkinObjectState(HitObject hitObject, GameplaySkinObjectState state)
        {
            ArgumentNullException.ThrowIfNull(hitObject);
            hitObject = getGameplaySkinObjectIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(hitObject, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            long objectId = getGameplaySkinObjectId(hitObject);

            if (!gameplaySkinActiveObjectIds.Contains(objectId) || GameplaySkinEventRuntime == null)
                return;

            GameplaySkinEventRuntime.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectStateChanged,
                kind,
                state,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                getGameplaySkinObjectProgress(hitObject));
        }

        private double getGameplaySkinObjectProgress(HitObject hitObject)
            => GameplaySkinEventRuntime?.GetObjectProgress(hitObject.StartTime, hitObject.GetEndTime()) ?? 0;

        private static bool tryMapJudgementGrade(HitResult result, out GameplaySkinJudgementGrade grade)
        {
            grade = result switch
            {
                HitResult.Miss or HitResult.SmallTickMiss or HitResult.LargeTickMiss or HitResult.ComboBreak => GameplaySkinJudgementGrade.Miss,
                HitResult.Meh => GameplaySkinJudgementGrade.Meh,
                HitResult.Ok => GameplaySkinJudgementGrade.Ok,
                HitResult.Good => GameplaySkinJudgementGrade.Good,
                HitResult.Great => GameplaySkinJudgementGrade.Great,
                HitResult.Perfect or HitResult.SmallTickHit or HitResult.LargeTickHit => GameplaySkinJudgementGrade.Perfect,
                _ => GameplaySkinJudgementGrade.Unspecified,
            };
            return grade != GameplaySkinJudgementGrade.Unspecified;
        }

        public bool AdjustLaneCover(float scrollDelta, bool preferBottom = false)
        {
            if (!canAdjustGameplaySettings || scrollDelta == 0)
                return false;

            bool adjusted = adjustGameplayAdjustment(
                scrollDelta,
                preferBottom
                    ? getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Hidden, BmsGameplayAdjustmentTarget.Sudden)
                    : getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Sudden, BmsGameplayAdjustmentTarget.Hidden),
                refreshLaneCoverFocus: false);

            if (adjusted)
                UpdateLaneCoverFocus(preferBottom);

            return adjusted;
        }

        public bool AdjustGameplayAdjustment(float scrollDelta)
        {
            if (!canAdjustGameplaySettings || scrollDelta == 0)
                return false;

            return adjustGameplayAdjustment(scrollDelta, getPersistentGameplayAdjustmentTarget());
        }

        public bool CycleGameplayAdjustmentTarget()
        {
            if (!canAdjustGameplaySettings)
                return false;

            var enabledTargets = getEnabledGameplayAdjustmentTargets();

            if (enabledTargets.Count <= 1)
                return false;

            var currentTarget = getPersistentGameplayAdjustmentTarget(enabledTargets) ?? enabledTargets[0];
            int currentIndex = enabledTargets.IndexOf(currentTarget);

            currentGameplayAdjustmentTarget = enabledTargets[(currentIndex + 1) % enabledTargets.Count];
            RefreshLaneCoverFocus();
            showSpeedMetricsToast(currentGameplayAdjustmentTarget);
            return true;
        }

        public void RefreshLaneCoverFocus() => updateLaneCoverFocus(getDisplayedGameplayAdjustmentTarget());

        public void UpdateLaneCoverFocus(bool preferBottom)
            => updateLaneCoverFocus(preferBottom
                ? getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Hidden, BmsGameplayAdjustmentTarget.Sudden)
                : getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Sudden, BmsGameplayAdjustmentTarget.Hidden));

        private void updateLaneCoverFocus(BmsGameplayAdjustmentTarget? target)
        {
            var enabledTargets = getEnabledGameplayAdjustmentTargets();
            var persistentTarget = getPersistentGameplayAdjustmentTarget(enabledTargets);

            enabledAdjustmentTargetCount.Value = enabledTargets.Count;
            activeAdjustmentTargetIndex.Value = target.HasValue ? enabledTargets.IndexOf(target.Value) : -1;
            adjustmentTargetTemporarilyOverridden.Value = target != persistentTarget;

            BmsLaneCoverPosition? targetPosition = getLaneCoverTargetPosition(target);

            activeAdjustmentTarget.Value = target;

            foreach (var laneCover in Playfield.LaneCovers)
                laneCover.IsFocused.Value = targetPosition == laneCover.CoverPosition;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnScroll(e);

            if (adjustGameplayAdjustment((float)e.ScrollDelta.Y, getDisplayedGameplayAdjustmentTarget()))
                return true;

            return base.OnScroll(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed && e.Button == MouseButton.Middle && CycleGameplayAdjustmentTarget())
                return true;

            return base.OnMouseDown(e);
        }

        protected override void AdjustScrollSpeed(int amount)
        {
            AdjustSelectedHiSpeed(amount);
        }

        public static double ComputeScrollTime(double scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;

        public void SetAllowAdjustmentWhilePaused(bool allow)
        {
            allowAdjustmentWhilePaused.Value = allow;
            RefreshLaneCoverFocus();
        }

        public void SetPreStartSpeedPreviewState(bool active, bool paused = false)
            => preStartSpeedPreview?.UpdateState(active, paused);

        public bool AdjustSelectedHiSpeed(int amount)
        {
            if (amount == 0)
                return false;

            BindableDouble bindable = getSelectedHiSpeedBindable();
            double previous = bindable.Value;

            bindable.Value += amount * configHiSpeedMode.Value.GetAdjustmentStep();

            if (previous == bindable.Value)
                return false;

            showSpeedMetricsToast(getPersistentGameplayAdjustmentTarget());
            return true;
        }

        internal BmsScrollSpeedMetrics GetScrollSpeedMetrics()
            => BmsScrollSpeedMetrics.FromRuntime(
                configHiSpeedMode.Value,
                selectedHiSpeed.Value,
                playfieldScrollLengthRatio?.Value ?? 1,
                getModeTimeRangeScale(configHiSpeedMode.Value),
                getLaneCoverUnits(BmsLaneCoverPosition.Sudden),
                getLaneCoverUnits(BmsLaneCoverPosition.Hidden),
                Playfield.LiftUnits.Value);

        public void DisplaySpeedMetricsToast(BmsGameplayAdjustmentTarget? target = null)
            => showSpeedMetricsToast(target);

        private void refreshHiSpeedConfiguration()
        {
            selectedHiSpeed.Value = getSelectedHiSpeedBindable().Value;
            updateTimeRange();
            refreshSpeedMetrics();
        }

        private void initialisePreStartSpeedPreview()
        {
            if (preStartSpeedPreview != null)
                return;

            BmsLane? previewLane = Playfield.Lanes.FirstOrDefault(lane => !lane.IsScratch);

            if (previewLane == null)
                return;

            previewLane.PreviewContainer.Add(preStartSpeedPreview = new BmsPreStartSpeedPreview(
                previewLane.LayoutSnapshotLane ?? throw new InvalidOperationException("Pre-start preview requires the exact gameplay layout lane."),
                Playfield.LayoutSnapshot.Keymode,
                SpeedMetrics,
                Playfield.LayoutSnapshot,
                LayoutProvider.CurrentMaterialSet));
        }

        private void updateTimeRange() => TimeRange.Value = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(configHiSpeedMode.Value, selectedHiSpeed.Value, Beatmap.GetMostCommonBeatLength(), getInitialBeatLength(), Beatmap.Difficulty.SliderMultiplier) * (playfieldScrollLengthRatio?.Value ?? 1);

        private void refreshSpeedMetrics()
        {
            speedMetrics.Value = GetScrollSpeedMetrics();
        }

        internal void HandleGameplayJudgementResult(JudgementResult judgementResult)
        {
            // A miss (POOR) flashes the BGA poor layer per the chart's #POORBGA mode.
            if (judgementResult.Type == HitResult.Miss)
                bgaPanel?.NotifyMiss();
        }

        private float getLaneCoverUnits(BmsLaneCoverPosition position)
            => Playfield.LaneCovers.FirstOrDefault(cover => cover.CoverPosition == position)?.CoverPercent.Value ?? 0;

        protected override PassThroughInputManager CreateInputManager() => new BmsInputManager(Ruleset.RulesetInfo, Variant);

        private bool adjustGameplayAdjustment(float scrollDelta, BmsGameplayAdjustmentTarget? target, bool refreshLaneCoverFocus = true)
        {
            Mod? adjustedMod = getModForAdjustmentTarget(target);

            bool adjusted = target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => getSuddenMod()?.AdjustCoverPercent(scrollDelta) == true,
                BmsGameplayAdjustmentTarget.Hidden => getHiddenMod()?.AdjustCoverPercent(scrollDelta) == true,
                BmsGameplayAdjustmentTarget.Lift => getLiftMod()?.AdjustLiftUnits(scrollDelta) == true,
                _ => false,
            };

            if (!adjusted)
                return false;

            rememberGameplayAdjustment(adjustedMod);

            if (refreshLaneCoverFocus)
                RefreshLaneCoverFocus();

            showSpeedMetricsToast(target);
            return true;
        }

        private List<BmsGameplayAdjustmentTarget> getEnabledGameplayAdjustmentTargets()
        {
            var targets = new List<BmsGameplayAdjustmentTarget>(3);

            if (getSuddenMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Sudden);

            if (getHiddenMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Hidden);

            if (getLiftMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Lift);

            return targets;
        }

        private BmsGameplayAdjustmentTarget? getPersistentGameplayAdjustmentTarget(IReadOnlyList<BmsGameplayAdjustmentTarget>? enabledTargets = null)
        {
            enabledTargets ??= getEnabledGameplayAdjustmentTargets();

            if (enabledTargets.Count == 0)
                return null;

            if (currentGameplayAdjustmentTarget == null || !enabledTargets.Contains(currentGameplayAdjustmentTarget.Value))
                currentGameplayAdjustmentTarget = enabledTargets[0];

            return currentGameplayAdjustmentTarget;
        }

        private BmsGameplayAdjustmentTarget? getFirstEnabledGameplayAdjustmentTarget(params BmsGameplayAdjustmentTarget[] orderedTargets)
        {
            foreach (var target in orderedTargets)
            {
                if (getModForAdjustmentTarget(target) != null)
                    return target;
            }

            return null;
        }

        private BmsGameplayAdjustmentTarget? getDisplayedGameplayAdjustmentTarget()
        {
            return getPersistentGameplayAdjustmentTarget();
        }

        private BmsModSudden? getSuddenMod() => Mods.OfType<BmsModSudden>().SingleOrDefault();

        private BmsModHidden? getHiddenMod() => Mods.OfType<BmsModHidden>().SingleOrDefault();

        private BmsModLift? getLiftMod() => Mods.OfType<BmsModLift>().SingleOrDefault();

        private void rememberGameplayAdjustment(Mod? adjustedMod)
        {
            if (adjustedMod is not IBmsGameplayAdjustmentMod gameplayAdjustmentMod || !gameplayAdjustmentMod.RememberGameplayChanges.Value)
                return;

            var selectedMod = selectedMods?.Value.SingleOrDefault(mod => mod.GetType() == adjustedMod.GetType());

            if (selectedMod == null || ReferenceEquals(selectedMod, adjustedMod))
                return;

            selectedMod.CopyFrom(adjustedMod);
        }

        private Mod? getModForAdjustmentTarget(BmsGameplayAdjustmentTarget? target)
            => target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => getSuddenMod(),
                BmsGameplayAdjustmentTarget.Hidden => getHiddenMod(),
                BmsGameplayAdjustmentTarget.Lift => getLiftMod(),
                _ => null,
            };

        private static BmsLaneCoverPosition? getLaneCoverTargetPosition(BmsGameplayAdjustmentTarget? target)
            => target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => BmsLaneCoverPosition.Sudden,
                BmsGameplayAdjustmentTarget.Hidden => BmsLaneCoverPosition.Hidden,
                _ => null,
            };

        private void showSpeedMetricsToast(BmsGameplayAdjustmentTarget? target = null)
        {
            SpeedMetricsToastDisplayCount++;
            bmsOnScreenDisplay?.Display(new BmsSpeedMetricsToast(GetScrollSpeedMetrics(), target ?? getPersistentGameplayAdjustmentTarget()));
        }

        private bool canAdjustGameplaySettings => (!IsPaused.Value && (FrameStableClock?.IsRunning ?? true)) || allowAdjustmentWhilePaused.Value;

        private BindableDouble getSelectedHiSpeedBindable()
            => configHiSpeedMode.Value switch
            {
                BmsHiSpeedMode.Normal => configNormalHiSpeed,
                BmsHiSpeedMode.Floating => configFloatingHiSpeed,
                BmsHiSpeedMode.Classic => configClassicHiSpeed,
                _ => configNormalHiSpeed,
            };

        private double getModeTimeRangeScale(BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => 1,
                BmsHiSpeedMode.Floating => Beatmap.GetMostCommonBeatLength() / getInitialBeatLength(),
                BmsHiSpeedMode.Classic => Beatmap.GetMostCommonBeatLength() / ((Beatmap.Difficulty.SliderMultiplier > 0 ? Beatmap.Difficulty.SliderMultiplier : 1) * TimingControlPoint.DEFAULT_BEAT_LENGTH),
                _ => 1,
            };

        private double getInitialBeatLength()
        {
            double referenceTime = Beatmap.HitObjects.Count > 0 ? Math.Max(0, Beatmap.HitObjects[0].StartTime) : 0;
            double beatLength = Beatmap.ControlPointInfo.TimingPointAt(referenceTime).BeatLength;
            return beatLength > 0 ? beatLength : TimingControlPoint.DEFAULT_BEAT_LENGTH;
        }

        private partial class BmsSpeedMetricsToast : Toast
        {
            public BmsSpeedMetricsToast(BmsScrollSpeedMetrics metrics, BmsGameplayAdjustmentTarget? target)
                : base(@"BMS speed", $@"GN {metrics.GreenNumber} ({metrics.VisibleLaneTime:0}ms)")
            {
                string targetText = target == null ? @"AUTO" : target.Value.GetAbbreviation();
                ExtraText = $@"Target {targetText} | {metrics.HiSpeedMode.GetShortLabel()} {metrics.HiSpeedMode.FormatValue(metrics.ScrollSpeed)} | WN {metrics.WhiteNumber} | HID {metrics.HiddenUnits} | LIFT {metrics.LiftUnits}";
            }
        }
    }
}
