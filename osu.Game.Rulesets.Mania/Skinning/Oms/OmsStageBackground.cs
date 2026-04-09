// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsStageBackground : CompositeDrawable
    {
        private Drawable? leftSprite;
        private Drawable? rightSprite;

        public OmsStageBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            string leftImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.LeftStageImage)?.Value
                               ?? "mania-stage-left";

            string rightImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.RightStageImage)?.Value
                                ?? "mania-stage-right";

            InternalChildren = new Drawable[]
            {
                leftSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopRight,
                    X = 0.05f,
                    Texture = skin.GetTexture(leftImage),
                },
                rightSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    X = -0.05f,
                    Texture = skin.GetTexture(rightImage),
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            if (leftSprite?.Height > 0)
                leftSprite.Scale = new Vector2(1, DrawHeight / leftSprite.Height);

            if (rightSprite?.Height > 0)
                rightSprite.Scale = new Vector2(1, DrawHeight / rightSprite.Height);
        }
    }
}