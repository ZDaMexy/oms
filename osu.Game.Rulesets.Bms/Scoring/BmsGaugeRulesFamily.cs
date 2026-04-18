// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsGaugeRulesFamily
    {
        Legacy,
        Beatoraja,
        LR2,
        IIDX,
    }

    public static class BmsGaugeRulesFamilyExtensions
    {
        public static string GetDisplayName(this BmsGaugeRulesFamily gaugeRulesFamily)
            => gaugeRulesFamily switch
            {
                BmsGaugeRulesFamily.Legacy => "OMS LEGACY",
                BmsGaugeRulesFamily.Beatoraja => "BEATORAJA",
                BmsGaugeRulesFamily.LR2 => "LR2",
                BmsGaugeRulesFamily.IIDX => "IIDX",
                _ => gaugeRulesFamily.ToString().ToUpperInvariant(),
            };
    }
}
