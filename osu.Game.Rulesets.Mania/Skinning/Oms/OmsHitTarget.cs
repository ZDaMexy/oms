// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHitTarget : OmsManiaColumnElement, IKeyBindingHandler<ManiaAction>, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                       IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        private readonly Bindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;
        private Container lightContainer = null!;
        private Drawable light = null!;

        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
            = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

        public OmsHitTarget()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, ManiaGameplaySkinStageContext stageContext)
        {
            string targetImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.HitTargetImage)?.Value
                                 ?? "mania-stage-hint";

            bool showJudgementLine = GetColumnSkinConfig<bool>(skin, LegacyManiaSkinConfigurationLookups.ShowJudgementLine)?.Value
                                     ?? true;

            Color4 lineColour = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.JudgementLineColour)?.Value
                                ?? Color4.White;

            string lightImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.LightImage)?.Value
                                ?? "mania-stage-light";

            float lightPosition = GetColumnSkinConfig<float>(skin, LegacyManiaSkinConfigurationLookups.LightPosition)?.Value
                                  ?? 0;

            Color4 lightColour = GetColumnSkinConfig<Color4>(skin, LegacyManiaSkinConfigurationLookups.ColumnLightColour)?.Value
                                 ?? Color4.White;

            int lightFramePerSecond = GetColumnSkinConfig<int>(skin, LegacyManiaSkinConfigurationLookups.LightFramePerSecond)?.Value ?? 60;

            var lightAnimation = skin.GetAnimation(lightImage, true, true, frameLength: 1000d / lightFramePerSecond);

            if (lightAnimation != null)
            {
                lightAnimation.Anchor = Anchor.BottomCentre;
                lightAnimation.Origin = Anchor.BottomCentre;
                lightAnimation.Colour = LegacyColourCompatibility.DisallowZeroAlpha(lightColour);
                lightAnimation.RelativeSizeAxes = Axes.X;
                lightAnimation.Width = 1;
                lightAnimation.Alpha = 0;
            }

            Sprite hitTarget;
            Box judgementLine;

            InternalChildren = new Drawable[]
            {
                directionContainer = new Container
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
                            Width = 1,
                        },
                        judgementLine = new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Colour = LegacyColourCompatibility.DisallowZeroAlpha(lineColour),
                            Alpha = showJudgementLine ? 0.9f : 0,
                        },
                    },
                },
                lightContainer = new Container
                {
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = lightPosition },
                    Child = light = lightAnimation ?? Empty(),
                },
            };

            gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.HitTarget, hitTarget),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.JudgementLine, judgementLine),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.KeyFlash, lightContainer),
            });
            GameplaySkinProgrammaticVisualPartsReady();

            direction.Value = stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                directionContainer.Anchor = Anchor.TopLeft;
                directionContainer.Scale = new Vector2(1, -1);
                lightContainer.Anchor = Anchor.TopCentre;
                lightContainer.Scale = new Vector2(1, -1);
            }
            else
            {
                directionContainer.Anchor = Anchor.BottomLeft;
                directionContainer.Scale = Vector2.One;
                lightContainer.Anchor = Anchor.BottomCentre;
                lightContainer.Scale = Vector2.One;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
            {
                light.FadeIn();
                light.ScaleTo(Vector2.One);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            const double animation_length = 250;

            if (e.Action == Column.Action.Value)
            {
                light.FadeTo(0, animation_length);
                light.ScaleTo(new Vector2(1, 0), animation_length);
            }
        }
    }
}
