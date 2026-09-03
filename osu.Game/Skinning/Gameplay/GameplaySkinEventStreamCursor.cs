// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Fail-closed stateful validation for the engine's complete gameplay skin event stream before capability or family filtering.
    /// </summary>
    /// <remarks>
    /// The cursor validates and advances; it never sorts, repairs or publishes events. A rejected envelope leaves the
    /// last accepted envelope unchanged. The initial snapshot may carry any non-negative epoch and sequence so a newly
    /// attached runtime can start from a complete mid-session snapshot. After attachment, only edges may continue the
    /// same epoch; every complete state replacement is either a newer-epoch Reset or a fresh consumer reattach.
    /// </remarks>
    internal sealed class GameplaySkinEventStreamCursor
    {
        private readonly string contractId;
        private readonly int apiVersion;
        private bool hasAcceptedRecord;
        private long acceptedEpoch;
        private long acceptedSequence;
        private double acceptedGameplayTime;
        private GameplaySkinEventRevision acceptedRevision;

        public GameplaySkinEventEnvelope? LastAcceptedEnvelope { get; private set; }

        internal long? LastAcceptedEpoch => hasAcceptedRecord ? acceptedEpoch : null;

        public GameplaySkinEventStreamCursor(int apiVersion)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion);

            contractId = GameplaySkinEventApiVersions.ContractId;

            if (!GameplaySkinEventApiVersions.IsSupported(contractId, apiVersion))
                throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "The gameplay skin event API version is not supported by this cursor.");

            this.apiVersion = apiVersion;
        }

        public void ValidateAndAdvance(GameplaySkinEventEnvelope envelope)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            if (!string.Equals(envelope.ContractId, contractId, StringComparison.Ordinal) || envelope.ApiVersion != apiVersion)
                throw streamViolation($"Expected gameplay skin event contract {contractId} at API version {apiVersion}.");

            if (!hasAcceptedRecord)
            {
                if (envelope.DeliveryKind != GameplaySkinEventDeliveryKind.Snapshot)
                    throw streamViolation("A gameplay skin event stream must attach with a complete snapshot.");

                accept(envelope);
                LastAcceptedEnvelope = envelope;
                return;
            }

            if (envelope.Epoch != acceptedEpoch)
            {
                if (acceptedEpoch == long.MaxValue || envelope.Epoch != acceptedEpoch + 1)
                    throw streamViolation("Gameplay skin event epochs must be contiguous and cannot wrap.");

                validateNewEpoch(acceptedRevision, envelope.DeliveryKind, envelope.Sequence, envelope.Revision);
                accept(envelope);
                LastAcceptedEnvelope = envelope;
                return;
            }

            validateSameEpoch(acceptedSequence, acceptedGameplayTime, acceptedRevision, envelope);
            accept(envelope);
            LastAcceptedEnvelope = envelope;
        }

        /// <summary>
        /// Validates the compact production queue item without materialising a public envelope or payload object.
        /// </summary>
        internal void ValidateAndAdvance(in GameplaySkinEventRecord record)
        {
            if (!string.Equals(record.ContractId, contractId, StringComparison.Ordinal) || record.ApiVersion != apiVersion)
                throw streamViolation($"Expected gameplay skin event contract {contractId} at API version {apiVersion}.");

            if (!hasAcceptedRecord)
            {
                if (record.DeliveryKind != GameplaySkinEventDeliveryKind.Snapshot)
                    throw streamViolation("A gameplay skin event stream must attach with a complete snapshot.");

                accept(record);
                return;
            }

            if (record.Epoch != acceptedEpoch)
            {
                if (acceptedEpoch == long.MaxValue || record.Epoch != acceptedEpoch + 1)
                    throw streamViolation("Gameplay skin event epochs must be contiguous and cannot wrap.");

                validateNewEpoch(acceptedRevision, record.DeliveryKind, record.Sequence, record.Revision);
                accept(record);
                return;
            }

            validateSameEpoch(acceptedSequence, acceptedGameplayTime, acceptedRevision, record);
            accept(record);
        }

        /// <summary>
        /// Reattaches a lagging bounded consumer from a new complete snapshot after an explicit stream reset discarded
        /// queued old-epoch edges. Production ordering validation must never call this on its producer cursor.
        /// </summary>
        internal void ResetForCompleteReattach()
        {
            LastAcceptedEnvelope = null;
            hasAcceptedRecord = false;
            acceptedEpoch = 0;
            acceptedSequence = 0;
            acceptedGameplayTime = 0;
            acceptedRevision = default;
        }

        private static void validateNewEpoch(
            GameplaySkinEventRevision previousRevision,
            GameplaySkinEventDeliveryKind deliveryKind,
            long sequence,
            GameplaySkinEventRevision revision)
        {
            if (deliveryKind != GameplaySkinEventDeliveryKind.Reset)
                throw streamViolation("A newer gameplay skin event epoch must start with a complete reset.");

            if (sequence != 0)
                throw streamViolation("A gameplay skin event reset must restart its epoch sequence at zero.");

            if (!revision.ContainsNoRegressionFrom(previousRevision))
                throw streamViolation("A gameplay skin event reset cannot return to an older gameplay, layout, material or scene revision.");
        }

        private static void validateSameEpoch(
            long previousSequence,
            double previousGameplayTime,
            GameplaySkinEventRevision previousRevision,
            GameplaySkinEventEnvelope envelope)
        {
            if (previousSequence == long.MaxValue)
                throw streamViolation("The current gameplay skin event epoch has exhausted its sequence range.");

            if (envelope.Sequence != previousSequence + 1)
                throw streamViolation("Gameplay skin event sequence values must be contiguous within an epoch.");

            if (envelope.GameplayTime < previousGameplayTime)
                throw streamViolation("Gameplay skin event time cannot move backwards within an epoch.");

            switch (envelope.DeliveryKind)
            {
                case GameplaySkinEventDeliveryKind.Reset:
                    throw streamViolation("A gameplay skin event reset must start a newer epoch.");

                case GameplaySkinEventDeliveryKind.Edge:
                    if (envelope.Revision != previousRevision)
                        throw streamViolation("A gameplay skin event edge must target the current exact gameplay, layout, material and scene revisions.");

                    break;

                case GameplaySkinEventDeliveryKind.Snapshot:
                    throw streamViolation("A gameplay skin event snapshot cannot replace state within an active epoch; use a newer-epoch reset or complete reattach.");

                default:
                    throw streamViolation("The gameplay skin event delivery kind is unsupported.");
            }
        }

        private static void validateSameEpoch(
            long previousSequence,
            double previousGameplayTime,
            GameplaySkinEventRevision previousRevision,
            in GameplaySkinEventRecord record)
        {
            if (previousSequence == long.MaxValue)
                throw streamViolation("The current gameplay skin event epoch has exhausted its sequence range.");

            if (record.Sequence != previousSequence + 1)
                throw streamViolation("Gameplay skin event sequence values must be contiguous within an epoch.");

            if (record.GameplayTime < previousGameplayTime)
                throw streamViolation("Gameplay skin event time cannot move backwards within an epoch.");

            switch (record.DeliveryKind)
            {
                case GameplaySkinEventDeliveryKind.Reset:
                    throw streamViolation("A gameplay skin event reset must start a newer epoch.");

                case GameplaySkinEventDeliveryKind.Edge:
                    if (record.Revision != previousRevision)
                        throw streamViolation("A gameplay skin event edge must target the current exact gameplay, layout, material and scene revisions.");

                    break;

                case GameplaySkinEventDeliveryKind.Snapshot:
                    throw streamViolation("A gameplay skin event snapshot cannot replace state within an active epoch; use a newer-epoch reset or complete reattach.");

                default:
                    throw streamViolation("The gameplay skin event delivery kind is unsupported.");
            }
        }

        private void accept(GameplaySkinEventEnvelope envelope)
        {
            hasAcceptedRecord = true;
            acceptedEpoch = envelope.Epoch;
            acceptedSequence = envelope.Sequence;
            acceptedGameplayTime = envelope.GameplayTime;
            acceptedRevision = envelope.Revision;
        }

        private void accept(in GameplaySkinEventRecord record)
        {
            hasAcceptedRecord = true;
            acceptedEpoch = record.Epoch;
            acceptedSequence = record.Sequence;
            acceptedGameplayTime = record.GameplayTime;
            acceptedRevision = record.Revision;
            LastAcceptedEnvelope = null;
        }

        private static InvalidOperationException streamViolation(string message) => new InvalidOperationException(message);
    }
}
