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

            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            Color4 defaultColour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);

            string? texturePath = skinSource.GetBmsSkinConfig<string>(
                BmsSkinConfigurationLookups.KeyFlashImage, keymode, laneIndex, isScratch)?.Value;

            Color4 colour = skinSource.GetBmsSkinConfig<Color4>(
                BmsSkinConfigurationLookups.KeyFlashColour, keymode)?.Value
                ?? defaultColour;

            if (!string.IsNullOrEmpty(texturePath))
            {
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                    Texture = skinSource.GetTexture(texturePath),
                };
            }
            else
            {
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour.Opacity(0.4f),
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Parent chain: DefaultBmsKeyFlashDisplay → SkinnableDrawable → BmsLane
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
