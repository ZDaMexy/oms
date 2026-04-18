// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class OsuOdJudgementSystem : BmsJudgementSystem
    {
        private static readonly DifficultyRange perfect_window_range = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange bad_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange poor_window_range = new DifficultyRange(188, 173, 158);

        private readonly double[] windows = new double[5];
        private readonly double[] longNoteReleaseWindows = new double[5];

        public override IReadOnlyList<double> Windows => windows;

        public override IReadOnlyList<double> LongNoteReleaseWindows => longNoteReleaseWindows;

        public OsuOdJudgementSystem()
            => SetDifficulty(MapRankToOverallDifficulty(2));

        public override void SetDifficulty(double overallDifficulty)
        {
            windows[0] = calculateWindow(overallDifficulty, perfect_window_range);
            windows[1] = calculateWindow(overallDifficulty, great_window_range);
            windows[2] = calculateWindow(overallDifficulty, good_window_range);
            windows[3] = calculateWindow(overallDifficulty, bad_window_range);
            windows[4] = calculateWindow(overallDifficulty, poor_window_range);

            SetLongNoteReleaseWindows(windows, longNoteReleaseWindows, BmsHoldNote.DEFAULT_RELEASE_MISS_LENIENCE);
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false, bool isScratch = false)
            => EvaluateOffset(offsetMs, isLongNoteRelease ? LongNoteReleaseWindows : Windows);

        public static float MapRankToOverallDifficulty(int rank)
            => rank switch
            {
                0 => 9,
                1 => 8,
                2 => 7,
                3 => 5,
                4 => 3,
                _ => 7,
            };

        public static int MapOverallDifficultyToRank(double overallDifficulty)
            => Enumerable.Range(0, 5)
                         .OrderBy(rank => Math.Abs(MapRankToOverallDifficulty(rank) - overallDifficulty))
                         .First();

        public static string GetRankDisplayName(int rank)
            => rank switch
            {
                0 => "VERY HARD",
                1 => "HARD",
                2 => "NORMAL",
                3 => "EASY",
                4 => "VERY EASY",
                _ => "NORMAL",
            };

        public override double? GetExcessivePoorEarlyWindow(bool isScratch = false) => 500;

        public override double? GetExcessivePoorLateWindow(bool isScratch = false) => 0;

        private static double calculateWindow(double overallDifficulty, DifficultyRange range)
            => Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, range)) + 0.5;
    }
}
