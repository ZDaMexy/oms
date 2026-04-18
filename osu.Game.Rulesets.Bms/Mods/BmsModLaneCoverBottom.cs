// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModLaneCoverBottom : BmsModLaneCover
    {
        public override string Name => "Bottom Lane Cover";

        public override string Acronym => "HID";

        public override LocalisableString Description => BmsModStrings.BottomLaneCoverDescription;

        protected override BmsLaneCoverPosition Position => BmsLaneCoverPosition.Bottom;
    }
}
