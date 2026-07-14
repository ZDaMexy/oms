// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// One explicitly declared per-lane resource name copied from a configuration source.
    /// </summary>
    public sealed class GameplaySkinLaneResourceDeclaration
    {
        public GameplaySkinLaneId LaneId { get; }

        public GameplaySkinLaneResourceField Field { get; }

        /// <summary>
        /// The source resource name exactly as decoded. It may be empty and has not been validated or resolved.
        /// </summary>
        /// <remarks>
        /// This value may contain a package-relative path and must not be written to persistent diagnostics without sanitisation.
        /// </remarks>
        public string ResourceName { get; }

        private GameplaySkinLaneResourceDeclaration(GameplaySkinLaneId laneId, GameplaySkinLaneResourceField field, string resourceName)
        {
            LaneId = laneId;
            Field = field;
            ResourceName = resourceName;
        }

        public static GameplaySkinLaneResourceDeclaration Create(
            GameplaySkinLaneId laneId,
            GameplaySkinLaneResourceField field,
            string resourceName)
        {
            ArgumentNullException.ThrowIfNull(laneId);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(resourceName);

            if (!GameplaySkinLaneResourceFieldCatalog.IsCanonical(field))
                throw new ArgumentException("The lane resource field is not part of the closed gameplay skin catalog.", nameof(field));

            return new GameplaySkinLaneResourceDeclaration(laneId, field, resourceName);
        }

        /// <summary>
        /// Returns declaration identity only and never includes the resource name.
        /// </summary>
        public override string ToString() => $"{LaneId.Value}:{Field.Id}:Declared";
    }

    /// <summary>
    /// An immutable, ruleset-neutral snapshot of explicitly declared lane resource fields for one source bucket.
    /// </summary>
    /// <remarks>
    /// Missing entries are <see cref="GameplaySkinConfigurationDeclaration{T}.Absent"/> while an explicitly empty resource
    /// remains declared. The snapshot does not validate resources, infer defaults, resolve provider precedence, or produce
    /// <c>Provide</c>/<c>Inherit</c>/<c>Suppress</c>. It is not the complete Skin V1 configuration model.
    /// </remarks>
    public sealed class GameplaySkinLaneResourceSnapshot
    {
        private readonly Dictionary<(GameplaySkinLaneId LaneId, GameplaySkinLaneResourceField Field), string> values;

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        public IReadOnlyList<GameplaySkinLaneResourceDeclaration> Declarations { get; }

        private GameplaySkinLaneResourceSnapshot(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinLaneResourceDeclaration[] declarations,
            Dictionary<(GameplaySkinLaneId LaneId, GameplaySkinLaneResourceField Field), string> values)
        {
            Topology = topology;
            Declarations = Array.AsReadOnly(declarations);
            this.values = values;
        }

        public static GameplaySkinLaneResourceSnapshot Create(
            GameplaySkinLaneTopologySnapshot topology,
            IEnumerable<GameplaySkinLaneResourceDeclaration> declarations)
        {
            ArgumentNullException.ThrowIfNull(topology);
            ArgumentNullException.ThrowIfNull(declarations);

            GameplaySkinLaneResourceDeclaration[] copiedDeclarations = declarations.ToArray();

            if (copiedDeclarations.Any(declaration => declaration == null))
                throw new ArgumentException("A gameplay skin lane resource snapshot cannot contain a null declaration.", nameof(declarations));

            var copiedValues = new Dictionary<(GameplaySkinLaneId, GameplaySkinLaneResourceField), string>();

            foreach (GameplaySkinLaneResourceDeclaration declaration in copiedDeclarations)
            {
                if (!topology.TryGetLane(declaration.LaneId, out _))
                    throw new ArgumentException("Every lane resource declaration must target a lane in the snapshot topology.", nameof(declarations));

                if (!GameplaySkinLaneResourceFieldCatalog.IsCanonical(declaration.Field))
                    throw new ArgumentException("Every lane resource declaration must use a canonical field descriptor.", nameof(declarations));

                if (!copiedValues.TryAdd((declaration.LaneId, declaration.Field), declaration.ResourceName))
                    throw new ArgumentException("A lane resource field may be declared at most once in a source snapshot.", nameof(declarations));
            }

            GameplaySkinLaneResourceDeclaration[] orderedDeclarations = copiedDeclarations
                .OrderBy(declaration => getLogicalLaneIndex(topology, declaration.LaneId))
                .ThenBy(declaration => getFieldIndex(declaration.Field))
                .ToArray();

            return new GameplaySkinLaneResourceSnapshot(topology, orderedDeclarations, copiedValues);
        }

        public GameplaySkinConfigurationDeclaration<string> GetDeclaration(
            GameplaySkinLaneId laneId,
            GameplaySkinLaneResourceField field)
        {
            ArgumentNullException.ThrowIfNull(laneId);
            ArgumentNullException.ThrowIfNull(field);

            if (!Topology.TryGetLane(laneId, out _))
                throw new ArgumentException("The requested lane does not belong to this gameplay skin topology.", nameof(laneId));

            if (!GameplaySkinLaneResourceFieldCatalog.IsCanonical(field))
                throw new ArgumentException("The requested lane resource field is not part of the closed catalog.", nameof(field));

            return values.TryGetValue((laneId, field), out string? value)
                ? GameplaySkinConfigurationDeclaration<string>.Declared(value)
                : GameplaySkinConfigurationDeclaration<string>.Absent;
        }

        private static int getLogicalLaneIndex(GameplaySkinLaneTopologySnapshot topology, GameplaySkinLaneId laneId)
        {
            topology.TryGetLane(laneId, out GameplaySkinLaneTopologyEntry? lane);
            return lane!.GlobalLogicalIndex;
        }

        private static int getFieldIndex(GameplaySkinLaneResourceField field)
        {
            for (int i = 0; i < GameplaySkinLaneResourceFieldCatalog.All.Count; i++)
            {
                if (ReferenceEquals(GameplaySkinLaneResourceFieldCatalog.All[i], field))
                    return i;
            }

            throw new InvalidOperationException("The lane resource field is not part of the canonical catalog.");
        }
    }
}
