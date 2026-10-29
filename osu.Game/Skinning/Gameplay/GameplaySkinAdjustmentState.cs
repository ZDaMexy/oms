// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinHiSpeedMode
    {
        Unspecified = 0,
        Normal = 1,
        Floating = 2,
        Classic = 3,
    }

    /// <summary>Read-only native adjustment values. Inactive covers retain their configured units.</summary>
    public readonly record struct GameplaySkinAdjustmentState
    {
        public bool IsAvailable { get; }
        public bool Adjusting { get; }
        public GameplaySkinHiSpeedMode Mode { get; }
        public int GreenNumber { get; }
        public float SuddenUnits { get; }
        public float HiddenUnits { get; }
        public bool SuddenActive { get; }
        public bool HiddenActive { get; }
        public float? ActualHiSpeed { get; }

        public GameplaySkinAdjustmentState(bool isAvailable, bool adjusting, GameplaySkinHiSpeedMode mode, int greenNumber,
                                          float suddenUnits, float hiddenUnits, bool suddenActive, bool hiddenActive, float? actualHiSpeed = null)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            ArgumentOutOfRangeException.ThrowIfNegative(greenNumber);
            if (!float.IsFinite(suddenUnits) || suddenUnits < 0 || suddenUnits > 1000)
                throw new ArgumentOutOfRangeException(nameof(suddenUnits));
            if (!float.IsFinite(hiddenUnits) || hiddenUnits < 0 || hiddenUnits > 1000)
                throw new ArgumentOutOfRangeException(nameof(hiddenUnits));
            if (isAvailable && mode == GameplaySkinHiSpeedMode.Unspecified)
                throw new ArgumentException("An available adjustment requires its actual speed mode.");
            if (actualHiSpeed is float speed && (!float.IsFinite(speed) || speed <= 0))
                throw new ArgumentOutOfRangeException(nameof(actualHiSpeed));
            if (!isAvailable && (adjusting || mode != GameplaySkinHiSpeedMode.Unspecified || greenNumber != 0
                                 || suddenUnits != 0 || hiddenUnits != 0 || suddenActive || hiddenActive || actualHiSpeed != null))
                throw new ArgumentException("An unavailable adjustment cannot carry live values.");
            IsAvailable = isAvailable;
            Adjusting = adjusting;
            Mode = mode;
            GreenNumber = greenNumber;
            SuddenUnits = suddenUnits;
            HiddenUnits = hiddenUnits;
            SuddenActive = suddenActive;
            HiddenActive = hiddenActive;
            ActualHiSpeed = actualHiSpeed;
        }
    }

    public interface IGameplaySkinAdjustmentStateSource
    {
        GameplaySkinAdjustmentState GameplaySkinAdjustmentState { get; }
    }
}
