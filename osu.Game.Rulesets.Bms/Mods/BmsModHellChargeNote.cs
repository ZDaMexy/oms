// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModHellChargeNote : BmsModLongNoteMode
    {
        public override string Name => "Hell Charge Note";

        public override string Acronym => "HCN";

        public override LocalisableString Description => BmsModStrings.HellChargeNoteDescription;

        public override BmsLongNoteMode LongNoteMode => BmsLongNoteMode.HCN;
    }
}
