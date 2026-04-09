// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsColumnBackground : OmsManiaColumnElement
    {
        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        public OmsColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            float leftLineWidth = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.LeftLineWidth)?.Value ?? 1;
            float rightLineWidth = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.RightLineWidth)?.Value ?? 1;

            bool hasLeftLine = leftLineWidth > 0;
            bool isLastColumn = Column.Index % stageDefinition.Columns == stageDefinition.Columns - 1;
            bool hasRightLine = (rightLineWidth > 0 && skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value >= 2.4m) || isLastColumn;

            Color4 lineColour = GetColumnSkinConfig<Color4>(skin, LegacyManiaSkinConfigurationLookups.ColumnLineColour)?.Value ?? Color4.White;
            Color4 backgroundColour = GetColumnSkinConfig<Color4>(skin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour)?.Value ?? Color4.Black;

            InternalChildren = new Drawable[]
            {
                LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }, backgroundColour),
                new HitPositionPaddedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = leftLineWidth,
                                Scale = new Vector2(0.740f, 1),
                                Alpha = hasLeftLine ? 1 : 0,
                                Child = LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                }, lineColour),
                            },
                            new Container
                            {
                                X = isLastColumn ? -0.16f : 0,
                                Anchor = Anchor.TopRight,
                                RelativeSizeAxes = Axes.Y,
                                Width = rightLineWidth,
                                Scale = new Vector2(0.740f, 1),
                                Alpha = hasRightLine ? 1 : 0,
                                Child = LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                }, lineColour),
                            },
                        },
                    },
                },
            };
        }
    }
}