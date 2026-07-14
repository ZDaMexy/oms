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
    /// attached runtime can start from a complete mid-session snapshot. After attachment, epoch and sequence values are
    /// contiguous in their respective scopes; reset is a complete newer-epoch anchor.
    /// </remarks>
    internal sealed class GameplaySkinEventStreamCursor
    {
        private readonly int apiVersion;

        public GameplaySkinEventEnvelope? LastAcceptedEnvelope { get; private set; }

        public GameplaySkinEventStreamCursor(int apiVersion)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion);

            if (!GameplaySkinEventApiVersions.IsSupported(apiVersion))
                throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "The gameplay skin event API version is not supported by this cursor.");

            this.apiVersion = apiVersion;
        }

        public void ValidateAndAdvance(GameplaySkinEventEnvelope envelope)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            if (envelope.ApiVersion != apiVersion)
                throw streamViolation($"Expected gameplay skin event API version {apiVersion}, but received {envelope.ApiVersion}.");

            GameplaySkinEventEnvelope? previous = LastAcceptedEnvelope;

            if (previous == null)
            {
                if (envelope.DeliveryKind != GameplaySkinEventDeliveryKind.Snapshot)
                    throw streamViolation("A gameplay skin event stream must attach with a complete snapshot.");

                LastAcceptedEnvelope = envelope;
                return;
            }

            if (envelope.Epoch != previous.Epoch)
            {
                if (previous.Epoch == long.MaxValue || envelope.Epoch != previous.Epoch + 1)
                    throw streamViolation("Gameplay skin event epochs must be contiguous and cannot wrap.");

                validateNewEpoch(previous, envelope);
                LastAcceptedEnvelope = envelope;
                return;
            }

            validateSameEpoch(previous, envelope);
            LastAcceptedEnvelope = envelope;
        }

        private static void validateNewEpoch(GameplaySkinEventEnvelope previous, GameplaySkinEventEnvelope envelope)
        {
            if (envelope.DeliveryKind != GameplaySkinEventDeliveryKind.Reset)
                throw streamViolation("A newer gameplay skin event epoch must start with a complete reset.");

            if (envelope.Sequence != 0)
                throw streamViolation("A gameplay skin event reset must restart its epoch sequence at zero.");

            if (envelope.LayoutRevision < previous.LayoutRevision)
                throw streamViolation("A gameplay skin event reset cannot return to an older layout revision.");
        }

        private static void validateSameEpoch(GameplaySkinEventEnvelope previous, GameplaySkinEventEnvelope envelope)
        {
            if (previous.Sequence == long.MaxValue)
                throw streamViolation("The current gameplay skin event epoch has exhausted its sequence range.");

            if (envelope.Sequence != previous.Sequence + 1)
                throw streamViolation("Gameplay skin event sequence values must be contiguous within an epoch.");

            if (envelope.GameplayTime < previous.GameplayTime)
                throw streamViolation("Gameplay skin event time cannot move backwards within an epoch.");

            switch (envelope.DeliveryKind)
            {
                case GameplaySkinEventDeliveryKind.Reset:
                    throw streamViolation("A gameplay skin event reset must start a newer epoch.");

                case GameplaySkinEventDeliveryKind.Edge:
                    if (envelope.LayoutRevision != previous.LayoutRevision)
                        throw streamViolation("A gameplay skin event edge must target the current layout revision.");

                    break;

                case GameplaySkinEventDeliveryKind.Snapshot:
                    if (envelope.LayoutRevision < previous.LayoutRevision)
                        throw streamViolation("A gameplay skin event snapshot cannot return to an older layout revision within an epoch.");

                    break;

                default:
                    throw streamViolation("The gameplay skin event delivery kind is unsupported.");
            }
        }

        private static InvalidOperationException streamViolation(string message) => new InvalidOperationException(message);
    }
}
