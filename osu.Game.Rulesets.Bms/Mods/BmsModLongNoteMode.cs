// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public abstract class BmsModLongNoteMode : Mod, IApplicableToBeatmap
    {
        public override ModType Type => ModType.Conversion;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModLongNoteMode) };

        public abstract BmsLongNoteMode LongNoteMode { get; }

        public void ApplyToBeatmap(IBeatmap beatmap)
            => LongNoteMode.ApplyToBeatmap(beatmap);
    }
}
