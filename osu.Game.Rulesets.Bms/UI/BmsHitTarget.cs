// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsHitTargetDisplay
    {
        void SetPressed(bool isPressed);

        void SetFocused(bool isFocused);
    }

    public partial class BmsHitTarget : CompositeDrawable
    {
        public readonly BindableBool IsPressed = new BindableBool();

        public readonly BindableBool IsFocused = new BindableBool();

        protected float PressedOverlayAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.PressedOverlayAlpha ?? 0;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.FocusEdgeAlpha ?? 0;

        private readonly BmsPlayfieldLayoutProfile layoutProfile;
        private readonly SkinnableHitTargetDisplay display;

        public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public BmsHitTarget(BmsLaneSkinLookup lookup, BmsPlayfieldLayoutProfile layoutProfile, BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            this.layoutProfile = layoutProfile;
            LayoutSnapshot = layoutSnapshot;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            if (layoutSnapshot == null)
            {
                // Explicit compatibility-only construction retains the historical local pixel metric.
                RelativeSizeAxes = Axes.X;
                Height = layoutProfile.HitTargetHeight;
            }
            else
            {
                // Production geometry is projected exclusively from the exact immutable snapshot. In particular this
                // keeps the renderer and the neutral surface identical when DPI scaling changes the solved height.
                RelativeSizeAxes = Axes.Both;
                Height = layoutSnapshot.HitTargetRect.Height / layoutSnapshot.PlayfieldRect.Height;
            }

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

        private sealed partial class SkinnableHitTargetDisplay : SkinnableDrawable
        {
            private readonly BmsHitTarget owner;

            public Drawable? CurrentDisplay => Drawable;

            public SkinnableHitTargetDisplay(BmsHitTarget owner, BmsLaneSkinLookup lookup)
                : base(lookup, _ => new DefaultBmsHitTargetDisplay(lookup.IsScratch, lookup.Keymode, owner.layoutProfile, owner.LayoutSnapshot))
            {
                this.owner = owner;
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);

                owner.updateState();
            }
        }
    }

    internal partial class DefaultBmsHitTargetDisplay : CompositeDrawable, IBmsHitTargetDisplay
    {
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        private Box bar = null!;
        private Container line = null!;
        private Box lineFill = null!;
        private Box pressedOverlay = null!;
        private Box focusEdge = null!;
        private Sprite? textureBase;
        private bool isPressed;
        private bool isFocused;
        private Color4 glowColour;

        public float PressedOverlayAlpha => pressedOverlay?.Alpha ?? 0;

        public float FocusEdgeAlpha => focusEdge?.Alpha ?? 0;

        internal float BarHeight => bar?.Height ?? 0;

        internal float LineHeight => line?.Height ?? 0;

        internal float LineDrawHeight => line?.DrawHeight ?? 0;

        internal float LineScreenSpaceHeight => line?.ScreenSpaceDrawQuad.Height ?? 0;

        internal float LineScreenSpaceTop => line?.ScreenSpaceDrawQuad.TopLeft.Y ?? 0;

        internal float FocusEdgeHeight => focusEdge?.Height ?? 0;

        internal float GlowRadius { get; private set; }

        public DefaultBmsHitTargetDisplay(
            bool isScratch,
            BmsKeymode keymode,
            BmsPlayfieldLayoutProfile layoutProfile,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            this.isScratch = isScratch;
            this.keymode = keymode;
            RelativeSizeAxes = Axes.Both;

            var barColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetBar : BmsDefaultPlayfieldPalette.HitTargetBar;
            var lineColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetLine : BmsDefaultPlayfieldPalette.HitTargetLine;
            glowColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetGlow : BmsDefaultPlayfieldPalette.HitTargetGlow;

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
                    Child = lineFill = new Box
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

            initialiseLayout(layoutProfile, layoutSnapshot);
            updateState();
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Colour overrides drive the programmatic bar / line / glow; a texture (below) instead owns the look.
            var configuredBar = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetBarColour : BmsSkinConfigurationLookups.HitTargetBarColour, keymode)?.Value;
            if (configuredBar.HasValue)
                bar.Colour = configuredBar.Value;

            var configuredLine = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetLineColour : BmsSkinConfigurationLookups.HitTargetLineColour, keymode)?.Value;
            if (configuredLine.HasValue)
                lineFill.Colour = configuredLine.Value;

            var configuredGlow = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetGlowColour : BmsSkinConfigurationLookups.HitTargetGlowColour, keymode)?.Value;
            if (configuredGlow.HasValue)
            {
                glowColour = configuredGlow.Value;
                pressedOverlay.Colour = glowColour;
                applyGlow();
            }

            // Texture override: a HitTargetImage owns the static look — hide the programmatic bar / line; the press and
            // focus overlays still draw on top (Depth keeps the texture behind them).
            string? imagePath = skin.GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.HitTargetImage, keymode)?.Value;
            var texture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            if (texture != null)
            {
                bar.Alpha = 0;
                line.Alpha = 0;
                AddInternal(textureBase = new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture, Depth = 1 });
            }
        }

        private void initialiseLayout(BmsPlayfieldLayoutProfile layoutProfile, BmsGameplayLayoutSnapshot? layoutSnapshot)
        {
            GlowRadius = layoutProfile.HitTargetGlowRadius;

            if (layoutSnapshot == null)
            {
                // Explicit isolated compatibility displays retain their historical pixel-sized metrics.
                bar.Height = layoutProfile.HitTargetBarHeight;
                line.Height = layoutProfile.HitTargetLineHeight;
                focusEdge.Height = layoutProfile.HitTargetLineHeight;
            }
            else
            {
                GameplaySkinLayoutRect targetRect = layoutSnapshot.HitTargetRect;
                GameplaySkinLayoutRect lineRect = layoutSnapshot.JudgementLineRect;
                bool reverse = layoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up;
                Anchor lineAnchor = reverse ? Anchor.TopLeft : Anchor.BottomLeft;

                // The outer target owns the exact target surface. Its children use ratios of that same surface so no
                // profile pixel metric can diverge from the neutral publication at DPI 1/2 (or any later scale).
                bar.RelativeSizeAxes = Axes.Both;
                bar.Height = Math.Clamp(layoutProfile.HitTargetBarHeight / layoutProfile.HitTargetHeight, 0, 1);
                bar.Anchor = bar.Origin = lineAnchor;

                line.RelativeSizeAxes = Axes.Both;
                line.Height = Math.Clamp(lineRect.Height / targetRect.Height, 0, 1);
                line.Anchor = line.Origin = lineAnchor;

                focusEdge.RelativeSizeAxes = Axes.Both;
                focusEdge.Height = line.Height;
                focusEdge.Anchor = focusEdge.Origin = lineAnchor;
            }

            applyGlow();
        }

        // Rebuilds the line's glow edge effect from the current radius + (possibly skin-overridden) glow colour. Called
        // from both layout changes (radius) and the skin load (colour) so a later layout change keeps the configured colour.
        private void applyGlow()
            => line.EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Radius = GlowRadius,
                Colour = glowColour,
            };

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
