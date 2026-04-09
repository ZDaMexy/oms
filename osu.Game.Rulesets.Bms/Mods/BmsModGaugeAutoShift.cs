// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeAutoShift : Mod, IApplicableHealthProcessor
    {
        public override string Name => "Gauge Auto Shift";

        public override string Acronym => "GAS";

        public override LocalisableString Description => @"Downgrades to a lower gauge instead of failing immediately.";

        public override ModType Type => ModType.DifficultyIncrease;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModGauge), typeof(BmsModGaugeAutoShift) };

        [SettingSource("Starting gauge", "The highest gauge tier to begin the chart on.")]
        public Bindable<BmsGaugeType> StartingGauge { get; } = new Bindable<BmsGaugeType>(BmsGaugeType.ExHard);

        [SettingSource("Floor gauge", "The lowest gauge tier GAS may downgrade to.")]
        public Bindable<BmsGaugeType> FloorGauge { get; } = new Bindable<BmsGaugeType>(BmsGaugeType.Easy);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!StartingGauge.IsDefault)
                    yield return ("Starting gauge", GetStartingGaugeType().GetDisplayName());

                if (!FloorGauge.IsDefault)
                    yield return ("Floor gauge", GetFloorGaugeType().GetDisplayName());
            }
        }

        public BmsGaugeType GetStartingGaugeType() => StartingGauge.Value;

        public BmsGaugeType GetFloorGaugeType()
            => FloorGauge.Value > GetStartingGaugeType() ? GetStartingGaugeType() : FloorGauge.Value;

        public HealthProcessor? CreateHealthProcessor(double drainStartTime)
            => new BmsGasGaugeProcessor(drainStartTime, GetStartingGaugeType(), GetFloorGaugeType());
    }
}
