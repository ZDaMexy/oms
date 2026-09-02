// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Identifies one field-level BMS lane-resource lookup without carrying its resource name into slot identity.
    /// </summary>
    internal sealed class BmsGameplaySkinLaneResourceContext
    {
        internal GameplaySkinLaneTopologySnapshot Topology { get; }

        public GameplaySkinLaneId LaneId { get; }

        public GameplaySkinLaneResourceField Field { get; }

        internal BmsGameplaySkinLaneResourceContext(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinLaneId laneId,
            GameplaySkinLaneResourceField field)
        {
            ArgumentNullException.ThrowIfNull(topology);
            ArgumentNullException.ThrowIfNull(laneId);
            ArgumentNullException.ThrowIfNull(field);

            if (!GameplaySkinLaneResourceFieldCatalog.TryGet(field.Id, out GameplaySkinLaneResourceField? canonical)
                || !ReferenceEquals(field, canonical)
                || !BmsGameplaySkinNoteResourceFields.Contains(field))
                throw new ArgumentException("The BMS lane-resource context must use one hosted Note/LN field descriptor.", nameof(field));

            if (!topology.TryGetLane(laneId, out _))
                throw new ArgumentException("The lane-resource context must target the supplied topology.", nameof(laneId));

            Topology = topology;
            LaneId = laneId;
            Field = field;
        }

        public override string ToString() => $"{LaneId.Value}:{Field.Id}";
    }

    /// <summary>
    /// A source-aware resource declaration passed to a materializer before it may become a slot <c>Provide</c>.
    /// </summary>
    internal sealed class BmsGameplaySkinLaneResourceReference
    {
        public BmsGameplaySkinConfigurationCandidateSource Source { get; }

        public int? ManiaKeys { get; }

        public GameplaySkinLaneId LaneId { get; }

        public GameplaySkinLaneResourceField Field { get; }

        /// <summary>
        /// The unvalidated package-relative resource name. It must not be copied into persistent diagnostics.
        /// </summary>
        public string ResourceName { get; }

        internal BmsGameplaySkinLaneResourceReference(
            BmsGameplaySkinConfigurationCandidate candidate,
            BmsGameplaySkinLaneResourceContext context,
            string resourceName)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(resourceName);

            Source = candidate.Source;
            ManiaKeys = candidate.ManiaKeys;
            LaneId = context.LaneId;
            Field = context.Field;
            ResourceName = resourceName;
        }

        internal BmsGameplaySkinLaneResourceReference(
            BmsGameplaySkinLaneResourceContext context,
            string resourceName)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(resourceName);

            Source = BmsGameplaySkinConfigurationCandidateSource.SelectedDocument;
            LaneId = context.LaneId;
            Field = context.Field;
            ResourceName = resourceName;
        }

        /// <summary>
        /// Returns declaration identity only and never includes the resource name.
        /// </summary>
        public override string ToString() => ManiaKeys.HasValue
            ? $"{Source}:Keys{ManiaKeys}:{LaneId.Value}:{Field.Id}"
            : $"{Source}:{LaneId.Value}:{Field.Id}";
    }

    /// <summary>
    /// Owns every component materialized for one selected-package configuration revision.
    /// </summary>
    /// <remarks>
    /// <see cref="Materialize"/> must retain ownership before returning a component and must perform the component's
    /// basic validation. Both winning components and candidates rejected by an additional resolver validator remain
    /// borrowed from this owner. The caller must keep an active owner alive while any resolved component is in use. A
    /// failed reload disposes only its new provisional owner and leaves the active owner untouched. After a successful
    /// atomic replacement, the caller must detach consumers from the superseded revision before disposing its owner;
    /// gameplay teardown follows the same detach-then-dispose order. The resolver and candidate providers never dispose
    /// the owner or individual components.
    /// </remarks>
    internal interface IBmsGameplaySkinLaneResourceComponentOwner<TComponent> : IDisposable
        where TComponent : notnull
    {
        TComponent Materialize(BmsGameplaySkinLaneResourceReference reference);
    }

    /// <summary>
    /// Adapts the selected package's BMS-to-mania declaration plan into ordered gameplay slot providers.
    /// </summary>
    /// <remarks>
    /// Production C4 composes the bound selected-package document first, then these selected-package BMS/mania
    /// compatibility buckets and the real lower-authority providers in one final resolver chain.
    /// Legacy beatmap direct-drawable compatibility remains outside this authoring authority and is never inserted into
    /// this candidate plan. A declaration never becomes <c>Provide</c> until the supplied
    /// owner has constructed, retained, and performed basic validation of a component. Missing declarations inherit
    /// without invoking the owner, and ini declarations can never manufacture <c>Suppress</c>. The caller owns the
    /// revision-scoped owner supplied to <see cref="Create{TComponent}"/>; it must outlive every borrowed resolved value.
    /// This process-local adapter does not load files, connect <see cref="ISkin"/>, or define an author-facing ABI.
    /// </remarks>
    internal static class BmsGameplaySkinLaneResourceCandidateProviderFactory
    {
        public static IReadOnlyList<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TComponent>> Create<TComponent>(
            BmsGameplaySkinConfigurationCandidatePlan plan,
            IBmsGameplaySkinLaneResourceComponentOwner<TComponent> componentOwner)
            where TComponent : notnull
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(componentOwner);

            var providers = new List<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TComponent>>();

            foreach (BmsGameplaySkinConfigurationCandidate candidate in plan.Candidates)
                providers.Add(new CandidateProvider<TComponent>(plan.Topology, candidate, componentOwner));

            return providers.AsReadOnly();
        }

        private sealed class CandidateProvider<TComponent> : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TComponent>
            where TComponent : notnull
        {
            private readonly GameplaySkinLaneTopologySnapshot topology;
            private readonly BmsGameplaySkinConfigurationCandidate candidate;
            private readonly IBmsGameplaySkinLaneResourceComponentOwner<TComponent> componentOwner;

            public string Name { get; }

            public CandidateProvider(
                GameplaySkinLaneTopologySnapshot topology,
                BmsGameplaySkinConfigurationCandidate candidate,
                IBmsGameplaySkinLaneResourceComponentOwner<TComponent> componentOwner)
            {
                ArgumentNullException.ThrowIfNull(topology);
                ArgumentNullException.ThrowIfNull(candidate);
                ArgumentNullException.ThrowIfNull(componentOwner);

                if (candidate.Snapshot.IsDeclared && !ReferenceEquals(candidate.Snapshot.Value.Topology, topology))
                    throw new ArgumentException("A candidate provider must use the plan's exact immutable topology.", nameof(candidate));

                this.topology = topology;
                this.candidate = candidate;
                this.componentOwner = componentOwner;
                Name = getProviderName(candidate);
            }

            public SkinSlotResult<TComponent> GetSlot(GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext> slot)
            {
                ArgumentNullException.ThrowIfNull(slot);

                BmsGameplaySkinLaneResourceContext context = slot.Context;

                if (!ReferenceEquals(slot.Descriptor, context.Field.Slot))
                    throw new ArgumentException("The lane-resource lookup descriptor does not match its field.", nameof(slot));

                if (!ReferenceEquals(context.Topology, topology))
                    throw new ArgumentException("The lane-resource lookup must use the candidate plan's exact immutable topology.", nameof(slot));

                if (!candidate.Snapshot.IsDeclared)
                    return SkinSlotResult<TComponent>.Inherit;

                GameplaySkinConfigurationDeclaration<string> declaration = candidate.Snapshot.Value.GetDeclaration(context.LaneId, context.Field);

                if (!declaration.IsDeclared)
                    return SkinSlotResult<TComponent>.Inherit;

                var reference = new BmsGameplaySkinLaneResourceReference(candidate, context, declaration.Value);
                return SkinSlotResult<TComponent>.Provide(componentOwner.Materialize(reference));
            }

            private static string getProviderName(BmsGameplaySkinConfigurationCandidate candidate)
            {
                return candidate.Source switch
                {
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride => "selected.bms-role-override",
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane => $"selected.mania-full-keys-{candidate.ManiaKeys!.Value.ToString(CultureInfo.InvariantCulture)}",
                    BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck => $"selected.mania-deck-keys-{candidate.ManiaKeys!.Value.ToString(CultureInfo.InvariantCulture)}",
                    BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly => $"selected.mania-key-only-keys-{candidate.ManiaKeys!.Value.ToString(CultureInfo.InvariantCulture)}",
                    _ => throw new ArgumentOutOfRangeException(nameof(candidate), candidate.Source, "Unknown materializable BMS lane-resource candidate source."),
                };
            }
        }
    }
}
