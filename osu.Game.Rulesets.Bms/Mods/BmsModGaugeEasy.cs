// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeEasy : BmsModGauge
    {
        public override string Name => "Easy Gauge";

        public override string Acronym => "EASY";

        public override LocalisableString Description => @"Uses the easy BMS gauge.";

        public override ModType Type => ModType.DifficultyReduction;

        public override BmsGaugeType GaugeType => BmsGaugeType.Easy;
    }
}
