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
        private const double stars_at_reference_density = 10;
        private const double max_star_rating = 20;

        private readonly BmsNoteDensityAnalyzer densityAnalyzer = new BmsNoteDensityAnalyzer();

        public override int Version => 20260402;

        public BmsDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            var densityAnalysis = densityAnalyzer.Analyze(beatmap);

            return new BmsDifficultyAttributes
            {
                Mods = mods,
                StarRating = calculateStarRating(beatmap, densityAnalysis.Percentile95DensityNps),
                MaxCombo = densityAnalysis.TotalNoteCount,
                TotalNoteCount = densityAnalysis.TotalNoteCount,
                ScratchNoteCount = densityAnalysis.ScratchNoteCount,
                LnNoteCount = densityAnalysis.LnNoteCount,
                PeakDensityNps = densityAnalysis.PeakDensityNps,
                PeakDensityMs = densityAnalysis.PeakDensityMs,
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate) => Enumerable.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => Array.Empty<Skill>();

        private static double calculateStarRating(IBeatmap beatmap, double percentileDensityNps)
        {
            if (percentileDensityNps <= 0)
                return 0;

            double referenceDensity = getReferenceDensity(resolveKeymode(beatmap));
            double starRating = stars_at_reference_density * Math.Sqrt(percentileDensityNps / referenceDensity);

            return Math.Clamp(starRating, 0, max_star_rating);
        }

        private static BmsKeymode resolveKeymode(IBeatmap beatmap)
        {
            if (beatmap is BmsBeatmap bmsBeatmap)
                return bmsBeatmap.BmsInfo.Keymode;

            int storedKeyCount = (int)Math.Round(beatmap.Difficulty.CircleSize);

            if (storedKeyCount > 0)
                return keyCountToKeymode(storedKeyCount);

            int laneCount = beatmap.HitObjects.OfType<BmsHitObject>()
                                  .Select(hitObject => hitObject.LaneIndex)
                                  .DefaultIfEmpty(-1)
                                  .Max() + 1;

            return keyCountToKeymode(laneCount switch
            {
                6 => 5,
                8 => 7,
                9 => 9,
                16 => 14,
                _ => 7,
            });
        }

        private static BmsKeymode keyCountToKeymode(int keyCount)
            => keyCount switch
            {
                5 => BmsKeymode.Key5K,
                9 => BmsKeymode.Key9K_Bms,
                14 => BmsKeymode.Key14K,
                _ => BmsKeymode.Key7K,
            };

        private static double getReferenceDensity(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => 13.5,
                BmsKeymode.Key7K => 16,
                BmsKeymode.Key9K_Bms => 18,
                BmsKeymode.Key9K_Pms => 17,
                BmsKeymode.Key14K => 27,
                _ => 16,
            };
    }
}
