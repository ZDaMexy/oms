// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public abstract class BmsModLongNoteMode : Mod
    {
        public override ModType Type => ModType.Conversion;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModChargeNote), typeof(BmsModHellChargeNote) };

        public abstract BmsLongNoteMode LongNoteMode { get; }
    }
}
