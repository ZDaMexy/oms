// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
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

        public readonly BindableFloat CoverOpacity = new BindableFloat(1000)
        {
            MinValue = 0,
            MaxValue = 1000,
            Precision = 1,
            Default = 1000,
        };

        public readonly BindableBool IsFocused = new BindableBool();

        public BmsLaneCoverPosition CoverPosition { get; }

        private readonly Container cover;
        private readonly SkinnableLaneCoverDisplay display;

        [Resolved]
        private BmsGameplayLayoutProvider layoutProvider { get; set; } = null!;

        internal BmsGameplayLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        protected float CoverContainerHeight => cover.Height;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsLaneCoverDisplay)?.FocusEdgeAlpha ?? 0;

        protected float CoverDisplayAlpha => display.Alpha;

        public BmsLaneCover(BmsLaneCoverPosition position)
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;

            CoverPosition = position;

            InternalChild = cover = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = position == BmsLaneCoverPosition.Sudden ? Anchor.TopCentre : Anchor.BottomCentre,
                Origin = position == BmsLaneCoverPosition.Sudden ? Anchor.TopCentre : Anchor.BottomCentre,
                Width = 1,
                Height = 0,
                Child = display = new SkinnableLaneCoverDisplay(this, position)
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                }
            };

            CoverPercent.BindValueChanged(_ => updateCoverage(), true);
            CoverOpacity.BindValueChanged(_ => updateOpacity(), true);
            IsFocused.BindValueChanged(_ => updateFocusState(), true);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            LayoutSnapshot = layoutProvider.Current;
        }

        private void updateCoverage()
        {
            float coverage = Math.Clamp(CoverPercent.Value / 1000f, 0, 1);

            cover.Height = coverage;

            updateFocusState();
        }

        private void updateOpacity()
            => display.Alpha = Math.Clamp(CoverOpacity.Value / 1000f, 0, 1);

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
        private readonly BmsKeymode? isolatedKeymode;
        private Box focusEdge = null!;
        private Box focusWash = null!;
        private bool isFocused;

        public float FocusEdgeAlpha => focusEdge?.Alpha ?? 0;

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        public DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition position)
        {
            this.position = position;
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Creates an isolated skin-component preview with an explicit keymode authority. Production gameplay uses the
        /// gameplay-root <see cref="BmsGameplayLayoutProvider"/> resolved by the parameterless overload.
        /// </summary>
        internal DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition position, BmsKeymode isolatedKeymode)
            : this(position)
        {
            this.isolatedKeymode = isolatedKeymode;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            BmsKeymode? keymodeAuthority = layoutProvider?.Current.Keymode ?? isolatedKeymode;

            // A detached component has no parser-owned authority. Keep it inert rather than inventing a 7K surface.
            if (!keymodeAuthority.HasValue)
            {
                InternalChild = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 };
                return;
            }

            BmsKeymode keymode = keymodeAuthority.Value;

            var fillColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverFillColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.LaneCoverFill;
            var shadeColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverShadeColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.LaneCoverShade;
            var focusColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverFocusColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.FocusAccent;

            bool isSudden = position == BmsLaneCoverPosition.Sudden;
            var edgeAnchor = isSudden ? Anchor.BottomLeft : Anchor.TopLeft;

            // Texture base: LaneCoverTopImage for Sudden (covers from the top), LaneCoverBottomImage for Hidden (from the bottom).
            string? imagePath = skin.GetBmsSkinConfig<string>(isSudden ? BmsSkinConfigurationLookups.LaneCoverTopImage : BmsSkinConfigurationLookups.LaneCoverBottomImage, keymode)?.Value;
            var texture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            var children = new List<Drawable>
            {
                texture != null
                    ? new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture }
                    : new Box { RelativeSizeAxes = Axes.Both, Alpha = 1, Colour = fillColour },
            };

            // The programmatic shade gradient only applies to the box fallback; a texture owns its own look.
            if (texture == null)
            {
                children.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.18f,
                    Alpha = 0.88f,
                    Anchor = edgeAnchor,
                    Origin = edgeAnchor,
                    Colour = isSudden
                        ? ColourInfo.GradientVertical(Color4.Transparent, shadeColour)
                        : ColourInfo.GradientVertical(shadeColour, Color4.Transparent),
                });
            }

            children.Add(focusWash = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Height = 0.3f,
                Alpha = 0,
                Anchor = edgeAnchor,
                Origin = edgeAnchor,
                Colour = isSudden
                    ? ColourInfo.GradientVertical(Color4.Transparent, BmsDefaultPlayfieldPalette.FocusWash)
                    : ColourInfo.GradientVertical(BmsDefaultPlayfieldPalette.FocusWash, Color4.Transparent),
            });

            children.Add(focusEdge = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Alpha = 0,
                Anchor = edgeAnchor,
                Origin = edgeAnchor,
                Colour = focusColour,
            });

            InternalChildren = children.ToArray();

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
        Sudden,
        Hidden,
    }
}
