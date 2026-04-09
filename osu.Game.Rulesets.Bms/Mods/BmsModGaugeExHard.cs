// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeExHard : BmsModGauge
    {
        public override string Name => "EX-HARD Gauge";

        public override string Acronym => "EX-HARD";

        public override LocalisableString Description => @"Uses the EX-HARD BMS survival gauge.";

        public override ModType Type => ModType.DifficultyIncrease;

        public override BmsGaugeType GaugeType => BmsGaugeType.ExHard;
    }
}
