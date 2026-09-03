// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// How one exact resolved public material entry is consumed by the shared scene host.
    /// </summary>
    public enum GameplaySkinSceneHostRoute
    {
        Scene = 1,
        Semantic = 2,
        Specialised = 3,
        Suppressed = 4,
        Programmatic = 5,
    }

    /// <summary>
    /// Frozen render stratum. Layer drawables are mounted separately so an underlay can never cover gameplay objects.
    /// </summary>
    public enum GameplaySkinSceneLayer
    {
        Background = 1,
        Underlay = 2,
        Object = 3,
        GameplayEffects = 4,
        Overlay = 5,
        HudForeground = 6,
    }

    /// <summary>
    /// Fixed controller output mounted by a ruleset at the corresponding production playfield strata.
    /// </summary>
    public sealed class GameplaySkinSceneRuntimeLayers : IDisposable
    {
        public Container Background { get; } = create("gameplay-skin.background");

        public Container Underlay { get; } = create("gameplay-skin.underlay");

        public Container Object { get; } = create("gameplay-skin.object");

        public Container GameplayEffects { get; } = create("gameplay-skin.gameplay-effects");

        public Container Overlay { get; } = create("gameplay-skin.overlay");

        public Container HudForeground { get; } = create("gameplay-skin.hud-foreground");

        private bool disposed;

        internal Container Get(GameplaySkinSceneLayer layer) => layer switch
        {
            GameplaySkinSceneLayer.Background => Background,
            GameplaySkinSceneLayer.Underlay => Underlay,
            GameplaySkinSceneLayer.Object => Object,
            GameplaySkinSceneLayer.GameplayEffects => GameplayEffects,
            GameplaySkinSceneLayer.Overlay => Overlay,
            GameplaySkinSceneLayer.HudForeground => HudForeground,
            _ => throw new ArgumentOutOfRangeException(nameof(layer)),
        };

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Background.Dispose();
            Underlay.Dispose();
            Object.Dispose();
            GameplayEffects.Dispose();
            Overlay.Dispose();
            HudForeground.Dispose();
        }

        private static Container create(string name) => new Container
        {
            Name = name,
            RelativeSizeAxes = Axes.Both,
            Alpha = 0,
        };
    }

    /// <summary>
    /// Immutable capability truth for one exact catalog slot and C3 target.
    /// </summary>
    public sealed class GameplaySkinSceneHostedSlot
    {
        internal GameplaySkinPreparedHostedSlot PreparedRoute { get; }

        public GameplaySkinResolvedMaterialKey Key { get; }

        public GameplaySkinSceneHostRoute Route { get; private set; }

        public GameplaySkinSceneHostRoute FailureRoute => PreparedRoute.FailureRoute;

        public GameplaySkinSceneLayer Layer { get; }

        /// <summary>
        /// Maximum number of simultaneously prepared native visuals admitted for this exact key.
        /// Production pools use this immutable prepare-time value as both their warm size and hard ceiling.
        /// </summary>
        public int SpecialisedPoolCapacity => PreparedRoute.SpecialisedPoolCapacity;

        /// <summary>
        /// Exact C3 surface frozen by background scene preparation for this material key.
        /// Runtime consumers may project an existing native visual into this rectangle, but must not resolve geometry again.
        /// </summary>
        internal GameplaySkinLayoutRect PreparedRect => PreparedRoute.Rect;

        /// <summary>
        /// Whether the existing OmsSkin/ruleset visual remains the visible owner for this exact key.
        /// </summary>
        public bool AllowsProgrammaticVisual => Route == GameplaySkinSceneHostRoute.Programmatic
                                                || Route is GameplaySkinSceneHostRoute.Scene or GameplaySkinSceneHostRoute.Semantic or GameplaySkinSceneHostRoute.Specialised
                                                && !IsReplacementReady;

        public bool SuppressesProgrammaticVisual => Route == GameplaySkinSceneHostRoute.Suppressed
                                                    || Route is GameplaySkinSceneHostRoute.Scene or GameplaySkinSceneHostRoute.Semantic or GameplaySkinSceneHostRoute.Specialised
                                                    && IsReplacementReady;

        public bool IsReplacementReady { get; internal set; }

        /// <summary>
        /// Prepared author nodes routed to an existing specialised Note/LN/KeyVisual consumer without double rendering.
        /// </summary>
        public IReadOnlyList<GameplaySkinPreparedSceneNode> RoutedNodes { get; }

        internal GameplaySkinSceneHostedSlot(
            GameplaySkinPreparedHostedSlot preparedRoute)
        {
            PreparedRoute = preparedRoute ?? throw new ArgumentNullException(nameof(preparedRoute));
            Key = preparedRoute.Key;
            Route = preparedRoute.Route;
            Layer = preparedRoute.Layer;
            RoutedNodes = Array.AsReadOnly(preparedRoute.RoutedNodes.ToArray());
        }

        internal void UsePreparedFailureRoute()
        {
            Route = PreparedRoute.FailureRoute;
            IsReplacementReady = Route == GameplaySkinSceneHostRoute.Suppressed;
        }
    }

    /// <summary>
    /// Stable, value-free runtime diagnostic. One broken author node does not invalidate another node or gameplay.
    /// </summary>
    public sealed class GameplaySkinSceneRuntimeFault
    {
        public string Code { get; }

        internal GameplaySkinSceneRuntimeFault(string code) => Code = code;

        public override string ToString() => Code;
    }

    /// <summary>
    /// A runtime drawable projected directly from one immutable prepared node.
    /// </summary>
    public sealed class GameplaySkinSceneRuntimeNode
    {
        public string InstanceId { get; }

        public GameplaySkinLayoutRect Rect { get; }

        public Drawable RootDrawable { get; }

        public Drawable ContentDrawable { get; }

        /// <summary>
        /// The universal node-local transform which owns this node's visual and all declarative children.
        /// </summary>
        public Container TransformDrawable { get; }

        internal string RuntimeScopeId { get; }

        internal long? BoundObjectId { get; set; }

        internal GameplaySkinPreparedSceneNode PreparedNode { get; }

        internal GameplaySkinSceneRuntimeNode(
            GameplaySkinPreparedSceneNode preparedNode,
            Drawable rootDrawable,
            Drawable contentDrawable,
            Container transformDrawable,
            string? instanceId = null,
            string? runtimeScopeId = null)
        {
            PreparedNode = preparedNode;
            InstanceId = instanceId ?? preparedNode.InstanceId;
            Rect = preparedNode.Rect;
            RootDrawable = rootDrawable;
            ContentDrawable = contentDrawable;
            TransformDrawable = transformDrawable;
            RuntimeScopeId = runtimeScopeId ?? createPreparedScopeId(preparedNode);
        }

        private static string createPreparedScopeId(GameplaySkinPreparedSceneNode node)
            => $"prepared:{node.MaterialTarget}:{node.ResolvedTarget.Kind}:{node.ResolvedTarget.StableId ?? "none"}:{node.ResolvedTarget.Index?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
    }

    /// <summary>
    /// Shared C5 renderer for an already committed package/layout/material/scene publication.
    /// </summary>
    /// <remarks>
    /// This type owns no parser, package handle, resolver, resource preparer, event producer or geometry authority.
    /// It only projects the exact immutable publication and consumes the bounded read-only event stream.
    /// </remarks>
    public partial class GameplaySkinSceneRuntimeHost : CompositeDrawable
    {
        private const int max_runtime_faults = 128;

        private readonly GameplaySkinEventSubscription subscription;
        private readonly Action<GameplaySkinEventRecord> consumeEventAction;
        private readonly Queue<PendingNode> pendingNodes = new Queue<PendingNode>();
        private readonly Queue<PendingSemantic> pendingSemantics = new Queue<PendingSemantic>();
        private readonly Dictionary<string, GameplaySkinSceneRuntimeNode> runtimeNodes = new Dictionary<string, GameplaySkinSceneRuntimeNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<GameplaySkinSceneRuntimeNode>> runtimeNodesBySourceId = new Dictionary<string, List<GameplaySkinSceneRuntimeNode>>(StringComparer.Ordinal);
        private readonly Dictionary<GameplaySkinResolvedMaterialKey, Drawable> hostedDrawables = new Dictionary<GameplaySkinResolvedMaterialKey, Drawable>();
        private readonly Dictionary<GameplaySkinResolvedMaterialKey, SemanticVisual> semanticVisuals = new Dictionary<GameplaySkinResolvedMaterialKey, SemanticVisual>();
        private readonly Dictionary<GameplaySkinResolvedMaterialKey, GameplaySkinSceneHostedSlot> hostedSlotsByKey = new Dictionary<GameplaySkinResolvedMaterialKey, GameplaySkinSceneHostedSlot>();
        private readonly Dictionary<GameplaySkinResolvedMaterialKey, SceneOwnerBuild> sceneOwnerBuildsByKey = new Dictionary<GameplaySkinResolvedMaterialKey, SceneOwnerBuild>();
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinInputStateSnapshot> inputs = new Dictionary<GameplaySkinLaneId, GameplaySkinInputStateSnapshot>();
        private readonly Dictionary<long, GameplaySkinObjectStateSnapshot> objects = new Dictionary<long, GameplaySkinObjectStateSnapshot>();
        private readonly Dictionary<int, GameplaySkinBgaStateSnapshot> bga = new Dictionary<int, GameplaySkinBgaStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByLane = new Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByGroup = new Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot> judgementsByObject = new Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly List<long> objectJudgementsToRetire = new List<long>(GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME);
        private readonly List<GameplaySkinLaneId> expiredLaneJudgements = new List<GameplaySkinLaneId>(GameplaySkinEventBudgets.MAX_INPUT_STATES);
        private readonly List<GameplaySkinLaneGroupId> expiredGroupJudgements = new List<GameplaySkinLaneGroupId>(GameplaySkinEventBudgets.MAX_INPUT_STATES);
        private readonly Dictionary<string, string> stateMachineStates = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<StateMachineInstance> stateMachineInstances = new List<StateMachineInstance>();
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinInputStateSnapshot> firstInputByGroup = new Dictionary<GameplaySkinLaneGroupId, GameplaySkinInputStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinObjectStateSnapshot> firstObjectByLane = new Dictionary<GameplaySkinLaneId, GameplaySkinObjectStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinObjectStateSnapshot> firstObjectByGroup = new Dictionary<GameplaySkinLaneGroupId, GameplaySkinObjectStateSnapshot>();
        private readonly ObjectIdMinHeap objectIds = new ObjectIdMinHeap();
        private readonly Dictionary<GameplaySkinLaneId, ObjectIdMinHeap> objectIdsByLane = new Dictionary<GameplaySkinLaneId, ObjectIdMinHeap>();
        private readonly Dictionary<GameplaySkinLaneGroupId, ObjectIdMinHeap> objectIdsByGroup = new Dictionary<GameplaySkinLaneGroupId, ObjectIdMinHeap>();
        private readonly List<GameplaySkinSceneRuntimeFault> runtimeFaults = new List<GameplaySkinSceneRuntimeFault>();
        private readonly List<GameplaySkinSceneHostedSlot> hostedSlots = new List<GameplaySkinSceneHostedSlot>();
        private readonly List<RegisteredProgrammaticVisual> registeredProgrammaticVisuals = new List<RegisteredProgrammaticVisual>();

        private GameplaySkinLifecycleState lifecycleState;
        private GameplaySkinCurrentJudgementStateSnapshot? globalJudgement;
        private GameplaySkinScoreStateSnapshot scoreState;
        private GameplaySkinTimingStateSnapshot timingState;
        private GameplaySkinInputStateSnapshot? firstInput;
        private GameplaySkinObjectStateSnapshot? firstObject;
        private GameplaySkinBgaStateSnapshot? firstBga;
        private int runtimeEffectCount;
        private int runtimeTextGlyphs;
        private GameplaySkinSceneStateFamily bindingStateFamiliesDirty = GameplaySkinSceneStateFamily.All;
        private GameplaySkinSceneStateFamily semanticStateFamiliesDirty = GameplaySkinSceneStateFamily.All;
        private bool stateMachinesDirty;
        private bool layersMounted;
        private bool sceneReadyPublished;
        private bool disposed;

        public GameplaySkinLayoutPublication Publication { get; }

        public GameplaySkinPreparedScene PreparedScene => Publication.PreparedScene;

        public GameplaySkinResolvedMaterialSet MaterialSet => Publication.MaterialSet;

        public GameplaySkinEventStream EventStream { get; }

        /// <summary>
        /// Fixed, separately mountable draw strata. The controller itself is update-only and never flattens these into one overlay.
        /// </summary>
        public GameplaySkinSceneRuntimeLayers Layers { get; } = new GameplaySkinSceneRuntimeLayers();

        public IReadOnlyList<GameplaySkinSceneHostedSlot> HostedSlots => hostedSlots.AsReadOnly();

        public IReadOnlyList<GameplaySkinSlotDescriptor> HostedSlotDescriptors { get; }

        public GameplaySkinRuntimeCapabilitySet RuntimeCapabilities { get; }

        public IReadOnlyDictionary<string, string> StateMachineStates => new ReadOnlyDictionary<string, string>(stateMachineStates);

        public IReadOnlyList<GameplaySkinSceneRuntimeFault> RuntimeFaults => runtimeFaults.AsReadOnly();

        public int PendingCreationCount => pendingNodes.Count + pendingSemantics.Count;

        public int CreatedThisFrame { get; private set; }

        public int RuntimeNodeCount => runtimeNodes.Count;

        public int RuntimeInstanceCount { get; private set; }

        internal int StateMachineProjectionPassCount { get; private set; }

        internal long BindingApplicationCount { get; private set; }

        internal long VariantApplicationCount { get; private set; }

        internal long SemanticStateApplicationCount { get; private set; }

        public bool IsSceneReady => pendingNodes.Count == 0 && pendingSemantics.Count == 0;

        public long CurrentEpoch { get; private set; } = -1;

        public long LastSequence { get; private set; } = -1;

        public double LastGameplayTime { get; private set; }

        public GameplaySkinSceneRuntimeHost(GameplaySkinLayoutPublication publication, GameplaySkinEventStream eventStream)
        {
            Publication = publication ?? throw new ArgumentNullException(nameof(publication));
            EventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));

            if (!ReferenceEquals(eventStream.Publication, publication)
                || eventStream.CurrentRevision != publication.EventRevision)
                throw new ArgumentException("The scene renderer requires the event stream for its exact committed publication.", nameof(eventStream));

            subscription = eventStream.Subscribe();
            consumeEventAction = consumeEvent;
            RelativeSizeAxes = Axes.Both;

            GameplaySkinEventStateSnapshot initialState = PreparedScene.InitialEventState;
            lifecycleState = initialState.LifecycleState;
            scoreState = initialState.Score;
            timingState = initialState.Timing;

            foreach (GameplaySkinInputStateSnapshot input in initialState.Inputs)
                inputs.Add(input.LaneId, input);

            foreach (GameplaySkinBgaStateSnapshot viewport in initialState.BgaViewports)
                bga.Add(viewport.ViewportIndex, viewport);

            rebuildInputIndexes();
            rebuildObjectIndexes();
            rebuildBgaIndex();

            initialiseStateMachines();
            initialiseHostedSlots();

            foreach (GameplaySkinPreparedSceneNode root in PreparedScene.Roots)
                pendingNodes.Enqueue(new PendingNode(root, null, null, null, true));

            HostedSlotDescriptors = Array.AsReadOnly(hostedSlots.Select(slot => slot.Key.Slot).Distinct().ToArray());
            RuntimeCapabilities = GameplaySkinRuntimeCapabilitySet.Create(HostedSlotDescriptors
                .Where(MaterialSet.RuntimeSupportProfile.IsSupported)
                .Select(descriptor =>
                GameplaySkinRuntimeSlotSupport.Create(
                    descriptor,
                    GameplaySkinRuntimeSlotCapability.Provide
                    | (descriptor.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed
                        ? GameplaySkinRuntimeSlotCapability.Suppress
                        : GameplaySkinRuntimeSlotCapability.None))));

            // Accept the already background-prepared attach snapshot before the production event host announces the
            // exact committed publication in LoadComplete. This preserves a contiguous Snapshot -> publication Reset
            // sequence even when both hosts are constructed before either is mounted into the gameplay tree.
            if (subscription.DrainProductionFrame(consumeEventAction, 1) != 1)
                throw new InvalidOperationException("A gameplay skin scene consumer must attach with one complete prepared snapshot.");
        }

        protected override void Update()
        {
            base.Update();
            ProcessFrame();
        }

        internal void ProcessFrame()
        {
            if (disposed)
                return;

            subscription.DrainProductionFrame(consumeEventAction);
            EventStream.ReadConsumerTimingHighWater(
                subscription,
                LastGameplayTime,
                timingState,
                out double gameplayTime,
                out GameplaySkinTimingStateSnapshot currentTiming);

            if (!timingEquals(timingState, currentTiming))
            {
                timingState = currentTiming;
                markStateFamilyDirty(GameplaySkinSceneStateFamily.Timing);
            }

            if (expireTransientJudgements(gameplayTime))
            {
                markStateFamilyDirty(GameplaySkinSceneStateFamily.Judgement);
                stateMachinesDirty = true;
            }

            // A frame may contain up to the fixed edge-drain budget. Fold all of those immutable edges first, then
            // perform one canonical projection so dense 14K traffic is O(instances), never O(edges * instances).
            if (stateMachinesDirty)
            {
                resetStateMachines();
                stateMachinesDirty = false;
            }

            CreatedThisFrame = 0;

            while (CreatedThisFrame < GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME
                   && (pendingNodes.Count > 0 || pendingSemantics.Count > 0))
            {
                if (pendingNodes.Count > 0)
                    createNode(pendingNodes.Dequeue());
                else
                    createSemantic(pendingSemantics.Dequeue());

                CreatedThisFrame++;
            }

            sampleTracks(gameplayTime);

            GameplaySkinSceneStateFamily bindingFamilies = bindingStateFamiliesDirty;
            bindingStateFamiliesDirty = GameplaySkinSceneStateFamily.None;

            if (bindingFamilies != GameplaySkinSceneStateFamily.None)
            {
                applyBindings(bindingFamilies);
                applyVariants(bindingFamilies);
            }

            GameplaySkinSceneStateFamily semanticFamilies = semanticStateFamiliesDirty;
            semanticStateFamiliesDirty = GameplaySkinSceneStateFamily.None;
            updateSemanticState(gameplayTime, semanticFamilies);

            // Keep a judged object's exact result through the frame which consumes its despawn edge so pooled
            // scene variants/bindings and state machines observe the same terminal state. Retire only after all
            // consumers sampled that frame; the list is bounded by the stream's per-frame drain budget.
            bool retiredObjectJudgement = objectJudgementsToRetire.Count > 0;

            foreach (long objectId in objectJudgementsToRetire)
                judgementsByObject.Remove(objectId);

            objectJudgementsToRetire.Clear();

            if (retiredObjectJudgement)
            {
                markStateFamilyDirty(GameplaySkinSceneStateFamily.Judgement);
                stateMachinesDirty = true;
            }

            if (IsSceneReady && !sceneReadyPublished)
            {
                sceneReadyPublished = true;
                setLayerVisibility(1);
            }
        }

        internal bool TryGetRuntimeNode(string instanceId, out GameplaySkinSceneRuntimeNode? node)
        {
            ArgumentException.ThrowIfNullOrEmpty(instanceId);
            return runtimeNodes.TryGetValue(instanceId, out node);
        }

        internal bool TryGetHostedDrawable(GameplaySkinResolvedMaterialKey key, out Drawable? drawable)
        {
            ArgumentNullException.ThrowIfNull(key);
            return hostedDrawables.TryGetValue(key, out drawable);
        }

        /// <summary>
        /// Gets the exact replacement/suppression gate a production ruleset applies to its legacy/programmatic visual.
        /// </summary>
        public bool TryGetVisualGate(GameplaySkinResolvedMaterialKey key, out GameplaySkinSceneHostedSlot? gate)
        {
            ArgumentNullException.ThrowIfNull(key);
            return hostedSlotsByKey.TryGetValue(key, out gate);
        }

        /// <summary>
        /// Transfers lifetime ownership of the six fixed layer containers to their production ruleset parents.
        /// The update-only controller will then dispose only its subscription and specialised handles.
        /// </summary>
        public void MarkLayersMounted() => layersMounted = true;

        /// <summary>
        /// Registers the existing exact-key programmatic/legacy wrapper. It stays visible while the author scene is
        /// being built and is atomically hidden only after a replacement or authorised suppression is ready.
        /// </summary>
        public IDisposable RegisterProgrammaticVisual(GameplaySkinResolvedMaterialKey key, Drawable wrapper)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(wrapper);

            if (!hostedSlotsByKey.TryGetValue(key, out GameplaySkinSceneHostedSlot? gate))
                throw new ArgumentException("The visual key is not part of this exact publication.", nameof(key));

            var registration = new RegisteredProgrammaticVisual(this, new[] { gate }, Array.Empty<GameplaySkinSceneHostedSlot>(), wrapper);
            registeredProgrammaticVisuals.Add(registration);
            registration.Refresh(IsSceneReady);
            return registration;
        }

        /// <summary>
        /// Registers one exact visual partition which is replaced by any one of a fixed set of keys. This is used
        /// only when the partition has two independent author scopes, such as global and stage-local text HUD.
        /// </summary>
        public IDisposable RegisterProgrammaticVisual(
            IEnumerable<GameplaySkinResolvedMaterialKey> keys,
            Drawable wrapper)
        {
            ArgumentNullException.ThrowIfNull(keys);
            ArgumentNullException.ThrowIfNull(wrapper);

            GameplaySkinResolvedMaterialKey[] copiedKeys = keys.ToArray();

            if (copiedKeys.Length == 0)
                throw new ArgumentException("A partitioned programmatic visual requires at least one exact key.", nameof(keys));

            if (copiedKeys.Any(key => key == null))
                throw new ArgumentException("A partitioned programmatic visual cannot contain a null key.", nameof(keys));

            if (copiedKeys.Distinct().Count() != copiedKeys.Length)
                throw new ArgumentException("A partitioned programmatic visual requires unique exact keys.", nameof(keys));

            var gates = new GameplaySkinSceneHostedSlot[copiedKeys.Length];

            for (int i = 0; i < copiedKeys.Length; i++)
            {
                if (!hostedSlotsByKey.TryGetValue(copiedKeys[i], out GameplaySkinSceneHostedSlot? gate))
                    throw new ArgumentException("A partitioned visual key is not part of this exact publication.", nameof(keys));

                gates[i] = gate;
            }

            var registration = new RegisteredProgrammaticVisual(this, gates, Array.Empty<GameplaySkinSceneHostedSlot>(), wrapper);
            registeredProgrammaticVisuals.Add(registration);
            registration.Refresh(IsSceneReady);
            return registration;
        }

        /// <summary>
        /// Registers an exact residual compatibility partition. It is replaced when any global gate is ready, or when
        /// every stage gate is ready. This keeps partial stage authoring local while removing the final compatibility
        /// remainder once the complete stage vector has an author-owned replacement.
        /// </summary>
        internal IDisposable RegisterResidualProgrammaticVisual(
            IEnumerable<GameplaySkinResolvedMaterialKey> anyKeys,
            IEnumerable<GameplaySkinResolvedMaterialKey> allKeys,
            Drawable wrapper)
        {
            ArgumentNullException.ThrowIfNull(anyKeys);
            ArgumentNullException.ThrowIfNull(allKeys);
            ArgumentNullException.ThrowIfNull(wrapper);

            GameplaySkinResolvedMaterialKey[] copiedAnyKeys = anyKeys.ToArray();
            GameplaySkinResolvedMaterialKey[] copiedAllKeys = allKeys.ToArray();
            GameplaySkinResolvedMaterialKey[] copiedKeys = copiedAnyKeys.Concat(copiedAllKeys).ToArray();

            if (copiedAllKeys.Length == 0)
                throw new ArgumentException("A residual programmatic visual requires at least one exact stage key.", nameof(allKeys));

            if (copiedKeys.Any(key => key == null))
                throw new ArgumentException("A residual programmatic visual cannot contain a null key.");

            if (copiedKeys.Distinct().Count() != copiedKeys.Length)
                throw new ArgumentException("A residual programmatic visual requires unique exact keys.");

            GameplaySkinSceneHostedSlot[] resolve(GameplaySkinResolvedMaterialKey[] keys, string parameterName)
            {
                var gates = new GameplaySkinSceneHostedSlot[keys.Length];

                for (int i = 0; i < keys.Length; i++)
                {
                    if (!hostedSlotsByKey.TryGetValue(keys[i], out GameplaySkinSceneHostedSlot? gate))
                        throw new ArgumentException("A residual visual key is not part of this exact publication.", parameterName);

                    gates[i] = gate;
                }

                return gates;
            }

            var registration = new RegisteredProgrammaticVisual(
                this,
                resolve(copiedAnyKeys, nameof(anyKeys)),
                resolve(copiedAllKeys, nameof(allKeys)),
                wrapper);
            registeredProgrammaticVisuals.Add(registration);
            registration.Refresh(IsSceneReady);
            return registration;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (!disposed)
            {
                disposed = true;

                if (isDisposing)
                {
                    for (int index = registeredProgrammaticVisuals.Count - 1; index >= 0; index--)
                        registeredProgrammaticVisuals[index].Dispose();

                    subscription.Dispose();
                    if (!layersMounted)
                        Layers.Dispose();
                }
            }

            base.Dispose(isDisposing);
        }

        private void initialiseHostedSlots()
        {
            foreach (GameplaySkinPreparedHostedSlot preparedRoute in PreparedScene.HostedSlots)
            {
                var hosted = new GameplaySkinSceneHostedSlot(preparedRoute)
                {
                    IsReplacementReady = preparedRoute.Route == GameplaySkinSceneHostRoute.Suppressed
                };
                hostedSlots.Add(hosted);
                hostedSlotsByKey.Add(preparedRoute.Key, hosted);

                if (preparedRoute.Route == GameplaySkinSceneHostRoute.Scene)
                {
                    int expectedNodes = preparedRoute.RoutedNodes.Sum(countPreparedNodes);

                    if (expectedNodes <= 0)
                        throw new InvalidOperationException("A prepared scene route must retain at least one owning node.");

                    sceneOwnerBuildsByKey.Add(preparedRoute.Key, new SceneOwnerBuild(hosted, expectedNodes));
                }

                if (preparedRoute.Route == GameplaySkinSceneHostRoute.Semantic
                    && preparedRoute.Entry.Material is GameplaySkinPublicSlotMaterial material)
                    pendingSemantics.Enqueue(new PendingSemantic(preparedRoute, material));
            }
        }

        private void setLayerVisibility(float alpha)
        {
            Layers.Background.Alpha = alpha;
            Layers.Underlay.Alpha = alpha;
            Layers.Object.Alpha = alpha;
            Layers.GameplayEffects.Alpha = alpha;
            Layers.Overlay.Alpha = alpha;
            Layers.HudForeground.Alpha = alpha;

            foreach (RegisteredProgrammaticVisual registration in registeredProgrammaticVisuals)
                registration.Refresh(IsSceneReady);

            refreshSpecialisedVisuals();
        }

        private void unregister(RegisteredProgrammaticVisual registration)
            => registeredProgrammaticVisuals.Remove(registration);

        private void createNode(PendingNode pending)
        {
            GameplaySkinPreparedSceneNode prepared = pending.Node;
            int instanceCountBefore = RuntimeInstanceCount;
            int effectCountBefore = runtimeEffectCount;
            int textGlyphCountBefore = runtimeTextGlyphs;
            Container? attachedParent = null;
            Drawable? content = null;
            Drawable? root = null;
            GameplaySkinSceneRuntimeNode? runtime = null;
            SceneOwnerBuild? owner = prepared.OwningSlotKey != null
                && sceneOwnerBuildsByKey.TryGetValue(prepared.OwningSlotKey, out SceneOwnerBuild? preparedOwner)
                ? preparedOwner
                : null;

            if (owner?.Failed == true)
                return;

            try
            {
                GameplaySkinSceneHostedSlot? gate = null;

                if (prepared.Slot != null && prepared.MaterialTarget != null)
                {
                    var key = new GameplaySkinResolvedMaterialKey(prepared.Slot, prepared.MaterialTarget);

                    if (!hostedSlotsByKey.TryGetValue(key, out gate))
                        throw new InvalidOperationException();

                    if (gate.Route is GameplaySkinSceneHostRoute.Specialised or GameplaySkinSceneHostRoute.Suppressed)
                        return;
                }

                if (RuntimeInstanceCount >= GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES)
                    throw new InvalidOperationException();

                GameplaySkinSceneLayer layer = prepared.Layer;
                bool crossesLayer = pending.ParentLayer.HasValue && pending.ParentLayer.Value != layer;

                if (crossesLayer && !pending.ParentAllowsLayerDispatch)
                    throw new InvalidOperationException();

                Container parent = crossesLayer || pending.Parent == null ? Layers.Get(layer) : pending.Parent;
                GameplaySkinLayoutRect? parentRect = crossesLayer ? null : pending.ParentRect;
                content = createNodeContent(prepared);
                Container transform = createNodeTransform(prepared, content);
                applyNodeProperties(transform, content, prepared.Source.Properties);
                Drawable effected = wrapEffects(transform, prepared.Source.Effects);
                root = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = effected,
                    Depth = (prepared.Slot == null ? 0 : GameplaySkinSceneHostPolicy.BaseDepthFor(prepared.Slot))
                            + (float)getNumber(prepared.Source.Properties, "z", 0)
                };
                applyGeometryAndBlend(root, prepared.Rect, parentRect, prepared.Source.Blend);
                parent.Add(root);
                attachedParent = parent;

                runtime = new GameplaySkinSceneRuntimeNode(prepared, root, content, transform);
                runtimeNodes.Add(runtime.InstanceId, runtime);
                RuntimeInstanceCount++;

                if (!runtimeNodesBySourceId.TryGetValue(prepared.Source.Id, out List<GameplaySkinSceneRuntimeNode>? bySource))
                    runtimeNodesBySourceId.Add(prepared.Source.Id, bySource = new List<GameplaySkinSceneRuntimeNode>());

                bySource.Add(runtime);
                registerStateMachineScopes(runtime);
                bindingStateFamiliesDirty |= GameplaySkinSceneStateFamily.All;
                Container childParent = transform;
                bool dispatcher = prepared.Source.Type == GameplaySkinSceneNodeType.Container
                                  && prepared.AllowsLayerDispatch;
                enqueueChildren(pending, childParent, prepared.Rect, layer, dispatcher);

                if (owner != null)
                {
                    owner.Add(runtime, prepared.Slot != null ? attachedParent : null, prepared.Slot != null ? root : null,
                        runtimeEffectCount - effectCountBefore, runtimeTextGlyphs - textGlyphCountBefore);

                    if (owner.CompletedNodes == owner.ExpectedNodes)
                    {
                        if (owner.FirstRoot != null)
                            hostedDrawables[owner.Gate.Key] = owner.FirstRoot;

                        owner.Gate.IsReplacementReady = true;
                    }
                }
            }
            catch
            {
                if (runtime != null)
                {
                    runtimeNodes.Remove(runtime.InstanceId);

                    if (runtimeNodesBySourceId.TryGetValue(runtime.PreparedNode.Source.Id, out List<GameplaySkinSceneRuntimeNode>? bySource))
                    {
                        bySource.Remove(runtime);

                        if (bySource.Count == 0)
                            runtimeNodesBySourceId.Remove(runtime.PreparedNode.Source.Id);
                    }
                }

                RuntimeInstanceCount = instanceCountBefore;
                runtimeEffectCount = effectCountBefore;
                runtimeTextGlyphs = textGlyphCountBefore;

                if (root != null && attachedParent != null)
                    attachedParent.Remove(root, true);
                else
                    (root ?? content)?.Dispose();

                rebuildStateMachineScopes();
                addFault("OMS-SKIN-SCENE-RUNTIME-001");

                if (owner != null)
                    failSceneOwner(owner);
                else
                    queueSemanticFallback(prepared);
            }
        }

        private void failSceneOwner(SceneOwnerBuild owner)
        {
            if (owner.Failed)
                return;

            owner.Failed = true;

            // A runtime program fault belongs to the exact prepared slot, not just the clone which happened to
            // observe it first. Retire every prebuilt native clone for that key before exposing the immutable
            // prepared failure route; unrelated slots and gameplay state remain mounted.
            GameplaySkinSpecialisedSceneVisual[] failedSpecialised = specialisedVisuals
                                                                     .Where(visual => visual.Key.Equals(owner.Gate.Key))
                                                                     .ToArray();

            foreach (GameplaySkinSpecialisedSceneVisual visual in failedSpecialised)
                visual.Dispose();

            foreach ((Container parent, Drawable root) in owner.Roots)
            {
                try
                {
                    parent.Remove(root, true);
                }
                catch
                {
                    // A scene fault remains isolated even if a framework drawable was already externally retired.
                }
            }

            foreach (GameplaySkinSceneRuntimeNode node in owner.Nodes)
            {
                runtimeNodes.Remove(node.InstanceId);

                if (runtimeNodesBySourceId.TryGetValue(node.PreparedNode.Source.Id, out List<GameplaySkinSceneRuntimeNode>? bySource))
                {
                    bySource.Remove(node);

                    if (bySource.Count == 0)
                        runtimeNodesBySourceId.Remove(node.PreparedNode.Source.Id);
                }
            }

            RuntimeInstanceCount = Math.Max(0, RuntimeInstanceCount - owner.Nodes.Count);
            runtimeEffectCount = Math.Max(0, runtimeEffectCount - owner.RuntimeEffectCost);
            runtimeTextGlyphs = Math.Max(0, runtimeTextGlyphs - owner.RuntimeTextGlyphCost);
            hostedDrawables.Remove(owner.Gate.Key);
            owner.Gate.IsReplacementReady = false;
            owner.Gate.UsePreparedFailureRoute();

            if (owner.Gate.Route == GameplaySkinSceneHostRoute.Semantic
                && owner.Gate.PreparedRoute.Entry.Material is GameplaySkinPublicSlotMaterial material)
            {
                pendingSemantics.Enqueue(new PendingSemantic(owner.Gate.PreparedRoute, material));
            }

            rebuildStateMachineScopes();
            markStateFamilyDirty(GameplaySkinSceneStateFamily.All);
            refreshRegisteredProgrammaticVisuals();
        }

        private void handleRuntimeNodeFault(GameplaySkinSceneRuntimeNode node, string code)
        {
            addFault(code);

            if (node.PreparedNode.OwningSlotKey is GameplaySkinResolvedMaterialKey bgaKey
                && (ReferenceEquals(bgaKey.Slot, GameplaySkinSlotCatalog.BgaViewport)
                    || ReferenceEquals(bgaKey.Slot, GameplaySkinSlotCatalog.BgaFrame)))
            {
                GameplaySkinSpecialisedSceneVisual? failedViewport = specialisedVisuals
                    .FirstOrDefault(visual => visual.RuntimeNodes.Contains(node));

                if (failedViewport != null)
                {
                    // BGA material authority is global, but each explicit target index is a separate engine-owned
                    // viewport surface. Keep the failed clone reserved and hidden so its native owner can recover
                    // locally without invalidating the other prepared indices or constructing a replacement.
                    failedViewport.MarkFailed();
                    rebuildStateMachineScopes();
                    markStateFamilyDirty(GameplaySkinSceneStateFamily.All);
                    return;
                }
            }

            if (node.PreparedNode.OwningSlotKey != null
                && sceneOwnerBuildsByKey.TryGetValue(node.PreparedNode.OwningSlotKey, out SceneOwnerBuild? owner))
            {
                failSceneOwner(owner);
                return;
            }

            if (node.PreparedNode.OwningSlotKey != null
                && hostedSlotsByKey.TryGetValue(node.PreparedNode.OwningSlotKey, out GameplaySkinSceneHostedSlot? gate)
                && gate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                failSpecialisedKey(node.PreparedNode.OwningSlotKey);
                return;
            }

            // Ownerless structural nodes have no catalog fallback. Isolate only that declarative subtree.
            node.RootDrawable.Alpha = 0;
        }

        private void enqueueChildren(
            PendingNode pending,
            Container? parent,
            GameplaySkinLayoutRect? parentRect,
            GameplaySkinSceneLayer? parentLayer,
            bool parentAllowsLayerDispatch)
        {
            foreach (GameplaySkinPreparedSceneNode child in pending.Node.Children)
                pendingNodes.Enqueue(new PendingNode(child, parent, parentRect, parentLayer, parentAllowsLayerDispatch));
        }

        private Drawable createNodeContent(GameplaySkinPreparedSceneNode prepared)
        {
            return prepared.Source.Type switch
            {
                GameplaySkinSceneNodeType.Sprite => new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = prepared.ResolvedTexture,
                },
                GameplaySkinSceneNodeType.Container => new Container { RelativeSizeAxes = Axes.Both },
                GameplaySkinSceneNodeType.Text => createText(prepared),
                // V1 Mask is a resource-free, allowlisted elliptical shape stencil. Clip is the distinct
                // rectangular/rounded-bounds stencil. Neither node admits arbitrary author shaders.
                GameplaySkinSceneNodeType.Mask => new GameplaySkinShapeMaskContainer { RelativeSizeAxes = Axes.Both },
                GameplaySkinSceneNodeType.Clip => new Container { RelativeSizeAxes = Axes.Both, Masking = true },
                _ => throw new InvalidOperationException(),
            };
        }

        private static Container createNodeTransform(GameplaySkinPreparedSceneNode prepared, Drawable content)
        {
            if (content is Container container
                && prepared.Source.Type is GameplaySkinSceneNodeType.Container or GameplaySkinSceneNodeType.Mask or GameplaySkinSceneNodeType.Clip)
            {
                return container;
            }

            if (content is Sprite)
                content.RelativeSizeAxes = Axes.Both;

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = content,
            };
        }

        private SpriteText createText(GameplaySkinPreparedSceneNode prepared)
        {
            GameplaySkinSceneNode source = prepared.Source;
            string text = getString(source.Properties, "text", string.Empty);
            int reservation = PreparedScene.GetTextGlyphReservation(source.Id);

            if (reservation < text.Length
                || runtimeTextGlyphs + reservation > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPHS)
                throw new InvalidOperationException();

            runtimeTextGlyphs += reservation;
            return new OsuSpriteText
            {
                Text = text,
                Font = FontUsage.Default.With(size: (float)getNumber(source.Properties, "font-size", 16)),
            };
        }

        private Drawable wrapEffects(Drawable content, IReadOnlyList<GameplaySkinSceneEffect> effects)
        {
            Drawable current = content;

            try
            {
                foreach (GameplaySkinSceneEffect effect in effects)
                {
                    if (++runtimeEffectCount > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_EFFECT_INSTANCES)
                        throw new InvalidOperationException();

                    switch (effect.Type)
                    {
                        case "blur":
                            current = new BufferedContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                BlurSigma = new Vector2((float)getNumber(effect.Properties, "radius", 0)),
                                DrawOriginal = true,
                                Child = current,
                            };
                            break;

                        case "glow":
                        {
                            Color4 effectColour = colour(getString(effect.Properties, "colour", "#ffffffff"));
                            float strength = (float)getNumber(effect.Properties, "strength", 1);
                            effectColour.A = (byte)Math.Clamp((int)Math.Round(effectColour.A * strength), 0, 255);
                            current = new BufferedContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                BlurSigma = new Vector2((float)getNumber(effect.Properties, "radius", 0)),
                                DrawOriginal = true,
                                EffectColour = effectColour,
                                EffectBlending = BlendingParameters.Additive,
                                Child = current,
                            };
                            break;
                        }

                        case "outline":
                            current = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                EdgeEffect = new EdgeEffectParameters
                                {
                                    Type = EdgeEffectType.Glow,
                                    Radius = (float)getNumber(effect.Properties, "width", 0),
                                    Colour = colour(getString(effect.Properties, "colour", "#ffffffff")),
                                    Hollow = true,
                                },
                                Child = current,
                            };
                            break;

                        case "shadow":
                            current = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                EdgeEffect = new EdgeEffectParameters
                                {
                                    Type = EdgeEffectType.Shadow,
                                    Radius = (float)getNumber(effect.Properties, "blur", 0),
                                    Offset = new Vector2(
                                        (float)getNumber(effect.Properties, "x", 0),
                                        (float)getNumber(effect.Properties, "y", 0)),
                                    Colour = colour(getString(effect.Properties, "colour", "#80000000")),
                                },
                                Child = current,
                            };
                            break;

                        default:
                            throw new InvalidOperationException();
                    }
                }

                return current;
            }
            catch
            {
                // A later effect can fail after earlier wrappers have already taken ownership of the content.
                // Dispose the outermost provisional wrapper here so no unattached drawable tree survives the
                // local slot fault. Callers still restore their numeric budgets transactionally.
                try
                {
                    current.Dispose();
                }
                catch
                {
                    // Preserve the stable author-content fault; disposal failures cannot promote one visual fault
                    // into gameplay failure. The exact publication still owns and retires all prepared resources.
                }

                throw;
            }
        }

        private void createSemantic(PendingSemantic pending)
        {
            int instanceCountBefore = RuntimeInstanceCount;
            int textGlyphCountBefore = runtimeTextGlyphs;
            Container? visual = null;
            Container? attachedParent = null;

            try
            {
                if (pending.Material.IsProgrammaticFallback)
                    return;

                if (RuntimeInstanceCount >= GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES)
                    throw new InvalidOperationException();

                GameplaySkinResolvedMaterialEntry entry = pending.PreparedRoute.Entry;
                GameplaySkinLayoutRect rect = pending.PreparedRoute.Rect;
                visual = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = GameplaySkinSceneHostPolicy.BaseDepthFor(entry.Slot),
                };
                var sprite = new Sprite { RelativeSizeAxes = Axes.Both, Texture = pending.Material.Texture };
                OsuSpriteText? text = createSemanticText(entry.Slot);
                visual.Children = text == null ? new Drawable[] { sprite } : new Drawable[] { sprite, text };
                applyGeometryAndBlend(visual, rect, null, GameplaySkinSceneBlendMode.Alpha);
                GameplaySkinSceneLayer layer = pending.PreparedRoute.Layer;
                attachedParent = Layers.Get(layer);
                attachedParent.Add(visual);
                RuntimeInstanceCount++;
                hostedDrawables[entry.Key] = visual;
                semanticVisuals[entry.Key] = new SemanticVisual(entry, visual, sprite, text);
                semanticStateFamiliesDirty |= semanticStateFamily(entry.Slot);

                if (hostedSlotsByKey.TryGetValue(entry.Key, out GameplaySkinSceneHostedSlot? gate))
                    gate.IsReplacementReady = true;

                if (isEventDriven(entry.Slot))
                    visual.Alpha = 0;
            }
            catch
            {
                RuntimeInstanceCount = instanceCountBefore;
                runtimeTextGlyphs = textGlyphCountBefore;
                hostedDrawables.Remove(pending.PreparedRoute.Key);
                semanticVisuals.Remove(pending.PreparedRoute.Key);

                if (attachedParent != null && visual != null)
                    attachedParent.Remove(visual, true);
                else
                    visual?.Dispose();

                if (hostedSlotsByKey.TryGetValue(pending.PreparedRoute.Key, out GameplaySkinSceneHostedSlot? gate))
                    gate.IsReplacementReady = false;

                addFault("OMS-SKIN-SCENE-RUNTIME-002");
            }
        }

        private void queueSemanticFallback(GameplaySkinPreparedSceneNode prepared)
        {
            if (prepared.Slot == null || prepared.MaterialTarget == null)
                return;

            var key = new GameplaySkinResolvedMaterialKey(prepared.Slot, prepared.MaterialTarget);

            if (!hostedSlotsByKey.TryGetValue(key, out GameplaySkinSceneHostedSlot? hosted))
                return;

            hosted.UsePreparedFailureRoute();

            if (hosted.Route == GameplaySkinSceneHostRoute.Semantic
                && hosted.PreparedRoute.Entry.Material is GameplaySkinPublicSlotMaterial material)
            {
                pendingSemantics.Enqueue(new PendingSemantic(hosted.PreparedRoute, material));
            }
        }

        private OsuSpriteText? createSemanticText(GameplaySkinSlotDescriptor descriptor)
        {
            if (!ReferenceEquals(descriptor, GameplaySkinSlotCatalog.ComboDisplay)
                && !ReferenceEquals(descriptor, GameplaySkinSlotCatalog.TextHud)
                && !ReferenceEquals(descriptor, GameplaySkinSlotCatalog.JudgementDisplay))
                return null;

            const int reserved_glyphs = 32;

            if (runtimeTextGlyphs + reserved_glyphs > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPHS)
                throw new InvalidOperationException();

            runtimeTextGlyphs += reserved_glyphs;
            return new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = FontUsage.Default.With(size: 24),
            };
        }

        private void consumeEvent(GameplaySkinEventRecord envelope)
        {
            if (envelope.Revision != Publication.EventRevision)
            {
                addFault("OMS-SKIN-SCENE-RUNTIME-003");
                return;
            }

            CurrentEpoch = envelope.Epoch;
            LastSequence = envelope.Sequence;
            LastGameplayTime = envelope.GameplayTime;
            GameplaySkinSceneStateFamily changedStateFamilies = GameplaySkinSceneStateFamily.None;

            if (!timingEquals(timingState, envelope.AuthoritativeTiming))
                changedStateFamilies |= GameplaySkinSceneStateFamily.Timing;

            timingState = envelope.AuthoritativeTiming;

            switch (envelope.Payload.Family)
            {
                case GameplaySkinEventPayloadFamily.State:
                    applyCompleteState(envelope.Payload.CompleteState);
                    break;

                case GameplaySkinEventPayloadFamily.Publication:
                    applyCompleteState(envelope.Payload.CompleteState);
                    break;

                case GameplaySkinEventPayloadFamily.Lifecycle:
                    lifecycleState = envelope.Payload.GetLifecycle();
                    break;

                case GameplaySkinEventPayloadFamily.Input:
                    GameplaySkinInputStateSnapshot input = envelope.Payload.GetInput(envelope.GroupId!, envelope.LaneId!);
                    updateInputIndex(input);
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Input;
                    break;

                case GameplaySkinEventPayloadFamily.Object:
                    GameplaySkinObjectStateSnapshot obj = envelope.Payload.GetObject(envelope.GroupId!, envelope.LaneId);
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Object;

                    if (envelope.EventKind == GameplaySkinEventKind.ObjectDespawned)
                    {
                        removeObjectFromIndex(obj);
                        if (judgementsByObject.ContainsKey(obj.ObjectId))
                        {
                            if (objectJudgementsToRetire.Count >= GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME)
                                addFault("OMS-SKIN-SCENE-RUNTIME-008");
                            else
                                objectJudgementsToRetire.Add(obj.ObjectId);
                        }
                    }
                    else
                    {
                        if (envelope.EventKind == GameplaySkinEventKind.ObjectSpawned)
                        {
                            judgementsByObject.Remove(obj.ObjectId);
                            changedStateFamilies |= GameplaySkinSceneStateFamily.Judgement;
                        }

                        updateObjectIndex(obj, envelope.EventKind == GameplaySkinEventKind.ObjectSpawned);
                    }
                    break;

                case GameplaySkinEventPayloadFamily.Judgement:
                    GameplaySkinJudgementStateSnapshot judgement = envelope.Payload.GetJudgement(envelope.GroupId, envelope.LaneId);
                    double displayUntil = envelope.GameplayTime + GameplaySkinEventBudgets.JUDGEMENT_DISPLAY_DURATION;
                    globalJudgement = new GameplaySkinCurrentJudgementStateSnapshot(
                        GameplaySkinJudgementScope.Global,
                        judgement,
                        envelope.GameplayTime,
                        displayUntil);

                    if (judgement.ObjectId.HasValue)
                    {
                        int maximumRetained = GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS
                                              + GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME;

                        if (judgementsByObject.ContainsKey(judgement.ObjectId.Value)
                            || judgementsByObject.Count < maximumRetained)
                            judgementsByObject[judgement.ObjectId.Value] = new GameplaySkinCurrentJudgementStateSnapshot(
                                GameplaySkinJudgementScope.Object,
                                judgement,
                                envelope.GameplayTime,
                                displayUntil);
                        else
                            addFault("OMS-SKIN-SCENE-RUNTIME-008");
                    }

                    if (judgement.GroupId != null)
                    {
                        judgementsByGroup[judgement.GroupId] = new GameplaySkinCurrentJudgementStateSnapshot(
                            GameplaySkinJudgementScope.Group,
                            judgement,
                            envelope.GameplayTime,
                            displayUntil);
                    }

                    if (judgement.LaneId != null)
                    {
                        judgementsByLane[judgement.LaneId] = new GameplaySkinCurrentJudgementStateSnapshot(
                            GameplaySkinJudgementScope.Lane,
                            judgement,
                            envelope.GameplayTime,
                            displayUntil);
                    }
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Judgement;
                    break;

                case GameplaySkinEventPayloadFamily.Score:
                    scoreState = envelope.Payload.GetScore();
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Score;
                    break;

                case GameplaySkinEventPayloadFamily.Timing:
                    timingState = envelope.Payload.GetTiming();
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Timing;
                    break;

                case GameplaySkinEventPayloadFamily.Bga:
                    GameplaySkinBgaStateSnapshot bgaState = envelope.Payload.GetBga();
                    updateBgaIndex(bgaState);
                    changedStateFamilies |= GameplaySkinSceneStateFamily.Bga;
                    break;
            }

            markStateFamilyDirty(changedStateFamilies);

            // Persistent authored state is a pure projection of the folded complete state. Rebuilding from the
            // initial state after every accepted edge makes live delivery, late attach and every epoch Reset
            // isomorphic; an author machine can never retain incremental history which Snapshot cannot express.
            stateMachinesDirty = true;
        }

        private void applyCompleteState(GameplaySkinEventStateSnapshot state)
        {
            lifecycleState = state.LifecycleState;
            scoreState = state.Score;
            timingState = state.Timing;
            inputs.Clear();
            objects.Clear();
            bga.Clear();
            judgementsByLane.Clear();
            judgementsByGroup.Clear();
            judgementsByObject.Clear();
            objectJudgementsToRetire.Clear();
            globalJudgement = null;

            foreach (GameplaySkinCurrentJudgementStateSnapshot retained in state.CurrentJudgements)
            {
                switch (retained.Scope)
                {
                    case GameplaySkinJudgementScope.Global:
                        globalJudgement = retained;
                        break;

                    case GameplaySkinJudgementScope.Group:
                        judgementsByGroup.Add(retained.Judgement.GroupId!, retained);
                        break;

                    case GameplaySkinJudgementScope.Lane:
                        judgementsByLane.Add(retained.Judgement.LaneId!, retained);
                        break;

                    case GameplaySkinJudgementScope.Object:
                        judgementsByObject.Add(retained.Judgement.ObjectId!.Value, retained);
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            foreach (GameplaySkinInputStateSnapshot input in state.Inputs)
                inputs.Add(input.LaneId, input);

            foreach (GameplaySkinObjectStateSnapshot obj in state.ActiveObjects)
                objects.Add(obj.ObjectId, obj);

            foreach (GameplaySkinBgaStateSnapshot viewport in state.BgaViewports)
                bga.Add(viewport.ViewportIndex, viewport);

            rebuildInputIndexes();
            rebuildObjectIndexes();
            rebuildBgaIndex();
            markStateFamilyDirty(GameplaySkinSceneStateFamily.All);

        }

        private void markStateFamilyDirty(GameplaySkinSceneStateFamily stateFamilies)
        {
            bindingStateFamiliesDirty |= stateFamilies;
            semanticStateFamiliesDirty |= stateFamilies;
        }

        private void rebuildInputIndexes()
        {
            firstInput = null;
            firstInputByGroup.Clear();

            foreach (GameplaySkinInputStateSnapshot input in inputs.Values)
                updateInputIndexCache(input);
        }

        private void updateInputIndex(GameplaySkinInputStateSnapshot input)
        {
            inputs[input.LaneId] = input;
            updateInputIndexCache(input);
        }

        private void updateInputIndexCache(GameplaySkinInputStateSnapshot input)
        {
            if (firstInput is not { } first
                || input.LaneId == first.LaneId
                || StringComparer.Ordinal.Compare(input.LaneId.Value, first.LaneId.Value) < 0)
                firstInput = input;

            if (!firstInputByGroup.TryGetValue(input.GroupId, out GameplaySkinInputStateSnapshot current)
                || input.LaneId == current.LaneId
                || StringComparer.Ordinal.Compare(input.LaneId.Value, current.LaneId.Value) < 0)
                firstInputByGroup[input.GroupId] = input;
        }

        private void rebuildObjectIndexes()
        {
            firstObject = null;
            firstObjectByLane.Clear();
            firstObjectByGroup.Clear();
            objectIds.Clear();
            objectIdsByLane.Clear();
            objectIdsByGroup.Clear();

            foreach (GameplaySkinObjectStateSnapshot obj in objects.Values)
                addObjectToIndexes(obj);

            refreshFirstObject();

            foreach (GameplaySkinLaneId laneId in objectIdsByLane.Keys)
                refreshFirstObject(laneId);

            foreach (GameplaySkinLaneGroupId groupId in objectIdsByGroup.Keys)
                refreshFirstObject(groupId);
        }

        private void updateObjectIndex(GameplaySkinObjectStateSnapshot obj, bool spawned)
        {
            objects[obj.ObjectId] = obj;

            if (spawned)
            {
                if (objectIds.Count >= GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS * 2)
                {
                    // Bound lazy tombstones across arbitrarily long sessions. This deterministic compaction is
                    // amortised over at least MAX_ACTIVE_OBJECTS spawn edges; it is never an every-edge scan.
                    rebuildObjectIndexes();
                    return;
                }

                addObjectToIndexes(obj);
            }

            if (firstObject is not { } global || obj.ObjectId <= global.ObjectId)
                firstObject = obj;

            if (obj.LaneId != null
                && (!firstObjectByLane.TryGetValue(obj.LaneId, out GameplaySkinObjectStateSnapshot laneCurrent)
                    || obj.ObjectId <= laneCurrent.ObjectId))
                firstObjectByLane[obj.LaneId] = obj;

            if (!firstObjectByGroup.TryGetValue(obj.GroupId, out GameplaySkinObjectStateSnapshot groupCurrent)
                || obj.ObjectId <= groupCurrent.ObjectId)
                firstObjectByGroup[obj.GroupId] = obj;
        }

        private void removeObjectFromIndex(GameplaySkinObjectStateSnapshot obj)
        {
            if (!objects.Remove(obj.ObjectId))
                return;

            if (firstObject?.ObjectId == obj.ObjectId)
                refreshFirstObject();

            if (obj.LaneId != null
                && firstObjectByLane.TryGetValue(obj.LaneId, out GameplaySkinObjectStateSnapshot laneCurrent)
                && laneCurrent.ObjectId == obj.ObjectId)
                refreshFirstObject(obj.LaneId);

            if (firstObjectByGroup.TryGetValue(obj.GroupId, out GameplaySkinObjectStateSnapshot groupCurrent)
                && groupCurrent.ObjectId == obj.ObjectId)
                refreshFirstObject(obj.GroupId);
        }

        private void addObjectToIndexes(GameplaySkinObjectStateSnapshot obj)
        {
            objectIds.Add(obj.ObjectId);
            getOrCreateHeap(objectIdsByGroup, obj.GroupId).Add(obj.ObjectId);

            if (obj.LaneId != null)
                getOrCreateHeap(objectIdsByLane, obj.LaneId).Add(obj.ObjectId);
        }

        private void refreshFirstObject()
        {
            while (objectIds.TryPeek(out long objectId))
            {
                if (objects.TryGetValue(objectId, out GameplaySkinObjectStateSnapshot obj))
                {
                    firstObject = obj;
                    return;
                }

                objectIds.RemoveMin();
            }

            firstObject = null;
        }

        private void refreshFirstObject(GameplaySkinLaneId laneId)
        {
            ObjectIdMinHeap heap = objectIdsByLane[laneId];

            while (heap.TryPeek(out long objectId))
            {
                if (objects.TryGetValue(objectId, out GameplaySkinObjectStateSnapshot obj) && obj.LaneId == laneId)
                {
                    firstObjectByLane[laneId] = obj;
                    return;
                }

                heap.RemoveMin();
            }

            firstObjectByLane.Remove(laneId);
        }

        private void refreshFirstObject(GameplaySkinLaneGroupId groupId)
        {
            ObjectIdMinHeap heap = objectIdsByGroup[groupId];

            while (heap.TryPeek(out long objectId))
            {
                if (objects.TryGetValue(objectId, out GameplaySkinObjectStateSnapshot obj) && obj.GroupId == groupId)
                {
                    firstObjectByGroup[groupId] = obj;
                    return;
                }

                heap.RemoveMin();
            }

            firstObjectByGroup.Remove(groupId);
        }

        private static ObjectIdMinHeap getOrCreateHeap<TKey>(Dictionary<TKey, ObjectIdMinHeap> heaps, TKey key)
            where TKey : notnull
        {
            if (!heaps.TryGetValue(key, out ObjectIdMinHeap? heap))
            {
                heap = new ObjectIdMinHeap();
                heaps.Add(key, heap);
            }

            return heap;
        }

        private void rebuildBgaIndex()
        {
            firstBga = null;

            foreach (GameplaySkinBgaStateSnapshot viewport in bga.Values)
            {
                if (firstBga is not { } first || viewport.ViewportIndex < first.ViewportIndex)
                    firstBga = viewport;
            }
        }

        private void updateBgaIndex(GameplaySkinBgaStateSnapshot viewport)
        {
            bga[viewport.ViewportIndex] = viewport;

            if (firstBga is not { } first
                || viewport.ViewportIndex == first.ViewportIndex
                || viewport.ViewportIndex < first.ViewportIndex)
                firstBga = viewport;
        }

        private GameplaySkinInputStateSnapshot? inputFor(GameplaySkinResolvedMaterialTarget target)
        {
            if (target.LaneId != null)
                return inputs.TryGetValue(target.LaneId, out GameplaySkinInputStateSnapshot lane) ? lane : null;

            if (target.GroupId != null)
                return firstInputByGroup.TryGetValue(target.GroupId, out GameplaySkinInputStateSnapshot group) ? group : null;

            return firstInput;
        }

        private bool anyInputPressed(GameplaySkinResolvedMaterialTarget target)
        {
            if (target.LaneId != null)
                return inputs.TryGetValue(target.LaneId, out GameplaySkinInputStateSnapshot lane) && lane.IsPressed;

            foreach (GameplaySkinInputStateSnapshot input in inputs.Values)
            {
                if (input.IsPressed && (target.GroupId == null || input.GroupId == target.GroupId))
                    return true;
            }

            return false;
        }

        private GameplaySkinObjectStateSnapshot? objectFor(GameplaySkinResolvedMaterialTarget target)
        {
            if (target.LaneId != null)
                return firstObjectByLane.TryGetValue(target.LaneId, out GameplaySkinObjectStateSnapshot lane) ? lane : null;

            if (target.GroupId != null)
                return firstObjectByGroup.TryGetValue(target.GroupId, out GameplaySkinObjectStateSnapshot group) ? group : null;

            return firstObject;
        }

        private void initialiseStateMachines()
        {
            foreach (GameplaySkinPreparedSceneStateMachine machine in PreparedScene.Program.StateMachines)
                stateMachineStates.Add(machine.Id, machine.InitialStateId);
        }

        private void registerStateMachineScopes(GameplaySkinSceneRuntimeNode runtime)
        {
            if (runtime.PreparedNode.MaterialTarget == null)
                return;

            foreach (GameplaySkinPreparedSceneStateMachine machine in PreparedScene.Program.StateMachines)
            {
                if (!machine.ReferencesNode(runtime.PreparedNode.Source.Id))
                    continue;

                GameplaySkinResolvedMaterialTarget target = runtime.PreparedNode.MaterialTarget;
                StateMachineInstance? instance = null;

                foreach (StateMachineInstance candidate in stateMachineInstances)
                {
                    if (ReferenceEquals(candidate.Machine, machine)
                        && string.Equals(candidate.RuntimeScopeId, runtime.RuntimeScopeId, StringComparison.Ordinal))
                    {
                        instance = candidate;
                        break;
                    }
                }

                if (instance == null)
                {
                    bool hasExistingScope = false;

                    foreach (StateMachineInstance candidate in stateMachineInstances)
                    {
                        if (ReferenceEquals(candidate.Machine, machine))
                        {
                            hasExistingScope = true;
                            break;
                        }
                    }

                    string publicKey = hasExistingScope ? stateMachineScopeKey(machine, runtime) : machine.Id;
                    instance = new StateMachineInstance(
                        machine,
                        target,
                        runtime.PreparedNode.ResolvedTarget,
                        runtime.RuntimeScopeId,
                        publicKey,
                        runtime.BoundObjectId);
                    stateMachineInstances.Add(instance);
                    stateMachineStates[publicKey] = instance.StateId;
                    advanceStateMachineForCurrentSnapshot(instance);
                }

                applyStateAssignments(instance);
            }
        }

        private void advanceStateMachineForCurrentSnapshot(StateMachineInstance instance)
        {
            // Snapshot projection is an author-neutral canonical replay, not a reconstruction from lost delta
            // history. Every complete state is projected in this fixed family order so late attach, retry, seek and
            // consumer rebuild converge from the machine's declared initial state.
            tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayAttach);

            // A complete lifecycle value is the folded result of the same ordered engine edges which a live
            // consumer would have observed. Replay that prefix explicitly so a late attach or epoch reset reaches
            // the same author state as incremental delivery. Loaded is not an absence of lifecycle: it is the
            // committed GameplayLoaded state and must remain observable after history has been discarded.
            tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayLoaded);

            if (lifecycleState is GameplaySkinLifecycleState.Running
                or GameplaySkinLifecycleState.Paused
                or GameplaySkinLifecycleState.Completed
                or GameplaySkinLifecycleState.Failed)
            {
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayStart);
            }

            if (lifecycleState == GameplaySkinLifecycleState.Paused)
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayPause);
            else if (lifecycleState == GameplaySkinLifecycleState.Completed)
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayComplete);
            else if (lifecycleState == GameplaySkinLifecycleState.Failed)
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.GameplayFailed);

            bool inputPressed = anyInputPressed(instance.Target);
            tryAdvanceStateMachine(
                instance,
                inputPressed ? GameplaySkinSceneEvent.InputKeyDown : GameplaySkinSceneEvent.InputKeyUp);

            GameplaySkinObjectStateSnapshot? projectedObject;
            if (instance.BoundObjectId.HasValue)
            {
                projectedObject = objects.TryGetValue(instance.BoundObjectId.Value, out GameplaySkinObjectStateSnapshot boundObject)
                    ? boundObject
                    : null;
            }
            else
                projectedObject = objectFor(instance.Target);

            if (projectedObject is { } obj)
                projectObject(obj);

            GameplaySkinJudgementStateSnapshot? snapshotJudgement = judgementFor(instance.Target);

            if (instance.BoundObjectId.HasValue)
                snapshotJudgement = judgementsByObject.TryGetValue(instance.BoundObjectId.Value, out GameplaySkinCurrentJudgementStateSnapshot exactJudgement)
                    ? exactJudgement.Judgement
                    : null;

            if (snapshotJudgement is { } judgement
                && (!instance.BoundObjectId.HasValue || judgement.ObjectId == instance.BoundObjectId.Value)
                && eventTargets(instance.Target, judgement.GroupId, judgement.LaneId))
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.JudgementHit);

            if (timingState.IsStopped)
                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.TimingStop);

            if (instance.SceneTarget.Kind == GameplaySkinSceneTargetKind.Bga)
            {
                if (bga.TryGetValue(instance.SceneTarget.Index ?? 0, out GameplaySkinBgaStateSnapshot exactBga)
                    && exactBga.ContentState != GameplaySkinBgaContentState.Empty)
                    tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.BgaState);
            }
            else
            {
                if (bga.Values.Any(viewport => viewport.ContentState != GameplaySkinBgaContentState.Empty))
                    tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.BgaState);
            }

            void projectObject(GameplaySkinObjectStateSnapshot obj)
            {
                if (!eventTargets(instance.Target, obj.GroupId, obj.LaneId))
                    return;

                tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.ObjectSpawn);

                if (obj.State is not (GameplaySkinObjectState.Scheduled or GameplaySkinObjectState.Visible))
                    tryAdvanceStateMachine(instance, GameplaySkinSceneEvent.ObjectState);
            }
        }

        private void resetSpecialisedStateMachineScope(string runtimeScopeId)
        {
            long? objectId = null;

            foreach (GameplaySkinSceneRuntimeNode node in runtimeNodes.Values)
            {
                if (!string.Equals(node.RuntimeScopeId, runtimeScopeId, StringComparison.Ordinal))
                    continue;

                objectId = node.BoundObjectId;
                break;
            }

            foreach (StateMachineInstance instance in stateMachineInstances)
            {
                if (!string.Equals(instance.RuntimeScopeId, runtimeScopeId, StringComparison.Ordinal))
                    continue;

                instance.BoundObjectId = objectId;
                instance.StateId = instance.Machine.InitialStateId;
                stateMachineStates[instance.PublicKey] = instance.StateId;
                advanceStateMachineForCurrentSnapshot(instance);
                applyStateAssignments(instance);
            }
        }

        private void resetStateMachines()
        {
            if (PreparedScene.Program.StateMachines.Count == 0)
                return;

            StateMachineProjectionPassCount++;

            foreach (StateMachineInstance instance in stateMachineInstances)
            {
                instance.StateId = instance.Machine.InitialStateId;
                stateMachineStates[instance.PublicKey] = instance.StateId;
                advanceStateMachineForCurrentSnapshot(instance);
                applyStateAssignments(instance);
            }
        }

        /// <summary>
        /// Reconstructs scoped state-machine instances only from runtime nodes which survived a local build/retire.
        /// This is a fault/retirement path, never a per-frame operation.
        /// </summary>
        private void rebuildStateMachineScopes()
        {
            stateMachineInstances.Clear();
            stateMachineStates.Clear();
            initialiseStateMachines();

            foreach (GameplaySkinSceneRuntimeNode runtime in runtimeNodes.Values.OrderBy(node => node.InstanceId, StringComparer.Ordinal))
                registerStateMachineScopes(runtime);
        }

        private bool tryAdvanceStateMachine(StateMachineInstance instance, GameplaySkinSceneEvent sceneEvent)
        {
            foreach (GameplaySkinPreparedSceneTransition transition in instance.Machine.Transitions)
            {
                if (transition.Event == sceneEvent
                    && string.Equals(transition.FromStateId, instance.StateId, StringComparison.Ordinal))
                {
                    instance.StateId = transition.ToStateId;
                    stateMachineStates[instance.PublicKey] = instance.StateId;
                    return true;
                }
            }

            return false;
        }

        private void applyStateAssignments(StateMachineInstance instance)
        {
            if (!instance.Machine.TryGetState(instance.StateId, out GameplaySkinPreparedSceneState state))
                return;

            foreach (GameplaySkinPreparedSceneStateAssignment assignment in state.Assignments)
            {
                if (!runtimeNodesBySourceId.TryGetValue(assignment.TargetNodeId, out List<GameplaySkinSceneRuntimeNode>? targets))
                    continue;

                for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
                {
                    GameplaySkinSceneRuntimeNode target = targets[targetIndex];

                    if (string.Equals(target.RuntimeScopeId, instance.RuntimeScopeId, StringComparison.Ordinal))
                    {
                        try
                        {
                            applyRuntimeProperty(target, assignment.Property, assignment.Value);
                        }
                        catch
                        {
                            handleRuntimeNodeFault(target, "OMS-SKIN-SCENE-RUNTIME-006");
                            break;
                        }
                    }
                }
            }
        }

        private static bool eventTargets(
            GameplaySkinResolvedMaterialTarget target,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId)
        {
            return target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => true,
                GameplaySkinResolvedMaterialTargetKind.Stage or GameplaySkinResolvedMaterialTargetKind.Group =>
                    groupId == null || target.GroupId == groupId,
                GameplaySkinResolvedMaterialTargetKind.Lane => laneId != null
                    ? target.LaneId == laneId
                    : groupId == null || target.GroupId == groupId,
                _ => false,
            };
        }

        private static string stateMachineScopeKey(
            GameplaySkinPreparedSceneStateMachine machine,
            GameplaySkinSceneRuntimeNode runtime)
            => $"{machine.Id}@{runtime.RuntimeScopeId}";

        /// <summary>
        /// Samples every V1 track against the exact stream's authoritative gameplay time. Track phase is global to
        /// the committed publication (including pooled specialised clones); object-relative effects retain their
        /// engine-owned lifetime and state instead of introducing a renderer-local clock or an OnApply phase origin.
        /// A reused clone is therefore fully resampled on its next frame and cannot retain a prior object's phase.
        /// </summary>
        private void sampleTracks(double gameplayTime)
        {
            IReadOnlyList<GameplaySkinPreparedSceneTrack> tracks = PreparedScene.Program.Tracks;

            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                GameplaySkinPreparedSceneTrack track = tracks[trackIndex];

                if (!runtimeNodesBySourceId.TryGetValue(track.TargetNodeId, out List<GameplaySkinSceneRuntimeNode>? targets))
                    continue;

                if (track.Type == GameplaySkinSceneTrackType.Tween)
                {
                    double value = sampleTweenTrack(track, gameplayTime);

                    for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
                    {
                        if (!tryApplyRuntimeNumberProperty(targets[targetIndex], track.Property, value))
                            break;
                    }
                }
                else
                {
                    GameplaySkinPreparedSceneValue value = sampleFrameTrack(track, gameplayTime);

                    for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
                    {
                        if (!tryApplyRuntimeProperty(targets[targetIndex], track.Property, value))
                            break;
                    }
                }
            }
        }

        private static GameplaySkinPreparedSceneValue sampleFrameTrack(GameplaySkinPreparedSceneTrack track, double gameplayTime)
        {
            IReadOnlyList<GameplaySkinPreparedSceneKeyframe> frames = track.Keyframes;
            double time = gameplayTime;
            double end = frames[^1].Time;

            if (track.Loop && end > 0)
                time = ((time % end) + end) % end;

            if (time <= frames[0].Time)
                return frames[0].Value;

            if (time >= end)
                return frames[^1].Value;

            int nextIndex = 1;

            while (nextIndex < frames.Count && frames[nextIndex].Time <= time)
                nextIndex++;

            GameplaySkinPreparedSceneKeyframe previous = frames[nextIndex - 1];
            return previous.Value;
        }

        private static double sampleTweenTrack(GameplaySkinPreparedSceneTrack track, double gameplayTime)
        {
            IReadOnlyList<GameplaySkinPreparedSceneKeyframe> frames = track.Keyframes;
            double time = gameplayTime;
            double end = frames[^1].Time;

            if (track.Loop && end > 0)
                time = ((time % end) + end) % end;

            if (time <= frames[0].Time)
                return frames[0].Value.NumberValue;

            if (time >= end)
                return frames[^1].Value.NumberValue;

            int nextIndex = 1;

            while (nextIndex < frames.Count && frames[nextIndex].Time < time)
                nextIndex++;

            GameplaySkinPreparedSceneKeyframe previous = frames[nextIndex - 1];
            GameplaySkinPreparedSceneKeyframe next = frames[nextIndex];
            double amount = ease((time - previous.Time) / (next.Time - previous.Time), track.Easing);
            return previous.Value.NumberValue + (next.Value.NumberValue - previous.Value.NumberValue) * amount;
        }

        private static double ease(double amount, GameplaySkinSceneEasing easing) => easing switch
        {
            GameplaySkinSceneEasing.Step => 0,
            GameplaySkinSceneEasing.Linear => amount,
            GameplaySkinSceneEasing.In => amount * amount,
            GameplaySkinSceneEasing.Out => 1 - (1 - amount) * (1 - amount),
            GameplaySkinSceneEasing.InOut => amount < 0.5 ? 2 * amount * amount : 1 - Math.Pow(-2 * amount + 2, 2) / 2,
            _ => 0,
        };

        private void applyBindings(GameplaySkinSceneStateFamily stateFamilies)
        {
            foreach (GameplaySkinPreparedSceneBinding binding in PreparedScene.Program.Bindings)
            {
                if ((stateFamilies & binding.StateFamily) == 0)
                    continue;

                if (!runtimeNodesBySourceId.TryGetValue(binding.TargetNodeId, out List<GameplaySkinSceneRuntimeNode>? targets))
                    continue;

                for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
                {
                    GameplaySkinSceneRuntimeNode target = targets[targetIndex];

                    try
                    {
                        BindingApplicationCount++;
                        applyBinding(binding, target);
                    }
                    catch
                    {
                        handleRuntimeNodeFault(target, "OMS-SKIN-SCENE-RUNTIME-006");
                        break;
                    }
                }
            }
        }

        private void applyVariants(GameplaySkinSceneStateFamily stateFamilies)
        {
            foreach (GameplaySkinPreparedSceneVariant variant in PreparedScene.Program.Variants)
            {
                if ((stateFamilies & variant.StateFamily) == 0)
                    continue;

                if (!runtimeNodesBySourceId.TryGetValue(variant.TargetNodeId, out List<GameplaySkinSceneRuntimeNode>? targets))
                    continue;

                for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
                {
                    GameplaySkinSceneRuntimeNode target = targets[targetIndex];

                    try
                    {
                        VariantApplicationCount++;
                        string key = variantKey(variant.Source, target);
                        GameplaySkinPreparedSceneResource resource = variant.SelectResource(key);

                        if (target.ContentDrawable is not Sprite sprite)
                            throw new InvalidOperationException();

                        sprite.Texture = resource.Texture!;
                    }
                    catch
                    {
                        handleRuntimeNodeFault(target, "OMS-SKIN-SCENE-RUNTIME-007");
                        break;
                    }
                }
            }
        }

        private string variantKey(GameplaySkinSceneBindingSource source, GameplaySkinSceneRuntimeNode target)
        {
            GameplaySkinResolvedMaterialTarget? materialTarget = target.PreparedNode.MaterialTarget;
            GameplaySkinLaneId? laneId = materialTarget?.LaneId;
            GameplaySkinLaneGroupId? groupId = materialTarget?.GroupId;
            GameplaySkinObjectStateSnapshot? obj = objectFor(target, laneId, groupId);
            GameplaySkinJudgementStateSnapshot? judgement = judgementFor(target, materialTarget);
            GameplaySkinBgaStateSnapshot? bgaState = bgaFor(target.PreparedNode.ResolvedTarget);

            return source switch
            {
                GameplaySkinSceneBindingSource.ObjectState => objectStateId(obj?.State ?? GameplaySkinObjectState.Scheduled),
                GameplaySkinSceneBindingSource.JudgementResult => judgementGradeId(judgement?.Grade ?? GameplaySkinJudgementGrade.Miss),
                GameplaySkinSceneBindingSource.BgaContentState => bgaStateId(bgaState?.ContentState ?? GameplaySkinBgaContentState.Empty),
                _ => throw new InvalidOperationException(),
            };
        }

        private static GameplaySkinSceneStateFamily semanticStateFamily(GameplaySkinSlotDescriptor slot)
        {
            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyFlash)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyVisual)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.Laser))
                return GameplaySkinSceneStateFamily.Input;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.HitExplosion)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementDisplay))
                return GameplaySkinSceneStateFamily.Judgement;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.ComboDisplay)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual))
                return GameplaySkinSceneStateFamily.Score;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.BgaViewport))
                return GameplaySkinSceneStateFamily.Bga;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.Turntable))
                return GameplaySkinSceneStateFamily.Timing | GameplaySkinSceneStateFamily.Input;

            return GameplaySkinSceneStateFamily.None;
        }

        private void updateSemanticState(double gameplayTime, GameplaySkinSceneStateFamily stateFamilies)
        {
            foreach (SemanticVisual semantic in semanticVisuals.Values)
            {
                GameplaySkinSlotDescriptor slot = semantic.Entry.Slot;

                // Judgement/effect expiry is the only semantic visibility which advances without a new state event.
                bool expiryDriven = ReferenceEquals(slot, GameplaySkinSlotCatalog.HitExplosion)
                                    || ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementDisplay);
                bool continuousTiming = ReferenceEquals(slot, GameplaySkinSlotCatalog.Turntable);
                bool stateChanged = (stateFamilies & semanticStateFamily(slot)) != 0;

                if (!stateChanged && !expiryDriven && !continuousTiming)
                    continue;

                SemanticStateApplicationCount++;

                if (ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyFlash)
                    || ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyVisual))
                {
                    GameplaySkinLaneId? laneId = semantic.Entry.Target.LaneId;
                    bool pressed = laneId != null && inputs.TryGetValue(laneId, out GameplaySkinInputStateSnapshot input) && input.IsPressed;
                    semantic.Drawable.Alpha = ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyFlash)
                        ? (pressed ? 1 : 0)
                        : (pressed ? 1 : 0.65f);
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.HitExplosion)
                         || ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementDisplay))
                {
                    GameplaySkinJudgementStateSnapshot? targetJudgement = judgementFor(semantic.Entry.Target);
                    bool matchingTarget = targetJudgement != null;
                    semantic.Drawable.Alpha = matchingTarget && gameplayTime <= judgementExpiryFor(semantic.Entry.Target) ? 1 : 0;

                    if (stateChanged && semantic.Text != null)
                        semantic.Text.Text = targetJudgement == null ? string.Empty : judgementGradeId(targetJudgement.Value.Grade);
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.ComboDisplay))
                {
                    if (semantic.Text != null)
                        semantic.Text.Text = scoreState.Combo.ToString(CultureInfo.InvariantCulture);
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud))
                {
                    if (semantic.Text != null)
                        semantic.Text.Text = $"{scoreState.Score.ToString(CultureInfo.InvariantCulture)}  {scoreState.Combo.ToString(CultureInfo.InvariantCulture)}x";
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual))
                {
                    semantic.Sprite.RelativeSizeAxes = Axes.Both;
                    semantic.Sprite.Width = (float)scoreState.Gauge;
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.BgaViewport))
                {
                    GameplaySkinBgaContentState state = firstBga?.ContentState ?? GameplaySkinBgaContentState.Empty;
                    semantic.Drawable.Alpha = state is GameplaySkinBgaContentState.Playing
                        or GameplaySkinBgaContentState.Ready
                        or GameplaySkinBgaContentState.Paused
                        ? 1
                        : 0;
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.Turntable))
                {
                    GameplaySkinInputStateSnapshot? input = inputFor(semantic.Entry.Target);
                    double laneOffset = input?.IsPressed == true ? 22.5 : 0;
                    semantic.Drawable.Rotation = (float)((timingState.Beat * 360 + laneOffset) % 360);
                }
                else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.Laser))
                    semantic.Drawable.Alpha = inputFor(semantic.Entry.Target)?.IsPressed == true ? 1 : 0;
            }
        }

        private void applyBinding(GameplaySkinPreparedSceneBinding binding, GameplaySkinSceneRuntimeNode target)
        {
            GameplaySkinResolvedMaterialTarget? materialTarget = target.PreparedNode.MaterialTarget;
            GameplaySkinLaneId? laneId = materialTarget?.LaneId;
            GameplaySkinLaneGroupId? groupId = materialTarget?.GroupId;
            GameplaySkinInputStateSnapshot? input = materialTarget == null ? firstInput : inputFor(materialTarget);
            GameplaySkinObjectStateSnapshot? obj = objectFor(target, laneId, groupId);
            GameplaySkinJudgementStateSnapshot? judgement = judgementFor(target, materialTarget);
            GameplaySkinBgaStateSnapshot? bgaState = bgaFor(target.PreparedNode.ResolvedTarget);

            switch (binding.Source)
            {
                case GameplaySkinSceneBindingSource.LayoutStage:
                case GameplaySkinSceneBindingSource.LayoutGroup:
                    applyRuntimeStringProperty(target, binding.Property, groupId?.Value ?? string.Empty);
                    return;

                case GameplaySkinSceneBindingSource.LayoutLane:
                    applyRuntimeStringProperty(target, binding.Property, laneId?.Value ?? string.Empty);
                    return;

                case GameplaySkinSceneBindingSource.InputPressed:
                    applyRuntimeBooleanProperty(target, binding.Property, input?.IsPressed ?? false);
                    return;

                case GameplaySkinSceneBindingSource.ObjectState:
                    applyRuntimeStringProperty(target, binding.Property, objectStateId(obj?.State ?? GameplaySkinObjectState.Scheduled));
                    return;

                case GameplaySkinSceneBindingSource.JudgementResult:
                    applyRuntimeStringProperty(target, binding.Property, judgementGradeId(judgement?.Grade ?? GameplaySkinJudgementGrade.Miss));
                    return;

                case GameplaySkinSceneBindingSource.JudgementOffset:
                    applyRuntimeNumberProperty(target, binding.Property, judgement?.Offset ?? 0);
                    return;

                case GameplaySkinSceneBindingSource.ScoreValue:
                    applyRuntimeNumberProperty(target, binding.Property, scoreState.Score);
                    return;

                case GameplaySkinSceneBindingSource.ComboValue:
                    applyRuntimeNumberProperty(target, binding.Property, scoreState.Combo);
                    return;

                case GameplaySkinSceneBindingSource.GaugeValue:
                    applyRuntimeNumberProperty(target, binding.Property, scoreState.Gauge);
                    return;

                case GameplaySkinSceneBindingSource.TimingBeat:
                    applyRuntimeNumberProperty(target, binding.Property, timingState.Beat);
                    return;

                case GameplaySkinSceneBindingSource.TimingMeasure:
                    applyRuntimeNumberProperty(target, binding.Property, timingState.BarIndex);
                    return;

                case GameplaySkinSceneBindingSource.TimingBpm:
                    applyRuntimeNumberProperty(target, binding.Property, timingState.Bpm);
                    return;

                case GameplaySkinSceneBindingSource.BgaContentState:
                    applyRuntimeStringProperty(target, binding.Property, bgaStateId(bgaState?.ContentState ?? GameplaySkinBgaContentState.Empty));
                    return;

                default:
                    throw new InvalidOperationException();
            }
        }

        private GameplaySkinJudgementStateSnapshot? judgementFor(GameplaySkinResolvedMaterialTarget target)
        {
            if (target.LaneId != null && judgementsByLane.TryGetValue(target.LaneId, out GameplaySkinCurrentJudgementStateSnapshot lane))
                return lane.Judgement;

            if (target.GroupId != null && judgementsByGroup.TryGetValue(target.GroupId, out GameplaySkinCurrentJudgementStateSnapshot group))
                return group.Judgement;

            return target.GroupId == null ? globalJudgement?.Judgement : null;
        }

        private GameplaySkinJudgementStateSnapshot? judgementFor(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinResolvedMaterialTarget? target)
        {
            // A pooled per-object visual has stronger identity than its lane/group material target. Falling through
            // to the latest lane judgement when that exact object has no result would let an adjacent same-lane
            // object drive this clone's bindings, even though state-machine event routing correctly rejected it.
            if (node.BoundObjectId.HasValue)
                return judgementsByObject.TryGetValue(node.BoundObjectId.Value, out GameplaySkinCurrentJudgementStateSnapshot exact) ? exact.Judgement : null;

            return target == null ? globalJudgement?.Judgement : judgementFor(target);
        }

        private GameplaySkinObjectStateSnapshot? objectFor(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinLaneId? laneId,
            GameplaySkinLaneGroupId? groupId)
        {
            if (node.BoundObjectId.HasValue)
                return objects.TryGetValue(node.BoundObjectId.Value, out GameplaySkinObjectStateSnapshot exact) ? exact : null;

            if (laneId != null)
                return firstObjectByLane.TryGetValue(laneId, out GameplaySkinObjectStateSnapshot lane) ? lane : null;

            if (groupId != null)
                return firstObjectByGroup.TryGetValue(groupId, out GameplaySkinObjectStateSnapshot group) ? group : null;

            return firstObject;
        }

        private GameplaySkinBgaStateSnapshot? bgaFor(GameplaySkinSceneTarget target)
        {
            if (target.Kind == GameplaySkinSceneTargetKind.Bga)
                return bga.TryGetValue(target.Index ?? 0, out GameplaySkinBgaStateSnapshot exact) ? exact : null;

            return firstBga;
        }

        private double judgementExpiryFor(GameplaySkinResolvedMaterialTarget target)
        {
            if (target.LaneId != null && judgementsByLane.TryGetValue(target.LaneId, out GameplaySkinCurrentJudgementStateSnapshot lane))
                return lane.DisplayUntil;

            if (target.GroupId != null && judgementsByGroup.TryGetValue(target.GroupId, out GameplaySkinCurrentJudgementStateSnapshot group))
                return group.DisplayUntil;

            return target.GroupId == null ? globalJudgement?.DisplayUntil ?? double.NegativeInfinity : double.NegativeInfinity;
        }

        private bool expireTransientJudgements(double gameplayTime)
        {
            bool changed = false;
            expiredLaneJudgements.Clear();
            expiredGroupJudgements.Clear();

            foreach ((GameplaySkinLaneId laneId, GameplaySkinCurrentJudgementStateSnapshot retained) in judgementsByLane)
            {
                if (retained.DisplayUntil <= gameplayTime)
                    expiredLaneJudgements.Add(laneId);
            }

            foreach (GameplaySkinLaneId laneId in expiredLaneJudgements)
            {
                judgementsByLane.Remove(laneId);
                changed = true;
            }

            foreach ((GameplaySkinLaneGroupId groupId, GameplaySkinCurrentJudgementStateSnapshot retained) in judgementsByGroup)
            {
                if (retained.DisplayUntil <= gameplayTime)
                    expiredGroupJudgements.Add(groupId);
            }

            foreach (GameplaySkinLaneGroupId groupId in expiredGroupJudgements)
            {
                judgementsByGroup.Remove(groupId);
                changed = true;
            }

            if (globalJudgement is { } global && global.DisplayUntil <= gameplayTime)
            {
                globalJudgement = null;
                changed = true;
            }

            return changed;
        }

        private void applyRuntimeProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            GameplaySkinPreparedSceneValue value)
        {
            if (property == GameplaySkinSceneProperty.Resource)
            {
                if (value.Texture != null && node.ContentDrawable is Sprite resourceSprite)
                {
                    resourceSprite.Texture = value.Texture;
                    return;
                }

                throw new InvalidOperationException();
            }

            switch (value.Kind)
            {
                case GameplaySkinScenePropertyValueKind.Number:
                    applyRuntimeNumberProperty(node, property, value.NumberValue);
                    return;

                case GameplaySkinScenePropertyValueKind.Boolean:
                    applyRuntimeBooleanProperty(node, property, value.BooleanValue);
                    return;

                case GameplaySkinScenePropertyValueKind.String:
                    applyRuntimeStringProperty(node, property, value.StringValue ?? string.Empty);
                    return;

                default:
                    throw new InvalidOperationException();
            }
        }

        private bool tryApplyRuntimeProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            GameplaySkinPreparedSceneValue value)
        {
            try
            {
                applyRuntimeProperty(node, property, value);
                return true;
            }
            catch
            {
                handleRuntimeNodeFault(node, "OMS-SKIN-SCENE-RUNTIME-006");
                return false;
            }
        }

        private bool tryApplyRuntimeNumberProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            double value)
        {
            try
            {
                applyRuntimeNumberProperty(node, property, value);
                return true;
            }
            catch
            {
                handleRuntimeNodeFault(node, "OMS-SKIN-SCENE-RUNTIME-006");
                return false;
            }
        }

        private void applyRuntimeBooleanProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            bool value)
        {
            switch (property)
            {
                case GameplaySkinSceneProperty.Visible:
                    node.TransformDrawable.Alpha = value ? 1 : 0;
                    return;

                case GameplaySkinSceneProperty.Text when node.ContentDrawable is SpriteText text:
                    string booleanText = value ? "true" : "false";

                    if (booleanText.Length > PreparedScene.GetTextGlyphReservation(node.PreparedNode.Source.Id))
                        throw new InvalidOperationException();

                    text.Text = booleanText;
                    return;

                default:
                    throw new InvalidOperationException();
            }
        }

        private void applyRuntimeStringProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            string value)
        {
            switch (property)
            {
                case GameplaySkinSceneProperty.Resource:
                    throw new InvalidOperationException();

                case GameplaySkinSceneProperty.Text when node.ContentDrawable is SpriteText text:
                    if (value.Length <= PreparedScene.GetTextGlyphReservation(node.PreparedNode.Source.Id))
                    {
                        text.Text = value;
                        return;
                    }

                    throw new InvalidOperationException();

                case GameplaySkinSceneProperty.Anchor:
                    node.TransformDrawable.Anchor = anchor(value);
                    return;

                case GameplaySkinSceneProperty.Origin:
                    node.TransformDrawable.Origin = anchor(value);
                    return;

                case GameplaySkinSceneProperty.Colour:
                    node.ContentDrawable.Colour = colour(value);
                    return;

                case GameplaySkinSceneProperty.FillMode when node.ContentDrawable is Sprite fillSprite:
                    fillSprite.FillMode = value switch
                    {
                        "stretch" => FillMode.Stretch,
                        "fit" => FillMode.Fit,
                        "fill" => FillMode.Fill,
                        _ => throw new InvalidOperationException(),
                    };
                    return;

                case GameplaySkinSceneProperty.Alignment when node.ContentDrawable is SpriteText alignedText:
                    (alignedText.Anchor, alignedText.Origin) = value switch
                    {
                        "left" => (Anchor.CentreLeft, Anchor.CentreLeft),
                        "centre" => (Anchor.Centre, Anchor.Centre),
                        "right" => (Anchor.CentreRight, Anchor.CentreRight),
                        _ => throw new InvalidOperationException(),
                    };
                    return;

                case GameplaySkinSceneProperty.MaskMode when node.ContentDrawable is GameplaySkinShapeMaskContainer mask:
                    if (value != "ellipse")
                        throw new InvalidOperationException();

                    mask.Masking = true;
                    return;

                case GameplaySkinSceneProperty.ClipMode when node.ContentDrawable is Container clip:
                    if (value is not ("bounds" or "rounded"))
                        throw new InvalidOperationException();

                    clip.Masking = true;

                    if (value == "bounds")
                        clip.CornerRadius = 0;

                    return;

                default:
                    throw new InvalidOperationException();
            }
        }

        private void applyRuntimeNumberProperty(
            GameplaySkinSceneRuntimeNode node,
            GameplaySkinSceneProperty property,
            double value)
        {
            // Engine-owned values (score, timing, judgement, gauge) are intentionally read-only and may exceed an
            // author's visual range. Clamp them through the same frozen V1 range authority used by the codec before
            // converting to framework floats or changing glyph-atlas parameters.
            value = GameplaySkinSceneNumericRange.ClampBoundValue(property, value);

            switch (property)
            {
                case GameplaySkinSceneProperty.Opacity:
                    node.TransformDrawable.Alpha = (float)value;
                    return;

                case GameplaySkinSceneProperty.X:
                    node.TransformDrawable.RelativePositionAxes |= Axes.X;
                    node.TransformDrawable.X = (float)value;
                    return;

                case GameplaySkinSceneProperty.Y:
                    node.TransformDrawable.RelativePositionAxes |= Axes.Y;
                    node.TransformDrawable.Y = (float)value;
                    return;

                case GameplaySkinSceneProperty.Width:
                    node.TransformDrawable.RelativeSizeAxes |= Axes.X;
                    node.TransformDrawable.Width = (float)value;
                    return;

                case GameplaySkinSceneProperty.Height:
                    node.TransformDrawable.RelativeSizeAxes |= Axes.Y;
                    node.TransformDrawable.Height = (float)value;
                    return;

                case GameplaySkinSceneProperty.ScaleX:
                    node.TransformDrawable.Scale = new Vector2((float)value, node.TransformDrawable.Scale.Y);
                    return;

                case GameplaySkinSceneProperty.ScaleY:
                    node.TransformDrawable.Scale = new Vector2(node.TransformDrawable.Scale.X, (float)value);
                    return;

                case GameplaySkinSceneProperty.Rotation:
                    node.TransformDrawable.Rotation = (float)value;
                    return;

                case GameplaySkinSceneProperty.Z:
                    node.ContentDrawable.Depth = (float)value;
                    return;

                case GameplaySkinSceneProperty.FontSize when node.ContentDrawable is SpriteText text:
                    text.Font = text.Font.With(size: (float)value);
                    return;

                case GameplaySkinSceneProperty.CornerRadius when node.ContentDrawable is Container clip:
                    clip.CornerRadius = (float)value;
                    clip.Masking = true;
                    return;

                case GameplaySkinSceneProperty.Text when node.ContentDrawable is SpriteText text:
                    string display = value.ToString("0.###", CultureInfo.InvariantCulture);

                    if (display.Length <= PreparedScene.GetTextGlyphReservation(node.PreparedNode.Source.Id))
                    {
                        text.Text = display;
                        return;
                    }

                    throw new InvalidOperationException();

                default:
                    throw new InvalidOperationException();
            }
        }

        private static void applyNodeProperties(
            Container transform,
            Drawable content,
            IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties)
        {
            foreach ((string property, GameplaySkinScenePropertyValue value) in properties)
            {
                if (property == "z")
                    continue;

                Drawable target = property is "opacity" or "visible" or "x" or "y" or "width" or "height"
                    or "scale-x" or "scale-y" or "rotation" or "anchor" or "origin"
                    ? transform
                    : content;
                applyProperty(target, property, value);
            }
        }

        private static void applyProperty(Drawable drawable, string property, GameplaySkinScenePropertyValue value)
        {
            switch (property)
            {
                case "opacity" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.Alpha = (float)value.NumberValue;
                    break;

                case "visible" when value.Kind == GameplaySkinScenePropertyValueKind.Boolean:
                    drawable.Alpha = value.BooleanValue ? Math.Max(drawable.Alpha, 1) : 0;
                    break;

                case "x" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.RelativePositionAxes |= Axes.X;
                    drawable.X = (float)value.NumberValue;
                    break;

                case "y" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.RelativePositionAxes |= Axes.Y;
                    drawable.Y = (float)value.NumberValue;
                    break;

                case "width" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.RelativeSizeAxes |= Axes.X;
                    drawable.Width = (float)value.NumberValue;
                    break;

                case "height" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.RelativeSizeAxes |= Axes.Y;
                    drawable.Height = (float)value.NumberValue;
                    break;

                case "scale-x" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.Scale = new Vector2((float)value.NumberValue, drawable.Scale.Y);
                    break;

                case "scale-y" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.Scale = new Vector2(drawable.Scale.X, (float)value.NumberValue);
                    break;

                case "rotation" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.Rotation = (float)value.NumberValue;
                    break;

                case "z" when value.Kind == GameplaySkinScenePropertyValueKind.Number:
                    drawable.Depth = (float)value.NumberValue;
                    break;

                case "anchor" when value.Kind == GameplaySkinScenePropertyValueKind.String:
                    drawable.Anchor = anchor(value.StringValue);
                    break;

                case "origin" when value.Kind == GameplaySkinScenePropertyValueKind.String:
                    drawable.Origin = anchor(value.StringValue);
                    break;

                case "colour" when value.Kind == GameplaySkinScenePropertyValueKind.String:
                    drawable.Colour = colour(value.StringValue);
                    break;

                case "font-size" when value.Kind == GameplaySkinScenePropertyValueKind.Number && drawable is SpriteText text:
                    text.Font = text.Font.With(size: (float)value.NumberValue);
                    break;

                case "text" when drawable is SpriteText text:
                    text.Text = value.StringValue ?? string.Empty;
                    break;

                case "fill-mode" when value.Kind == GameplaySkinScenePropertyValueKind.String && drawable is Sprite sprite:
                    sprite.FillMode = value.StringValue switch
                    {
                        "stretch" => FillMode.Stretch,
                        "fit" => FillMode.Fit,
                        "fill" => FillMode.Fill,
                        _ => throw new InvalidOperationException(),
                    };
                    break;

                case "alignment" when value.Kind == GameplaySkinScenePropertyValueKind.String && drawable is SpriteText alignedText:
                    (alignedText.Anchor, alignedText.Origin) = value.StringValue switch
                    {
                        "left" => (Anchor.CentreLeft, Anchor.CentreLeft),
                        "centre" => (Anchor.Centre, Anchor.Centre),
                        "right" => (Anchor.CentreRight, Anchor.CentreRight),
                        _ => throw new InvalidOperationException(),
                    };
                    break;

                case "mask-mode" when value.Kind == GameplaySkinScenePropertyValueKind.String && drawable is GameplaySkinShapeMaskContainer mask:
                    if (value.StringValue != "ellipse")
                        throw new InvalidOperationException();

                    mask.Masking = true;
                    break;

                case "clip-mode" when value.Kind == GameplaySkinScenePropertyValueKind.String && drawable is Container clip:
                    if (value.StringValue is not ("bounds" or "rounded"))
                        throw new InvalidOperationException();

                    clip.Masking = true;

                    if (value.StringValue == "bounds")
                        clip.CornerRadius = 0;
                    break;

                case "corner-radius" when value.Kind == GameplaySkinScenePropertyValueKind.Number && drawable is Container roundedClip:
                    roundedClip.CornerRadius = (float)value.NumberValue;
                    roundedClip.Masking = true;
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }

        private static void applyGeometryAndBlend(
            Drawable drawable,
            GameplaySkinLayoutRect rect,
            GameplaySkinLayoutRect? parentRect,
            GameplaySkinSceneBlendMode blend)
        {
            GameplaySkinLayoutRect relative = parentRect.HasValue ? relativeTo(rect, parentRect.Value) : rect;
            drawable.RelativePositionAxes = Axes.Both;
            drawable.RelativeSizeAxes = Axes.Both;
            drawable.Position = new Vector2(relative.X, relative.Y);
            drawable.Size = new Vector2(relative.Width, relative.Height);
            drawable.Blending = ResolveBlend(blend);
        }

        internal static BlendingParameters ResolveBlend(GameplaySkinSceneBlendMode blend)
            => blend switch
            {
                GameplaySkinSceneBlendMode.Inherit => BlendingParameters.Inherit,
                GameplaySkinSceneBlendMode.Alpha => BlendingParameters.Mixture,
                GameplaySkinSceneBlendMode.Additive => BlendingParameters.Additive,
                GameplaySkinSceneBlendMode.Multiply => new BlendingParameters
                {
                    RGBEquation = BlendingEquation.Add,
                    Source = BlendingType.DstColor,
                    Destination = BlendingType.Zero,
                    AlphaEquation = BlendingEquation.Add,
                    SourceAlpha = BlendingType.One,
                    DestinationAlpha = BlendingType.OneMinusSrcAlpha,
                },
                GameplaySkinSceneBlendMode.Screen => new BlendingParameters
                {
                    RGBEquation = BlendingEquation.Add,
                    Source = BlendingType.One,
                    Destination = BlendingType.OneMinusSrcColor,
                    AlphaEquation = BlendingEquation.Add,
                    SourceAlpha = BlendingType.One,
                    DestinationAlpha = BlendingType.OneMinusSrcAlpha,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(blend)),
            };

        private static GameplaySkinLayoutRect relativeTo(GameplaySkinLayoutRect rect, GameplaySkinLayoutRect parent)
            => GameplaySkinLayoutRect.Create(
                (rect.X - parent.X) / parent.Width,
                (rect.Y - parent.Y) / parent.Height,
                rect.Width / parent.Width,
                rect.Height / parent.Height);

        private static string objectStateId(GameplaySkinObjectState state) => state switch
        {
            GameplaySkinObjectState.Scheduled => "scheduled",
            GameplaySkinObjectState.Visible => "visible",
            GameplaySkinObjectState.Holding => "holding",
            GameplaySkinObjectState.Hit => "hit",
            GameplaySkinObjectState.Missed => "missed",
            GameplaySkinObjectState.Completed => "completed",
            GameplaySkinObjectState.Despawned => "despawned",
            _ => "scheduled",
        };

        private static string judgementGradeId(GameplaySkinJudgementGrade grade) => grade switch
        {
            GameplaySkinJudgementGrade.Miss => "miss",
            GameplaySkinJudgementGrade.Meh => "meh",
            GameplaySkinJudgementGrade.Ok => "ok",
            GameplaySkinJudgementGrade.Good => "good",
            GameplaySkinJudgementGrade.Great => "great",
            GameplaySkinJudgementGrade.Perfect => "perfect",
            _ => "miss",
        };

        private static string bgaStateId(GameplaySkinBgaContentState state) => state switch
        {
            GameplaySkinBgaContentState.Empty => "empty",
            GameplaySkinBgaContentState.Ready => "ready",
            GameplaySkinBgaContentState.Playing => "playing",
            GameplaySkinBgaContentState.Paused => "paused",
            GameplaySkinBgaContentState.Failed => "failed",
            _ => "empty",
        };

        private static bool isEventDriven(GameplaySkinSlotDescriptor descriptor)
            => ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Mine)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BarLine)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.KeyFlash)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Laser)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.HitExplosion)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.JudgementDisplay);

        private static bool timingEquals(GameplaySkinTimingStateSnapshot first, GameplaySkinTimingStateSnapshot second)
            => first.Beat.Equals(second.Beat)
               && first.BarIndex == second.BarIndex
               && first.Bpm.Equals(second.Bpm)
               && first.IsStopped == second.IsStopped
               && first.ScrollMultiplier.Equals(second.ScrollMultiplier);

        internal static GameplaySkinSceneLayer LayerFor(GameplaySkinSlotDescriptor descriptor)
            => GameplaySkinSceneHostPolicy.LayerFor(descriptor);

        private void addFault(string code)
        {
            if (runtimeFaults.Count < max_runtime_faults)
                runtimeFaults.Add(new GameplaySkinSceneRuntimeFault(code));
        }

        private static double getNumber(IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties, string key, double fallback)
            => properties.TryGetValue(key, out GameplaySkinScenePropertyValue? value)
               && value.Kind == GameplaySkinScenePropertyValueKind.Number
                ? value.NumberValue
                : fallback;

        private static string getString(IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties, string key, string fallback)
            => properties.TryGetValue(key, out GameplaySkinScenePropertyValue? value)
               && value.Kind == GameplaySkinScenePropertyValueKind.String
                ? value.StringValue ?? fallback
                : fallback;

        private static Anchor anchor(string? value) => value switch
        {
            "top-centre" => Anchor.TopCentre,
            "top-right" => Anchor.TopRight,
            "centre-left" => Anchor.CentreLeft,
            "centre" => Anchor.Centre,
            "centre-right" => Anchor.CentreRight,
            "bottom-left" => Anchor.BottomLeft,
            "bottom-centre" => Anchor.BottomCentre,
            "bottom-right" => Anchor.BottomRight,
            _ => Anchor.TopLeft,
        };

        private static Color4 colour(string? value)
        {
            if (value == null || value.Length is not (7 or 9) || value[0] != '#')
                return Color4.White;

            return new Color4(
                byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                value.Length == 9
                    ? byte.Parse(value.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    : byte.MaxValue);
        }

        private sealed record PendingNode(
            GameplaySkinPreparedSceneNode Node,
            Container? Parent,
            GameplaySkinLayoutRect? ParentRect,
            GameplaySkinSceneLayer? ParentLayer,
            bool ParentAllowsLayerDispatch);

        private sealed record PendingSemantic(
            GameplaySkinPreparedHostedSlot PreparedRoute,
            GameplaySkinPublicSlotMaterial Material);

        /// <summary>
        /// Bounded allocation-amortised minimum index. Deletions are lazy and validated against the authoritative
        /// active-object dictionary, so every edge is O(log n) without rebuilding all active-object indexes.
        /// </summary>
        private sealed class ObjectIdMinHeap
        {
            private readonly List<long> values = new List<long>();

            public int Count => values.Count;

            public void Clear() => values.Clear();

            public void Add(long value)
            {
                int index = values.Count;
                values.Add(value);

                while (index > 0)
                {
                    int parent = (index - 1) / 2;

                    if (values[parent] <= value)
                        break;

                    values[index] = values[parent];
                    index = parent;
                }

                values[index] = value;
            }

            public bool TryPeek(out long value)
            {
                if (values.Count == 0)
                {
                    value = 0;
                    return false;
                }

                value = values[0];
                return true;
            }

            public void RemoveMin()
            {
                int lastIndex = values.Count - 1;

                if (lastIndex < 0)
                    return;

                long replacement = values[lastIndex];
                values.RemoveAt(lastIndex);

                if (lastIndex == 0)
                    return;

                int index = 0;

                while (true)
                {
                    int left = index * 2 + 1;

                    if (left >= values.Count)
                        break;

                    int right = left + 1;
                    int child = right < values.Count && values[right] < values[left] ? right : left;

                    if (values[child] >= replacement)
                        break;

                    values[index] = values[child];
                    index = child;
                }

                values[index] = replacement;
            }
        }

        private sealed class SceneOwnerBuild
        {
            private readonly List<GameplaySkinSceneRuntimeNode> nodes = new List<GameplaySkinSceneRuntimeNode>();
            private readonly List<(Container Parent, Drawable Root)> roots = new List<(Container, Drawable)>();

            public GameplaySkinSceneHostedSlot Gate { get; }

            public int ExpectedNodes { get; }

            public int CompletedNodes => nodes.Count;

            public int RuntimeEffectCost { get; private set; }

            public int RuntimeTextGlyphCost { get; private set; }

            public bool Failed { get; set; }

            public IReadOnlyList<GameplaySkinSceneRuntimeNode> Nodes => nodes;

            public IReadOnlyList<(Container Parent, Drawable Root)> Roots => roots;

            public Drawable? FirstRoot => roots.Count == 0 ? null : roots[0].Root;

            public SceneOwnerBuild(GameplaySkinSceneHostedSlot gate, int expectedNodes)
            {
                Gate = gate;
                ExpectedNodes = expectedNodes;
            }

            public void Add(
                GameplaySkinSceneRuntimeNode node,
                Container? rootParent,
                Drawable? root,
                int effectCost,
                int textGlyphCost)
            {
                nodes.Add(node);
                RuntimeEffectCost = checked(RuntimeEffectCost + effectCost);
                RuntimeTextGlyphCost = checked(RuntimeTextGlyphCost + textGlyphCost);

                if (rootParent != null && root != null)
                    roots.Add((rootParent, root));
            }
        }

        private sealed record SemanticVisual(
            GameplaySkinResolvedMaterialEntry Entry,
            Container Drawable,
            Sprite Sprite,
            OsuSpriteText? Text);

        private static int countPreparedNodes(GameplaySkinPreparedSceneNode node)
            => checked(1 + node.Children.Sum(countPreparedNodes));

        private sealed class RegisteredProgrammaticVisual : IDisposable
        {
            private readonly GameplaySkinSceneRuntimeHost host;
            private readonly IReadOnlyList<GameplaySkinSceneHostedSlot> anyGates;
            private readonly IReadOnlyList<GameplaySkinSceneHostedSlot> allGates;
            private readonly Drawable wrapper;
            private readonly float originalAlpha;
            private bool hiddenByHost;
            private bool disposed;

            public RegisteredProgrammaticVisual(
                GameplaySkinSceneRuntimeHost host,
                IReadOnlyList<GameplaySkinSceneHostedSlot> anyGates,
                IReadOnlyList<GameplaySkinSceneHostedSlot> allGates,
                Drawable wrapper)
            {
                this.host = host;
                this.anyGates = anyGates;
                this.allGates = allGates;
                this.wrapper = wrapper;
                originalAlpha = wrapper.Alpha;
            }

            public void Refresh(bool sceneReady)
            {
                if (disposed)
                    return;

                bool shouldHide = sceneReady
                                  && (anyGates.Any(gate => gate.SuppressesProgrammaticVisual)
                                      || (allGates.Count > 0 && allGates.All(gate => gate.SuppressesProgrammaticVisual)));

                if (shouldHide)
                {
                    wrapper.Alpha = 0;
                    hiddenByHost = true;
                }
                else if (hiddenByHost)
                {
                    wrapper.Alpha = originalAlpha;
                    hiddenByHost = false;
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;

                if (hiddenByHost)
                    wrapper.Alpha = originalAlpha;

                host.unregister(this);
            }
        }

        private sealed class StateMachineInstance
        {
            public GameplaySkinPreparedSceneStateMachine Machine { get; }

            public GameplaySkinResolvedMaterialTarget Target { get; }

            public GameplaySkinSceneTarget SceneTarget { get; }

            public string RuntimeScopeId { get; }

            public string PublicKey { get; }

            public string StateId { get; set; }

            public long? BoundObjectId { get; set; }

            public StateMachineInstance(
                GameplaySkinPreparedSceneStateMachine machine,
                GameplaySkinResolvedMaterialTarget target,
                GameplaySkinSceneTarget sceneTarget,
                string runtimeScopeId,
                string publicKey,
                long? boundObjectId)
            {
                Machine = machine;
                Target = target;
                SceneTarget = sceneTarget;
                RuntimeScopeId = runtimeScopeId;
                PublicKey = publicKey;
                StateId = machine.InitialStateId;
                BoundObjectId = boundObjectId;
            }
        }

        internal sealed partial class GameplaySkinShapeMaskContainer : CircularContainer
        {
        }
    }
}
