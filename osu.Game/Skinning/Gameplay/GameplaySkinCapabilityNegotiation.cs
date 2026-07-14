// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A stable process-local reason why a requested capability was not granted.
    /// </summary>
    public enum GameplaySkinCapabilityDiagnosticCode
    {
        /// <summary>
        /// The request targeted engine or host authority which is never exposed to a skin.
        /// </summary>
        HardDeniedAuthority = 0,

        /// <summary>
        /// The capability is not present in the engine's closed allowlist.
        /// </summary>
        UnknownCapability = 1,

        /// <summary>
        /// The engine feature required to implement the capability is unavailable.
        /// </summary>
        HostFeatureUnavailable = 2,

        /// <summary>
        /// The capability is supported but has not been authorised for the current skin.
        /// </summary>
        PerSkinAuthorizationRequired = 3,
    }

    /// <summary>
    /// A structured process-local capability denial diagnostic.
    /// </summary>
    public sealed class GameplaySkinCapabilityDiagnostic
    {
        public GameplaySkinCapabilityDiagnosticCode Code { get; }

        public GameplaySkinCapabilityId CapabilityId { get; }

        internal GameplaySkinCapabilityDiagnostic(GameplaySkinCapabilityDiagnosticCode code, GameplaySkinCapabilityId capabilityId)
        {
            ArgumentNullException.ThrowIfNull(capabilityId);

            if (code is not GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority
                and not GameplaySkinCapabilityDiagnosticCode.UnknownCapability
                and not GameplaySkinCapabilityDiagnosticCode.HostFeatureUnavailable
                and not GameplaySkinCapabilityDiagnosticCode.PerSkinAuthorizationRequired)
            {
                throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown gameplay skin capability diagnostic code.");
            }

            Code = code;
            CapabilityId = capabilityId;
        }

        /// <summary>
        /// Returns only the stable diagnostic code and non-sensitive capability ID. This is log-safe, not a serialisation ABI.
        /// </summary>
        public override string ToString() => $"{Code}: {CapabilityId}";
    }

    /// <summary>
    /// An immutable snapshot of one capability negotiation.
    /// </summary>
    /// <remarks>
    /// This snapshot contains decisions only. It never carries a delegate, service, authority handle or mutable authorisation state.
    /// Revocation and host feature changes take effect by producing a new negotiation snapshot; existing snapshots do not mutate.
    /// A denied capability does not stop gameplay. Activation/fallback policy for a future capability-bearing scene or script layer is
    /// deliberately deferred until the versioned manifest contract exists.
    /// </remarks>
    public sealed class GameplaySkinCapabilityNegotiation
    {
        private readonly HashSet<GameplaySkinCapabilityId> grantedLookup;

        public IReadOnlyList<GameplaySkinCapabilityId> GrantedCapabilityIds { get; }

        public IReadOnlyList<GameplaySkinCapabilityDiagnostic> Diagnostics { get; }

        internal GameplaySkinCapabilityNegotiation(
            IEnumerable<GameplaySkinCapabilityId> grantedCapabilityIds,
            IEnumerable<GameplaySkinCapabilityDiagnostic> diagnostics)
        {
            ArgumentNullException.ThrowIfNull(grantedCapabilityIds);
            ArgumentNullException.ThrowIfNull(diagnostics);

            GameplaySkinCapabilityId[] grantedSnapshot = grantedCapabilityIds.ToArray();
            GameplaySkinCapabilityDiagnostic[] diagnosticSnapshot = diagnostics.ToArray();

            if (grantedSnapshot.Any(capabilityId => capabilityId == null))
                throw new ArgumentException("Granted gameplay skin capability IDs cannot contain null.", nameof(grantedCapabilityIds));

            if (diagnosticSnapshot.Any(diagnostic => diagnostic == null))
                throw new ArgumentException("Gameplay skin capability diagnostics cannot contain null.", nameof(diagnostics));

            grantedLookup = new HashSet<GameplaySkinCapabilityId>(grantedSnapshot);

            if (grantedLookup.Count != grantedSnapshot.Length)
                throw new ArgumentException("Granted gameplay skin capability IDs must be unique.", nameof(grantedCapabilityIds));

            if (grantedLookup.Any(GameplaySkinCapabilityHardDenyCatalog.IsHardDenied))
                throw new ArgumentException("Hard-denied gameplay skin authority cannot appear in the granted set.", nameof(grantedCapabilityIds));

            var deniedLookup = new HashSet<GameplaySkinCapabilityId>();

            foreach (GameplaySkinCapabilityDiagnostic diagnostic in diagnosticSnapshot)
            {
                if (!deniedLookup.Add(diagnostic.CapabilityId))
                    throw new ArgumentException("A gameplay skin capability can have at most one denial diagnostic.", nameof(diagnostics));

                if (grantedLookup.Contains(diagnostic.CapabilityId))
                    throw new ArgumentException("A gameplay skin capability cannot be both granted and denied.", nameof(diagnostics));

                bool isHardDenied = GameplaySkinCapabilityHardDenyCatalog.IsHardDenied(diagnostic.CapabilityId);

                if ((diagnostic.Code == GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority) != isHardDenied)
                {
                    throw new ArgumentException(
                        "Hard-denied authority and its diagnostic code must agree.",
                        nameof(diagnostics));
                }
            }

            Array.Sort(grantedSnapshot, (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            Array.Sort(diagnosticSnapshot, (left, right) => StringComparer.Ordinal.Compare(left.CapabilityId.Value, right.CapabilityId.Value));

            GrantedCapabilityIds = Array.AsReadOnly(grantedSnapshot);
            Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
        }

        /// <summary>
        /// Returns whether the exact capability was granted in this snapshot.
        /// </summary>
        public bool IsGranted(GameplaySkinCapabilityId? capabilityId)
            => capabilityId != null && grantedLookup.Contains(capabilityId);

        /// <summary>
        /// Returns counts only and never includes package or source information.
        /// </summary>
        public override string ToString() => $"Granted={GrantedCapabilityIds.Count}, Denied={Diagnostics.Count}";
    }
}
