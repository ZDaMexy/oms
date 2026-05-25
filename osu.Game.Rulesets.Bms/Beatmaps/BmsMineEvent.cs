// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved landmine event from the D/E mine channels.
    /// </summary>
    public readonly record struct BmsMineEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int Channel { get; }

        public int DamageValue { get; }

        public BmsMineEvent(int measureIndex, double fractionWithinMeasure, int channel, int damageValue)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (damageValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(damageValue), @"Damage value must be greater than zero.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            Channel = channel;
            DamageValue = damageValue;
        }
    }
}
