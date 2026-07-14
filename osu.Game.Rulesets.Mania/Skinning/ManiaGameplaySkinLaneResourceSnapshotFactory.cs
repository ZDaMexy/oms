// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Projects a native mania topology and the actual decoder output into the shared lane-resource snapshot.
    /// </summary>
    internal static class ManiaGameplaySkinLaneResourceSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> Create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            ManiaBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            ArgumentNullException.ThrowIfNull(beatmap);

            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(beatmap);
            Dictionary<GameplaySkinLaneId, int> laneColumns = topology.LanesInLogicalOrder
                .ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalLogicalIndex);

            return LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decodedConfigurations,
                topology.LanesInLogicalOrder.Count,
                topology,
                laneColumns);
        }
    }
}
