// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Bms.Configuration
{
    /// <summary>
    /// A detached reading configuration for one play, including the state saved with a replay.
    /// </summary>
    public readonly record struct BmsReadingSettingsSnapshot
    {
        [JsonProperty("hi_speed_mode")]
        public BmsHiSpeedMode HiSpeedMode { get; init; }

        [JsonProperty("normal_hi_speed")]
        public double NormalHiSpeed { get; init; }

        // The preset resolves to a multiplier when selected. BPM/cover changes do not reselect it.
        // This is per-play replay state; the persistent preset remains chart-independent.
        [JsonProperty("normal_hi_speed_multiplier", NullValueHandling = NullValueHandling.Ignore)]
        public double? NormalHiSpeedMultiplier { get; init; }

        [JsonProperty("floating_hi_speed")]
        public double FloatingHiSpeed { get; init; }

        [JsonProperty("classic_hi_speed")]
        public double ClassicHiSpeed { get; init; }

        [JsonProperty("sudden_enabled")]
        public bool SuddenEnabled { get; init; }

        [JsonProperty("hidden_enabled")]
        public bool HiddenEnabled { get; init; }

        [JsonProperty("lift_enabled")]
        public bool LiftEnabled { get; init; }

        [JsonProperty("lane_covers_enabled")]
        public bool LaneCoversEnabled { get; init; }

        [JsonProperty("sudden_units")]
        public float SuddenUnits { get; init; }

        [JsonProperty("hidden_units")]
        public float HiddenUnits { get; init; }

        [JsonProperty("lift_units")]
        public float LiftUnits { get; init; }

        [JsonProperty("sudden_opacity")]
        public float SuddenOpacity { get; init; }

        [JsonProperty("hidden_opacity")]
        public float HiddenOpacity { get; init; }

        [JsonConstructor]
        public BmsReadingSettingsSnapshot([JsonProperty("hi_speed_mode")] BmsHiSpeedMode hiSpeedMode,
                                          [JsonProperty("normal_hi_speed")] double normalHiSpeed,
                                          [JsonProperty("floating_hi_speed")] double floatingHiSpeed,
                                          [JsonProperty("classic_hi_speed")] double classicHiSpeed,
                                          [JsonProperty("sudden_enabled")] bool suddenEnabled,
                                          [JsonProperty("hidden_enabled")] bool hiddenEnabled,
                                          [JsonProperty("lift_enabled")] bool liftEnabled,
                                          [JsonProperty("lane_covers_enabled")] bool laneCoversEnabled,
                                          [JsonProperty("sudden_units")] float suddenUnits,
                                          [JsonProperty("hidden_units")] float hiddenUnits,
                                          [JsonProperty("lift_units")] float liftUnits,
                                          [JsonProperty("sudden_opacity")] float suddenOpacity,
                                          [JsonProperty("hidden_opacity")] float hiddenOpacity,
                                          [JsonProperty("normal_hi_speed_multiplier")] double? normalHiSpeedMultiplier = null)
        {
            HiSpeedMode = hiSpeedMode;
            NormalHiSpeed = normalHiSpeed;
            NormalHiSpeedMultiplier = normalHiSpeedMultiplier;
            FloatingHiSpeed = floatingHiSpeed;
            ClassicHiSpeed = classicHiSpeed;
            SuddenEnabled = suddenEnabled;
            HiddenEnabled = hiddenEnabled;
            LiftEnabled = liftEnabled;
            LaneCoversEnabled = laneCoversEnabled;
            SuddenUnits = suddenUnits;
            HiddenUnits = hiddenUnits;
            LiftUnits = liftUnits;
            SuddenOpacity = suddenOpacity;
            HiddenOpacity = hiddenOpacity;
        }

        public void Validate()
        {
            if (!Enum.IsDefined(HiSpeedMode))
                throw new InvalidDataException("The BMS reading configuration has an unknown Hi-Speed mode.");

            validateRange(NormalHiSpeed, BmsRulesetConfigManager.NORMAL_HI_SPEED_MIN, BmsRulesetConfigManager.NORMAL_HI_SPEED_MAX, nameof(NormalHiSpeed));
            if (NormalHiSpeedMultiplier is double normalMultiplier)
            {
                validateRange(normalMultiplier, double.Epsilon, double.MaxValue, nameof(NormalHiSpeedMultiplier));
                float raw = (float)(normalMultiplier * 3);
                if (!float.IsFinite(raw) || raw <= 0)
                    throw new InvalidDataException("The resolved Normal Hi-Speed multiplier must fit a positive native raw speed.");
            }
            validateRange(FloatingHiSpeed, BmsRulesetConfigManager.FLOATING_HI_SPEED_MIN, BmsRulesetConfigManager.FLOATING_HI_SPEED_MAX, nameof(FloatingHiSpeed));
            validateRange(ClassicHiSpeed, BmsRulesetConfigManager.CLASSIC_HI_SPEED_MIN, BmsRulesetConfigManager.CLASSIC_HI_SPEED_MAX, nameof(ClassicHiSpeed));
            validateRange(SuddenUnits, 0, 1000, nameof(SuddenUnits));
            validateRange(HiddenUnits, 0, 1000, nameof(HiddenUnits));
            validateRange(LiftUnits, 0, 1000, nameof(LiftUnits));
            validateRange(SuddenOpacity, 0, 1000, nameof(SuddenOpacity));
            validateRange(HiddenOpacity, 0, 1000, nameof(HiddenOpacity));
        }

        private static void validateRange(double value, double minimum, double maximum, string name)
        {
            if (!double.IsFinite(value) || value < minimum || value > maximum)
                throw new InvalidDataException($"The BMS reading configuration's {name} must be finite and between {minimum} and {maximum}.");
        }
    }
}
