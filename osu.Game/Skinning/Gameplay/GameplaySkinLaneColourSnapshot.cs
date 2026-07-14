// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK.Graphics;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// One explicitly declared per-lane colour copied from a configuration source.
    /// </summary>
    public sealed class GameplaySkinLaneColourDeclaration
    {
        public GameplaySkinLaneId LaneId { get; }

        public GameplaySkinLaneColourField Field { get; }

        /// <summary>
        /// The parser-accepted colour before renderer compatibility transforms, defaulting or visual validation.
        /// </summary>
        public Color4 Value { get; }

        private GameplaySkinLaneColourDeclaration(GameplaySkinLaneId laneId, GameplaySkinLaneColourField field, Color4 value)
        {
            LaneId = laneId;
            Field = field;
            Value = value;
        }

        public static GameplaySkinLaneColourDeclaration Create(
            GameplaySkinLaneId laneId,
            GameplaySkinLaneColourField field,
            Color4 value)
        {
            ArgumentNullException.ThrowIfNull(laneId);
            ArgumentNullException.ThrowIfNull(field);

            if (!GameplaySkinLaneColourFieldCatalog.IsCanonical(field))
                throw new ArgumentException("The lane colour field is not part of the closed gameplay skin catalog.", nameof(field));

            return new GameplaySkinLaneColourDeclaration(laneId, field, value);
        }

        /// <summary>
        /// Returns declaration identity only and never includes the colour value.
        /// </summary>
        public override string ToString() => $"{LaneId.Value}:{Field.Id}:Declared";
    }

    /// <summary>
    /// An immutable ruleset-neutral snapshot of explicitly declared lane colours for one source bucket projection.
    /// </summary>
    /// <remarks>
    /// Missing lane fields remain <see cref="GameplaySkinConfigurationDeclaration{T}.Absent"/>. The snapshot does not infer
    /// defaults, apply legacy alpha compatibility, validate presentation safety, resolve provider precedence or inject visuals.
    /// It is not a complete Skin V1 configuration, manifest or serialisation ABI.
    /// </remarks>
    public sealed class GameplaySkinLaneColourSnapshot
    {
        private readonly Dictionary<(GameplaySkinLaneId LaneId, GameplaySkinLaneColourField Field), Color4> values;

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        public IReadOnlyList<GameplaySkinLaneColourDeclaration> Declarations { get; }

        private GameplaySkinLaneColourSnapshot(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinLaneColourDeclaration[] declarations,
            Dictionary<(GameplaySkinLaneId LaneId, GameplaySkinLaneColourField Field), Color4> values)
        {
            Topology = topology;
            Declarations = Array.AsReadOnly(declarations);
            this.values = values;
        }

        public static GameplaySkinLaneColourSnapshot Create(
            GameplaySkinLaneTopologySnapshot topology,
            IEnumerable<GameplaySkinLaneColourDeclaration> declarations)
        {
            ArgumentNullException.ThrowIfNull(topology);
            ArgumentNullException.ThrowIfNull(declarations);

            GameplaySkinLaneColourDeclaration[] copiedDeclarations = declarations.ToArray();

            if (copiedDeclarations.Any(declaration => declaration == null))
                throw new ArgumentException("A gameplay skin lane colour snapshot cannot contain a null declaration.", nameof(declarations));

            var copiedValues = new Dictionary<(GameplaySkinLaneId, GameplaySkinLaneColourField), Color4>();

            foreach (GameplaySkinLaneColourDeclaration declaration in copiedDeclarations)
            {
                if (!topology.TryGetLane(declaration.LaneId, out _))
                    throw new ArgumentException("Every lane colour declaration must target a lane in the snapshot topology.", nameof(declarations));

                if (!GameplaySkinLaneColourFieldCatalog.IsCanonical(declaration.Field))
                    throw new ArgumentException("Every lane colour declaration must use a canonical field descriptor.", nameof(declarations));

                if (!copiedValues.TryAdd((declaration.LaneId, declaration.Field), declaration.Value))
                    throw new ArgumentException("A lane colour field may be declared at most once in a source snapshot.", nameof(declarations));
            }

            GameplaySkinLaneColourDeclaration[] orderedDeclarations = copiedDeclarations
                .OrderBy(declaration => getLogicalLaneIndex(topology, declaration.LaneId))
                .ThenBy(declaration => getFieldIndex(declaration.Field))
                .ToArray();

            return new GameplaySkinLaneColourSnapshot(topology, orderedDeclarations, copiedValues);
        }

        public GameplaySkinConfigurationDeclaration<Color4> GetDeclaration(
            GameplaySkinLaneId laneId,
            GameplaySkinLaneColourField field)
        {
            ArgumentNullException.ThrowIfNull(laneId);
            ArgumentNullException.ThrowIfNull(field);

            if (!Topology.TryGetLane(laneId, out _))
                throw new ArgumentException("The requested lane does not belong to this gameplay skin topology.", nameof(laneId));

            if (!GameplaySkinLaneColourFieldCatalog.IsCanonical(field))
                throw new ArgumentException("The requested lane colour field is not part of the closed catalog.", nameof(field));

            return values.TryGetValue((laneId, field), out Color4 value)
                ? GameplaySkinConfigurationDeclaration<Color4>.Declared(value)
                : GameplaySkinConfigurationDeclaration<Color4>.Absent;
        }

        private static int getLogicalLaneIndex(GameplaySkinLaneTopologySnapshot topology, GameplaySkinLaneId laneId)
        {
            topology.TryGetLane(laneId, out GameplaySkinLaneTopologyEntry? lane);
            return lane!.GlobalLogicalIndex;
        }

        private static int getFieldIndex(GameplaySkinLaneColourField field)
        {
            for (int i = 0; i < GameplaySkinLaneColourFieldCatalog.All.Count; i++)
            {
                if (ReferenceEquals(GameplaySkinLaneColourFieldCatalog.All[i], field))
                    return i;
            }

            throw new InvalidOperationException("The lane colour field is not part of the canonical catalog.");
        }

        /// <summary>
        /// Returns only the carrier type and never includes lane IDs or colour values.
        /// </summary>
        public override string ToString() => nameof(GameplaySkinLaneColourSnapshot);
    }
}
