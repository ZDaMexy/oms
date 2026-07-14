// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK.Graphics;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects decoder-accepted legacy mania per-column colours onto stable lanes in an exact target topology.
    /// </summary>
    /// <remarks>
    /// Source columns are zero-based compatibility coordinates for one <c>Keys:</c> bucket. They are never lane identity and
    /// must be mapped explicitly by the ruleset adapter. Partial and many-to-one source mappings are permitted for compatibility
    /// projections such as key-only and 14K deck fallback. This factory reads only decoder-time provenance sidecars, not the
    /// mutable <see cref="LegacyManiaSkinConfiguration.CustomColours"/> dictionary. It is public only as a cross-ruleset CLR
    /// bridge and is not an author, package, manifest, scene or serialisation API.
    /// </remarks>
    public static class LegacyManiaGameplaySkinLaneColourSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> Create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            int totalColumns,
            GameplaySkinLaneTopologySnapshot targetTopology,
            IReadOnlyDictionary<GameplaySkinLaneId, int> targetLaneColumns)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            ArgumentNullException.ThrowIfNull(targetTopology);
            ArgumentNullException.ThrowIfNull(targetLaneColumns);

            if (totalColumns <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalColumns), totalColumns, "A legacy mania configuration bucket must contain at least one column.");

            KeyValuePair<GameplaySkinLaneId, int>[] copiedLaneColumns = targetLaneColumns.ToArray();
            var mappedTargetLanes = new HashSet<GameplaySkinLaneId>();

            foreach (KeyValuePair<GameplaySkinLaneId, int> mapping in copiedLaneColumns)
            {
                if (mapping.Key == null)
                    throw new ArgumentException("A legacy mania lane-to-column map cannot contain a null lane ID.", nameof(targetLaneColumns));

                if (!mappedTargetLanes.Add(mapping.Key))
                    throw new ArgumentException("A legacy mania lane-to-column map cannot contain a target lane more than once.", nameof(targetLaneColumns));

                if (!targetTopology.TryGetLane(mapping.Key, out _))
                    throw new ArgumentException("Every mapped target lane must belong to the supplied topology.", nameof(targetLaneColumns));

                if (mapping.Value < 0 || mapping.Value >= totalColumns)
                    throw new ArgumentOutOfRangeException(nameof(targetLaneColumns), mapping.Value, "A mapped legacy mania column is outside its Keys bucket.");
            }

            LegacyManiaSkinConfiguration? source = null;

            foreach (LegacyManiaSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded legacy mania configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keys != totalColumns)
                    continue;

                if (source != null)
                    throw new ArgumentException("Decoded legacy mania configurations cannot contain duplicate key-count buckets.", nameof(decodedConfigurations));

                source = configuration;
            }

            if (source == null)
                return GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot>.Absent;

            GameplaySkinConfigurationDeclaration<Color4>[] backgroundColours =
                source.CopyAcceptedPerColumnColourDeclarations(LegacyManiaSkinPerColumnColourField.ColumnBackground);
            GameplaySkinConfigurationDeclaration<Color4>[] lightColours =
                source.CopyAcceptedPerColumnColourDeclarations(LegacyManiaSkinPerColumnColourField.ColumnLight);
            var declarations = new List<GameplaySkinLaneColourDeclaration>();

            foreach (KeyValuePair<GameplaySkinLaneId, int> mapping in copiedLaneColumns)
            {
                addIfDeclared(mapping.Key, GameplaySkinLaneColourFieldCatalog.LaneBackground, backgroundColours[mapping.Value], declarations);
                addIfDeclared(mapping.Key, GameplaySkinLaneColourFieldCatalog.LaneLight, lightColours[mapping.Value], declarations);
            }

            return GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot>.Declared(
                GameplaySkinLaneColourSnapshot.Create(targetTopology, declarations));
        }

        private static void addIfDeclared(
            GameplaySkinLaneId laneId,
            GameplaySkinLaneColourField field,
            GameplaySkinConfigurationDeclaration<Color4> declaration,
            ICollection<GameplaySkinLaneColourDeclaration> output)
        {
            if (declaration.IsDeclared)
                output.Add(GameplaySkinLaneColourDeclaration.Create(laneId, field, declaration.Value));
        }
    }
}
