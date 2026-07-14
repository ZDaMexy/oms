// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    internal enum GameplaySkinCapabilityAccessPolicy
    {
        Unspecified = 0,

        /// <summary>
        /// Reserved for low-risk baseline capabilities which need no optional per-skin consent after request and host support.
        /// Optional package abilities must use <see cref="PerSkinAuthorization"/>.
        /// </summary>
        NoAdditionalAuthorization = 1,

        PerSkinAuthorization = 2,
    }

    /// <summary>
    /// One entry in an engine-owned closed capability allowlist.
    /// </summary>
    internal sealed class GameplaySkinCapabilityDefinition
    {
        public GameplaySkinCapabilityId CapabilityId { get; }

        public string RequiredHostFeatureId { get; }

        public GameplaySkinCapabilityAccessPolicy AccessPolicy { get; }

        public GameplaySkinCapabilityDefinition(
            GameplaySkinCapabilityId capabilityId,
            string requiredHostFeatureId,
            GameplaySkinCapabilityAccessPolicy accessPolicy)
        {
            ArgumentNullException.ThrowIfNull(capabilityId);
            GameplaySkinCapabilityId.ValidateToken(requiredHostFeatureId, nameof(requiredHostFeatureId));

            if (accessPolicy is not GameplaySkinCapabilityAccessPolicy.NoAdditionalAuthorization
                and not GameplaySkinCapabilityAccessPolicy.PerSkinAuthorization)
            {
                throw new ArgumentOutOfRangeException(nameof(accessPolicy), accessPolicy, "Unknown or unspecified gameplay skin capability access policy.");
            }

            CapabilityId = capabilityId;
            RequiredHostFeatureId = requiredHostFeatureId;
            AccessPolicy = accessPolicy;
        }
    }

    /// <summary>
    /// Pure, fail-closed capability negotiation with no storage, service or runtime side effects.
    /// </summary>
    internal static class GameplaySkinCapabilityNegotiator
    {
        public static GameplaySkinCapabilityNegotiation Negotiate(
            GameplaySkinCapabilityRequest request,
            IEnumerable<GameplaySkinCapabilityDefinition> definitions,
            IEnumerable<string> availableHostFeatureIds,
            IEnumerable<GameplaySkinCapabilityId> authorisedCapabilityIdsForCurrentSkin)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(definitions);
            ArgumentNullException.ThrowIfNull(availableHostFeatureIds);
            ArgumentNullException.ThrowIfNull(authorisedCapabilityIdsForCurrentSkin);

            var definitionsById = new Dictionary<GameplaySkinCapabilityId, GameplaySkinCapabilityDefinition>();

            foreach (GameplaySkinCapabilityDefinition? definition in definitions)
            {
                ArgumentNullException.ThrowIfNull(definition, nameof(definitions));

                if (!definitionsById.TryAdd(definition.CapabilityId, definition))
                    throw new ArgumentException($"Gameplay skin capability '{definition.CapabilityId}' has more than one definition.", nameof(definitions));
            }

            var availableFeatures = new HashSet<string>(StringComparer.Ordinal);

            foreach (string? featureId in availableHostFeatureIds)
            {
                ArgumentNullException.ThrowIfNull(featureId, nameof(availableHostFeatureIds));
                GameplaySkinCapabilityId.ValidateToken(featureId, nameof(availableHostFeatureIds));
                availableFeatures.Add(featureId);
            }

            var authorisedCapabilities = new HashSet<GameplaySkinCapabilityId>();

            foreach (GameplaySkinCapabilityId? capabilityId in authorisedCapabilityIdsForCurrentSkin)
            {
                ArgumentNullException.ThrowIfNull(capabilityId, nameof(authorisedCapabilityIdsForCurrentSkin));
                authorisedCapabilities.Add(capabilityId);
            }

            var granted = new List<GameplaySkinCapabilityId>();
            var diagnostics = new List<GameplaySkinCapabilityDiagnostic>();

            foreach (GameplaySkinCapabilityId capabilityId in request.CapabilityIds)
            {
                if (GameplaySkinCapabilityHardDenyCatalog.IsHardDenied(capabilityId))
                {
                    diagnostics.Add(denied(GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority, capabilityId));
                    continue;
                }

                if (!definitionsById.TryGetValue(capabilityId, out GameplaySkinCapabilityDefinition? definition))
                {
                    diagnostics.Add(denied(GameplaySkinCapabilityDiagnosticCode.UnknownCapability, capabilityId));
                    continue;
                }

                if (!availableFeatures.Contains(definition.RequiredHostFeatureId))
                {
                    diagnostics.Add(denied(GameplaySkinCapabilityDiagnosticCode.HostFeatureUnavailable, capabilityId));
                    continue;
                }

                switch (definition.AccessPolicy)
                {
                    case GameplaySkinCapabilityAccessPolicy.NoAdditionalAuthorization:
                        granted.Add(capabilityId);
                        break;

                    case GameplaySkinCapabilityAccessPolicy.PerSkinAuthorization when authorisedCapabilities.Contains(capabilityId):
                        granted.Add(capabilityId);
                        break;

                    case GameplaySkinCapabilityAccessPolicy.PerSkinAuthorization:
                        diagnostics.Add(denied(GameplaySkinCapabilityDiagnosticCode.PerSkinAuthorizationRequired, capabilityId));
                        break;

                    default:
                        throw new InvalidOperationException($"Capability '{capabilityId}' has an invalid access policy.");
                }
            }

            return new GameplaySkinCapabilityNegotiation(granted, diagnostics);
        }

        private static GameplaySkinCapabilityDiagnostic denied(GameplaySkinCapabilityDiagnosticCode code, GameplaySkinCapabilityId capabilityId)
            => new GameplaySkinCapabilityDiagnostic(code, capabilityId);
    }
}
