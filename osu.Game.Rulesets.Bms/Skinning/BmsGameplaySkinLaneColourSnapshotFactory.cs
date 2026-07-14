// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Projects legacy mania per-column colour provenance through BMS-owned compatibility lane mappings.
    /// </summary>
    /// <remarks>
    /// These methods are fixture-only projections. They do not create a provider chain, resolve fallback, or connect a renderer.
    /// </remarks>
    internal static class BmsGameplaySkinLaneColourSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> CreateFullVisual(
            BmsLaneLayout layout,
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations)
        {
            BmsGameplaySkinLaneTopologyProjection projection = createProjection(layout, decodedConfigurations);
            IReadOnlyDictionary<GameplaySkinLaneId, int> mapping =
                BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateFullVisual(projection);

            return create(decodedConfigurations, projection, projection.Topology.LanesInVisualOrder.Count, mapping);
        }

        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> CreateEightColumnDeck(
            BmsLaneLayout layout,
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations)
        {
            BmsGameplaySkinLaneTopologyProjection projection = createProjection(layout, decodedConfigurations);
            IReadOnlyDictionary<GameplaySkinLaneId, int> mapping =
                BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateEightColumnDeck(projection);

            return create(decodedConfigurations, projection, 8, mapping);
        }

        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> CreateKeyOnly(
            BmsLaneLayout layout,
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations)
        {
            BmsGameplaySkinLaneTopologyProjection projection = createProjection(layout, decodedConfigurations);
            IReadOnlyDictionary<GameplaySkinLaneId, int> mapping =
                BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateKeyOnly(projection);

            return create(decodedConfigurations, projection, mapping.Count, mapping);
        }

        private static BmsGameplaySkinLaneTopologyProjection createProjection(
            BmsLaneLayout layout,
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            return BmsGameplaySkinLaneTopologyFactory.Create(layout);
        }

        private static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            BmsGameplaySkinLaneTopologyProjection projection,
            int sourceColumns,
            IReadOnlyDictionary<GameplaySkinLaneId, int> mapping)
            => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decodedConfigurations,
                sourceColumns,
                projection.Topology,
                mapping);
    }
}
