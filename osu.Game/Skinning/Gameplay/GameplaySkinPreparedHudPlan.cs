// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Fixed semantic role of an engine-owned HUD compatibility visual.
    /// </summary>
    public enum GameplaySkinPreparedHudRole
    {
        Gauge = 1,
        Text = 2,
        Combo = 3,
        Judgement = 4,
        Decoration = 5,
    }

    /// <summary>
    /// One exact stage clip and its already-resolved controlling keys.
    /// </summary>
    public sealed class GameplaySkinPreparedHudPartition
    {
        public GameplaySkinResolvedMaterialKey StageKey { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialKey> ControllingKeys { get; }

        public float RelativeStart { get; }

        public float RelativeWidth { get; }

        internal GameplaySkinPreparedHudPartition(
            GameplaySkinResolvedMaterialKey stageKey,
            IEnumerable<GameplaySkinResolvedMaterialKey> controllingKeys,
            float relativeStart,
            float relativeWidth)
        {
            StageKey = stageKey ?? throw new ArgumentNullException(nameof(stageKey));
            ControllingKeys = Array.AsReadOnly(controllingKeys.ToArray());
            RelativeStart = relativeStart;
            RelativeWidth = relativeWidth;
        }
    }

    /// <summary>
    /// One exact gap/outside-stage clip. A global author replacement hides it immediately; otherwise it remains
    /// until every exact stage owns a replacement, preserving the original full-screen compatibility visual.
    /// </summary>
    public sealed class GameplaySkinPreparedHudResidual
    {
        public IReadOnlyList<GameplaySkinResolvedMaterialKey> AnyKeys { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialKey> AllKeys { get; }

        public float RelativeStart { get; }

        public float RelativeWidth { get; }

        internal GameplaySkinPreparedHudResidual(
            IEnumerable<GameplaySkinResolvedMaterialKey> anyKeys,
            IEnumerable<GameplaySkinResolvedMaterialKey> allKeys,
            float relativeStart,
            float relativeWidth)
        {
            AnyKeys = Array.AsReadOnly(anyKeys.ToArray());
            AllKeys = Array.AsReadOnly(allKeys.ToArray());
            RelativeStart = relativeStart;
            RelativeWidth = relativeWidth;
        }
    }

    /// <summary>
    /// Immutable prepare-time route for one allowlisted HUD role.
    /// </summary>
    public sealed class GameplaySkinPreparedHudRolePlan
    {
        public GameplaySkinPreparedHudRole Role { get; }

        public GameplaySkinSlotDescriptor Slot { get; }

        public bool RequiresRouting { get; }

        public IReadOnlyList<GameplaySkinPreparedHudPartition> Partitions { get; }

        public IReadOnlyList<GameplaySkinPreparedHudResidual> Residuals { get; }

        public int MaximumSourceOwners => GameplaySkinPreparedSceneBudgets.MAX_HUD_SOURCE_OWNERS_PER_SLOT;

        internal GameplaySkinPreparedHudRolePlan(
            GameplaySkinPreparedHudRole role,
            GameplaySkinSlotDescriptor slot,
            bool requiresRouting,
            IEnumerable<GameplaySkinPreparedHudPartition> partitions,
            IEnumerable<GameplaySkinPreparedHudResidual> residuals)
        {
            Role = role;
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            RequiresRouting = requiresRouting;
            Partitions = Array.AsReadOnly(partitions.ToArray());
            Residuals = Array.AsReadOnly(residuals.ToArray());
        }
    }

    /// <summary>
    /// Single background-prepared HUD compatibility plan retained by an exact C5 publication.
    /// </summary>
    public sealed class GameplaySkinPreparedHudPlan
    {
        private readonly IReadOnlyDictionary<GameplaySkinPreparedHudRole, GameplaySkinPreparedHudRolePlan> rolesById;

        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public GameplaySkinResolvedMaterialSet MaterialSet { get; }

        public IReadOnlyList<GameplaySkinPreparedHudRolePlan> Roles { get; }

        public int MaximumSourceOwners => GameplaySkinPreparedSceneBudgets.MAX_HUD_SOURCE_OWNERS;

        public int ReservedRuntimeFactoryInstances { get; }

        public int ReservedCaptureSurfaces { get; }

        public long ReservedCaptureSurfacePixels { get; }

        private GameplaySkinPreparedHudPlan(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            IEnumerable<GameplaySkinPreparedHudRolePlan> roles,
            int reservedRuntimeFactoryInstances,
            int reservedCaptureSurfaces,
            long reservedCaptureSurfacePixels)
        {
            Snapshot = snapshot;
            MaterialSet = materialSet;
            GameplaySkinPreparedHudRolePlan[] copiedRoles = roles.ToArray();
            Roles = Array.AsReadOnly(copiedRoles);
            rolesById = copiedRoles.ToDictionary(role => role.Role);
            ReservedRuntimeFactoryInstances = reservedRuntimeFactoryInstances;
            ReservedCaptureSurfaces = reservedCaptureSurfaces;
            ReservedCaptureSurfacePixels = reservedCaptureSurfacePixels;
        }

        public GameplaySkinPreparedHudRolePlan GetRole(GameplaySkinPreparedHudRole role)
            => rolesById.TryGetValue(role, out GameplaySkinPreparedHudRolePlan? plan)
                ? plan
                : throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown prepared HUD role.");

        internal static GameplaySkinPreparedHudPlan Create(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            IReadOnlyList<GameplaySkinPreparedHostedSlot> hostedSlots)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(hostedSlots);

            if (!ReferenceEquals(materialSet.Snapshot, snapshot))
                throw new ArgumentException("A HUD plan must retain the exact material/layout snapshot.", nameof(materialSet));

            var definitions = new[]
            {
                (GameplaySkinPreparedHudRole.Gauge, GameplaySkinSlotCatalog.GaugeVisual),
                (GameplaySkinPreparedHudRole.Text, GameplaySkinSlotCatalog.TextHud),
                (GameplaySkinPreparedHudRole.Combo, GameplaySkinSlotCatalog.ComboDisplay),
                (GameplaySkinPreparedHudRole.Judgement, GameplaySkinSlotCatalog.JudgementDisplay),
                (GameplaySkinPreparedHudRole.Decoration, GameplaySkinSlotCatalog.Decoration),
            };
            var roles = new List<GameplaySkinPreparedHudRolePlan>(definitions.Length);
            int reservedRuntimeFactoryInstances = 0;
            int reservedCaptureSurfaces = 0;
            int totalPartitions = 0;
            int totalResiduals = 0;
            GameplaySkinLayoutRect screen = snapshot.Context.ScreenBounds;

            foreach ((GameplaySkinPreparedHudRole role, GameplaySkinSlotDescriptor slot) in definitions)
            {
                GameplaySkinPreparedHostedSlot? global = hostedSlots.SingleOrDefault(route =>
                    ReferenceEquals(route.Entry.Slot, slot)
                    && route.Entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global);
                GameplaySkinPreparedHostedSlot[] stages = snapshot.Context.Topology.GroupsInLogicalOrder
                    .Select(group => hostedSlots.SingleOrDefault(route =>
                        ReferenceEquals(route.Entry.Slot, slot)
                        && route.Entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Stage
                        && route.Entry.Target.GroupId == group.Identity.Id))
                    .Where(route => route != null)
                    .Cast<GameplaySkinPreparedHostedSlot>()
                    .ToArray();

                bool supported = materialSet.RuntimeSupportProfile.IsSupported(slot);
                bool hasGlobalScope = (slot.AllowedScopes & GameplaySkinSlotScope.Global) != 0;
                bool hasCompleteProductionRoute = (!hasGlobalScope || global != null)
                                                  && stages.Length == snapshot.Context.Topology.GroupsInLogicalOrder.Count;
                bool requiresRouting = supported
                                       && hasCompleteProductionRoute
                                       && (isAuthorOwned(global) || stages.Any(isAuthorOwned));

                if (!requiresRouting)
                {
                    roles.Add(new GameplaySkinPreparedHudRolePlan(
                        role,
                        slot,
                        false,
                        Array.Empty<GameplaySkinPreparedHudPartition>(),
                        Array.Empty<GameplaySkinPreparedHudResidual>()));
                    continue;
                }

                GameplaySkinPreparedHostedSlot[] visualStages = stages.OrderBy(route => route.Rect.Left).ToArray();
                var partitions = new List<GameplaySkinPreparedHudPartition>(visualStages.Length);
                var spans = new List<(float Start, float Width)>(visualStages.Length);

                foreach (GameplaySkinPreparedHostedSlot stage in visualStages)
                {
                    GameplaySkinLayoutRect rect = stage.Rect;

                    if (!screen.Contains(rect)
                        || !float.IsFinite(rect.Left)
                        || !float.IsFinite(rect.Right)
                        || !float.IsFinite(rect.Width))
                    {
                        throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    }

                    float start = (rect.Left - screen.Left) / screen.Width;
                    float width = rect.Width / screen.Width;

                    if (!float.IsFinite(start) || !float.IsFinite(width) || start < 0 || width <= 0 || start + width > 1.0001f)
                        throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidReference);

                    GameplaySkinResolvedMaterialKey[] controllingKeys = global == null
                        ? new[] { stage.Key }
                        : new[] { global.Key, stage.Key };
                    partitions.Add(new GameplaySkinPreparedHudPartition(stage.Key, controllingKeys, start, width));
                    spans.Add((start, width));
                }

                for (int i = 1; i < spans.Count; i++)
                {
                    if (spans[i].Start < spans[i - 1].Start + spans[i - 1].Width)
                        throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidReference);
                }

                var residuals = new List<GameplaySkinPreparedHudResidual>();
                float cursor = 0;
                GameplaySkinResolvedMaterialKey[] stageKeys = visualStages.Select(stage => stage.Key).ToArray();
                GameplaySkinResolvedMaterialKey[] globalKeys = global == null
                    ? Array.Empty<GameplaySkinResolvedMaterialKey>()
                    : new[] { global.Key };

                foreach ((float start, float width) in spans)
                {
                    if (start > cursor)
                        residuals.Add(new GameplaySkinPreparedHudResidual(globalKeys, stageKeys, cursor, start - cursor));

                    cursor = start + width;
                }

                if (cursor < 1)
                    residuals.Add(new GameplaySkinPreparedHudResidual(globalKeys, stageKeys, cursor, 1 - cursor));

                totalPartitions = checked(totalPartitions + partitions.Count);
                totalResiduals = checked(totalResiduals + residuals.Count);
                int segments = checked(partitions.Count + residuals.Count);
                reservedRuntimeFactoryInstances = checked(reservedRuntimeFactoryInstances + 2 + segments * 3);
                reservedCaptureSurfaces++;
                roles.Add(new GameplaySkinPreparedHudRolePlan(role, slot, true, partitions, residuals));
            }

            long reservedCaptureSurfacePixels = checked(
                (long)snapshot.Context.RenderPixelWidth * snapshot.Context.RenderPixelHeight * reservedCaptureSurfaces);

            if (totalPartitions > GameplaySkinPreparedSceneBudgets.MAX_HUD_PARTITION_RECORDS
                || totalResiduals > GameplaySkinPreparedSceneBudgets.MAX_HUD_RESIDUAL_RECORDS
                || reservedRuntimeFactoryInstances > GameplaySkinPreparedSceneBudgets.MAX_HUD_RUNTIME_FACTORY_INSTANCES
                || reservedCaptureSurfaces > GameplaySkinPreparedSceneBudgets.MAX_HUD_CAPTURE_SURFACES
                || reservedCaptureSurfacePixels > GameplaySkinPreparedSceneBudgets.MAX_HUD_CAPTURE_SURFACE_PIXELS)
            {
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }

            return new GameplaySkinPreparedHudPlan(
                snapshot,
                materialSet,
                roles,
                reservedRuntimeFactoryInstances,
                reservedCaptureSurfaces,
                reservedCaptureSurfacePixels);
        }

        private static bool isAuthorOwned(GameplaySkinPreparedHostedSlot? route)
            => route != null
               && (route.Route is GameplaySkinSceneHostRoute.Scene or GameplaySkinSceneHostRoute.Suppressed
                   || route.Entry.Source.IsSelectedDocumentDeclaration);
    }
}
