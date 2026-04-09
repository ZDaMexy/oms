// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public abstract class BmsModGauge : Mod, IApplicableHealthProcessor
    {
        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModGauge), typeof(BmsModGaugeAutoShift) };

        public abstract BmsGaugeType GaugeType { get; }

        public HealthProcessor? CreateHealthProcessor(double drainStartTime) => new BmsGaugeProcessor(drainStartTime, GaugeType);
    }
}
