// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModMirror : ModMirror, IApplicableToBeatmap
    {
        public override LocalisableString Description => BmsModStrings.MirrorDescription;

        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModMirror), typeof(BmsModRandom) };

        public void ApplyToBeatmap(IBeatmap beatmap)
            => BmsLaneRearrangement.ApplyMirror(beatmap);
    }
}
