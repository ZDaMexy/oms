// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModLift : Mod, IApplicableToDrawableRuleset<HitObject>
    {
        public override IconUsage? Icon => OsuIcon.ModCover;

        public override ModType Type => ModType.Conversion;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override string Name => "Lift";

        public override string Acronym => "LIFT";

        public override LocalisableString Description => BmsModStrings.LiftDescription;

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.LiftValue), nameof(BmsModStrings.LiftValueDescription))]
        public BindableFloat LiftUnits { get; } = new BindableFloat(250)
        {
            MinValue = 0,
            MaxValue = 1000,
            Precision = 1,
            Default = 250,
        };

        public bool AdjustLiftUnits(float delta)
        {
            if (delta == 0)
                return false;

            float nextValue = Math.Clamp(LiftUnits.Value + Math.Sign(delta), LiftUnits.MinValue, LiftUnits.MaxValue);

            if (nextValue == LiftUnits.Value)
                return false;

            LiftUnits.Value = nextValue;
            return true;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<HitObject> drawableRuleset)
        {
            if (drawableRuleset.Playfield is BmsPlayfield bmsPlayfield)
                bmsPlayfield.LiftUnits.BindTo(LiftUnits);
        }
    }
}
