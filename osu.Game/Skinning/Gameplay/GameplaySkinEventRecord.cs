// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Closed engine-owned payload families used by the allocation-free production dispatcher.
    /// </summary>
    internal enum GameplaySkinEventPayloadFamily
    {
        State = 1,
        Publication = 2,
        Lifecycle = 3,
        Input = 4,
        Object = 5,
        Judgement = 6,
        Score = 7,
        Timing = 8,
        Bga = 9,
    }

    /// <summary>
    /// Compact immutable discriminated payload. Edge data is stored inline; complete Snapshot/Reset state is the
    /// only reference payload because it is created once per epoch rather than once per gameplay edge.
    /// </summary>
    internal readonly struct GameplaySkinEventValue
    {
        private readonly GameplaySkinEventPayload? materializedPayload;
        private readonly GameplaySkinEventStateSnapshot? completeState;
        private readonly long integral0;
        private readonly long integral1;
        private readonly int scalar0;
        private readonly int scalar1;
        private readonly int scalar2;
        private readonly double number0;
        private readonly double number1;
        private readonly double number2;
        private readonly double number3;
        private readonly double number4;
        private readonly GameplaySkinJudgementStatistics statistics;
        private readonly GameplaySkinLayoutRect rect;

        internal GameplaySkinEventPayloadFamily Family { get; }

        internal GameplaySkinEventDeliveryKind DeliveryKind { get; }

        internal GameplaySkinEventKind EventKind { get; }

        internal GameplaySkinEventResetReason ResetReason => (GameplaySkinEventResetReason)scalar0;

        internal GameplaySkinEventStateSnapshot CompleteState
            => completeState ?? throw new InvalidOperationException("The gameplay skin event payload does not carry complete state.");

        private GameplaySkinEventValue(
            GameplaySkinEventPayloadFamily family,
            GameplaySkinEventDeliveryKind deliveryKind,
            GameplaySkinEventKind eventKind,
            GameplaySkinEventPayload? materializedPayload = null,
            GameplaySkinEventStateSnapshot? completeState = null,
            long integral0 = 0,
            long integral1 = 0,
            int scalar0 = 0,
            int scalar1 = 0,
            int scalar2 = 0,
            double number0 = 0,
            double number1 = 0,
            double number2 = 0,
            double number3 = 0,
            GameplaySkinLayoutRect rect = default,
            double number4 = 0,
            GameplaySkinJudgementStatistics statistics = default)
        {
            Family = family;
            DeliveryKind = deliveryKind;
            EventKind = eventKind;
            this.materializedPayload = materializedPayload;
            this.completeState = completeState;
            this.integral0 = integral0;
            this.integral1 = integral1;
            this.scalar0 = scalar0;
            this.scalar1 = scalar1;
            this.scalar2 = scalar2;
            this.number0 = number0;
            this.number1 = number1;
            this.number2 = number2;
            this.number3 = number3;
            this.number4 = number4;
            this.statistics = statistics;
            this.rect = rect;
        }

        internal static GameplaySkinEventValue Snapshot(GameplaySkinEventStateSnapshot state)
            => stateValue(GameplaySkinEventDeliveryKind.Snapshot, state, GameplaySkinEventResetReason.Unspecified, null);

        internal static GameplaySkinEventValue Reset(GameplaySkinEventStateSnapshot state, GameplaySkinEventResetReason reason)
            => stateValue(GameplaySkinEventDeliveryKind.Reset, state, reason, null);

        internal static GameplaySkinEventValue Publication(GameplaySkinEventStateSnapshot state)
            => publicationValue(state, null);

        internal static GameplaySkinEventValue Lifecycle(GameplaySkinEventKind eventKind, GameplaySkinLifecycleState state, bool isFullCombo = false)
            => lifecycleValue(eventKind, state, isFullCombo, null);

        internal static GameplaySkinEventValue Input(GameplaySkinEventKind eventKind, GameplaySkinInputStateSnapshot state)
            => inputValue(eventKind, state, null);

        internal static GameplaySkinEventValue Object(GameplaySkinEventKind eventKind, GameplaySkinObjectStateSnapshot state)
            => objectValue(eventKind, state, null);

        internal static GameplaySkinEventValue Judgement(GameplaySkinJudgementStateSnapshot state)
            => judgementValue(state, null);

        internal static GameplaySkinEventValue Score(GameplaySkinEventKind eventKind, GameplaySkinScoreStateSnapshot state)
            => scoreValue(eventKind, state, null);

        internal static GameplaySkinEventValue Timing(GameplaySkinEventKind eventKind, GameplaySkinTimingStateSnapshot state)
            => timingValue(eventKind, state, null);

        internal static GameplaySkinEventValue Bga(GameplaySkinEventKind eventKind, GameplaySkinBgaStateSnapshot state)
            => bgaValue(eventKind, state, null);

        internal GameplaySkinInputStateSnapshot GetInput(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId)
        {
            requireFamily(GameplaySkinEventPayloadFamily.Input);
            return new GameplaySkinInputStateSnapshot(groupId, laneId, scalar0 != 0, (float)number0,
                (scalar1 & 1) != 0 ? number1 : null, (scalar1 & 2) != 0 ? number2 : null);
        }

        internal GameplaySkinObjectStateSnapshot GetObject(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId? laneId)
        {
            requireFamily(GameplaySkinEventPayloadFamily.Object);
            return new GameplaySkinObjectStateSnapshot(
                integral0,
                (GameplaySkinObjectKind)scalar0,
                (GameplaySkinObjectState)scalar1,
                groupId,
                laneId,
                number0,
                number1,
                number2,
                (GameplaySkinLongNoteMode)integral1);
        }

        internal GameplaySkinJudgementStateSnapshot GetJudgement(GameplaySkinLaneGroupId? groupId, GameplaySkinLaneId? laneId)
        {
            requireFamily(GameplaySkinEventPayloadFamily.Judgement);
            return new GameplaySkinJudgementStateSnapshot(
                integral1 == 0 ? null : integral0,
                groupId,
                laneId,
                (GameplaySkinJudgementGrade)scalar0,
                number0,
                number1);
        }

        internal GameplaySkinScoreStateSnapshot GetScore()
        {
            requireFamily(GameplaySkinEventPayloadFamily.Score);
            return new GameplaySkinScoreStateSnapshot(integral0, scalar0, scalar1, number0, number1, statistics,
                new GameplaySkinGaugeStateSnapshot((GameplaySkinGaugeType)scalar2, number2, number3, number4 != 0), integral1);
        }

        internal GameplaySkinTimingStateSnapshot GetTiming()
        {
            requireFamily(GameplaySkinEventPayloadFamily.Timing);
            // Timing reuses the existing union slots to retain both native
            // doubles bit-for-bit without growing every queued event record.
            long offsetBits = ((long)scalar2 << 32) | (uint)scalar1;
            var lift = new GameplaySkinLiftState((scalar0 & 2) != 0, BitConverter.Int64BitsToDouble(integral1),
                BitConverter.Int64BitsToDouble(offsetBits), (scalar0 & 4) != 0);
            return new GameplaySkinTimingStateSnapshot(number0, integral0, number1, (scalar0 & 1) != 0, number2, number3, number4, lift);
        }

        internal GameplaySkinBgaStateSnapshot GetBga()
        {
            requireFamily(GameplaySkinEventPayloadFamily.Bga);
            return new GameplaySkinBgaStateSnapshot(scalar0, rect, (GameplaySkinBgaContentState)scalar1, integral0);
        }

        internal GameplaySkinLifecycleState GetLifecycle()
        {
            requireFamily(GameplaySkinEventPayloadFamily.Lifecycle);
            return (GameplaySkinLifecycleState)scalar0;
        }

        internal bool GetCompletionFullCombo()
        {
            requireFamily(GameplaySkinEventPayloadFamily.Lifecycle);
            return scalar1 != 0;
        }

        internal GameplaySkinEventPayload Materialize(GameplaySkinLaneGroupId? groupId, GameplaySkinLaneId? laneId)
        {
            if (materializedPayload != null)
                return materializedPayload;

            return Family switch
            {
                GameplaySkinEventPayloadFamily.State => new GameplaySkinStateEventPayload(DeliveryKind, CompleteState, ResetReason),
                GameplaySkinEventPayloadFamily.Publication => new GameplaySkinPublicationEventPayload(CompleteState),
                GameplaySkinEventPayloadFamily.Lifecycle => new GameplaySkinLifecycleEventPayload(EventKind, GetLifecycle(), GetCompletionFullCombo()),
                GameplaySkinEventPayloadFamily.Input => new GameplaySkinInputEventPayload(EventKind, GetInput(requireGroup(groupId), requireLane(laneId))),
                GameplaySkinEventPayloadFamily.Object => new GameplaySkinObjectEventPayload(EventKind, GetObject(requireGroup(groupId), laneId)),
                GameplaySkinEventPayloadFamily.Judgement => new GameplaySkinJudgementEventPayload(GetJudgement(groupId, laneId)),
                GameplaySkinEventPayloadFamily.Score => new GameplaySkinScoreEventPayload(EventKind, GetScore()),
                GameplaySkinEventPayloadFamily.Timing => new GameplaySkinTimingEventPayload(EventKind, GetTiming()),
                GameplaySkinEventPayloadFamily.Bga => new GameplaySkinBgaEventPayload(EventKind, GetBga()),
                _ => throw new InvalidOperationException("The gameplay skin event payload family is unsupported."),
            };
        }

        internal void ValidateTarget(GameplaySkinLaneGroupId? groupId, GameplaySkinLaneId? laneId)
        {
            switch (Family)
            {
                case GameplaySkinEventPayloadFamily.Input:
                    _ = GetInput(requireGroup(groupId), requireLane(laneId));
                    break;

                case GameplaySkinEventPayloadFamily.Object:
                    _ = GetObject(requireGroup(groupId), laneId);
                    break;

                case GameplaySkinEventPayloadFamily.Judgement:
                    _ = GetJudgement(groupId, laneId);
                    break;

                default:
                    if (groupId != null || laneId != null)
                        throw new ArgumentException("A non-targeted gameplay skin event cannot carry stable lane or group IDs.");

                    break;
            }
        }

        private static GameplaySkinEventValue stateValue(
            GameplaySkinEventDeliveryKind deliveryKind,
            GameplaySkinEventStateSnapshot state,
            GameplaySkinEventResetReason reason,
            GameplaySkinEventPayload? materialized)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (deliveryKind is not GameplaySkinEventDeliveryKind.Snapshot and not GameplaySkinEventDeliveryKind.Reset)
                throw new ArgumentOutOfRangeException(nameof(deliveryKind));

            if (deliveryKind == GameplaySkinEventDeliveryKind.Snapshot && reason != GameplaySkinEventResetReason.Unspecified)
                throw new ArgumentOutOfRangeException(nameof(reason));

            if (deliveryKind == GameplaySkinEventDeliveryKind.Reset
                && (reason == GameplaySkinEventResetReason.Unspecified || !Enum.IsDefined(reason)))
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.State,
                deliveryKind,
                deliveryKind == GameplaySkinEventDeliveryKind.Snapshot ? GameplaySkinEventKind.StateSnapshot : GameplaySkinEventKind.StateReset,
                materialized,
                state,
                scalar0: (int)reason);
        }

        private static GameplaySkinEventValue publicationValue(GameplaySkinEventStateSnapshot state, GameplaySkinEventPayload? materialized)
        {
            ArgumentNullException.ThrowIfNull(state);
            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Publication,
                GameplaySkinEventDeliveryKind.Reset,
                GameplaySkinEventKind.LayoutPublicationCommitted,
                materialized,
                state);
        }

        private static GameplaySkinEventValue lifecycleValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinLifecycleState state,
            bool isFullCombo,
            GameplaySkinEventPayload? materialized)
        {
            GameplaySkinLifecycleState expected = eventKind switch
            {
                GameplaySkinEventKind.GameplayLoaded => GameplaySkinLifecycleState.Loaded,
                GameplaySkinEventKind.GameplayStarted or GameplaySkinEventKind.GameplayResumed => GameplaySkinLifecycleState.Running,
                GameplaySkinEventKind.GameplayPaused => GameplaySkinLifecycleState.Paused,
                GameplaySkinEventKind.GameplayCompleted => GameplaySkinLifecycleState.Completed,
                GameplaySkinEventKind.GameplayFailed => GameplaySkinLifecycleState.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(eventKind)),
            };

            if (state != expected)
                throw new ArgumentException("Lifecycle event kind must match the complete lifecycle state.", nameof(state));

            if (isFullCombo && state != GameplaySkinLifecycleState.Completed)
                throw new ArgumentException("Only the completion edge can carry a full-combo outcome.", nameof(isFullCombo));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Lifecycle,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                scalar0: (int)state,
                scalar1: isFullCombo ? 1 : 0);
        }

        private static GameplaySkinEventValue inputValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinInputStateSnapshot state,
            GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinInputStateSnapshot(state.GroupId, state.LaneId, state.IsPressed, state.Strength, state.PressedAt, state.ReleasedAt);

            if (eventKind is not GameplaySkinEventKind.InputPressed
                and not GameplaySkinEventKind.InputReleased)
                throw new ArgumentOutOfRangeException(nameof(eventKind));

            if ((eventKind == GameplaySkinEventKind.InputPressed) != state.IsPressed)
                throw new ArgumentException("Input event kind must match the complete pressed state.", nameof(eventKind));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Input,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                scalar0: state.IsPressed ? 1 : 0,
                scalar1: (state.PressedAt.HasValue ? 1 : 0) | (state.ReleasedAt.HasValue ? 2 : 0),
                number0: state.Strength,
                number1: state.PressedAt ?? 0,
                number2: state.ReleasedAt ?? 0);
        }

        private static GameplaySkinEventValue objectValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinObjectStateSnapshot state,
            GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinObjectStateSnapshot(
                state.ObjectId,
                state.Kind,
                state.State,
                state.GroupId,
                state.LaneId,
                state.StartTime,
                state.EndTime,
                state.Progress,
                state.LongNoteMode);

            if (eventKind is not GameplaySkinEventKind.ObjectSpawned
                and not GameplaySkinEventKind.ObjectDespawned
                and not GameplaySkinEventKind.ObjectStateChanged)
                throw new ArgumentOutOfRangeException(nameof(eventKind));

            if ((eventKind == GameplaySkinEventKind.ObjectDespawned) != (state.State == GameplaySkinObjectState.Despawned))
                throw new ArgumentException("Object despawn event kind must match the complete object state.", nameof(eventKind));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Object,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                integral0: state.ObjectId,
                integral1: (int)state.LongNoteMode,
                scalar0: (int)state.Kind,
                scalar1: (int)state.State,
                number0: state.StartTime,
                number1: state.EndTime,
                number2: state.Progress);
        }

        private static GameplaySkinEventValue judgementValue(GameplaySkinJudgementStateSnapshot state, GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinJudgementStateSnapshot(
                state.ObjectId,
                state.GroupId,
                state.LaneId,
                state.Grade,
                state.Offset,
                state.GaugeDelta);

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Judgement,
                GameplaySkinEventDeliveryKind.Edge,
                GameplaySkinEventKind.JudgementApplied,
                materialized,
                integral0: state.ObjectId ?? 0,
                integral1: state.ObjectId.HasValue ? 1 : 0,
                scalar0: (int)state.Grade,
                number0: state.Offset,
                number1: state.GaugeDelta);
        }

        private static GameplaySkinEventValue scoreValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinScoreStateSnapshot state,
            GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinScoreStateSnapshot(state.Score, state.Combo, state.MaxCombo, state.Accuracy, state.Gauge, state.Statistics, state.GaugeState, state.MaximumScore);

            if (eventKind is not GameplaySkinEventKind.ScoreChanged
                and not GameplaySkinEventKind.ComboChanged
                and not GameplaySkinEventKind.GaugeChanged)
                throw new ArgumentOutOfRangeException(nameof(eventKind));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Score,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                integral0: state.Score,
                integral1: state.MaximumScore,
                scalar0: state.Combo,
                scalar1: state.MaxCombo,
                scalar2: (int)state.GaugeState.Type,
                number0: state.Accuracy,
                number1: state.Gauge,
                number2: state.GaugeState.Maximum,
                number3: state.GaugeState.ClearThreshold,
                number4: state.GaugeState.IsClear ? 1 : 0,
                statistics: state.Statistics);
        }

        private static GameplaySkinEventValue timingValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinTimingStateSnapshot state,
            GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinTimingStateSnapshot(state.Beat, state.BarIndex, state.Bpm, state.IsStopped, state.ScrollMultiplier, state.Progress, state.ScrollSpeed, state.LiftState);

            if (eventKind is not GameplaySkinEventKind.TimingBeat
                and not GameplaySkinEventKind.TimingBar
                and not GameplaySkinEventKind.TimingBpmChanged
                and not GameplaySkinEventKind.TimingStopStarted
                and not GameplaySkinEventKind.TimingStopEnded
                and not GameplaySkinEventKind.TimingScrollChanged)
                throw new ArgumentOutOfRangeException(nameof(eventKind));

            if (eventKind is GameplaySkinEventKind.TimingStopStarted or GameplaySkinEventKind.TimingStopEnded
                && (eventKind == GameplaySkinEventKind.TimingStopStarted) != state.IsStopped)
                throw new ArgumentException("Timing stop event kind must match the complete timing state.", nameof(eventKind));

            long offsetBits = BitConverter.DoubleToInt64Bits(state.LiftState.OffsetRatio);
            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Timing,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                integral0: state.BarIndex,
                integral1: BitConverter.DoubleToInt64Bits(state.LiftState.Units),
                scalar0: (state.IsStopped ? 1 : 0) | (state.LiftState.IsAvailable ? 2 : 0) | (state.LiftState.Reversed ? 4 : 0),
                scalar1: unchecked((int)offsetBits),
                scalar2: unchecked((int)(offsetBits >> 32)),
                number0: state.Beat,
                number1: state.Bpm,
                number2: state.ScrollMultiplier,
                number3: state.Progress,
                number4: state.ScrollSpeed);
        }

        private static GameplaySkinEventValue bgaValue(
            GameplaySkinEventKind eventKind,
            GameplaySkinBgaStateSnapshot state,
            GameplaySkinEventPayload? materialized)
        {
            _ = new GameplaySkinBgaStateSnapshot(state.ViewportIndex, state.Viewport, state.ContentState, state.ContentRevision);

            if (eventKind is not GameplaySkinEventKind.BgaViewportChanged and not GameplaySkinEventKind.BgaContentStateChanged)
                throw new ArgumentOutOfRangeException(nameof(eventKind));

            return new GameplaySkinEventValue(
                GameplaySkinEventPayloadFamily.Bga,
                GameplaySkinEventDeliveryKind.Edge,
                eventKind,
                materialized,
                integral0: state.ContentRevision,
                scalar0: state.ViewportIndex,
                scalar1: (int)state.ContentState,
                rect: state.Viewport);
        }

        private void requireFamily(GameplaySkinEventPayloadFamily family)
        {
            if (Family != family)
                throw new InvalidOperationException("The gameplay skin event payload was read as the wrong typed family.");
        }

        private static GameplaySkinLaneGroupId requireGroup(GameplaySkinLaneGroupId? groupId)
            => groupId ?? throw new InvalidOperationException("The gameplay skin event is missing its stable group target.");

        private static GameplaySkinLaneId requireLane(GameplaySkinLaneId? laneId)
            => laneId ?? throw new InvalidOperationException("The gameplay skin event is missing its stable lane target.");
    }

    /// <summary>
    /// Allocation-free immutable queue item shared by the production producer and renderer.
    /// </summary>
    internal readonly struct GameplaySkinEventRecord
    {
        internal string ContractId { get; }

        internal int ApiVersion { get; }

        internal long Epoch { get; }

        internal long Sequence { get; }

        internal double GameplayTime { get; }

        internal GameplaySkinEventRevision Revision { get; }

        /// <summary>
        /// Engine timing state at this record's sequence high-water. This internal sideband lets a bounded renderer
        /// stop exactly at the last consumed record instead of observing newer state still queued behind it.
        /// </summary>
        internal GameplaySkinTimingStateSnapshot AuthoritativeTiming { get; }

        internal GameplaySkinLaneGroupId? GroupId { get; }

        internal GameplaySkinLaneId? LaneId { get; }

        internal GameplaySkinEventValue Payload { get; }

        internal GameplaySkinEventKind EventKind => Payload.EventKind;

        internal GameplaySkinEventDeliveryKind DeliveryKind => Payload.DeliveryKind;

        private GameplaySkinEventRecord(
            string contractId,
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            GameplaySkinEventRevision revision,
            GameplaySkinTimingStateSnapshot authoritativeTiming,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinEventValue payload)
        {
            ContractId = contractId;
            ApiVersion = apiVersion;
            Epoch = epoch;
            Sequence = sequence;
            GameplayTime = gameplayTime;
            Revision = revision;
            AuthoritativeTiming = authoritativeTiming;
            GroupId = groupId;
            LaneId = laneId;
            Payload = payload;
        }

        internal static GameplaySkinEventRecord Create(
            long epoch,
            long sequence,
            double gameplayTime,
            GameplaySkinEventRevision revision,
            GameplaySkinTimingStateSnapshot authoritativeTiming,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinEventValue payload)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(epoch);
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);

            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime));

            _ = new GameplaySkinTimingStateSnapshot(
                authoritativeTiming.Beat,
                authoritativeTiming.BarIndex,
                authoritativeTiming.Bpm,
                authoritativeTiming.IsStopped,
                authoritativeTiming.ScrollMultiplier,
                authoritativeTiming.Progress,
                authoritativeTiming.ScrollSpeed,
                authoritativeTiming.LiftState);

            if (laneId != null && groupId == null)
                throw new ArgumentException("A lane-targeted gameplay skin event must also carry its stable group ID.", nameof(groupId));

            payload.ValidateTarget(groupId, laneId);

            return new GameplaySkinEventRecord(
                GameplaySkinEventApiVersions.ContractId,
                GameplaySkinEventApiVersions.V1,
                epoch,
                sequence,
                gameplayTime,
                revision,
                authoritativeTiming,
                groupId,
                laneId,
                payload);
        }

        internal GameplaySkinEventEnvelope Materialize()
            => GameplaySkinEventEnvelope.Create(
                ContractId,
                ApiVersion,
                Epoch,
                Sequence,
                GameplayTime,
                Revision,
                GroupId,
                LaneId,
                Payload.Materialize(GroupId, LaneId));
    }
}
