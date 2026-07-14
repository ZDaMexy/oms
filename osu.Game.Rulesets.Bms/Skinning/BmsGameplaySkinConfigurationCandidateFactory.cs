// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
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
            ArgumentNullException.ThrowIfNull(bmsConfigurations);
            ArgumentNullException.ThrowIfNull(maniaConfigurations);

            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(layout);
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

            candidates.Add(new BmsGameplaySkinConfigurationCandidate(
                BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback,
                null,
                GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent));

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
                LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                    maniaConfigurations,
                    keys,
                    topology,
                    mapping));
        }
    }
}
