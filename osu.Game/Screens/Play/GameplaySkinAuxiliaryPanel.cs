// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Optional viewer for the exact scene's auxiliary output. It never changes the main playfield or owns a
    /// second clock, publication, texture store or input lane. Pointer events are confined to this panel.
    /// </summary>
    internal sealed partial class GameplaySkinAuxiliaryPanel : CompositeDrawable
    {
        internal const float LOGICAL_HEIGHT = 768;
        internal const float LOGICAL_WIDTH = LOGICAL_HEIGHT * 16 / 9;
        internal const float MAX_SCREEN_WIDTH = 1920;
        internal const float MAX_SCREEN_HEIGHT = 1080;
        private const float header_height = 32;
        private const float header_gap = 6;

        internal GameplaySkinAuxiliaryPresentationState Presentation { get; }
        internal Container NormalMount { get; }
        internal Container ConcentrationMount { get; }
        internal Container Canvas { get; }
        internal Container Viewport { get; }
        private readonly OsuSpriteText buttonText;

        public override bool PropagateNonPositionalInputSubTree => false;

        internal static GameplaySkinAuxiliaryPanel? TryCreate(GameplaySkinSceneRuntimeHost runtime)
        {
            GameplaySkinAuxiliaryDisplayDefinition? definition = runtime.PreparedScene.AuxiliaryDisplay;
            if (definition == null || !definition.TryResolve(runtime.PreparedScene.Snapshot.Context.OptionValues, out GameplaySkinAuxiliaryDisplaySettings settings))
                return null;
            if (!settings.InitiallyOpen)
                return null;
            return new GameplaySkinAuxiliaryPanel(settings, runtime.Layers.Auxiliary, runtime.Layers.AuxiliaryConcentration);
        }

        internal GameplaySkinAuxiliaryPanel(GameplaySkinAuxiliaryDisplaySettings settings, Container normal, Container concentration)
        {
            Presentation = new GameplaySkinAuxiliaryPresentationState(settings);
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-16, 16);
            Width = 480;
            Depth = 1; // Remains behind pause / fail controls in the existing gameplay overlay.

            NormalMount = new Container { RelativeSizeAxes = Axes.Both, Child = normal };
            ConcentrationMount = new Container { RelativeSizeAxes = Axes.Both, Child = concentration };
            Canvas = new Container
            {
                Size = new Vector2(LOGICAL_WIDTH, LOGICAL_HEIGHT),
                Children = new Drawable[] { NormalMount, ConcentrationMount },
            };
            Viewport = new RestoreViewport(RestoreNormalForSession)
            {
                RelativeSizeAxes = Axes.X,
                Y = header_height + header_gap,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Depth = float.MaxValue },
                    Canvas,
                },
            };
            InternalChildren = new Drawable[]
            {
                new OsuClickableContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Action = ToggleOpen,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0.12f, 0.12f, 0.12f, 1), Depth = float.MaxValue },
                        buttonText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.Default.With(size: 16),
                        },
                    },
                },
                Viewport,
            };
            applyPresentation();
            applySize();
        }

        internal void ToggleOpen()
        {
            Presentation.ToggleOpen();
            applyPresentation();
            applySize();
        }

        internal void StartSession()
        {
            Presentation.StartSession();
            applyPresentation();
        }

        internal void SetPlaying(bool playing)
        {
            Presentation.SetPlaying(playing);
            applyPresentation();
        }

        internal void RestoreNormalForSession()
        {
            Presentation.RestoreNormalForSession();
            applyPresentation();
        }

        protected override void Update()
        {
            base.Update();
            if (Parent == null)
                return;
            Vector2 screenOrigin = ToScreenSpace(Vector2.Zero);
            float screenScaleX = (ToScreenSpace(Vector2.UnitX) - screenOrigin).Length;
            float screenScaleY = (ToScreenSpace(Vector2.UnitY) - screenOrigin).Length;
            float screenLimit = Math.Min(screenScaleX > 0 ? MAX_SCREEN_WIDTH / screenScaleX : MAX_SCREEN_WIDTH,
                screenScaleY > 0 ? MAX_SCREEN_HEIGHT * 16 / 9 / screenScaleY : MAX_SCREEN_WIDTH);
            Width = Math.Max(1, Math.Min(Math.Min(640, Parent.DrawWidth * 0.36f), screenLimit));
            applySize();
        }

        private void applySize()
        {
            float height = Width * 9 / 16;
            Viewport.Height = height;
            Canvas.Scale = new Vector2(Width / LOGICAL_WIDTH);
            Height = header_height + (Presentation.IsOpen ? header_gap + height : 0);
        }

        private void applyPresentation()
        {
            Viewport.Alpha = Presentation.IsOpen ? 1 : 0;
            NormalMount.Alpha = Presentation.IsConcentrating ? 0 : 1;
            ConcentrationMount.Alpha = Presentation.IsConcentrating ? 1 : 0;
            float brightness = Presentation.Brightness;
            ConcentrationMount.Colour = new Color4(brightness, brightness, brightness, 1);
            buttonText.Text = Presentation.IsOpen ? "辅助屏 · 收起" : "辅助屏 · 打开";
        }

        private sealed partial class RestoreViewport : Container
        {
            private readonly Action restore;
            public RestoreViewport(Action restore) => this.restore = restore;

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                restore();
                return true;
            }

            protected override bool OnTouchDown(TouchDownEvent e)
            {
                restore();
                return true;
            }
        }
    }

    /// <summary>Pointer restoration lasts for this gameplay session, including pause/break/resume.</summary>
    internal sealed class GameplaySkinAuxiliaryPresentationState
    {
        private readonly bool concentration;
        private bool started;
        private bool playing;
        private bool restored;
        public bool IsOpen { get; private set; }
        public float Brightness { get; }
        public bool IsConcentrating => started && playing && concentration && !restored;

        public GameplaySkinAuxiliaryPresentationState(GameplaySkinAuxiliaryDisplaySettings settings)
        {
            IsOpen = settings.InitiallyOpen;
            concentration = settings.Concentration;
            Brightness = settings.Brightness;
        }

        public void ToggleOpen() => IsOpen = !IsOpen;
        public void SetPlaying(bool value) => playing = value;
        public void StartSession()
        {
            started = true;
            restored = false;
        }

        public void RestoreNormalForSession() => restored = true;
    }
}
