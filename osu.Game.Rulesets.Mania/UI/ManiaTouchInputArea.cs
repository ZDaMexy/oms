// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// An overlay that captures and displays osu!mania mouse and touch input.
    /// </summary>
    public partial class ManiaTouchInputArea : VisibilityContainer
    {
        private readonly DrawableManiaRuleset drawableRuleset;

        // visibility state affects our child. we always want to handle input.
        public override bool PropagatePositionalInputSubTree => true;
        public override bool PropagateNonPositionalInputSubTree => true;

        [SettingSource("Spacing", "The spacing between receptors.")]
        public BindableFloat Spacing { get; } = new BindableFloat(10)
        {
            Precision = 1,
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Opacity", "The receptor opacity.")]
        public BindableFloat Opacity { get; } = new BindableFloat(1)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 1
        };

        private Container gridContainer = null!;

        public ManiaTouchInputArea(DrawableManiaRuleset drawableRuleset)
        {
            this.drawableRuleset = drawableRuleset;

            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            RelativeSizeAxes = Axes.Both;
            Height = 0.5f;
        }

        [BackgroundDependencyLoader]
        private void load(GameplaySkinLayoutSnapshot snapshot, GameplaySkinLayoutRevisionOwner owner)
        {
            ManiaGameplaySkinLayout.ValidateConsumerCarrier(snapshot, owner, "touch overlay");

            if (!ReferenceEquals(snapshot, drawableRuleset.LayoutSnapshot))
                throw new System.InvalidOperationException("The mania touch overlay must retain the exact production layout snapshot.");

            InternalChild = gridContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                AlwaysPresent = true,
            };

            GameplaySkinLayoutRect screen = snapshot.Context.ScreenBounds;

            foreach (GameplaySkinLayoutLane lane in snapshot.LanesInLogicalOrder)
            {
                Column column = drawableRuleset.Playfield.GetColumn(lane.TopologyEntry.GlobalLogicalIndex);
                gridContainer.Add(new ColumnInputReceptor
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativePositionAxes = Axes.Both,
                    RelativeSizeAxes = Axes.Both,
                    Position = new Vector2((lane.Rect.X - screen.X) / screen.Width, 0),
                    Size = new Vector2(lane.Rect.Width / screen.Width, 1),
                    Action = { BindTarget = column.Action },
                    Spacing = { BindTarget = Spacing },
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Opacity.BindValueChanged(o => Alpha = o.NewValue, true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Hide whenever the keyboard is used.
            Hide();
            return false;
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            Show();
            return true;
        }

        protected override void PopIn()
        {
            gridContainer.FadeIn(500, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            gridContainer.FadeOut(300);
        }

        public partial class ColumnInputReceptor : CompositeDrawable
        {
            public readonly IBindable<ManiaAction> Action = new Bindable<ManiaAction>();
            public readonly IBindable<float> Spacing = new BindableFloat();

            private readonly Box highlightOverlay;

            [Resolved]
            private ManiaInputManager? inputManager { get; set; }

            private bool isPressed;

            public ColumnInputReceptor()
            {
                RelativeSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 10,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0.15f,
                            },
                            highlightOverlay = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                Blending = BlendingParameters.Additive,
                            }
                        }
                    }
                };
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                // Extend input coverage to the gaps close to this receptor.
                => DrawRectangle.Inflate(new Vector2(Spacing.Value / 2, 0)).Contains(ToLocalSpace(screenSpacePos));

            protected override bool OnTouchDown(TouchDownEvent e)
            {
                updateButton(true);
                return false; // handled by parent container to show overlay.
            }

            protected override void OnTouchUp(TouchUpEvent e)
            {
                updateButton(false);
            }

            private void updateButton(bool press)
            {
                if (press == isPressed)
                    return;

                isPressed = press;

                if (press)
                {
                    inputManager?.KeyBindingContainer.TriggerPressed(Action.Value);
                    highlightOverlay.FadeTo(0.1f, 80, Easing.OutQuint);
                }
                else
                {
                    inputManager?.KeyBindingContainer.TriggerReleased(Action.Value);
                    highlightOverlay.FadeTo(0, 400, Easing.OutQuint);
                }
            }
        }
    }
}
