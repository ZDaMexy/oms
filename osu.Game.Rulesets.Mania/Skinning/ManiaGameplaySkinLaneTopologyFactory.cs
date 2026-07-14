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
    /// Projects the existing mania stage authority into the neutral gameplay skin topology contract.
    /// </summary>
    internal static class ManiaGameplaySkinLaneTopologyFactory
    {
        public static GameplaySkinLaneTopologySnapshot Create(ManiaBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            StageDefinition[] stages = beatmap.Stages?.ToArray()
                                       ?? throw new ArgumentException("The mania stage collection cannot be null.", nameof(beatmap));

            if (stages.Length is < 1 or > 2)
                throw new ArgumentException("Only supported single-stage or dual-stage mania topologies can be projected.", nameof(beatmap));

            if (stages.Any(stage => stage == null || stage.Columns > ManiaRuleset.MAX_STAGE_KEYS))
                throw new ArgumentException("Every mania stage must use the supported canonical key-count range.", nameof(beatmap));

            var groups = new List<GameplaySkinLaneTopologyGroup>(stages.Length);
            int globalIndex = 0;

            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                StageDefinition stage = stages[stageIndex];
                GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                    GameplaySkinLaneGroupId.Create($"mania.group.stage-{stageIndex + 1}"),
                    getSide(stages.Length, stageIndex));
                var lanes = new List<GameplaySkinLaneTopologyEntry>(stage.Columns);

                for (int localIndex = 0; localIndex < stage.Columns; localIndex++)
                {
                    GameplaySkinLaneRole role = stage.IsSpecialColumn(localIndex)
                        ? GameplaySkinLaneRole.SpecialKey
                        : GameplaySkinLaneRole.Key;
                    GameplaySkinLaneIdentity laneIdentity = GameplaySkinLaneIdentity.Create(
                        GameplaySkinLaneId.Create($"mania.lane.column-{globalIndex + 1}"), groupIdentity, role);
                    lanes.Add(GameplaySkinLaneTopologyEntry.Create(
                        laneIdentity,
                        globalIndex,
                        localIndex,
                        globalIndex,
                        localIndex));
                    globalIndex++;
                }

                groups.Add(GameplaySkinLaneTopologyGroup.Create(groupIdentity, stageIndex, stageIndex, lanes));
            }

            return GameplaySkinLaneTopologySnapshot.Create(groups);
        }

        private static GameplaySkinLaneSide getSide(int stageCount, int stageIndex)
        {
            if (stageCount == 1)
                return GameplaySkinLaneSide.Neutral;

            return stageIndex switch
            {
                0 => GameplaySkinLaneSide.Primary,
                1 => GameplaySkinLaneSide.Secondary,
                _ => throw new ArgumentOutOfRangeException(nameof(stageIndex)),
            };
        }
    }
}
