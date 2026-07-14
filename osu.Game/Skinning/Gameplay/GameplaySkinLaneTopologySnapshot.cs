// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The current order projection of one gameplay lane within a topology snapshot.
    /// </summary>
    /// <remarks>
    /// Logical indices are stable only while the topology is unchanged. Visual indices may change between presentation revisions.
    /// These indices are current snapshot data, never identity, geometry, a ruleset action or an author-facing manifest ABI.
    /// </remarks>
    public sealed class GameplaySkinLaneTopologyEntry
    {
        public GameplaySkinLaneIdentity Identity { get; }

        public int GlobalLogicalIndex { get; }

        public int GroupLocalLogicalIndex { get; }

        public int GlobalVisualIndex { get; }

        public int GroupLocalVisualIndex { get; }

        private GameplaySkinLaneTopologyEntry(
            GameplaySkinLaneIdentity identity,
            int globalLogicalIndex,
            int groupLocalLogicalIndex,
            int globalVisualIndex,
            int groupLocalVisualIndex)
        {
            Identity = identity;
            GlobalLogicalIndex = globalLogicalIndex;
            GroupLocalLogicalIndex = groupLocalLogicalIndex;
            GlobalVisualIndex = globalVisualIndex;
            GroupLocalVisualIndex = groupLocalVisualIndex;
        }

        public static GameplaySkinLaneTopologyEntry Create(
            GameplaySkinLaneIdentity identity,
            int globalLogicalIndex,
            int groupLocalLogicalIndex,
            int globalVisualIndex,
            int groupLocalVisualIndex)
        {
            ArgumentNullException.ThrowIfNull(identity);

            ArgumentOutOfRangeException.ThrowIfNegative(globalLogicalIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(groupLocalLogicalIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(globalVisualIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(groupLocalVisualIndex);

            return new GameplaySkinLaneTopologyEntry(identity, globalLogicalIndex, groupLocalLogicalIndex, globalVisualIndex, groupLocalVisualIndex);
        }
    }

    /// <summary>
    /// One immutable lane-group projection within a gameplay topology snapshot.
    /// </summary>
    public sealed class GameplaySkinLaneTopologyGroup
    {
        public GameplaySkinLaneGroupIdentity Identity { get; }

        public int LogicalIndex { get; }

        public int VisualIndex { get; }

        public IReadOnlyList<GameplaySkinLaneTopologyEntry> LanesInLogicalOrder { get; }

        public IReadOnlyList<GameplaySkinLaneTopologyEntry> LanesInVisualOrder { get; }

        private GameplaySkinLaneTopologyGroup(
            GameplaySkinLaneGroupIdentity identity,
            int logicalIndex,
            int visualIndex,
            GameplaySkinLaneTopologyEntry[] lanesInLogicalOrder,
            GameplaySkinLaneTopologyEntry[] lanesInVisualOrder)
        {
            Identity = identity;
            LogicalIndex = logicalIndex;
            VisualIndex = visualIndex;
            LanesInLogicalOrder = Array.AsReadOnly(lanesInLogicalOrder);
            LanesInVisualOrder = Array.AsReadOnly(lanesInVisualOrder);
        }

        public static GameplaySkinLaneTopologyGroup Create(
            GameplaySkinLaneGroupIdentity identity,
            int logicalIndex,
            int visualIndex,
            IEnumerable<GameplaySkinLaneTopologyEntry> lanes)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(lanes);

            ArgumentOutOfRangeException.ThrowIfNegative(logicalIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(visualIndex);

            GameplaySkinLaneTopologyEntry[] copiedLanes = lanes.ToArray();

            if (copiedLanes.Length == 0)
                throw new ArgumentException("A gameplay skin lane group must contain at least one lane.", nameof(lanes));

            if (copiedLanes.Any(lane => lane == null))
                throw new ArgumentException("A gameplay skin lane group cannot contain a null lane.", nameof(lanes));

            if (copiedLanes.Any(lane => lane.Identity.Group != identity))
                throw new ArgumentException("Every lane must reference the exact identity of its containing group.", nameof(lanes));

            if (copiedLanes.GroupBy(lane => lane.Identity.Id).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("Gameplay skin lane IDs must be unique within a topology group.", nameof(lanes));

            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedLanes.Select(lane => lane.GroupLocalLogicalIndex), copiedLanes.Length, nameof(lanes), "Group-local logical lane indices");
            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedLanes.Select(lane => lane.GroupLocalVisualIndex), copiedLanes.Length, nameof(lanes), "Group-local visual lane indices");

            GameplaySkinLaneTopologyEntry[] lanesInLogicalOrder = copiedLanes.OrderBy(lane => lane.GlobalLogicalIndex).ToArray();
            GameplaySkinLaneTopologyEntry[] lanesInVisualOrder = copiedLanes.OrderBy(lane => lane.GlobalVisualIndex).ToArray();

            GameplaySkinLaneTopologyIndexValidation.EnsureExactOrder(
                lanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), nameof(lanes), "Group-local logical lane order");
            GameplaySkinLaneTopologyIndexValidation.EnsureExactOrder(
                lanesInVisualOrder.Select(lane => lane.GroupLocalVisualIndex), nameof(lanes), "Group-local visual lane order");

            return new GameplaySkinLaneTopologyGroup(identity, logicalIndex, visualIndex, lanesInLogicalOrder, lanesInVisualOrder);
        }
    }

    /// <summary>
    /// An immutable ruleset-neutral snapshot of gameplay lane grouping and order.
    /// </summary>
    /// <remarks>
    /// This snapshot deliberately excludes keymode, style, action, source channel, geometry, revision and native ruleset context.
    /// It is not <c>GameplaySkinLayoutContext</c> and does not define a JSON, event or author-facing manifest ABI.
    /// <see cref="Create"/> validates one snapshot. Callers that have separately established compatible native context can use
    /// <see cref="GameplaySkinLaneTopologyTransitionValidator"/> to validate a topology-preserving neutral transition.
    /// </remarks>
    public sealed class GameplaySkinLaneTopologySnapshot
    {
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinLaneTopologyGroup> groupsById;
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinLaneTopologyEntry> lanesById;

        public IReadOnlyList<GameplaySkinLaneTopologyGroup> GroupsInLogicalOrder { get; }

        public IReadOnlyList<GameplaySkinLaneTopologyGroup> GroupsInVisualOrder { get; }

        public IReadOnlyList<GameplaySkinLaneTopologyEntry> LanesInLogicalOrder { get; }

        public IReadOnlyList<GameplaySkinLaneTopologyEntry> LanesInVisualOrder { get; }

        private GameplaySkinLaneTopologySnapshot(
            GameplaySkinLaneTopologyGroup[] groupsInLogicalOrder,
            GameplaySkinLaneTopologyGroup[] groupsInVisualOrder,
            GameplaySkinLaneTopologyEntry[] lanesInLogicalOrder,
            GameplaySkinLaneTopologyEntry[] lanesInVisualOrder)
        {
            GroupsInLogicalOrder = Array.AsReadOnly(groupsInLogicalOrder);
            GroupsInVisualOrder = Array.AsReadOnly(groupsInVisualOrder);
            LanesInLogicalOrder = Array.AsReadOnly(lanesInLogicalOrder);
            LanesInVisualOrder = Array.AsReadOnly(lanesInVisualOrder);

            groupsById = groupsInLogicalOrder.ToDictionary(group => group.Identity.Id);
            lanesById = lanesInLogicalOrder.ToDictionary(lane => lane.Identity.Id);
        }

        public static GameplaySkinLaneTopologySnapshot Create(IEnumerable<GameplaySkinLaneTopologyGroup> groups)
        {
            ArgumentNullException.ThrowIfNull(groups);

            GameplaySkinLaneTopologyGroup[] copiedGroups = groups.ToArray();

            if (copiedGroups.Length == 0)
                throw new ArgumentException("A gameplay skin lane topology must contain at least one group.", nameof(groups));

            if (copiedGroups.Any(group => group == null))
                throw new ArgumentException("A gameplay skin lane topology cannot contain a null group.", nameof(groups));

            if (copiedGroups.GroupBy(group => group.Identity.Id).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("Gameplay skin lane group IDs must be unique within a topology.", nameof(groups));

            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedGroups.Select(group => group.LogicalIndex), copiedGroups.Length, nameof(groups), "Logical group indices");
            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedGroups.Select(group => group.VisualIndex), copiedGroups.Length, nameof(groups), "Visual group indices");

            GameplaySkinLaneTopologyEntry[] copiedLanes = copiedGroups.SelectMany(group => group.LanesInLogicalOrder).ToArray();

            if (copiedLanes.GroupBy(lane => lane.Identity.Id).Any(group => group.Skip(1).Any()))
                throw new ArgumentException("Gameplay skin lane IDs must be unique across all groups in a topology.", nameof(groups));

            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedLanes.Select(lane => lane.GlobalLogicalIndex), copiedLanes.Length, nameof(groups), "Global logical lane indices");
            GameplaySkinLaneTopologyIndexValidation.EnsurePermutation(
                copiedLanes.Select(lane => lane.GlobalVisualIndex), copiedLanes.Length, nameof(groups), "Global visual lane indices");

            GameplaySkinLaneTopologyGroup[] groupsInLogicalOrder = copiedGroups.OrderBy(group => group.LogicalIndex).ToArray();
            GameplaySkinLaneTopologyGroup[] groupsInVisualOrder = copiedGroups.OrderBy(group => group.VisualIndex).ToArray();
            GameplaySkinLaneTopologyEntry[] lanesInLogicalOrder = copiedLanes.OrderBy(lane => lane.GlobalLogicalIndex).ToArray();
            GameplaySkinLaneTopologyEntry[] lanesInVisualOrder = copiedLanes.OrderBy(lane => lane.GlobalVisualIndex).ToArray();

            if (!groupsInLogicalOrder.SelectMany(group => group.LanesInLogicalOrder).SequenceEqual(lanesInLogicalOrder))
                throw new ArgumentException("Logical groups must form contiguous blocks in global logical lane order.", nameof(groups));

            if (!groupsInVisualOrder.SelectMany(group => group.LanesInVisualOrder).SequenceEqual(lanesInVisualOrder))
                throw new ArgumentException("Visual groups must form contiguous blocks in global visual lane order.", nameof(groups));

            return new GameplaySkinLaneTopologySnapshot(groupsInLogicalOrder, groupsInVisualOrder, lanesInLogicalOrder, lanesInVisualOrder);
        }

        public bool TryGetGroup(GameplaySkinLaneGroupId id, out GameplaySkinLaneTopologyGroup? group)
        {
            ArgumentNullException.ThrowIfNull(id);
            return groupsById.TryGetValue(id, out group);
        }

        public bool TryGetLane(GameplaySkinLaneId id, out GameplaySkinLaneTopologyEntry? lane)
        {
            ArgumentNullException.ThrowIfNull(id);
            return lanesById.TryGetValue(id, out lane);
        }
    }

    internal static class GameplaySkinLaneTopologyIndexValidation
    {
        public static void EnsurePermutation(IEnumerable<int> indices, int count, string parameterName, string description)
        {
            if (!indices.Order().SequenceEqual(Enumerable.Range(0, count)))
                throw new ArgumentException($"{description} must contain each index from zero through count minus one exactly once.", parameterName);
        }

        public static void EnsureExactOrder(IEnumerable<int> indices, string parameterName, string description)
        {
            int[] copiedIndices = indices.ToArray();

            if (!copiedIndices.SequenceEqual(Enumerable.Range(0, copiedIndices.Length)))
                throw new ArgumentException($"{description} must agree with its corresponding global order.", parameterName);
        }
    }
}
