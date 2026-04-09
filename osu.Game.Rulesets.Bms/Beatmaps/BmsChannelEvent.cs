// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A raw event decoded from a BMS measure/channel block before conversion to osu! hit objects.
    /// </summary>
    public readonly record struct BmsChannelEvent
    {
        public int MeasureIndex { get; }

        public int Channel { get; }

        public double FractionWithinMeasure { get; }

        public string RawValue { get; }

        public BmsChannelEvent(int measureIndex, int channel, double fractionWithinMeasure, string rawValue)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            MeasureIndex = measureIndex;
            Channel = channel;
            FractionWithinMeasure = fractionWithinMeasure;
            RawValue = rawValue ?? throw new ArgumentNullException(nameof(rawValue));
        }
    }
}
