// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsColumnBackground : OmsManiaColumnElement, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                               IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        internal bool IsStageLastColumn { get; private set; }

        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
            = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

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
            GameplaySkinLaneTopologyEntry lane = TopologyLane;
            GameplaySkinLaneTopologyGroup group = TopologyGroup;
            bool isLastColumn = lane.GroupLocalLogicalIndex == group.LanesInLogicalOrder.Count - 1;
            IsStageLastColumn = isLastColumn;
            bool hasRightLine = (rightLineWidth > 0 && skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value >= 2.4m) || isLastColumn;

            Color4 lineColour = GetColumnSkinConfig<Color4>(skin, LegacyManiaSkinConfigurationLookups.ColumnLineColour)?.Value ?? Color4.White;
            Color4 backgroundColour = GetColumnSkinConfig<Color4>(skin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour)?.Value ?? Color4.Black;

            Box laneSurface;
            HitPositionPaddedContainer laneDividers;

            InternalChildren = new Drawable[]
            {
                laneSurface = LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }, backgroundColour),
                laneDividers = new HitPositionPaddedContainer
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

            gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneSurface, laneSurface),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneDivider, laneDividers),
            });
            GameplaySkinProgrammaticVisualPartsReady();
        }
    }
}
