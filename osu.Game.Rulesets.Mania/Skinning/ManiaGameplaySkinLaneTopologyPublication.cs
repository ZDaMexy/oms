// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// One immutable mania-native stage context paired with its engine-issued neutral lane-topology publication.
    /// </summary>
    /// <remarks>
    /// The stage-column vector is native topology continuity authority which cannot be inferred from total columns or neutral
    /// lane IDs. This internal process-local wrapper is not a complete gameplay layout context or wire ABI.
    /// </remarks>
    internal sealed class ManiaGameplaySkinLaneTopologyPublication
    {
        public IReadOnlyList<int> StageColumnCounts { get; }

        public GameplaySkinLaneTopologyPublication Publication { get; }

        internal ManiaGameplaySkinLaneTopologyPublication(
            ManiaGameplaySkinLaneTopologyProjection projection,
            GameplaySkinLaneTopologyPublication publication)
        {
            ArgumentNullException.ThrowIfNull(projection);
            ArgumentNullException.ThrowIfNull(publication);

            if (!ReferenceEquals(projection.Topology, publication.Topology))
                throw new ArgumentException("The mania native context must wrap the exact topology issued by the shared revision owner.", nameof(publication));

            StageColumnCounts = Array.AsReadOnly(projection.StageColumnCounts.ToArray());
            Publication = publication;
        }

        public override string ToString() => $"Mania:Revision{Publication.Revision}";
    }

    /// <summary>
    /// Issues consecutive mania lane-topology publications for one gameplay attachment.
    /// </summary>
    /// <remarks>
    /// Native continuity requires the exact ordered stage-column vector. A changed stage count, changed per-stage column count or
    /// reordered dual-stage shape is rejected even if total columns happen to match. Projection, native-context, neutral-transition
    /// and revision-overflow rejection never replaces <see cref="Current"/> or consumes a revision. This owner does not connect
    /// production layout or rendering.
    /// </remarks>
    internal sealed class ManiaGameplaySkinLaneTopologyRevisionOwner
    {
        private readonly GameplaySkinLaneTopologyRevisionOwner<IReadOnlyList<int>> revisionOwner =
            new GameplaySkinLaneTopologyRevisionOwner<IReadOnlyList<int>>(stageShapesMatch);

        public ManiaGameplaySkinLaneTopologyPublication? Current { get; private set; }

        public ManiaGameplaySkinLaneTopologyPublication Publish(ManiaBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            ManiaGameplaySkinLaneTopologyProjection projection = ManiaGameplaySkinLaneTopologyFactory.CreateProjection(beatmap);
            GameplaySkinLaneTopologyPublication publication = revisionOwner.Publish(projection.StageColumnCounts, projection.Topology);
            var result = new ManiaGameplaySkinLaneTopologyPublication(projection, publication);

            Current = result;
            return result;
        }

        private static bool stageShapesMatch(IReadOnlyList<int> previous, IReadOnlyList<int> current)
            => previous.SequenceEqual(current);

        public override string ToString() => nameof(ManiaGameplaySkinLaneTopologyRevisionOwner);
    }
}
