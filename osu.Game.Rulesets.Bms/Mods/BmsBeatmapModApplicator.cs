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
            // Lane-rearrangement mods (Mirror / Random) are intentionally NOT applied here.
            // They are IApplicableToBeatmap and therefore applied exactly once by
            // WorkingBeatmap.GetPlayableBeatmap. This applicator runs again on the same playable
            // beatmap instance (from DrawableBmsRuleset's ctor and from BmsScoreProcessor.ApplyBeatmap),
            // and lane permutations COMPOSE under repeated application: re-applying them here would
            // corrupt RANDOM / custom patterns (P∘P∘P ≠ P) and silently cancel MIRROR on an even count.
            // The mods below are idempotent state-setters, and LongNote / Judge additionally carry the
            // default (no-mod) mode that must always be applied to the beatmap.
            BmsScoreProcessor.GetLongNoteMode(mods).ApplyToBeatmap(beatmap);
            BmsJudgeModeExtensions.GetJudgeMode(mods).ApplyToBeatmap(beatmap);
            mods?.OfType<BmsModAutoScratch>().LastOrDefault()?.ApplyToBeatmap(beatmap);
            mods?.OfType<BmsModAutoNote>().LastOrDefault()?.ApplyToBeatmap(beatmap);
        }
    }
}
