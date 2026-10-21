// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaRuleset
    {
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
