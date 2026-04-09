// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeHard : BmsModGauge
    {
        public override string Name => "Hard Gauge";

        public override string Acronym => "HARD";

        public override LocalisableString Description => @"Uses the hard BMS survival gauge.";

        public override ModType Type => ModType.DifficultyIncrease;

        public override BmsGaugeType GaugeType => BmsGaugeType.Hard;
    }
}
