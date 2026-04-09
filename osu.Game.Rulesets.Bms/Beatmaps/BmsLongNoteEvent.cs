// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A resolved BMS long note produced from LNOBJ or LNTYPE 1 data.
    /// </summary>
    public readonly record struct BmsLongNoteEvent
    {
        public int StartMeasureIndex { get; }

        public double StartFractionWithinMeasure { get; }

        public int EndMeasureIndex { get; }

        public double EndFractionWithinMeasure { get; }

        public int LaneChannel { get; }

        public int HeadObjectId { get; }

        public int TailObjectId { get; }

        public BmsLongNoteEncoding Encoding { get; }

        public BmsLongNoteEvent(int startMeasureIndex, double startFractionWithinMeasure, int endMeasureIndex, double endFractionWithinMeasure, int laneChannel, int headObjectId, int tailObjectId, BmsLongNoteEncoding encoding)
        {
            if (startMeasureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startMeasureIndex), @"Measure index must be zero or greater.");

            if (endMeasureIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(endMeasureIndex), @"Measure index must be zero or greater.");

            if (startFractionWithinMeasure < 0 || startFractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(startFractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (endFractionWithinMeasure < 0 || endFractionWithinMeasure >= 1)
                throw new ArgumentOutOfRangeException(nameof(endFractionWithinMeasure), @"Fraction must be in the range [0, 1).");

            if (endMeasureIndex < startMeasureIndex || endMeasureIndex == startMeasureIndex && endFractionWithinMeasure <= startFractionWithinMeasure)
                throw new ArgumentException(@"Long note tail must be after the head.");

            if (headObjectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(headObjectId), @"Object ID must be greater than zero.");

            if (tailObjectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(tailObjectId), @"Object ID must be greater than zero.");

            StartMeasureIndex = startMeasureIndex;
            StartFractionWithinMeasure = startFractionWithinMeasure;
            EndMeasureIndex = endMeasureIndex;
            EndFractionWithinMeasure = endFractionWithinMeasure;
            LaneChannel = laneChannel;
            HeadObjectId = headObjectId;
            TailObjectId = tailObjectId;
            Encoding = encoding;
        }
    }
}
