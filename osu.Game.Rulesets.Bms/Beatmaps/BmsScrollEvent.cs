// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved SCROLL event from the SC channel.
    /// </summary>
    public readonly record struct BmsScrollEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int ScrollIndex { get; }

        public double ScrollValue { get; }

        public BmsScrollEvent(int measureIndex, double fractionWithinMeasure, int scrollIndex, double scrollValue)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (scrollIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(scrollIndex), @"SCROLL index must be greater than zero.");

            if (!double.IsFinite(scrollValue))
                throw new ArgumentOutOfRangeException(nameof(scrollValue), @"SCROLL value must be finite.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            ScrollIndex = scrollIndex;
            ScrollValue = scrollValue;
        }
    }
}
