// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsHitTargetDisplay
    {
        void SetPressed(bool isPressed);

        void SetFocused(bool isFocused);
    }

    public interface IBmsHitTargetLayoutDisplay
    {
        void ApplyLayoutProfile(BmsPlayfieldLayoutProfile layoutProfile);
    }

    public partial class BmsHitTarget : CompositeDrawable
    {
        public readonly BindableBool IsPressed = new BindableBool();

        public readonly BindableBool IsFocused = new BindableBool();

        protected float PressedOverlayAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.PressedOverlayAlpha ?? 0;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.FocusEdgeAlpha ?? 0;

        private BmsPlayfieldLayoutProfile currentLayoutProfile;
        private readonly SkinnableHitTargetDisplay display;

        public BmsHitTarget(BmsLaneSkinLookup lookup, BmsPlayfieldLayoutProfile layoutProfile)
        {
            currentLayoutProfile = layoutProfile;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Height = layoutProfile.HitTargetHeight;

            InternalChild = display = new SkinnableHitTargetDisplay(this, lookup)
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            };

            IsPressed.BindValueChanged(_ => updateState(), true);
            IsFocused.BindValueChanged(_ => updateState(), true);
        }

        private void updateState()
        {
            if (display.CurrentDisplay is not IBmsHitTargetDisplay hitTargetDisplay)
                return;

            hitTargetDisplay.SetPressed(IsPressed.Value);
            hitTargetDisplay.SetFocused(IsFocused.Value);
        }

        public void ApplyLayoutProfile(BmsPlayfieldLayoutProfile layoutProfile)
        {
            currentLayoutProfile = layoutProfile;
            Height = layoutProfile.HitTargetHeight;

            if (display.CurrentDisplay is IBmsHitTargetLayoutDisplay layoutDisplay)
                layoutDisplay.ApplyLayoutProfile(layoutProfile);

            updateState();
        }

        private sealed partial class SkinnableHitTargetDisplay : SkinnableDrawable
        {
            private readonly BmsHitTarget owner;

            public Drawable? CurrentDisplay => Drawable;

            public SkinnableHitTargetDisplay(BmsHitTarget owner, BmsLaneSkinLookup lookup)
                : base(lookup, _ => new DefaultBmsHitTargetDisplay(lookup.IsScratch, owner.currentLayoutProfile))
            {
                this.owner = owner;
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);

                if (Drawable is IBmsHitTargetLayoutDisplay layoutDisplay)
                    layoutDisplay.ApplyLayoutProfile(owner.currentLayoutProfile);

                owner.updateState();
            }
        }
    }

    internal partial class DefaultBmsHitTargetDisplay : CompositeDrawable, IBmsHitTargetDisplay, IBmsHitTargetLayoutDisplay
    {
        private readonly bool isScratch;

        private Box bar = null!;
        private Container line = null!;
        private Box pressedOverlay = null!;
        private Box focusEdge = null!;
        private bool isPressed;
        private bool isFocused;
        private float glowRadius;

        public float PressedOverlayAlpha => pressedOverlay?.Alpha ?? 0;

        public float FocusEdgeAlpha => focusEdge?.Alpha ?? 0;

        internal float BarHeight => bar?.Height ?? 0;

        internal float LineHeight => line?.Height ?? 0;

        internal float FocusEdgeHeight => focusEdge?.Height ?? 0;

        internal float GlowRadius => glowRadius;

        public DefaultBmsHitTargetDisplay(bool isScratch, BmsPlayfieldLayoutProfile layoutProfile)
        {
            this.isScratch = isScratch;
            RelativeSizeAxes = Axes.Both;

            var barColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetBar : BmsDefaultPlayfieldPalette.HitTargetBar;
            var lineColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetLine : BmsDefaultPlayfieldPalette.HitTargetLine;
            var glowColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetGlow : BmsDefaultPlayfieldPalette.HitTargetGlow;

            InternalChildren = new Drawable[]
            {
                bar = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Colour = barColour,
                },
                line = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = lineColour,
                    }
                },
                pressedOverlay = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Blending = BlendingParameters.Additive,
                    Colour = glowColour,
                },
                focusEdge = new Box
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Alpha = 0,
                    Colour = BmsDefaultPlayfieldPalette.FocusAccent,
                }
            };

            ApplyLayoutProfile(layoutProfile);
            updateState();
        }

        public void ApplyLayoutProfile(BmsPlayfieldLayoutProfile layoutProfile)
        {
            glowRadius = layoutProfile.HitTargetGlowRadius;
            bar.Height = layoutProfile.HitTargetBarHeight;
            line.Height = layoutProfile.HitTargetLineHeight;
            line.EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Radius = glowRadius,
                Colour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetGlow : BmsDefaultPlayfieldPalette.HitTargetGlow,
            };
            focusEdge.Height = layoutProfile.HitTargetLineHeight;
        }

        public void SetPressed(bool isPressed)
        {
            this.isPressed = isPressed;
            updateState();
        }

        public void SetFocused(bool isFocused)
        {
            this.isFocused = isFocused;
            updateState();
        }

        private void updateState()
        {
            if (pressedOverlay == null || focusEdge == null)
                return;

            pressedOverlay.Alpha = isPressed ? 0.18f : 0;
            focusEdge.Alpha = isFocused ? 1 : 0;
        }
    }
}
