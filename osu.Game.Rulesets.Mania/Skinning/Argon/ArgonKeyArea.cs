// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Argon
{
    public partial class ArgonKeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly Bindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;
        private Drawable background = null!;

        private Circle hitTargetLine = null!;

        private Container<Circle> bottomIcon = null!;
        private CircularContainer topIcon = null!;

        private Bindable<Color4> accentColour = null!;

        private float hitTargetInsetFraction;

        [Resolved]
        private Column column { get; set; } = null!;

        public ArgonKeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ManiaGameplaySkinStageContext stageContext)
        {
            hitTargetInsetFraction = ManiaGameplaySkinLayoutProjection.GetHitTargetInsetFraction(stageContext);
            const float icon_circle_size = 8;
            const float icon_spacing = 7;
            const float icon_vertical_offset = -30;

            InternalChild = directionContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Children = new[]
                {
                    new Container
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = ArgonNotePiece.CORNER_RADIUS,
                        Child = background = new Box
                        {
                            Name = "Key gradient",
                            Alpha = 0,
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    hitTargetLine = new Circle
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Colour = OsuColour.Gray(196 / 255f),
                        Height = ArgonNotePiece.CORNER_RADIUS * 2,
                        Masking = true,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                    },
                    new Container
                    {
                        Name = "Icons",
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Children = new Drawable[]
                        {
                            bottomIcon = new Container<Circle>
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.Centre,
                                Blending = BlendingParameters.Additive,
                                Y = icon_vertical_offset,
                                Children = new[]
                                {
                                    new Circle
                                    {
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                    new Circle
                                    {
                                        X = -icon_spacing,
                                        Y = icon_spacing * 1.2f,
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                    new Circle
                                    {
                                        X = icon_spacing,
                                        Y = icon_spacing * 1.2f,
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                }
                            },
                            topIcon = new CircularContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.Centre,
                                Y = -icon_vertical_offset,
                                Size = new Vector2(22, 14),
                                Masking = true,
                                BorderThickness = 4,
                                BorderColour = Color4.White,
                                EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0,
                                        AlwaysPresent = true,
                                    },
                                },
                            }
                        }
                    },
                }
            };

            direction.Value = stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;
            direction.BindValueChanged(onDirectionChanged, true);

            accentColour = column.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(colour =>
                {
                    background.Colour = colour.NewValue.Darken(0.2f);
                    bottomIcon.Colour = colour.NewValue;
                },
                true);

            // Yes, proxy everything.
            column.TopLevelContainer.Add(CreateProxy());
        }

        protected override void Update()
        {
            base.Update();
            // The corner-radius extension is a component effect. The target position itself comes exclusively from
            // the exact snapshot shared by the stage and its lanes.
            directionContainer.Height = DrawHeight * hitTargetInsetFraction + ArgonNotePiece.CORNER_RADIUS * 2;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            switch (direction.NewValue)
            {
                case ScrollingDirection.Up:
                    directionContainer.Scale = new Vector2(1, -1);
                    directionContainer.Anchor = Anchor.TopLeft;
                    directionContainer.Origin = Anchor.BottomLeft;
                    break;

                case ScrollingDirection.Down:
                    directionContainer.Scale = new Vector2(1, 1);
                    directionContainer.Anchor = Anchor.BottomLeft;
                    directionContainer.Origin = Anchor.BottomLeft;
                    break;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return false;

            const double lighting_fade_in_duration = 70;
            Color4 lightingColour = getLightingColour();

            background
                .FlashColour(accentColour.Value.Lighten(0.8f), 200, Easing.OutQuint)
                .FadeTo(1, lighting_fade_in_duration, Easing.OutQuint)
                .Then()
                .FadeTo(0.8f, 500);

            hitTargetLine.FadeColour(Color4.White, lighting_fade_in_duration, Easing.OutQuint);
            hitTargetLine.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour.Opacity(0.4f),
                Radius = 20,
            }, lighting_fade_in_duration, Easing.OutQuint);

            topIcon.ScaleTo(0.9f, lighting_fade_in_duration, Easing.OutQuint);
            topIcon.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour.Opacity(0.1f),
                Radius = 20,
            }, lighting_fade_in_duration, Easing.OutQuint);

            bottomIcon.FadeColour(Color4.White, lighting_fade_in_duration, Easing.OutQuint);

            foreach (var circle in bottomIcon)
            {
                circle.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = lightingColour.Opacity(0.2f),
                    Radius = 60,
                }, lighting_fade_in_duration, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return;

            const double lighting_fade_out_duration = 800;

            Color4 lightingColour = getLightingColour().Opacity(0);

            // background fades out faster than lighting elements to give better definition to the player.
            background.FadeTo(0.3f, 50, Easing.OutQuint)
                      .Then()
                      .FadeOut(lighting_fade_out_duration, Easing.OutQuint);

            topIcon.ScaleTo(1f, 200, Easing.OutQuint);
            topIcon.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour,
                Radius = 20,
            }, lighting_fade_out_duration, Easing.OutQuint);

            hitTargetLine.FadeColour(OsuColour.Gray(196 / 255f), lighting_fade_out_duration, Easing.OutQuint);
            hitTargetLine.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour,
                Radius = 25,
            }, lighting_fade_out_duration, Easing.OutQuint);

            bottomIcon.FadeColour(accentColour.Value, lighting_fade_out_duration, Easing.OutQuint);

            foreach (var circle in bottomIcon)
            {
                circle.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = lightingColour,
                    Radius = 30,
                }, lighting_fade_out_duration, Easing.OutQuint);
            }
        }

        private Color4 getLightingColour() => Interpolation.ValueAt(0.2f, accentColour.Value, Color4.White, 0, 1);
    }
}
