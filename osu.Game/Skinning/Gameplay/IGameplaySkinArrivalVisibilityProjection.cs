// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Read-only engine projection of an arrival through its original scrolling container. A true result means
    /// the note origin has reached the incoming edge, including positions which have already passed the hit line.
    /// Author scenes receive the resulting presentation state, never this interface or control over scrolling.
    /// </summary>
    internal interface IGameplaySkinArrivalVisibilityProjection
    {
        bool IsArrivalVisible(GameplaySkinLaneId laneId, double hitTime, double gameplayTime);
    }
}
