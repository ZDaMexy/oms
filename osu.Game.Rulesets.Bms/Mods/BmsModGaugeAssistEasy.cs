// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeAssistEasy : BmsModGauge
    {
        public override string Name => "Assist Easy Gauge";

        public override string Acronym => "A-EASY";

        public override LocalisableString Description => @"Uses the assist-easy BMS gauge.";

        public override ModType Type => ModType.DifficultyReduction;

        public override BmsGaugeType GaugeType => BmsGaugeType.AssistEasy;
    }
}
