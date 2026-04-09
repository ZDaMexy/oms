// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsLaneCoverDisplay
    {
        void SetFocused(bool isFocused);
    }

    public partial class BmsLaneCover : CompositeDrawable
    {
        public readonly BindableFloat CoverPercent = new BindableFloat();

        public readonly BindableBool IsFocused = new BindableBool();

        public BmsLaneCoverPosition CoverPosition { get; }

        private readonly Container cover;
        private readonly SkinnableLaneCoverDisplay display;

        protected float CoverContainerHeight => cover.Height;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsLaneCoverDisplay)?.FocusEdgeAlpha ?? 0;

        public BmsLaneCover(BmsLaneCoverPosition position)
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;

            CoverPosition = position;

            InternalChild = cover = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = position == BmsLaneCoverPosition.Top ? Anchor.TopCentre : Anchor.BottomCentre,
                Origin = position == BmsLaneCoverPosition.Top ? Anchor.TopCentre : Anchor.BottomCentre,
                Width = 1,
                Height = 0,
                Child = display = new SkinnableLaneCoverDisplay(this, position)
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                }
            };

            CoverPercent.BindValueChanged(_ => updateCoverage(), true);
            IsFocused.BindValueChanged(_ => updateFocusState(), true);
        }

        private void updateCoverage()
        {
            float coverage = Math.Clamp(CoverPercent.Value / 100f, 0, 1);

            cover.Height = coverage;

            updateFocusState();
        }

        private void updateFocusState()
        {
            bool showFocus = CoverPercent.Value > 0 && IsFocused.Value;

            if (display.CurrentDisplay is IBmsLaneCoverDisplay laneCoverDisplay)
                laneCoverDisplay.SetFocused(showFocus);
        }

        private sealed partial class SkinnableLaneCoverDisplay : SkinnableDrawable
        {
            private readonly BmsLaneCover owner;

            public Drawable? CurrentDisplay => Drawable;

            public SkinnableLaneCoverDisplay(BmsLaneCover owner, BmsLaneCoverPosition position)
                : base(new BmsLaneCoverSkinLookup(position), _ => new DefaultBmsLaneCoverDisplay(position))
            {
                this.owner = owner;
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                owner.updateFocusState();
            }
        }
    }

    internal partial class DefaultBmsLaneCoverDisplay : CompositeDrawable, IBmsLaneCoverDisplay
    {
        private readonly BmsLaneCoverPosition position;
        private Box focusEdge = null!;
        private Box focusWash = null!;
        private bool isFocused;

        public float FocusEdgeAlpha => focusEdge?.Alpha ?? 0;

        public DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition position)
        {
            this.position = position;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.92f,
                    Colour = BmsDefaultPlayfieldPalette.LaneCoverFill,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.18f,
                    Alpha = 0.88f,
                    Anchor = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Origin = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Colour = position == BmsLaneCoverPosition.Top
                        ? ColourInfo.GradientVertical(Color4.Transparent, BmsDefaultPlayfieldPalette.LaneCoverShade)
                        : ColourInfo.GradientVertical(BmsDefaultPlayfieldPalette.LaneCoverShade, Color4.Transparent),
                },
                focusWash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.3f,
                    Alpha = 0,
                    Anchor = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Origin = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Colour = position == BmsLaneCoverPosition.Top
                        ? ColourInfo.GradientVertical(Color4.Transparent, BmsDefaultPlayfieldPalette.FocusWash)
                        : ColourInfo.GradientVertical(BmsDefaultPlayfieldPalette.FocusWash, Color4.Transparent),
                },
                focusEdge = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Alpha = 0,
                    Anchor = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Origin = position == BmsLaneCoverPosition.Top ? Anchor.BottomLeft : Anchor.TopLeft,
                    Colour = BmsDefaultPlayfieldPalette.FocusAccent,
                }
            };

            updateFocusState();
        }

        public void SetFocused(bool isFocused)
        {
            this.isFocused = isFocused;
            updateFocusState();
        }

        private void updateFocusState()
        {
            if (focusEdge == null || focusWash == null)
                return;

            focusEdge.Alpha = isFocused ? 1 : 0;
            focusWash.Alpha = isFocused ? 0.24f : 0;
        }
    }

    public enum BmsLaneCoverPosition
    {
        Top,
        Bottom,
    }
}
