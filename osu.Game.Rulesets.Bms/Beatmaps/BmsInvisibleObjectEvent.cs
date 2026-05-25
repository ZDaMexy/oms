// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved invisible object event carried on the hidden object channels.
    /// </summary>
    public readonly record struct BmsInvisibleObjectEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int Channel { get; }

        public int ObjectId { get; }

        public BmsInvisibleObjectEvent(int measureIndex, double fractionWithinMeasure, int channel, int objectId)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (objectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(objectId), @"Object ID must be greater than zero.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            Channel = channel;
            ObjectId = objectId;
        }
    }
}
