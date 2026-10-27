// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public partial class GameplaySkinEventRuntimeHost
    {
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinJudgementTimingStateSnapshot> judgementTimingGroups = [];

        /// <summary>Current OMS display qualification, kept separate from the actual grade and offset.</summary>
        internal static GameplaySkinJudgementTimingDirection QualifyJudgementTiming(bool hasPlayerTiming, bool automated,
                                                                                  GameplaySkinJudgementGrade grade, double offset)
            => hasPlayerTiming && !automated
                               && grade is GameplaySkinJudgementGrade.Great or GameplaySkinJudgementGrade.Good
                                   or GameplaySkinJudgementGrade.Ok or GameplaySkinJudgementGrade.Meh
                               && Math.Abs(offset) >= 1e-6
                ? offset < 0 ? GameplaySkinJudgementTimingDirection.Fast : GameplaySkinJudgementTimingDirection.Slow
                : GameplaySkinJudgementTimingDirection.None;

        internal static GameplaySkinJudgementTimingStateSnapshot FoldJudgementTiming(GameplaySkinJudgementTimingStateSnapshot previous,
                                                                                   GameplaySkinLaneRole role, bool hasPlayerTiming,
                                                                                   bool automated, GameplaySkinJudgementGrade grade,
                                                                                   double offset, double appliedTime,
                                                                                   GameplaySkinJudgementTimingDirection timingFeedback = GameplaySkinJudgementTimingDirection.None)
        {
            // Rulesets may qualify display-only feedback without claiming that an automatic result measured input.
            GameplaySkinJudgementTimingDirection direction = automated ? GameplaySkinJudgementTimingDirection.None
                : timingFeedback != GameplaySkinJudgementTimingDirection.None ? timingFeedback
                : QualifyJudgementTiming(hasPlayerTiming, false, grade, offset);
            return direction != GameplaySkinJudgementTimingDirection.None
                   && role is GameplaySkinLaneRole.Key or GameplaySkinLaneRole.SpecialKey or GameplaySkinLaneRole.Scratch
                ? previous.Apply(role, new GameplaySkinJudgementTimingHint(direction, appliedTime))
                : previous;
        }

        private GameplaySkinJudgementTimingStateSnapshot foldJudgementTiming(GameplaySkinLaneGroupId? groupId, GameplaySkinLaneId? laneId,
                                                                            GameplaySkinJudgementGrade grade, double offset,
                                                                            bool hasPlayerTiming, double appliedTime,
                                                                            GameplaySkinJudgementTimingDirection timingFeedback)
        {
            if (groupId == null)
                return default;

            GameplaySkinJudgementTimingStateSnapshot state = judgementTimingGroups[groupId];
            if (laneId != null)
            {
                GameplaySkinLaneRole role = Publication.Snapshot.GetLane(laneId).TopologyEntry.Identity.Role;
                state = FoldJudgementTiming(state, role, hasPlayerTiming, automatedTiming, grade, offset, appliedTime, timingFeedback);
            }
            judgementTimingGroups[groupId] = state;
            return state;
        }

        private void restoreJudgementTimingGroups(IEnumerable<GameplaySkinJudgementTimingGroupStateSnapshot> entries)
        {
            foreach (GameplaySkinLayoutGroup group in Publication.Snapshot.GroupsInLogicalOrder)
                judgementTimingGroups.Add(group.GroupId, default);
            foreach (GameplaySkinJudgementTimingGroupStateSnapshot entry in entries)
            {
                if (!judgementTimingGroups.ContainsKey(entry.GroupId))
                    throw new ArgumentException("A timing snapshot group is not part of the exact layout publication.", nameof(entries));
                judgementTimingGroups[entry.GroupId] = entry.State;
            }
        }

        private void clearJudgementTimingGroups()
        {
            foreach (GameplaySkinLayoutGroup group in Publication.Snapshot.GroupsInLogicalOrder)
                judgementTimingGroups[group.GroupId] = default;
        }

        private IEnumerable<GameplaySkinJudgementTimingGroupStateSnapshot> currentJudgementTimingGroups()
            => judgementTimingGroups.Select(entry => new GameplaySkinJudgementTimingGroupStateSnapshot(entry.Key, entry.Value));
    }
}
