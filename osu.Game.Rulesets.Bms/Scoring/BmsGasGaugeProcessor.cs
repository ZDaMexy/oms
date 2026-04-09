// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Rulesets.Judgements;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public partial class BmsGasGaugeProcessor : BmsGaugeProcessor
    {
        public override bool IsGaugeAutoShiftActive => true;

        public BmsGaugeType StartingGaugeType { get; }

        public BmsGaugeType FloorGaugeType { get; }

        public IReadOnlyList<BmsGaugeType> ActivatedGaugeTypes => activatedGaugeTypes;

        private readonly List<BmsGaugeType> activatedGaugeTypes = new List<BmsGaugeType>();
        private readonly Dictionary<JudgementResult, GaugeStateSnapshot> snapshots = new Dictionary<JudgementResult, GaugeStateSnapshot>();

        public BmsGasGaugeProcessor(double drainStartTime, BmsGaugeType startingGaugeType, BmsGaugeType floorGaugeType)
            : base(drainStartTime, startingGaugeType)
        {
            StartingGaugeType = startingGaugeType;
            FloorGaugeType = floorGaugeType > startingGaugeType ? startingGaugeType : floorGaugeType;
        }

        protected override void ApplyResultInternal(JudgementResult result)
        {
            snapshots[result] = new GaugeStateSnapshot(GaugeType, activatedGaugeTypes.Count);

            base.ApplyResultInternal(result);

            if (HasFailed && CheckDefaultFailCondition(result) && TryGetDowngradeTarget(out BmsGaugeType nextGaugeType))
            {
                base.RevertResultInternal(result);
                activateGauge(nextGaugeType);
            }
        }

        protected override void RevertResultInternal(JudgementResult result)
        {
            base.RevertResultInternal(result);

            if (!snapshots.Remove(result, out GaugeStateSnapshot snapshot))
                return;

            GaugeType = snapshot.GaugeType;

            if (activatedGaugeTypes.Count > snapshot.ActivatedGaugeCount)
                activatedGaugeTypes.RemoveRange(snapshot.ActivatedGaugeCount, activatedGaugeTypes.Count - snapshot.ActivatedGaugeCount);

            updateCurrentGaugeBounds();
        }

        protected override void Reset(bool storeResults)
        {
            snapshots.Clear();
            activatedGaugeTypes.Clear();
            GaugeType = StartingGaugeType;
            activatedGaugeTypes.Add(GaugeType);

            base.Reset(storeResults);

            updateCurrentGaugeBounds();
            Health.Value = GetStartingGauge(GaugeType);
        }

        private void activateGauge(BmsGaugeType gaugeType)
        {
            GaugeType = gaugeType;
            activatedGaugeTypes.Add(gaugeType);
            updateCurrentGaugeBounds();
            Health.Value = GetStartingGauge(gaugeType);
        }

        private bool TryGetDowngradeTarget(out BmsGaugeType nextGaugeType)
        {
            nextGaugeType = GaugeType;

            if (!UsesSurvivalClear(GaugeType) || GaugeType <= FloorGaugeType)
                return false;

            nextGaugeType = GetLowerGaugeType(GaugeType);
            return nextGaugeType >= FloorGaugeType;
        }

        private void updateCurrentGaugeBounds()
            => Health.MinValue = GetFloorGauge(GaugeType);

        private readonly struct GaugeStateSnapshot
        {
            public readonly BmsGaugeType GaugeType;
            public readonly int ActivatedGaugeCount;

            public GaugeStateSnapshot(BmsGaugeType gaugeType, int activatedGaugeCount)
            {
                GaugeType = gaugeType;
                ActivatedGaugeCount = activatedGaugeCount;
            }
        }
    }
}
