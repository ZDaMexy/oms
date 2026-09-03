// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Immutable musical-time projection emitted by the converter's one authoritative BMS timeline walk.
    /// STOP intervals retain the preceding real BPM and freeze beat progression.
    /// </summary>
    public sealed class BmsTimingProfile
    {
        private readonly BmsTimingSegment[] segments;
        private readonly double[] measureStartTimes;

        internal BmsTimingProfile(IEnumerable<BmsTimingSegment> segments, IEnumerable<double> measureStartTimes)
        {
            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(measureStartTimes);

            this.segments = segments.ToArray();
            this.measureStartTimes = measureStartTimes.ToArray();

            if (this.segments.Length == 0 || this.segments[0].StartTime != 0)
                throw new ArgumentException("A BMS timing profile must begin at gameplay time zero.", nameof(segments));

            if (!this.segments.Select(segment => segment.StartTime).SequenceEqual(this.segments.Select(segment => segment.StartTime).Order()))
                throw new ArgumentException("BMS timing profile segments must be ordered.", nameof(segments));

            if (!this.measureStartTimes.SequenceEqual(this.measureStartTimes.Order()))
                throw new ArgumentException("BMS measure starts must be ordered.", nameof(measureStartTimes));
        }

        public BmsTimingSample Sample(double gameplayTime)
        {
            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime));

            int segmentIndex = findFloor(segments, gameplayTime);
            BmsTimingSegment segment = segments[Math.Max(0, segmentIndex)];
            double beat = segment.StartBeat;

            if (!segment.IsStopped)
                beat += (gameplayTime - segment.StartTime) / (60000 / segment.Bpm);

            long barIndex = findFloor(measureStartTimes, gameplayTime);
            return new BmsTimingSample(beat, Math.Max(-1, barIndex), segment.Bpm, segment.IsStopped);
        }

        private static int findFloor(BmsTimingSegment[] values, double time)
        {
            int low = 0;
            int high = values.Length - 1;
            int result = -1;

            while (low <= high)
            {
                int middle = low + (high - low) / 2;

                if (values[middle].StartTime <= time)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }

        private static int findFloor(double[] values, double time)
        {
            int low = 0;
            int high = values.Length - 1;
            int result = -1;

            while (low <= high)
            {
                int middle = low + (high - low) / 2;

                if (values[middle] <= time)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }
    }

    public readonly struct BmsTimingSample
    {
        public double Beat { get; }

        public long BarIndex { get; }

        public double Bpm { get; }

        public bool IsStopped { get; }

        internal BmsTimingSample(double beat, long barIndex, double bpm, bool isStopped)
        {
            Beat = beat;
            BarIndex = barIndex;
            Bpm = bpm;
            IsStopped = isStopped;
        }
    }

    internal readonly struct BmsTimingSegment
    {
        public double StartTime { get; }

        public double StartBeat { get; }

        public double Bpm { get; }

        public bool IsStopped { get; }

        public BmsTimingSegment(double startTime, double startBeat, double bpm, bool isStopped)
        {
            StartTime = startTime;
            StartBeat = startBeat;
            Bpm = bpm;
            IsStopped = isStopped;
        }
    }
}
