// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Bms.UI.Scrolling
{
    /// <summary>
    /// BMS stop-motion visual scroll algorithm (P1-L Phase 2). Positions objects by the pre-computed unclamped scroll
    /// distance <see cref="BmsScrollProfile"/> instead of the clamped <c>TimingControlPoint</c>-derived multipliers, so
    /// extreme BPM (snap), STOP (true freeze) and measure-length placement are reproduced faithfully.
    /// </summary>
    /// <remarks>
    /// This is a drop-in <see cref="IScrollAlgorithm"/>: it reuses the shared <c>ScrollingHitObjectContainer</c>
    /// machinery untouched (the container delegates every position/length/lifetime query here). It is identical in form
    /// to <see cref="ConstantScrollAlgorithm"/> with chart time replaced by profile distance <c>D(t)</c>; for a normal
    /// chart <c>D(t) ≈ t</c> so it degenerates to constant scroll. It only takes over <b>visual positioning</b> and is
    /// engaged solely via the gated <see cref="BmsScrollingInfo"/>; judgement/scoring keep running on object times.
    /// The <c>originTime</c> argument (used by the sequential algorithm for control-point lookups) is irrelevant here
    /// because position is an absolute distance difference, so it is ignored.
    /// </remarks>
    public sealed class BmsStopMotionScrollAlgorithm : IScrollAlgorithm
    {
        private readonly BmsScrollProfile profile;

        public BmsStopMotionScrollAlgorithm(BmsScrollProfile profile)
        {
            this.profile = profile;
        }

        public double GetDisplayStartTime(double originTime, float offset, double timeRange, float scrollLength)
            => TimeAt(-(scrollLength + offset), originTime, timeRange, scrollLength);

        public float GetLength(double startTime, double endTime, double timeRange, float scrollLength)
            => (float)(profile.PositionDelta(startTime, endTime) / timeRange * scrollLength);

        public float PositionAt(double time, double currentTime, double timeRange, float scrollLength, double? originTime = null)
            => (float)(profile.PositionDelta(currentTime, time) / timeRange * scrollLength);

        public double TimeAt(float position, double currentTime, double timeRange, float scrollLength)
        {
            double targetDistance = profile.DistanceAt(currentTime) + position / scrollLength * timeRange;
            return profile.TimeAtDistance(targetDistance);
        }

        public void Reset()
        {
        }
    }
}
