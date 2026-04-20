// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsPreStartHiSpeedOverlay : VisibilityContainer, IKeyBindingHandler<BmsAction>
    {
        private readonly IBindable<BmsHiSpeedMode> hiSpeedMode;
        private readonly IBindable<double> hiSpeedValue;
        private readonly Func<int, bool> adjustHiSpeed;
        private readonly int variant;

        private OsuSpriteText modeText = null!;
        private OsuSpriteText valueText = null!;

        public BmsPreStartHiSpeedOverlay(int variant, IBindable<BmsHiSpeedMode> hiSpeedMode, IBindable<double> hiSpeedValue, Func<int, bool> adjustHiSpeed)
        {
            this.variant = variant;
            this.adjustHiSpeed = adjustHiSpeed;
            this.hiSpeedMode = hiSpeedMode.GetBoundCopy();
            this.hiSpeedValue = hiSpeedValue.GetBoundCopy();

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Right = 32 },
                Child = new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 220,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 12,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BmsDefaultHudPalette.SurfaceBackground,
                            Alpha = 0.92f,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Padding = new MarginPadding(16),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = @"READY HOLD",
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                    Colour = BmsDefaultHudPalette.SpeedFeedbackAccent,
                                },
                                modeText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                    Colour = BmsDefaultHudPalette.SurfaceText,
                                },
                                valueText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 26, weight: FontWeight.Black),
                                    Colour = BmsDefaultHudPalette.SpeedFeedbackAccent,
                                },
                                new OsuSpriteText
                                {
                                    Text = @"松开阻塞键后开始播放",
                                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Medium),
                                    Colour = BmsDefaultHudPalette.SurfaceSubtext,
                                },
                            }
                        }
                    }
                }
            };

            hiSpeedMode.BindValueChanged(_ => updateText(), true);
            hiSpeedValue.BindValueChanged(_ => updateText(), true);

            Hide();
        }

        public bool OnPressed(KeyBindingPressEvent<BmsAction> e)
            => TryHandleActionPress(e.Action);

        public bool TryHandleActionPress(BmsAction action)
        {
            if (State.Value != Visibility.Visible)
                return false;

            int direction = action.GetHiSpeedAdjustmentDirection(variant);

            if (direction == 0)
                return false;

            return adjustHiSpeed(direction);
        }

        public void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
        {
        }

        private void updateText()
        {
            modeText.Text = hiSpeedMode.Value switch
            {
                BmsHiSpeedMode.Normal => @"Normal Hi-Speed",
                BmsHiSpeedMode.Floating => @"Floating Hi-Speed",
                BmsHiSpeedMode.Classic => @"Classic Hi-Speed",
                _ => @"Hi-Speed",
            };

            valueText.Text = hiSpeedMode.Value.FormatValue(hiSpeedValue.Value);
        }

        protected override void PopIn() => this.FadeIn(120, Easing.OutQuint);

        protected override void PopOut() => this.FadeOut(120, Easing.OutQuint);
    }
}
