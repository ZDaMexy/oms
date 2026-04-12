// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const double stars_at_reference_density = 5;
        private const double max_star_rating = 20;

        private readonly BmsNoteDensityAnalyzer densityAnalyzer = new BmsNoteDensityAnalyzer();

        public override int Version => 20260413;

        public BmsDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            var densityAnalysis = densityAnalyzer.Analyze(beatmap);
            double starRating = resolveAuthorStarRating(beatmap)
                                ?? calculateStarRating(beatmap, densityAnalysis.Percentile95DensityNps);

            return new BmsDifficultyAttributes
            {
                Mods = mods,
                StarRating = starRating,
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

        /// <summary>
        /// Attempts to derive a star rating from the BMS chart author's <c>#PLAYLEVEL</c> header.
        /// Extracts the first numeric value from the string (e.g. "★12" → 12, "15" → 15, "sl3" → 3).
        /// Returns <c>null</c> if no valid number is found, or the value is non-positive.
        /// </summary>
        private static double? resolveAuthorStarRating(IBeatmap beatmap)
        {
            if (beatmap is not BmsBeatmap bmsBeatmap)
                return null;

            string playLevel = bmsBeatmap.BmsInfo.PlayLevel;

            if (string.IsNullOrWhiteSpace(playLevel))
                return null;

            // Extract the first numeric value (integer or decimal) from the PlayLevel string.
            // Covers: "12", "★12", "☆3", "sl12", "Lv.7", "15.5", "Normal 8", etc.
            var match = Regex.Match(playLevel, @"(\d+(?:\.\d+)?)");

            if (!match.Success)
                return null;

            if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double level) || level <= 0)
                return null;

            return level;
        }

        private static double calculateStarRating(IBeatmap beatmap, double percentileDensityNps)
        {
            if (percentileDensityNps <= 0)
                return 0;

            double referenceDensity = getReferenceDensity(resolveKeymode(beatmap));
            double starRating = stars_at_reference_density * Math.Log2(1 + percentileDensityNps / referenceDensity);

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
