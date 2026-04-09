// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved STOP event from channel 09.
    /// </summary>
    public readonly record struct BmsStopEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int StopIndex { get; }

        public double StopValue { get; }

        public BmsStopEvent(int measureIndex, double fractionWithinMeasure, int stopIndex, double stopValue)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (stopIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(stopIndex), @"STOP index must be greater than zero.");

            if (stopValue < 0)
                throw new ArgumentOutOfRangeException(nameof(stopValue), @"STOP value must be zero or greater.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            StopIndex = stopIndex;
            StopValue = stopValue;
        }
    }
}
