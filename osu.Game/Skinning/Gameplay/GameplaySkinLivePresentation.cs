// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Read-only display of an offset which the ruleset has actually applied.</summary>
    public readonly record struct GameplaySkinOffsetStateSnapshot
    {
        public bool IsAvailable { get; }
        public double Milliseconds { get; }
        public bool Automatic { get; }

        internal GameplaySkinOffsetStateSnapshot(bool isAvailable, double milliseconds, bool automatic)
        {
            if (!double.IsFinite(milliseconds))
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (!isAvailable && (milliseconds != 0 || automatic))
                throw new ArgumentException("An unavailable offset cannot claim an applied value or automatic adjustment.");
            IsAvailable = isAvailable;
            Milliseconds = milliseconds;
            Automatic = automatic;
        }
    }

    /// <summary>The first playable note in one real, post-mod lane. Empty authored slots have no entry.</summary>
    public readonly record struct GameplaySkinLaneArrival
    {
        public GameplaySkinLaneGroupId GroupId { get; }
        public GameplaySkinLaneId LaneId { get; }
        public double Time { get; }

        internal GameplaySkinLaneArrival(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId, double time)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            ArgumentNullException.ThrowIfNull(laneId);
            if (!double.IsFinite(time))
                throw new ArgumentOutOfRangeException(nameof(time));
            GroupId = groupId;
            LaneId = laneId;
            Time = time;
        }
    }

    /// <summary>Bounded immutable chart information, never a beatmap, score record or storage capability.</summary>
    public sealed class GameplaySkinChartPresentation
    {
        private readonly Dictionary<GameplaySkinLaneId, double> lanes;
        private readonly Dictionary<GameplaySkinLaneGroupId, double> groups;
        private readonly double? firstTime;

        public IReadOnlyList<GameplaySkinLaneArrival> Arrivals { get; }
        public long? PersonalBestScore { get; }
        public bool UsesExScore { get; }

        internal GameplaySkinChartPresentation(IEnumerable<GameplaySkinLaneArrival> arrivals, bool usesExScore = false, long? personalBestScore = null)
        {
            GameplaySkinLaneArrival[] copy = arrivals.ToArray();
            if (copy.Length > GameplaySkinEventBudgets.MAX_INPUT_STATES || copy.Select(arrival => arrival.LaneId).Distinct().Count() != copy.Length)
                throw new ArgumentException("Arrival metadata must contain at most one entry per real gameplay lane.", nameof(arrivals));
            if (personalBestScore < 0)
                throw new ArgumentOutOfRangeException(nameof(personalBestScore));
            Arrivals = Array.AsReadOnly(copy);
            lanes = copy.ToDictionary(arrival => arrival.LaneId, arrival => arrival.Time);
            groups = copy.GroupBy(arrival => arrival.GroupId).ToDictionary(group => group.Key, group => group.Min(arrival => arrival.Time));
            firstTime = copy.Length == 0 ? null : copy.Min(arrival => arrival.Time);
            UsesExScore = usesExScore;
            PersonalBestScore = personalBestScore;
        }

        internal double? ArrivalFor(GameplaySkinLaneId? laneId, GameplaySkinLaneGroupId? groupId)
            => laneId != null ? lanes.TryGetValue(laneId, out double lane) ? lane : null
                : groupId != null ? groups.TryGetValue(groupId, out double group) ? group : null : firstTime;

        internal static long RankThreshold(long maximum, int ninths)
            => maximum / 9 * ninths + (maximum % 9 * ninths + 8) / 9;
    }
}
