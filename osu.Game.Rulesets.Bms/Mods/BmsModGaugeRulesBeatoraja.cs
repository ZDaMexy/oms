// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeRulesBeatoraja : BmsModGaugeRules
    {
        public override string Name => "beatoraja Gauge";

        public override string Acronym => "BRG";

        public override LocalisableString Description => BmsModStrings.GaugeRulesBeatorajaDescription;

        public override BmsGaugeRulesFamily GaugeRulesFamily => BmsGaugeRulesFamily.Beatoraja;
    }
}
