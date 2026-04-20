// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModSudden : BmsModLaneCover
    {
        public override string Name => "Sudden";

        public override string Acronym => "SUD";

        public override LocalisableString Description => BmsModStrings.SuddenDescription;

        protected override BmsLaneCoverPosition Position => BmsLaneCoverPosition.Sudden;
    }
}