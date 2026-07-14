// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable set of capabilities explicitly requested by one gameplay skin package layer.
    /// </summary>
    /// <remarks>
    /// A request is not proof that a capability is known, supported or authorised. IDs are sorted ordinally for deterministic
    /// negotiation, and duplicate declarations are rejected instead of using first- or last-wins behaviour. Creation remains
    /// engine-internal until the versioned package manifest mapping is defined.
    /// </remarks>
    public sealed class GameplaySkinCapabilityRequest
    {
        public IReadOnlyList<GameplaySkinCapabilityId> CapabilityIds { get; }

        private GameplaySkinCapabilityRequest(GameplaySkinCapabilityId[] capabilityIds)
        {
            CapabilityIds = Array.AsReadOnly(capabilityIds);
        }

        internal static GameplaySkinCapabilityRequest Create(IEnumerable<GameplaySkinCapabilityId> capabilityIds)
        {
            ArgumentNullException.ThrowIfNull(capabilityIds);

            var seen = new HashSet<GameplaySkinCapabilityId>();
            GameplaySkinCapabilityId[] snapshot = capabilityIds.ToArray();

            foreach (GameplaySkinCapabilityId? capabilityId in snapshot)
            {
                ArgumentNullException.ThrowIfNull(capabilityId, nameof(capabilityIds));

                if (!seen.Add(capabilityId))
                    throw new ArgumentException($"Gameplay skin capability '{capabilityId}' was requested more than once.", nameof(capabilityIds));
            }

            Array.Sort(snapshot, (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            return new GameplaySkinCapabilityRequest(snapshot);
        }
    }
}
