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
    /// until a visual offset or <see cref="EngageStopMotion"/> selects a BMS-specific algorithm.
    /// </summary>
    /// <remarks>
    /// While disengaged with zero fixed offset and no automatic adjustment, the algorithm value tracks the base bindable
    /// instance-for-instance, so rendering is byte-for-byte identical to the normal forward-scroll path. Engagement is
    /// gated by <see cref="Configuration.BmsGimmickScrollMode"/> and is fully reversible.
    /// </remarks>
    public sealed class BmsScrollingInfo : IScrollingInfo
    {
        private readonly IBindable<IScrollAlgorithm> baseAlgorithm;
        private readonly Bindable<IScrollAlgorithm> algorithm = new Bindable<IScrollAlgorithm>();

        private IScrollAlgorithm? stopMotionAlgorithm;
        private BmsVisualOffsetScrollAlgorithm? visualOffsetAlgorithm;

        public BindableDouble VisualOffset { get; } = new BindableDouble();

        public BindableBool AutomaticVisualOffsetEnabled { get; } = new BindableBool();

        public IBindable<ScrollingDirection> Direction { get; }

        public IBindable<double> TimeRange { get; }

        public IBindable<IScrollAlgorithm> Algorithm => algorithm;

        public BmsScrollingInfo(IScrollingInfo baseInfo)
        {
            Direction = baseInfo.Direction;
            TimeRange = baseInfo.TimeRange;

            baseAlgorithm = baseInfo.Algorithm.GetBoundCopy();
            baseAlgorithm.BindValueChanged(_ =>
            {
                if (stopMotionAlgorithm == null)
                    updateAlgorithm();
            }, true);
            VisualOffset.BindValueChanged(_ => updateAlgorithm());
            AutomaticVisualOffsetEnabled.BindValueChanged(_ => updateAlgorithm());
        }

        /// <summary>Take over visual positioning with the BMS stop-motion algorithm (gimmick render mode).</summary>
        public void EngageStopMotion(IScrollAlgorithm stopMotionAlgorithm)
        {
            this.stopMotionAlgorithm = stopMotionAlgorithm;
            updateAlgorithm();
        }

        /// <summary>Revert to the base (normal forward-scroll) algorithm.</summary>
        public void Disengage()
        {
            stopMotionAlgorithm = null;
            updateAlgorithm();
        }

        private void updateAlgorithm()
        {
            IScrollAlgorithm selected = stopMotionAlgorithm ?? baseAlgorithm.Value;

            if (VisualOffset.Value == 0 && !AutomaticVisualOffsetEnabled.Value)
            {
                algorithm.Value = selected;
                return;
            }

            if (visualOffsetAlgorithm == null || !ReferenceEquals(visualOffsetAlgorithm.BaseAlgorithm, selected))
                visualOffsetAlgorithm = new BmsVisualOffsetScrollAlgorithm(selected, VisualOffset);

            // Keep this instance throughout live adjustment, including zero crossings. Containers recalculate all
            // lifetimes when the algorithm bindable changes, while positions already update on every frame.
            algorithm.Value = visualOffsetAlgorithm;
        }
    }
}
