// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning.Argon
{
    public partial class ArgonStageBackground : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider
    {
        private readonly IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> gameplaySkinProgrammaticVisualParts;

        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> IManiaGameplaySkinProgrammaticVisualPartProvider.GameplaySkinProgrammaticVisualParts
            => gameplaySkinProgrammaticVisualParts;

        public ArgonStageBackground()
        {
            RelativeSizeAxes = Axes.Both;
            var stageShell = new Container
            {
                Name = "Stage shell compatibility owner",
                RelativeSizeAxes = Axes.Both,
            };
            var playfieldBackdrop = new Container
            {
                Name = "Playfield backdrop compatibility owner",
                RelativeSizeAxes = Axes.Both,
            };
            var baseplate = new Container
            {
                Name = "Playfield baseplate compatibility owner",
                RelativeSizeAxes = Axes.Both,
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
        }
    }
}
