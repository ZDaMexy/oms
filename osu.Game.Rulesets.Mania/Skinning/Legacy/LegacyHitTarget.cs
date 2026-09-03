// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyHitTarget : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                          IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        private readonly int? groupLocalLaneIndex;
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;

        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
            = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

        public LegacyHitTarget(int? groupLocalLaneIndex = null)
        {
            if (groupLocalLaneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(groupLocalLaneIndex));

            this.groupLocalLaneIndex = groupLocalLaneIndex;
            Masking = groupLocalLaneIndex.HasValue;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, ManiaGameplaySkinStageContext stageContext)
        {
            string targetImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.HitTargetImage)?.Value
                                 ?? "mania-stage-hint";

            bool showJudgementLine = skin.GetManiaSkinConfig<bool>(LegacyManiaSkinConfigurationLookups.ShowJudgementLine)?.Value
                                     ?? true;

            Color4 lineColour = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.JudgementLineColour)?.Value
                                ?? Color4.White;

            Sprite hitTarget;
            Box judgementLine;

            InternalChild = directionContainer = new Container
            {
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    hitTarget = new Sprite
                    {
                        Texture = skin.GetTexture(targetImage),
                        Scale = new Vector2(1, 0.9f * 1.6025f),
                        RelativeSizeAxes = Axes.X,
                        Width = 1
                    },
                    judgementLine = new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Colour = LegacyColourCompatibility.DisallowZeroAlpha(lineColour),
                        Alpha = showJudgementLine ? 0.9f : 0
                    }
                }
            };

            if (groupLocalLaneIndex is int localIndex)
            {
                GameplaySkinLaneTopologyGroup group = stageContext.Group.TopologyGroup;

                if ((uint)localIndex >= (uint)group.LanesInLogicalOrder.Count)
                    throw new InvalidOperationException("A legacy hit-target slice requires an exact group-local lane index.");

                GameplaySkinLayoutRect groupRect = stageContext.Group.Rect;
                GameplaySkinLayoutRect laneRect = stageContext.Snapshot.GetLane(group.LanesInLogicalOrder[localIndex].Identity.Id).Rect;
                hitTarget.Width = groupRect.Width / laneRect.Width;
                hitTarget.X = -(laneRect.Left - groupRect.Left) / laneRect.Width;
            }

            gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.HitTarget, hitTarget, groupLocalLaneIndex),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.JudgementLine, judgementLine),
            });
            GameplaySkinProgrammaticVisualPartsReady();

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                directionContainer.Anchor = Anchor.TopLeft;
                directionContainer.Scale = new Vector2(1, -1);
            }
            else
            {
                directionContainer.Anchor = Anchor.BottomLeft;
                directionContainer.Scale = Vector2.One;
            }
        }
    }
}
