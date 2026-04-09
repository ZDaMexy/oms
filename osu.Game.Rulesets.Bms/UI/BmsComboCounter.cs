// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsComboCounter : ComboCounter
    {
        private TextComponent textComponent = null!;

        protected override double RollingDuration => 80;

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                textComponent.UpdateState(combo.NewValue);

                if (combo.NewValue > combo.OldValue && combo.NewValue > 0)
                    textComponent.Pulse();
                else if (combo.OldValue > 1 && combo.NewValue == 0)
                    textComponent.FlashMiss();
            }, true);
        }

        protected override LocalisableString FormatCount(int count) => $@"{count}x";

        protected override IHasText CreateText() => textComponent = new TextComponent();

        private partial class TextComponent : CompositeDrawable, IHasText
        {
            private readonly Container body;
            private readonly Box background;
            private readonly Box glow;
            private readonly Box accentStrip;
            private readonly OsuSpriteText labelText;
            private readonly OsuSpriteText countText;

            private Color4 currentAccent = BmsDefaultHudPalette.ComboInactiveAccent;

            public LocalisableString Text
            {
                get => countText.Text;
                set => countText.Text = value;
            }

            public TextComponent()
            {
                AutoSizeAxes = Axes.Both;

                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        labelText = new OsuSpriteText
                        {
                            Text = "COMBO",
                            Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                            Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            Padding = new MarginPadding { Left = 4 },
                        },
                        body = new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 7,
                            BorderThickness = 1,
                            BorderColour = BmsDefaultHudPalette.SurfaceBorder,
                            Children = new Drawable[]
                            {
                                background = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                glow = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0.08f,
                                },
                                accentStrip = new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 3,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                countText = new OsuSpriteText
                                {
                                    Margin = new MarginPadding
                                    {
                                        Top = 7,
                                        Right = 16,
                                        Bottom = 8,
                                        Left = 16,
                                    },
                                    Font = OsuFont.Numeric.With(size: 30, fixedWidth: true),
                                    Colour = BmsDefaultHudPalette.SurfaceText,
                                }
                            }
                        }
                    }
                };
            }

            public void UpdateState(int combo)
            {
                currentAccent = combo switch
                {
                    >= 100 => BmsDefaultHudPalette.ComboMilestoneAccent,
                    > 0 => BmsDefaultHudPalette.ComboActiveAccent,
                    _ => BmsDefaultHudPalette.ComboInactiveAccent,
                };

                bool active = combo > 0;

                background.Colour = ColourInfo.GradientVertical(
                    BmsDefaultHudPalette.ComboBackground,
                    currentAccent.Opacity(active ? 0.16f : 0.06f));
                glow.Colour = currentAccent;
                glow.Alpha = active ? 0.16f : 0.08f;
                accentStrip.Colour = currentAccent;
                body.BorderColour = currentAccent.Opacity(active ? 0.46f : 0.24f);
                labelText.Colour = active ? currentAccent : BmsDefaultHudPalette.SurfaceSubtext;
                countText.Colour = active ? BmsDefaultHudPalette.SurfaceText : BmsDefaultHudPalette.SurfaceSubtext;
            }

            public void Pulse()
            {
                body.ClearTransforms();
                body.ScaleTo(new Vector2(1.04f), 60, Easing.OutQuint)
                    .Then()
                    .ScaleTo(Vector2.One, 180, Easing.OutQuint);

                glow.ClearTransforms();
                glow.FadeTo(0.34f, 60, Easing.OutQuint)
                    .Then()
                    .FadeTo(0.16f, 220, Easing.OutQuint);
            }

            public void FlashMiss()
            {
                body.ClearTransforms();
                body.ScaleTo(new Vector2(0.96f), 70, Easing.OutQuint)
                    .Then()
                    .ScaleTo(Vector2.One, 220, Easing.OutQuint);

                glow.ClearTransforms();
                glow.FadeTo(0.24f, 50, Easing.OutQuint)
                    .Then()
                    .FadeTo(0.08f, 240, Easing.OutQuint);
            }
        }
    }
}
