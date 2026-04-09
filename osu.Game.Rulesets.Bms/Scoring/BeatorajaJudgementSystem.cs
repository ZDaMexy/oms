// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class BeatorajaJudgementSystem : BmsJudgementSystem
    {
        private const double release_miss_lenience = 1.2;

        private readonly double[] windows = new double[5];
        private readonly double[] longNoteReleaseWindows = new double[5];

        public override IReadOnlyList<double> Windows => windows;

        public override IReadOnlyList<double> LongNoteReleaseWindows => longNoteReleaseWindows;

        public BeatorajaJudgementSystem()
            => SetDifficulty(OsuOdJudgementSystem.MapRankToOverallDifficulty(2));

        public override void SetDifficulty(double overallDifficulty)
        {
            double scale = getScale(overallDifficulty);

            windows[0] = 20 * scale;
            windows[1] = 40 * scale;
            windows[2] = 100 * scale;
            windows[3] = 200 * scale;
            windows[4] = windows[3];

            SetLongNoteReleaseWindows(windows, longNoteReleaseWindows, release_miss_lenience);
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease)
            => EvaluateOffset(offsetMs, isLongNoteRelease ? LongNoteReleaseWindows : Windows);

        private static double getScale(double overallDifficulty)
            => mapOverallDifficultyToRank(overallDifficulty) switch
            {
                0 => 0.25,
                1 => 0.5,
                2 => 0.75,
                _ => 1.0,
            };

        private static int mapOverallDifficultyToRank(double overallDifficulty)
            => Enumerable.Range(0, 5)
                         .OrderBy(rank => Math.Abs(OsuOdJudgementSystem.MapRankToOverallDifficulty(rank) - overallDifficulty))
                         .First();
    }
}
