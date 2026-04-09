// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsJudgeMode
    {
        OD,
        Beatoraja,
        LR2,
    }

    public static class BmsJudgeModeExtensions
    {
        public static string GetDisplayName(this BmsJudgeMode judgeMode)
            => judgeMode switch
            {
                BmsJudgeMode.OD => "OD",
                BmsJudgeMode.Beatoraja => "BEATORAJA",
                BmsJudgeMode.LR2 => "LR2",
                _ => judgeMode.ToString().ToUpperInvariant(),
            };

        public static BmsJudgementSystem CreateJudgementSystem(this BmsJudgeMode judgeMode)
            => judgeMode switch
            {
                BmsJudgeMode.Beatoraja => new BeatorajaJudgementSystem(),
                BmsJudgeMode.LR2 => new Lr2JudgementSystem(),
                _ => new OsuOdJudgementSystem(),
            };

        public static BmsJudgeMode GetJudgeMode(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModJudgeMode>().LastOrDefault()?.JudgeMode ?? BmsJudgeMode.OD;

        public static BmsJudgeMode GetJudgeMode(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.JudgeMode ?? GetJudgeMode(score.Mods);

        public static void ApplyToBeatmap(this BmsJudgeMode judgeMode, IBeatmap beatmap)
        {
            double overallDifficulty = beatmap.Difficulty.OverallDifficulty;

            foreach (var hitObject in beatmap.HitObjects)
                applyToHitObject(hitObject, judgeMode, overallDifficulty);
        }

        private static void applyToHitObject(HitObject hitObject, BmsJudgeMode judgeMode, double overallDifficulty)
        {
            if (hitObject is BmsHitObject bmsHitObject && bmsHitObject.HitWindows is BmsTimingWindows timingWindows)
            {
                timingWindows.JudgementSystem = judgeMode.CreateJudgementSystem();
                timingWindows.SetDifficulty(overallDifficulty);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                applyToHitObject(nested, judgeMode, overallDifficulty);
        }
    }
}
