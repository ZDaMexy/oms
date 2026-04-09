// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModChargeNote : BmsModLongNoteMode
    {
        public override string Name => "Charge Note";

        public override string Acronym => "CN";

        public override LocalisableString Description => @"Uses charge-note long note timing.";

        public override BmsLongNoteMode LongNoteMode => BmsLongNoteMode.CN;
    }
}
