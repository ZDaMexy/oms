// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Pre-computed piecewise-linear cumulative scroll distance <c>D(t)</c> used by the BMS stop-motion visual bypass
    /// (P1-L Phase 2). Built once at conversion time from the <b>unclamped</b> BMS BPM / STOP / measure-length / scroll
    /// walk, so it is immune to the shared <see cref="osu.Game.Beatmaps.ControlPoints.TimingControlPoint"/>
    /// <c>[6, 60000]</c> beat-length clamp that squashes extreme soflan in the normal scrolling path.
    /// </summary>
    /// <remarks>
    /// Distance is expressed in <i>base-BPM milliseconds</i>: for a normal section (most-common BPM, scroll = 1) the
    /// slope is ~1, so <c>D(t) ≈ t</c> and the bypass degenerates to constant scroll. A STOP region contributes zero
    /// slope (a true freeze); an extreme-BPM region (e.g. 1,320,000 BPM) covers a large distance in near-zero time
    /// (a visual snap). The function is monotonic non-decreasing for charts without negative BPM/scroll — the only
    /// kind targeted by Phase 2. Negative scroll is deferred to Phase 3 and would make <see cref="TimeAtDistance"/>
    /// multi-valued.
    ///
    /// This type is pure data + pure functions (no framework dependency) so it can be unit-tested in isolation and
    /// consumed by a BMS-only <c>IScrollAlgorithm</c> without touching the shared scrolling container or judgement.
    /// </remarks>
    public sealed class BmsScrollProfile
    {
        private readonly double[] times;
        private readonly double[] distances;

        /// <summary>
        /// Reference beat length (ms) of the most-common BPM by playing time, used to scale distance into base-BPM
        /// milliseconds. Exposed for hi-speed calibration / diagnostics.
        /// </summary>
        public double BaseBeatLength { get; }

        /// <summary>Knot times (ms), strictly increasing.</summary>
        public IReadOnlyList<double> KnotTimes => times;

        /// <summary>Cumulative distance at each knot (base-BPM ms), non-decreasing in Phase 2.</summary>
        public IReadOnlyList<double> KnotDistances => distances;

        /// <summary>
        /// Fastest segment speed relative to base (|Δdistance| / Δtime). Base sections are ~1; a STOP plateau is 0; an
        /// extreme-BPM snap is huge (DEAD SOUL ≈ 10000). Used by gimmick auto-detection.
        /// </summary>
        public double MaxSlope { get; }

        /// <summary>Fraction of the timeline frozen by STOP (zero-distance plateaus). Used by gimmick auto-detection.</summary>
        public double FrozenFraction { get; }

        // Conservative auto-detection thresholds. Normal / moderate-soflan charts stay well under both (typical soflan
        // is < ~10x base and ~0% frozen); genuine stop-motion gimmick charts blow past them (DEAD SOUL: 10000x, 43%).
        private const double stop_motion_min_max_slope = 50;
        private const double stop_motion_min_frozen_fraction = 0.05;

        /// <summary>
        /// Heuristic: does this chart need the stop-motion bypass (i.e. the normal forward-scroll model would squash it)?
        /// True when the chart contains an extreme-BPM snap or a meaningful STOP freeze. Conservative by design so the
        /// gate's <c>Auto</c> mode never engages on ordinary charts.
        /// </summary>
        public bool IsStopMotionGimmick => MaxSlope >= stop_motion_min_max_slope || FrozenFraction >= stop_motion_min_frozen_fraction;

        public BmsScrollProfile(IReadOnlyList<double> knotTimes, IReadOnlyList<double> knotDistances, double baseBeatLength)
        {
            ArgumentNullException.ThrowIfNull(knotTimes);
            ArgumentNullException.ThrowIfNull(knotDistances);

            if (knotTimes.Count != knotDistances.Count)
                throw new ArgumentException("Knot time and distance counts must match.", nameof(knotDistances));

            // Guarantee at least an origin knot so every query is total.
            if (knotTimes.Count == 0)
            {
                times = new[] { 0d };
                distances = new[] { 0d };
            }
            else
            {
                times = knotTimes.ToArray();
                distances = knotDistances.ToArray();
            }

            BaseBeatLength = baseBeatLength > 0 ? baseBeatLength : 1;

            double maxSlope = 0;
            double frozen = 0;
            double total = times.Length > 1 ? times[^1] - times[0] : 0;

            for (int i = 1; i < times.Length; i++)
            {
                double dt = times[i] - times[i - 1];

                if (dt <= 0)
                    continue;

                double dd = Math.Abs(distances[i] - distances[i - 1]);

                if (dd / dt > maxSlope)
                    maxSlope = dd / dt;

                if (dd < 1e-9)
                    frozen += dt;
            }

            MaxSlope = maxSlope;
            FrozenFraction = total > 0 ? frozen / total : 0;
        }

        /// <summary>
        /// Cumulative scroll distance at <paramref name="time"/>. Linear interpolation between knots; linear
        /// extrapolation (using the nearest segment's slope) past either end so positions stay continuous.
        /// </summary>
        public double DistanceAt(double time)
        {
            int n = times.Length;

            if (n == 1)
                return distances[0];

            if (time <= times[0])
                return distances[0] + (time - times[0]) * segmentSlope(0, 1);

            if (time >= times[n - 1])
                return distances[n - 1] + (time - times[n - 1]) * segmentSlope(n - 2, n - 1);

            int hi = firstKnotAfterTime(time);
            int lo = hi - 1;
            double span = times[hi] - times[lo];
            double frac = span > 0 ? (time - times[lo]) / span : 0;
            return distances[lo] + frac * (distances[hi] - distances[lo]);
        }

        /// <summary>Signed distance covered between two times (<c>D(to) - D(from)</c>).</summary>
        public double PositionDelta(double fromTime, double toTime) => DistanceAt(toTime) - DistanceAt(fromTime);

        /// <summary>
        /// Inverse of <see cref="DistanceAt"/>: the <b>earliest</b> time whose distance reaches
        /// <paramref name="distance"/>. On a frozen (STOP) plateau the start of the plateau is returned. Assumes the
        /// non-decreasing distance of Phase 2; extrapolates past either end using the nearest segment's slope.
        /// </summary>
        public double TimeAtDistance(double distance)
        {
            int n = times.Length;

            if (n == 1)
                return times[0];

            if (distance <= distances[0])
                return times[0] + (distance - distances[0]) * inverseSegmentSlope(0, 1);

            if (distance >= distances[n - 1])
                return times[n - 1] + (distance - distances[n - 1]) * inverseSegmentSlope(n - 2, n - 1);

            int hi = firstKnotAtOrAfterDistance(distance);
            int lo = hi - 1;
            double span = distances[hi] - distances[lo];
            double frac = span > 0 ? (distance - distances[lo]) / span : 0;
            return times[lo] + frac * (times[hi] - times[lo]);
        }

        /// <summary>Distance covered per ms across the segment (0 across a STOP plateau).</summary>
        private double segmentSlope(int i0, int i1)
        {
            double dt = times[i1] - times[i0];
            return dt > 0 ? (distances[i1] - distances[i0]) / dt : 0;
        }

        /// <summary>Ms elapsed per unit distance across the segment (0 across a STOP plateau, so it returns the boundary time).</summary>
        private double inverseSegmentSlope(int i0, int i1)
        {
            double dd = distances[i1] - distances[i0];
            return dd > 0 ? (times[i1] - times[i0]) / dd : 0;
        }

        /// <summary>Smallest index whose time is strictly greater than <paramref name="time"/> (within the interior).</summary>
        private int firstKnotAfterTime(double time)
        {
            int lo = 1;
            int hi = times.Length - 1;

            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);

                if (times[mid] > time)
                    hi = mid;
                else
                    lo = mid + 1;
            }

            return lo;
        }

        /// <summary>Smallest index whose distance is greater than or equal to <paramref name="distance"/> (within the interior).</summary>
        private int firstKnotAtOrAfterDistance(double distance)
        {
            int lo = 1;
            int hi = distances.Length - 1;

            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);

                if (distances[mid] >= distance)
                    hi = mid;
                else
                    lo = mid + 1;
            }

            return lo;
        }
    }
}
