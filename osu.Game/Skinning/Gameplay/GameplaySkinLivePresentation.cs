// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Read-only progress of the player's preparation stage, independent of music and judgement time.</summary>
    public readonly record struct GameplaySkinPreparationStateSnapshot
    {
        public bool Available { get; }
        public bool Active { get; }
        public double Elapsed { get; }
        public double Remaining { get; }
        public bool Ready { get; }
        public double ReadyElapsed { get; }

        internal GameplaySkinPreparationStateSnapshot(bool available, bool active, double elapsed, double remaining, bool ready, double readyElapsed)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (!double.IsFinite(remaining) || remaining < 0)
                throw new ArgumentOutOfRangeException(nameof(remaining));
            if (!double.IsFinite(readyElapsed) || readyElapsed < 0 || readyElapsed > 1500)
                throw new ArgumentOutOfRangeException(nameof(readyElapsed));
            if (!available && (active || elapsed != 0 || remaining != 0 || ready || readyElapsed != 0))
                throw new ArgumentException("Unavailable preparation cannot retain progress or claim an active stage.");
            if (!active && (remaining != 0 || ready || readyElapsed != 0))
                throw new ArgumentException("Completed preparation cannot retain a countdown or GET READY animation.");
            if (!ready && readyElapsed != 0)
                throw new ArgumentException("An inactive GET READY animation has no elapsed time.");
            Available = available;
            Active = active;
            Elapsed = elapsed;
            Remaining = remaining;
            Ready = ready;
            ReadyElapsed = readyElapsed;
        }
    }

    /// <summary>Read-only display of an offset which the ruleset has actually applied.</summary>
    public readonly record struct GameplaySkinOffsetStateSnapshot
    {
        public bool IsAvailable { get; }
        public double Milliseconds { get; }
        public bool Automatic { get; }
        public bool AutomaticAvailable { get; }

        internal GameplaySkinOffsetStateSnapshot(bool isAvailable, double milliseconds, bool automatic, bool automaticAvailable)
        {
            if (!double.IsFinite(milliseconds))
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (!isAvailable && (milliseconds != 0 || automatic))
                throw new ArgumentException("An unavailable offset cannot claim an applied value or automatic adjustment.");
            if (automatic && !automaticAvailable || !isAvailable && automaticAvailable)
                throw new ArgumentException("Automatic offset mode must be explicitly known before it can be published.");
            IsAvailable = isAvailable;
            Milliseconds = milliseconds;
            Automatic = automatic;
            AutomaticAvailable = automaticAvailable;
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
        public bool ArrivalCueAvailable { get; }

        internal GameplaySkinChartPresentation(IEnumerable<GameplaySkinLaneArrival> arrivals, bool usesExScore = false, long? personalBestScore = null,
                                               bool arrivalCueAvailable = false)
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
            ArrivalCueAvailable = arrivalCueAvailable && copy.Length != 0;
        }

        internal double? ArrivalFor(GameplaySkinLaneId? laneId, GameplaySkinLaneGroupId? groupId)
            => laneId != null ? lanes.TryGetValue(laneId, out double lane) ? lane : null
                : groupId != null ? groups.TryGetValue(groupId, out double group) ? group : null : firstTime;

        internal bool IsFirstRow(GameplaySkinLaneId? laneId, GameplaySkinLaneGroupId? groupId)
            => firstTime is double first && ArrivalFor(laneId, groupId) is double arrival && Math.Abs(arrival - first) <= 0.000001;

        internal static long RankThreshold(long maximum, int ninths)
            => maximum / 9 * ninths + (maximum % 9 * ninths + 8) / 9;
    }
}
