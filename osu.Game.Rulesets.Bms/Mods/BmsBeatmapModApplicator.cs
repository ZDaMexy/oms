// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    internal static class BmsBeatmapModApplicator
    {
        public static void ApplyToBeatmap(IBeatmap beatmap, IEnumerable<Mod>? mods)
        {
            mods?.OfType<BmsModMirror>().LastOrDefault()?.ApplyToBeatmap(beatmap);
            mods?.OfType<BmsModRandom>().LastOrDefault()?.ApplyToBeatmap(beatmap);
            BmsScoreProcessor.GetLongNoteMode(mods).ApplyToBeatmap(beatmap);
            BmsJudgeModeExtensions.GetJudgeMode(mods).ApplyToBeatmap(beatmap);
            mods?.OfType<BmsModAutoScratch>().LastOrDefault()?.ApplyToBeatmap(beatmap);
            mods?.OfType<BmsModAutoNote>().LastOrDefault()?.ApplyToBeatmap(beatmap);
        }
    }
}
