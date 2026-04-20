// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Configuration;

namespace osu.Game.Rulesets.Bms.UI
{
    public readonly struct BmsScrollSpeedMetrics : IEquatable<BmsScrollSpeedMetrics>
    {
        public BmsHiSpeedMode HiSpeedMode { get; }

        public double ScrollSpeed { get; }

        public double BaseTimeRange { get; }

        public double RuntimeTimeRange { get; }

        public double ScrollLengthRatio { get; }

        public int SuddenUnits { get; }

        public int HiddenUnits { get; }

        public int LiftUnits { get; }

        public int VisibleLaneUnits { get; }

        public double VisibleLaneTime { get; }

        public int WhiteNumber => SuddenUnits;

        public int GreenNumber { get; }

        private BmsScrollSpeedMetrics(BmsHiSpeedMode hiSpeedMode, double scrollSpeed, double baseTimeRange, double runtimeTimeRange, double scrollLengthRatio, int suddenUnits, int hiddenUnits, int liftUnits, int visibleLaneUnits, double visibleLaneTime)
        {
            HiSpeedMode = hiSpeedMode;
            ScrollSpeed = scrollSpeed;
            BaseTimeRange = baseTimeRange;
            RuntimeTimeRange = runtimeTimeRange;
            ScrollLengthRatio = scrollLengthRatio;
            SuddenUnits = suddenUnits;
            HiddenUnits = hiddenUnits;
            LiftUnits = liftUnits;
            VisibleLaneUnits = visibleLaneUnits;
            VisibleLaneTime = visibleLaneTime;
            GreenNumber = Math.Max(0, (int)Math.Round(visibleLaneTime * 0.6, MidpointRounding.AwayFromZero));
        }

        public static BmsScrollSpeedMetrics FromRuntime(BmsHiSpeedMode hiSpeedMode, double scrollSpeed, double scrollLengthRatio, double timeRangeScale = 1, float suddenUnits = 0, float hiddenUnits = 0, float liftUnits = 0)
        {
            double baseTimeRange = DrawableBmsRuleset.ComputeScrollTime(scrollSpeed) * Math.Max(0, timeRangeScale);
            double normalisedScrollLengthRatio = Math.Max(0, scrollLengthRatio);
            double runtimeTimeRange = baseTimeRange * normalisedScrollLengthRatio;

            int clampedSuddenUnits = clampCoverUnits(suddenUnits);
            int clampedHiddenUnits = clampCoverUnits(hiddenUnits);
            int clampedLiftUnits = clampCoverUnits(liftUnits);
            int visibleLaneUnits = Math.Max(0, 1000 - clampedSuddenUnits - clampedHiddenUnits);

            return new BmsScrollSpeedMetrics(
                hiSpeedMode,
                scrollSpeed,
                baseTimeRange,
                runtimeTimeRange,
                normalisedScrollLengthRatio,
                clampedSuddenUnits,
                clampedHiddenUnits,
                clampedLiftUnits,
                visibleLaneUnits,
                runtimeTimeRange * visibleLaneUnits / 1000.0);
        }

        public bool Equals(BmsScrollSpeedMetrics other)
            => HiSpeedMode == other.HiSpeedMode
               && ScrollSpeed.Equals(other.ScrollSpeed)
               && BaseTimeRange.Equals(other.BaseTimeRange)
               && RuntimeTimeRange.Equals(other.RuntimeTimeRange)
               && ScrollLengthRatio.Equals(other.ScrollLengthRatio)
               && SuddenUnits == other.SuddenUnits
               && HiddenUnits == other.HiddenUnits
               && LiftUnits == other.LiftUnits
               && VisibleLaneUnits == other.VisibleLaneUnits
               && VisibleLaneTime.Equals(other.VisibleLaneTime)
               && GreenNumber == other.GreenNumber;

        public override bool Equals(object? obj)
            => obj is BmsScrollSpeedMetrics other && Equals(other);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(HiSpeedMode);
            hashCode.Add(ScrollSpeed);
            hashCode.Add(BaseTimeRange);
            hashCode.Add(RuntimeTimeRange);
            hashCode.Add(ScrollLengthRatio);
            hashCode.Add(SuddenUnits);
            hashCode.Add(HiddenUnits);
            hashCode.Add(LiftUnits);
            hashCode.Add(VisibleLaneUnits);
            hashCode.Add(VisibleLaneTime);
            hashCode.Add(GreenNumber);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(BmsScrollSpeedMetrics left, BmsScrollSpeedMetrics right)
            => left.Equals(right);

        public static bool operator !=(BmsScrollSpeedMetrics left, BmsScrollSpeedMetrics right)
            => !left.Equals(right);

        private static int clampCoverUnits(float coverUnits)
            => (int)Math.Clamp(Math.Round(coverUnits), 0, 1000);
    }
}
