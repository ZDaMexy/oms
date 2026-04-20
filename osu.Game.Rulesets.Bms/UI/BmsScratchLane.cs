// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Difficulty;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsScratchLane : BmsLane
    {
        public BmsScratchLane(BmsLaneLayout.Lane lane, int laneCount, BmsKeymode keymode, BmsPlayfieldLayoutProfile layoutProfile, BindableFloat? liftUnits = null)
            : base(lane, laneCount, keymode, layoutProfile, liftUnits)
        {
        }

        protected override BmsHitTarget createHitTarget() => new BmsScratchHitTarget(createLookup(Skinning.BmsLaneSkinElements.HitTarget), LayoutProfile);
    }
}
