// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Read-only projection of a native playfield's already-applied lift adjustment.</summary>
    public readonly record struct GameplaySkinLiftState
    {
        public bool IsAvailable { get; }
        public double Units { get; }
        public double OffsetRatio { get; }
        public bool Reversed { get; }

        public GameplaySkinLiftState(bool isAvailable, double units, double offsetRatio, bool reversed)
        {
            if (!double.IsFinite(units) || units < 0 || units > 1000)
                throw new ArgumentOutOfRangeException(nameof(units));
            if (!double.IsFinite(offsetRatio) || offsetRatio < 0 || offsetRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(offsetRatio));
            if (!isAvailable && (units != 0 || offsetRatio != 0 || reversed))
                throw new ArgumentException("An unavailable playfield adjustment cannot carry live values.");
            IsAvailable = isAvailable;
            Units = units;
            OffsetRatio = offsetRatio;
            Reversed = reversed;
        }
    }

    public interface IGameplaySkinLiftStateSource
    {
        GameplaySkinLiftState GameplaySkinLiftState { get; }
    }
}
