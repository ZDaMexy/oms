// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Builds the field-preserving BMS-to-mania compatibility candidate chain from real decoder outputs.
    /// </summary>
    internal static class BmsGameplaySkinConfigurationCandidateFactory
    {
        public static BmsGameplaySkinConfigurationCandidatePlan Create(
            BmsLaneLayout layout,
            IReadOnlyList<BmsSkinConfiguration> bmsConfigurations,
            IReadOnlyList<LegacyManiaSkinConfiguration> maniaConfigurations)
        {
            ArgumentNullException.ThrowIfNull(layout);
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(layout);

            return create(projection, bmsConfigurations, maniaConfigurations);
        }

        /// <summary>
        /// Builds a production compatibility plan over the exact topology reference retained by the committed C3 layout
        /// publication. This entry point must never reconstruct an equivalent topology from keymode or presentation style.
        /// </summary>
        public static BmsGameplaySkinConfigurationCandidatePlan CreateExact(
            BmsGameplayLayoutSnapshot layout,
            IReadOnlyList<BmsSkinConfiguration> bmsConfigurations,
            IReadOnlyList<LegacyManiaSkinConfiguration> maniaConfigurations)
        {
            ArgumentNullException.ThrowIfNull(layout);

            GameplaySkinLaneTopologySnapshot topology = layout.Neutral.Context.Topology;

            if (layout.LanesInLogicalOrder.Count != topology.LanesInLogicalOrder.Count)
                throw new ArgumentException("The exact BMS layout and topology lane counts differ.", nameof(layout));

            for (int logicalIndex = 0; logicalIndex < layout.LanesInLogicalOrder.Count; logicalIndex++)
            {
                BmsGameplayLayoutLane lane = layout.LanesInLogicalOrder[logicalIndex];
                GameplaySkinLaneTopologyEntry topologyLane = topology.LanesInLogicalOrder[logicalIndex];

                if (!ReferenceEquals(lane.NeutralLane.TopologyEntry, topologyLane)
                    || lane.LogicalIndex != topologyLane.GlobalLogicalIndex
                    || lane.VisualIndex != topologyLane.GlobalVisualIndex
                    || lane.GroupLocalLogicalIndex != topologyLane.GroupLocalLogicalIndex
                    || lane.GroupLocalVisualIndex != topologyLane.GroupLocalVisualIndex
                    || !lane.LaneId.Equals(topologyLane.Identity.Id))
                {
                    throw new ArgumentException("The BMS compatibility plan must retain every exact C3 lane identity and explicit index.", nameof(layout));
                }
            }

            var projection = new BmsGameplaySkinLaneTopologyProjection(layout.Keymode, layout.Style, topology);
            return create(projection, bmsConfigurations, maniaConfigurations);
        }

        private static BmsGameplaySkinConfigurationCandidatePlan create(
            BmsGameplaySkinLaneTopologyProjection projection,
            IReadOnlyList<BmsSkinConfiguration> bmsConfigurations,
            IReadOnlyList<LegacyManiaSkinConfiguration> maniaConfigurations)
        {
            ArgumentNullException.ThrowIfNull(projection);
            ArgumentNullException.ThrowIfNull(bmsConfigurations);
            ArgumentNullException.ThrowIfNull(maniaConfigurations);

            var candidates = new List<BmsGameplaySkinConfigurationCandidate>
            {
                new BmsGameplaySkinConfigurationCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                    null,
                    BmsGameplaySkinLaneResourceSnapshotFactory.Create(bmsConfigurations, projection)),
            };

            int fullVisualKeys = projection.Topology.LanesInVisualOrder.Count;
            IReadOnlyDictionary<GameplaySkinLaneId, int> fullVisualMapping =
                BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateFullVisual(projection);

            candidates.Add(createManiaCandidate(
                BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                fullVisualKeys,
                projection.Topology,
                fullVisualMapping,
                maniaConfigurations));

            if (projection.Keymode == BmsKeymode.Key14K)
            {
                IReadOnlyDictionary<GameplaySkinLaneId, int> deckMapping =
                    BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateEightColumnDeck(projection);

                // One real Keys:8 bucket is projected independently over both engine-owned decks. The legacy decoder
                // does not preserve a second duplicate Keys:8 section. Deck mapping precedes the 14-key fallback because
                // it preserves scratch and deck-local presentation; Keys:14 remains available for ordinary lanes only.
                candidates.Add(createManiaCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck,
                    8,
                    projection.Topology,
                    deckMapping,
                    maniaConfigurations));
            }

            if (projection.Keymode is BmsKeymode.Key5K or BmsKeymode.Key7K or BmsKeymode.Key14K)
            {
                IReadOnlyDictionary<GameplaySkinLaneId, int> keyOnlyMapping =
                    BmsGameplaySkinLegacyManiaLaneMappingFactory.CreateKeyOnly(projection);
                int keyOnlyKeys = keyOnlyMapping.Count;

                candidates.Add(createManiaCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    keyOnlyKeys,
                    projection.Topology,
                    keyOnlyMapping,
                    maniaConfigurations));
            }

            return new BmsGameplaySkinConfigurationCandidatePlan(
                projection.Keymode,
                projection.AppliedStyle,
                projection.Topology,
                candidates.ToArray());
        }

        private static BmsGameplaySkinConfigurationCandidate createManiaCandidate(
            BmsGameplaySkinConfigurationCandidateSource source,
            int keys,
            GameplaySkinLaneTopologySnapshot topology,
            IReadOnlyDictionary<GameplaySkinLaneId, int> mapping,
            IReadOnlyList<LegacyManiaSkinConfiguration> maniaConfigurations)
        {
            return new BmsGameplaySkinConfigurationCandidate(
                source,
                keys,
                retainBmsHostedFields(LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                    maniaConfigurations,
                    keys,
                    topology,
                    mapping)));
        }

        private static GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> retainBmsHostedFields(
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> declaration)
        {
            if (!declaration.IsDeclared)
                return declaration;

            GameplaySkinLaneResourceSnapshot snapshot = declaration.Value;
            return GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Declared(
                GameplaySkinLaneResourceSnapshot.Create(
                    snapshot.Topology,
                    snapshot.Declarations.Where(candidate => BmsGameplaySkinNoteResourceFields.Contains(candidate.Field))));
        }
    }
}
