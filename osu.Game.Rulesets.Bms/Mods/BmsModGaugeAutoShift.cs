// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModGaugeAutoShift : Mod, IApplicableHealthProcessor, IPreserveSettingsWhenDisabled
    {
        public override string Name => "Gauge Auto Shift";

        public override string Acronym => "GAS";

        public override LocalisableString Description => BmsModStrings.GaugeAutoShiftDescription;

        public override ModType Type => ModType.DifficultyReduction;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModGauge), typeof(BmsModGaugeAutoShift) };

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.StartingGauge), nameof(BmsModStrings.StartingGaugeDescription))]
        public Bindable<BmsGaugeType> StartingGauge { get; } = new Bindable<BmsGaugeType>(BmsGaugeType.ExHard);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.FloorGauge), nameof(BmsModStrings.FloorGaugeDescription))]
        public Bindable<BmsGaugeType> FloorGauge { get; } = new Bindable<BmsGaugeType>(BmsGaugeType.Easy);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!StartingGauge.IsDefault)
                    yield return (BmsModStrings.StartingGauge, GetStartingGaugeType().GetLocalisableDescription());

                if (!FloorGauge.IsDefault)
                    yield return (BmsModStrings.FloorGauge, GetFloorGaugeType().GetLocalisableDescription());
            }
        }

        public BmsGaugeType GetStartingGaugeType() => StartingGauge.Value;

        public BmsGaugeType GetFloorGaugeType()
            => FloorGauge.Value > GetStartingGaugeType() ? GetStartingGaugeType() : FloorGauge.Value;

        public HealthProcessor? CreateHealthProcessor(double drainStartTime)
            => new BmsGasGaugeProcessor(drainStartTime, GetStartingGaugeType(), GetFloorGaugeType());
    }
}
