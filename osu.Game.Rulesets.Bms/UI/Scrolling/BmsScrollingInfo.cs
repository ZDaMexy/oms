// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Bms.UI.Scrolling
{
    /// <summary>
    /// A BMS-side <see cref="IScrollingInfo"/> that wraps the ruleset's base scrolling info and is re-cached by
    /// <see cref="BmsPlayfield"/> so the BMS lanes resolve it instead of the shared one (P1-L Phase 2). This is how the
    /// stop-motion bypass is injected <b>without touching any shared core type</b>: <see cref="Direction"/> and
    /// <see cref="TimeRange"/> pass straight through, and <see cref="Algorithm"/> follows the base algorithm exactly
    /// until <see cref="EngageStopMotion"/> swaps it for a gimmick chart.
    /// </summary>
    /// <remarks>
    /// While disengaged (the default, and always for non-gimmick charts) the algorithm value tracks the base bindable
    /// instance-for-instance, so rendering is byte-for-byte identical to the normal forward-scroll path. Engagement is
    /// gated by <see cref="Configuration.BmsGimmickScrollMode"/> and is fully reversible.
    /// </remarks>
    public sealed class BmsScrollingInfo : IScrollingInfo
    {
        private readonly IBindable<IScrollAlgorithm> baseAlgorithm;
        private readonly Bindable<IScrollAlgorithm> algorithm = new Bindable<IScrollAlgorithm>();

        private bool engaged;

        public IBindable<ScrollingDirection> Direction { get; }

        public IBindable<double> TimeRange { get; }

        public IBindable<IScrollAlgorithm> Algorithm => algorithm;

        public BmsScrollingInfo(IScrollingInfo baseInfo)
        {
            Direction = baseInfo.Direction;
            TimeRange = baseInfo.TimeRange;

            baseAlgorithm = baseInfo.Algorithm.GetBoundCopy();
            // Mirror the base algorithm while disengaged so the normal path is unchanged.
            baseAlgorithm.BindValueChanged(e =>
            {
                if (!engaged)
                    algorithm.Value = e.NewValue;
            }, true);
        }

        /// <summary>Take over visual positioning with the BMS stop-motion algorithm (gimmick render mode).</summary>
        public void EngageStopMotion(IScrollAlgorithm stopMotionAlgorithm)
        {
            engaged = true;
            algorithm.Value = stopMotionAlgorithm;
        }

        /// <summary>Revert to the base (normal forward-scroll) algorithm.</summary>
        public void Disengage()
        {
            engaged = false;
            algorithm.Value = baseAlgorithm.Value;
        }
    }
}
