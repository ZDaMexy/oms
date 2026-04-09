// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeHazard : BmsModGauge
    {
        public override string Name => "Hazard Gauge";

        public override string Acronym => "HAZARD";

        public override LocalisableString Description => @"Uses the hazard BMS survival gauge.";

        public override ModType Type => ModType.DifficultyIncrease;

        public override BmsGaugeType GaugeType => BmsGaugeType.Hazard;
    }
}
