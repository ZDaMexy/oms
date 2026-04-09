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

        private readonly double[] windows = new double[5];
        private readonly double[] longNoteReleaseWindows = new double[5];

        public override IReadOnlyList<double> Windows => windows;

        public override IReadOnlyList<double> LongNoteReleaseWindows => longNoteReleaseWindows;

        public Lr2JudgementSystem()
            => SetDifficulty(OsuOdJudgementSystem.MapRankToOverallDifficulty(2));

        public override void SetDifficulty(double overallDifficulty)
        {
            windows[0] = 18;
            windows[1] = 40;
            windows[2] = 90;
            windows[3] = 200;
            windows[4] = windows[3];

            SetLongNoteReleaseWindows(windows, longNoteReleaseWindows, release_miss_lenience);
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease)
            => EvaluateOffset(offsetMs, isLongNoteRelease ? LongNoteReleaseWindows : Windows);
    }
}
