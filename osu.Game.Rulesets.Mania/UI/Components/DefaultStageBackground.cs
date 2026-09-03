// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.UI.Components
{
    public partial class DefaultStageBackground : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider,
                                                  IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        private IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts
            = Array.Empty<ManiaGameplaySkinProgrammaticVisualPart>();

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public event Action GameplaySkinProgrammaticVisualPartsReady = delegate { };

        public DefaultStageBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var stageShell = new Container
            {
                Name = "Stage shell",
                RelativeSizeAxes = Axes.Both,
            };
            var playfieldBackdrop = new Container
            {
                Name = "Playfield backdrop compatibility owner",
                RelativeSizeAxes = Axes.Both,
            };
            var baseplate = new Box
            {
                Name = "Playfield baseplate",
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black
            };
            InternalChildren = new Drawable[]
            {
                stageShell,
                playfieldBackdrop,
                baseplate,
            };
            gameplaySkinProgrammaticVisualParts = Array.AsReadOnly(new[]
            {
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.StageBackground, stageShell),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBackdrop, playfieldBackdrop),
                new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.PlayfieldBaseplate, baseplate),
            });
            GameplaySkinProgrammaticVisualPartsReady();
        }
    }
}
