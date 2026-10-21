// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaRuleset : IGameplaySkinArrivalVisibilityProjection
    {
        bool IGameplaySkinArrivalVisibilityProjection.IsArrivalVisible(GameplaySkinLaneId laneId, double hitTime, double gameplayTime)
        {
            if (!LayoutSnapshot.Context.Topology.TryGetLane(laneId, out GameplaySkinLaneTopologyEntry? target))
                throw new InvalidOperationException("An arrival must belong to the exact mania gameplay topology.");

            Column column = Playfield.GetColumn(target!.GlobalLogicalIndex);
            ScrollingHitObjectContainer scrolling = column.HitObjectContainer;
            if (!scrolling.IsLoaded || scrolling.DrawHeight <= 0)
                return false;

            float position = scrolling.PositionAtTime(hitTime, gameplayTime);
            return column.ScrollingInfo.Direction.Value == ScrollingDirection.Up
                ? position <= scrolling.DrawHeight
                : position >= -scrolling.DrawHeight;
        }

        private IEnumerable<GameplaySkinLaneArrival> createGameplaySkinArrivals(GameplaySkinLayoutPublication publication)
        {
            var topology = publication.Snapshot.Context.Topology;
            foreach (var group in Beatmap.HitObjects.Where(note => note is Note or HoldNote).GroupBy(note => note.Column))
            {
                GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[group.Key];
                yield return new GameplaySkinLaneArrival(lane.Identity.Group.Id, lane.Identity.Id, group.Min(note => note.StartTime));
            }
        }
    }
}
