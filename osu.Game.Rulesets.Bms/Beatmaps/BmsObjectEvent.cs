// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved BMS object event that is not part of a paired long note.
    /// </summary>
    public readonly record struct BmsObjectEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int Channel { get; }

        public int ObjectId { get; }

        public bool AutoPlay { get; }

        public BmsObjectEvent(int measureIndex, double fractionWithinMeasure, int channel, int objectId, bool autoPlay)
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
            AutoPlay = autoPlay;
        }
    }
}
