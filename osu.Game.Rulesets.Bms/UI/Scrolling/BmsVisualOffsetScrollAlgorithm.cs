// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Bms.UI.Scrolling
{
    /// <summary>
    /// Shifts only the BMS display clock. Object times, nested geometry and judgement lifetimes retain their original
    /// meaning; a positive offset brings objects to the judgement line earlier.
    /// </summary>
    public sealed class BmsVisualOffsetScrollAlgorithm : IScrollAlgorithm
    {
        public const double MaximumOffset = 500;

        internal IScrollAlgorithm BaseAlgorithm { get; }

        private readonly IBindable<double> visualOffset;

        public BmsVisualOffsetScrollAlgorithm(IScrollAlgorithm baseAlgorithm, IBindable<double> visualOffset)
        {
            BaseAlgorithm = baseAlgorithm;
            this.visualOffset = visualOffset;
        }

        // Preload for the complete supported adjustment range. This remains valid when the offset changes live,
        // avoiding a full-chart lifetime invalidation for every judgement. The shared container still ensures each
        // object is loaded before its earliest judgement and owns its original judged lifetime end.
        public double GetDisplayStartTime(double originTime, float offset, double timeRange, float scrollLength)
            => BaseAlgorithm.GetDisplayStartTime(originTime, offset, timeRange, scrollLength) - MaximumOffset;

        public float GetLength(double startTime, double endTime, double timeRange, float scrollLength)
            => BaseAlgorithm.GetLength(startTime, endTime, timeRange, scrollLength);

        public float PositionAt(double time, double currentTime, double timeRange, float scrollLength, double? originTime = null)
            => BaseAlgorithm.PositionAt(time, originTime.HasValue ? currentTime : currentTime + visualOffset.Value, timeRange, scrollLength, originTime);

        public double TimeAt(float position, double currentTime, double timeRange, float scrollLength)
            => BaseAlgorithm.TimeAt(position, currentTime + visualOffset.Value, timeRange, scrollLength);

        public void Reset() => BaseAlgorithm.Reset();
    }
}
