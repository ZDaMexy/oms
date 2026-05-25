// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public enum BmsBgaLayer
    {
        Base,
        Poor,
        Layer,
        Layer2,
    }

    /// <summary>
    /// A resolved BGA event from the base / poor / layer channels.
    /// </summary>
    public readonly record struct BmsBgaEvent
    {
        public int MeasureIndex { get; }

        public double FractionWithinMeasure { get; }

        public int Channel { get; }

        public int BitmapId { get; }

        public BmsBgaLayer Layer { get; }

        public BmsBgaEvent(int measureIndex, double fractionWithinMeasure, int channel, int bitmapId, BmsBgaLayer layer)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (bitmapId <= 0)
                throw new ArgumentOutOfRangeException(nameof(bitmapId), @"Bitmap ID must be greater than zero.");

            MeasureIndex = measureIndex;
            FractionWithinMeasure = fractionWithinMeasure;
            Channel = channel;
            BitmapId = bitmapId;
            Layer = layer;
        }
    }
}
