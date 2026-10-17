// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinGaugeType
    {
        None = 0,
        AssistEasy = 1,
        Easy = 2,
        Normal = 3,
        Hard = 4,
        ExHard = 5,
        Hazard = 6,
    }

    public enum GameplaySkinGaugeClearState
    {
        Unavailable = 0,
        Uncleared = 1,
        Clear = 2,
    }

    /// <summary>
    /// Read-only gauge semantics resolved by the actual health processor. The default value explicitly means
    /// that the ruleset does not provide gauge type or clear-condition semantics.
    /// </summary>
    public readonly record struct GameplaySkinGaugeStateSnapshot
    {
        public GameplaySkinGaugeType Type { get; }

        public double Maximum { get; }

        public double ClearThreshold { get; }

        public bool IsClear { get; }

        public GameplaySkinGaugeClearState ClearState => Type == GameplaySkinGaugeType.None
            ? GameplaySkinGaugeClearState.Unavailable
            : IsClear ? GameplaySkinGaugeClearState.Clear : GameplaySkinGaugeClearState.Uncleared;

        internal GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType type, double maximum, double clearThreshold, bool isClear)
        {
            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(nameof(type), type, "Gauge type must be a known engine-owned value.");

            if (type == GameplaySkinGaugeType.None)
            {
                if (maximum != 0 || clearThreshold != 0 || isClear)
                    throw new ArgumentException("An unavailable gauge cannot carry clear-condition state.");
            }
            else
            {
                if (!double.IsFinite(maximum) || maximum <= 0 || maximum > 1)
                    throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Gauge maximum must be finite, positive and no greater than one.");

                if (!double.IsFinite(clearThreshold) || clearThreshold < 0 || clearThreshold > maximum)
                    throw new ArgumentOutOfRangeException(nameof(clearThreshold), clearThreshold, "Gauge clear threshold must be finite and within its gauge bounds.");
            }

            Type = type;
            Maximum = maximum;
            ClearThreshold = clearThreshold;
            IsClear = isClear;
        }
    }

    /// <summary>
    /// Engine-only projection of health-processor state. Author scenes receive the immutable snapshot through the
    /// existing score/gauge event family and cannot access or mutate the processor.
    /// </summary>
    internal interface IGameplaySkinGaugeStateSource
    {
        GameplaySkinGaugeStateSnapshot GameplaySkinGaugeState { get; }
    }
}
