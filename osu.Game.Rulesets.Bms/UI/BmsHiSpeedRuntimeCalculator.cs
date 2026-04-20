// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Configuration;

namespace osu.Game.Rulesets.Bms.UI
{
    public static class BmsHiSpeedRuntimeCalculator
    {
        public static double ComputeBaseTimeRange(BmsHiSpeedMode mode, double hiSpeedValue, double mostCommonBeatLength, double initialBeatLength, double sliderMultiplier)
        {
            double clampedMostCommonBeatLength = clampPositive(mostCommonBeatLength, TimingControlPoint.DEFAULT_BEAT_LENGTH);
            double clampedInitialBeatLength = clampPositive(initialBeatLength, clampedMostCommonBeatLength);
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

        private static double clampPositive(double value, double fallback)
            => value > 0 ? value : fallback;
    }
}
