// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Supported process-local gameplay skin event contract versions.
    /// </summary>
    /// <remarks>
    /// An envelope can carry a future positive version so a consumer can reject it without misreading the payload.
    /// A stream cursor accepts only versions explicitly listed as supported here.
    /// </remarks>
    public static class GameplaySkinEventApiVersions
    {
        public const int V1 = 1;

        internal static bool IsSupported(int apiVersion) => apiVersion == V1;
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
    /// Base type for engine-owned immutable gameplay skin event payload DTOs.
    /// </summary>
    /// <remarks>
    /// Concrete payload families are deliberately not part of this foundation slice. Future payloads must remain
    /// ruleset-neutral and must not expose framework drawables, hit objects, bindables or mutable gameplay state.
    /// The internal constructor prevents third-party packages from defining unvalidated public event payload families.
    /// </remarks>
    public abstract class GameplaySkinEventPayload
    {
        public GameplaySkinEventDeliveryKind DeliveryKind { get; }

        internal GameplaySkinEventPayload(GameplaySkinEventDeliveryKind deliveryKind)
        {
            if (deliveryKind is not GameplaySkinEventDeliveryKind.Snapshot
                and not GameplaySkinEventDeliveryKind.Reset
                and not GameplaySkinEventDeliveryKind.Edge)
            {
                throw new ArgumentOutOfRangeException(nameof(deliveryKind), deliveryKind, "A gameplay skin event payload must declare a supported delivery kind.");
            }

            DeliveryKind = deliveryKind;
        }
    }

    /// <summary>
    /// Immutable ordering and revision metadata for one engine-produced gameplay skin event payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GameplayTime"/> uses the gameplay clock's millisecond domain and may be negative during lead-in.
    /// It is never a wall-clock timestamp.
    /// </para>
    /// <para>
    /// A snapshot is a complete attach or reload state. A reset is a complete state rebuilt for a newer epoch after
    /// seek or retry, rather than a request for a later snapshot. An edge is an incremental change against the current
    /// epoch and layout revision. Stream transition rules are validated separately by the engine-owned cursor.
    /// </para>
    /// <para>
    /// This is a process-local engine contract, not a serialisation, script or author-facing manifest ABI. Only the
    /// engine constructs envelopes; external skin packages may consume validated DTOs but cannot publish gameplay truth.
    /// </para>
    /// </remarks>
    public sealed class GameplaySkinEventEnvelope
    {
        public int ApiVersion { get; }

        public long Epoch { get; }

        public long Sequence { get; }

        public double GameplayTime { get; }

        public long LayoutRevision { get; }

        public GameplaySkinEventPayload Payload { get; }

        public GameplaySkinEventDeliveryKind DeliveryKind => Payload.DeliveryKind;

        private GameplaySkinEventEnvelope(
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            long layoutRevision,
            GameplaySkinEventPayload payload)
        {
            ApiVersion = apiVersion;
            Epoch = epoch;
            Sequence = sequence;
            GameplayTime = gameplayTime;
            LayoutRevision = layoutRevision;
            Payload = payload;
        }

        internal static GameplaySkinEventEnvelope Create(
            int apiVersion,
            long epoch,
            long sequence,
            double gameplayTime,
            long layoutRevision,
            GameplaySkinEventPayload payload)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion);
            ArgumentOutOfRangeException.ThrowIfNegative(epoch);
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);
            ArgumentOutOfRangeException.ThrowIfNegative(layoutRevision);
            ArgumentNullException.ThrowIfNull(payload);

            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime), gameplayTime, "Gameplay event time must be finite.");

            return new GameplaySkinEventEnvelope(apiVersion, epoch, sequence, gameplayTime, layoutRevision, payload);
        }
    }
}
