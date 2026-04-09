// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public sealed class BmsGaugeHistory
    {
        public double StartTime { get; }

        public double EndTime { get; }

        public IReadOnlyList<BmsGaugeHistoryTimeline> Timelines { get; }

        public BmsGaugeHistory(double startTime, double endTime, IReadOnlyList<BmsGaugeHistoryTimeline> timelines)
        {
            StartTime = startTime;
            EndTime = endTime;
            Timelines = timelines;
        }
    }

    public sealed class BmsGaugeHistoryTimeline
    {
        public BmsGaugeType GaugeType { get; }

        public IReadOnlyList<BmsGaugeHistoryPoint> Samples { get; }

        public BmsGaugeHistoryTimeline(BmsGaugeType gaugeType, IReadOnlyList<BmsGaugeHistoryPoint> samples)
        {
            GaugeType = gaugeType;
            Samples = samples;
        }
    }

    public readonly struct BmsGaugeHistoryPoint
    {
        public readonly double Time;
        public readonly double Value;

        public BmsGaugeHistoryPoint(double time, double value)
        {
            Time = time;
            Value = value;
        }
    }
}
