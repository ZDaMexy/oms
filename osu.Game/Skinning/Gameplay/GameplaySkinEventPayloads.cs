// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Hard in-memory bounds shared by the producer, snapshots and every consumer subscription.
    /// </summary>
    public static class GameplaySkinEventBudgets
    {
        public const int MAX_SUBSCRIPTIONS = 64;
        public const int MAX_PENDING_EVENTS_PER_SUBSCRIPTION = 4096;
        public const int MAX_EVENTS_CONSUMED_PER_FRAME = 2048;
        public const int MAX_INPUT_STATES = 64;
        public const int MAX_ACTIVE_OBJECTS = 32768;
        public const int MAX_BGA_VIEWPORTS = 16;
        public const int MAX_CURRENT_JUDGEMENT_STATES = MAX_ACTIVE_OBJECTS + MAX_INPUT_STATES * 2 + 1;
        public const double JUDGEMENT_DISPLAY_DURATION = 500;
    }

    public enum GameplaySkinLifecycleState
    {
        Unspecified = 0,
        Loaded = 1,
        Running = 2,
        Paused = 3,
        Completed = 4,
        Failed = 5,
    }

    public enum GameplaySkinEventResetReason
    {
        Unspecified = 0,
        Seek = 1,
        Rewind = 2,
        Retry = 3,
        ConsumerRebuilt = 5,
        CapacityExceeded = 6,
    }

    public enum GameplaySkinObjectKind
    {
        Unspecified = 0,
        Note = 1,
        LongNote = 2,
        Mine = 3,
        BarLine = 4,
    }

    public enum GameplaySkinObjectState
    {
        Unspecified = 0,
        Scheduled = 1,
        Visible = 2,
        Holding = 3,
        Hit = 4,
        Missed = 5,
        Completed = 6,
        Despawned = 7,
    }

    public enum GameplaySkinJudgementGrade
    {
        Unspecified = 0,
        Miss = 1,
        Meh = 2,
        Ok = 3,
        Good = 4,
        Great = 5,
        Perfect = 6,
    }

    public enum GameplaySkinBgaContentState
    {
        Unspecified = 0,
        Empty = 1,
        Ready = 2,
        Playing = 3,
        Paused = 4,
        Failed = 5,
    }

    /// <summary>
    /// Complete state of one stable gameplay input lane.
    /// </summary>
    public readonly struct GameplaySkinInputStateSnapshot
    {
        public GameplaySkinLaneGroupId GroupId { get; }

        public GameplaySkinLaneId LaneId { get; }

        public bool IsPressed { get; }

        public float Strength { get; }

        internal GameplaySkinInputStateSnapshot(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId, bool isPressed, float strength)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            ArgumentNullException.ThrowIfNull(laneId);

            if (!float.IsFinite(strength) || strength < 0 || strength > 1)
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Input strength must be finite and between zero and one.");

            if (!isPressed && strength != 0)
                throw new ArgumentException("A released input must have zero strength.", nameof(strength));

            GroupId = groupId;
            LaneId = laneId;
            IsPressed = isPressed;
            Strength = strength;
        }
    }

    /// <summary>
    /// Complete neutral state for one engine-owned drawable gameplay object.
    /// </summary>
    public readonly struct GameplaySkinObjectStateSnapshot
    {
        public long ObjectId { get; }

        public GameplaySkinObjectKind Kind { get; }

        public GameplaySkinObjectState State { get; }

        public GameplaySkinLaneGroupId GroupId { get; }

        public GameplaySkinLaneId? LaneId { get; }

        public double StartTime { get; }

        public double EndTime { get; }

        public double Progress { get; }

        internal GameplaySkinObjectStateSnapshot(
            long objectId,
            GameplaySkinObjectKind kind,
            GameplaySkinObjectState state,
            GameplaySkinLaneGroupId groupId,
            GameplaySkinLaneId? laneId,
            double startTime,
            double endTime,
            double progress)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);
            ArgumentNullException.ThrowIfNull(groupId);

            if (kind == GameplaySkinObjectKind.Unspecified || !Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Object kind must be specified.");

            if (state == GameplaySkinObjectState.Unspecified || !Enum.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state), state, "Object state must be specified.");

            if (kind != GameplaySkinObjectKind.BarLine && laneId == null)
                throw new ArgumentNullException(nameof(laneId), "Lane objects must carry a stable lane ID.");

            if (!double.IsFinite(startTime))
                throw new ArgumentOutOfRangeException(nameof(startTime), startTime, "Object start time must be finite.");

            if (!double.IsFinite(endTime) || endTime < startTime)
                throw new ArgumentOutOfRangeException(nameof(endTime), endTime, "Object end time must be finite and no earlier than start time.");

            if (!double.IsFinite(progress) || progress < 0 || progress > 1)
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "Object progress must be finite and between zero and one.");

            ObjectId = objectId;
            Kind = kind;
            State = state;
            GroupId = groupId;
            LaneId = laneId;
            StartTime = startTime;
            EndTime = endTime;
            Progress = progress;
        }
    }

    public readonly struct GameplaySkinJudgementStateSnapshot
    {
        public long? ObjectId { get; }

        public GameplaySkinLaneGroupId? GroupId { get; }

        public GameplaySkinLaneId? LaneId { get; }

        public GameplaySkinJudgementGrade Grade { get; }

        public double Offset { get; }

        public double GaugeDelta { get; }

        internal GameplaySkinJudgementStateSnapshot(
            long? objectId,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinJudgementGrade grade,
            double offset,
            double gaugeDelta)
        {
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId));

            if (laneId != null && groupId == null)
                throw new ArgumentException("A lane-targeted judgement must also carry its stable group ID.", nameof(groupId));

            if (grade == GameplaySkinJudgementGrade.Unspecified || !Enum.IsDefined(grade))
                throw new ArgumentOutOfRangeException(nameof(grade), grade, "Judgement grade must be specified.");

            ensureFinite(offset, nameof(offset));
            ensureFinite(gaugeDelta, nameof(gaugeDelta));
            ObjectId = objectId;
            GroupId = groupId;
            LaneId = laneId;
            Grade = grade;
            Offset = offset;
            GaugeDelta = gaugeDelta;
        }

        private static void ensureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(name, value, "Judgement values must be finite.");
        }
    }

    /// <summary>
    /// The exact retained projection of one judgement for a complete Snapshot/Reset.
    /// </summary>
    /// <remarks>
    /// A single judgement edge is deliberately projected into multiple scopes. Object scope survives until the
    /// corresponding active object despawns, allowing pooled object bindings to retain exact identity. Global,
    /// group and lane scopes expire at <see cref="DisplayUntil"/> and drive the short-lived judgement display.
    /// </remarks>
    public readonly struct GameplaySkinCurrentJudgementStateSnapshot
    {
        public GameplaySkinJudgementScope Scope { get; }

        public GameplaySkinJudgementStateSnapshot Judgement { get; }

        public double AppliedTime { get; }

        public double DisplayUntil { get; }

        internal GameplaySkinCurrentJudgementStateSnapshot(
            GameplaySkinJudgementScope scope,
            GameplaySkinJudgementStateSnapshot judgement,
            double appliedTime,
            double displayUntil)
        {
            if (scope == GameplaySkinJudgementScope.Unspecified || !Enum.IsDefined(scope))
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Judgement snapshot scope must be specified.");

            if (!double.IsFinite(appliedTime) || !double.IsFinite(displayUntil) || displayUntil < appliedTime)
                throw new ArgumentOutOfRangeException(nameof(displayUntil), displayUntil, "Judgement snapshot times must be finite and ordered.");

            if (scope == GameplaySkinJudgementScope.Object && !judgement.ObjectId.HasValue)
                throw new ArgumentException("An object judgement snapshot requires a stable object ID.", nameof(judgement));

            if (scope == GameplaySkinJudgementScope.Group && judgement.GroupId == null)
                throw new ArgumentException("A group judgement snapshot requires a stable group ID.", nameof(judgement));

            if (scope == GameplaySkinJudgementScope.Lane && judgement.LaneId == null)
                throw new ArgumentException("A lane judgement snapshot requires a stable lane ID.", nameof(judgement));

            Scope = scope;
            Judgement = judgement;
            AppliedTime = appliedTime;
            DisplayUntil = displayUntil;
        }
    }

    public enum GameplaySkinJudgementScope
    {
        Unspecified = 0,
        Global = 1,
        Group = 2,
        Lane = 3,
        Object = 4,
    }

    public readonly struct GameplaySkinScoreStateSnapshot
    {
        public long Score { get; }

        public int Combo { get; }

        public int MaxCombo { get; }

        public double Accuracy { get; }

        public double Gauge { get; }

        internal GameplaySkinScoreStateSnapshot(long score, int combo, int maxCombo, double accuracy, double gauge)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(score);
            ArgumentOutOfRangeException.ThrowIfNegative(combo);
            ArgumentOutOfRangeException.ThrowIfNegative(maxCombo);

            if (maxCombo < combo)
                throw new ArgumentOutOfRangeException(nameof(maxCombo), maxCombo, "Maximum combo cannot be below current combo.");

            validateUnitValue(accuracy, nameof(accuracy));
            validateUnitValue(gauge, nameof(gauge));
            Score = score;
            Combo = combo;
            MaxCombo = maxCombo;
            Accuracy = accuracy;
            Gauge = gauge;
        }

        private static void validateUnitValue(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, value, "The value must be finite and between zero and one.");
        }
    }

    public readonly struct GameplaySkinTimingStateSnapshot
    {
        public double Beat { get; }

        public long BarIndex { get; }

        public double Bpm { get; }

        public bool IsStopped { get; }

        public double ScrollMultiplier { get; }

        internal GameplaySkinTimingStateSnapshot(double beat, long barIndex, double bpm, bool isStopped, double scrollMultiplier)
        {
            if (!double.IsFinite(beat))
                throw new ArgumentOutOfRangeException(nameof(beat), beat, "Beat must be finite.");

            if (barIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(barIndex), barIndex, "Bar index must be -1 during lead-in or non-negative.");

            if (!double.IsFinite(bpm) || bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "BPM must be finite and positive.");

            if (!double.IsFinite(scrollMultiplier) || scrollMultiplier == 0)
                throw new ArgumentOutOfRangeException(nameof(scrollMultiplier), scrollMultiplier, "Scroll multiplier must be finite and non-zero.");

            Beat = beat;
            BarIndex = barIndex;
            Bpm = bpm;
            IsStopped = isStopped;
            ScrollMultiplier = scrollMultiplier;
        }
    }

    /// <summary>
    /// Read-only summary of engine-owned BGA state. It intentionally carries no media handle or timeline authority.
    /// </summary>
    public readonly struct GameplaySkinBgaStateSnapshot
    {
        public int ViewportIndex { get; }

        public GameplaySkinLayoutRect Viewport { get; }

        public GameplaySkinBgaContentState ContentState { get; }

        public long ContentRevision { get; }

        internal GameplaySkinBgaStateSnapshot(int viewportIndex, GameplaySkinLayoutRect viewport, GameplaySkinBgaContentState contentState, long contentRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(contentRevision);

            if (contentState == GameplaySkinBgaContentState.Unspecified || !Enum.IsDefined(contentState))
                throw new ArgumentOutOfRangeException(nameof(contentState), contentState, "BGA content state must be specified.");

            ViewportIndex = viewportIndex;
            Viewport = viewport;
            ContentState = contentState;
            ContentRevision = contentRevision;
        }
    }

    /// <summary>
    /// Complete, defensively copied gameplay state used for attach and every epoch reset.
    /// </summary>
    public sealed class GameplaySkinEventStateSnapshot
    {
        public GameplaySkinLifecycleState LifecycleState { get; }

        public IReadOnlyList<GameplaySkinInputStateSnapshot> Inputs { get; }

        public IReadOnlyList<GameplaySkinObjectStateSnapshot> ActiveObjects { get; }

        public IReadOnlyList<GameplaySkinCurrentJudgementStateSnapshot> CurrentJudgements { get; }

        public GameplaySkinJudgementStateSnapshot? LastJudgement { get; }

        public GameplaySkinScoreStateSnapshot Score { get; }

        public GameplaySkinTimingStateSnapshot Timing { get; }

        public IReadOnlyList<GameplaySkinBgaStateSnapshot> BgaViewports { get; }

        internal GameplaySkinEventStateSnapshot(
            GameplaySkinLifecycleState lifecycleState,
            IEnumerable<GameplaySkinInputStateSnapshot> inputs,
            IEnumerable<GameplaySkinObjectStateSnapshot> activeObjects,
            IEnumerable<GameplaySkinCurrentJudgementStateSnapshot> currentJudgements,
            GameplaySkinScoreStateSnapshot score,
            GameplaySkinTimingStateSnapshot timing,
            IEnumerable<GameplaySkinBgaStateSnapshot> bgaViewports)
        {
            if (lifecycleState == GameplaySkinLifecycleState.Unspecified || !Enum.IsDefined(lifecycleState))
                throw new ArgumentOutOfRangeException(nameof(lifecycleState), lifecycleState, "Lifecycle state must be specified.");

            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(activeObjects);
            ArgumentNullException.ThrowIfNull(currentJudgements);
            ArgumentNullException.ThrowIfNull(bgaViewports);

            GameplaySkinInputStateSnapshot[] copiedInputs = inputs.ToArray();
            GameplaySkinObjectStateSnapshot[] copiedObjects = activeObjects.ToArray();
            GameplaySkinCurrentJudgementStateSnapshot[] copiedJudgements = currentJudgements.ToArray();
            GameplaySkinBgaStateSnapshot[] copiedBga = bgaViewports.ToArray();

            validateCollection(copiedInputs, GameplaySkinEventBudgets.MAX_INPUT_STATES, nameof(inputs));
            validateCollection(copiedObjects, GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS, nameof(activeObjects));
            validateCollection(copiedJudgements, GameplaySkinEventBudgets.MAX_CURRENT_JUDGEMENT_STATES, nameof(currentJudgements));
            validateCollection(copiedBga, GameplaySkinEventBudgets.MAX_BGA_VIEWPORTS, nameof(bgaViewports));

            foreach (GameplaySkinInputStateSnapshot input in copiedInputs)
                _ = new GameplaySkinInputStateSnapshot(input.GroupId, input.LaneId, input.IsPressed, input.Strength);

            foreach (GameplaySkinObjectStateSnapshot obj in copiedObjects)
            {
                _ = new GameplaySkinObjectStateSnapshot(
                    obj.ObjectId,
                    obj.Kind,
                    obj.State,
                    obj.GroupId,
                    obj.LaneId,
                    obj.StartTime,
                    obj.EndTime,
                    obj.Progress);
            }

            foreach (GameplaySkinCurrentJudgementStateSnapshot retained in copiedJudgements)
            {
                GameplaySkinJudgementStateSnapshot judgement = retained.Judgement;
                _ = new GameplaySkinJudgementStateSnapshot(
                    judgement.ObjectId,
                    judgement.GroupId,
                    judgement.LaneId,
                    judgement.Grade,
                    judgement.Offset,
                    judgement.GaugeDelta);

                _ = new GameplaySkinCurrentJudgementStateSnapshot(retained.Scope, judgement, retained.AppliedTime, retained.DisplayUntil);
            }

            _ = new GameplaySkinScoreStateSnapshot(score.Score, score.Combo, score.MaxCombo, score.Accuracy, score.Gauge);
            _ = new GameplaySkinTimingStateSnapshot(timing.Beat, timing.BarIndex, timing.Bpm, timing.IsStopped, timing.ScrollMultiplier);

            foreach (GameplaySkinBgaStateSnapshot viewport in copiedBga)
                _ = new GameplaySkinBgaStateSnapshot(viewport.ViewportIndex, viewport.Viewport, viewport.ContentState, viewport.ContentRevision);

            if (copiedInputs.GroupBy(input => input.LaneId).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("A complete event snapshot cannot contain duplicate lane input states.", nameof(inputs));

            if (copiedObjects.GroupBy(obj => obj.ObjectId).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("A complete event snapshot cannot contain duplicate active object IDs.", nameof(activeObjects));

            if (copiedObjects.Any(obj => obj.State == GameplaySkinObjectState.Despawned))
                throw new ArgumentException("A despawned object cannot remain in a complete active-object snapshot.", nameof(activeObjects));

            if (copiedJudgements.GroupBy(judgementKey).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("A complete event snapshot cannot contain duplicate judgement scopes.", nameof(currentJudgements));

            HashSet<long> activeObjectIds = copiedObjects.Select(obj => obj.ObjectId).ToHashSet();

            if (copiedJudgements.Any(retained => retained.Scope == GameplaySkinJudgementScope.Object
                                                 && !activeObjectIds.Contains(retained.Judgement.ObjectId!.Value)))
            {
                throw new ArgumentException("An object judgement snapshot must belong to an active object.", nameof(currentJudgements));
            }

            if (copiedBga.GroupBy(state => state.ViewportIndex).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("A complete event snapshot cannot contain duplicate BGA viewport indices.", nameof(bgaViewports));

            Array.Sort(copiedInputs, static (left, right) => StringComparer.Ordinal.Compare(left.LaneId.Value, right.LaneId.Value));
            Array.Sort(copiedObjects, static (left, right) => left.ObjectId.CompareTo(right.ObjectId));
            Array.Sort(copiedJudgements, compareJudgements);
            Array.Sort(copiedBga, static (left, right) => left.ViewportIndex.CompareTo(right.ViewportIndex));

            LifecycleState = lifecycleState;
            Inputs = Array.AsReadOnly(copiedInputs);
            ActiveObjects = Array.AsReadOnly(copiedObjects);
            CurrentJudgements = Array.AsReadOnly(copiedJudgements);
            LastJudgement = copiedJudgements.FirstOrDefault(retained => retained.Scope == GameplaySkinJudgementScope.Global).Judgement;

            if (!copiedJudgements.Any(retained => retained.Scope == GameplaySkinJudgementScope.Global))
                LastJudgement = null;
            Score = score;
            Timing = timing;
            BgaViewports = Array.AsReadOnly(copiedBga);
        }

        private static string judgementKey(GameplaySkinCurrentJudgementStateSnapshot retained) => retained.Scope switch
        {
            GameplaySkinJudgementScope.Global => "global",
            GameplaySkinJudgementScope.Group => $"group:{retained.Judgement.GroupId!.Value}",
            GameplaySkinJudgementScope.Lane => $"lane:{retained.Judgement.LaneId!.Value}",
            GameplaySkinJudgementScope.Object => $"object:{retained.Judgement.ObjectId!.Value}",
            _ => throw new ArgumentOutOfRangeException(nameof(retained)),
        };

        private static int compareJudgements(
            GameplaySkinCurrentJudgementStateSnapshot left,
            GameplaySkinCurrentJudgementStateSnapshot right)
        {
            int scope = left.Scope.CompareTo(right.Scope);
            return scope != 0 ? scope : StringComparer.Ordinal.Compare(judgementKey(left), judgementKey(right));
        }

        internal void ValidateForGameplayTime(double gameplayTime)
        {
            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime));

            foreach (GameplaySkinCurrentJudgementStateSnapshot retained in CurrentJudgements)
            {
                if (retained.AppliedTime > gameplayTime)
                    throw new ArgumentException("A complete snapshot cannot contain a judgement from the future.", nameof(gameplayTime));

                if (retained.Scope is not GameplaySkinJudgementScope.Object && retained.DisplayUntil <= gameplayTime)
                    throw new ArgumentException("A complete snapshot cannot retain an expired transient judgement scope.", nameof(gameplayTime));
            }
        }

        private static void validateCollection<T>(T[] items, int maximum, string name)
        {
            if (items.Length > maximum)
                throw new ArgumentException($"The complete event snapshot exceeds its hard {maximum}-entry budget.", name);

        }
    }

    public sealed class GameplaySkinStateEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinEventStateSnapshot State { get; }

        public GameplaySkinEventResetReason ResetReason { get; }

        internal GameplaySkinStateEventPayload(GameplaySkinEventDeliveryKind deliveryKind, GameplaySkinEventStateSnapshot state, GameplaySkinEventResetReason resetReason)
            : base(deliveryKind, deliveryKind == GameplaySkinEventDeliveryKind.Snapshot ? GameplaySkinEventKind.StateSnapshot : GameplaySkinEventKind.StateReset)
        {
            if (deliveryKind is not GameplaySkinEventDeliveryKind.Snapshot and not GameplaySkinEventDeliveryKind.Reset)
                throw new ArgumentOutOfRangeException(nameof(deliveryKind), deliveryKind, "Complete state can only be delivered as snapshot or reset.");

            if (deliveryKind == GameplaySkinEventDeliveryKind.Snapshot && resetReason != GameplaySkinEventResetReason.Unspecified)
                throw new ArgumentOutOfRangeException(nameof(resetReason), resetReason, "An attach snapshot cannot carry a reset reason.");

            if (deliveryKind == GameplaySkinEventDeliveryKind.Reset
                && (resetReason == GameplaySkinEventResetReason.Unspecified || !Enum.IsDefined(resetReason)))
            {
                throw new ArgumentOutOfRangeException(nameof(resetReason), resetReason, "A reset must carry a supported reason.");
            }

            State = state;
            ResetReason = resetReason;
        }
    }

    public sealed class GameplaySkinPublicationEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinEventStateSnapshot State { get; }

        internal GameplaySkinPublicationEventPayload(GameplaySkinEventStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Reset, GameplaySkinEventKind.LayoutPublicationCommitted)
        {
            State = state;
        }
    }

    public sealed class GameplaySkinLifecycleEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinLifecycleState State { get; }

        internal GameplaySkinLifecycleEventPayload(GameplaySkinEventKind eventKind, GameplaySkinLifecycleState state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is < GameplaySkinEventKind.GameplayLoaded or > GameplaySkinEventKind.GameplayFailed)
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not a lifecycle edge.");

            if (state == GameplaySkinLifecycleState.Unspecified || !Enum.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state), state, "Lifecycle state must be specified.");

            GameplaySkinLifecycleState requiredState = eventKind switch
            {
                GameplaySkinEventKind.GameplayLoaded => GameplaySkinLifecycleState.Loaded,
                GameplaySkinEventKind.GameplayStarted or GameplaySkinEventKind.GameplayResumed => GameplaySkinLifecycleState.Running,
                GameplaySkinEventKind.GameplayPaused => GameplaySkinLifecycleState.Paused,
                GameplaySkinEventKind.GameplayCompleted => GameplaySkinLifecycleState.Completed,
                GameplaySkinEventKind.GameplayFailed => GameplaySkinLifecycleState.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(eventKind)),
            };

            if (state != requiredState)
                throw new ArgumentException("Lifecycle event kind must match the complete lifecycle state.", nameof(state));

            State = state;
        }
    }

    public sealed class GameplaySkinInputEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinInputStateSnapshot State { get; }

        internal GameplaySkinInputEventPayload(GameplaySkinEventKind eventKind, GameplaySkinInputStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is not GameplaySkinEventKind.InputPressed
                and not GameplaySkinEventKind.InputReleased)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not an input edge.");
            }

            if ((eventKind == GameplaySkinEventKind.InputPressed) != state.IsPressed)
            {
                throw new ArgumentException("Input event kind must match the complete pressed state.", nameof(eventKind));
            }

            State = state;
        }
    }

    public sealed class GameplaySkinObjectEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinObjectStateSnapshot State { get; }

        internal GameplaySkinObjectEventPayload(GameplaySkinEventKind eventKind, GameplaySkinObjectStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is not GameplaySkinEventKind.ObjectSpawned
                and not GameplaySkinEventKind.ObjectDespawned
                and not GameplaySkinEventKind.ObjectStateChanged)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not an object edge.");
            }

            if ((eventKind == GameplaySkinEventKind.ObjectDespawned) != (state.State == GameplaySkinObjectState.Despawned))
                throw new ArgumentException("Object despawn event kind must match the complete object state.", nameof(eventKind));

            State = state;
        }
    }

    public sealed class GameplaySkinJudgementEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinJudgementStateSnapshot State { get; }

        internal GameplaySkinJudgementEventPayload(GameplaySkinJudgementStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, GameplaySkinEventKind.JudgementApplied)
        {
            State = state;
        }
    }

    public sealed class GameplaySkinScoreEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinScoreStateSnapshot State { get; }

        internal GameplaySkinScoreEventPayload(GameplaySkinEventKind eventKind, GameplaySkinScoreStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is not GameplaySkinEventKind.ScoreChanged
                and not GameplaySkinEventKind.ComboChanged
                and not GameplaySkinEventKind.GaugeChanged)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not a score edge.");
            }

            State = state;
        }
    }

    public sealed class GameplaySkinTimingEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinTimingStateSnapshot State { get; }

        internal GameplaySkinTimingEventPayload(GameplaySkinEventKind eventKind, GameplaySkinTimingStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is not GameplaySkinEventKind.TimingBeat
                and not GameplaySkinEventKind.TimingBar
                and not GameplaySkinEventKind.TimingBpmChanged
                and not GameplaySkinEventKind.TimingStopStarted
                and not GameplaySkinEventKind.TimingStopEnded
                and not GameplaySkinEventKind.TimingScrollChanged)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not a timing edge.");
            }

            if ((eventKind == GameplaySkinEventKind.TimingStopStarted) != state.IsStopped
                && eventKind is GameplaySkinEventKind.TimingStopStarted or GameplaySkinEventKind.TimingStopEnded)
            {
                throw new ArgumentException("Timing stop event kind must match the complete timing state.", nameof(eventKind));
            }

            State = state;
        }
    }

    public sealed class GameplaySkinBgaEventPayload : GameplaySkinEventPayload
    {
        public GameplaySkinBgaStateSnapshot State { get; }

        internal GameplaySkinBgaEventPayload(GameplaySkinEventKind eventKind, GameplaySkinBgaStateSnapshot state)
            : base(GameplaySkinEventDeliveryKind.Edge, eventKind)
        {
            if (eventKind is not GameplaySkinEventKind.BgaViewportChanged and not GameplaySkinEventKind.BgaContentStateChanged)
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "The event kind is not a BGA edge.");

            State = state;
        }
    }
}
