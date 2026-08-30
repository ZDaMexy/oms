// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    /// <summary>
    /// A <see cref="CompositeDrawable"/> which is placed somewhere within a <see cref="Column"/>.
    /// </summary>
    public partial class LegacyManiaColumnElement : CompositeDrawable
    {
        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private ManiaGameplaySkinLaneContext layoutLaneContext { get; set; } = null!;

        protected GameplaySkinLaneTopologyEntry TopologyLane => layoutLaneContext.Lane.TopologyEntry;

        protected GameplaySkinLaneTopologyGroup TopologyGroup => layoutLaneContext.Snapshot.GetGroup(TopologyLane.Identity.Group.Id).TopologyGroup;

        /// <summary>
        /// The column type identifier to use for texture lookups, in the case of no user-provided configuration.
        /// </summary>
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

        protected IBindable<T>? GetColumnSkinConfig<T>(ISkin skin, LegacyManiaSkinConfigurationLookups lookup) where T : notnull
            => skin.GetManiaSkinConfig<T>(lookup, Column.Index);
    }
}
