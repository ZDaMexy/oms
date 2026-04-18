// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsJudgeRank
    {
        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.JudgeRankVeryHard))]
        VeryHard = 0,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.JudgeRankHard))]
        Hard = 1,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.JudgeRankNormal))]
        Normal = 2,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.JudgeRankEasy))]
        Easy = 3,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.JudgeRankVeryEasy))]
        VeryEasy = 4,
    }

    public static class BmsJudgeRankExtensions
    {
        public static BmsJudgeRank FromHeaderValue(int rank)
            => rank switch
            {
                0 => BmsJudgeRank.VeryHard,
                1 => BmsJudgeRank.Hard,
                3 => BmsJudgeRank.Easy,
                4 => BmsJudgeRank.VeryEasy,
                _ => BmsJudgeRank.Normal,
            };

        public static BmsJudgeRank FromOverallDifficulty(double overallDifficulty)
            => FromHeaderValue(OsuOdJudgementSystem.MapOverallDifficultyToRank(overallDifficulty));

        public static BmsJudgeRank GetBeatmapJudgeRank(IBeatmapInfo beatmapInfo)
            => beatmapInfo.Metadata.GetChartMetadata()?.JudgeRank is int rank
                ? FromHeaderValue(rank)
                : FromOverallDifficulty(beatmapInfo.Difficulty.OverallDifficulty);

        public static BmsJudgeRank? GetJudgeRankOverride(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModJudgeRank>().LastOrDefault()?.JudgeRank.Value;

        public static BmsJudgeRank? GetJudgeRankOverride(ScoreInfo score)
            => GetJudgeRankOverride(score.Mods);

        public static int ToHeaderValue(this BmsJudgeRank judgeRank) => (int)judgeRank;

        public static float ToOverallDifficulty(this BmsJudgeRank judgeRank)
            => OsuOdJudgementSystem.MapRankToOverallDifficulty(judgeRank.ToHeaderValue());

        public static string GetDisplayName(this BmsJudgeRank judgeRank)
            => judgeRank switch
            {
                BmsJudgeRank.VeryHard => "VERY HARD",
                BmsJudgeRank.Hard => "HARD",
                BmsJudgeRank.Normal => "NORMAL",
                BmsJudgeRank.Easy => "EASY",
                BmsJudgeRank.VeryEasy => "VERY EASY",
                _ => "NORMAL",
            };

        public static string GetBucketToken(this BmsJudgeRank judgeRank)
            => judgeRank switch
            {
                BmsJudgeRank.VeryHard => "VERY_HARD",
                BmsJudgeRank.Hard => "HARD",
                BmsJudgeRank.Normal => "NORMAL",
                BmsJudgeRank.Easy => "EASY",
                BmsJudgeRank.VeryEasy => "VERY_EASY",
                _ => "NORMAL",
            };
    }
}
