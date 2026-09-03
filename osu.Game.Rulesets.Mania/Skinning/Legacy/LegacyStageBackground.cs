// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyStageBackground : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                                 IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        private Drawable leftSprite;
        private Drawable rightSprite;
        private Drawable playfieldBackdrop;
        private Drawable baseplate;
        private ColumnFlow<Drawable> columnBackgrounds;
        private ColumnFlow<LegacyHitTarget> hitTargets;

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => getGameplaySkinProgrammaticVisualParts();

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

        public LegacyStageBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, StageDefinition stageDefinition)
        {
            string leftImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.LeftStageImage)?.Value
                               ?? "mania-stage-left";

            string rightImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.RightStageImage)?.Value
                                ?? "mania-stage-right";

            InternalChildren = new[]
            {
                baseplate = new Container
                {
                    Name = "Playfield baseplate compatibility owner",
                    RelativeSizeAxes = Axes.Both,
                },
                playfieldBackdrop = new Container
                {
                    Name = "Playfield backdrop compatibility owner",
                    RelativeSizeAxes = Axes.Both,
                },
                leftSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopRight,
                    X = 0.05f,
                    Texture = skin.GetTexture(leftImage),
                },
                rightSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    X = -0.05f,
                    Texture = skin.GetTexture(rightImage)
                },
                columnBackgrounds = new ColumnFlow<Drawable>(stageDefinition)
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = false,
                },
                new HitTargetInsetContainer
                {
                    Child = hitTargets = new ColumnFlow<LegacyHitTarget>(stageDefinition)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = false,
                    },
                }
            };

            for (int i = 0; i < stageDefinition.Columns; i++)
            {
                var columnBackground = new ColumnBackground(i, i == stageDefinition.Columns - 1);
                var hitTarget = new LegacyHitTarget(i) { RelativeSizeAxes = Axes.Both };
                columnBackground.GameplaySkinProgrammaticVisualPartsReady += forwardProgrammaticVisualPartsReady;
                hitTarget.GameplaySkinProgrammaticVisualPartsReady += forwardProgrammaticVisualPartsReady;
                columnBackgrounds.SetContentForColumn(i, columnBackground);
                hitTargets.SetContentForColumn(i, hitTarget);
            }

            GameplaySkinProgrammaticVisualPartsReady();
        }

        private void forwardProgrammaticVisualPartsReady() => GameplaySkinProgrammaticVisualPartsReady();

        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> getGameplaySkinProgrammaticVisualParts()
        {
            if (leftSprite == null || rightSprite == null || playfieldBackdrop == null || columnBackgrounds == null || hitTargets == null)
                return Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

            var parts = new List<ManiaGameplaySkinProgrammaticVisualPart>
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.StageBackground, leftSprite),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.StageBackground, rightSprite),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBackdrop, playfieldBackdrop),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBaseplate, baseplate),
            };

            foreach (Drawable columnBackground in columnBackgrounds.Content)
            {
                if (columnBackground is IManiaGameplaySkinProgrammaticVisualPartProvider provider)
                    parts.AddRange(provider.GameplaySkinProgrammaticVisualParts);
            }

            foreach (LegacyHitTarget hitTarget in hitTargets.Content)
            {
                parts.AddRange(((IManiaGameplaySkinProgrammaticVisualPartProvider)hitTarget)
                               .GameplaySkinProgrammaticVisualParts);
            }

            return parts.AsReadOnly();
        }

        protected override void Update()
        {
            base.Update();

            if (leftSprite?.Height > 0)
                leftSprite.Scale = new Vector2(1, DrawHeight / leftSprite.Height);

            if (rightSprite?.Height > 0)
                rightSprite.Scale = new Vector2(1, DrawHeight / rightSprite.Height);
        }

        private partial class ColumnBackground : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                                 IManiaGameplaySkinProgrammaticVisualPartReadinessSource
        {
            private readonly int columnIndex;
            private readonly bool isLastColumn;

            private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
                = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

            IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
                => gameplaySkinProgrammaticVisualParts;

            public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

            public ColumnBackground(int columnIndex, bool isLastColumn)
            {
                this.columnIndex = columnIndex;
                this.isLastColumn = isLastColumn;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                float leftLineWidth = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.LeftLineWidth, columnIndex)?.Value ?? 1;
                float rightLineWidth = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.RightLineWidth, columnIndex)?.Value ?? 1;

                bool hasLeftLine = leftLineWidth > 0;
                bool hasRightLine = (rightLineWidth > 0 && skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value >= 2.4m) || isLastColumn;

                Color4 lineColour = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.ColumnLineColour, columnIndex)?.Value ?? Color4.White;
                Color4 backgroundColour = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, columnIndex)?.Value ?? Color4.Black;

                Box laneSurface;
                HitTargetInsetContainer laneDividers;

                InternalChildren = new Drawable[]
                {
                    laneSurface = LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    }, backgroundColour),
                    laneDividers = new HitTargetInsetContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = leftLineWidth,
                                Scale = new Vector2(0.740f, 1),
                                Alpha = hasLeftLine ? 1 : 0,
                                Child = LegacyColourCompatibility.ApplyWithDoubledAlpha(new Box
                                {
                                    RelativeSizeAxes = Axes.Both
                                }, lineColour)
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
                                    RelativeSizeAxes = Axes.Both
                                }, lineColour)
                            },
                        }
                    }
                };

                gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
                {
                    new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneSurface, laneSurface, columnIndex),
                    new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneDivider, laneDividers, columnIndex),
                });
                GameplaySkinProgrammaticVisualPartsReady();
            }
        }
    }
}
