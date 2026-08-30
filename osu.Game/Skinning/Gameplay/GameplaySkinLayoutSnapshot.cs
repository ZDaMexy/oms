// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// One stable, path-free geometry fallback diagnostic.
    /// </summary>
    public sealed class GameplaySkinLayoutDiagnostic
    {
        public string Code { get; }

        public GameplaySkinLayoutDiagnostic(string code)
        {
            ArgumentException.ThrowIfNullOrEmpty(code);

            if (code.Length > 96 || code.Any(character => !isTokenCharacter(character)))
                throw new ArgumentException("Gameplay layout diagnostics must use a stable lowercase ASCII code.", nameof(code));

            Code = code;
        }

        public override string ToString() => Code;

        private static bool isTokenCharacter(char character)
            => character is >= 'a' and <= 'z'
               or >= '0' and <= '9'
               or '.' or '-';
    }

    /// <summary>
    /// Geometry for one exact topology lane. Identity and all four indices come from <see cref="TopologyEntry"/>.
    /// </summary>
    public sealed class GameplaySkinLayoutLane
    {
        public GameplaySkinLaneTopologyEntry TopologyEntry { get; }

        public GameplaySkinLaneId LaneId => TopologyEntry.Identity.Id;

        public GameplaySkinLayoutRect Rect { get; }

        public GameplaySkinLayoutLane(GameplaySkinLaneTopologyEntry topologyEntry, GameplaySkinLayoutRect rect)
        {
            ArgumentNullException.ThrowIfNull(topologyEntry);
            TopologyEntry = topologyEntry;
            Rect = rect;
        }
    }

    /// <summary>
    /// Geometry for one exact topology group/stage.
    /// </summary>
    public sealed class GameplaySkinLayoutGroup
    {
        public GameplaySkinLaneTopologyGroup TopologyGroup { get; }

        public GameplaySkinLaneGroupId GroupId => TopologyGroup.Identity.Id;

        public GameplaySkinLayoutRect Rect { get; }

        public GameplaySkinLayoutGroup(GameplaySkinLaneTopologyGroup topologyGroup, GameplaySkinLayoutRect rect)
        {
            ArgumentNullException.ThrowIfNull(topologyGroup);
            TopologyGroup = topologyGroup;
            Rect = rect;
        }
    }

    /// <summary>
    /// A named production surface with explicit stacking, clipping and input semantics.
    /// </summary>
    public sealed class GameplaySkinLayoutSurface
    {
        public string Id { get; }

        public GameplaySkinLayoutRect Rect { get; }

        public int ZIndex { get; }

        public bool Clips { get; }

        public bool ReceivesPositionalInput { get; }

        public GameplaySkinLayoutSurface(
            string id,
            GameplaySkinLayoutRect rect,
            int zIndex,
            bool clips,
            bool receivesPositionalInput)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            if (id.Length > 80 || id.Any(character => !isTokenCharacter(character)))
                throw new ArgumentException("Gameplay layout surface IDs must be short lowercase ASCII values.", nameof(id));

            Id = id;
            Rect = rect;
            ZIndex = zIndex;
            Clips = clips;
            ReceivesPositionalInput = receivesPositionalInput;
        }

        private static bool isTokenCharacter(char character)
            => character is >= 'a' and <= 'z'
               or >= '0' and <= '9'
               or '.' or '-';
    }

    /// <summary>
    /// The one immutable, ruleset-neutral geometry snapshot consumed by a gameplay tree.
    /// </summary>
    public sealed class GameplaySkinLayoutSnapshot
    {
        private readonly Dictionary<GameplaySkinLaneId, GameplaySkinLayoutLane> lanesById;
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinLayoutGroup> groupsById;
        private readonly Dictionary<string, GameplaySkinLayoutSurface> surfacesById;

        public GameplaySkinLayoutContext Context { get; }

        public IReadOnlyList<GameplaySkinLayoutGroup> GroupsInLogicalOrder { get; }

        public IReadOnlyList<GameplaySkinLayoutLane> LanesInLogicalOrder { get; }

        public IReadOnlyList<GameplaySkinLayoutSurface> Surfaces { get; }

        public IReadOnlyList<GameplaySkinLayoutRect> BgaViewports { get; }

        public IReadOnlyList<GameplaySkinLayoutDiagnostic> Diagnostics { get; }

        private GameplaySkinLayoutSnapshot(
            GameplaySkinLayoutContext context,
            GameplaySkinLayoutGroup[] groups,
            GameplaySkinLayoutLane[] lanes,
            GameplaySkinLayoutSurface[] surfaces,
            GameplaySkinLayoutRect[] bgaViewports,
            GameplaySkinLayoutDiagnostic[] diagnostics)
        {
            Context = context;
            GroupsInLogicalOrder = Array.AsReadOnly(groups);
            LanesInLogicalOrder = Array.AsReadOnly(lanes);
            Surfaces = Array.AsReadOnly(surfaces);
            BgaViewports = Array.AsReadOnly(bgaViewports);
            Diagnostics = Array.AsReadOnly(diagnostics);
            groupsById = groups.ToDictionary(group => group.GroupId);
            lanesById = lanes.ToDictionary(lane => lane.LaneId);
            surfacesById = surfaces.ToDictionary(surface => surface.Id, StringComparer.Ordinal);
        }

        public static GameplaySkinLayoutSnapshot Create(
            GameplaySkinLayoutContext context,
            IEnumerable<GameplaySkinLayoutGroup> groups,
            IEnumerable<GameplaySkinLayoutLane> lanes,
            IEnumerable<GameplaySkinLayoutSurface> surfaces,
            IEnumerable<GameplaySkinLayoutRect>? bgaViewports = null,
            IEnumerable<GameplaySkinLayoutDiagnostic>? diagnostics = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(groups);
            ArgumentNullException.ThrowIfNull(lanes);
            ArgumentNullException.ThrowIfNull(surfaces);

            GameplaySkinLayoutGroup[] copiedGroups = groups.ToArray();
            GameplaySkinLayoutLane[] copiedLanes = lanes.ToArray();
            GameplaySkinLayoutSurface[] copiedSurfaces = surfaces.ToArray();
            GameplaySkinLayoutRect[] copiedBgaViewports = bgaViewports?.ToArray() ?? Array.Empty<GameplaySkinLayoutRect>();
            GameplaySkinLayoutDiagnostic[] copiedDiagnostics = diagnostics?.ToArray() ?? Array.Empty<GameplaySkinLayoutDiagnostic>();

            if (copiedGroups.Any(group => group == null) || copiedLanes.Any(lane => lane == null)
                || copiedSurfaces.Any(surface => surface == null) || copiedDiagnostics.Any(diagnostic => diagnostic == null))
            {
                throw new ArgumentException("Gameplay layout snapshot collections cannot contain null entries.");
            }

            GameplaySkinLaneTopologyGroup[] topologyGroups = context.Topology.GroupsInLogicalOrder.ToArray();
            GameplaySkinLaneTopologyEntry[] topologyLanes = context.Topology.LanesInLogicalOrder.ToArray();

            if (copiedGroups.Length != topologyGroups.Length
                || !copiedGroups.Select(group => group.TopologyGroup).SequenceEqual(topologyGroups))
            {
                throw new ArgumentException("Layout groups must use every exact topology group in logical order.", nameof(groups));
            }

            if (copiedLanes.Length != topologyLanes.Length
                || !copiedLanes.Select(lane => lane.TopologyEntry).SequenceEqual(topologyLanes))
            {
                throw new ArgumentException("Layout lanes must use every exact topology lane in logical order.", nameof(lanes));
            }

            if (copiedGroups.Any(group => !context.SafeBounds.Contains(group.Rect))
                || copiedLanes.Any(lane => !context.SafeBounds.Contains(lane.Rect))
                || copiedSurfaces.Any(surface => !context.SafeBounds.Contains(surface.Rect))
                || copiedBgaViewports.Any(viewport => !context.SafeBounds.Contains(viewport)))
            {
                throw new ArgumentException("Every gameplay layout rectangle must remain inside the exact safe bounds.");
            }

            if (copiedGroups.GroupBy(group => group.GroupId).Any(group => group.Skip(1).Any())
                || copiedLanes.GroupBy(lane => lane.LaneId).Any(group => group.Skip(1).Any())
                || copiedSurfaces.GroupBy(surface => surface.Id, StringComparer.Ordinal).Any(group => group.Skip(1).Any()))
            {
                throw new ArgumentException("Gameplay layout group, lane and surface identifiers must be unique.");
            }

            foreach (GameplaySkinLayoutGroup group in copiedGroups)
            {
                GameplaySkinLayoutLane[] groupLanes = copiedLanes
                                                       .Where(lane => lane.TopologyEntry.Identity.Group.Id == group.GroupId)
                                                       .ToArray();

                if (groupLanes.Length == 0 || groupLanes.Any(lane => !group.Rect.Contains(lane.Rect)))
                    throw new ArgumentException("Every lane rectangle must be contained by its exact topology group.", nameof(lanes));
            }

            return new GameplaySkinLayoutSnapshot(
                context,
                copiedGroups,
                copiedLanes,
                copiedSurfaces,
                copiedBgaViewports,
                copiedDiagnostics);
        }

        public GameplaySkinLayoutLane GetLane(GameplaySkinLaneId laneId)
        {
            ArgumentNullException.ThrowIfNull(laneId);
            return lanesById.TryGetValue(laneId, out GameplaySkinLayoutLane? lane)
                ? lane
                : throw new KeyNotFoundException("The requested lane identity is not part of this exact layout snapshot.");
        }

        public GameplaySkinLayoutGroup GetGroup(GameplaySkinLaneGroupId groupId)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            return groupsById.TryGetValue(groupId, out GameplaySkinLayoutGroup? group)
                ? group
                : throw new KeyNotFoundException("The requested group identity is not part of this exact layout snapshot.");
        }

        public GameplaySkinLayoutSurface GetSurface(string id)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            return surfacesById.TryGetValue(id, out GameplaySkinLayoutSurface? surface)
                ? surface
                : throw new KeyNotFoundException("The requested surface is not part of this exact layout snapshot.");
        }

        public override string ToString() => nameof(GameplaySkinLayoutSnapshot);
    }
}
