// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Validates that two neutral lane topology snapshots describe a topology-preserving transition.
    /// </summary>
    /// <remarks>
    /// This validator only checks stable neutral topology data. It deliberately allows presentation side and visual order to change,
    /// and does not decide whether native keymode, style, action, source channel, geometry or revision context is compatible.
    /// Callers must establish that native context separately before using this process-local CLR contract. This is not a manifest,
    /// event or wire ABI.
    /// </remarks>
    public static class GameplaySkinLaneTopologyTransitionValidator
    {
        /// <summary>
        /// Validates that <paramref name="current"/> preserves the stable topology represented by <paramref name="previous"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Either snapshot is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="current"/> does not preserve the neutral topology.</exception>
        public static void Validate(GameplaySkinLaneTopologySnapshot previous, GameplaySkinLaneTopologySnapshot current)
        {
            ArgumentNullException.ThrowIfNull(previous);
            ArgumentNullException.ThrowIfNull(current);

            if (previous.GroupsInLogicalOrder.Count != current.GroupsInLogicalOrder.Count)
                throw transitionViolation("must contain the same number of lane groups", nameof(current));

            foreach (GameplaySkinLaneTopologyGroup previousGroup in previous.GroupsInLogicalOrder)
            {
                if (!current.TryGetGroup(previousGroup.Identity.Id, out GameplaySkinLaneTopologyGroup? currentGroup) || currentGroup == null)
                    throw transitionViolation($"is missing lane group '{previousGroup.Identity.Id}'", nameof(current));

                if (previousGroup.LogicalIndex != currentGroup.LogicalIndex)
                    throw transitionViolation($"changed the logical index of lane group '{previousGroup.Identity.Id}'", nameof(current));
            }

            if (previous.LanesInLogicalOrder.Count != current.LanesInLogicalOrder.Count)
                throw transitionViolation("must contain the same number of lanes", nameof(current));

            foreach (GameplaySkinLaneTopologyEntry previousLane in previous.LanesInLogicalOrder)
            {
                if (!current.TryGetLane(previousLane.Identity.Id, out GameplaySkinLaneTopologyEntry? currentLane) || currentLane == null)
                    throw transitionViolation($"is missing lane '{previousLane.Identity.Id}'", nameof(current));

                if (previousLane.Identity.Group.Id != currentLane.Identity.Group.Id)
                    throw transitionViolation($"changed the lane group of lane '{previousLane.Identity.Id}'", nameof(current));

                if (previousLane.Identity.Role != currentLane.Identity.Role)
                    throw transitionViolation($"changed the role of lane '{previousLane.Identity.Id}'", nameof(current));

                if (previousLane.GlobalLogicalIndex != currentLane.GlobalLogicalIndex
                    || previousLane.GroupLocalLogicalIndex != currentLane.GroupLocalLogicalIndex)
                    throw transitionViolation($"changed the global or group-local logical index of lane '{previousLane.Identity.Id}'", nameof(current));
            }
        }

        private static ArgumentException transitionViolation(string description, string parameterName)
            => new($"A topology-preserving gameplay skin lane transition {description}.", parameterName);
    }
}
