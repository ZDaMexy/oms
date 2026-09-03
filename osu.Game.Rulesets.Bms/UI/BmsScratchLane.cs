// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsScratchLane : BmsLane
    {
        public BmsScratchLane(
            BmsLaneLayout.Lane lane,
            int laneCount,
            BmsKeymode keymode,
            BmsPlayfieldLayoutProfile layoutProfile,
            BindableFloat? liftUnits = null,
            BmsGameplayLayoutLane? layoutSnapshotLane = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null,
            GameplaySkinResolvedMaterialSet? resolvedMaterialSet = null)
            : base(lane, laneCount, keymode, layoutProfile, liftUnits, layoutSnapshotLane, layoutSnapshot, resolvedMaterialSet)
        {
        }

        protected override BmsHitTarget createHitTarget() => new BmsScratchHitTarget(createLookup(BmsLaneSkinElements.HitTarget), LayoutProfile, LayoutSnapshot);
    }
}
