// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The single bounded, read-only dispatcher for one gameplay skin event lifetime.
    /// </summary>
    /// <remarks>
    /// Skin code can only subscribe and dequeue immutable envelopes. Creation and publication are internal engine APIs.
    /// A slow consumer causes an explicit atomic publish failure; no edge is silently discarded. An explicit reset clears
    /// queued old-epoch edges and supplies every attached consumer with a complete replacement state.
    /// </remarks>
    public sealed class GameplaySkinEventStream : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<GameplaySkinEventSubscription> subscriptions = new List<GameplaySkinEventSubscription>();
        private readonly GameplaySkinEventStreamCursor producerCursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);
        private readonly int pendingEventBudget;

        private GameplaySkinEventRevision revision;
        private MutableState state;
        private GameplaySkinEventRecord latestRecord;
        private double latestGameplayTime;
        private GameplaySkinEventProducer? producer;
        private bool disposed;

        /// <summary>
        /// Exact committed publication for a production stream. Null only for isolated event-contract tests which
        /// cannot be attached to a scene runtime host.
        /// </summary>
        internal GameplaySkinLayoutPublication? Publication { get; }

        public long CurrentEpoch
        {
            get
            {
                lock (sync)
                    return latestRecord.Epoch;
            }
        }

        public GameplaySkinEventRevision CurrentRevision
        {
            get
            {
                lock (sync)
                    return revision;
            }
        }

        internal GameplaySkinEventStream(
            GameplaySkinLayoutPublication publication,
            double gameplayTime,
            GameplaySkinEventStateSnapshot initialState,
            int pendingEventBudget = GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION)
            : this(
                (publication ?? throw new ArgumentNullException(nameof(publication))).EventRevision,
                gameplayTime,
                initialState,
                pendingEventBudget)
        {
            Publication = publication;
        }

        /// <summary>
        /// Isolated event-contract constructor. A stream without an exact publication is intentionally rejected by
        /// the production scene renderer even when its revision vector happens to compare equal.
        /// </summary>
        internal GameplaySkinEventStream(
            GameplaySkinEventRevision revision,
            double gameplayTime,
            GameplaySkinEventStateSnapshot initialState,
            int pendingEventBudget = GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION)
        {
            ArgumentNullException.ThrowIfNull(initialState);

            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime), gameplayTime, "Gameplay event time must be finite.");

            initialState.ValidateForGameplayTime(gameplayTime);

            if (pendingEventBudget <= 0 || pendingEventBudget > GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION)
                throw new ArgumentOutOfRangeException(nameof(pendingEventBudget));

            this.revision = revision;
            this.pendingEventBudget = pendingEventBudget;
            state = new MutableState(initialState, allowInitialResynchronisation: false);
            latestRecord = createRecord(
                epoch: 0,
                sequence: 0,
                gameplayTime,
                revision,
                state.Timing,
                null,
                null,
                GameplaySkinEventValue.Snapshot(state.CreateSnapshot(gameplayTime)));
            latestGameplayTime = gameplayTime;
            producerCursor.ValidateAndAdvance(latestRecord);
        }

        /// <summary>
        /// Atomically attaches a bounded consumer queue whose first item is a complete snapshot at the current high-water mark.
        /// </summary>
        public GameplaySkinEventSubscription Subscribe()
        {
            lock (sync)
            {
                throwIfDisposed();

                if (subscriptions.Count >= GameplaySkinEventBudgets.MAX_SUBSCRIPTIONS)
                    throw new InvalidOperationException($"The gameplay skin event stream has reached its {GameplaySkinEventBudgets.MAX_SUBSCRIPTIONS}-subscription hard budget.");

                var subscription = new GameplaySkinEventSubscription(this, pendingEventBudget);
                subscription.Enqueue(createAttachSnapshot());
                subscriptions.Add(subscription);
                return subscription;
            }
        }

        internal GameplaySkinEventProducer CreateProducer()
        {
            lock (sync)
            {
                throwIfDisposed();

                if (producer != null)
                    throw new InvalidOperationException("A gameplay skin event stream can have only one active engine producer.");

                producer = new GameplaySkinEventProducer(this, Environment.CurrentManagedThreadId);
                return producer;
            }
        }

        internal void Publish(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinEventValue payload,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId)
            => publish(source, gameplayTime, payload, groupId, laneId);

        private void publish(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinEventValue payload,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId)
        {

            lock (sync)
            {
                validateProducer(source);

                if (payload.DeliveryKind != GameplaySkinEventDeliveryKind.Edge)
                    throw new ArgumentException("The edge publication API accepts only edge payloads.", nameof(payload));

                if (latestRecord.Sequence == long.MaxValue)
                    throw new InvalidOperationException("The current gameplay skin event sequence is exhausted; an explicit reset is required.");

                if (gameplayTime < latestGameplayTime)
                    throw new InvalidOperationException("Gameplay skin event time cannot move backwards within an epoch.");

                ensureEveryQueueHasCapacity();

                state.ValidateApply(payload, groupId, laneId);
                GameplaySkinTimingStateSnapshot authoritativeTiming = payload.Family == GameplaySkinEventPayloadFamily.Timing
                    ? payload.GetTiming()
                    : state.Timing;
                GameplaySkinEventRecord record = createRecord(
                    latestRecord.Epoch,
                    latestRecord.Sequence + 1,
                    gameplayTime,
                    revision,
                    authoritativeTiming,
                    groupId,
                    laneId,
                    payload);

                producerCursor.ValidateAndAdvance(record);
                state.ApplyValidated(payload, groupId, laneId, gameplayTime);
                latestRecord = record;
                latestGameplayTime = gameplayTime;

                foreach (GameplaySkinEventSubscription subscription in subscriptions)
                    subscription.Enqueue(record);
            }
        }

        internal void SynchroniseTiming(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinTimingStateSnapshot timing)
        {
            lock (sync)
            {
                validateProducer(source);

                if (!double.IsFinite(gameplayTime) || gameplayTime < latestGameplayTime)
                    throw new InvalidOperationException("Gameplay skin timing cannot move backwards within an epoch.");

                state.SynchroniseTiming(timing);
                latestGameplayTime = gameplayTime;
            }
        }

        /// <summary>
        /// Reads the exact timing high-water visible to one bounded consumer. A renderer with pending records remains
        /// pinned to its last consumed record; only an empty queue may observe the stream's newer fractional sample.
        /// </summary>
        internal void ReadConsumerTimingHighWater(
            GameplaySkinEventSubscription subscription,
            double consumedGameplayTime,
            GameplaySkinTimingStateSnapshot consumedTiming,
            out double gameplayTime,
            out GameplaySkinTimingStateSnapshot timing)
        {
            lock (sync)
            {
                throwIfDisposed();

                if (!subscriptions.Contains(subscription) || subscription.IsDisposed)
                    throw new InvalidOperationException("A detached gameplay skin event consumer has no timing high-water.");

                if (subscription.PendingCountUnsafe > 0)
                {
                    gameplayTime = consumedGameplayTime;
                    timing = consumedTiming;
                }
                else
                {
                    gameplayTime = latestGameplayTime;
                    timing = state.Timing;
                }
            }
        }

        internal GameplaySkinEventProducer Reset(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinEventStateSnapshot completeState,
            GameplaySkinEventResetReason reason)
            => reset(source, gameplayTime, revision, GameplaySkinEventValue.Reset(completeState, reason));

        /// <summary>
        /// Announces the exact publication which created this production stream. The prepared attach snapshot remains
        /// epoch zero; this complete epoch-one anchor replaces it with the engine-synchronised initial state and makes
        /// the layout/publication event observable to consumers which attached with the exact root.
        /// </summary>
        internal GameplaySkinEventProducer AnnounceCurrentPublication(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinEventStateSnapshot completeState)
        {
            lock (sync)
            {
                validateProducer(source);

                if (latestRecord.Epoch != 0
                    || latestRecord.Sequence != 0
                    || latestRecord.DeliveryKind != GameplaySkinEventDeliveryKind.Snapshot)
                {
                    throw new InvalidOperationException("The current gameplay skin publication can be announced only once from its prepared attach snapshot.");
                }

                return reset(source, gameplayTime, revision, GameplaySkinEventValue.Publication(completeState));
            }
        }

        private GameplaySkinEventProducer reset(
            GameplaySkinEventProducer source,
            double gameplayTime,
            GameplaySkinEventRevision newRevision,
            GameplaySkinEventValue payload)
        {
            lock (sync)
            {
                validateProducer(source);

                if (payload.DeliveryKind != GameplaySkinEventDeliveryKind.Reset)
                    throw new ArgumentException("The epoch reset API requires a complete reset payload.", nameof(payload));

                if (latestRecord.Epoch == long.MaxValue)
                    throw new InvalidOperationException("The gameplay skin event epoch is exhausted and cannot wrap.");

                long previousEpoch = latestRecord.Epoch;
                GameplaySkinEventStateSnapshot completeState = payload.CompleteState;
                completeState.ValidateForGameplayTime(gameplayTime);

                var candidateState = new MutableState(completeState, allowInitialResynchronisation: true);
                GameplaySkinEventRecord record = createRecord(
                    latestRecord.Epoch + 1,
                    0,
                    gameplayTime,
                    newRevision,
                    completeState.Timing,
                    null,
                    null,
                    payload);

                producerCursor.ValidateAndAdvance(record);

                revision = newRevision;
                state = candidateState;
                latestRecord = record;
                latestGameplayTime = gameplayTime;
                source.Deactivate();
                producer = new GameplaySkinEventProducer(this, source.ProducingThreadId);

                foreach (GameplaySkinEventSubscription subscription in subscriptions)
                {
                    subscription.ClearPending();

                    if (subscription.LastAcceptedEpoch == previousEpoch)
                        subscription.Enqueue(record);
                    else
                        subscription.Reattach(createAttachSnapshot());
                }

                return producer;
            }
        }

        internal void ReleaseProducer(GameplaySkinEventProducer source)
        {
            lock (sync)
            {
                if (ReferenceEquals(producer, source))
                {
                    source.Deactivate();
                    producer = null;
                }
            }
        }

        internal bool TryDequeue(GameplaySkinEventSubscription subscription, out GameplaySkinEventEnvelope? envelope)
        {
            lock (sync)
            {
                if (disposed || !subscriptions.Contains(subscription) || subscription.IsDisposed)
                {
                    envelope = null;
                    return false;
                }

                return subscription.TryDequeueValidated(out envelope);
            }
        }

        internal bool TryDequeue(GameplaySkinEventSubscription subscription, out GameplaySkinEventRecord record)
        {
            lock (sync)
            {
                if (disposed || !subscriptions.Contains(subscription) || subscription.IsDisposed)
                {
                    record = default;
                    return false;
                }

                return subscription.TryDequeueValidated(out record);
            }
        }

        internal void Unsubscribe(GameplaySkinEventSubscription subscription)
        {
            lock (sync)
            {
                if (subscriptions.Remove(subscription))
                    subscription.MarkDisposed();
            }
        }

        internal int GetPendingCount(GameplaySkinEventSubscription subscription)
        {
            lock (sync)
                return subscriptions.Contains(subscription) && !subscription.IsDisposed ? subscription.PendingCountUnsafe : 0;
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;
                producer?.Deactivate();
                producer = null;

                foreach (GameplaySkinEventSubscription subscription in subscriptions)
                    subscription.MarkDisposed();

                subscriptions.Clear();
            }
        }

        private GameplaySkinEventRecord createAttachSnapshot()
            => createRecord(
                latestRecord.Epoch,
                latestRecord.Sequence,
                latestGameplayTime,
                revision,
                state.Timing,
                null,
                null,
                GameplaySkinEventValue.Snapshot(state.CreateSnapshot(latestGameplayTime)));

        private void ensureEveryQueueHasCapacity()
        {
            foreach (GameplaySkinEventSubscription subscription in subscriptions)
            {
                if (subscription.PendingCountUnsafe >= pendingEventBudget)
                    throw new GameplaySkinEventBackpressureException("A gameplay skin event consumer queue is full; publication failed atomically and requires drain, detach or explicit reset.");
            }
        }

        private void validateProducer(GameplaySkinEventProducer source)
        {
            throwIfDisposed();
            ArgumentNullException.ThrowIfNull(source);

            if (!ReferenceEquals(producer, source) || !source.IsActive)
                throw new InvalidOperationException("The gameplay skin event producer is detached or belongs to an older epoch.");

            if (source.ProducingThreadId != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("Gameplay skin events must be published in deterministic order from the producer's owning thread.");
        }

        private void throwIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static GameplaySkinEventRecord createRecord(
            long epoch,
            long sequence,
            double gameplayTime,
            GameplaySkinEventRevision revision,
            GameplaySkinTimingStateSnapshot authoritativeTiming,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinEventValue payload)
            => GameplaySkinEventRecord.Create(
                epoch,
                sequence,
                gameplayTime,
                revision,
                authoritativeTiming,
                groupId,
                laneId,
                payload);

        private sealed class MutableState
        {
            private GameplaySkinLifecycleState lifecycleState;
            private readonly Dictionary<GameplaySkinLaneId, GameplaySkinInputStateSnapshot> inputs;
            private readonly Dictionary<long, GameplaySkinObjectStateSnapshot> objects;
            private readonly HashSet<long> objectResynchronisationAllowed;
            private readonly Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot> judgementsByObject = new Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot>();
            private readonly Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByLane = new Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot>();
            private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByGroup = new Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot>();
            private GameplaySkinCurrentJudgementStateSnapshot? globalJudgement;
            private GameplaySkinScoreStateSnapshot score;
            private readonly Dictionary<int, GameplaySkinBgaStateSnapshot> bga;
            private readonly HashSet<int> bgaResynchronisationAllowed;

            public MutableState(GameplaySkinEventStateSnapshot snapshot, bool allowInitialResynchronisation)
            {
                lifecycleState = snapshot.LifecycleState;
                inputs = snapshot.Inputs.ToDictionary(input => input.LaneId);
                objects = snapshot.ActiveObjects.ToDictionary(obj => obj.ObjectId);
                objectResynchronisationAllowed = allowInitialResynchronisation
                    ? objects.Keys.ToHashSet()
                    : new HashSet<long>();
                restoreJudgements(snapshot.CurrentJudgements);
                score = snapshot.Score;
                Timing = snapshot.Timing;
                bga = snapshot.BgaViewports.ToDictionary(viewport => viewport.ViewportIndex);
                bgaResynchronisationAllowed = allowInitialResynchronisation
                    ? bga.Keys.ToHashSet()
                    : new HashSet<int>();
            }

            public void ValidateApply(
                GameplaySkinEventValue payload,
                GameplaySkinLaneGroupId? groupId,
                GameplaySkinLaneId? laneId)
            {
                switch (payload.Family)
                {
                    case GameplaySkinEventPayloadFamily.Input:
                        GameplaySkinInputStateSnapshot input = payload.GetInput(requireGroup(groupId), requireLane(laneId));

                        if (inputs.TryGetValue(input.LaneId, out GameplaySkinInputStateSnapshot previousInput))
                        {
                            if (input.GroupId != previousInput.GroupId)
                                throw new InvalidOperationException("A stable input lane cannot change group within an event epoch.");
                        }
                        else
                        {
                            ensureBudget(inputs.Count + 1, GameplaySkinEventBudgets.MAX_INPUT_STATES, "input-state");
                        }

                        break;

                    case GameplaySkinEventPayloadFamily.Object:
                        validateObject(payload.EventKind, payload.GetObject(requireGroup(groupId), laneId));
                        break;

                    case GameplaySkinEventPayloadFamily.Judgement:
                        GameplaySkinJudgementStateSnapshot judgement = payload.GetJudgement(groupId, laneId);

                        if (judgement.ObjectId.HasValue)
                        {
                            if (!objects.TryGetValue(judgement.ObjectId.Value, out GameplaySkinObjectStateSnapshot active))
                                throw new InvalidOperationException("An object judgement edge requires an active stable object ID.");

                            if (active.GroupId != judgement.GroupId || active.LaneId != judgement.LaneId)
                                throw new InvalidOperationException("An object judgement edge must retain the active object's exact group and lane identity.");
                        }

                        break;

                    case GameplaySkinEventPayloadFamily.Bga:
                        GameplaySkinBgaStateSnapshot bgaState = payload.GetBga();

                        if (bga.TryGetValue(bgaState.ViewportIndex, out GameplaySkinBgaStateSnapshot previousBga))
                        {
                            if (!bgaResynchronisationAllowed.Contains(bgaState.ViewportIndex)
                                && bgaState.ContentRevision < previousBga.ContentRevision)
                            {
                                throw new InvalidOperationException("BGA content revision cannot move backwards within an event epoch.");
                            }
                        }
                        else
                        {
                            ensureBudget(bga.Count + 1, GameplaySkinEventBudgets.MAX_BGA_VIEWPORTS, "bga-viewport");
                        }

                        break;

                    case GameplaySkinEventPayloadFamily.Lifecycle:
                    case GameplaySkinEventPayloadFamily.Score:
                    case GameplaySkinEventPayloadFamily.Timing:
                        break;

                    default:
                        throw new ArgumentException("The edge payload family is not supported by the production event stream.", nameof(payload));
                }
            }

            public void ApplyValidated(
                GameplaySkinEventValue payload,
                GameplaySkinLaneGroupId? groupId,
                GameplaySkinLaneId? laneId,
                double gameplayTime)
            {
                switch (payload.Family)
                {
                    case GameplaySkinEventPayloadFamily.Lifecycle:
                        lifecycleState = payload.GetLifecycle();
                        break;

                    case GameplaySkinEventPayloadFamily.Input:
                        GameplaySkinInputStateSnapshot input = payload.GetInput(requireGroup(groupId), requireLane(laneId));
                        inputs[input.LaneId] = input;
                        break;

                    case GameplaySkinEventPayloadFamily.Object:
                        applyValidatedObject(payload.EventKind, payload.GetObject(requireGroup(groupId), laneId));
                        break;

                    case GameplaySkinEventPayloadFamily.Judgement:
                        GameplaySkinJudgementStateSnapshot judgement = payload.GetJudgement(groupId, laneId);
                        double displayUntil = gameplayTime + GameplaySkinEventBudgets.JUDGEMENT_DISPLAY_DURATION;
                        globalJudgement = new GameplaySkinCurrentJudgementStateSnapshot(
                            GameplaySkinJudgementScope.Global,
                            judgement,
                            gameplayTime,
                            displayUntil);

                        if (judgement.ObjectId.HasValue)
                        {
                            judgementsByObject[judgement.ObjectId.Value] = new GameplaySkinCurrentJudgementStateSnapshot(
                                GameplaySkinJudgementScope.Object,
                                judgement,
                                gameplayTime,
                                displayUntil);
                        }

                        if (judgement.GroupId != null)
                        {
                            judgementsByGroup[judgement.GroupId] = new GameplaySkinCurrentJudgementStateSnapshot(
                                GameplaySkinJudgementScope.Group,
                                judgement,
                                gameplayTime,
                                displayUntil);
                        }

                        if (judgement.LaneId != null)
                        {
                            judgementsByLane[judgement.LaneId] = new GameplaySkinCurrentJudgementStateSnapshot(
                                GameplaySkinJudgementScope.Lane,
                                judgement,
                                gameplayTime,
                                displayUntil);
                        }
                        break;

                    case GameplaySkinEventPayloadFamily.Score:
                        score = payload.GetScore();
                        break;

                    case GameplaySkinEventPayloadFamily.Timing:
                        Timing = payload.GetTiming();
                        break;

                    case GameplaySkinEventPayloadFamily.Bga:
                        GameplaySkinBgaStateSnapshot bgaState = payload.GetBga();
                        bga[bgaState.ViewportIndex] = bgaState;

                        if (payload.EventKind == GameplaySkinEventKind.BgaContentStateChanged)
                            bgaResynchronisationAllowed.Remove(bgaState.ViewportIndex);

                        break;

                    default:
                        throw new ArgumentException("The edge payload family is not supported by the production event stream.", nameof(payload));
                }
            }

            public GameplaySkinEventStateSnapshot CreateSnapshot(double gameplayTime)
                => new GameplaySkinEventStateSnapshot(
                    lifecycleState,
                    inputs.Values.OrderBy(input => input.LaneId.Value, StringComparer.Ordinal),
                    objects.Values.OrderBy(obj => obj.ObjectId).Select(obj => GameplaySkinEventRuntimeHost.SnapshotObjectAt(obj, gameplayTime)),
                    currentJudgements(gameplayTime),
                    score,
                    Timing,
                    bga.Values.OrderBy(viewport => viewport.ViewportIndex));

            private IEnumerable<GameplaySkinCurrentJudgementStateSnapshot> currentJudgements(double gameplayTime)
            {
                foreach (GameplaySkinCurrentJudgementStateSnapshot retained in judgementsByObject.Values)
                {
                    if (retained.Judgement.ObjectId is long objectId && objects.ContainsKey(objectId))
                        yield return retained;
                }

                foreach (GameplaySkinCurrentJudgementStateSnapshot retained in judgementsByGroup.Values)
                {
                    if (retained.DisplayUntil > gameplayTime)
                        yield return retained;
                }

                foreach (GameplaySkinCurrentJudgementStateSnapshot retained in judgementsByLane.Values)
                {
                    if (retained.DisplayUntil > gameplayTime)
                        yield return retained;
                }

                if (globalJudgement is { } global && global.DisplayUntil > gameplayTime)
                    yield return global;
            }

            private void restoreJudgements(IEnumerable<GameplaySkinCurrentJudgementStateSnapshot> current)
            {
                foreach (GameplaySkinCurrentJudgementStateSnapshot retained in current)
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
                            throw new ArgumentOutOfRangeException(nameof(current));
                    }
                }
            }

            public void SynchroniseTiming(GameplaySkinTimingStateSnapshot state) => Timing = state;

            public GameplaySkinTimingStateSnapshot Timing { get; private set; }

            private void validateObject(GameplaySkinEventKind eventKind, GameplaySkinObjectStateSnapshot obj)
            {
                switch (eventKind)
                {
                    case GameplaySkinEventKind.ObjectSpawned:
                        if (objects.ContainsKey(obj.ObjectId))
                            throw new InvalidOperationException("An object spawn cannot reuse an active object ID.");

                        ensureBudget(objects.Count + 1, GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS, "active-object");
                        break;

                    case GameplaySkinEventKind.ObjectStateChanged:
                        if (!objects.TryGetValue(obj.ObjectId, out GameplaySkinObjectStateSnapshot previous))
                            throw new InvalidOperationException("An object state edge requires an active object ID.");

                        if (obj.Kind != previous.Kind
                            || obj.GroupId != previous.GroupId
                            || obj.LaneId != previous.LaneId
                            || obj.StartTime != previous.StartTime
                            || obj.EndTime != previous.EndTime)
                        {
                            throw new InvalidOperationException("An active object cannot change its stable identity, target or timeline within an event epoch.");
                        }

                        if (!objectResynchronisationAllowed.Contains(obj.ObjectId) && obj.Progress < previous.Progress)
                            throw new InvalidOperationException("Object progress cannot move backwards within an event epoch.");

                        break;

                    case GameplaySkinEventKind.ObjectDespawned:
                        if (!objects.TryGetValue(obj.ObjectId, out GameplaySkinObjectStateSnapshot despawned))
                            throw new InvalidOperationException("An object despawn requires an active object ID.");

                        if (obj.Kind != despawned.Kind
                            || obj.GroupId != despawned.GroupId
                            || obj.LaneId != despawned.LaneId
                            || obj.StartTime != despawned.StartTime
                            || obj.EndTime != despawned.EndTime
                            || (!objectResynchronisationAllowed.Contains(obj.ObjectId) && obj.Progress < despawned.Progress))
                        {
                            throw new InvalidOperationException("An object despawn must retain the active object's stable target and timeline.");
                        }

                        break;
                }
            }

            private void applyValidatedObject(GameplaySkinEventKind eventKind, GameplaySkinObjectStateSnapshot obj)
            {
                if (eventKind == GameplaySkinEventKind.ObjectDespawned)
                {
                    objects.Remove(obj.ObjectId);
                    judgementsByObject.Remove(obj.ObjectId);
                }
                else
                    objects[obj.ObjectId] = obj;

                objectResynchronisationAllowed.Remove(obj.ObjectId);
            }

            private static GameplaySkinLaneGroupId requireGroup(GameplaySkinLaneGroupId? groupId)
                => groupId ?? throw new InvalidOperationException("The gameplay skin event is missing its stable group target.");

            private static GameplaySkinLaneId requireLane(GameplaySkinLaneId? laneId)
                => laneId ?? throw new InvalidOperationException("The gameplay skin event is missing its stable lane target.");

            private static void ensureBudget(int count, int maximum, string family)
            {
                if (count > maximum)
                    throw new InvalidOperationException($"The gameplay skin event {family} hard budget of {maximum} entries was exceeded.");
            }
        }
    }

    /// <summary>
    /// One bounded pull subscription. It exposes no publication surface.
    /// </summary>
    public sealed class GameplaySkinEventSubscription : IDisposable
    {
        private readonly GameplaySkinEventStream owner;
        private readonly Queue<GameplaySkinEventRecord> pending;
        private readonly GameplaySkinEventStreamCursor cursor = new GameplaySkinEventStreamCursor(GameplaySkinEventApiVersions.V1);

        internal bool IsDisposed { get; private set; }

        internal long? LastAcceptedEpoch => cursor.LastAcceptedEpoch;

        internal int PendingCountUnsafe => pending.Count;

        public int PendingCount => owner.GetPendingCount(this);

        internal GameplaySkinEventSubscription(GameplaySkinEventStream owner, int capacity)
        {
            this.owner = owner;
            pending = new Queue<GameplaySkinEventRecord>(capacity);
        }

        /// <summary>
        /// Drains no more than the frozen per-frame budget in validated stream order.
        /// </summary>
        public int DrainFrame(Action<GameplaySkinEventEnvelope> consumer, int maximum = GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME)
        {
            ArgumentNullException.ThrowIfNull(consumer);

            if (maximum <= 0 || maximum > GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME)
                throw new ArgumentOutOfRangeException(nameof(maximum));

            int consumed = 0;

            while (consumed < maximum && TryDequeue(out GameplaySkinEventEnvelope? envelope))
            {
                consumer(envelope!);
                consumed++;
            }

            return consumed;
        }

        /// <summary>
        /// Production renderer path. It preserves the same ordering validation and frame budget without materialising
        /// public envelope/payload objects for every gameplay edge.
        /// </summary>
        internal int DrainProductionFrame(Action<GameplaySkinEventRecord> consumer, int maximum = GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME)
        {
            ArgumentNullException.ThrowIfNull(consumer);

            if (maximum <= 0 || maximum > GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME)
                throw new ArgumentOutOfRangeException(nameof(maximum));

            int consumed = 0;

            while (consumed < maximum && owner.TryDequeue(this, out GameplaySkinEventRecord record))
            {
                consumer(record);
                consumed++;
            }

            return consumed;
        }

        internal bool TryDequeue(out GameplaySkinEventEnvelope? envelope) => owner.TryDequeue(this, out envelope);

        public void Dispose() => owner.Unsubscribe(this);

        internal void Enqueue(GameplaySkinEventRecord record) => pending.Enqueue(record);

        internal void ClearPending() => pending.Clear();

        internal void Reattach(GameplaySkinEventRecord snapshot)
        {
            cursor.ResetForCompleteReattach();
            pending.Enqueue(snapshot);
        }

        internal bool TryDequeueValidated(out GameplaySkinEventEnvelope? envelope)
        {
            if (!pending.TryDequeue(out GameplaySkinEventRecord record))
            {
                envelope = null;
                return false;
            }

            cursor.ValidateAndAdvance(record);
            envelope = record.Materialize();
            return true;
        }

        internal bool TryDequeueValidated(out GameplaySkinEventRecord record)
        {
            if (!pending.TryDequeue(out record))
                return false;

            cursor.ValidateAndAdvance(record);
            return true;
        }

        internal void MarkDisposed()
        {
            IsDisposed = true;
            pending.Clear();
        }
    }

    internal sealed class GameplaySkinEventProducer : IDisposable
    {
        private readonly GameplaySkinEventStream stream;

        internal int ProducingThreadId { get; }

        internal bool IsActive { get; private set; } = true;

        internal GameplaySkinEventProducer(GameplaySkinEventStream stream, int producingThreadId)
        {
            this.stream = stream;
            ProducingThreadId = producingThreadId;
        }

        internal void SynchroniseTiming(double gameplayTime, GameplaySkinTimingStateSnapshot state)
            => stream.SynchroniseTiming(this, gameplayTime, state);

        internal GameplaySkinEventProducer Reset(
            double gameplayTime,
            GameplaySkinEventStateSnapshot completeState,
            GameplaySkinEventResetReason reason)
            => stream.Reset(this, gameplayTime, completeState, reason);

        internal GameplaySkinEventProducer AnnounceCurrentPublication(
            double gameplayTime,
            GameplaySkinEventStateSnapshot completeState)
            => stream.AnnounceCurrentPublication(this, gameplayTime, completeState);

        public void Dispose() => stream.ReleaseProducer(this);

        internal void Deactivate() => IsActive = false;
    }

    internal sealed class GameplaySkinEventBackpressureException : InvalidOperationException
    {
        public GameplaySkinEventBackpressureException(string message)
            : base(message)
        {
        }
    }
}
