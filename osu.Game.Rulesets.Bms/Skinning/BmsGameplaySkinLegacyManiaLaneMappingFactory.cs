// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Owns the explicit stable-lane to legacy mania source-column mappings used by BMS compatibility projections.
    /// </summary>
    /// <remarks>
    /// Source indices are compatibility coordinates, never lane identity. The 14K deck mapping intentionally maps both decks
    /// onto the same eight source indices, while key-only mappings intentionally omit scratch lanes.
    /// </remarks>
    internal static class BmsGameplaySkinLegacyManiaLaneMappingFactory
    {
        public static IReadOnlyDictionary<GameplaySkinLaneId, int> CreateFullVisual(BmsGameplaySkinLaneTopologyProjection projection)
        {
            ArgumentNullException.ThrowIfNull(projection);

            return readOnly(projection.Topology.LanesInVisualOrder
                                      .ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalVisualIndex));
        }

        public static IReadOnlyDictionary<GameplaySkinLaneId, int> CreateEightColumnDeck(BmsGameplaySkinLaneTopologyProjection projection)
        {
            ArgumentNullException.ThrowIfNull(projection);

            if (projection.Keymode != BmsKeymode.Key14K)
                throw new ArgumentException("Only a 14K BMS topology can project one legacy eight-column bucket over both decks.", nameof(projection));

            return readOnly(projection.Topology.LanesInLogicalOrder
                                      .ToDictionary(lane => lane.Identity.Id, lane => lane.GroupLocalVisualIndex));
        }

        public static IReadOnlyDictionary<GameplaySkinLaneId, int> CreateKeyOnly(BmsGameplaySkinLaneTopologyProjection projection)
        {
            ArgumentNullException.ThrowIfNull(projection);

            if (projection.Keymode is not BmsKeymode.Key5K and not BmsKeymode.Key7K and not BmsKeymode.Key14K)
                throw new ArgumentException("This BMS topology does not have a distinct scratch-omitting legacy mania bucket.", nameof(projection));

            return readOnly(projection.Topology.LanesInVisualOrder
                                      .Where(lane => lane.Identity.Role != GameplaySkinLaneRole.Scratch)
                                      .Select((lane, sourceColumnIndex) => (lane.Identity.Id, sourceColumnIndex))
                                      .ToDictionary(mapping => mapping.Id, mapping => mapping.sourceColumnIndex));
        }

        private static IReadOnlyDictionary<GameplaySkinLaneId, int> readOnly(Dictionary<GameplaySkinLaneId, int> mapping)
            => new ReadOnlyDictionary<GameplaySkinLaneId, int>(mapping);
    }
}
