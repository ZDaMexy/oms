// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModAutoplay : ModAutoplay
    {
        public override ModType Type => ModType.DifficultyReduction;

        public override LocalisableString Description => BmsModStrings.AutoplayDescription;

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(BmsModAutoScratch) }).ToArray();

        public override ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            if (beatmap is not BmsBeatmap bmsBeatmap)
                throw new ArgumentException("BMS autoplay requires a BMS beatmap.", nameof(beatmap));

            return new ModReplayData(new BmsAutoGenerator(bmsBeatmap).Generate(), new ModCreatedUser { Username = @"autoplay" });
        }
    }
}
