// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsManiaColumnElement : CompositeDrawable
    {
        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private ManiaGameplaySkinLaneContext layoutLaneContext { get; set; } = null!;

        protected GameplaySkinLaneTopologyEntry TopologyLane => layoutLaneContext.Lane.TopologyEntry;

        protected GameplaySkinLaneTopologyGroup TopologyGroup => layoutLaneContext.Snapshot.GetGroup(TopologyLane.Identity.Group.Id).TopologyGroup;

        protected string FallbackColumnIndex { get; private set; } = null!;

        internal string ResolvedFallbackColumnIndex => FallbackColumnIndex;

        [BackgroundDependencyLoader]
        private void load()
        {
            GameplaySkinLaneTopologyEntry lane = TopologyLane;
            GameplaySkinLaneTopologyGroup group = TopologyGroup;

            if (lane.Identity.Role == GameplaySkinLaneRole.SpecialKey)
                FallbackColumnIndex = "S";
            else
            {
                int columnInStage = lane.GroupLocalLogicalIndex;
                int distanceToEdge = Math.Min(columnInStage, (group.LanesInLogicalOrder.Count - 1) - columnInStage);

                FallbackColumnIndex = distanceToEdge % 2 == 0 ? "1" : "2";
            }
        }

        protected IBindable<T>? GetColumnSkinConfig<T>(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
            where T : notnull
            => skin.GetManiaSkinConfig<T>(lookup, Column.Index);
    }
}
