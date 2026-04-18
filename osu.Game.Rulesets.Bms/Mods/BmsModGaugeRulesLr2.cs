// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeRulesLr2 : BmsModGaugeRules
    {
        public override string Name => "LR2 Gauge";

        public override string Acronym => "LR2G";

        public override LocalisableString Description => BmsModStrings.GaugeRulesLr2Description;

        public override BmsGaugeRulesFamily GaugeRulesFamily => BmsGaugeRulesFamily.LR2;
    }
}
