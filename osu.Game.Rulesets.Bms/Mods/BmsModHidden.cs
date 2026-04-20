// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModHidden : BmsModLaneCover
    {
        public override string Name => "Hidden";

        public override string Acronym => "HID";

        public override LocalisableString Description => BmsModStrings.HiddenDescription;

        protected override BmsLaneCoverPosition Position => BmsLaneCoverPosition.Hidden;
    }
}