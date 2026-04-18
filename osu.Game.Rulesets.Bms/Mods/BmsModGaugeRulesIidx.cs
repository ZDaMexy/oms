// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeRulesIidx : BmsModGaugeRules
    {
        public override string Name => "IIDX Gauge";

        public override string Acronym => "IIDXG";

        public override LocalisableString Description => BmsModStrings.GaugeRulesIidxDescription;

        public override BmsGaugeRulesFamily GaugeRulesFamily => BmsGaugeRulesFamily.IIDX;
    }
}
