// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// One immutable, engine-owned publication of a gameplay skin lane topology.
    /// </summary>
    /// <remarks>
    /// The revision identifies a successful publication within one process-local owner. It is not a package revision,
    /// serialisation or event wire ABI, and this topology-only frame is not a complete gameplay skin layout context.
    /// </remarks>
    public sealed class GameplaySkinLaneTopologyPublication
    {
        public long Revision { get; }

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        private GameplaySkinLaneTopologyPublication(long revision, GameplaySkinLaneTopologySnapshot topology)
        {
            Revision = revision;
            Topology = topology;
        }

        internal static GameplaySkinLaneTopologyPublication Create(long revision, GameplaySkinLaneTopologySnapshot topology)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(revision);
            ArgumentNullException.ThrowIfNull(topology);

            return new GameplaySkinLaneTopologyPublication(revision, topology);
        }

        /// <summary>
        /// Returns only the carrier type and never includes native context or topology data.
        /// </summary>
        public override string ToString() => nameof(GameplaySkinLaneTopologyPublication);
    }
}
