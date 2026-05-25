// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A raw event decoded from a BMS measure/channel block before conversion to osu! hit objects.
    /// </summary>
    public readonly record struct BmsChannelEvent
    {
        public int MeasureIndex { get; }

        public int Channel { get; }

        public string RawChannelToken { get; }

        public double FractionWithinMeasure { get; }

        public string RawValue { get; }

        public int SourceLineOrder { get; }

        public BmsChannelEvent(int measureIndex, int channel, string rawChannelToken, double fractionWithinMeasure, string rawValue, int sourceLineOrder)
        {
            if (measureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(measureIndex), @"Measure index must be zero or greater.");

            if (string.IsNullOrWhiteSpace(rawChannelToken))
                throw new ArgumentException(@"Raw channel token must not be null or whitespace.", nameof(rawChannelToken));

            if (fractionWithinMeasure < 0 || fractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(fractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (sourceLineOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceLineOrder), @"Source line order must be zero or greater.");

            MeasureIndex = measureIndex;
            Channel = channel;
            RawChannelToken = rawChannelToken;
            FractionWithinMeasure = fractionWithinMeasure;
            RawValue = rawValue ?? throw new ArgumentNullException(nameof(rawValue));
            SourceLineOrder = sourceLineOrder;
        }

        public BmsChannelEvent(int measureIndex, int channel, double fractionWithinMeasure, string rawValue)
            : this(measureIndex, channel, channel.ToString(@"X2", CultureInfo.InvariantCulture), fractionWithinMeasure, rawValue, 0)
        {
        }
    }
}
