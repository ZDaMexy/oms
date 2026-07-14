// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// An immutable projection of mania's native stage shape and its neutral gameplay skin lane topology.
    /// </summary>
    /// <remarks>
    /// The stage-column vector preserves native continuity which cannot be recovered from neutral lane identity alone.
    /// This process-local carrier is not a layout, event, manifest or serialisation ABI.
    /// </remarks>
    internal sealed class ManiaGameplaySkinLaneTopologyProjection
    {
        public IReadOnlyList<int> StageColumnCounts { get; }

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        private ManiaGameplaySkinLaneTopologyProjection(int[] stageColumnCounts, GameplaySkinLaneTopologySnapshot topology)
        {
            StageColumnCounts = Array.AsReadOnly(stageColumnCounts);
            Topology = topology;
        }

        internal static ManiaGameplaySkinLaneTopologyProjection Create(IEnumerable<int> stageColumnCounts)
        {
            ArgumentNullException.ThrowIfNull(stageColumnCounts);

            int[] copiedStageColumnCounts = stageColumnCounts.ToArray();

            if (copiedStageColumnCounts.Length is < 1 or > 2)
                throw new ArgumentException("A mania gameplay skin topology projection must contain one or two stages.", nameof(stageColumnCounts));

            if (copiedStageColumnCounts.Any(columns => columns is < 1 or > ManiaRuleset.MAX_STAGE_KEYS))
                throw new ArgumentException("Every projected mania stage must use the supported canonical key-count range.", nameof(stageColumnCounts));

            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.CreateCanonicalTopology(copiedStageColumnCounts);

            return new ManiaGameplaySkinLaneTopologyProjection(copiedStageColumnCounts, topology);
        }

        /// <summary>
        /// Returns only the carrier type and never expands native context data.
        /// </summary>
        public override string ToString() => nameof(ManiaGameplaySkinLaneTopologyProjection);
    }
}
