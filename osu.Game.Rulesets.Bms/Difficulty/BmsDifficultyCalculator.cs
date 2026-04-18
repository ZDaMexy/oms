// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Difficulty
{
    public class BmsDifficultyCalculator : DifficultyCalculator
    {
        public override int Version => 0;

        public BmsDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            return new DifficultyAttributes
            {
                Mods = mods,
                StarRating = resolveAuthorStarRating(beatmap)
                             ?? (WorkingBeatmap.BeatmapInfo != null && BmsStarRatingResolver.IsBmsBeatmap(WorkingBeatmap.BeatmapInfo)
                                 ? BmsStarRatingResolver.ResolveOrDefault(WorkingBeatmap.BeatmapInfo)
                                 : 0),
                MaxCombo = beatmap.HitObjects.OfType<BmsHitObject>().Count(hitObject => hitObject.CountsForScore),
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate) => Enumerable.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => Array.Empty<Skill>();

        /// <summary>
        /// Attempts to derive a star rating from the BMS chart author's <c>#PLAYLEVEL</c> header.
        /// Extracts the first numeric value from the string (e.g. "★12" → 12, "15" → 15, "sl3" → 3).
        /// Returns <c>null</c> if no valid number is found, or the value is non-positive.
        /// </summary>
        private static double? resolveAuthorStarRating(IBeatmap beatmap)
        {
            if (beatmap is not BmsBeatmap bmsBeatmap)
                return null;

            return BmsStarRatingResolver.TryParsePlayLevel(bmsBeatmap.BmsInfo.PlayLevel, out double level)
                ? level
                : null;
        }
    }
}
