// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsManiaColumnElement : CompositeDrawable
    {
        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        protected string FallbackColumnIndex { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Column.IsSpecial)
                FallbackColumnIndex = "S";
            else
            {
                int columnInStage = Column.Index % stageDefinition.Columns;
                int distanceToEdge = Math.Min(columnInStage, (stageDefinition.Columns - 1) - columnInStage);

                FallbackColumnIndex = distanceToEdge % 2 == 0 ? "1" : "2";
            }
        }

        protected IBindable<T>? GetColumnSkinConfig<T>(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
            where T : notnull
            => skin.GetManiaSkinConfig<T>(lookup, Column.Index);
    }
}