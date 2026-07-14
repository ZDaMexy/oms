// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// BMS-owned source context paired with its ruleset-neutral lane topology projection.
    /// </summary>
    /// <remarks>
    /// This internal result is not a shared layout context or serialisation ABI. Native action and geometry remain in BMS.
    /// </remarks>
    internal sealed class BmsGameplaySkinLaneTopologyProjection
    {
        public BmsKeymode Keymode { get; }

        public BmsPlayfieldStyle AppliedStyle { get; }

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        internal BmsGameplaySkinLaneTopologyProjection(
            BmsKeymode keymode,
            BmsPlayfieldStyle appliedStyle,
            GameplaySkinLaneTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(topology);

            Keymode = keymode;
            AppliedStyle = appliedStyle;
            Topology = topology;
        }
    }

    /// <summary>
    /// Projects the existing BMS lane-layout authority into the neutral gameplay skin topology contract.
    /// </summary>
    internal static class BmsGameplaySkinLaneTopologyFactory
    {
        public static BmsGameplaySkinLaneTopologyProjection Create(BmsLaneLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            validateContext(layout);

            IGrouping<int, BmsLaneLayout.Lane>[] sourceGroups = layout.Lanes
                .GroupBy(lane => getGroupLogicalIndex(layout.Keymode, lane.Action))
                .OrderBy(group => group.Key)
                .ToArray();
            Dictionary<int, int> groupVisualIndices = sourceGroups
                .OrderBy(group => group.Min(lane => lane.VisualIndex))
                .Select((group, visualIndex) => (group.Key, visualIndex))
                .ToDictionary(pair => pair.Key, pair => pair.visualIndex);
            var groups = new List<GameplaySkinLaneTopologyGroup>(sourceGroups.Length);

            foreach (IGrouping<int, BmsLaneLayout.Lane> sourceGroup in sourceGroups)
            {
                int groupLogicalIndex = sourceGroup.Key;
                GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                    GameplaySkinLaneGroupId.Create($"bms.group.deck-{groupLogicalIndex + 1}"),
                    getSide(layout.Keymode, layout.Style, groupLogicalIndex));
                Dictionary<int, int> groupLogicalIndices = sourceGroup
                    .OrderBy(lane => lane.LaneIndex)
                    .Select((lane, index) => (lane.LaneIndex, index))
                    .ToDictionary(pair => pair.LaneIndex, pair => pair.index);
                Dictionary<int, int> groupLocalVisualIndices = sourceGroup
                    .OrderBy(lane => lane.VisualIndex)
                    .Select((lane, index) => (lane.LaneIndex, index))
                    .ToDictionary(pair => pair.LaneIndex, pair => pair.index);
                var lanes = new List<GameplaySkinLaneTopologyEntry>();

                foreach (BmsLaneLayout.Lane sourceLane in sourceGroup)
                {
                    GameplaySkinLaneRole role = getRole(sourceLane.Action);

                    if (sourceLane.IsScratch != (role == GameplaySkinLaneRole.Scratch))
                        throw new ArgumentException("BMS lane scratch metadata does not agree with its action.", nameof(layout));

                    GameplaySkinLaneIdentity laneIdentity = GameplaySkinLaneIdentity.Create(
                        GameplaySkinLaneId.Create(getLaneId(sourceLane.Action)), groupIdentity, role);
                    lanes.Add(GameplaySkinLaneTopologyEntry.Create(
                        laneIdentity,
                        sourceLane.LaneIndex,
                        groupLogicalIndices[sourceLane.LaneIndex],
                        sourceLane.VisualIndex,
                        groupLocalVisualIndices[sourceLane.LaneIndex]));
                }

                groups.Add(GameplaySkinLaneTopologyGroup.Create(
                    groupIdentity,
                    groupLogicalIndex,
                    groupVisualIndices[groupLogicalIndex],
                    lanes));
            }

            return new BmsGameplaySkinLaneTopologyProjection(
                layout.Keymode,
                layout.Style,
                GameplaySkinLaneTopologySnapshot.Create(groups));
        }

        private static void validateContext(BmsLaneLayout layout)
        {
            if (layout.Keymode is not BmsKeymode.Key5K
                and not BmsKeymode.Key7K
                and not BmsKeymode.Key9K_Bms
                and not BmsKeymode.Key9K_Pms
                and not BmsKeymode.Key14K)
            {
                throw new ArgumentException("Unsupported BMS keymode for gameplay skin topology projection.", nameof(layout));
            }

            if (layout.Style is not BmsPlayfieldStyle.P1
                and not BmsPlayfieldStyle.P2
                and not BmsPlayfieldStyle.Center
                and not BmsPlayfieldStyle.CenterRightScratch)
            {
                throw new ArgumentException("Unsupported BMS playfield style for gameplay skin topology projection.", nameof(layout));
            }

            if (layout.Lanes.Count != BmsRuleset.GetLaneCount(layout.Keymode))
                throw new ArgumentException("Only canonical BMS lane counts can be projected into a stable gameplay skin topology.", nameof(layout));

            if (layout.Keymode is not BmsKeymode.Key5K and not BmsKeymode.Key7K && layout.Style != BmsPlayfieldStyle.Center)
                throw new ArgumentException("The resolved BMS playfield style is invalid for this keymode.", nameof(layout));

            BmsAction[] expectedActions = getCanonicalActions(layout.Keymode);

            for (int i = 0; i < expectedActions.Length; i++)
            {
                BmsLaneLayout.Lane lane = layout.Lanes[i];
                BmsAction expectedAction = expectedActions[i];
                bool expectedScratch = expectedAction is BmsAction.Scratch1 or BmsAction.Scratch2;

                if (lane.LaneIndex != i || lane.Action != expectedAction || lane.IsScratch != expectedScratch)
                    throw new ArgumentException("BMS lane composition does not match the canonical topology for its keymode.", nameof(layout));
            }
        }

        private static BmsAction[] getCanonicalActions(BmsKeymode keymode)
        {
            return keymode switch
            {
                BmsKeymode.Key5K => new[]
                {
                    BmsAction.Scratch1,
                    BmsAction.Key1,
                    BmsAction.Key2,
                    BmsAction.Key3,
                    BmsAction.Key4,
                    BmsAction.Key5,
                },
                BmsKeymode.Key7K => new[]
                {
                    BmsAction.Scratch1,
                    BmsAction.Key1,
                    BmsAction.Key2,
                    BmsAction.Key3,
                    BmsAction.Key4,
                    BmsAction.Key5,
                    BmsAction.Key6,
                    BmsAction.Key7,
                },
                BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms => new[]
                {
                    BmsAction.Key1,
                    BmsAction.Key2,
                    BmsAction.Key3,
                    BmsAction.Key4,
                    BmsAction.Key5,
                    BmsAction.Key6,
                    BmsAction.Key7,
                    BmsAction.Key8,
                    BmsAction.Key9,
                },
                BmsKeymode.Key14K => new[]
                {
                    BmsAction.Scratch1,
                    BmsAction.Key1,
                    BmsAction.Key2,
                    BmsAction.Key3,
                    BmsAction.Key4,
                    BmsAction.Key5,
                    BmsAction.Key6,
                    BmsAction.Key7,
                    BmsAction.Key8,
                    BmsAction.Key9,
                    BmsAction.Key10,
                    BmsAction.Key11,
                    BmsAction.Key12,
                    BmsAction.Key13,
                    BmsAction.Key14,
                    BmsAction.Scratch2,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(keymode), keymode, "Unsupported BMS keymode."),
            };
        }

        private static int getGroupLogicalIndex(BmsKeymode keymode, BmsAction action)
        {
            if (keymode != BmsKeymode.Key14K)
                return 0;

            return action switch
            {
                BmsAction.Scratch1 or
                    BmsAction.Key1 or BmsAction.Key2 or BmsAction.Key3 or BmsAction.Key4 or
                    BmsAction.Key5 or BmsAction.Key6 or BmsAction.Key7 => 0,
                BmsAction.Key8 or BmsAction.Key9 or BmsAction.Key10 or BmsAction.Key11 or
                    BmsAction.Key12 or BmsAction.Key13 or BmsAction.Key14 or BmsAction.Scratch2 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported BMS lane action."),
            };
        }

        private static GameplaySkinLaneSide getSide(BmsKeymode keymode, BmsPlayfieldStyle style, int groupLogicalIndex)
        {
            return keymode switch
            {
                BmsKeymode.Key5K or BmsKeymode.Key7K => style switch
                {
                    BmsPlayfieldStyle.P1 or BmsPlayfieldStyle.Center => GameplaySkinLaneSide.Primary,
                    BmsPlayfieldStyle.P2 or BmsPlayfieldStyle.CenterRightScratch => GameplaySkinLaneSide.Secondary,
                    _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported single-play BMS style."),
                },
                BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms => GameplaySkinLaneSide.Neutral,
                BmsKeymode.Key14K => groupLogicalIndex switch
                {
                    0 => GameplaySkinLaneSide.Primary,
                    1 => GameplaySkinLaneSide.Secondary,
                    _ => throw new ArgumentOutOfRangeException(nameof(groupLogicalIndex)),
                },
                _ => throw new ArgumentOutOfRangeException(nameof(keymode), keymode, "Unsupported BMS keymode."),
            };
        }

        private static GameplaySkinLaneRole getRole(BmsAction action)
        {
            return action switch
            {
                BmsAction.Scratch1 or BmsAction.Scratch2 => GameplaySkinLaneRole.Scratch,
                BmsAction.Key1 or BmsAction.Key2 or BmsAction.Key3 or BmsAction.Key4 or BmsAction.Key5 or BmsAction.Key6 or BmsAction.Key7 or
                    BmsAction.Key8 or BmsAction.Key9 or BmsAction.Key10 or BmsAction.Key11 or BmsAction.Key12 or BmsAction.Key13 or BmsAction.Key14
                    => GameplaySkinLaneRole.Key,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported BMS lane action."),
            };
        }

        private static string getLaneId(BmsAction action)
        {
            return action switch
            {
                BmsAction.Scratch1 => "bms.lane.scratch-1",
                BmsAction.Key1 => "bms.lane.key-1",
                BmsAction.Key2 => "bms.lane.key-2",
                BmsAction.Key3 => "bms.lane.key-3",
                BmsAction.Key4 => "bms.lane.key-4",
                BmsAction.Key5 => "bms.lane.key-5",
                BmsAction.Key6 => "bms.lane.key-6",
                BmsAction.Key7 => "bms.lane.key-7",
                BmsAction.Scratch2 => "bms.lane.scratch-2",
                BmsAction.Key8 => "bms.lane.key-8",
                BmsAction.Key9 => "bms.lane.key-9",
                BmsAction.Key10 => "bms.lane.key-10",
                BmsAction.Key11 => "bms.lane.key-11",
                BmsAction.Key12 => "bms.lane.key-12",
                BmsAction.Key13 => "bms.lane.key-13",
                BmsAction.Key14 => "bms.lane.key-14",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported BMS lane action."),
            };
        }
    }
}
