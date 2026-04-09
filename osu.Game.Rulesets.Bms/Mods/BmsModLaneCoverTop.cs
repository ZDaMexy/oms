// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModLaneCoverTop : BmsModLaneCover
    {
        public override string Name => "Top Lane Cover";

        public override string Acronym => "SUD";

        public override LocalisableString Description => @"Covers the top portion of the playfield.";

        protected override BmsLaneCoverPosition Position => BmsLaneCoverPosition.Top;
    }
}
