// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaRuleset : DrawableScrollingRuleset<ManiaHitObject>, IGameplaySkinSceneRuntimeSource, IManiaGameplaySkinObjectIdentityProvider,
                                                 IGameplaySkinEventObjectSnapshotSource
    {
        private const string bms_to_mania_drawable_factory_type = "osu.Game.Rulesets.Bms.Beatmaps.BmsToManiaDrawableRepresentationFactory";
        private const string bms_ruleset_assembly = "osu.Game.Rulesets.Bms";

        /// <summary>
        /// The minimum time range. This occurs at a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 40.
        /// </summary>
        public const double MIN_TIME_RANGE = 290;

        /// <summary>
        /// The maximum time range. This occurs with a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 1.
        /// </summary>
        public const double MAX_TIME_RANGE = 11485;

        public new ManiaPlayfield Playfield => (ManiaPlayfield)base.Playfield;

        public new ManiaBeatmap Beatmap => (ManiaBeatmap)base.Beatmap;

        public IEnumerable<BarLine> BarLines;

        public override bool RequiresPortraitOrientation => Beatmap.Stages.Count == 1 && mobileLayout.Value == ManiaMobileLayout.Portrait;

        protected override bool RelativeScaleBeatLengths => true;

        protected new ManiaRulesetConfigManager Config => (ManiaRulesetConfigManager)base.Config;

        private readonly BindableDouble configScrollSpeed = new BindableDouble();
        private readonly Bindable<ManiaMobileLayout> mobileLayout = new Bindable<ManiaMobileLayout>();
        private readonly Bindable<bool> touchOverlay = new Bindable<bool>();

        public double TargetTimeRange { get; protected set; }

        private double currentTimeRange;

        private static readonly Type? bms_drawable_factory_type = Type.GetType($"{bms_to_mania_drawable_factory_type}, {bms_ruleset_assembly}", throwOnError: false);
        private static readonly Func<ManiaHitObject, bool>? bms_can_create_drawable = createFactoryDelegate<Func<ManiaHitObject, bool>>("CanCreate");
        private static readonly Func<ManiaHitObject, DrawableHitObject<ManiaHitObject>?>? bms_create_drawable = createFactoryDelegate<Func<ManiaHitObject, DrawableHitObject<ManiaHitObject>?>>("Create");

        private static TDelegate? createFactoryDelegate<TDelegate>(string methodName) where TDelegate : Delegate
        {
            MethodInfo? method = bms_drawable_factory_type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            return method == null ? null : (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        private const string bms_to_mania_keysound_store_factory_type = "osu.Game.Rulesets.Bms.Beatmaps.BmsToManiaKeysoundStoreFactory";

        private static readonly Type? bms_keysound_store_factory_type = Type.GetType($"{bms_to_mania_keysound_store_factory_type}, {bms_ruleset_assembly}", throwOnError: false);
        private static readonly Func<IBeatmap, bool>? bms_should_host_keysound_store = createKeysoundStoreFactoryDelegate<Func<IBeatmap, bool>>("ShouldHost");
        private static readonly Func<IRulesetConfigCache, Drawable>? bms_create_keysound_store = createKeysoundStoreFactoryDelegate<Func<IRulesetConfigCache, Drawable>>("Create");

        private static TDelegate? createKeysoundStoreFactoryDelegate<TDelegate>(string methodName) where TDelegate : Delegate
        {
            MethodInfo? method = bms_keysound_store_factory_type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            return method == null ? null : (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        // Shared BMS keysound store hosted for converted-BMS mania playback (J6). Created + cached here so the converted
        // sample-only drawables (BGM / scratch) can resolve it, and added to the tree in load() so it resolves the
        // gameplay clock for pause / seek handling. Only created when the beatmap actually carries converted-BMS
        // keysounds, so normal mania play is unaffected; the BMS assembly being absent is a clean no-op.
        private Drawable? sharedKeysoundStore;

        private GameplaySkinLayoutPublication layoutPublication = null!;
        private readonly Dictionary<GameplaySkinObjectUsageKey, long> gameplaySkinObjectIds = new Dictionary<GameplaySkinObjectUsageKey, long>();
        private readonly Dictionary<HitObject, HitObject> gameplaySkinIdentityOwners = new Dictionary<HitObject, HitObject>();
        private readonly Dictionary<long, GameplaySkinObjectUsage> gameplaySkinActiveObjects = new Dictionary<long, GameplaySkinObjectUsage>();
        private readonly HashSet<long> gameplaySkinCompletedObjectIds = new HashSet<long>();
        private readonly List<GameplaySkinStageUsageSubscription> gameplaySkinStageUsageSubscriptions = new List<GameplaySkinStageUsageSubscription>();
        private readonly Dictionary<GameplaySkinLaneId, bool> gameplaySkinPressedInputs = new Dictionary<GameplaySkinLaneId, bool>();
        private long nextGameplaySkinObjectId;
        private bool gameplaySkinLifecycleStarted;

        internal GameplaySkinLayoutRevisionOwner LayoutRevisionOwner { get; private set; } = null!;

        internal ManiaGameplaySkinLayout LayoutAdapter => layoutPublication.GetAdapter<ManiaGameplaySkinLayout>();

        /// <summary>
        /// The exact immutable layout snapshot shared by the complete mania gameplay tree.
        /// </summary>
        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutPublication.Snapshot;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => layoutPublication.MaterialSet;

        /// <summary>
        /// The sole read-only C5 event stream for this exact production gameplay root.
        /// </summary>
        public GameplaySkinEventStream GameplaySkinEventStream => GameplaySkinEventRuntime.EventStream;

        internal GameplaySkinEventRuntimeHost GameplaySkinEventRuntime { get; private set; } = null!;

        /// <summary>
        /// The sole shared declarative scene controller for this exact production publication.
        /// </summary>
        internal GameplaySkinSceneRuntimeHost GameplaySkinSceneRuntime { get; private set; } = null!;

        GameplaySkinSceneRuntimeHost? IGameplaySkinSceneRuntimeSource.GameplaySkinSceneRuntime => GameplaySkinSceneRuntime;

        public ScrollingDirection PublishedDirection => Direction.Value;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            IReadOnlyDependencyContainer dependencies = base.CreateChildDependencies(parent);
            var wrapped = new DependencyContainer(dependencies);

            if (!dependencies.TryGet(out GameplaySkinLayoutRevisionOwner exactOwner))
            {
                throw new InvalidOperationException(
                    "A mania gameplay root requires an exact provider owner or an explicitly cached compatibility owner.");
            }

            LayoutRevisionOwner = exactOwner;
            layoutPublication = LayoutRevisionOwner.CurrentPublication!;

            if (layoutPublication == null)
            {
                if (LayoutRevisionOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("An exact mania gameplay root must complete background layout preparation before child loading.");

                // Explicit isolation-only compatibility path for visual/unit hosts which do not mount a managed
                // RulesetSkinProvidingContainer. Exact production roots are prepared by the stateless ruleset hook.
                GameplaySkinScrollDirection layoutDirection = Config.Get<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection) == ManiaScrollingDirection.Up
                    ? GameplaySkinScrollDirection.Up
                    : GameplaySkinScrollDirection.Down;
                layoutPublication = ManiaGameplaySkinLayout.PrepareAndPublish(
                    Beatmap,
                    dependencies.Get<ISkinSource>(),
                    LayoutRevisionOwner,
                    dependencies.Get<GameHost>(),
                    layoutDirection);
            }

            ManiaGameplaySkinLayout adapter = layoutPublication.GetAdapter<ManiaGameplaySkinLayout>();
            string expectedNativeContext = $"stages-{string.Join("-", Beatmap.Stages.Select(stage => stage.Columns))}";

            if (!ReferenceEquals(layoutPublication, LayoutRevisionOwner.CurrentPublication)
                || !ReferenceEquals(adapter.Snapshot, layoutPublication.Snapshot)
                || !ReferenceEquals(layoutPublication.MaterialSet.Snapshot, layoutPublication.Snapshot)
                || !ReferenceEquals(layoutPublication.PreparedScene.Snapshot, layoutPublication.Snapshot)
                || !ReferenceEquals(layoutPublication.PreparedScene.MaterialSet, layoutPublication.MaterialSet)
                || !ReferenceEquals(layoutPublication.MaterialSet.PackageRevision, LayoutRevisionOwner.PackageRevision)
                || !ReferenceEquals(layoutPublication.Snapshot.Context.PackageRevision, LayoutRevisionOwner.PackageRevision)
                || layoutPublication.Snapshot.Context.RulesetId != "mania"
                || layoutPublication.Snapshot.Context.NativeContextId != expectedNativeContext)
            {
                throw new InvalidOperationException("The mania gameplay layout does not retain this root's exact package revision.");
            }

            wrapped.Cache(adapter);
            wrapped.Cache(layoutPublication.Snapshot);
            wrapped.Cache(layoutPublication.MaterialSet);
            wrapped.Cache(layoutPublication.PreparedScene);
            wrapped.Cache(LayoutRevisionOwner);

            GameplaySkinEventRuntime = new GameplaySkinEventRuntimeHost(layoutPublication, Beatmap, null, this);
            wrapped.Cache(GameplaySkinEventRuntime);
            wrapped.Cache(GameplaySkinEventRuntime.EventStream);

            GameplaySkinSceneRuntime = new GameplaySkinSceneRuntimeHost(layoutPublication, GameplaySkinEventRuntime.EventStream);
            wrapped.Cache(GameplaySkinSceneRuntime);
            wrapped.CacheAs<IManiaGameplaySkinObjectIdentityProvider>(this);

            if (bms_should_host_keysound_store?.Invoke(Beatmap) == true && bms_create_keysound_store?.Invoke(dependencies.Get<IRulesetConfigCache>()) is Drawable store)
            {
                sharedKeysoundStore = store;

                // Cache under the store's runtime type (BmsKeysoundStore) so the BMS-assembly sample-only drawables
                // (BGM / scratch) resolve it; mania cannot name that type at compile time.
                wrapped.Cache(store);

                // Also cache under the mania-owned IManiaKeysoundStore interface so a pooled DrawableNote (a converted
                // KEY note) can route its keysound through the store without referencing the BMS assembly. This is what
                // lets converted KEY notes stay pooled instead of each becoming a non-pooled drawable (J6 / P1-J #10).
                if (store is IManiaKeysoundStore keysoundStore)
                    wrapped.CacheAs(keysoundStore);
            }

            return wrapped;
        }

        // Stores the current speed adjustment active in gameplay.
        private readonly Track speedAdjustmentTrack = new TrackVirtual(0);

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        public DrawableManiaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            BarLines = new BarLineGenerator<BarLine>(Beatmap).BarLines;

            TimeRange.MinValue = 1;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Attach the engine-owned usage bridge before generated bar lines enter their real stage hosts. Direct
            // non-pooled visual objects begin usage during child loading, before this ruleset reaches LoadComplete.
            initialiseGameplaySkinEventBridge();

            foreach (var mod in Mods.OfType<IApplicableToTrack>())
                mod.ApplyToTrack(speedAdjustmentTrack);

            bool isForCurrentRuleset = Beatmap.BeatmapInfo.Ruleset.Equals(Ruleset.RulesetInfo);

            foreach (var p in ControlPoints)
            {
                // Mania doesn't care about global velocity
                p.Velocity = 1;
                p.BaseBeatLength *= Beatmap.Difficulty.SliderMultiplier;

                // For non-mania beatmap, speed changes should only happen through timing points
                if (!isForCurrentRuleset)
                    p.EffectPoint = new EffectControlPoint();
            }

            BarLines.ForEach(Playfield.Add);

            // Geometry and scrolling direction form one immutable publication. A configuration change is therefore
            // deliberately applied only to the next gameplay root rather than splitting this root across revisions.
            Direction.Value = LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;

            Config.BindWith(ManiaRulesetSetting.ScrollSpeed, configScrollSpeed);
            configScrollSpeed.BindValueChanged(speed =>
            {
                if (!AllowScrollSpeedAdjustment)
                    return;

                TargetTimeRange = ComputeScrollTime(speed.NewValue);
            });

            TimeRange.Value = TargetTimeRange = currentTimeRange = ComputeScrollTime(configScrollSpeed.Value);

            Config.BindWith(ManiaRulesetSetting.MobileLayout, mobileLayout);
            mobileLayout.BindValueChanged(_ => updateMobileLayout(), true);

            Config.BindWith(ManiaRulesetSetting.TouchOverlay, touchOverlay);
            touchOverlay.BindValueChanged(_ => updateMobileLayout(), true);

            // Host the shared converted-BMS keysound store under the gameplay clock so it resolves pause / seek.
            if (sharedKeysoundStore != null)
                AddInternal(sharedKeysoundStore);

            // The producer is engine-owned and frame-stable. Author scene consumers receive only its read-only stream.
            FrameStableComponents.Add(GameplaySkinEventRuntime);
            mountGameplaySkinSceneLayers(GameplaySkinSceneRuntime);
            FrameStableComponents.Add(GameplaySkinSceneRuntime);
        }

        private void mountGameplaySkinSceneLayers(GameplaySkinSceneRuntimeHost sceneRuntime)
        {
            GameplaySkinSceneRuntimeLayers layers = sceneRuntime.Layers;

            // Preserve the native mania playfield transform: background/underlay render behind it while authored
            // object/effect strata render above it. Native-geometry Note/LN/Key/BarLine visuals stay in their pools.
            layers.Background.Depth = 3;
            layers.Underlay.Depth = 2;
            layers.Object.Depth = -1;
            layers.GameplayEffects.Depth = -2;
            PlayfieldAdjustmentContainer.Add(layers.Background);
            PlayfieldAdjustmentContainer.Add(layers.Underlay);
            PlayfieldAdjustmentContainer.Add(layers.Object);
            PlayfieldAdjustmentContainer.Add(layers.GameplayEffects);

            // Mania's engine-owned overlay content is at depth zero. Stage foreground, BGA frame and decoration must
            // decorate above that content without receiving authority over gameplay or any BGA timeline.
            layers.Overlay.Depth = -1;
            layers.HudForeground.Depth = -2;
            Overlays.Add(layers.Overlay);
            Overlays.Add(layers.HudForeground);
            sceneRuntime.MarkLayersMounted();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            prewarmConvertedKeysounds();
            updateGameplaySkinInputEvents();
        }

        // Mirrors BMS-native DrawableBmsRuleset.PrewarmKeysounds (J6 / P1-J): every converted keysound will sound during
        // the play, so preload them all (decoding into the shared sample store / BASS) at load instead of paying the
        // first-play decode cost mid-gameplay. Runs for PLAYER MODE too, not just autoplay: a converted chart's full
        // keysound set (~hundreds of distinct WAVs) otherwise cold-decodes DURING gameplay, and the resulting transient
        // large buffers / promotion bursts were measured triggering blocking gen2 full GCs (~220ms freezes) in the first
        // ~30s of play (P1-J, 2026-06-11 probe). Preloading everything before gameplay is also what LR2/beatoraja do.
        // Two sources are warmed: (1) the standard Samples / NodeSamples (a converted KEY note carries its keysound
        // there for the store-absent fallback, and a hold node carries the head keysound); (2) the
        // IHasManiaKeysound.KeysoundSample of every converted object — required because BGM / scratch deliberately
        // carry EMPTY Samples (so a key press's GameplaySampleTriggerSource can't fire them; P1-J #11), so their
        // keysound is reachable only via KeysoundSample. Warming the underlying decode also warms the store playback
        // path, since the store and the playfield sample pool share the same decoded sample-store cache. Gated to
        // converted-BMS charts (store hosted), so normal mania play is unaffected.
        private void prewarmConvertedKeysounds()
        {
            if (sharedKeysoundStore == null)
                return;

            foreach (var hitObject in Beatmap.HitObjects)
            {
                foreach (var sample in hitObject.Samples)
                    Playfield.PrepareSamplePool(sample);

                if (hitObject is HoldNote hold && hold.NodeSamples != null)
                {
                    foreach (var nodeSamples in hold.NodeSamples)
                        foreach (var sample in nodeSamples)
                            Playfield.PrepareSamplePool(sample);
                }

                // BGM / scratch carry their keysound only here (empty Samples; see above); KEY notes expose theirs too.
                // Warm it so the store's first play of each unique keysound doesn't hit a cold decode mid-gameplay.
                if (hitObject is IHasManiaKeysound keysound && keysound.KeysoundSample != null)
                    Playfield.PrepareSamplePool(keysound.KeysoundSample);
            }
        }

        private ManiaTouchInputArea? touchInputArea;

        private void updateMobileLayout()
        {
            if (touchOverlay.Value)
                KeyBindingInputManager.Add(touchInputArea = new ManiaTouchInputArea(this));
            else
            {
                if (touchInputArea != null)
                    KeyBindingInputManager.Remove(touchInputArea, true);

                touchInputArea = null;
            }
        }

        protected override void AdjustScrollSpeed(int amount) => configScrollSpeed.Value += amount;

        protected override void Update()
        {
            base.Update();
            updateTimeRange();
            updateGameplaySkinInputEvents();
        }

        private void initialiseGameplaySkinEventBridge()
        {
            for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            {
                HitObject hitObject = Beatmap.HitObjects[i];
                registerGameplaySkinIdentityTree(hitObject);

                if (tryGetGameplaySkinObjectTarget(hitObject, null, out GameplaySkinLaneGroupId? groupId, out _, out _))
                    gameplaySkinObjectIds.TryAdd(new GameplaySkinObjectUsageKey(hitObject, groupId!), i);
            }

            nextGameplaySkinObjectId = Beatmap.HitObjects.Count;

            // Mania adds the same BarLine instance to every stage. Subscribe at the actual stage host so one shared
            // gameplay object becomes one stable read-only event carrier per production usage/group. The aggregate
            // playfield event intentionally lacks this source identity and therefore cannot represent dual-stage bars.
            for (int stageIndex = 0; stageIndex < Playfield.Stages.Count; stageIndex++)
            {
                Stage stage = Playfield.Stages[stageIndex];
                GameplaySkinLaneGroupId groupId = validateGameplaySkinStageTarget(stage, stageIndex);
                Action<HitObject> began = hitObject => onGameplaySkinObjectBegan(hitObject, groupId);
                Action<HitObject> finished = hitObject => onGameplaySkinObjectFinished(hitObject, groupId);
                stage.HitObjectUsageBegan += began;
                stage.HitObjectUsageFinished += finished;
                gameplaySkinStageUsageSubscriptions.Add(new GameplaySkinStageUsageSubscription(stage, began, finished));
            }

            foreach (BarLine barLine in BarLines)
            {
                for (int stageIndex = 0; stageIndex < Playfield.Stages.Count; stageIndex++)
                {
                    GameplaySkinLaneGroupId groupId = LayoutSnapshot.Context.Topology.GroupsInLogicalOrder[stageIndex].Identity.Id;
                    getGameplaySkinObjectId(new GameplaySkinObjectUsageKey(barLine, groupId));
                }
            }

            NewResult += onGameplaySkinJudgement;
            gameplaySkinLifecycleStarted = true;
        }

        private void updateGameplaySkinInputEvents()
        {
            if (!gameplaySkinLifecycleStarted)
                return;

            GameplaySkinLaneTopologySnapshot topology = LayoutSnapshot.Context.Topology;
            ManiaInputManager inputManager = (ManiaInputManager)KeyBindingInputManager;

            foreach (Stage stage in Playfield.Stages)
            {
                foreach (Column column in stage.Columns)
                {
                    GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[column.Index];
                    bool pressed = inputManager.KeyBindingContainer.PressedActions.Contains(column.Action.Value);

                    if (gameplaySkinPressedInputs.TryGetValue(lane.Identity.Id, out bool previous) && previous == pressed)
                        continue;

                    gameplaySkinPressedInputs[lane.Identity.Id] = pressed;
                    GameplaySkinEventRuntime.PublishInput(lane.Identity.Group.Id, lane.Identity.Id, pressed);
                }
            }
        }

        private void onGameplaySkinObjectBegan(HitObject hitObject, GameplaySkinLaneGroupId usageGroupId)
        {
            registerGameplaySkinIdentityTree(hitObject);
            hitObject = getGameplaySkinIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(hitObject, usageGroupId, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            var usageKey = new GameplaySkinObjectUsageKey(hitObject, groupId!);
            long objectId = getGameplaySkinObjectId(usageKey);

            if (gameplaySkinActiveObjects.ContainsKey(objectId))
                return;

            gameplaySkinCompletedObjectIds.Remove(objectId);

            GameplaySkinEventRuntime.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectSpawned,
                kind,
                GameplaySkinObjectState.Visible,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                getGameplaySkinObjectProgress(hitObject));

            gameplaySkinActiveObjects.Add(objectId, new GameplaySkinObjectUsage(hitObject, groupId!, laneId, kind));
        }

        private void onGameplaySkinObjectFinished(HitObject hitObject, GameplaySkinLaneGroupId usageGroupId)
        {
            hitObject = getGameplaySkinIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(hitObject, usageGroupId, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
                return;

            var usageKey = new GameplaySkinObjectUsageKey(hitObject, groupId!);
            long objectId = getGameplaySkinObjectId(usageKey);

            if (!gameplaySkinActiveObjects.ContainsKey(objectId))
                return;

            if (gameplaySkinCompletedObjectIds.Add(objectId))
            {
                GameplaySkinEventRuntime.PublishObject(
                    objectId,
                    GameplaySkinEventKind.ObjectStateChanged,
                    kind,
                    GameplaySkinObjectState.Completed,
                    groupId!,
                    laneId,
                    hitObject.StartTime,
                    hitObject.GetEndTime(),
                    1);
            }

            GameplaySkinEventRuntime.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectDespawned,
                kind,
                GameplaySkinObjectState.Despawned,
                groupId!,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                1);

            gameplaySkinActiveObjects.Remove(objectId);
        }

        private void onGameplaySkinJudgement(JudgementResult result)
        {
            HitObject identityOwner = getGameplaySkinIdentityOwner(result.HitObject);

            if (!tryMapJudgementGrade(result.Type, out GameplaySkinJudgementGrade grade)
                || !tryGetGameplaySkinObjectTarget(identityOwner, null, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId, out GameplaySkinObjectKind kind))
            {
                return;
            }

            var usageKey = new GameplaySkinObjectUsageKey(identityOwner, groupId!);
            long objectId = getGameplaySkinObjectId(usageKey);
            // A pre-roll/late-added drawable may produce its terminal result without ever entering the bounded
            // lifetime container on this epoch. Keep the real lane/group judgement edge, but never attach a stale ID:
            // object-targeted events are valid only while that exact engine usage is active in the stream.
            long? activeObjectId = gameplaySkinActiveObjects.ContainsKey(objectId) ? objectId : null;
            GameplaySkinEventRuntime.PublishJudgement(activeObjectId, groupId, laneId, grade, result.TimeOffset, result.HealthIncrease);

            // A hold's nested head/body/tail judgements all retain the sole parent ID. Their continuous state is
            // published by the real pooled hold owner, so a nested tick cannot overwrite Holding with Hit/Missed.
            if (identityOwner is HoldNote || activeObjectId == null)
                return;

            GameplaySkinEventRuntime.PublishObject(
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

        IEnumerable<GameplaySkinObjectStateSnapshot> IGameplaySkinEventObjectSnapshotSource.CreateGameplaySkinActiveObjectSnapshot(double gameplayTime)
        {
            GameplaySkinLaneTopologySnapshot topology = LayoutSnapshot.Context.Topology;
            var snapshots = new List<GameplaySkinObjectStateSnapshot>();
            var includedIds = new HashSet<long>();

            for (int stageIndex = 0; stageIndex < Playfield.Stages.Count; stageIndex++)
            {
                Stage stage = Playfield.Stages[stageIndex];
                GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[stageIndex];

                foreach (DrawableHitObject drawable in stage.HitObjectContainer.Objects)
                {
                    if (getGameplaySkinIdentityOwner(drawable.HitObject) is BarLine barLine)
                        addSnapshot(drawable, barLine, group.Identity.Id);
                }

                for (int localIndex = 0; localIndex < stage.Columns.Length; localIndex++)
                {
                    Column column = stage.Columns[localIndex];

                    foreach (DrawableHitObject drawable in column.HitObjectContainer.Objects)
                    {
                        HitObject owner = getGameplaySkinIdentityOwner(drawable.HitObject);

                        if (owner is ManiaHitObject)
                            addSnapshot(drawable, owner, group.Identity.Id);
                    }
                }
            }

            return snapshots;

            void addSnapshot(DrawableHitObject drawable, HitObject hitObject, GameplaySkinLaneGroupId groupId)
            {
                GameplaySkinObjectStateSnapshot snapshot = createGameplaySkinActiveObjectSnapshot(drawable, hitObject, groupId, gameplayTime);

                if (includedIds.Add(snapshot.ObjectId))
                    snapshots.Add(snapshot);
            }
        }

        private GameplaySkinObjectStateSnapshot createGameplaySkinActiveObjectSnapshot(
            DrawableHitObject drawable,
            HitObject hitObject,
            GameplaySkinLaneGroupId usageGroupId,
            double gameplayTime)
        {
            if (!tryGetGameplaySkinObjectTarget(hitObject, usageGroupId, out GameplaySkinLaneGroupId? groupId, out GameplaySkinLaneId? laneId,
                    out GameplaySkinObjectKind kind)
                || groupId == null)
            {
                throw new InvalidOperationException("An active mania drawable must retain one exact C3 gameplay-skin target.");
            }

            var usageKey = new GameplaySkinObjectUsageKey(hitObject, groupId);

            if (!gameplaySkinObjectIds.TryGetValue(usageKey, out long objectId))
                throw new InvalidOperationException("An active mania drawable must retain its sole pre-registered gameplay-skin object ID.");

            GameplaySkinObjectState state = drawable switch
            {
                DrawableHoldNote hold when hold.AllJudged && gameplayTime >= hitObject.GetEndTime() => GameplaySkinObjectState.Completed,
                DrawableHoldNote hold when hold.IsHolding.Value => GameplaySkinObjectState.Holding,
                DrawableHoldNote => gameplayTime < hitObject.StartTime ? GameplaySkinObjectState.Scheduled : GameplaySkinObjectState.Visible,
                _ when drawable.Judged => drawable.IsHit ? GameplaySkinObjectState.Hit : GameplaySkinObjectState.Missed,
                _ => gameplayTime < hitObject.StartTime ? GameplaySkinObjectState.Scheduled : GameplaySkinObjectState.Visible,
            };

            return new GameplaySkinObjectStateSnapshot(
                objectId,
                kind,
                state,
                groupId,
                laneId,
                hitObject.StartTime,
                hitObject.GetEndTime(),
                0);
        }

        private bool tryGetGameplaySkinObjectTarget(
            HitObject hitObject,
            GameplaySkinLaneGroupId? usageGroupId,
            out GameplaySkinLaneGroupId? groupId,
            out GameplaySkinLaneId? laneId,
            out GameplaySkinObjectKind kind)
        {
            hitObject = getGameplaySkinIdentityOwner(hitObject);
            groupId = null;
            laneId = null;
            kind = GameplaySkinObjectKind.Unspecified;

            if (hitObject is BarLine)
            {
                if (usageGroupId == null || !LayoutSnapshot.Context.Topology.TryGetGroup(usageGroupId, out _))
                    return false;

                groupId = usageGroupId;
                kind = GameplaySkinObjectKind.BarLine;
                return true;
            }

            if (hitObject is not ManiaHitObject maniaObject)
                return false;

            GameplaySkinLaneTopologySnapshot topology = LayoutSnapshot.Context.Topology;

            if (maniaObject.Column < 0 || maniaObject.Column >= topology.LanesInLogicalOrder.Count)
                return false;

            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[maniaObject.Column];
            groupId = lane.Identity.Group.Id;

            if (usageGroupId != null && usageGroupId != groupId)
                throw new InvalidOperationException("A production mania object usage cannot cross its exact C3 stage target.");

            laneId = lane.Identity.Id;
            kind = hitObject is HoldNote ? GameplaySkinObjectKind.LongNote : GameplaySkinObjectKind.Note;
            return true;
        }

        private void registerGameplaySkinIdentityTree(HitObject owner)
        {
            gameplaySkinIdentityOwners[owner] = owner;

            foreach (HitObject nested in owner.NestedHitObjects)
            {
                gameplaySkinIdentityOwners[nested] = owner;
                registerNestedGameplaySkinIdentity(owner, nested);
            }
        }

        private void registerNestedGameplaySkinIdentity(HitObject owner, HitObject nestedOwner)
        {
            foreach (HitObject nested in nestedOwner.NestedHitObjects)
            {
                gameplaySkinIdentityOwners[nested] = owner;
                registerNestedGameplaySkinIdentity(owner, nested);
            }
        }

        private HitObject getGameplaySkinIdentityOwner(HitObject hitObject)
            => gameplaySkinIdentityOwners.TryGetValue(hitObject, out HitObject? owner) ? owner : hitObject;

        private GameplaySkinLaneGroupId validateGameplaySkinStageTarget(Stage stage, int stageIndex)
        {
            GameplaySkinLaneTopologySnapshot topology = LayoutSnapshot.Context.Topology;

            if (stageIndex < 0 || stageIndex >= topology.GroupsInLogicalOrder.Count)
                throw new InvalidOperationException("A production mania stage event carrier requires an exact C3 topology group.");

            GameplaySkinLaneGroupId groupId = topology.GroupsInLogicalOrder[stageIndex].Identity.Id;

            if (!topology.TryGetGroup(groupId, out GameplaySkinLaneTopologyGroup? group)
                || group == null
                || group.LanesInLogicalOrder.Count != stage.Columns.Length
                || stage.Columns.Where((column, localIndex) =>
                    column.Index < 0
                    || column.Index >= topology.LanesInLogicalOrder.Count
                    || topology.LanesInLogicalOrder[column.Index].Identity.Group.Id != groupId
                    || topology.LanesInLogicalOrder[column.Index].GroupLocalLogicalIndex != localIndex).Any())
            {
                throw new InvalidOperationException("A production mania stage event carrier must match its exact C3 topology group and global column vector.");
            }

            return groupId;
        }

        private long getGameplaySkinObjectId(GameplaySkinObjectUsageKey usageKey)
        {
            if (gameplaySkinObjectIds.TryGetValue(usageKey, out long id))
                return id;

            id = nextGameplaySkinObjectId++;
            gameplaySkinObjectIds.Add(usageKey, id);
            return id;
        }

        long IManiaGameplaySkinObjectIdentityProvider.GetObjectId(HitObject hitObject, GameplaySkinLaneGroupId? usageGroupId)
        {
            ArgumentNullException.ThrowIfNull(hitObject);
            HitObject owner = getGameplaySkinIdentityOwner(hitObject);

            if (!tryGetGameplaySkinObjectTarget(owner, usageGroupId, out GameplaySkinLaneGroupId? groupId, out _, out _))
                throw new InvalidOperationException("A pooled mania scene visual requires one exact engine-owned object target.");

            return getGameplaySkinObjectId(new GameplaySkinObjectUsageKey(owner, groupId!));
        }

        void IManiaGameplaySkinObjectIdentityProvider.PublishLongNoteState(long objectId, GameplaySkinObjectState state)
        {
            if (state is not (GameplaySkinObjectState.Visible or GameplaySkinObjectState.Holding or GameplaySkinObjectState.Completed))
                throw new ArgumentOutOfRangeException(nameof(state), state, "A mania long-note owner may publish only visible, holding or completed state.");

            if (!gameplaySkinActiveObjects.TryGetValue(objectId, out GameplaySkinObjectUsage usage)
                || usage.Kind != GameplaySkinObjectKind.LongNote)
            {
                return;
            }

            if (state == GameplaySkinObjectState.Completed && !gameplaySkinCompletedObjectIds.Add(objectId))
                return;

            GameplaySkinEventRuntime.PublishObject(
                objectId,
                GameplaySkinEventKind.ObjectStateChanged,
                usage.Kind,
                state,
                usage.GroupId,
                usage.LaneId,
                usage.HitObject.StartTime,
                usage.HitObject.GetEndTime(),
                state == GameplaySkinObjectState.Completed ? 1 : getGameplaySkinObjectProgress(usage.HitObject));
        }

        private double getGameplaySkinObjectProgress(HitObject hitObject)
            => GameplaySkinEventRuntime.GetObjectProgress(hitObject.StartTime, hitObject.GetEndTime());

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

        private void updateTimeRange()
        {
            GameplaySkinLayoutSurface playfieldSurface = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.PLAYFIELD_SURFACE);
            GameplaySkinLayoutSurface hitTargetSurface = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE);
            float resolvedScrollLength = LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Down
                ? hitTargetSurface.Rect.Top - playfieldSurface.Rect.Top
                : playfieldSurface.Rect.Bottom - hitTargetSurface.Rect.Bottom;
            float defaultScrollLength = 1 - LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION / 768f;

            // This scaling factor preserves the scroll speed as the scroll length varies from changes to the hit position.
            float scale = resolvedScrollLength / defaultScrollLength;

            // we're intentionally using the game host's update clock here to decouple the time range tween from the gameplay clock (which can be arbitrarily paused, or even rewinding)
            currentTimeRange = Interpolation.DampContinuously(currentTimeRange, TargetTimeRange, 50, gameHost.UpdateThread.Clock.ElapsedFrameTime);
            TimeRange.Value = currentTimeRange * speedAdjustmentTrack.AggregateTempo.Value * speedAdjustmentTrack.AggregateFrequency.Value * scale;
        }

        /// <summary>
        /// Computes a scroll time (in milliseconds) from a scroll speed in the range of 1-40.
        /// </summary>
        /// <param name="scrollSpeed">The scroll speed.</param>
        /// <returns>The scroll time.</returns>
        public static double ComputeScrollTime(double scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new ManiaPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new ManiaPlayfield(Beatmap.Stages);

        public override int Variant => (int)(Beatmap.Stages.Count == 1 ? PlayfieldType.Single : PlayfieldType.Dual) + Beatmap.TotalColumns;

        protected override PassThroughInputManager CreateInputManager() => new ManiaInputManager(Ruleset.RulesetInfo, Variant);

        public override DrawableHitObject<ManiaHitObject>? CreateDrawableRepresentation(ManiaHitObject h)
        {
            if (h.GetType().Assembly.GetName().Name == bms_ruleset_assembly && tryCreateBmsDrawableRepresentation(h, out var drawableRepresentation))
                return drawableRepresentation;

            return null;
        }

        private static bool tryCreateBmsDrawableRepresentation(ManiaHitObject hitObject, out DrawableHitObject<ManiaHitObject>? drawableRepresentation)
        {
            drawableRepresentation = null;

            if (bms_can_create_drawable?.Invoke(hitObject) is not true)
                return false;

            if (bms_create_drawable?.Invoke(hitObject) is not DrawableHitObject<ManiaHitObject> createdDrawableRepresentation)
                return false;

            drawableRepresentation = createdDrawableRepresentation;
            return true;
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new ManiaFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new ManiaReplayRecorder(score);

        protected override ResumeOverlay CreateResumeOverlay() => new DelayedResumeOverlay();

        protected override void Dispose(bool isDisposing)
        {
            if (gameplaySkinLifecycleStarted)
            {
                foreach (GameplaySkinStageUsageSubscription subscription in gameplaySkinStageUsageSubscriptions)
                {
                    subscription.Stage.HitObjectUsageBegan -= subscription.Began;
                    subscription.Stage.HitObjectUsageFinished -= subscription.Finished;
                }

                gameplaySkinStageUsageSubscriptions.Clear();
                NewResult -= onGameplaySkinJudgement;
                gameplaySkinLifecycleStarted = false;
            }

            base.Dispose(isDisposing);
        }

        private readonly record struct GameplaySkinObjectUsageKey(HitObject HitObject, GameplaySkinLaneGroupId GroupId);

        private readonly record struct GameplaySkinObjectUsage(
            HitObject HitObject,
            GameplaySkinLaneGroupId GroupId,
            GameplaySkinLaneId? LaneId,
            GameplaySkinObjectKind Kind);

        private readonly record struct GameplaySkinStageUsageSubscription(
            Stage Stage,
            Action<HitObject> Began,
            Action<HitObject> Finished);

    }
}
