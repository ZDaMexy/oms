// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Engine-only projection of a ruleset's already-resolved timing authority into the neutral C5 event contract.
    /// Implementations must be immutable, side-effect free and allocation-free per sample.
    /// </summary>
    internal interface IGameplaySkinTimingProjection
    {
        GameplaySkinTimingStateSnapshot Sample(double gameplayTime);
    }

    /// <summary>
    /// Engine-only source used to rebuild the complete active-object state at an event epoch barrier.
    /// </summary>
    /// <remarks>
    /// Implementations must enumerate the ruleset's actual active drawable usages, including their current state and
    /// exact C3 target. The event host owns progress sampling and defensively copies every value before publication;
    /// author scenes never receive this interface and cannot submit gameplay state.
    /// </remarks>
    internal interface IGameplaySkinEventObjectSnapshotSource
    {
        IEnumerable<GameplaySkinObjectStateSnapshot> CreateGameplaySkinActiveObjectSnapshot(double gameplayTime);
    }

    /// <summary>
    /// Engine-owned bridge from production gameplay state into the one read-only C5 event stream.
    /// </summary>
    /// <remarks>
    /// Rulesets may submit neutral primitives through internal methods; author code can only obtain
    /// <see cref="EventStream"/> and subscribe. This drawable runs below the frame-stable gameplay clock and stamps
    /// every edge with that authoritative time.
    /// </remarks>
    public partial class GameplaySkinEventRuntimeHost : CompositeDrawable
    {
        private readonly IGameplaySkinTimingProjection timingProjection;
        private readonly IGameplaySkinEventObjectSnapshotSource? objectSnapshotSource;
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinInputStateSnapshot> inputs;
        private readonly Dictionary<long, GameplaySkinObjectStateSnapshot> activeObjects;
        private readonly Dictionary<int, GameplaySkinBgaStateSnapshot> bga;
        private readonly Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot> judgementsByObject = new Dictionary<long, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByLane = new Dictionary<GameplaySkinLaneId, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot> judgementsByGroup = new Dictionary<GameplaySkinLaneGroupId, GameplaySkinCurrentJudgementStateSnapshot>();
        private readonly HashSet<long> objectResynchronisationAllowed = new HashSet<long>();
        private readonly HashSet<int> bgaResynchronisationAllowed = new HashSet<int>();

        private GameplaySkinEventProducer? producer;
        private ScoreProcessor? scoreProcessor;
        private HealthProcessor? healthProcessor;
        private GameplayClockContainer? gameplayClockContainer;
        private GameplaySkinLifecycleState lifecycle;
        private GameplaySkinCurrentJudgementStateSnapshot? globalJudgement;
        private GameplaySkinScoreStateSnapshot score;
        private GameplaySkinTimingStateSnapshot timing;
        private GameplaySkinEventResetReason pendingReset;
        private double previousTime = double.NaN;
        private long previousBeat = long.MinValue;
        private long previousBar = long.MinValue;
        private double previousBpm = double.NaN;
        private double previousScroll = double.NaN;
        private bool timingStopped;
        private GameplaySkinEventKind? lastLifecycleEvent;
        private bool hasStarted;
        private bool observedFailure;
        private bool disposed;
        private bool objectCapacityExceeded;
        private bool deferDiscontinuityResetForDrawableState;
        private double? pendingDiscontinuityGameplayTime;

        public GameplaySkinEventStream EventStream { get; }

        public GameplaySkinLayoutPublication Publication { get; }

        /// <summary>
        /// Engine-only time used for ruleset summaries which must cross a seek atomically with this event host.
        /// It is never exposed through the author-facing stream except as the stamped envelope time.
        /// </summary>
        internal double AuthoritativeGameplayTime => pendingDiscontinuityGameplayTime ?? currentTime;

        public GameplaySkinEventRuntimeHost(GameplaySkinLayoutPublication publication, IBeatmap beatmap)
            : this(publication, beatmap, null, null)
        {
        }

        internal GameplaySkinEventRuntimeHost(
            GameplaySkinLayoutPublication publication,
            IBeatmap beatmap,
            IGameplaySkinTimingProjection? timingProjection,
            IGameplaySkinEventObjectSnapshotSource? objectSnapshotSource)
        {
            Publication = publication ?? throw new ArgumentNullException(nameof(publication));
            ArgumentNullException.ThrowIfNull(beatmap);
            this.timingProjection = timingProjection ?? new GameplaySkinBeatmapTimingProjection(beatmap);
            this.objectSnapshotSource = objectSnapshotSource;

            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            inputs = initial.Inputs.ToDictionary(input => input.LaneId);
            activeObjects = initial.ActiveObjects.ToDictionary(obj => obj.ObjectId);
            bga = initial.BgaViewports.ToDictionary(viewport => viewport.ViewportIndex);
            lifecycle = initial.LifecycleState;
            restoreJudgements(initial.CurrentJudgements);
            score = initial.Score;
            timing = initial.Timing;
            timingStopped = initial.Timing.IsStopped;
            hasStarted = lifecycle is not GameplaySkinLifecycleState.Loaded;
            EventStream = new GameplaySkinEventStream(publication, 0, buildSnapshot(0));
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ScoreProcessor? scoreProcessor, HealthProcessor? healthProcessor, GameplayClockContainer? gameplayClockContainer)
        {
            this.scoreProcessor = scoreProcessor;
            this.healthProcessor = healthProcessor;
            this.gameplayClockContainer = gameplayClockContainer;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            producer = EventStream.CreateProducer();

            synchroniseScoreState();
            timing = createTimingState(currentTime);
            timingStopped = timing.IsStopped;
            double announcementTime = currentTime;
            synchroniseActiveObjectsFromEngine(announcementTime);
            producer = producer.AnnounceCurrentPublication(announcementTime, buildSnapshot(announcementTime));
            allowOneStateResynchronisation();
            previousBeat = (long)Math.Floor(timing.Beat);
            previousBar = timing.BarIndex;
            previousBpm = timing.Bpm;
            previousScroll = timing.ScrollMultiplier;

            if (lifecycle == GameplaySkinLifecycleState.Loaded)
                publishLifecycle(GameplaySkinEventKind.GameplayLoaded, GameplaySkinLifecycleState.Loaded);

            foreach (int viewportIndex in bga.Keys.Order())
                PublishBgaViewport(viewportIndex);

            if (scoreProcessor != null)
            {
                scoreProcessor.TotalScore.ValueChanged += onTotalScoreChanged;
                scoreProcessor.Combo.ValueChanged += onComboChanged;
                scoreProcessor.HighestCombo.ValueChanged += onHighestComboChanged;
                scoreProcessor.Accuracy.ValueChanged += onAccuracyChanged;
                scoreProcessor.HasCompleted.ValueChanged += onCompletedChanged;
                scoreProcessor.OnResetFromReplayFrame += onReplayReset;
            }

            if (healthProcessor != null)
            {
                healthProcessor.Health.ValueChanged += onHealthChanged;
            }

            if (gameplayClockContainer != null)
            {
                gameplayClockContainer.OnSeek += onSeek;
                gameplayClockContainer.OnReset += onRetry;
                gameplayClockContainer.IsPaused.ValueChanged += onPausedChanged;
                SetPaused(gameplayClockContainer.IsPaused.Value);
            }


            refreshFailureLifecycle();
            refreshCompletionLifecycle();
        }

        /// <summary>
        /// Idempotently projects the engine-owned pause state into Started/Paused/Resumed edges.
        /// </summary>
        internal void SetPaused(bool paused)
        {
            if (lifecycle is GameplaySkinLifecycleState.Completed or GameplaySkinLifecycleState.Failed)
                return;

            if (paused)
            {
                if (hasStarted && lifecycle == GameplaySkinLifecycleState.Running)
                    publishLifecycle(GameplaySkinEventKind.GameplayPaused, GameplaySkinLifecycleState.Paused);

                return;
            }

            if (!hasStarted)
            {
                hasStarted = true;
                publishLifecycle(GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            }
            else if (lifecycle == GameplaySkinLifecycleState.Paused)
            {
                publishLifecycle(GameplaySkinEventKind.GameplayResumed, GameplaySkinLifecycleState.Running);
            }
        }

        internal void PublishInput(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId, bool pressed, float strength = 1)
        {
            validateExactTarget(groupId, laneId);
            var state = new GameplaySkinInputStateSnapshot(groupId, laneId, pressed, pressed ? strength : 0);
            inputs[laneId] = state;
            publish(
                GameplaySkinEventValue.Input(pressed ? GameplaySkinEventKind.InputPressed : GameplaySkinEventKind.InputReleased, state),
                groupId,
                laneId);
        }

        /// <summary>
        /// Samples object progress from the same engine-owned gameplay clock used to stamp event envelopes.
        /// Ruleset drawables must not derive event payload state from their presentation clock: during seek/reset
        /// that clock may still expose the previous frame while <see cref="GameplayClockContainer"/> has already
        /// switched atomically to the new authoritative time.
        /// </summary>
        internal double GetObjectProgress(double startTime, double endTime)
        {
            double duration = endTime - startTime;
            return duration <= 0
                ? currentTime < startTime ? 0 : 1
                : Math.Clamp((currentTime - startTime) / duration, 0, 1);
        }

        internal void PublishObject(
            long objectId,
            GameplaySkinEventKind kind,
            GameplaySkinObjectKind objectKind,
            GameplaySkinObjectState objectState,
            GameplaySkinLaneGroupId groupId,
            GameplaySkinLaneId? laneId,
            double startTime,
            double endTime,
            double progress)
        {
            validateExactTarget(groupId, laneId);
            var state = new GameplaySkinObjectStateSnapshot(objectId, objectKind, objectState, groupId, laneId, startTime, endTime, progress);
            GameplaySkinEventValue payload = GameplaySkinEventValue.Object(kind, state);

            if (kind == GameplaySkinEventKind.ObjectSpawned)
            {
                if (objectCapacityExceeded)
                    return;

                if (activeObjects.ContainsKey(objectId))
                {
                    requestObjectResynchronisation();
                    return;
                }

                if (activeObjects.Count >= GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS)
                {
                    // Enter an explicit fail-closed epoch before mutating either host or stream state. The skin stops
                    // receiving object deltas for the remainder of this epoch, while gameplay and all non-object
                    // read-only events continue. A retry/seek/reload reset reopens admission from a full snapshot.
                    objectCapacityExceeded = true;
                    pendingReset = GameplaySkinEventResetReason.CapacityExceeded;
                    return;
                }
            }
            else
            {
                if (!activeObjects.TryGetValue(objectId, out GameplaySkinObjectStateSnapshot previous))
                {
                    if (objectCapacityExceeded)
                        return;

                    requestObjectResynchronisation();
                    return;
                }

                if (previous.Kind != state.Kind
                    || previous.GroupId != state.GroupId
                    || previous.LaneId != state.LaneId
                    || previous.StartTime != state.StartTime
                    || previous.EndTime != state.EndTime
                    || (!objectResynchronisationAllowed.Contains(objectId) && state.Progress < previous.Progress))
                {
                    requestObjectResynchronisation();
                    return;
                }
            }

            if (kind == GameplaySkinEventKind.ObjectDespawned)
            {
                activeObjects.Remove(objectId);
                judgementsByObject.Remove(objectId);
            }
            else
                activeObjects[objectId] = state;

            objectResynchronisationAllowed.Remove(objectId);

            publish(payload, groupId, laneId);
        }

        private void requestObjectResynchronisation()
        {
            if (producer != null && pendingReset == GameplaySkinEventResetReason.Unspecified)
                pendingReset = GameplaySkinEventResetReason.ConsumerRebuilt;
        }

        internal void PublishJudgement(
            long? objectId,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinJudgementGrade grade,
            double offset,
            double gaugeDelta)
        {
            if (groupId != null)
                validateExactTarget(groupId, laneId);

            // A discontinuity discards all previous judgements and rebuilds active objects on the next stable
            // playfield frame. Result callbacks can race that rebuild in ruleset traversal order; they must not
            // resurrect pre-seek history or be validated against the intentionally stale object index.
            if (pendingReset is GameplaySkinEventResetReason.Seek or GameplaySkinEventResetReason.Rewind or GameplaySkinEventResetReason.Retry)
                return;

            var state = new GameplaySkinJudgementStateSnapshot(objectId, groupId, laneId, grade, offset, gaugeDelta);
            if (objectId.HasValue)
            {
                if (!activeObjects.TryGetValue(objectId.Value, out GameplaySkinObjectStateSnapshot active))
                    throw new ArgumentException("An object-targeted judgement must belong to an active gameplay object.", nameof(objectId));

                if (active.GroupId != groupId || active.LaneId != laneId)
                {
                    throw new ArgumentException(
                        "An object-targeted judgement must retain the active object's exact group and lane identity.",
                        nameof(objectId));
                }
            }

            double appliedTime = currentTime;
            double displayUntil = appliedTime + GameplaySkinEventBudgets.JUDGEMENT_DISPLAY_DURATION;
            globalJudgement = new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Global, state, appliedTime, displayUntil);

            if (objectId.HasValue)
            {
                judgementsByObject[objectId.Value] = new GameplaySkinCurrentJudgementStateSnapshot(
                    GameplaySkinJudgementScope.Object,
                    state,
                    appliedTime,
                    displayUntil);
            }

            if (groupId != null)
            {
                judgementsByGroup[groupId] = new GameplaySkinCurrentJudgementStateSnapshot(
                    GameplaySkinJudgementScope.Group,
                    state,
                    appliedTime,
                    displayUntil);
            }

            if (laneId != null)
            {
                judgementsByLane[laneId] = new GameplaySkinCurrentJudgementStateSnapshot(
                    GameplaySkinJudgementScope.Lane,
                    state,
                    appliedTime,
                    displayUntil);
            }

            publish(GameplaySkinEventValue.Judgement(state), groupId, laneId);
        }

        internal void PublishBga(int viewportIndex, GameplaySkinBgaContentState contentState, long contentRevision)
        {
            GameplaySkinLayoutRect viewport = getBgaViewport(viewportIndex);
            var state = new GameplaySkinBgaStateSnapshot(viewportIndex, viewport, contentState, contentRevision);

            // A seek may legitimately move the engine-owned BGA content revision backwards. Fold that summary
            // while edge publication is muted; the following complete Reset is the sole observable epoch barrier.
            // BGA presentation can settle one frame after the scrolling playfield. Each changed summary therefore
            // rearms the same bounded barrier, ensuring the one Reset contains the final engine-owned selection
            // instead of publishing an intermediate revision and then observing a second backwards edge.
            if (pendingReset is GameplaySkinEventResetReason.Seek or GameplaySkinEventResetReason.Rewind or GameplaySkinEventResetReason.Retry)
            {
                bga[viewportIndex] = state;

                if (objectSnapshotSource != null)
                    deferDiscontinuityResetForDrawableState = true;

                return;
            }

            if (bga.TryGetValue(viewportIndex, out GameplaySkinBgaStateSnapshot previous)
                && !bgaResynchronisationAllowed.Contains(viewportIndex)
                && contentRevision < previous.ContentRevision)
            {
                throw new ArgumentOutOfRangeException(nameof(contentRevision), contentRevision, "BGA content revision cannot move backwards without a complete epoch reset.");
            }

            bga[viewportIndex] = state;
            bgaResynchronisationAllowed.Remove(viewportIndex);
            publish(GameplaySkinEventValue.Bga(GameplaySkinEventKind.BgaContentStateChanged, state));
        }

        /// <summary>
        /// Republishes the engine-owned viewport from the exact C3 layout. Callers cannot inject geometry.
        /// </summary>
        internal void PublishBgaViewport(int viewportIndex)
        {
            GameplaySkinLayoutRect viewport = getBgaViewport(viewportIndex);
            GameplaySkinBgaStateSnapshot previous = bga.TryGetValue(viewportIndex, out GameplaySkinBgaStateSnapshot value)
                ? value
                : new GameplaySkinBgaStateSnapshot(viewportIndex, viewport, GameplaySkinBgaContentState.Empty, 0);
            var state = new GameplaySkinBgaStateSnapshot(viewportIndex, viewport, previous.ContentState, previous.ContentRevision);
            bga[viewportIndex] = state;
            publish(GameplaySkinEventValue.Bga(GameplaySkinEventKind.BgaViewportChanged, state));
        }

        internal void RequestReset(GameplaySkinEventResetReason reason)
        {
            if (reason == GameplaySkinEventResetReason.Unspecified)
                throw new ArgumentOutOfRangeException(nameof(reason));

            pendingReset = reason;

            if (objectSnapshotSource != null
                && reason is GameplaySkinEventResetReason.Seek or GameplaySkinEventResetReason.Rewind or GameplaySkinEventResetReason.Retry)
            {
                deferDiscontinuityResetForDrawableState = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (producer == null)
                return;

            refreshFailureLifecycle();

            // Keep the exact seek destination authoritative through the complete reset frame. The framed gameplay
            // clock may still expose the old position to siblings later in that traversal; release the override only
            // after it has actually converged on the destination.
            if (pendingReset == GameplaySkinEventResetReason.Unspecified
                && pendingDiscontinuityGameplayTime is double discontinuityTime
                && Math.Abs(currentTime - discontinuityTime) <= 250)
            {
                pendingDiscontinuityGameplayTime = null;
            }

            double time = AuthoritativeGameplayTime;

            if (double.IsFinite(previousTime) && time < previousTime && pendingReset == GameplaySkinEventResetReason.Unspecified)
            {
                pendingReset = GameplaySkinEventResetReason.Rewind;

                if (objectSnapshotSource != null)
                    deferDiscontinuityResetForDrawableState = true;
            }

            // FrameStableComponents update before the scrolling playfield. Hold this barrier for one playfield update
            // so the replacement snapshot is read from drawables re-applied at the new clock position, rather than
            // combining the new time with terminal state retained by the previous frame. All producers remain muted
            // while pendingReset is set, so no edge can cross the epoch boundary during this deterministic deferral.
            if (pendingReset != GameplaySkinEventResetReason.Unspecified && deferDiscontinuityResetForDrawableState)
            {
                deferDiscontinuityResetForDrawableState = false;
                previousTime = time;
                return;
            }

            if (pendingReset != GameplaySkinEventResetReason.Unspecified)
            {
                GameplaySkinEventResetReason reason = pendingReset;
                pendingReset = GameplaySkinEventResetReason.Unspecified;

                if (reason is GameplaySkinEventResetReason.Seek or GameplaySkinEventResetReason.Rewind or GameplaySkinEventResetReason.Retry)
                {
                    clearJudgements();
                    objectCapacityExceeded = false;
                }

                synchroniseScoreState();
                timing = createTimingState(time);
                synchroniseActiveObjectsFromEngine(time);
                producer = producer.Reset(time, buildSnapshot(time), reason);
                allowOneStateResynchronisation();
                previousBeat = (long)Math.Floor(timing.Beat);
                previousBar = timing.BarIndex;
                previousBpm = timing.Bpm;
                previousScroll = timing.ScrollMultiplier;
            }

            publishTiming(time);

            if (pendingReset == GameplaySkinEventResetReason.Unspecified)
                producer.SynchroniseTiming(time, timing);

            previousTime = time;
        }

        // GameplayClockContainer is the gameplay authority and changes atomically at Seek/Reset. Drawable.Time is
        // frame-stabilised presentation time and can remain on the pre-seek value while child callbacks are rebuilt.
        private double currentTime => gameplayClockContainer?.CurrentTime ?? Time.Current;

        private void publishTiming(double time)
        {
            GameplaySkinTimingStateSnapshot next = createTimingState(time);
            bool stoppedChanged = timingStopped != next.IsStopped;
            timing = next;
            long wholeBeat = (long)Math.Floor(timing.Beat);

            if (!previousBpm.Equals(timing.Bpm))
            {
                publish(time, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBpmChanged, timing));
                previousBpm = timing.Bpm;
            }

            if (!previousScroll.Equals(timing.ScrollMultiplier))
            {
                publish(time, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingScrollChanged, timing));
                previousScroll = timing.ScrollMultiplier;
            }

            if (stoppedChanged)
            {
                timingStopped = timing.IsStopped;
                publish(time, GameplaySkinEventValue.Timing(
                    timing.IsStopped ? GameplaySkinEventKind.TimingStopStarted : GameplaySkinEventKind.TimingStopEnded,
                    timing));
            }

            if (wholeBeat != previousBeat)
            {
                publish(time, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBeat, timing));
                previousBeat = wholeBeat;
            }

            if (timing.BarIndex != previousBar)
            {
                publish(time, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBar, timing));
                previousBar = timing.BarIndex;
            }
        }

        private GameplaySkinTimingStateSnapshot createTimingState(double time)
            => timingProjection.Sample(time);

        private void synchroniseActiveObjectsFromEngine(double gameplayTime)
        {
            if (objectSnapshotSource == null)
                return;

            IEnumerable<GameplaySkinObjectStateSnapshot> source = objectSnapshotSource.CreateGameplaySkinActiveObjectSnapshot(gameplayTime)
                                                                  ?? throw new InvalidOperationException("The gameplay object snapshot source returned no collection.");
            var replacement = new Dictionary<long, GameplaySkinObjectStateSnapshot>();

            foreach (GameplaySkinObjectStateSnapshot candidate in source)
            {
                if (replacement.Count >= GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS)
                    throw new InvalidOperationException("The engine-owned active-object snapshot exceeds the fixed event budget.");

                validateExactTarget(candidate.GroupId, candidate.LaneId);
                GameplaySkinObjectStateSnapshot normalised = SnapshotObjectAt(candidate, gameplayTime);

                if (normalised.State == GameplaySkinObjectState.Despawned)
                    throw new InvalidOperationException("A despawned object cannot be returned by the active drawable snapshot source.");

                if (normalised.State == GameplaySkinObjectState.Completed && normalised.Progress < 1)
                {
                    throw new InvalidOperationException(
                        "The active drawable snapshot source returned a completed object before its authoritative end time.");
                }

                if (!replacement.TryAdd(normalised.ObjectId, normalised))
                    throw new InvalidOperationException("The active drawable snapshot source returned a duplicate stable object ID.");
            }

            activeObjects.Clear();

            foreach ((long objectId, GameplaySkinObjectStateSnapshot state) in replacement)
                activeObjects.Add(objectId, state);

            foreach (long objectId in judgementsByObject.Keys.Where(objectId => !activeObjects.ContainsKey(objectId)).ToArray())
                judgementsByObject.Remove(objectId);
        }

        private GameplaySkinLayoutRect getBgaViewport(int viewportIndex)
            => ResolveBgaViewport(Publication.Snapshot.BgaViewports, viewportIndex);

        internal static GameplaySkinLayoutRect ResolveBgaViewport(
            IReadOnlyList<GameplaySkinLayoutRect> exactViewports,
            int viewportIndex)
        {
            ArgumentNullException.ThrowIfNull(exactViewports);
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);

            if (viewportIndex >= exactViewports.Count)
                throw new ArgumentOutOfRangeException(nameof(viewportIndex), viewportIndex, "The BGA viewport is not part of the exact layout publication.");

            return exactViewports[viewportIndex];
        }

        private void allowOneStateResynchronisation()
        {
            objectResynchronisationAllowed.Clear();
            objectResynchronisationAllowed.UnionWith(activeObjects.Keys);
            bgaResynchronisationAllowed.Clear();
            bgaResynchronisationAllowed.UnionWith(bga.Keys);
        }

        private void validateExactTarget(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId? laneId)
        {
            ArgumentNullException.ThrowIfNull(groupId);

            if (!Publication.Snapshot.Context.Topology.TryGetGroup(groupId, out _))
                throw new ArgumentException("The event target group is not part of the exact layout publication.", nameof(groupId));

            if (laneId == null)
                return;

            if (!Publication.Snapshot.Context.Topology.TryGetLane(laneId, out GameplaySkinLaneTopologyEntry? lane)
                || lane == null
                || lane.Identity.Group.Id != groupId)
            {
                throw new ArgumentException("The event target lane/group pair is not part of the exact layout publication.", nameof(laneId));
            }
        }

        private void refreshScore(GameplaySkinEventKind kind)
        {
            synchroniseScoreState();
            publish(GameplaySkinEventValue.Score(kind, score));
        }

        private void refreshCompletionLifecycle()
        {
            if (scoreProcessor?.HasCompleted.Value == true)
            {
                if (healthProcessor?.HasFailed != true)
                    publishLifecycle(GameplaySkinEventKind.GameplayCompleted, GameplaySkinLifecycleState.Completed);

                return;
            }

            if (lifecycle == GameplaySkinLifecycleState.Completed)
            {
                restoreLifecycleAfterDiscontinuity();
                requestTerminalReopenReset();
            }
        }

        private void refreshFailureLifecycle()
        {
            // HealthProcessor.Failed is a veto-producing Func<bool>; subscribing would change the authoritative failure
            // decision through multicast return-value ordering. Observe the committed engine state instead.
            bool failed = healthProcessor?.HasFailed == true;

            if (failed == observedFailure)
                return;

            observedFailure = failed;

            if (failed)
                publishLifecycle(GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed);
            else if (lifecycle == GameplaySkinLifecycleState.Failed)
            {
                restoreLifecycleAfterDiscontinuity();
                requestTerminalReopenReset();
            }
        }

        private void publishLifecycle(GameplaySkinEventKind kind, GameplaySkinLifecycleState state)
        {
            if (lastLifecycleEvent == kind && lifecycle == state)
                return;

            // Validate the typed pairing before mutating the complete state used by a possible backpressure reset.
            GameplaySkinEventValue payload = GameplaySkinEventValue.Lifecycle(kind, state);
            lifecycle = state;
            lastLifecycleEvent = kind;
            publish(payload);
        }

        private void restoreLifecycleAfterDiscontinuity()
        {
            bool paused = gameplayClockContainer?.IsPaused.Value != false;
            lifecycle = hasStarted ? paused ? GameplaySkinLifecycleState.Paused : GameplaySkinLifecycleState.Running : GameplaySkinLifecycleState.Loaded;
            lastLifecycleEvent = null;
        }

        private void requestTerminalReopenReset()
        {
            if (producer != null && pendingReset == GameplaySkinEventResetReason.Unspecified)
                pendingReset = GameplaySkinEventResetReason.ConsumerRebuilt;
        }

        private void synchroniseScoreState()
        {
            long total = scoreProcessor?.TotalScore.Value ?? score.Score;
            int combo = scoreProcessor?.Combo.Value ?? score.Combo;
            int maxCombo = Math.Max(combo, scoreProcessor?.HighestCombo.Value ?? score.MaxCombo);
            double accuracy = scoreProcessor?.Accuracy.Value ?? score.Accuracy;
            double gauge = healthProcessor?.Health.Value ?? score.Gauge;
            score = new GameplaySkinScoreStateSnapshot(total, combo, maxCombo, accuracy, gauge);
        }

        private GameplaySkinEventStateSnapshot buildSnapshot(double gameplayTime)
            => new GameplaySkinEventStateSnapshot(
                lifecycle,
                inputs.Values.OrderBy(input => input.LaneId.Value, StringComparer.Ordinal),
                activeObjects.Values.OrderBy(obj => obj.ObjectId).Select(obj => SnapshotObjectAt(obj, gameplayTime)),
                currentJudgements(gameplayTime),
                score,
                timing,
                bga.Values.OrderBy(viewport => viewport.ViewportIndex));

        private IEnumerable<GameplaySkinCurrentJudgementStateSnapshot> currentJudgements(double gameplayTime)
        {
            foreach (GameplaySkinCurrentJudgementStateSnapshot retained in judgementsByObject.Values)
            {
                if (retained.Judgement.ObjectId is long objectId && activeObjects.ContainsKey(objectId))
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
            clearJudgements();

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

        private void clearJudgements()
        {
            globalJudgement = null;
            judgementsByObject.Clear();
            judgementsByGroup.Clear();
            judgementsByLane.Clear();
        }

        internal static GameplaySkinObjectStateSnapshot SnapshotObjectAt(GameplaySkinObjectStateSnapshot state, double gameplayTime)
        {
            double duration = state.EndTime - state.StartTime;
            double progress = duration <= 0
                ? gameplayTime < state.StartTime ? 0 : 1
                : Math.Clamp((gameplayTime - state.StartTime) / duration, 0, 1);

            return new GameplaySkinObjectStateSnapshot(
                state.ObjectId,
                state.Kind,
                state.State,
                state.GroupId,
                state.LaneId,
                state.StartTime,
                state.EndTime,
                progress);
        }

        private void publish(
            GameplaySkinEventValue payload,
            GameplaySkinLaneGroupId? groupId = null,
            GameplaySkinLaneId? laneId = null)
            => publish(currentTime, payload, groupId, laneId);

        private void publish(
            double gameplayTime,
            GameplaySkinEventValue payload,
            GameplaySkinLaneGroupId? groupId = null,
            GameplaySkinLaneId? laneId = null)
        {
            if (producer == null || pendingReset != GameplaySkinEventResetReason.Unspecified)
                return;

            if (double.IsFinite(previousTime) && gameplayTime < previousTime)
            {
                pendingReset = GameplaySkinEventResetReason.Rewind;
                return;
            }

            try
            {
                EventStream.Publish(producer, gameplayTime, payload, groupId, laneId);
            }
            catch (GameplaySkinEventBackpressureException)
            {
                // The failed edge was atomic. The next update clears every bounded queue with a complete state reset;
                // no consumer continues from a silently incomplete delta history.
                pendingReset = GameplaySkinEventResetReason.ConsumerRebuilt;
            }
        }

        private void onTotalScoreChanged(ValueChangedEvent<long> _) => refreshScore(GameplaySkinEventKind.ScoreChanged);

        private void onComboChanged(ValueChangedEvent<int> _) => refreshScore(GameplaySkinEventKind.ComboChanged);

        private void onHighestComboChanged(ValueChangedEvent<int> _) => refreshScore(GameplaySkinEventKind.ComboChanged);

        private void onAccuracyChanged(ValueChangedEvent<double> _) => refreshScore(GameplaySkinEventKind.ScoreChanged);

        private void onHealthChanged(ValueChangedEvent<double> _) => refreshScore(GameplaySkinEventKind.GaugeChanged);

        private void onCompletedChanged(ValueChangedEvent<bool> _) => refreshCompletionLifecycle();

        private void onPausedChanged(ValueChangedEvent<bool> paused) => SetPaused(paused.NewValue);

        private void onReplayReset()
        {
            pendingReset = GameplaySkinEventResetReason.Retry;
            deferDiscontinuityResetForDrawableState = objectSnapshotSource != null;
            pendingDiscontinuityGameplayTime = gameplayClockContainer?.LastSeekTarget;
            restoreLifecycleAfterDiscontinuity();
        }

        private void onSeek()
        {
            // Drawable.Time is frame-stabilised and may still expose the pre-seek value while OnSeek is raised.
            // Classify against the clock container's already-updated source position so a real rewind cannot be
            // mislabeled as a forward seek for the whole replacement epoch.
            double seekDestination = gameplayClockContainer?.LastSeekTarget
                                     ?? gameplayClockContainer?.CurrentTime
                                     ?? currentTime;
            pendingReset = double.IsFinite(previousTime) && seekDestination < previousTime
                ? GameplaySkinEventResetReason.Rewind
                : GameplaySkinEventResetReason.Seek;
            deferDiscontinuityResetForDrawableState = objectSnapshotSource != null;
            pendingDiscontinuityGameplayTime = seekDestination;
            restoreLifecycleAfterDiscontinuity();
        }

        private void onRetry()
        {
            pendingReset = GameplaySkinEventResetReason.Retry;
            deferDiscontinuityResetForDrawableState = objectSnapshotSource != null;
            pendingDiscontinuityGameplayTime = gameplayClockContainer?.LastSeekTarget;
            restoreLifecycleAfterDiscontinuity();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (disposed)
                return;

            disposed = true;

            if (scoreProcessor != null)
            {
                scoreProcessor.TotalScore.ValueChanged -= onTotalScoreChanged;
                scoreProcessor.Combo.ValueChanged -= onComboChanged;
                scoreProcessor.HighestCombo.ValueChanged -= onHighestComboChanged;
                scoreProcessor.Accuracy.ValueChanged -= onAccuracyChanged;
                scoreProcessor.HasCompleted.ValueChanged -= onCompletedChanged;
                scoreProcessor.OnResetFromReplayFrame -= onReplayReset;
            }

            if (healthProcessor != null)
                healthProcessor.Health.ValueChanged -= onHealthChanged;

            if (gameplayClockContainer != null)
            {
                gameplayClockContainer.OnSeek -= onSeek;
                gameplayClockContainer.OnReset -= onRetry;
                gameplayClockContainer.IsPaused.ValueChanged -= onPausedChanged;
            }

            producer?.Dispose();
            EventStream.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
