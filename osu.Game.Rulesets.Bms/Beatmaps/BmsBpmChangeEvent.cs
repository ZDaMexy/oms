// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved BPM change from channel 03 or 08.
    /// </summary>
    public readonly record struct BmsBpmChangeEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int Channel { get; }

        public int SourceValue { get; }

        public double Bpm { get; }

        public BmsBpmChangeEvent(int measureIndex, double fractionWithinMeasure, int channel, int sourceValue, double bpm)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (sourceValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceValue), @"Source value must be greater than zero.");

            if (bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm), @"BPM must be greater than zero.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            Channel = channel;
            SourceValue = sourceValue;
            Bpm = bpm;
        }
    }
}
