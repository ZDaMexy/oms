// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Default
{
    public class ManiaTrianglesSkinTransformer : SkinTransformer
    {
        private readonly ManiaBeatmap beatmap;
        private readonly GameplaySkinLaneTopologySnapshot topology;

        public ManiaTrianglesSkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;
            topology = ManiaGameplaySkinLaneTopologyFactory.Create(this.beatmap);
        }

        private readonly Color4 colourEven = new Color4(6, 84, 0, 255);
        private readonly Color4 colourOdd = new Color4(94, 0, 57, 255);
        private readonly Color4 colourSpecial = new Color4(0, 48, 63, 255);

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:
                        int column = maniaLookup.ColumnIndex ?? 0;
                        GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[column];
                        GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder
                                                                            .Single(candidate => candidate.Identity.Id.Equals(lane.Identity.Group.Id));

                        if (lane.Identity.Role == GameplaySkinLaneRole.SpecialKey)
                            return SkinUtils.As<TValue>(new Bindable<Color4>(colourSpecial));

                        int columnInStage = lane.GroupLocalLogicalIndex;
                        int distanceToEdge = Math.Min(columnInStage, (group.LanesInLogicalOrder.Count - 1) - columnInStage);
                        return SkinUtils.As<TValue>(new Bindable<Color4>(distanceToEdge % 2 == 0 ? colourOdd : colourEven));
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }
    }
}
