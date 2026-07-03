// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DefaultBmsKeyFlashDisplay : CompositeDrawable
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        private IBindable<bool>? pressedSource;

        public DefaultBmsKeyFlashDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            this.laneIndex = laneIndex;
            this.isScratch = isScratch;
            this.keymode = keymode;

            // Occupies the bottom half of the lane, centred horizontally.
            RelativeSizeAxes = Axes.X;
            Height = 0.5f;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            Color4 defaultColour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);

            string? texturePath = skinSource.GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.KeyFlashImage, keymode, laneIndex, isScratch)?.Value;

            if (!string.IsNullOrEmpty(texturePath))
            {
                // File-skin path: a full-lane sprite (the texture itself provides edge fade / shape).
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Stretch,
                    Texture = skinSource.GetTexture(texturePath),
                };
            }
            else
            {
                // Programmatic default: layered vertical strips at each lane edge, stepping from
                // wide-dim → narrow-bright, giving a rough glow-fade from edge to centre.
                Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                    BmsSkinConfigurationLookups.KeyFlashColour, keymode)?.Value
                    ?? defaultColour;

                InternalChildren = new Drawable[]
                {
                    // Left edge — three stacked strips, inner to outer
                    new Box { RelativeSizeAxes = Axes.Y, Width = 4, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.6f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 10, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.2f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 20, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Colour = colour.Opacity(0.06f) },
                    // Right edge
                    new Box { RelativeSizeAxes = Axes.Y, Width = 4, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.6f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 10, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.2f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 20, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Colour = colour.Opacity(0.06f) },
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var lane = Parent?.Parent as BmsLane;

            if (lane?.HitTarget == null)
                return;

            pressedSource = lane.HitTarget.IsPressed.GetBoundCopy();
            pressedSource.BindValueChanged(e => SetPressed(e.NewValue), true);
        }

        public void SetPressed(bool pressed)
        {
            if (pressed)
                this.FadeIn(40, Easing.OutQuint);
            else
                this.FadeOut(250, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            pressedSource?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
