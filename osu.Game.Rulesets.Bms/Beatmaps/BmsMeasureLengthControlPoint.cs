// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Represents a BMS channel 02 measure-length multiplier.
    /// </summary>
    public readonly record struct BmsMeasureLengthControlPoint
    {
        public int MeasureIndex { get; }

        public double Multiplier { get; }

        public BmsMeasureLengthControlPoint(int measureIndex, double multiplier)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(multiplier), @"Measure length multiplier must be a finite positive value.");

            MeasureIndex = measureIndex;
            Multiplier = multiplier;
        }
    }
}
