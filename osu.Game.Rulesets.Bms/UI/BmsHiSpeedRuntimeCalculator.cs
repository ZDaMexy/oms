// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Configuration;

namespace osu.Game.Rulesets.Bms.UI
{
    public static class BmsHiSpeedRuntimeCalculator
    {
        public const int CURRENT_VERSION = 1;

        private static readonly double[] classic_choices = { 1, 1.5, 2, 2.25, 2.5, 2.75, 3, 3.25, 3.5, 3.75, 4 };
        private static readonly int[] normal_green_numbers = { 1200, 1000, 800, 700, 650, 600, 550, 500, 480, 460, 440, 420, 400, 380, 360, 340, 320, 300, 280, 260 };

        public static ReadOnlySpan<double> ClassicChoices => classic_choices;

        // Native lane/scroll units, expressed on a fixed reference clock. Rendering FPS does not own
        // the OMS gameplay clock. The corresponding native tick frequency cancels from elapsed time.
        private const float reference_clock_rate = 120;
        private const float raw_speed_basis = 3;
        private const float native_lane_height = 723;
        private const float native_scroll_scale = 290.05419921875f;

        public static double ComputeBaseTimeRange(BmsHiSpeedMode mode, double hiSpeedValue, double mostCommonBeatLength, double initialBeatLength, double sliderMultiplier, int calculationVersion = 0, double? normalMultiplier = null)
        {
            double clampedMostCommonBeatLength = clampPositive(mostCommonBeatLength, TimingControlPoint.DEFAULT_BEAT_LENGTH);
            double clampedInitialBeatLength = clampPositive(initialBeatLength, clampedMostCommonBeatLength);

            if (calculationVersion == CURRENT_VERSION)
            {
                double multiplier = mode == BmsHiSpeedMode.Normal
                    ? normalMultiplier ?? GetNormalMultiplier(hiSpeedValue, clampedInitialBeatLength)
                    : hiSpeedValue;
                return ComputeMultiplierTimeRange(multiplier, clampedMostCommonBeatLength);
            }

            ArgumentOutOfRangeException.ThrowIfNotEqual(calculationVersion, 0);

            double clampedSliderMultiplier = clampPositive(sliderMultiplier, 1);

            double baseTimeRange = DrawableBmsRuleset.ComputeScrollTime(hiSpeedValue);

            double modeScale = mode switch
            {
                BmsHiSpeedMode.Normal => 1,
                BmsHiSpeedMode.Floating => clampedMostCommonBeatLength / clampedInitialBeatLength,
                BmsHiSpeedMode.Classic => clampedMostCommonBeatLength / (clampedSliderMultiplier * TimingControlPoint.DEFAULT_BEAT_LENGTH),
                _ => 1,
            };

            return baseTimeRange * modeScale;
        }

        public static int GetNormalLevel(double value) => (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 1, normal_green_numbers.Length);

        public static int GetNormalGreenNumber(double level) => normal_green_numbers[GetNormalLevel(level) - 1];

        public static double ComputeMultiplierTimeRange(double multiplier, double beatLength)
        {
            // Preserve the native float32 operation boundaries before converting the result to milliseconds.
            float factor = 1000f / reference_clock_rate;
            factor *= 480;
            factor /= 1000;
            factor /= native_scroll_scale;
            float denominator = (float)(multiplier * raw_speed_basis) * (factor * (float)(60000 / beatLength));
            float unroundedGreenNumber = native_lane_height / denominator * 10;
            return unroundedGreenNumber / 0.6;
        }

        public static double GetNormalMultiplier(double level, double referenceBeatLength, double visibleLaneRatio = 1)
        {
            float factor = 1000f / reference_clock_rate;
            factor *= 480;
            factor /= 1000;
            factor /= native_scroll_scale;
            factor *= (float)(60000 / referenceBeatLength);
            factor *= GetNormalGreenNumber(level) / 10f;
            float raw = (float)(native_lane_height * visibleLaneRatio) / factor;
            return raw / (double)raw_speed_basis;
        }

        public static double NearestClassic(double multiplier)
        {
            float target = (float)(multiplier * raw_speed_basis);
            double nearest = classic_choices[0];
            float distance = float.PositiveInfinity;
            foreach (double choice in classic_choices)
            {
                float candidateDistance = Math.Abs((float)(choice * raw_speed_basis) - target);
                if (candidateDistance < distance)
                {
                    distance = candidateDistance;
                    nearest = choice;
                }
            }

            return nearest;
        }

        public static int NearestNormalLevel(double multiplier, double referenceBeatLength, double visibleLaneRatio = 1)
        {
            float target = (float)(multiplier * raw_speed_basis);
            int nearest = 1;
            float distance = float.PositiveInfinity;
            for (int level = 1; level <= normal_green_numbers.Length; level++)
            {
                float candidateDistance = Math.Abs((float)(GetNormalMultiplier(level, referenceBeatLength, visibleLaneRatio) * raw_speed_basis) - target);
                if (candidateDistance < distance)
                {
                    distance = candidateDistance;
                    nearest = level;
                }
            }

            return nearest;
        }

        public static double AdjustFloatingFromScratch(double multiplier, double steps)
        {
            float raw = (float)(multiplier * raw_speed_basis);
            raw += (float)(steps * 0.01);
            return Math.Clamp(raw / (double)raw_speed_basis, BmsRulesetConfigManager.FLOATING_HI_SPEED_MIN, BmsRulesetConfigManager.FLOATING_HI_SPEED_MAX);
        }

        private static double clampPositive(double value, double fallback)
            => value > 0 ? value : fallback;
    }
}
