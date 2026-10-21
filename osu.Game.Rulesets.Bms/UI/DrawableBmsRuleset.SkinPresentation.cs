// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsRuleset : IGameplaySkinArrivalVisibilityProjection
    {
        bool IGameplaySkinArrivalVisibilityProjection.IsArrivalVisible(GameplaySkinLaneId laneId, double hitTime, double gameplayTime)
        {
            if (!LayoutSnapshot.Neutral.Context.Topology.TryGetLane(laneId, out GameplaySkinLaneTopologyEntry? target))
                throw new InvalidOperationException("An arrival must belong to the exact BMS gameplay topology.");

            BmsLane lane = Playfield.Lanes[target!.GlobalLogicalIndex];
            ScrollingHitObjectContainer scrolling = lane.HitObjectContainer;
            if (!scrolling.IsLoaded || scrolling.DrawHeight <= 0)
                return false;

            float position = scrolling.PositionAtTime(hitTime, gameplayTime);
            return lane.ScrollingInfo.Direction.Value == ScrollingDirection.Up
                ? position <= scrolling.DrawHeight
                : position >= -scrolling.DrawHeight;
        }

        private IEnumerable<GameplaySkinLaneArrival> createGameplaySkinArrivals(GameplaySkinLayoutPublication publication)
        {
            var topology = publication.Snapshot.Context.Topology;
            foreach (var group in Beatmap.HitObjects.OfType<BmsHitObject>().Where(note => !note.AutoPlay).GroupBy(note => note.LaneIndex))
            {
                GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[group.Key];
                yield return new GameplaySkinLaneArrival(lane.Identity.Group.Id, lane.Identity.Id, group.Min(note => note.StartTime));
            }
        }

        private void updateGameplaySkinMotion()
        {
            if (gameplaySkinInputManager == null || GameplaySkinEventRuntime == null)
                return;
            var motion = gameplaySkinInputManager.GetScratchPresentationMotion();
            bool available = gameplaySkinInputManager.ScratchPresentationMotionAvailable;
            var groups = LayoutSnapshot.Context.Topology.GroupsInLogicalOrder;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                for (int laneIndex = 0; laneIndex < group.LanesInLogicalOrder.Count; laneIndex++)
                {
                    GameplaySkinLaneTopologyEntry lane = group.LanesInLogicalOrder[laneIndex];
                    if (lane.Identity.Role != GameplaySkinLaneRole.Scratch)
                        continue;
                    GameplaySkinEventRuntime.PublishInputMotion(group.Identity.Id, lane.Identity.Id,
                        groupIndex == 0 ? motion.Counts1 : motion.Counts2,
                        groupIndex == 0 ? motion.Revolutions1 : motion.Revolutions2, available);
                }
            }
        }
    }
}
