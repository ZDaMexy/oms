// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsBarLine : CompositeDrawable
    {
        private const float major_height_multiplier = 1.2f;
        private const float minor_height_multiplier = 0.9f;
        private const float minor_alpha = 0.65f;

        private IBindable<bool> major = null!;
        private Box line = null!;
        private float configuredHeight;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, DrawableHitObject drawableHitObject)
        {
            configuredHeight = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.BarLineHeight)?.Value ?? 1;

            RelativeSizeAxes = Axes.X;
            Colour = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.BarLineColour)?.Value ?? Color4.White;

            var edgeSmoothness = new Vector2(0.3f);

            AddInternal(line = new Box
            {
                Name = "Bar line",
                EdgeSmoothness = edgeSmoothness,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.Both,
            });

            major = ((DrawableBarLine)drawableHitObject).Major.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            major.BindValueChanged(updateMajor, true);
        }

        private void updateMajor(ValueChangedEvent<bool> isMajor)
        {
            Height = configuredHeight * (isMajor.NewValue ? major_height_multiplier : minor_height_multiplier);
            line.Alpha = isMajor.NewValue ? 1f : minor_alpha;
        }
    }
}
