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
    /// Projects native mania global logical columns into stable-lane colour declarations.
    /// </summary>
    /// <remarks>
    /// Dual-stage columns remain one absolute source sequence and never restart at the second stage. This factory is fixture-only
    /// and does not connect the production skin lookup or renderer.
    /// </remarks>
    internal static class ManiaGameplaySkinLaneColourSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> Create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            ManiaBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            ArgumentNullException.ThrowIfNull(beatmap);

            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(beatmap);
            Dictionary<GameplaySkinLaneId, int> laneColumns = topology.LanesInLogicalOrder
                .ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalLogicalIndex);

            return LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decodedConfigurations,
                topology.LanesInLogicalOrder.Count,
                topology,
                laneColumns);
        }
    }
}
