// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Projects one actual <see cref="BmsSkinDecoder"/> bucket into the shared lane-resource snapshot.
    /// </summary>
    /// <remarks>
    /// Only decoder-time accepted sidecars are read; later mutation of the public compatibility image dictionary cannot
    /// forge, erase or alter a declaration.
    /// </remarks>
    internal static class BmsGameplaySkinLaneResourceSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> Create(
            IReadOnlyList<BmsSkinConfiguration> decodedConfigurations,
            BmsGameplaySkinLaneTopologyProjection projection)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            ArgumentNullException.ThrowIfNull(projection);

            BmsSkinConfiguration? source = null;

            foreach (BmsSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded BMS configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keymode != projection.Keymode)
                    continue;

                if (source != null)
                    throw new ArgumentException("Decoded BMS configurations cannot contain duplicate keymode buckets.", nameof(decodedConfigurations));

                source = configuration;
            }

            if (source == null)
                return GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent;

            var declarations = new List<GameplaySkinLaneResourceDeclaration>();

            foreach (GameplaySkinLaneTopologyEntry lane in projection.Topology.LanesInLogicalOrder)
            {
                string laneToken = getLegacyLaneToken(projection, lane);

                foreach (GameplaySkinLaneResourceField field in GameplaySkinLaneResourceFieldCatalog.All)
                {
                    GameplaySkinConfigurationDeclaration<string> declaration = source.GetAcceptedLaneResource(field, laneToken);

                    if (!declaration.TryGetValue(out string? resourceName))
                        continue;

                    declarations.Add(GameplaySkinLaneResourceDeclaration.Create(lane.Identity.Id, field, resourceName));
                }
            }

            return GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Declared(
                GameplaySkinLaneResourceSnapshot.Create(projection.Topology, declarations));
        }

        private static string getLegacyLaneToken(
            BmsGameplaySkinLaneTopologyProjection projection,
            GameplaySkinLaneTopologyEntry lane)
        {
            if (lane.Identity.Role != GameplaySkinLaneRole.Scratch)
            {
                // Preserve the current unversioned [Bms] compatibility input exactly. BmsLegacySkin resolves non-scratch
                // resources from raw LaneIndex, which is the topology's global logical index: 5K/7K/14K therefore use
                // K1..K14 while scratch occupies index zero, but scratch-less 9K currently uses numeric tokens 0..8.
                // A canonical 1..9 migration requires a versioned format/diagnostic and cannot be guessed here.
                return lane.GlobalLogicalIndex.ToString(CultureInfo.InvariantCulture);
            }

            if (!projection.Topology.TryGetGroup(lane.Identity.Group.Id, out GameplaySkinLaneTopologyGroup? group) || group == null)
                throw new ArgumentException("The BMS lane topology does not contain the scratch lane's group.", nameof(projection));

            return projection.Keymode == BmsKeymode.Key14K && group.LogicalIndex == 1 ? "S2" : "S";
        }
    }
}
