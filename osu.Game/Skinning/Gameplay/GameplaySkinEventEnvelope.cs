// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Stable process-local contract identifiers and versions for the read-only gameplay skin event stream.
    /// </summary>
    public static class GameplaySkinEventApiVersions
    {
        public static string ContractId => GameplaySkinSceneContracts.EVENT_CONTRACT_ID;

        public const int V1 = 1;

        internal static bool IsSupported(string contractId, int apiVersion)
            => string.Equals(contractId, ContractId, StringComparison.Ordinal) && apiVersion == V1;
    }

    /// <summary>
    /// Describes how a gameplay skin event payload establishes or changes consumer state.
    /// </summary>
    public enum GameplaySkinEventDeliveryKind
    {
        Unspecified = 0,
        Snapshot = 1,
        Reset = 2,
        Edge = 3,
    }

    /// <summary>
    /// A stable, ruleset-neutral event discriminator. Delivery semantics are carried separately by
    /// <see cref="GameplaySkinEventDeliveryKind"/>.
    /// </summary>
    public enum GameplaySkinEventKind
    {
        Unspecified = 0,
        StateSnapshot = 1,
        StateReset = 2,
        LayoutPublicationCommitted = 3,

        GameplayLoaded = 10,
        GameplayStarted = 11,
        GameplayPaused = 12,
        GameplayResumed = 13,
        GameplayCompleted = 14,
        GameplayFailed = 15,

        InputPressed = 20,
        InputReleased = 21,

        ObjectSpawned = 30,
        ObjectDespawned = 31,
        ObjectStateChanged = 32,

        JudgementApplied = 40,
        ScoreChanged = 41,
        ComboChanged = 42,
        GaugeChanged = 43,

        TimingBeat = 50,
        TimingBar = 51,
        TimingBpmChanged = 52,
        TimingStopStarted = 53,
        TimingStopEnded = 54,
        TimingScrollChanged = 55,

        BgaViewportChanged = 60,
        BgaContentStateChanged = 61,
    }

    /// <summary>
    /// The four exact revisions consumed by an event stream publication.
    /// </summary>
    public readonly struct GameplaySkinEventRevision : IEquatable<GameplaySkinEventRevision>
    {
        public long GameplayRevision { get; }

        public long LayoutRevision { get; }

        public long MaterialRevision { get; }

        public long SceneRevision { get; }

        private GameplaySkinEventRevision(long gameplayRevision, long layoutRevision, long materialRevision, long sceneRevision)
        {
            GameplayRevision = gameplayRevision;
            LayoutRevision = layoutRevision;
            MaterialRevision = materialRevision;
            SceneRevision = sceneRevision;
        }

        public static GameplaySkinEventRevision Create(long gameplayRevision, long layoutRevision, long materialRevision, long sceneRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(gameplayRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(layoutRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(materialRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(sceneRevision);
            return new GameplaySkinEventRevision(gameplayRevision, layoutRevision, materialRevision, sceneRevision);
        }

        public bool Equals(GameplaySkinEventRevision other)
            => GameplayRevision == other.GameplayRevision
               && LayoutRevision == other.LayoutRevision
               && MaterialRevision == other.MaterialRevision
               && SceneRevision == other.SceneRevision;

        public override bool Equals(object? obj) => obj is GameplaySkinEventRevision other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GameplayRevision, LayoutRevision, MaterialRevision, SceneRevision);

        public static bool operator ==(GameplaySkinEventRevision left, GameplaySkinEventRevision right) => left.Equals(right);

        public static bool operator !=(GameplaySkinEventRevision left, GameplaySkinEventRevision right) => !left.Equals(right);

        internal bool ContainsNoRegressionFrom(GameplaySkinEventRevision previous)
            => GameplayRevision >= previous.GameplayRevision
               && LayoutRevision >= previous.LayoutRevision
               && MaterialRevision >= previous.MaterialRevision
               && SceneRevision >= previous.SceneRevision;
    }

    /// <summary>
    /// Base type for engine-owned immutable gameplay skin event payload DTOs.
    /// </summary>
    /// <remarks>
    /// The internal constructor prevents skin packages and third-party ruleset code from defining new payload families.
    /// Payloads remain ruleset-neutral and never expose a drawable, hit object, judgement object, bindable, clock or Realm object.
    /// </remarks>
    public abstract class GameplaySkinEventPayload
    {
        public GameplaySkinEventDeliveryKind DeliveryKind { get; }

        public GameplaySkinEventKind EventKind { get; }

        internal GameplaySkinEventPayload(GameplaySkinEventDeliveryKind deliveryKind, GameplaySkinEventKind eventKind)
        {
            if (deliveryKind is not GameplaySkinEventDeliveryKind.Snapshot
                and not GameplaySkinEventDeliveryKind.Reset
                and not GameplaySkinEventDeliveryKind.Edge)
            {
                throw new ArgumentOutOfRangeException(nameof(deliveryKind), deliveryKind, "A gameplay skin event payload must declare a supported delivery kind.");
            }

            if (eventKind == GameplaySkinEventKind.Unspecified || !Enum.IsDefined(eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "A gameplay skin event payload must declare a supported event kind.");

            DeliveryKind = deliveryKind;
            EventKind = eventKind;
        }
    }

    /// <summary>
    /// Immutable ordering, target and exact-revision metadata for one engine-produced gameplay skin event payload.
    /// </summary>
    /// <remarks>
    /// <see cref="GameplayTime"/> is authoritative gameplay-clock time in milliseconds and may be negative during lead-in.
    /// It is never wall-clock time. Only engine code can construct an envelope; author content is strictly a consumer.
    /// </remarks>
    public sealed class GameplaySkinEventEnvelope
    {
        public string ContractId { get; }

        public int ApiVersion { get; }

        public long Epoch { get; }

        public long Sequence { get; }

        public double GameplayTime { get; }

        public long GameplayRevision => Revision.GameplayRevision;

        public long LayoutRevision => Revision.LayoutRevision;

        public long MaterialRevision => Revision.MaterialRevision;

        public long SceneRevision => Revision.SceneRevision;

        public GameplaySkinEventRevision Revision { get; }

        public GameplaySkinLaneGroupId? GroupId { get; }

        public GameplaySkinLaneId? LaneId { get; }

        public GameplaySkinEventKind EventKind { get; }

        public GameplaySkinEventPayload Payload { get; }

        public GameplaySkinEventDeliveryKind DeliveryKind => Payload.DeliveryKind;

        private GameplaySkinEventEnvelope(
            string contractId,
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            GameplaySkinEventRevision revision,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinEventPayload payload)
        {
            ContractId = contractId;
            ApiVersion = apiVersion;
            Epoch = epoch;
            Sequence = sequence;
            GameplayTime = gameplayTime;
            Revision = revision;
            GroupId = groupId;
            LaneId = laneId;
            EventKind = payload.EventKind;
            Payload = payload;
        }

        internal static GameplaySkinEventEnvelope Create(
            string contractId,
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            GameplaySkinEventRevision revision,
            GameplaySkinLaneGroupId? groupId,
            GameplaySkinLaneId? laneId,
            GameplaySkinEventPayload payload)
        {
            ArgumentException.ThrowIfNullOrEmpty(contractId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion);
            ArgumentOutOfRangeException.ThrowIfNegative(epoch);
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);
            ArgumentNullException.ThrowIfNull(payload);

            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime), gameplayTime, "Gameplay event time must be finite.");

            if (laneId != null && groupId == null)
                throw new ArgumentException("A lane-targeted gameplay skin event must also carry its stable group ID.", nameof(groupId));

            (GameplaySkinLaneGroupId? expectedGroup, GameplaySkinLaneId? expectedLane) = payload switch
            {
                GameplaySkinInputEventPayload input => (input.State.GroupId, input.State.LaneId),
                GameplaySkinObjectEventPayload obj => (obj.State.GroupId, obj.State.LaneId),
                GameplaySkinJudgementEventPayload judgement => (judgement.State.GroupId, judgement.State.LaneId),
                _ => (null, null),
            };

            if (groupId != expectedGroup || laneId != expectedLane)
                throw new ArgumentException("Gameplay skin event envelope target IDs must exactly match the immutable payload target.");

            return new GameplaySkinEventEnvelope(contractId, apiVersion, epoch, sequence, gameplayTime, revision, groupId, laneId, payload);
        }

        internal static GameplaySkinEventEnvelope Create(
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            long layoutRevision,
            GameplaySkinEventPayload payload)
            => Create(
                GameplaySkinEventApiVersions.ContractId,
                apiVersion,
                epoch,
                sequence,
                gameplayTime,
                GameplaySkinEventRevision.Create(0, layoutRevision, 0, 0),
                null,
                null,
                payload);
    }
}
