// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsStageBackground : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                              IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        private Drawable? leftSprite;
        private Drawable? rightSprite;
        private Drawable? playfieldBackdrop;
        private Drawable? baseplate;

        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
            = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

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
                baseplate = new Container
                {
                    Name = "Playfield baseplate compatibility owner",
                    RelativeSizeAxes = Axes.Both,
                },
                playfieldBackdrop = new Container
                {
                    Name = "Playfield backdrop compatibility owner",
                    RelativeSizeAxes = Axes.Both,
                },
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
            gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.StageBackground, leftSprite),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.StageBackground, rightSprite),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBackdrop, playfieldBackdrop),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBaseplate, baseplate),
            });
            GameplaySkinProgrammaticVisualPartsReady();
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
