// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModLaneCoverTop : BmsModLaneCover
    {
        public override string Name => "Top Lane Cover";

        public override string Acronym => "SUD";

        public override LocalisableString Description => BmsModStrings.TopLaneCoverDescription;

        protected override BmsLaneCoverPosition Position => BmsLaneCoverPosition.Top;
    }
}
