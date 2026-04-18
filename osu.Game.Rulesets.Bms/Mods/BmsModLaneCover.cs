// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Mods
{
    public abstract class BmsModLaneCover : Mod, IApplicableToDrawableRuleset<HitObject>
    {
        public override IconUsage? Icon => OsuIcon.ModCover;

        public override ModType Type => ModType.Conversion;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        protected abstract BmsLaneCoverPosition Position { get; }

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.CoverValue), nameof(BmsModStrings.CoverValueDescription))]
        public BindableFloat CoverPercent { get; } = new BindableFloat(500)
        {
            MinValue = 0,
            MaxValue = 1000,
            Precision = 1,
            Default = 500,
        };

        public bool AdjustCoverPercent(float delta)
        {
            if (delta == 0)
                return false;

            float nextValue = Math.Clamp(CoverPercent.Value + Math.Sign(delta), CoverPercent.MinValue, CoverPercent.MaxValue);

            if (nextValue == CoverPercent.Value)
                return false;

            CoverPercent.Value = nextValue;
            return true;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<HitObject> drawableRuleset)
        {
            var laneCover = new BmsLaneCover(Position);
            laneCover.CoverPercent.BindTo(CoverPercent);

            if (drawableRuleset.Playfield is BmsPlayfield bmsPlayfield)
                bmsPlayfield.CoverContainer.Add(laneCover);
            else
                drawableRuleset.PlayfieldAdjustmentContainer.Add(laneCover);

            if (drawableRuleset is DrawableBmsRuleset drawableBmsRuleset)
                drawableBmsRuleset.RefreshLaneCoverFocus();
        }
    }
}
