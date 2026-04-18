// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class Lr2JudgementSystem : BmsJudgementSystem
    {
        private const double release_miss_lenience = 1.2;
        private const double excessive_poor_early_window = 1000;
        private const double excessive_poor_late_window = 0;

        private readonly double[] windows = new double[5];
        private readonly double[] longNoteReleaseWindows = new double[5];

        public override IReadOnlyList<double> Windows => windows;

        public override IReadOnlyList<double> LongNoteReleaseWindows => longNoteReleaseWindows;

        public Lr2JudgementSystem()
            => SetDifficulty(BmsJudgeRank.Normal.ToOverallDifficulty());

        public override void SetDifficulty(double overallDifficulty)
        {
            switch (BmsJudgeRankExtensions.FromOverallDifficulty(overallDifficulty))
            {
                case BmsJudgeRank.VeryHard:
                    windows[0] = 8;
                    windows[1] = 24;
                    windows[2] = 40;
                    break;

                case BmsJudgeRank.Hard:
                    windows[0] = 15;
                    windows[1] = 30;
                    windows[2] = 60;
                    break;

                case BmsJudgeRank.Easy:
                case BmsJudgeRank.VeryEasy:
                    windows[0] = 21;
                    windows[1] = 60;
                    windows[2] = 120;
                    break;

                default:
                    windows[0] = 18;
                    windows[1] = 40;
                    windows[2] = 100;
                    break;
            }

            windows[3] = 200;
            windows[4] = windows[3];

            SetLongNoteReleaseWindows(windows, longNoteReleaseWindows, release_miss_lenience);
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false, bool isScratch = false)
            => EvaluateOffset(offsetMs, isLongNoteRelease ? LongNoteReleaseWindows : Windows);

        public override double? GetExcessivePoorEarlyWindow(bool isScratch = false)
            => excessive_poor_early_window;

        public override double? GetExcessivePoorLateWindow(bool isScratch = false)
            => excessive_poor_late_window;
    }
}
