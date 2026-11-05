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
        private readonly byte flags;
        private readonly float previewPhase;

        public bool IsAvailable => (flags & 1) != 0;
        public bool Adjusting => (flags & 2) != 0;
        public GameplaySkinHiSpeedMode Mode => (GameplaySkinHiSpeedMode)((flags >> 4) & 3);
        public int GreenNumber { get; }
        public float SuddenUnits { get; }
        public float HiddenUnits { get; }
        public bool SuddenActive => (flags & 4) != 0;
        public bool HiddenActive => (flags & 8) != 0;
        public float? ActualHiSpeed { get; }
        public int? GreenNumberMinimum { get; }
        public int? GreenNumberMaximum { get; }
        public bool GreenNumberRangeAvailable => GreenNumberMinimum.HasValue && GreenNumberMinimum != GreenNumberMaximum;
        public float? PreviewPhase => (flags & 64) != 0 ? previewPhase : null;

        public GameplaySkinAdjustmentState(bool isAvailable, bool adjusting, GameplaySkinHiSpeedMode mode, int greenNumber,
                                          float suddenUnits, float hiddenUnits, bool suddenActive, bool hiddenActive, float? actualHiSpeed = null,
                                          int? greenNumberMinimum = null, int? greenNumberMaximum = null, float? previewPhase = null)
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
            if (greenNumberMinimum.HasValue != greenNumberMaximum.HasValue
                || greenNumberMinimum < 0 || greenNumberMaximum < greenNumberMinimum)
                throw new ArgumentException("Green-number endpoints must be a complete ordered non-negative pair.");
            if (previewPhase.HasValue && (!float.IsFinite(previewPhase.Value) || previewPhase.Value < 0 || previewPhase.Value > 1))
                throw new ArgumentOutOfRangeException(nameof(previewPhase));
            if (!isAvailable && (adjusting || mode != GameplaySkinHiSpeedMode.Unspecified || greenNumber != 0
                                 || suddenUnits != 0 || hiddenUnits != 0 || suddenActive || hiddenActive || actualHiSpeed != null || greenNumberMinimum != null || previewPhase != null))
                throw new ArgumentException("An unavailable adjustment cannot carry live values.");
            // Keep the native timing sideband no larger than before the preview phase was added.
            flags = (byte)((isAvailable ? 1 : 0) | (adjusting ? 2 : 0) | (suddenActive ? 4 : 0) | (hiddenActive ? 8 : 0)
                           | ((int)mode << 4) | (previewPhase.HasValue ? 64 : 0));
            this.previewPhase = previewPhase ?? 0;
            GreenNumber = greenNumber;
            SuddenUnits = suddenUnits;
            HiddenUnits = hiddenUnits;
            ActualHiSpeed = actualHiSpeed;
            GreenNumberMinimum = greenNumberMinimum;
            GreenNumberMaximum = greenNumberMaximum;
        }
    }

    public interface IGameplaySkinAdjustmentStateSource
    {
        GameplaySkinAdjustmentState GameplaySkinAdjustmentState { get; }
    }
}
